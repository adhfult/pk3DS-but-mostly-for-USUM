using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.Structures;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.Core.Randomizers.Competitive;

public class CompetitiveBuildEngine
{
    private readonly GameConfig Config;
    private readonly LegalLearnsetAggregator LearnsetAggregator;
    private readonly Random Rnd = Util.Rand;

    // Stat index constants for PersonalInfo.Stats: [HP, ATK, DEF, SPE, SPA, SPD]
    private const int HP = CompetitiveDatabase.HP;
    private const int ATK = CompetitiveDatabase.ATK;
    private const int DEF = CompetitiveDatabase.DEF;
    private const int SPE = CompetitiveDatabase.SPE;
    private const int SPA = CompetitiveDatabase.SPA;
    private const int SPD = CompetitiveDatabase.SPD;

    // EV index constants for TrainerPoke7.EVs: [HP, ATK, DEF, SPA, SPD, SPE]
    private const int EV_HP = 0, EV_ATK = 1, EV_DEF = 2, EV_SPA = 3, EV_SPD = 4, EV_SPE = 5;

    private static byte MakeNature(int plusStat, int minusStat)
    {
        // plusStat/minusStat: 1=Atk, 2=Def, 3=Spe, 4=SpA, 5=SpD
        if (plusStat < 1 || plusStat > 5 || minusStat < 1 || minusStat > 5 || plusStat == minusStat)
            return 0; // Hardy (neutral)
        return (byte)((plusStat - 1) * 5 + (minusStat - 1));
    }

    // Named nature constants
    private static readonly byte Adamant  = MakeNature(1, 4); // +Atk, -SpA
    private static readonly byte Brave    = MakeNature(1, 3); // +Atk, -Spe
    private static readonly byte Bold     = MakeNature(2, 1); // +Def, -Atk
    private static readonly byte Impish   = MakeNature(2, 4); // +Def, -SpA
    private static readonly byte Jolly    = MakeNature(3, 4); // +Spe, -SpA
    private static readonly byte Timid    = MakeNature(3, 1); // +Spe, -Atk
    private static readonly byte Hasty    = MakeNature(3, 2); // +Spe, -Def
    private static readonly byte Naive    = MakeNature(3, 5); // +Spe, -SpD
    private static readonly byte Modest   = MakeNature(4, 1); // +SpA, -Atk
    private static readonly byte Quiet    = MakeNature(4, 3); // +SpA, -Spe
    private static readonly byte Calm     = MakeNature(5, 1); // +SpD, -Atk
    private static readonly byte Careful  = MakeNature(5, 4); // +SpD, -SpA

    public CompetitiveBuildEngine(GameConfig config)
    {
        Config = config;
        LearnsetAggregator = new LegalLearnsetAggregator(config);
    }

    public LegalLearnsetAggregator Learnsets => LearnsetAggregator;

    /// <summary>
    /// Resolves a trainer Pokemon's ability *slot index* (0-3, matching TrainerPoke7.Ability)
    /// to the actual ability name via the species' PersonalInfo.Abilities array.
    /// </summary>
    public static string ResolveAbilityName(PersonalInfo pi, int abilitySlot, string[] abilityNames)
    {
        if (pi?.Abilities == null || pi.Abilities.Length == 0 || abilityNames == null) return "";
        int arrIdx = abilitySlot switch
        {
            1 => 0,
            2 => pi.Abilities.Length > 1 ? 1 : 0,
            3 => pi.Abilities.Length > 2 ? 2 : 0,
            _ => 0, // "Any" (0) — resolve to Ability 1 for naming purposes; the caller should
                    // have already committed to a concrete slot before this is used for building.
        };
        if (arrIdx >= pi.Abilities.Length) arrIdx = 0;
        int abilId = pi.Abilities[arrIdx];
        return (abilId > 0 && abilId < abilityNames.Length) ? (abilityNames[abilId] ?? "") : "";
    }

    /// <summary>
    /// Picks the best ability slot (1, 2, or 3=Hidden — all fully eligible) for a competitive
    /// trainer Pokemon, preferring higher-tier competitive abilities. Returns a concrete slot
    /// (never 0/"Any") so downstream moveset/item logic has a definite ability to build around.
    /// </summary>
    /// <summary>
    /// The user's ability bans, honoured when picking a trainer Pokemon's ability slot.
    /// </summary>
    public AbilityBanList BanList { get; set; }

    public int ChooseCompetitiveAbilitySlot(PersonalInfo pi, string[] abilityNames)
    {
        if (pi?.Abilities == null || pi.Abilities.Length == 0 || abilityNames == null) return 1;

        var candidates = new List<(int slot, string name)>();
        for (int i = 0; i < pi.Abilities.Length && i < 3; i++)
        {
            int abilId = pi.Abilities[i];
            if (abilId <= 0 || abilId >= abilityNames.Length) continue;
            string name = abilityNames[abilId];
            if (string.IsNullOrEmpty(name)) continue;
            candidates.Add((i + 1, name));
        }
        if (candidates.Count == 0) return 1;

        // A species whose every ability is banned keeps its options rather than losing one, since
        // it has to end up with something.
        if (BanList != null)
        {
            var allowed = candidates.Where(c => !BanList.IsBanned(c.name)).ToList();
            if (allowed.Count > 0) candidates = allowed;
        }

        int Tier(string name)
        {
            if (CompetitiveDatabase.CompetitiveAbilities.Contains(name)) return 3;
            if (CompetitiveDatabase.SituationalAbilities.Contains(name)) return 2;
            if (CompetitiveDatabase.LessCompetitiveAbilities.Contains(name)) return 1;
            return 0;
        }

        int bestTier = candidates.Max(c => Tier(c.name));
        var bestCandidates = candidates.Where(c => Tier(c.name) == bestTier).ToList();
        return bestCandidates[Rnd.Next(bestCandidates.Count)].slot;
    }

    /// <summary>How many of a finished set's moves are physical, and how many special.</summary>
    private (int Physical, int Special) CountAttackCategories(int[] moves)
    {
        var data = Config?.Moves;
        if (moves == null || data == null) return (0, 0);

        int phys = 0, spec = 0;
        foreach (int m in moves)
        {
            if (m <= 0 || m >= data.Length) continue;
            if (data[m].Category == 1) phys++;
            else if (data[m].Category == 2) spec++;
        }
        return (phys, spec);
    }

