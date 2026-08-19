using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.Structures.PersonalInfo;
using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers.Competitive;

public static class CompetitiveValidator
{
    // Stat index constants for PersonalInfo.Stats: [HP, ATK, DEF, SPE, SPA, SPD]
    private const int HP = CompetitiveDatabase.HP;
    private const int ATK = CompetitiveDatabase.ATK;
    private const int DEF = CompetitiveDatabase.DEF;
    private const int SPE = CompetitiveDatabase.SPE;
    private const int SPA = CompetitiveDatabase.SPA;
    private const int SPD = CompetitiveDatabase.SPD;

    /// <summary>
    /// Performs strict validation and sanitization on a single trainer Pokemon.
    /// Ensures 100% compliance with competitive manifesto rules.
    /// </summary>
    public static void ValidateAndSanitize(
        TrainerPoke7 pk,
        PersonalInfo pi,
        string abilityName,
        CompetitiveRole role,
        string[] itemNames,
        string[] moveNames,
        Move[] moveData = null,
        LegalLearnsetAggregator learnsets = null)
    {
        if (pk == null || pi == null) return;

        ushort GetItemId(string name)
        {
            if (itemNames == null || itemNames.Length == 0 || string.IsNullOrEmpty(name)) return 0;
            int idx = Array.FindIndex(itemNames, z => z.Equals(name, StringComparison.OrdinalIgnoreCase));
            return idx > 0 ? (ushort)idx : (ushort)0;
        }

        ushort ReturnItem(string primary, string fallback = "Leftovers")
        {
            ushort id = GetItemId(primary);
            if (id > 0) return id;
            ushort fallbackId = GetItemId(fallback);
            return fallbackId > 0 ? fallbackId : GetItemId("Sitrus Berry");
        }

        string GetItemName(int id)
        {
            if (itemNames == null || id < 0 || id >= itemNames.Length) return "";
            return itemNames[id] ?? "";
        }

        string GetMoveName(int id)
        {
            if (moveNames == null || id < 0 || id >= moveNames.Length) return "";
            return moveNames[id] ?? "";
        }

        var currentMoveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int physCount = 0, specCount = 0, statusCount = 0;
        if (pk.Moves != null && moveNames != null)
        {
            foreach (int m in pk.Moves)
            {
                if (m <= 0) continue;
                if (m < moveNames.Length)
                {
                    string mName = moveNames[m];
                    if (!string.IsNullOrEmpty(mName)) currentMoveNames.Add(mName);
                }
                if (moveData != null && m < moveData.Length)
                {
                    switch (moveData[m].Category)
                    {
                        case 0: statusCount++; break;
                        case 1: physCount++; break;
                        case 2: specCount++; break;
                    }
                }
            }
        }

        string currentItem = GetItemName(pk.Item);

        // 1. LIGHT CLAY: only with (Light Screen + Reflect) or (Snow Warning + Aurora Veil)
        if (currentItem.Equals("Light Clay", StringComparison.OrdinalIgnoreCase))
        {
            bool hasDualScreens = currentMoveNames.Contains("Light Screen") && currentMoveNames.Contains("Reflect");
            bool hasSnowVeil = abilityName.Equals("Snow Warning", StringComparison.OrdinalIgnoreCase) && currentMoveNames.Contains("Aurora Veil");
            if (!hasDualScreens && !hasSnowVeil)
            {
                pk.Item = (int)ReturnItem(pi.Stats[DEF] + pi.Stats[SPD] < 140 ? "Focus Sash" : "Heavy-Duty Boots");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 2. LUM BERRY: only with setup move or Outrage/Raging Fury
        if (currentItem.Equals("Lum Berry", StringComparison.OrdinalIgnoreCase))
        {
            bool hasSetup = currentMoveNames.Overlaps(CompetitiveDatabase.SetupMoves);
            bool hasOutrage = currentMoveNames.Contains("Outrage") || currentMoveNames.Contains("Raging Fury");
            if (!hasSetup && !hasOutrage)
            {
                pk.Item = (int)GetItemId(pi.Stats[HP] + pi.Stats[DEF] > 150 ? "Leftovers" : "Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 3. ASSAULT VEST: no status moves allowed
        if (currentItem.Equals("Assault Vest", StringComparison.OrdinalIgnoreCase) && statusCount > 0)
        {
            pk.Item = (int)GetItemId(pi.Stats[HP] + pi.Stats[DEF] > 150 ? "Leftovers" : "Life Orb");
            currentItem = GetItemName(pk.Item);
        }

        // 4. CHOICE ITEMS: no status moves except Trick/Switcheroo
        if (currentItem.Equals("Choice Band", StringComparison.OrdinalIgnoreCase) ||
            currentItem.Equals("Choice Specs", StringComparison.OrdinalIgnoreCase) ||
            currentItem.Equals("Choice Scarf", StringComparison.OrdinalIgnoreCase))
        {
            bool hasInvalidStatus = false;
            if (moveData != null && pk.Moves != null)
            {
                foreach (int m in pk.Moves)
                {
                    if (m <= 0 || m >= moveData.Length || moveData[m].Category != 0) continue;
                    string mName = GetMoveName(m);
                    if (!mName.Equals("Trick", StringComparison.OrdinalIgnoreCase) &&
                        !mName.Equals("Switcheroo", StringComparison.OrdinalIgnoreCase))
                    {
                        hasInvalidStatus = true;
                        break;
                    }
                }
            }
            if (hasInvalidStatus)
            {
                pk.Item = (int)GetItemId(pi.Stats[HP] + pi.Stats[DEF] > 150 ? "Leftovers" : "Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 5. Z-CRYSTALS: MUST have matching move/type
        if (IsZCrystal(currentItem))
        {
            bool isZValid = false;
            if (CompetitiveDatabase.SpeciesZCrystals.TryGetValue(pk.Species, out var specZ) &&
                currentItem.Equals(specZ.Crystal, StringComparison.OrdinalIgnoreCase))
            {
                isZValid = currentMoveNames.Contains(specZ.Move);
            }
            else
            {
                int zType = -1;
                foreach (var kvp in CompetitiveDatabase.TypeZCrystals)
                {
                    if (kvp.Value.Equals(currentItem, StringComparison.OrdinalIgnoreCase))
                    {
                        zType = kvp.Key;
                        break;
                    }
                }
                if (zType >= 0 && moveData != null && pk.Moves != null)
                {
                    foreach (int m in pk.Moves)
                    {
                        if (m > 0 && m < moveData.Length && moveData[m].Category != 0 && moveData[m].Type == zType)
                        {
                            isZValid = true;
                            break;
                        }
                    }
                }
            }
            if (!isZValid)
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 6. EVIOLITE: pre-evolutions only
        if (currentItem.Equals("Eviolite", StringComparison.OrdinalIgnoreCase))
        {
            if (Legal.FinalEvolutions_7.Contains(pk.Species))
            {
                pk.Item = (int)GetItemId(pi.Stats[HP] + pi.Stats[DEF] > 150 ? "Leftovers" : "Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 7. BLACK SLUDGE: Poison-type only
        if (currentItem.Equals("Black Sludge", StringComparison.OrdinalIgnoreCase))
        {
            if (!pi.Types.Contains(PokemonTypes.Poison))
            {
                pk.Item = (int)GetItemId("Leftovers");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 8. AIR BALLOON: not redundant (no Flying type / Levitate / Earth Eater)
        if (currentItem.Equals("Air Balloon", StringComparison.OrdinalIgnoreCase))
        {
            if (pi.Types.Contains(PokemonTypes.Flying) ||
                abilityName.Equals("Levitate", StringComparison.OrdinalIgnoreCase) ||
                abilityName.Equals("Earth Eater", StringComparison.OrdinalIgnoreCase))
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 9. LOADED DICE: must have a multi-hit move
        if (currentItem.Equals("Loaded Dice", StringComparison.OrdinalIgnoreCase))
        {
            if (!currentMoveNames.Overlaps(CompetitiveDatabase.MultiHitMoves))
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 10. POWER HERB: must have a two-turn move
        if (currentItem.Equals("Power Herb", StringComparison.OrdinalIgnoreCase))
        {
            if (!currentMoveNames.Overlaps(CompetitiveDatabase.TwoTurnMoves))
            {
                pk.Item = (int)GetItemId("White Herb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 11. TOXIC ORB: must have Poison Heal, Toxic Boost, or Trick/Switcheroo
        if (currentItem.Equals("Toxic Orb", StringComparison.OrdinalIgnoreCase))
        {
            if (!abilityName.Equals("Poison Heal", StringComparison.OrdinalIgnoreCase) &&
                !abilityName.Equals("Toxic Boost", StringComparison.OrdinalIgnoreCase) &&
                !currentMoveNames.Contains("Trick") && !currentMoveNames.Contains("Switcheroo"))
            {
                pk.Item = (int)GetItemId("Leftovers");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 12. FLAME ORB: must have Guts, Flare Boost, or Trick/Switcheroo
        if (currentItem.Equals("Flame Orb", StringComparison.OrdinalIgnoreCase))
        {
            if (!abilityName.Equals("Guts", StringComparison.OrdinalIgnoreCase) &&
                !abilityName.Equals("Flare Boost", StringComparison.OrdinalIgnoreCase) &&
                !currentMoveNames.Contains("Trick") && !currentMoveNames.Contains("Switcheroo"))
            {
                pk.Item = (int)GetItemId("Leftovers");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 9. WISE GLASSES: must have 3+ special attacks
        if (currentItem.Equals("Wise Glasses", StringComparison.OrdinalIgnoreCase) && specCount < 3)
        {
            pk.Item = (int)GetItemId("Life Orb");
            currentItem = GetItemName(pk.Item);
        }

        // 10. MUSCLE BAND: must have 3+ physical attacks
        if (currentItem.Equals("Muscle Band", StringComparison.OrdinalIgnoreCase) && physCount < 3)
        {
            pk.Item = (int)GetItemId("Life Orb");
            currentItem = GetItemName(pk.Item);
        }

        // 11. NORMAL GEM: must have Explosion and Speed >= 90
        if (currentItem.Equals("Normal Gem", StringComparison.OrdinalIgnoreCase))
        {
            if (!currentMoveNames.Contains("Explosion") || pi.Stats[SPE] < 90)
            {
                pk.Item = (int)GetItemId("Focus Sash");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 12. ROCKY HELMET: must have HP + Def > 160
        if (currentItem.Equals("Rocky Helmet", StringComparison.OrdinalIgnoreCase))
        {
            if (pi.Stats[HP] + pi.Stats[DEF] <= 160)
            {
                pk.Item = (int)GetItemId("Leftovers");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 13. SALAC BERRY: must have a setup move or Substitute
        if (currentItem.Equals("Salac Berry", StringComparison.OrdinalIgnoreCase))
        {
            if (!currentMoveNames.Overlaps(CompetitiveDatabase.SetupMoves) && !currentMoveNames.Contains("Substitute"))
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 14. EXPERT BELT: must have 3+ attacking moves
        if (currentItem.Equals("Expert Belt", StringComparison.OrdinalIgnoreCase))
        {
            if (physCount + specCount < 3)
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 15. SCOPE LENS: must have Super Luck and a high-crit move
        if (currentItem.Equals("Scope Lens", StringComparison.OrdinalIgnoreCase))
        {
            if (!abilityName.Equals("Super Luck", StringComparison.OrdinalIgnoreCase) ||
                !currentMoveNames.Overlaps(CompetitiveDatabase.HighCritMoves))
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 16. BOOSTER ENERGY: must have Protosynthesis or Quark Drive
        if (currentItem.Equals("Booster Energy", StringComparison.OrdinalIgnoreCase))
        {
            if (!abilityName.Equals("Protosynthesis", StringComparison.OrdinalIgnoreCase) &&
                !abilityName.Equals("Quark Drive", StringComparison.OrdinalIgnoreCase))
            {
                pk.Item = (int)GetItemId("Life Orb");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 17. TERRAIN SEEDS: validated at team level (see ValidateTeam)

        // 18. TERRAIN EXTENDER: must have a terrain-setting ability
        if (currentItem.Equals("Terrain Extender", StringComparison.OrdinalIgnoreCase))
        {
            bool hasTerrainAbility = abilityName.Equals("Electric Surge", StringComparison.OrdinalIgnoreCase) ||
                                      abilityName.Equals("Grassy Surge", StringComparison.OrdinalIgnoreCase) ||
                                      abilityName.Equals("Psychic Surge", StringComparison.OrdinalIgnoreCase) ||
                                      abilityName.Equals("Misty Surge", StringComparison.OrdinalIgnoreCase);
            if (!hasTerrainAbility)
            {
                pk.Item = (int)ReturnItem("Heavy-Duty Boots");
                currentItem = GetItemName(pk.Item);
            }
        }

        // 19. HEAT ROCK / DAMP ROCK / ICY ROCK: must have the matching weather ability
        if (currentItem.Equals("Heat Rock", StringComparison.OrdinalIgnoreCase) &&
            !abilityName.Equals("Drought", StringComparison.OrdinalIgnoreCase))
        {
            pk.Item = (int)ReturnItem("Leftovers");
            currentItem = GetItemName(pk.Item);
        }
        if (currentItem.Equals("Damp Rock", StringComparison.OrdinalIgnoreCase) &&
            !abilityName.Equals("Drizzle", StringComparison.OrdinalIgnoreCase))
        {
            pk.Item = (int)ReturnItem("Leftovers");
            currentItem = GetItemName(pk.Item);
        }
        if (currentItem.Equals("Icy Rock", StringComparison.OrdinalIgnoreCase) &&
            !abilityName.Equals("Snow Warning", StringComparison.OrdinalIgnoreCase))
        {
            pk.Item = (int)ReturnItem("Leftovers");
            currentItem = GetItemName(pk.Item);
        }
        if (currentItem.Equals("Smooth Rock", StringComparison.OrdinalIgnoreCase) &&
            !abilityName.Equals("Sand Stream", StringComparison.OrdinalIgnoreCase))
        {
            pk.Item = (int)ReturnItem("Leftovers");
            currentItem = GetItemName(pk.Item);
        }

        // Adaptability: verify 2+ STAB attacks
        if (abilityName.Equals("Adaptability", StringComparison.OrdinalIgnoreCase))
        {
            ValidateMinSTABAttacks(pk, pi, moveNames, moveData, learnsets, 2);
        }

        // Aerilate/Pixilate/Galvanize/Dragonize: verify 2+ Normal attacks
        if (abilityName.Equals("Aerilate", StringComparison.OrdinalIgnoreCase) ||
            abilityName.Equals("Pixilate", StringComparison.OrdinalIgnoreCase) ||
            abilityName.Equals("Galvanize", StringComparison.OrdinalIgnoreCase) ||
            abilityName.Equals("Dragonize", StringComparison.OrdinalIgnoreCase))
        {
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.NormalAttacks, 2);
        }

        // Contrary: verify 1+ stat-drop move
        if (abilityName.Equals("Contrary", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.ContraryMoves, 1);

        // Iron Fist: 2+ punch moves
        if (abilityName.Equals("Iron Fist", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.PunchingMoves, 2);

        // Mega Launcher: 2+ pulse moves
        if (abilityName.Equals("Mega Launcher", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.PulseMoves, 2);

        // Punk Rock: 1+ sound move
        if (abilityName.Equals("Punk Rock", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.SoundMoves, 1);

        // Reckless: 1+ recoil move
        if (abilityName.Equals("Reckless", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.RecoilMoves, 1);

        // Sharpness: 1+ slicing move
        if (abilityName.Equals("Sharpness", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.SlicingMoves, 1);

        // Strong Jaw: 2+ biting moves
        if (abilityName.Equals("Strong Jaw", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.BitingMoves, 2);

        // Triage: 1+ healing attack
        if (abilityName.Equals("Triage", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.HealingAttackMoves, 1);

        // Tough Claws: 2+ contact moves
        if (abilityName.Equals("Tough Claws", StringComparison.OrdinalIgnoreCase))
            ValidateMinMovesFromCategory(pk, pi, moveNames, moveData, learnsets, CompetitiveDatabase.ContactMoves, 2);

        // Moody: must have Substitute + Protect
        if (abilityName.Equals("Moody", StringComparison.OrdinalIgnoreCase))
        {
            EnsureMovePresent(pk, "Substitute", moveNames, learnsets);
            EnsureMovePresent(pk, "Protect", moveNames, learnsets);
        }

        if (pk.EVs != null)
        {
            // Clamp individual EVs to 252
            for (int i = 0; i < pk.EVs.Length; i++)
            {
                if (pk.EVs[i] > 252) pk.EVs[i] = 252;
                if (pk.EVs[i] < 0) pk.EVs[i] = 0;
            }

            // Total must not exceed 510
            int totalEVs = pk.EVs.Sum();
            if (totalEVs > 510)
            {
                double scale = 510.0 / totalEVs;
                for (int i = 0; i < pk.EVs.Length; i++)
                    pk.EVs[i] = (int)Math.Floor(pk.EVs[i] * scale);
            }
        }
    }

    /// <summary>
    /// Validates the minimum number of STAB attacking moves on a Pokemon's moveset.
    /// Attempts to swap in valid moves from the legal pool if under-count.
    /// </summary>
    private static void ValidateMinSTABAttacks(
        TrainerPoke7 pk, PersonalInfo pi, string[] moveNames, Move[] moveData,
        LegalLearnsetAggregator learnsets, int minCount)
    {
        if (moveData == null || pk.Moves == null) return;
        int[] types = pi.Types;

        int stabCount = 0;
        foreach (int m in pk.Moves)
        {
            if (m <= 0 || m >= moveData.Length) continue;
            if (moveData[m].Category != 0 && Array.IndexOf(types, moveData[m].Type) >= 0)
                stabCount++;
        }

        if (stabCount >= minCount || learnsets == null) return;

        // Try to swap non-STAB non-essential moves with STAB moves
        var legal = learnsets.GetLegalMoves(pk.Species);
        var stabPool = legal.Where(m =>
            m > 0 && m < moveData.Length &&
            moveData[m].Category != 0 &&
            Array.IndexOf(types, moveData[m].Type) >= 0 &&
            !pk.Moves.Contains(m))
            .OrderByDescending(m => moveData[m].Power)
            .ToList();

        int[] currentMoves = pk.Moves;
        for (int i = 3; i >= 0 && stabCount < minCount && stabPool.Count > 0; i--)
        {
            int existing = currentMoves[i];
            if (existing <= 0 || existing >= moveData.Length) continue;
            // Don't replace STAB moves or setup/utility
            if (Array.IndexOf(types, moveData[existing].Type) >= 0) continue;

            currentMoves[i] = stabPool[0];
            stabPool.RemoveAt(0);
            stabCount++;
        }
        pk.Moves = currentMoves;
    }

    /// <summary>
    /// Validates minimum moves from a specific named category.
    /// </summary>
    private static void ValidateMinMovesFromCategory(
        TrainerPoke7 pk, PersonalInfo pi, string[] moveNames, Move[] moveData,
        LegalLearnsetAggregator learnsets, HashSet<string> category, int minCount)
    {
        if (moveNames == null || pk.Moves == null) return;

        int catCount = 0;
        foreach (int m in pk.Moves)
        {
            if (m <= 0 || m >= moveNames.Length) continue;
            if (category.Contains(moveNames[m])) catCount++;
        }

        if (catCount >= minCount || learnsets == null) return;

        var legal = learnsets.GetLegalMoves(pk.Species);
        var catPool = legal.Where(m =>
            m > 0 && m < moveNames.Length &&
            category.Contains(moveNames[m]) &&
            !pk.Moves.Contains(m)).ToList();

        if (moveData != null)
            catPool = catPool.OrderByDescending(m => m < moveData.Length ? moveData[m].Power : 0).ToList();

        int[] currentMoves = pk.Moves;
        for (int i = 3; i >= 0 && catCount < minCount && catPool.Count > 0; i--)
        {
            int existing = currentMoves[i];
            if (existing <= 0) continue;
            // Don't replace moves already in the target category
            if (existing < moveNames.Length && category.Contains(moveNames[existing])) continue;

            currentMoves[i] = catPool[0];
            catPool.RemoveAt(0);
            catCount++;
        }
        pk.Moves = currentMoves;
    }

    /// <summary>
    /// Ensures a specific named move is present in the moveset.
    /// </summary>
    private static void EnsureMovePresent(
        TrainerPoke7 pk, string moveName, string[] moveNames, LegalLearnsetAggregator learnsets)
    {
        if (moveNames == null || pk.Moves == null) return;

        // Already has it?
        foreach (int m in pk.Moves)
        {
            if (m > 0 && m < moveNames.Length && moveNames[m].Equals(moveName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        // Find the move ID
        int moveId = -1;
        for (int i = 0; i < moveNames.Length; i++)
        {
            if (moveNames[i].Equals(moveName, StringComparison.OrdinalIgnoreCase))
            {
                moveId = i;
                break;
            }
        }
        if (moveId <= 0) return;

        // Verify it's in the legal pool
        if (learnsets != null && !learnsets.LearnsMove(pk.Species, moveId)) return;

        // Replace last slot
        int[] moves = pk.Moves;
        moves[3] = moveId;
        pk.Moves = moves;
    }

    /// <summary>
    /// Validates team-level constraints: max 1 Mega Stone, max 1 Z-Crystal,
    /// terrain seed-surge pairing, archetype requirements.
    /// </summary>
    public static void ValidateTeam(
        TrainerPoke7[] team,
        PersonalInfo[] teamPI,
        string[] teamAbilities,
        CompetitiveRole[] roles,
        string[] itemNames,
        string[] moveNames,
        Move[] moveData,
        TeamArchetype archetype)
    {
        if (team == null || team.Length == 0 || itemNames == null) return;

        string GetItemName(int id) =>
            (id >= 0 && id < itemNames.Length) ? (itemNames[id] ?? "") : "";

        ushort GetItemId(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int idx = Array.FindIndex(itemNames, z => z.Equals(name, StringComparison.OrdinalIgnoreCase));
            return idx > 0 ? (ushort)idx : (ushort)0;
        }

        // --- Max 1 Mega Stone ---
        bool foundMega = false;
        for (int i = 0; i < team.Length; i++)
        {
            string item = GetItemName(team[i].Item);
            if (IsMegaStone(item))
            {
                if (foundMega)
                {
                    // Remove duplicate Mega Stone
                    team[i].Item = (int)GetItemId(teamPI != null && i < teamPI.Length ?
                        (teamPI[i].Stats[HP] + teamPI[i].Stats[DEF] > 150 ? "Leftovers" : "Life Orb") : "Life Orb");
                }
                else
                {
                    foundMega = true;
                }
            }
        }

        // --- Max 1 Z-Crystal ---
        bool foundZCrystal = false;
        for (int i = 0; i < team.Length; i++)
        {
            string item = GetItemName(team[i].Item);
            if (IsZCrystal(item))
            {
                if (foundZCrystal)
                {
                    team[i].Item = (int)GetItemId(teamPI != null && i < teamPI.Length ?
                        (teamPI[i].Stats[HP] + teamPI[i].Stats[DEF] > 150 ? "Leftovers" : "Life Orb") : "Life Orb");
                }
                else
                {
                    foundZCrystal = true;
                }
            }
        }

        // --- Terrain Seed validation: only if teammate has corresponding Surge ---
        bool hasElectricSurge = teamAbilities?.Any(a => a.Equals("Electric Surge", StringComparison.OrdinalIgnoreCase)) ?? false;
        bool hasGrassySurge = teamAbilities?.Any(a => a.Equals("Grassy Surge", StringComparison.OrdinalIgnoreCase)) ?? false;
        bool hasPsychicSurge = teamAbilities?.Any(a => a.Equals("Psychic Surge", StringComparison.OrdinalIgnoreCase)) ?? false;
        bool hasMistySurge = teamAbilities?.Any(a => a.Equals("Misty Surge", StringComparison.OrdinalIgnoreCase)) ?? false;

        for (int i = 0; i < team.Length; i++)
        {
            string item = GetItemName(team[i].Item);
            if (item.Equals("Electric Seed", StringComparison.OrdinalIgnoreCase) && !hasElectricSurge)
                team[i].Item = (int)GetItemId("Life Orb");
            if (item.Equals("Grassy Seed", StringComparison.OrdinalIgnoreCase) && !hasGrassySurge)
                team[i].Item = (int)GetItemId("Life Orb");
            if (item.Equals("Psychic Seed", StringComparison.OrdinalIgnoreCase) && !hasPsychicSurge)
                team[i].Item = (int)GetItemId("Life Orb");
            if (item.Equals("Misty Seed", StringComparison.OrdinalIgnoreCase) && !hasMistySurge)
                team[i].Item = (int)GetItemId("Life Orb");
        }

        // --- No duplicate held items (except berries/common items) ---
        var seenItems = new HashSet<int>();
        var allowDuplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Leftovers", "Focus Sash", "Life Orb", "Heavy-Duty Boots",
            "Sitrus Berry", "Lum Berry", "Aguav Berry", "Iapapa Berry",
            "Mago Berry", "Wiki Berry", "Figy Berry"
        };
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i].Item <= 0) continue;
            string item = GetItemName(team[i].Item);
            if (allowDuplicates.Contains(item)) continue;

            if (seenItems.Contains(team[i].Item))
            {
                team[i].Item = (int)GetItemId(teamPI != null && i < teamPI.Length ?
                    (teamPI[i].Stats[HP] + teamPI[i].Stats[DEF] > 150 ? "Leftovers" : "Life Orb") : "Life Orb");
            }
            else
            {
                seenItems.Add(team[i].Item);
            }
        }
    }

    /// <summary>
    /// Checks if an item name is a Mega Stone.
    /// </summary>
    /// <summary>
    /// Reports every ability, move or item this database names that the loaded ROM does not have.
    /// <para>
    /// A rule keyed on a name the ROM never produces cannot fire, and nothing else in the pipeline
    /// notices — the rule just silently does nothing. That is how six shop items spelled "Feather"
    /// instead of "Wing", a "Tomato Berry" that should read "Tamato", and three names written with
    /// an ASCII apostrophe where the games use U+2019 all sat here inert. Running this against a
    /// real ROM turns that whole class of defect into a list instead of a mystery.
    /// </para>
    /// </summary>
    /// <returns>Human-readable lines, empty when every name resolves.</returns>
    public static List<string> FindUnresolvedNames(GameConfig config)
    {
        var problems = new List<string>();
        if (config == null) return problems;

        HashSet<string> Vocab(TextName which)
        {
            var text = config.GetText(which);
            return new HashSet<string>(
                text?.Where(s => !string.IsNullOrWhiteSpace(s) && s != "-") ?? Enumerable.Empty<string>(),
                GameNameComparer.Instance);
        }

        var abilities = Vocab(TextName.AbilityNames);
        var moves = Vocab(TextName.MoveNames);
        var items = Vocab(TextName.ItemNames);

        void Check(string label, IEnumerable<string> declared, HashSet<string> real)
        {
            if (real.Count == 0) return; // text not loaded; nothing to say
            var missing = declared.Where(n => !string.IsNullOrWhiteSpace(n) && !real.Contains(n)).ToList();
            if (missing.Count > 0)
                problems.Add($"{label}: {missing.Count} unresolved -> {string.Join(", ", missing)}");
        }

        Check("CompetitiveAbilities", CompetitiveDatabase.CompetitiveAbilities, abilities);
        Check("SituationalAbilities", CompetitiveDatabase.SituationalAbilities, abilities);
        Check("LessCompetitiveAbilities", CompetitiveDatabase.LessCompetitiveAbilities, abilities);

        Check("CompetitiveMoves", CompetitiveDatabase.CompetitiveMoves, moves);
        Check("SituationalMoves", CompetitiveDatabase.SituationalMoves, moves);
        Check("PunchingMoves", CompetitiveDatabase.PunchingMoves, moves);
        Check("SlicingMoves", CompetitiveDatabase.SlicingMoves, moves);
        Check("SoundMoves", CompetitiveDatabase.SoundMoves, moves);
        Check("RecoilMoves", CompetitiveDatabase.RecoilMoves, moves);
        Check("BitingMoves", CompetitiveDatabase.BitingMoves, moves);
        Check("MultiHitMoves", CompetitiveDatabase.MultiHitMoves, moves);
        Check("HazardMoves", CompetitiveDatabase.HazardMoves, moves);
        Check("PivotMoves", CompetitiveDatabase.PivotMoves, moves);
        Check("ScreenMoves", CompetitiveDatabase.ScreenMoves, moves);
        Check("RecoveryMoves", CompetitiveDatabase.RecoveryMoves, moves);
        Check("SetupMoves", CompetitiveDatabase.SetupMoves, moves);
        Check("ContraryMoves", CompetitiveDatabase.ContraryMoves, moves);
        Check("SunAffectedMoves", CompetitiveDatabase.SunAffectedMoves, moves);
        Check("UtilityStatusMoves", CompetitiveDatabase.UtilityStatusMoves, moves);

        Check("MainShopItems", CompetitiveDatabase.MainShopItems, items);
        Check("CompetitiveShopItems", CompetitiveDatabase.CompetitiveShopItems, items);
        Check("BerryShopItems", CompetitiveDatabase.BerryShopItems, items);
        Check("TypeBoostShopItems", CompetitiveDatabase.TypeBoostShopItems, items);

        return problems;
    }

    public static bool IsMegaStone(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        // Mega Stones end in -ite, -nite, or -tite
        string lower = itemName.ToLowerInvariant();
        return lower.EndsWith("ite") || lower.EndsWith("nite") || lower.EndsWith("tite");
    }

    /// <summary>
    /// Checks if an item name is a Z-Crystal.
    /// </summary>
    public static bool IsZCrystal(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        return itemName.EndsWith(" Z", StringComparison.OrdinalIgnoreCase) &&
               !itemName.Equals("Absolite Z", StringComparison.OrdinalIgnoreCase) &&
               !itemName.Equals("Garchompite Z", StringComparison.OrdinalIgnoreCase) &&
               !itemName.Equals("Lucarionite Z", StringComparison.OrdinalIgnoreCase);
    }
}