    public (int[] evs, byte nature) BuildEVsAndNature(PersonalInfo pi, CompetitiveRole role, int[] moves, bool isTrickRoom = false)
    {
        int[] evs = new int[6]; // EV_HP, EV_ATK, EV_DEF, EV_SPA, EV_SPD, EV_SPE
        byte nature;

        int hp  = pi.Stats[HP];
        int atk = pi.Stats[ATK];
        int def = pi.Stats[DEF];
        int spe = pi.Stats[SPE];
        int spa = pi.Stats[SPA];
        int spd = pi.Stats[SPD];

        var (physCount, specCount) = CountAttackCategories(moves);
        bool physicalSet = physCount != specCount ? physCount > specCount : atk >= spa;

        // Used where a branch needs "how lopsided is this", which stays a stat question.
        int strongOffense = physicalSet ? atk : spa;
        int weakOffense = physicalSet ? spa : atk;

        // Trick Room: 0 Spe EVs, Speed-hindering nature, 252 HP + 252 better offense + 4 other
        if (isTrickRoom)
        {
            evs[EV_HP] = 252;
            if (physicalSet)
            {
                evs[EV_ATK] = 252; evs[EV_DEF] = 4;
                nature = Brave; // +Atk, -Spe
            }
            else
            {
                evs[EV_SPA] = 252; evs[EV_DEF] = 4;
                nature = Quiet; // +SpA, -Spe
            }
            return (evs, nature);
        }

        switch (role)
        {
            // --- FAST OFFENSIVE (Manifesto lines 735-738) ---
            case CompetitiveRole.OffensiveSweeper:
            case CompetitiveRole.OffensiveBreaker:
            case CompetitiveRole.OffensiveCleaner:
            case CompetitiveRole.SpeedControl:
                // "Mixed" only when the set really is mixed. Close base stats alone are not
                // enough - a 139/139 Pokemon running four physical moves wants nothing in Sp. Atk.
                bool mixedSet = physCount > 0 && specCount > 0 && Math.Abs(atk - spa) <= 15;
                if (mixedSet)
                {
                    if (physicalSet)
                    {
                        evs[EV_ATK] = 252; evs[EV_SPE] = 252; evs[EV_SPA] = 4;
                    }
                    else
                    {
                        evs[EV_SPA] = 252; evs[EV_SPE] = 252; evs[EV_ATK] = 4;
                    }
                    nature = (def <= spd) ? Hasty : Naive;
                }
                else if (physicalSet)
                {
                    evs[EV_ATK] = 252; evs[EV_SPE] = 252;
                    Assign4EVRemainder(evs, hp, def, spd);
                    nature = (spe >= atk) ? Jolly : Adamant;
                }
                else
                {
                    evs[EV_SPA] = 252; evs[EV_SPE] = 252;
                    Assign4EVRemainder(evs, hp, def, spd);
                    nature = (spe >= spa) ? Timid : Modest;
                }
                break;

            // --- OFFENSIVE PIVOT ---
            case CompetitiveRole.OffensivePivot:
                if (physicalSet)
                {
                    evs[EV_ATK] = 252; evs[EV_SPE] = 252;
                    Assign4EVRemainder(evs, hp, def, spd);
                    nature = Jolly;
                }
                else
                {
                    evs[EV_SPA] = 252; evs[EV_SPE] = 252;
                    Assign4EVRemainder(evs, hp, def, spd);
                    nature = Timid;
                }
                break;

            // --- OFFENSIVE UTILITY ---
            case CompetitiveRole.OffensiveUtility:
                if (physicalSet)
                {
                    evs[EV_ATK] = 252; evs[EV_SPE] = 252;
                    Assign4EVRemainder(evs, hp, def, spd);
                    nature = Jolly;
                }
                else
                {
                    evs[EV_SPA] = 252; evs[EV_SPE] = 252;
                    Assign4EVRemainder(evs, hp, def, spd);
                    nature = Timid;
                }
                break;

            // --- LEADS (Manifesto lines 738-740) ---
            case CompetitiveRole.OffensiveLead:
                if (atk >= 90 || spa >= 90) // Offensive lead
                {
                    if (physicalSet)
                    {
                        evs[EV_ATK] = 252; evs[EV_SPE] = 252;
                        Assign4EVRemainder(evs, hp, def, spd);
                        nature = Jolly;
                    }
                    else
                    {
                        evs[EV_SPA] = 252; evs[EV_SPE] = 252;
                        Assign4EVRemainder(evs, hp, def, spd);
                        nature = Timid;
                    }
                }
                else // Screen/Hazard lead: 252 HP / 252 Spe / 4 lower def
                {
                    evs[EV_HP] = 252; evs[EV_SPE] = 252;
                    evs[def <= spd ? EV_DEF : EV_SPD] = 4;
                    nature = (physicalSet) ? Jolly : Timid;
                }
                break;

            case CompetitiveRole.BulkyLead:
                // Bulky lead: 252 HP / 252 Spe / 4 lower def
                evs[EV_HP] = 252; evs[EV_SPE] = 252;
                evs[def <= spd ? EV_DEF : EV_SPD] = 4;
                nature = (physicalSet) ? Jolly : Timid;
                break;

            // --- TANKY / BULKY OFFENSIVE (Manifesto lines 743-745) ---
            case CompetitiveRole.TankySweeper:
            case CompetitiveRole.TankyCleaner:
            case CompetitiveRole.BulkyAttacker:
                evs[EV_HP] = 252;
                if (physicalSet)
                {
                    evs[EV_ATK] = 252; evs[EV_SPE] = 4;
                    nature = Adamant;
                }
                else
                {
                    evs[EV_SPA] = 252; evs[EV_SPE] = 4;
                    nature = Modest;
                }
                break;

            case CompetitiveRole.BulkySweeper:
                evs[EV_HP] = 252;
                if (physicalSet)
                {
                    evs[EV_SPE] = 252; evs[EV_ATK] = 4;
                    nature = Jolly;
                }
                else
                {
                    evs[EV_SPE] = 252; evs[EV_SPA] = 4;
                    nature = Timid;
                }
                break;

            // --- BULKY PIVOT / UTILITY ---
            case CompetitiveRole.BulkyPivot:
            case CompetitiveRole.BulkyUtility:
                evs[EV_HP] = 252;
                if (def >= spd) // Physically defensive pivot
                {
                    evs[EV_DEF] = 252;
                    evs[spd >= spe ? EV_SPD : EV_SPE] = 4;
                    nature = (physicalSet) ? Impish : Bold;
                }
                else // Specially defensive pivot
                {
                    evs[EV_SPD] = 252;
                    evs[def >= spe ? EV_DEF : EV_SPE] = 4;
                    nature = (physicalSet) ? Careful : Calm;
                }
                break;

            // --- DEFENSIVE (Manifesto lines 748-750) ---
            case CompetitiveRole.DefensiveWall:
            case CompetitiveRole.DefensivePivot:
                evs[EV_HP] = 252;
                if (def >= spd) // Physically Defensive
                {
                    evs[EV_DEF] = 252;
                    evs[spd >= spe ? EV_SPD : EV_SPE] = 4;
                    nature = (physicalSet) ? Impish : Bold;
                }
                else // Specially Defensive
                {
                    evs[EV_SPD] = 252;
                    evs[def >= spe ? EV_DEF : EV_SPE] = 4;
                    nature = (physicalSet) ? Careful : Calm;
                }
                break;

            default:
                evs[EV_HP] = 252;
                if (physicalSet) { evs[EV_ATK] = 252; evs[EV_SPE] = 4; nature = Adamant; }
                else { evs[EV_SPA] = 252; evs[EV_SPE] = 4; nature = Modest; }
                break;
        }

        return (evs, nature);
    }

    // Places 4 remaining EVs: HP if odd, else lower defense stat
    private static void Assign4EVRemainder(int[] evs, int hp, int def, int spd)
    {
        if (hp % 2 == 1) evs[EV_HP] = 4;
        else if (def <= spd) evs[EV_DEF] = 4;
        else evs[EV_SPD] = 4;
    }

    public ushort AssignItem(
        int species, PersonalInfo pi, string abilityName, int[] moves, CompetitiveRole role,
        string[] itemNames, string[] moveNames, Move[] moveData,
        ref bool assignedMega, ref bool assignedZCrystal,
        TeamArchetype archetype = TeamArchetype.Balance,
        List<string> teamAbilities = null)
    {
        ushort GetItemId(string name)
        {
            if (itemNames == null || itemNames.Length == 0 || string.IsNullOrEmpty(name)) return 0;
            int idx = Array.FindIndex(itemNames, z => z.Equals(name, StringComparison.OrdinalIgnoreCase));
            return idx > 0 ? (ushort)idx : (ushort)0;
        }

        bool HasItem(string name) => GetItemId(name) > 0;

        ushort ReturnItem(string primary, string fallback = "Leftovers")
        {
            ushort id = GetItemId(primary);
            if (id > 0) return id;
            ushort fallbackId = GetItemId(fallback);
            return fallbackId > 0 ? fallbackId : GetItemId("Sitrus Berry");
        }

        var currentMoveNames = ResolveMoveNames(moves, moveNames);
        int atk = pi.Stats[ATK], spa = pi.Stats[SPA], spe = pi.Stats[SPE];
        int def = pi.Stats[DEF], spd = pi.Stats[SPD], hp = pi.Stats[HP];
        bool isPhysical = atk >= spa;
        int physCount = 0, specCount = 0, statusCount = 0;
        if (moveData != null && moves != null)
        {
            foreach (int m in moves)
            {
                if (m <= 0 || m >= moveData.Length) continue;
                switch (moveData[m].Category)
                {
                    case 0: statusCount++; break;
                    case 1: physCount++; break;
                    case 2: specCount++; break;
                }
            }
        }

        // === 0. MEGA STONE (max 1 per team) ===
        if (!assignedMega && species > 0)
        {
            ushort megaItem = TryAssignMegaStone(species, itemNames, ref assignedMega);
            if (megaItem > 0) return megaItem;
        }

        // === 0b. Z-CRYSTAL (max 1 per team, Gen 7) ===
        if (!assignedZCrystal && species > 0 && Config != null && Config.Generation == 7 && IsOffensiveRole(role))
        {
            ushort zItem = TryAssignZCrystal(species, pi, moves, itemNames, moveNames, moveData, ref assignedZCrystal);
            if (zItem > 0) return zItem;
        }

        // === 1. STRICT ABILITY-SPECIFIC ITEMS ===

        // Poison Heal / Toxic Boost -> Toxic Orb
        if (abilityName.Equals("Poison Heal", StringComparison.OrdinalIgnoreCase) ||
            abilityName.Equals("Toxic Boost", StringComparison.OrdinalIgnoreCase))
            return ReturnItem("Toxic Orb");

        // Guts / Flare Boost -> Flame Orb
        if (abilityName.Equals("Guts", StringComparison.OrdinalIgnoreCase) ||
            abilityName.Equals("Flare Boost", StringComparison.OrdinalIgnoreCase))
            return ReturnItem("Flame Orb");

        // Anger Shell -> Focus Sash
        if (abilityName.Equals("Anger Shell", StringComparison.OrdinalIgnoreCase))
            return ReturnItem("Focus Sash");

        // Super Luck -> Scope Lens
        if (abilityName.Equals("Super Luck", StringComparison.OrdinalIgnoreCase))
            return ReturnItem("Scope Lens");

        // Gorilla Tactics -> Choice Band or Choice Scarf
        if (abilityName.Equals("Gorilla Tactics", StringComparison.OrdinalIgnoreCase))
        {
            if (atk + spe > 160) return ReturnItem("Choice Scarf");
            return ReturnItem(spe > atk ? "Choice Scarf" : "Choice Band");
        }

        // Protosynthesis / Quark Drive -> Booster Energy (prioritized for offensive)
        if (abilityName.Equals("Protosynthesis", StringComparison.OrdinalIgnoreCase) ||
            abilityName.Equals("Quark Drive", StringComparison.OrdinalIgnoreCase))
        {
            if (IsOffensiveRole(role)) return ReturnItem("Booster Energy", "Life Orb");
        }

        // Harvest -> Sitrus Berry (defensive) or Starf Berry (offensive)
        if (abilityName.Equals("Harvest", StringComparison.OrdinalIgnoreCase))
        {
            bool isSunTeam = archetype == TeamArchetype.Sun;
            if (IsDefensiveRole(role) || !IsOffensiveRole(role))
                return ReturnItem("Sitrus Berry");
            return ReturnItem("Starf Berry", "Sitrus Berry");
        }

        // Magic Guard + offensive -> Focus Sash (frail) or Life Orb
        if (abilityName.Equals("Magic Guard", StringComparison.OrdinalIgnoreCase) && IsOffensiveRole(role))
            return ReturnItem(def + spd < 140 ? "Focus Sash" : "Life Orb");

        // Moody -> Leftovers (manifesto: Substitute + Protect moveset)
        if (abilityName.Equals("Moody", StringComparison.OrdinalIgnoreCase))
            return ReturnItem("Leftovers");

        // === 2. SCREEN / AURORA VEIL + LIGHT CLAY ===
        bool hasDualScreens = currentMoveNames.Contains("Light Screen") && currentMoveNames.Contains("Reflect");
        bool hasSnowVeil = abilityName.Equals("Snow Warning", StringComparison.OrdinalIgnoreCase) && currentMoveNames.Contains("Aurora Veil");
        if (hasDualScreens || hasSnowVeil)
            return ReturnItem("Light Clay");

        // Snow Warning without Aurora Veil -> Icy Rock (weather teams)
        if (abilityName.Equals("Snow Warning", StringComparison.OrdinalIgnoreCase) && archetype == TeamArchetype.Snow)
            return ReturnItem("Icy Rock");

        // Drought -> Heat Rock (Sun team lead)
        if (abilityName.Equals("Drought", StringComparison.OrdinalIgnoreCase) && archetype == TeamArchetype.Sun)
            return ReturnItem("Heat Rock");

        // Drizzle -> Damp Rock (Rain team lead)
        if (abilityName.Equals("Drizzle", StringComparison.OrdinalIgnoreCase) && archetype == TeamArchetype.Rain)
            return ReturnItem("Damp Rock");

        // Electric/Grassy/Psychic/Misty Surge lead -> Terrain Extender (extends any terrain)
        if ((abilityName.Equals("Electric Surge", StringComparison.OrdinalIgnoreCase) ||
             abilityName.Equals("Grassy Surge", StringComparison.OrdinalIgnoreCase) ||
             abilityName.Equals("Psychic Surge", StringComparison.OrdinalIgnoreCase) ||
             abilityName.Equals("Misty Surge", StringComparison.OrdinalIgnoreCase)) &&
            archetype == TeamArchetype.TerrainTeam && role == CompetitiveRole.BulkyLead)
            return ReturnItem("Terrain Extender", "Heavy-Duty Boots");

        // === 3. UNBURDEN -> consumable items ===
        if (abilityName.Equals("Unburden", StringComparison.OrdinalIgnoreCase))
        {
            // Terrain seed if teammate has Surge
            if (teamAbilities != null)
            {
                if (teamAbilities.Any(a => a.Equals("Electric Surge", StringComparison.OrdinalIgnoreCase)))
                    return ReturnItem("Electric Seed", "Air Balloon");
                if (teamAbilities.Any(a => a.Equals("Grassy Surge", StringComparison.OrdinalIgnoreCase)))
                    return ReturnItem("Grassy Seed", "Air Balloon");
                if (teamAbilities.Any(a => a.Equals("Psychic Surge", StringComparison.OrdinalIgnoreCase)))
                    return ReturnItem("Psychic Seed", "Air Balloon");
                if (teamAbilities.Any(a => a.Equals("Misty Surge", StringComparison.OrdinalIgnoreCase)))
                    return ReturnItem("Misty Seed", "Air Balloon");
            }
            // Fallback consumable
            if (def + spd < 140) return ReturnItem("Focus Sash");
            if (currentMoveNames.Overlaps(CompetitiveDatabase.StatDropMoves)) return ReturnItem("White Herb");
            if (currentMoveNames.Overlaps(CompetitiveDatabase.TwoTurnMoves)) return ReturnItem("Power Herb");
            return ReturnItem("Air Balloon");
        }

        // === 4. MOVE-SPECIFIC ITEMS ===

        // Loaded Dice
        if (currentMoveNames.Overlaps(CompetitiveDatabase.MultiHitMoves) && HasItem("Loaded Dice"))
            return ReturnItem("Loaded Dice");

        // Power Herb
        if (currentMoveNames.Overlaps(CompetitiveDatabase.TwoTurnMoves))
            return ReturnItem("Power Herb");

        // === 5. MEGA STONE (max 1 per team) ===
        if (!assignedMega && pi is PersonalInfo megaPi)
        {
            // Check if this species has a Mega Stone in the game
            // Species ID isn't directly available from PersonalInfo; handled by caller
        }

        // === 6. ROLE-BASED ITEMS ===
        switch (role)
        {
            case CompetitiveRole.OffensiveLead:
                return ReturnItem(def + spd < 140 ? "Focus Sash" : "Heavy-Duty Boots");

            case CompetitiveRole.BulkyLead:
                if (currentMoveNames.Overlaps(CompetitiveDatabase.HazardMoves))
                    return ReturnItem("Mental Herb");
                return ReturnItem("Heavy-Duty Boots");

            case CompetitiveRole.OffensiveSweeper:
            case CompetitiveRole.OffensiveBreaker:
                if (statusCount == 0 && physCount + specCount == 4)
                {
                    // All attacks: Assault Vest viable for special bulk, or offensive item
                    if (isPhysical) return ReturnItem("Choice Band");
                    return ReturnItem("Life Orb");
                }
                if (currentMoveNames.Overlaps(CompetitiveDatabase.SetupMoves))
                {
                    // Setup sweeper: Lum Berry or Life Orb
                    if (Rnd.Next(100) < 40) return ReturnItem("Lum Berry");
                    return ReturnItem("Life Orb");
                }
                return ReturnItem("Life Orb");

            case CompetitiveRole.OffensiveCleaner:
                return ReturnItem("Life Orb");

            case CompetitiveRole.SpeedControl:
                return ReturnItem("Choice Scarf");

            case CompetitiveRole.OffensivePivot:
                if (physCount + specCount >= 3) return ReturnItem("Expert Belt");
                return ReturnItem("Heavy-Duty Boots");

            case CompetitiveRole.OffensiveUtility:
                if (physCount + specCount >= 3) return ReturnItem("Life Orb");
                return ReturnItem("Heavy-Duty Boots");

            case CompetitiveRole.TankySweeper:
            case CompetitiveRole.TankyCleaner:
                if (currentMoveNames.Overlaps(CompetitiveDatabase.SetupMoves))
                    return ReturnItem("Leftovers");
                return ReturnItem(isPhysical ? "Choice Band" : "Choice Specs");

            case CompetitiveRole.BulkyAttacker:
                if (statusCount == 0) return ReturnItem("Assault Vest");
                return ReturnItem("Leftovers");

            case CompetitiveRole.BulkySweeper:
                return ReturnItem("Leftovers");

            case CompetitiveRole.BulkyPivot:
            case CompetitiveRole.BulkyUtility:
                // Check for Stealth Rock weakness -> Heavy-Duty Boots
                if (Rnd.Next(100) < 30) return ReturnItem("Heavy-Duty Boots");
                return ReturnItem("Leftovers");

            case CompetitiveRole.DefensiveWall:
            case CompetitiveRole.DefensivePivot:
                if (hp + def > 160 && Rnd.Next(100) < 40)
                    return ReturnItem("Rocky Helmet");
                return ReturnItem("Leftovers");

            default:
                return ReturnItem(hp + def > 150 ? "Leftovers" : "Life Orb");
        }
    }

    // Mega Stone assignment as a separate pass (called by the team builder)
    public ushort TryAssignMegaStone(int species, string[] itemNames, ref bool assignedMega)
    {
        if (assignedMega) return 0;
        if (!CompetitiveDatabase.MegaStoneMap.TryGetValue(species, out var stones)) return 0;

        ushort GetItemId(string name)
        {
            if (itemNames == null || itemNames.Length == 0) return 0;
            int idx = Array.FindIndex(itemNames, z => z.Equals(name, StringComparison.OrdinalIgnoreCase));
            return idx > 0 ? (ushort)idx : (ushort)0;
        }

        // Pick a random valid Mega Stone for this species
        foreach (var stone in stones)
        {
            ushort id = GetItemId(stone);
            if (id > 0)
            {
                assignedMega = true;
                return id;
            }
        }
        return 0;
    }

    // Z-Crystal assignment as a separate pass
    public ushort TryAssignZCrystal(int species, PersonalInfo pi, int[] moves, string[] itemNames, string[] moveNames, Move[] moveData, ref bool assignedZCrystal)
    {
        if (assignedZCrystal) return 0;

        ushort GetItemId(string name)
        {
            if (itemNames == null || itemNames.Length == 0) return 0;
            int idx = Array.FindIndex(itemNames, z => z.Equals(name, StringComparison.OrdinalIgnoreCase));
            return idx > 0 ? (ushort)idx : (ushort)0;
        }

        // Species-specific Z-Crystals first
        if (CompetitiveDatabase.SpeciesZCrystals.TryGetValue(species, out var specZ))
        {
            ushort zId = GetItemId(specZ.Crystal);
            if (zId > 0)
            {
                assignedZCrystal = true;
                return zId;
            }
        }

        if (pi != null && moves != null && moveData != null && moveNames != null)
        {
            bool isPhysical = pi.Stats[ATK] >= pi.Stats[SPA];
            int bestPower = 0;
            int bestType = -1;
            foreach (int m in moves)
            {
                if (m <= 0 || m >= moveData.Length) continue;
                var md = moveData[m];
                if (md.Category == 0) continue; // Skip status
                bool matchesCat = (isPhysical && md.Category == 1) || (!isPhysical && md.Category == 2);
                if (!matchesCat) continue;
                int moveType = md.Type;
                bool isSTAB = Array.IndexOf(pi.Types, moveType) >= 0;
                if (isSTAB && md.Power >= 80 && md.Power > bestPower)
                {
                    bestPower = md.Power;
                    bestType = moveType;
                }
            }
            if (bestType >= 0 && CompetitiveDatabase.TypeZCrystals.TryGetValue(bestType, out var crystalName))
            {
                ushort zId = GetItemId(crystalName);
                if (zId > 0)
                {
                    assignedZCrystal = true;
                    return zId;
                }
            }
        }

        return 0;
    }

    public int[] BuildCompetitiveMoveset(
        int species, CompetitiveRole role, PersonalInfo pi,
        MoveRandomizer fallbackMoveRand, string abilityName = "",
        TeamArchetype archetype = TeamArchetype.Balance,
        Move[] moveData = null, List<string> teamAbilities = null)
    {
        var legal = LearnsetAggregator.GetLegalMoves(species);
        if (legal.Count < 4)
        {
            var broadMoveNames = Config?.GetText(TextName.MoveNames);
            var broadMoveData = moveData ?? Config?.Moves;
            if (Config != null && broadMoveNames != null && broadMoveData != null)
            {
                legal = Enumerable.Range(1, Math.Min(Config.Info.MaxMoveID, Math.Min(broadMoveNames.Length, broadMoveData.Length) - 1))
                    .Where(m => !string.IsNullOrEmpty(broadMoveNames[m]) && broadMoveNames[m] != "—" && broadMoveNames[m] != "———" &&
                                !Legal.Z_Moves.Contains(m) && broadMoveData[m].PP != 1)
                    .ToHashSet();
            }
            if (legal.Count < 4)
                return fallbackMoveRand?.GetRandomMoveset(species) ?? new int[4];
        }

        string[] moveNames = Config?.GetText(TextName.MoveNames) ?? Array.Empty<string>();
        if (moveData == null) moveData = Config?.Moves;
        int[] types = pi?.Types ?? new int[0];
        int spa = pi != null ? pi.Stats[SPA] : 0;

        var pool = legal.ToList();
        var selected = new List<int>();
        bool isPhysicalAttacker = pi != null && pi.Stats[ATK] >= spa;

        // Same rule the STAB and coverage phases use: once one attacking stat clearly dominates, a
        // move on the weaker side is not worth a slot whatever else it does for the role.
        int strongStat = pi != null ? Math.Max(pi.Stats[ATK], spa) : 0;
        int weakStat = pi != null ? Math.Min(pi.Stats[ATK], spa) : 0;
        bool statGapDecisive = pi != null && weakStat * 4 < strongStat * 3;

        // Helper: Try to add moves from a named category, prioritizing STAB
        void TryAddCategory(HashSet<string> categorySet, int maxAdd = 1, bool preferSTAB = false)
        {
            if (moveNames.Length == 0) return;

            // Sort pool: STAB first if preferred
            var candidates = new List<(int id, bool isStab, bool matchesCat, int power)>();
            foreach (int moveId in pool)
            {
                if (moveId <= 0 || moveId >= moveNames.Length) continue;
                string name = moveNames[moveId];
                if (string.IsNullOrEmpty(name) || !categorySet.Contains(name)) continue;
                if (selected.Contains(moveId)) continue;

                bool isStab = false;
                bool matchesCat = true; // status moves (Category == 0) have no phys/spec mismatch to worry about
                int power = 0;
                if (moveId < (moveData?.Length ?? 0))
                {
                    var md = moveData[moveId];
                    isStab = Array.IndexOf(types, md.Type) >= 0;
                    power = md.Power;
                    if (md.Category != 0)
                        matchesCat = (isPhysicalAttacker && md.Category == 1) || (!isPhysicalAttacker && md.Category == 2);
                }

                if (statGapDecisive && !matchesCat) continue;

                candidates.Add((moveId, isStab, matchesCat, power));
            }

            // Sort: STAB first (if preferred), then category-match, then by power descending
            if (preferSTAB)
                candidates.Sort((a, b) => a.isStab != b.isStab ? (b.isStab ? 1 : -1) :
                                           a.matchesCat != b.matchesCat ? (b.matchesCat ? 1 : -1) :
                                           b.power.CompareTo(a.power));
            else
                candidates.Sort((a, b) => a.matchesCat != b.matchesCat ? (b.matchesCat ? 1 : -1) : b.power.CompareTo(a.power));

            int count = 0;
            foreach (var (id, _, _, _) in candidates)
            {
                if (selected.Count >= 4 || count >= maxAdd) break;
                selected.Add(id);
                pool.Remove(id);
                count++;
            }
        }

        // --- Phase 1: ABILITY-SPECIFIC MOVE REQUIREMENTS (Manifesto lines 75-122) ---
        if (!string.IsNullOrEmpty(abilityName))
        {
            ApplyAbilityMoveRequirements(abilityName, selected, pool, moveNames, moveData, types, role, archetype, pi);
        }

        // --- Phase 2: ROLE-SPECIFIC MOVES ---
        switch (role)
        {
            case CompetitiveRole.OffensiveLead:
                TryAddCategory(CompetitiveDatabase.HazardMoves, 1);
                if (selected.Count < 4) TryAddCategory(CompetitiveDatabase.PivotMoves, 1);
                break;

            case CompetitiveRole.BulkyLead:
                TryAddCategory(CompetitiveDatabase.HazardMoves, 1);
                if (selected.Count < 4) TryAddCategory(CompetitiveDatabase.PivotMoves, 1);
                break;

            case CompetitiveRole.OffensiveSweeper:
            case CompetitiveRole.BulkySweeper:
            case CompetitiveRole.TankySweeper:
                if (pi.Stats[ATK] >= spa)
                    TryAddCategory(CompetitiveDatabase.AtkSetupMoves, 1);
                else
                    TryAddCategory(CompetitiveDatabase.SpaSetupMoves, 1);
                break;

            case CompetitiveRole.OffensivePivot:
            case CompetitiveRole.BulkyPivot:
                TryAddCategory(CompetitiveDatabase.PivotMoves, 1, true);
                break;

            case CompetitiveRole.DefensiveWall:
            case CompetitiveRole.DefensivePivot:
                TryAddCategory(CompetitiveDatabase.RecoveryMoves, 1);
                if (role == CompetitiveRole.DefensivePivot)
                    TryAddCategory(CompetitiveDatabase.PivotMoves, 1);
                TryAddCategory(CompetitiveDatabase.HazardMoves, 1);
                if (selected.Count < 4 && Rnd.Next(100) < 50)
                    TryAddCategory(CompetitiveDatabase.UtilityStatusMoves, 1);
                break;

            case CompetitiveRole.OffensiveBreaker:
                // Breakers just want attacks; no special role moves
                break;

            case CompetitiveRole.OffensiveCleaner:
                // Cleaners want attacks; maybe one setup
                if (Rnd.Next(100) < 30)
                {
                    if (pi.Stats[ATK] >= spa)
                        TryAddCategory(CompetitiveDatabase.AtkSetupMoves, 1);
                    else
                        TryAddCategory(CompetitiveDatabase.SpaSetupMoves, 1);
                }
                break;

            case CompetitiveRole.SpeedControl:
                // Choice Scarf user, just wants attacks
                break;

            case CompetitiveRole.OffensiveUtility:
                TryAddCategory(CompetitiveDatabase.HazardMoves, 1);
                if (selected.Count < 4) TryAddCategory(CompetitiveDatabase.PivotMoves, 1);
                break;

            case CompetitiveRole.BulkyUtility:
                TryAddCategory(CompetitiveDatabase.RecoveryMoves, 1);
                TryAddCategory(CompetitiveDatabase.HazardMoves, 1);
                if (selected.Count < 4 && Rnd.Next(100) < 50)
                    TryAddCategory(CompetitiveDatabase.UtilityStatusMoves, 1);
                break;

            case CompetitiveRole.BulkyAttacker:
            case CompetitiveRole.TankyCleaner:
                // Wants attacks primarily
                break;
        }

        // --- Phase 3: STAB ATTACKS fill ---
        // Ensure at least 2 STAB attacking moves for offensive roles
        if (IsOffensiveRole(role))
        {
            FillSTABAttacks(selected, pool, moveNames, moveData, types, pi, role, 2);
        }
        else
        {
            FillSTABAttacks(selected, pool, moveNames, moveData, types, pi, role, 1);
        }

        // --- Phase 4: COVERAGE fill ---
        // Fill remaining with coverage moves (non-STAB attacks that provide good type coverage)
        FillCoverageAttacks(selected, pool, moveNames, moveData, types, pi, role);

        bool isPhysicalFill = pi.Stats[ATK] >= spa;
        bool CategoryOk(int m) => moveData == null || m >= moveData.Length ||
            moveData[m].Category == 0 ||
            (isPhysicalFill && moveData[m].Category == 1) ||
            (!isPhysicalFill && moveData[m].Category == 2);
        bool IsCompetitiveMove(int m) => m < moveNames.Length &&
            (CompetitiveDatabase.CompetitiveMoves.Contains(moveNames[m]) || CompetitiveDatabase.SituationalMoves.Contains(moveNames[m]));

        while (selected.Count < 4 && pool.Count > 0)
        {
            var remaining = pool.Where(m => m > 0 && m < moveNames.Length).ToList();
            if (remaining.Count == 0) break;

            var tier = remaining.Where(m => CategoryOk(m) && IsCompetitiveMove(m)).ToList();
            if (tier.Count == 0) tier = remaining.Where(CategoryOk).ToList();
            if (tier.Count == 0) tier = remaining.Where(IsCompetitiveMove).ToList();
            if (tier.Count == 0) tier = remaining;

            int pick = tier[Rnd.Next(tier.Count)];
            selected.Add(pick);
            pool.Remove(pick);
        }

        while (selected.Count < 4) selected.Add(1); // Fallback: Pound

        // --- Phase 6: CATEGORY REPAIR ---
        EnforceAttackCategory(selected, pool, moveNames, moveData, types, pi, statGapDecisive, isPhysicalAttacker);

        return selected.Take(4).ToArray();
    }

    /// <summary>
    /// Last pass over a finished set: swaps out attacks that use the Pokemon's weaker attacking
    /// stat, whenever the legal pool still holds a same-category replacement.
    /// </summary>
    private void EnforceAttackCategory(
        List<int> selected, List<int> pool,
        string[] moveNames, Move[] moveData, int[] types,
        PersonalInfo pi, bool statGapDecisive, bool isPhysicalAttacker)
    {
        if (!statGapDecisive || moveData == null || pi == null) return;

        int wantedCategory = isPhysicalAttacker ? 1 : 2;

        bool Attacking(int m) => m > 0 && m < moveData.Length && moveData[m].Category != 0;
        bool RightSide(int m) => m > 0 && m < moveData.Length && moveData[m].Category == wantedCategory;

        // Keeping the set's type spread is the point of coverage, so a replacement is scored the
        // way the coverage phase scores: STAB and an uncovered type first, raw power last.
        var coveredTypes = selected.Where(Attacking).Select(m => moveData[m].Type).ToHashSet();

        int Score(int m)
        {
            string name = m < moveNames.Length ? moveNames[m] : "";
            bool isCompetitive = !string.IsNullOrEmpty(name) && CompetitiveDatabase.CompetitiveMoves.Contains(name);
            bool isSituational = !isCompetitive && !string.IsNullOrEmpty(name) && CompetitiveDatabase.SituationalMoves.Contains(name);
            bool isStab = types != null && Array.IndexOf(types, moveData[m].Type) >= 0;
            bool newType = !coveredTypes.Contains(moveData[m].Type);
            return (isStab ? 8000 : 0) + (newType ? 4000 : 0)
                 + (isCompetitive ? 2000 : isSituational ? 1000 : 0) + moveData[m].Power;
        }

        for (int i = 0; i < selected.Count; i++)
        {
            int current = selected[i];
            if (!Attacking(current) || RightSide(current)) continue;

            int replacement = pool
                .Where(m => RightSide(m) && !selected.Contains(m))
                .OrderByDescending(Score)
                .DefaultIfEmpty(0)
                .First();

            if (replacement <= 0) continue;   // nothing on the better side is learnable; leave it

            coveredTypes.Remove(moveData[current].Type);
            coveredTypes.Add(moveData[replacement].Type);
            selected[i] = replacement;
            pool.Remove(replacement);
            pool.Add(current);
        }
    }

    // Applies ability-specific move requirements from the manifesto
    private void ApplyAbilityMoveRequirements(
        string ability, List<int> selected, List<int> pool,
        string[] moveNames, Move[] moveData, int[] types, CompetitiveRole role,
        TeamArchetype archetype, PersonalInfo pi)
    {
        bool TryAdd(string moveName)
        {
            if (selected.Count >= 4 || moveNames.Length == 0) return false;
            int id = Array.FindIndex(moveNames, n => n.Equals(moveName, StringComparison.OrdinalIgnoreCase));
            if (id <= 0 || !pool.Contains(id) || selected.Contains(id)) return false;
            selected.Add(id); pool.Remove(id); return true;
        }

        bool isPhysicalForCategory = pi.Stats[ATK] >= pi.Stats[SPA];

        void AddFromCategory(HashSet<string> cat, int count, bool preferSTAB = true)
        {
            var candidates = pool.Where(m =>
                m > 0 && m < moveNames.Length &&
                cat.Contains(moveNames[m]) &&
                !selected.Contains(m)).ToList();

            if (moveData != null)
                candidates.Sort((a, b) =>
                {
                    bool aMatches = a < moveData.Length && ((isPhysicalForCategory && moveData[a].Category == 1) || (!isPhysicalForCategory && moveData[a].Category == 2));
                    bool bMatches = b < moveData.Length && ((isPhysicalForCategory && moveData[b].Category == 1) || (!isPhysicalForCategory && moveData[b].Category == 2));
                    if (aMatches != bMatches) return bMatches ? 1 : -1;

                    if (preferSTAB)
                    {
                        bool aStab = a < moveData.Length && Array.IndexOf(types, moveData[a].Type) >= 0;
                        bool bStab = b < moveData.Length && Array.IndexOf(types, moveData[b].Type) >= 0;
                        if (aStab != bStab) return bStab ? 1 : -1;
                    }
                    int aPow = a < moveData.Length ? moveData[a].Power : 0;
                    int bPow = b < moveData.Length ? moveData[b].Power : 0;
                    return bPow.CompareTo(aPow);
                });

            int added = 0;
            foreach (int id in candidates)
            {
                if (selected.Count >= 4 || added >= count) break;
                selected.Add(id); pool.Remove(id); added++;
            }
        }

        string abilLower = ability.ToLowerInvariant();

        // Aerilate/Pixilate/Galvanize/Dragonize/Refrigerate -> 2+ Normal attacks
        if (abilLower is "aerilate" or "pixilate" or "galvanize" or "dragonize" or "refrigerate")
            AddFromCategory(CompetitiveDatabase.NormalAttacks, 2);

        // Adaptability -> 2+ STAB attacks (handled by STAB fill with higher priority)
        // Already handled in Phase 3

        // Contrary -> stat-drop move
        else if (abilLower == "contrary")
            AddFromCategory(CompetitiveDatabase.ContraryMoves, 1, true);

        // Mega Launcher -> 2+ pulse moves
        else if (abilLower == "mega launcher")
            AddFromCategory(CompetitiveDatabase.PulseMoves, 2, true);

        // Punk Rock -> 1+ sound move
        else if (abilLower == "punk rock")
            AddFromCategory(CompetitiveDatabase.SoundMoves, 1, true);

        // Iron Fist -> 2+ punch moves
        else if (abilLower == "iron fist")
            AddFromCategory(CompetitiveDatabase.PunchingMoves, 2, true);

        // Reckless -> 1+ recoil move
        else if (abilLower == "reckless")
            AddFromCategory(CompetitiveDatabase.RecoilMoves, 1, true);

        // Sharpness -> 1+ slicing move
        else if (abilLower == "sharpness")
            AddFromCategory(CompetitiveDatabase.SlicingMoves, 1, true);

        // Strong Jaw -> 2+ biting moves
        else if (abilLower == "strong jaw")
            AddFromCategory(CompetitiveDatabase.BitingMoves, 2, true);

        // Triage -> 1+ healing attack
        else if (abilLower == "triage")
            AddFromCategory(CompetitiveDatabase.HealingAttackMoves, 1, true);

        // Super Luck -> 1+ high crit move
        else if (abilLower == "super luck")
            AddFromCategory(CompetitiveDatabase.HighCritMoves, 1, true);

        // Simple -> setup move
        else if (abilLower == "simple")
            AddFromCategory(CompetitiveDatabase.SetupMoves, 1);

        // Tough Claws -> 2+ contact moves
        else if (abilLower == "tough claws")
            AddFromCategory(CompetitiveDatabase.ContactMoves, 2, true);

        // Moody -> Substitute + Protect
        else if (abilLower == "moody")
        {
            TryAdd("Substitute"); TryAdd("Protect");
        }

        // Speed Boost -> Protect (common, offensive)
        else if (abilLower == "speed boost" && IsOffensiveRole(role))
            TryAdd("Protect");

        // Prankster -> 2+ status moves
        else if (abilLower == "prankster")
            AddFromCategory(CompetitiveDatabase.UtilityStatusMoves, 2);

        // Stamina -> Body Press
        else if (abilLower == "stamina")
            TryAdd("Body Press");

        // Snow Warning -> Blizzard (if SpA >= Atk), Aurora Veil handled by item logic
        else if (abilLower == "snow warning")
        {
            if (pi.Stats[SPA] >= pi.Stats[ATK]) TryAdd("Blizzard");
        }

        // Shed Skin + offensive/utility -> Rest
        else if (abilLower == "shed skin")
        {
            if (Rnd.Next(100) < 60) TryAdd("Rest");
        }

        // Grassy Surge + physical -> Grassy Glide
        else if (abilLower == "grassy surge" && pi.Stats[ATK] >= pi.Stats[SPA])
            TryAdd("Grassy Glide");

        // Electric Surge + special -> Rising Voltage
        else if (abilLower == "electric surge" && pi.Stats[SPA] >= pi.Stats[ATK])
            TryAdd("Rising Voltage");

        // Psychic Surge + special -> Expanding Force
        else if (abilLower == "psychic surge" && pi.Stats[SPA] >= pi.Stats[ATK])
            TryAdd("Expanding Force");

        bool isPhysicalMon = pi.Stats[ATK] >= pi.Stats[SPA];
        IEnumerable<int> OrderByBetterCategoryThenPower(IEnumerable<int> candidates) =>
            candidates.OrderByDescending(m =>
            {
                bool matchesCat = (isPhysicalMon && moveData[m].Category == 1) ||
                                  (!isPhysicalMon && moveData[m].Category == 2);
                return (matchesCat ? 100000 : 0) + moveData[m].Power;
            });

        // Dark Aura -> 2+ Dark moves (favors the mon's higher attacking stat)
        if (abilLower == "dark aura")
        {
            var darkMoves = pool.Where(m =>
                m > 0 && m < (moveData?.Length ?? 0) &&
                moveData[m].Type == PokemonTypes.Dark &&
                moveData[m].Category != 0).ToList();
            int added = 0;
            foreach (int id in OrderByBetterCategoryThenPower(darkMoves))
            {
                if (selected.Count >= 4 || added >= 2) break;
                selected.Add(id); pool.Remove(id); added++;
            }
        }

        // Fairy Aura -> 2+ Fairy moves (favors the mon's higher attacking stat)
        else if (abilLower == "fairy aura")
        {
            var fairyMoves = pool.Where(m =>
                m > 0 && m < (moveData?.Length ?? 0) &&
                moveData[m].Type == PokemonTypes.Fairy &&
                moveData[m].Category != 0).ToList();
            int added = 0;
            foreach (int id in OrderByBetterCategoryThenPower(fairyMoves))
            {
                if (selected.Count >= 4 || added >= 2) break;
                selected.Add(id); pool.Remove(id); added++;
            }
        }

        // Dragon's Maw -> 2+ Dragon moves (favors the mon's higher attacking stat)
        else if (abilLower == "dragon's maw")
        {
            var dragonMoves = pool.Where(m =>
                m > 0 && m < (moveData?.Length ?? 0) &&
                moveData[m].Type == PokemonTypes.Dragon &&
                moveData[m].Category != 0).ToList();
            int added = 0;
            foreach (int id in OrderByBetterCategoryThenPower(dragonMoves))
            {
                if (selected.Count >= 4 || added >= 2) break;
                selected.Add(id); pool.Remove(id); added++;
            }
        }

        // Desolate Land / Drought -> 1+ Fire attack (favors the mon's higher attacking stat)
        else if (abilLower is "desolate land" or "drought")
        {
            var fireMoves = pool.Where(m =>
                m > 0 && m < (moveData?.Length ?? 0) &&
                moveData[m].Type == PokemonTypes.Fire &&
                moveData[m].Category != 0).ToList();
            if (fireMoves.Count > 0)
            {
                int best = OrderByBetterCategoryThenPower(fireMoves).First();
                selected.Add(best); pool.Remove(best);
            }
        }

        // Drizzle / Primordial Sea -> 1+ Water attack (favors the mon's higher attacking stat)
        else if (abilLower is "drizzle" or "primordial sea")
        {
            var waterMoves = pool.Where(m =>
                m > 0 && m < (moveData?.Length ?? 0) &&
                moveData[m].Type == PokemonTypes.Water &&
                moveData[m].Category != 0).ToList();
            if (waterMoves.Count > 0)
            {
                int best = OrderByBetterCategoryThenPower(waterMoves).First();
                selected.Add(best); pool.Remove(best);
            }
            // Electric-type with rain -> Thunder
            if (Array.IndexOf(types, PokemonTypes.Electric) >= 0 && pi.Stats[SPA] >= pi.Stats[ATK])
                TryAdd("Thunder");
            // Flying-type with rain -> Hurricane
            if (Array.IndexOf(types, PokemonTypes.Flying) >= 0 && pi.Stats[SPA] >= pi.Stats[ATK])
                TryAdd("Hurricane");
        }

        // Protean / Libero -> all different-type attacks
        else if (abilLower is "protean" or "libero")
        {
            // Handled in coverage fill — ensure all attack types differ
            // Mark for the fill phase
        }

        // Download / Neuroforce -> 3+ attacks
        // Handled by ensuring minimum attacks in role-based logic
    }

    // Fills STAB attacking moves, prioritizing by power
    private void FillSTABAttacks(
        List<int> selected, List<int> pool,
        string[] moveNames, Move[] moveData, int[] types,
        PersonalInfo pi, CompetitiveRole role, int minSTAB)
    {
        if (moveData == null || types.Length == 0) return;
        bool isPhysical = pi.Stats[ATK] >= pi.Stats[SPA];

        int stabCount = selected.Count(m =>
            m > 0 && m < (moveData?.Length ?? 0) &&
            Array.IndexOf(types, moveData[m].Type) >= 0 &&
            moveData[m].Category != 0);

        int needed = minSTAB - stabCount;
        if (needed <= 0) return;

        int strong = Math.Max(pi.Stats[ATK], pi.Stats[SPA]);
        int weak = Math.Min(pi.Stats[ATK], pi.Stats[SPA]);
        bool gapIsDecisive = weak * 4 < strong * 3;      // weaker stat below 75% of the stronger

        var stabCandidates = pool.Where(m =>
            m > 0 && m < moveData.Length &&
            Array.IndexOf(types, moveData[m].Type) >= 0 &&
            moveData[m].Category != 0 &&
            !selected.Contains(m) &&
            (!gapIsDecisive ||
             (isPhysical && moveData[m].Category == 1) || (!isPhysical && moveData[m].Category == 2)))
            .OrderByDescending(m =>
            {
                bool matchesCat = (isPhysical && moveData[m].Category == 1) ||
                                  (!isPhysical && moveData[m].Category == 2);
                string name = m < moveNames.Length ? moveNames[m] : "";
                bool isCompetitive = !string.IsNullOrEmpty(name) && CompetitiveDatabase.CompetitiveMoves.Contains(name);
                bool isSituational = !isCompetitive && !string.IsNullOrEmpty(name) && CompetitiveDatabase.SituationalMoves.Contains(name);
                return (matchesCat ? 100000 : 0) + (isCompetitive ? 2000 : isSituational ? 1000 : 0) + moveData[m].Power;
            })
            .ToList();

        foreach (int id in stabCandidates)
        {
            if (selected.Count >= 4 || needed <= 0) break;
            selected.Add(id); pool.Remove(id); needed--;
        }
    }

    // Fills coverage (non-STAB) attacks
    private void FillCoverageAttacks(
        List<int> selected, List<int> pool,
        string[] moveNames, Move[] moveData, int[] types,
        PersonalInfo pi, CompetitiveRole role)
    {
        if (moveData == null || selected.Count >= 4) return;
        if (IsDefensiveRole(role)) return; // Defensive roles don't need coverage fill

        bool isPhysical = pi.Stats[ATK] >= pi.Stats[SPA];
        var coveredTypes = new HashSet<int>();

        // Already covered types from selected moves
        foreach (int m in selected)
        {
            if (m > 0 && m < moveData.Length && moveData[m].Category != 0)
                coveredTypes.Add(moveData[m].Type);
        }

        // Types that resist this Pokemon's own STAB — coverage should aim to hit these hard,
        // per the manifesto's "coverage that attacks Pokemon that resist [STAB]" requirement.
        var stabResistedTypes = new HashSet<int>();
        if (types != null)
        {
            for (int t = 0; t < 18; t++)
            {
                foreach (int stabType in types)
                {
                    if (TypeEffectivenessChart.GetEffectiveness(stabType, t) < 1f)
                    {
                        stabResistedTypes.Add(t);
                        break;
                    }
                }
            }
        }

        int CandidateScore(int m)
        {
            string name = m < moveNames.Length ? moveNames[m] : "";
            bool isCompetitive = !string.IsNullOrEmpty(name) && CompetitiveDatabase.CompetitiveMoves.Contains(name);
            bool isSituational = !isCompetitive && !string.IsNullOrEmpty(name) && CompetitiveDatabase.SituationalMoves.Contains(name);
            int moveType = moveData[m].Type;
            bool hitsStabResist = stabResistedTypes.Any(rt => TypeEffectivenessChart.GetEffectiveness(moveType, rt) > 1f);
            return (hitsStabResist ? 5000 : 0) + (isCompetitive ? 2000 : isSituational ? 1000 : 0) + moveData[m].Power;
        }

        var eligible = pool.Where(m =>
            m > 0 && m < moveData.Length &&
            moveData[m].Category != 0 &&
            !coveredTypes.Contains(moveData[m].Type) &&
            !selected.Contains(m))
            .ToList();

        var coverageCandidates = eligible
            .Where(m => (isPhysical && moveData[m].Category == 1) || (!isPhysical && moveData[m].Category == 2))
            .OrderByDescending(CandidateScore)
            .ToList();

        foreach (int id in coverageCandidates)
        {
            if (selected.Count >= 4) break;
            coveredTypes.Add(moveData[id].Type);
            selected.Add(id);
            pool.Remove(id);
        }
    }

    private static HashSet<string> ResolveMoveNames(int[] moves, string[] moveNames)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (moves == null || moveNames == null) return set;
        foreach (int m in moves)
        {
            if (m > 0 && m < moveNames.Length)
            {
                string name = moveNames[m];
                if (!string.IsNullOrEmpty(name)) set.Add(name);
            }
        }
        return set;
    }

    public static bool IsOffensiveRole(CompetitiveRole role) => role switch
    {
        CompetitiveRole.OffensiveSweeper or CompetitiveRole.OffensiveBreaker or
        CompetitiveRole.OffensiveCleaner or CompetitiveRole.OffensivePivot or
        CompetitiveRole.OffensiveLead or CompetitiveRole.OffensiveUtility or
        CompetitiveRole.SpeedControl => true,
        _ => false
    };

    public static bool IsDefensiveRole(CompetitiveRole role) => role switch
    {
        CompetitiveRole.DefensiveWall or CompetitiveRole.DefensivePivot => true,
        _ => false
    };

    public static bool IsBulkyRole(CompetitiveRole role) => role switch
    {
        CompetitiveRole.BulkyAttacker or CompetitiveRole.BulkySweeper or
        CompetitiveRole.BulkyPivot or CompetitiveRole.BulkyLead or
        CompetitiveRole.BulkyUtility or CompetitiveRole.TankySweeper or
        CompetitiveRole.TankyCleaner => true,
        _ => false
    };
}
