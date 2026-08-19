using System;
using System.Collections.Generic;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.Core.Randomizers.Competitive;

/// <summary>
/// Compares game names while ignoring which apostrophe character is used.
/// <para>
/// Every name set in this file is matched against text read out of the ROM, and the ROM writes
/// apostrophes as U+2019 ("Dragon’s Maw", "King’s Shield", "Mind’s Eye") while source code
/// naturally spells them with an ASCII quote. Under an ordinal comparer those never match, so the
/// rules keyed on them - the Dragon's Maw forced-move block, King's Shield as a utility move,
/// Mind's Eye as a Scrappy equivalent - silently never fired. The item table is worse: it mixes
/// both characters, so neither spelling alone is correct.
/// </para>
/// <para>Normalising the character removes the whole class of bug rather than fixing three names.</para>
/// </summary>
public sealed class GameNameComparer : IEqualityComparer<string>
{
    public static readonly GameNameComparer Instance = new();

    public bool Equals(string x, string y) => string.Equals(Normalize(x), Normalize(y), StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(string obj) => Normalize(obj)?.ToUpperInvariant().GetHashCode() ?? 0;

    /// <summary>Folds every apostrophe variant the games use onto the ASCII one.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace('’', '\'')  // right single quotation mark - what USUM uses
                .Replace('ʼ', '\'')  // modifier letter apostrophe
                .Replace('‘', '\''); // left single quotation mark
    }
}

// Pokémon type constants matching the game's internal type IDs (Gen 6/7).
public static class PokemonTypes
{
    public const int Normal = 0, Fighting = 1, Flying = 2, Poison = 3, Ground = 4,
                     Rock = 5, Bug = 6, Ghost = 7, Steel = 8, Fire = 9,
                     Water = 10, Grass = 11, Electric = 12, Psychic = 13,
                     Ice = 14, Dragon = 15, Dark = 16, Fairy = 17;
}

// Gen 6+ type effectiveness chart (attacker type -> defender type -> multiplier).
// Only non-neutral (not 1x) matchups are stored; anything absent defaults to 1x.
public static class TypeEffectivenessChart
{
    private static readonly Dictionary<(int Atk, int Def), float> Chart = BuildChart();

    public static float GetEffectiveness(int attackType, int defendType)
    {
        return Chart.TryGetValue((attackType, defendType), out float mult) ? mult : 1f;
    }

    // Effectiveness of attackType against a Pokemon with the given (1 or 2) defending types.
    public static float GetEffectiveness(int attackType, int[] defendTypes)
    {
        float mult = 1f;
        if (defendTypes == null) return mult;
        foreach (int t in defendTypes)
            mult *= GetEffectiveness(attackType, t);
        return mult;
    }

    private static Dictionary<(int, int), float> BuildChart()
    {
        var c = new Dictionary<(int, int), float>();
        void Set(int atk, int def, float mult) => c[(atk, def)] = mult;

        int Normal = PokemonTypes.Normal, Fighting = PokemonTypes.Fighting, Flying = PokemonTypes.Flying,
            Poison = PokemonTypes.Poison, Ground = PokemonTypes.Ground, Rock = PokemonTypes.Rock,
            Bug = PokemonTypes.Bug, Ghost = PokemonTypes.Ghost, Steel = PokemonTypes.Steel,
            Fire = PokemonTypes.Fire, Water = PokemonTypes.Water, Grass = PokemonTypes.Grass,
            Electric = PokemonTypes.Electric, Psychic = PokemonTypes.Psychic, Ice = PokemonTypes.Ice,
            Dragon = PokemonTypes.Dragon, Dark = PokemonTypes.Dark, Fairy = PokemonTypes.Fairy;

        Set(Normal, Rock, 0.5f); Set(Normal, Ghost, 0f); Set(Normal, Steel, 0.5f);

        Set(Fighting, Normal, 2f); Set(Fighting, Flying, 0.5f); Set(Fighting, Poison, 0.5f);
        Set(Fighting, Rock, 2f); Set(Fighting, Bug, 0.5f); Set(Fighting, Ghost, 0f);
        Set(Fighting, Steel, 2f); Set(Fighting, Psychic, 0.5f); Set(Fighting, Ice, 2f);
        Set(Fighting, Dark, 2f); Set(Fighting, Fairy, 0.5f);

        Set(Flying, Fighting, 2f); Set(Flying, Rock, 0.5f); Set(Flying, Bug, 2f);
        Set(Flying, Steel, 0.5f); Set(Flying, Grass, 2f); Set(Flying, Electric, 0.5f);

        Set(Poison, Poison, 0.5f); Set(Poison, Ground, 0.5f); Set(Poison, Rock, 0.5f);
        Set(Poison, Ghost, 0.5f); Set(Poison, Steel, 0f); Set(Poison, Grass, 2f); Set(Poison, Fairy, 2f);

        Set(Ground, Flying, 0f); Set(Ground, Poison, 2f); Set(Ground, Rock, 2f);
        Set(Ground, Bug, 0.5f); Set(Ground, Steel, 2f); Set(Ground, Fire, 2f);
        Set(Ground, Grass, 0.5f); Set(Ground, Electric, 2f);

        Set(Rock, Fighting, 0.5f); Set(Rock, Flying, 2f); Set(Rock, Ground, 0.5f);
        Set(Rock, Bug, 2f); Set(Rock, Steel, 0.5f); Set(Rock, Fire, 2f); Set(Rock, Ice, 2f);

        Set(Bug, Fighting, 0.5f); Set(Bug, Flying, 0.5f); Set(Bug, Poison, 0.5f);
        Set(Bug, Ghost, 0.5f); Set(Bug, Steel, 0.5f); Set(Bug, Fire, 0.5f); Set(Bug, Grass, 2f);
        Set(Bug, Psychic, 2f); Set(Bug, Dark, 2f); Set(Bug, Fairy, 0.5f);

        Set(Ghost, Normal, 0f); Set(Ghost, Ghost, 2f); Set(Ghost, Dark, 0.5f); Set(Ghost, Psychic, 2f);

        Set(Steel, Rock, 2f); Set(Steel, Steel, 0.5f); Set(Steel, Fire, 0.5f);
        Set(Steel, Water, 0.5f); Set(Steel, Electric, 0.5f); Set(Steel, Ice, 2f); Set(Steel, Fairy, 2f);

        Set(Fire, Rock, 0.5f); Set(Fire, Bug, 2f); Set(Fire, Steel, 2f); Set(Fire, Fire, 0.5f);
        Set(Fire, Water, 0.5f); Set(Fire, Grass, 2f); Set(Fire, Ice, 2f); Set(Fire, Dragon, 0.5f);

        Set(Water, Ground, 2f); Set(Water, Rock, 2f); Set(Water, Fire, 2f); Set(Water, Water, 0.5f);
        Set(Water, Grass, 0.5f); Set(Water, Dragon, 0.5f);

        Set(Grass, Flying, 0.5f); Set(Grass, Poison, 0.5f); Set(Grass, Ground, 2f); Set(Grass, Rock, 2f);
        Set(Grass, Bug, 0.5f); Set(Grass, Steel, 0.5f); Set(Grass, Fire, 0.5f); Set(Grass, Water, 2f);
        Set(Grass, Grass, 0.5f); Set(Grass, Dragon, 0.5f);

        Set(Electric, Flying, 2f); Set(Electric, Ground, 0f); Set(Electric, Water, 2f);
        Set(Electric, Grass, 0.5f); Set(Electric, Electric, 0.5f); Set(Electric, Dragon, 0.5f);

        Set(Psychic, Fighting, 2f); Set(Psychic, Poison, 2f); Set(Psychic, Steel, 0.5f);
        Set(Psychic, Psychic, 0.5f); Set(Psychic, Dark, 0f);

        Set(Ice, Flying, 2f); Set(Ice, Ground, 2f); Set(Ice, Steel, 0.5f); Set(Ice, Fire, 0.5f);
        Set(Ice, Water, 0.5f); Set(Ice, Grass, 2f); Set(Ice, Ice, 0.5f); Set(Ice, Dragon, 2f);

        Set(Dragon, Steel, 0.5f); Set(Dragon, Dragon, 2f); Set(Dragon, Fairy, 0f);

        Set(Dark, Fighting, 0.5f); Set(Dark, Ghost, 2f); Set(Dark, Psychic, 2f);
        Set(Dark, Dark, 0.5f); Set(Dark, Fairy, 0.5f);

        Set(Fairy, Fighting, 2f); Set(Fairy, Poison, 0.5f); Set(Fairy, Steel, 0.5f);
        Set(Fairy, Fire, 0.5f); Set(Fairy, Dragon, 2f); Set(Fairy, Dark, 2f);

        return c;
    }
}

// Encodes a single ability's placement bias/constraint from the manifesto.
public class AbilityBiasRule
{
    public HashSet<int> BiasedTypes { get; set; }               // Types this ability should favor
    public HashSet<int> ExcludedTypes { get; set; }             // Types this ability must NOT be on
    public int MinAtkStat { get; set; } = -1;                   // Minimum Attack stat (or -1 = no constraint)
    public int MaxAtkStat { get; set; } = int.MaxValue;         // Maximum Attack stat
    public int MinSpaStat { get; set; } = -1;                   // Minimum Special Attack stat
    public int MinSpeStat { get; set; } = -1;                   // Minimum Speed stat
    public int MinDefStat { get; set; } = -1;
    public int MaxDefStat { get; set; } = int.MaxValue;
    public int MinSpdStat { get; set; } = -1;
    public int MaxSpdStat { get; set; } = int.MaxValue;
    public int MinHpStat { get; set; } = -1;
    public int MaxHpStat { get; set; } = int.MaxValue;
    public int MaxBST { get; set; } = int.MaxValue;             // BST cap (e.g. Moody < 500)
    public int MinAtkOrSpaStat { get; set; } = -1;              // Min of EITHER Atk or SpA
    public HashSet<string> RequiredMovePool { get; set; }       // Must learn N+ of these moves
    public int RequiredMoveCount { get; set; } = 0;             // How many from RequiredMovePool
    public bool RequireSTABFromPool { get; set; } = false;      // 1 STAB from pool satisfies count-1
    public bool FinalStageOnly { get; set; } = false;           // Only on final-stage Pokemon
    public int SpeciesLock { get; set; } = -1;                  // Locked to a specific species ID
    public bool RequireHigherAtkForBias { get; set; } = false;  // Bias if Atk > SpA >= threshold
    public bool RequireHigherSpaForBias { get; set; } = false;  // Bias if SpA > Atk >= threshold
}

public static class CompetitiveDatabase
{
    public const int HP = 0, ATK = 1, DEF = 2, SPE = 3, SPA = 4, SPD = 5;

    public static readonly HashSet<string> CompetitiveAbilities = new(GameNameComparer.Instance)
    {
        "Adaptability", "Aerilate", "Anger Shell", "Arena Trap", "Beads of Ruin", "Beast Boost", "Bulletproof",
        "Chilling Neigh", "Chlorophyll", "Comatose", "Compound Eyes", "Contrary", "Dark Aura", "Dauntless Shield",
        "Defiant", "Delta Stream", "Desolate Land", "Disguise", "Download", "Dragonize", "Dragon's Maw", "Drizzle",
        "Drought", "Dry Skin", "Earth Eater", "Electric Surge", "Electromorphosis", "Fairy Aura", "Filter", "Fire Mane",
        "Flash Fire", "Fluffy", "Fur Coat", "Galvanize", "Good as Gold", "Gorilla Tactics", "Grassy Surge", "Grim Neigh",
        "Guts", "Hadron Engine", "Huge Power", "Hustle", "Ice Scales", "Imposter", "Innards Out", "Intimidate",
        "Intrepid Sword", "Iron Fist", "Levitate", "Libero", "Lightning Rod", "Magic Bounce", "Magic Guard", "Magnet Pull",
        "Mega Launcher", "Mega Sol", "Misty Surge", "Mold Breaker", "Moody", "Motor Drive", "Moxie", "Multiscale",
        "Neutralizing Gas", "No Guard", "Opportunist", "Orichalcum Pulse", "Parental Bond", "Pixilate", "Poison Heal",
        "Prankster", "Primordial Sea", "Prism Armor", "Protean", "Protosynthesis", "Psychic Surge", "Punk Rock",
        "Pure Power", "Purifying Salt", "Quark Drive", "Reckless", "Refrigerate", "Regenerator", "Rock Head",
        "Rocky Payload", "Sand Rush", "Sand Stream", "Sap Sipper", "Scrappy", "Mind's Eye", "Serene Grace",
        "Shadow Shield", "Shadow Tag", "Sharpness", "Shed Skin", "Sheer Force", "Simple", "Skill Link", "Slush Rush",
        "Snow Warning", "Solid Rock", "Soul-Heart", "Spicy Spray", "Speed Boost", "Stakeout", "Stamina", "Steelworker",
        "Steely Spirit", "Storm Drain", "Strong Jaw", "Sturdy", "Supreme Overlord", "Surge Surfer", "Swift Swim",
        "Sword of Ruin", "Tablets of Ruin", "Technician", "Teravolt", "Thick Fat", "Tinted Lens", "Tough Claws",
        "Toxic Boost", "Toxic Chain", "Toxic Debris", "Transistor", "Triage", "Turboblaze", "Unaware", "Unburden",
        "Vessel of Ruin", "Volt Absorb", "Water Absorb", "Water Bubble", "Well-Baked Body", "Wind Rider", "Wonder Guard"
    };

    public static readonly HashSet<string> SituationalAbilities = new(GameNameComparer.Instance)
    {
        "Aftermath", "Analytic", "Armor Tail", "Berserk", "Blaze", "Cheek Pouch", "Clear Body", "Competitive",
        "Corrosion", "Cotton Down", "Cursed Body", "Dazzling", "Effect Spore", "Flame Body", "Flare Boost",
        "Full Metal Body", "Gooey", "Guard Dog", "Harvest", "Heatproof", "Immunity", "Infiltrator", "Iron Barbs",
        "Justified", "Limber", "Marvel Scale", "Mirror Armor", "Mummy", "Natural Cure", "Neuroforce", "Overcoat",
        "Overgrow", "Pastel Veil", "Poison Touch", "Pressure", "Queenly Majesty", "Quick Feet", "Rough Skin",
        "Sand Force", "Shield Dust", "Sniper", "Solar Power", "Soundproof", "Static", "Swarm", "Thermal Exchange",
        "Torrent", "Trace", "Victory Star", "Weak Armor", "White Smoke", "Wonder Skin"
    };

    public static readonly HashSet<string> LessCompetitiveAbilities = new(GameNameComparer.Instance)
    {
        "Air Lock", "Anticipation", "Cloud Nine", "Rain Dish", "Super Luck"
    };

    public static readonly Dictionary<int, string> FormLockedAbilities = new()
    {
        { 778, "Disguise" },     // Mimikyu
        { 681, "Stance Change" }, // Aegislash
        { 877, "Hunger Switch" }, // Morpeko
        { 845, "Gulp Missile" },  // Cramorant
        { 875, "Ice Face" }       // Eiscue
    };

    public static readonly HashSet<int> SpecialFormStabilitySpecies = new()
    {
        681, // Aegislash (Stance Change)
        877, // Morpeko (Hunger Switch)
        845, // Cramorant (Gulp Missile)
        875  // Eiscue (Ice Face)
    };

    public static readonly Dictionary<string, HashSet<int>> TypeExclusiveAbilities = new(GameNameComparer.Instance)
    {
        { "Electromorphosis", new HashSet<int> { PokemonTypes.Electric } },
        { "Blaze", new HashSet<int> { PokemonTypes.Fire } },
        { "Overgrow", new HashSet<int> { PokemonTypes.Grass } },
        { "Torrent", new HashSet<int> { PokemonTypes.Water } },
        { "Swarm", new HashSet<int> { PokemonTypes.Bug } },
        { "Sand Force", new HashSet<int> { PokemonTypes.Rock, PokemonTypes.Ground, PokemonTypes.Steel } },
    };

    public static readonly Dictionary<string, AbilityBiasRule> AbilityBiasRules = BuildAbilityBiasRules();

    private static Dictionary<string, AbilityBiasRule> BuildAbilityBiasRules()
    {
        var rules = new Dictionary<string, AbilityBiasRule>(GameNameComparer.Instance);

        // -ate abilities: type bias
        rules["Aerilate"] = new() { BiasedTypes = new() { PokemonTypes.Flying } };
        rules["Galvanize"] = new() { BiasedTypes = new() { PokemonTypes.Electric } };
        rules["Pixilate"] = new() { BiasedTypes = new() { PokemonTypes.Fairy } };
        rules["Refrigerate"] = new() { BiasedTypes = new() { PokemonTypes.Ice } };
        rules["Dragonize"] = new() { BiasedTypes = new() { PokemonTypes.Dragon } };
        rules["Dragon's Maw"] = new() { BiasedTypes = new() { PokemonTypes.Dragon } };

        // SpA-biased abilities: SpA >= 90
        foreach (string a in new[] { "Beads of Ruin", "Competitive", "Grim Neigh", "Hadron Engine", "Solar Power", "Soul-Heart", "Berserk" })
            rules[a] = new() { MinSpaStat = 90, RequireHigherSpaForBias = true };

        // Atk-biased abilities: Atk >= 90
        foreach (string a in new[] { "Chilling Neigh", "Defiant", "Hustle", "Guard Dog", "Guts", "Intrepid Sword", "Moxie", "Orichalcum Pulse", "Sword of Ruin", "Tough Claws", "Toxic Boost" })
            rules[a] = new() { MinAtkStat = 90, RequireHigherAtkForBias = true };

        // Speed Boost: final stage, Speed >= 90
        rules["Speed Boost"] = new() { MinSpeStat = 90, FinalStageOnly = true };

        // Fluffy/Fur Coat: final stage, Def 50-100
        rules["Fluffy"] = new() { MinDefStat = 50, MaxDefStat = 100, FinalStageOnly = true };
        rules["Fur Coat"] = new() { MinDefStat = 50, MaxDefStat = 100, FinalStageOnly = true };

        // Tablets of Ruin: final stage, Def 50-130
        rules["Tablets of Ruin"] = new() { MinDefStat = 50, MaxDefStat = 130, FinalStageOnly = true };

        // Vessel of Ruin: final stage, SpD 50-130
        rules["Vessel of Ruin"] = new() { MinSpdStat = 50, MaxSpdStat = 130, FinalStageOnly = true };

        // Atk AND/OR SpA > 90 biased abilities
        foreach (string a in new[] { "Adaptability", "Supreme Overlord", "Sniper", "Download", "Anger Shell", "Technician", "Tinted Lens", "Transistor", "Steelworker", "Steely Spirit", "Stakeout", "Neuroforce", "Fire Mane" })
            rules[a] = new() { MinAtkOrSpaStat = 90, FinalStageOnly = true };

        // Anger Shell: offensive + frail (additional flag, combined with above)
        // Already in the list above; frailty check done at assignment time.

        // Contrary: requires specific stat-drop moves
        rules["Contrary"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Superpower", "Leaf Storm", "Draco Meteor", "Overheat", "Ice Hammer", "Hammer Arm",
                "Close Combat", "Dragon Ascent", "Armor Cannon", "V-create", "Headlong Rush",
                "Fleur Cannon", "Make It Rain", "Psycho Boost", "Spin Out"
            },
            RequiredMoveCount = 1
        };

        // Type-aura abilities
        rules["Fairy Aura"] = new() { BiasedTypes = new() { PokemonTypes.Fairy } };
        rules["Dark Aura"] = new() { BiasedTypes = new() { PokemonTypes.Dark } };

        // Surge/Terrain abilities: type bias
        rules["Electric Surge"] = new() { BiasedTypes = new() { PokemonTypes.Electric } };
        rules["Electromorphosis"] = new() { BiasedTypes = new() { PokemonTypes.Electric } };
        rules["Transistor"] = new() { BiasedTypes = new() { PokemonTypes.Electric }, MinAtkOrSpaStat = 90 };
        rules["Grassy Surge"] = new() { BiasedTypes = new() { PokemonTypes.Grass } };
        rules["Overgrow"] = new() { BiasedTypes = new() { PokemonTypes.Grass } };
        rules["Misty Surge"] = new() { BiasedTypes = new() { PokemonTypes.Fairy } };
        rules["Psychic Surge"] = new() { BiasedTypes = new() { PokemonTypes.Psychic } };

        // Weather abilities: type bias
        rules["Primordial Sea"] = new() { BiasedTypes = new() { PokemonTypes.Water } };
        rules["Drizzle"] = new() { BiasedTypes = new() { PokemonTypes.Water } };
        rules["Torrent"] = new() { BiasedTypes = new() { PokemonTypes.Water } };
        rules["Desolate Land"] = new() { BiasedTypes = new() { PokemonTypes.Fire } };
        rules["Drought"] = new() { BiasedTypes = new() { PokemonTypes.Fire } };
        rules["Blaze"] = new() { BiasedTypes = new() { PokemonTypes.Fire } };
        rules["Sand Stream"] = new() { BiasedTypes = new() { PokemonTypes.Rock, PokemonTypes.Ground, PokemonTypes.Steel } };
        rules["Snow Warning"] = new() { BiasedTypes = new() { PokemonTypes.Ice } };

        // Immunity-type abilities: must NOT be the immune type
        rules["Toxic Boost"] = new() { ExcludedTypes = new() { PokemonTypes.Poison, PokemonTypes.Steel }, MinAtkStat = 90 };
        rules["Immunity"] = new() { ExcludedTypes = new() { PokemonTypes.Poison, PokemonTypes.Steel } };
        rules["Poison Heal"] = new() { ExcludedTypes = new() { PokemonTypes.Poison, PokemonTypes.Steel } };
        rules["Earth Eater"] = new() { ExcludedTypes = new() { PokemonTypes.Flying } };
        rules["Levitate"] = new() { ExcludedTypes = new() { PokemonTypes.Flying } };
        rules["Volt Absorb"] = new() { ExcludedTypes = new() { PokemonTypes.Ground } };
        rules["Motor Drive"] = new() { ExcludedTypes = new() { PokemonTypes.Ground } };
        rules["Heatproof"] = new() { ExcludedTypes = new() { PokemonTypes.Fire } };
        rules["Purifying Salt"] = new() { ExcludedTypes = new() { PokemonTypes.Normal } };

        // BST cap
        rules["Moody"] = new() { MaxBST = 499 };

        // Attack stat caps
        rules["Huge Power"] = new() { MaxAtkStat = 80 };
        rules["Pure Power"] = new() { MaxAtkStat = 80 };
        rules["Gorilla Tactics"] = new() { MaxAtkStat = 140 };

        // Iron Fist: 2+ punch moves, Atk >= 90
        rules["Iron Fist"] = new()
        {
            MinAtkStat = 90,
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Bullet Punch", "Comet Punch", "Dizzy Punch", "Double Iron Bash", "Drain Punch", "Dynamic Punch",
                "Fire Punch", "Focus Punch", "Hammer Arm", "Headlong Rush", "Ice Hammer", "Ice Punch", "Jet Punch",
                "Mach Punch", "Mega Punch", "Meteor Mash", "Plasma Fists", "Power-Up Punch", "Rage Fist", "Shadow Punch",
                "Sky Uppercut", "Surging Strikes", "Thunder Punch", "Wicked Blow"
            },
            RequiredMoveCount = 2, RequireSTABFromPool = true
        };

        // Mega Sol: 2+ sun-affected moves favoring better attacking stat
        rules["Mega Sol"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Solar Beam", "Solar Blade", "Growth", "Weather Ball", "Moonlight", "Synthesis", "Morning Sun", "Hydro Steam"
            },
            RequiredMoveCount = 2
        };

        // Sharpness: 2+ slicing moves
        rules["Sharpness"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Aerial Ace", "Air Cutter", "Air Slash", "Aqua Cutter", "Behemoth Blade", "Bitter Blade", "Ceaseless Edge",
                "Cross Poison", "Cut", "Fury Cutter", "Kowtow Cleave", "Leaf Blade", "Mighty Cleave", "Night Slash",
                "Population Bomb", "Psyblade", "Psycho Cut", "Razor Leaf", "Razor Shell", "Sacred Sword", "Secret Sword",
                "Slash", "Solar Blade", "Stone Axe", "Tachyon Cutter", "X-Scissor"
            },
            RequiredMoveCount = 2, RequireSTABFromPool = true
        };

        // Ice Scales: final stage, SpD 50-100
        rules["Ice Scales"] = new() { MinSpdStat = 50, MaxSpdStat = 100, FinalStageOnly = true };

        // Iron Barbs, Stamina, Rough Skin: final stage, Def 90-200 + HP 50-200
        foreach (string a in new[] { "Iron Barbs", "Stamina", "Rough Skin" })
            rules[a] = new() { MinDefStat = 90, MaxDefStat = 200, MinHpStat = 50, MaxHpStat = 200, FinalStageOnly = true };

        // Punk Rock: 2+ sound moves
        rules["Punk Rock"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Alluring Voice", "Boomburst", "Bug Buzz", "Chatter", "Clanging Scales", "Echoed Voice", "Eerie Spell",
                "Hyper Voice", "Overdrive", "Psychic Noise", "Relic Song", "Round", "Snarl", "Sparkling Aria", "Torch Song", "Uproar"
            },
            RequiredMoveCount = 2, RequireSTABFromPool = true
        };

        // Analytic: Speed <= 80 (full line)
        rules["Analytic"] = new() { MaxDefStat = int.MaxValue }; // Speed constraint checked at assignment time
        // Imposter: final stage, HP < 70
        rules["Imposter"] = new() { MaxHpStat = 69, FinalStageOnly = true };

        // Reckless: 2+ recoil moves
        rules["Reckless"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Axe Kick", "Brave Bird", "Double-Edge", "Flare Blitz", "Head Charge", "Head Smash", "High Jump Kick",
                "Jump Kick", "Light of Ruin", "Submission", "Supercell Slam", "Take Down", "Volt Tackle", "Wave Crash",
                "Wild Charge", "Wood Hammer"
            },
            RequiredMoveCount = 2, RequireSTABFromPool = true
        };

        // No Guard: 2+ attacking moves with acc <= 80 and BP >= 100
        // This requires runtime move data inspection; the move pool here is a guide set
        rules["No Guard"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Hydro Pump", "Fire Blast", "Blizzard", "Thunder", "Focus Blast", "Stone Edge",
                "Megahorn", "Gunk Shot", "Zap Cannon", "Dynamic Punch", "Hurricane", "High Jump Kick"
            },
            RequiredMoveCount = 2, RequireSTABFromPool = true
        };

        // Strong Jaw: 2+ biting moves
        rules["Strong Jaw"] = new()
        {
            RequiredMovePool = new(GameNameComparer.Instance)
            {
                "Bite", "Crunch", "Fire Fang", "Fishious Rend", "Hyper Fang", "Ice Fang",
                "Jaw Lock", "Poison Fang", "Psychic Fangs", "Thunder Fang"
            },
            RequiredMoveCount = 2, RequireSTABFromPool = true
        };

        // Scrappy / Mind's Eye: Normal or Fighting type, Atk or SpA > 90
        rules["Scrappy"] = new()
        {
            BiasedTypes = new() { PokemonTypes.Normal, PokemonTypes.Fighting },
            MinAtkOrSpaStat = 90
        };
        rules["Mind's Eye"] = new()
        {
            BiasedTypes = new() { PokemonTypes.Normal, PokemonTypes.Fighting },
            MinAtkOrSpaStat = 90
        };

        // Disguise locked to Mimikyu
        rules["Disguise"] = new() { SpeciesLock = 778 };

        return rules;
    }

    public static readonly HashSet<int> MegaSpeciesIDs = new()
    {
        // Gen 1
        3, 6, 9, 15, 18, 26, 36, 65, 71, 80, 94, 115, 121, 127, 130, 142, 149, 150,
        // Gen 2
        154, 160, 181, 208, 212, 214, 227, 229, 248,
        // Gen 3
        254, 257, 260, 282, 302, 303, 304, 308, 310, 319, 323, 334, 354, 358, 359, 362, 373, 376, 380, 381, 384,
        // Gen 4
        398, 428, 445, 448, 460, 475, 478, 485, 491,
        // Gen 5
        500, 530, 545, 560, 604, 609, 623, 631,
        // Gen 6
        652, 655, 658, 668, 671, 678, 687, 691, 693, 701, 718, 719,
        // Gen 7
        740, 768, 780, 801, 807,
        // Gen 8
        870,
        // Gen 9
        952, 970, 990, 998
    };

    public static readonly Dictionary<int, string[]> MegaStoneMap = new()
    {
        { 3, new[] { "Venusaurite" } }, { 6, new[] { "Charizardite X", "Charizardite Y" } },
        { 9, new[] { "Blastoisinite" } }, { 15, new[] { "Beedrillite" } }, { 18, new[] { "Pidgeotite" } },
        { 26, new[] { "Raichunite X", "Raichunite Y" } }, { 36, new[] { "Clefablite" } },
        { 65, new[] { "Alakazite" } }, { 71, new[] { "Victreebelite" } }, { 80, new[] { "Slowbronite" } },
        { 94, new[] { "Gengarite" } }, { 115, new[] { "Kangaskhanite" } }, { 121, new[] { "Starminite" } },
        { 127, new[] { "Pinsirite" } }, { 130, new[] { "Gyaradosite" } }, { 142, new[] { "Aerodactylite" } },
        { 149, new[] { "Dragoninite" } }, { 150, new[] { "Mewtwonite X", "Mewtwonite Y" } },
        { 154, new[] { "Meganiumite" } }, { 160, new[] { "Feraligite" } }, { 181, new[] { "Ampharosite" } },
        { 208, new[] { "Steelixite" } }, { 212, new[] { "Scizorite" } }, { 214, new[] { "Heracronite" } },
        { 227, new[] { "Skarmorite" } }, { 229, new[] { "Houndoominite" } }, { 248, new[] { "Tyranitarite" } },
        { 254, new[] { "Sceptilite" } }, { 257, new[] { "Blazikenite" } }, { 260, new[] { "Swampertite" } },
        { 282, new[] { "Gardevoirite" } }, { 302, new[] { "Sablenite" } }, { 303, new[] { "Mawilite" } },
        { 306, new[] { "Aggronite" } }, { 308, new[] { "Medichamite" } }, { 310, new[] { "Manectite" } },
        { 319, new[] { "Sharpedonite" } }, { 323, new[] { "Cameruptite" } }, { 334, new[] { "Altarianite" } },
        { 354, new[] { "Banettite" } }, { 358, new[] { "Chimechite" } },
        { 359, new[] { "Absolite", "Absolite Z" } }, { 362, new[] { "Glalitite" } },
        { 373, new[] { "Salamencite" } }, { 376, new[] { "Metagrossite" } },
        { 380, new[] { "Latiasite" } }, { 381, new[] { "Latiosite" } },
        { 398, new[] { "Staraptite" } }, { 428, new[] { "Lopunnite" } },
        { 445, new[] { "Garchompite", "Garchompite Z" } },
        { 448, new[] { "Lucarionite", "Lucarionite Z" } },
        { 460, new[] { "Abomasite" } }, { 475, new[] { "Galladite" } },
        { 478, new[] { "Froslassite" } }, { 485, new[] { "Heatranite" } }, { 491, new[] { "Darkranite" } },
        { 500, new[] { "Emboarite" } }, { 530, new[] { "Excadrite" } }, { 545, new[] { "Scolipite" } },
        { 560, new[] { "Scraftinite" } }, { 604, new[] { "Eelektrossite" } }, { 609, new[] { "Chandelurite" } },
        { 623, new[] { "Golurkite" } }, { 531, new[] { "Audinite" } },
        { 652, new[] { "Chesnaughtite" } }, { 655, new[] { "Delphoxite" } }, { 658, new[] { "Greninjite" } },
        { 668, new[] { "Pyroarite" } }, { 670, new[] { "Floettite" } }, { 678, new[] { "Meowsticite" } },
        { 687, new[] { "Malamarite" } }, { 691, new[] { "Dragalgite" } }, { 689, new[] { "Barbaracite" } },
        { 701, new[] { "Hawluchanite" } }, { 718, new[] { "Zygardite" } }, { 719, new[] { "Diancite" } },
        { 740, new[] { "Crabominite" } }, { 768, new[] { "Golisopite" } }, { 780, new[] { "Drampanite" } },
        { 801, new[] { "Magearnite" } }, { 807, new[] { "Zeraorite" } },
        { 870, new[] { "Falinksite" } },
        { 952, new[] { "Scovillainite" } }, { 970, new[] { "Glimmoranite" } },
        { 978, new[] { "Tatsugirinite" } }, { 998, new[] { "Baxcalibrite" } },
    };

    /// <summary>
    /// Returns the table indices that are genuinely this species' Mega Evolution forms — NOT
    /// every alternate form. A Mega-capable species can also carry Expansion-Pack-added regional
    /// forms (e.g. a Galarian/Hisuian variant, or Raichu's pre-existing Alolan form) that must
    /// NOT be treated as Mega forms for BST-sync purposes. Since there's no explicit per-form
    /// "is Mega" flag anywhere in the data, this assumes Mega forms are the LAST N alternate
    /// forms (N = how many Mega Stones MegaStoneMap lists for this species) — i.e. any earlier
    /// alt forms are non-Mega and are left alone. This matches how the Expansion Pack's Mega
    /// additions are documented (appended on top of a species' existing forms).
    /// </summary>
    public static int[] GetMegaFormIndices(int species, PersonalInfo[] table)
    {
        if (table == null || species <= 0 || species >= table.Length) return [];
        if (!MegaStoneMap.TryGetValue(species, out var stones)) return [];

        var entry = table[species];
        int fc = entry?.FormeCount ?? 0;
        if (fc <= 1) return [];

        int stoneCount = stones.Length;
        int firstMegaForm = Math.Max(1, fc - stoneCount);

        var result = new List<int>();
        for (int form = firstMegaForm; form < fc; form++)
        {
            int formIdx = entry.FormeIndex(species, form);
            if (formIdx > 0 && formIdx < table.Length && formIdx != species)
                result.Add(formIdx);
        }
        return result.ToArray();
    }

    public static readonly Dictionary<int, string> TypeZCrystals = new()
    {
        { PokemonTypes.Normal, "Normalium Z" }, { PokemonTypes.Fighting, "Fightinium Z" },
        { PokemonTypes.Flying, "Flyinium Z" }, { PokemonTypes.Water, "Waterium Z" },
        { PokemonTypes.Fire, "Firium Z" }, { PokemonTypes.Grass, "Grassium Z" },
        { PokemonTypes.Rock, "Rockium Z" }, { PokemonTypes.Steel, "Steelium Z" },
        { PokemonTypes.Electric, "Electrium Z" }, { PokemonTypes.Ghost, "Ghostium Z" },
        { PokemonTypes.Psychic, "Psychium Z" }, { PokemonTypes.Bug, "Buginium Z" },
        { PokemonTypes.Dark, "Darkinium Z" }, { PokemonTypes.Poison, "Poisonium Z" },
        { PokemonTypes.Ground, "Groundium Z" }, { PokemonTypes.Ice, "Icium Z" },
        { PokemonTypes.Dragon, "Dragonium Z" }, { PokemonTypes.Fairy, "Fairium Z" },
    };

    // Species-specific Z-Crystals: species ID -> (Z-Crystal name, required move name)
    public static readonly Dictionary<int, (string Crystal, string Move)> SpeciesZCrystals = new()
    {
        { 25, ("Pikanium Z", "Volt Tackle") },       // Pikachu
        { 26, ("Aloraichium Z", "Thunderbolt") },     // Raichu (Alolan)
        { 133, ("Eevium Z", "Last Resort") },         // Eevee
        { 143, ("Snorlium Z", "Giga Impact") },       // Snorlax
        { 151, ("Mewnium Z", "Psychic") },            // Mew
        { 724, ("Decidium Z", "Spirit Shackle") },    // Decidueye
        { 727, ("Incinium Z", "Darkest Lariat") },    // Incineroar
        { 730, ("Primarium Z", "Sparkling Aria") },   // Primarina
        { 785, ("Tapunium Z", "Nature's Madness") },  // Tapu Koko (all Tapus)
        { 786, ("Tapunium Z", "Nature's Madness") },
        { 787, ("Tapunium Z", "Nature's Madness") },
        { 788, ("Tapunium Z", "Nature's Madness") },
        { 745, ("Lycanium Z", "Stone Edge") },        // Lycanroc
        { 784, ("Kommonium Z", "Clanging Scales") },  // Kommo-o
        { 778, ("Mimikium Z", "Play Rough") },        // Mimikyu
        { 791, ("Solganium Z", "Sunsteel Strike") },  // Solgaleo
        { 792, ("Lunalium Z", "Moongeist Beam") },    // Lunala
        { 800, ("Ultranecrozium Z", "Photon Geyser") }, // Necrozma
        { 802, ("Marshadium Z", "Spectral Thief") },  // Marshadow
    };

    public static readonly Dictionary<int, string> TypeBoostItems = new()
    {
        { PokemonTypes.Bug, "Silver Powder" }, { PokemonTypes.Steel, "Metal Coat" },
        { PokemonTypes.Ground, "Soft Sand" }, { PokemonTypes.Rock, "Hard Stone" },
        { PokemonTypes.Grass, "Miracle Seed" }, { PokemonTypes.Dark, "Black Glasses" },
        { PokemonTypes.Electric, "Magnet" }, { PokemonTypes.Water, "Mystic Water" },
        { PokemonTypes.Flying, "Sharp Beak" }, { PokemonTypes.Poison, "Poison Barb" },
        { PokemonTypes.Ice, "Never-Melt Ice" }, { PokemonTypes.Ghost, "Spell Tag" },
        { PokemonTypes.Psychic, "Twisted Spoon" }, { PokemonTypes.Fire, "Charcoal" },
        { PokemonTypes.Dragon, "Dragon Fang" }, { PokemonTypes.Normal, "Silk Scarf" },
        { PokemonTypes.Fairy, "Fairy Feather" }, { PokemonTypes.Fighting, "Black Belt" },
    };

    // Type ID -> Arceus Plate name
    public static readonly Dictionary<int, string> TypePlateItems = new()
    {
        { PokemonTypes.Fire, "Flame Plate" }, { PokemonTypes.Water, "Splash Plate" },
        { PokemonTypes.Electric, "Zap Plate" }, { PokemonTypes.Grass, "Meadow Plate" },
        { PokemonTypes.Ice, "Icicle Plate" }, { PokemonTypes.Fighting, "Fist Plate" },
        { PokemonTypes.Poison, "Toxic Plate" }, { PokemonTypes.Ground, "Earth Plate" },
        { PokemonTypes.Flying, "Sky Plate" }, { PokemonTypes.Psychic, "Mind Plate" },
        { PokemonTypes.Bug, "Insect Plate" }, { PokemonTypes.Rock, "Stone Plate" },
        { PokemonTypes.Ghost, "Spooky Plate" }, { PokemonTypes.Dragon, "Draco Plate" },
        { PokemonTypes.Dark, "Dread Plate" }, { PokemonTypes.Steel, "Iron Plate" },
    };

    // Resistance berries: type weakness -> berry name (Manifesto line 170)
    public static readonly Dictionary<int, string> ResistanceBerries = new()
    {
        { PokemonTypes.Fire, "Occa Berry" }, { PokemonTypes.Water, "Passho Berry" },
        { PokemonTypes.Electric, "Wacan Berry" }, { PokemonTypes.Grass, "Rindo Berry" },
        { PokemonTypes.Ice, "Yache Berry" }, { PokemonTypes.Fighting, "Chople Berry" },
        { PokemonTypes.Poison, "Kebia Berry" }, { PokemonTypes.Ground, "Shuca Berry" },
        { PokemonTypes.Flying, "Coba Berry" }, { PokemonTypes.Psychic, "Payapa Berry" },
        { PokemonTypes.Bug, "Tanga Berry" }, { PokemonTypes.Rock, "Charti Berry" },
        { PokemonTypes.Ghost, "Kasib Berry" }, { PokemonTypes.Dragon, "Haban Berry" },
        { PokemonTypes.Dark, "Colbur Berry" }, { PokemonTypes.Steel, "Babiri Berry" },
        { PokemonTypes.Fairy, "Roseli Berry" },
    };

    public static readonly HashSet<string> PunchingMoves = new(GameNameComparer.Instance)
    {
        "Bullet Punch", "Comet Punch", "Dizzy Punch", "Double Iron Bash", "Drain Punch", "Dynamic Punch",
        "Fire Punch", "Focus Punch", "Hammer Arm", "Headlong Rush", "Ice Hammer", "Ice Punch", "Jet Punch",
        "Mach Punch", "Mega Punch", "Meteor Mash", "Plasma Fists", "Power-Up Punch", "Rage Fist", "Shadow Punch",
        "Sky Uppercut", "Surging Strikes", "Thunder Punch", "Wicked Blow"
    };

    public static readonly HashSet<string> SlicingMoves = new(GameNameComparer.Instance)
    {
        "Aerial Ace", "Air Cutter", "Air Slash", "Aqua Cutter", "Behemoth Blade", "Bitter Blade", "Ceaseless Edge",
        "Cross Poison", "Crush Claw", "Cut", "Dire Claw", "Dragon Claw", "Fury Cutter", "Kowtow Cleave", "Leaf Blade",
        "Metal Claw", "Mighty Cleave", "Night Slash", "Population Bomb", "Psyblade", "Psycho Cut", "Razor Leaf",
        "Razor Shell", "Sacred Sword", "Secret Sword", "Shadow Claw", "Slash", "Solar Blade", "Stone Axe",
        "Tachyon Cutter", "X-Scissor"
    };

    public static readonly HashSet<string> SoundMoves = new(GameNameComparer.Instance)
    {
        "Alluring Voice", "Boomburst", "Bug Buzz", "Chatter", "Clanging Scales", "Echoed Voice", "Eerie Spell",
        "Hyper Voice", "Overdrive", "Psychic Noise", "Relic Song", "Round", "Snarl", "Sparkling Aria", "Torch Song", "Uproar"
    };

    public static readonly HashSet<string> RecoilMoves = new(GameNameComparer.Instance)
    {
        "Axe Kick", "Brave Bird", "Double-Edge", "Flare Blitz", "Head Charge", "Head Smash", "High Jump Kick",
        "Jump Kick", "Light of Ruin", "Submission", "Supercell Slam", "Take Down", "Volt Tackle", "Wave Crash",
        "Wild Charge", "Wood Hammer"
    };

    public static readonly HashSet<string> BitingMoves = new(GameNameComparer.Instance)
    {
        "Bite", "Crunch", "Fire Fang", "Fishious Rend", "Hyper Fang", "Ice Fang", "Jaw Lock", "Poison Fang",
        "Psychic Fangs", "Thunder Fang"
    };

    public static readonly HashSet<string> MultiHitMoves = new(GameNameComparer.Instance)
    {
        "Arm Thrust", "Bone Rush", "Bullet Seed", "Icicle Spear", "Pin Missile", "Population Bomb", "Rock Blast",
        "Scale Shot", "Water Shuriken", "Tail Slap", "Dual Wingbeat", "Double Iron Bash", "Surging Strikes"
    };

    public static readonly HashSet<string> HazardMoves = new(GameNameComparer.Instance)
    {
        "Stealth Rock", "Spikes", "Toxic Spikes", "Sticky Web", "Ceaseless Edge", "Stone Axe"
    };

    public static readonly HashSet<string> PivotMoves = new(GameNameComparer.Instance)
    {
        "U-turn", "Volt Switch", "Flip Turn", "Parting Shot", "Chilly Reception", "Teleport"
    };

    public static readonly HashSet<string> ScreenMoves = new(GameNameComparer.Instance)
    {
        "Light Screen", "Reflect", "Aurora Veil"
    };

    public static readonly HashSet<string> RecoveryMoves = new(GameNameComparer.Instance)
    {
        "Recover", "Roost", "Soft-Boiled", "Milk Drink", "Slack Off", "Shore Up", "Synthesis", "Morning Sun",
        "Moonlight", "Strength Sap", "Wish", "Rest", "Lunar Blessing"
    };

    public static readonly HashSet<string> SetupMoves = new(GameNameComparer.Instance)
    {
        "Swords Dance", "Dragon Dance", "Nasty Plot", "Calm Mind", "Quiver Dance", "Bulk Up", "Coil", "Shift Gear",
        "Shell Smash", "Rock Polish", "Agility", "Autotomize", "Clangorous Soul", "Fillet Away", "Victory Dance",
        "Tidy Up", "Take Heart", "Acid Armor", "Iron Defense", "Cotton Guard", "Belly Drum", "No Retreat",
        "Cosmic Power", "Defend Order", "Hone Claws", "Howl", "Growth", "Tail Glow", "Curse"
    };

    public static readonly HashSet<string> AtkSetupMoves = new(GameNameComparer.Instance)
    {
        "Swords Dance", "Dragon Dance", "Bulk Up", "Coil", "Belly Drum", "No Retreat", "Hone Claws",
        "Howl", "Shell Smash", "Shift Gear", "Victory Dance", "Tidy Up", "Fillet Away", "Clangorous Soul", "Growth"
    };

    public static readonly HashSet<string> SpaSetupMoves = new(GameNameComparer.Instance)
    {
        "Nasty Plot", "Calm Mind", "Quiver Dance", "Shell Smash", "Tail Glow", "Take Heart",
        "Fillet Away", "Clangorous Soul", "Growth", "No Retreat"
    };

    public static readonly HashSet<string> DefSetupMoves = new(GameNameComparer.Instance)
    {
        "Acid Armor", "Iron Defense", "Cotton Guard", "Bulk Up", "Coil", "Cosmic Power", "Defend Order", "Curse"
    };

    public static readonly HashSet<string> HighCritMoves = new(GameNameComparer.Instance)
    {
        "Aeroblast", "Aqua Cutter", "Attack Order", "Blaze Kick", "Crabhammer", "Cross Chop", "Cross Poison",
        "Drill Run", "Esper Wing", "Ivy Cudgel", "Leaf Blade", "Night Slash", "Poison Tail", "Psycho Cut",
        "Shadow Claw", "Slash", "Snipe Shot", "Spacial Rend", "Stone Edge", "Triple Arrows"
    };

    public static readonly HashSet<string> PulseMoves = new(GameNameComparer.Instance)
    {
        "Aura Sphere", "Dark Pulse", "Dragon Pulse", "Water Pulse", "Origin Pulse", "Terrain Pulse"
    };

    public static readonly HashSet<string> ContraryMoves = new(GameNameComparer.Instance)
    {
        "Superpower", "Leaf Storm", "Draco Meteor", "Overheat", "Ice Hammer", "Hammer Arm", "Close Combat",
        "Dragon Ascent", "Armor Cannon", "V-create", "Headlong Rush", "Fleur Cannon", "Make It Rain", "Psycho Boost", "Spin Out"
    };

    public static readonly HashSet<string> SunAffectedMoves = new(GameNameComparer.Instance)
    {
        "Solar Beam", "Solar Blade", "Growth", "Weather Ball", "Moonlight", "Synthesis", "Morning Sun", "Hydro Steam"
    };

    public static readonly HashSet<string> HealingAttackMoves = new(GameNameComparer.Instance)
    {
        "Bitter Blade", "Drain Punch", "Horn Leech", "Leech Life", "Draining Kiss", "Giga Drain", "Matcha Gotcha", "Oblivion Wing", "Parabolic Charge"
    };

    public static readonly HashSet<string> HighPowerLowAccMoves = new(GameNameComparer.Instance)
    {
        "Hydro Pump", "Fire Blast", "Blizzard", "Thunder", "Focus Blast", "Stone Edge", "Megahorn", "Gunk Shot",
        "Zap Cannon", "Dynamic Punch", "Hurricane", "High Jump Kick"
    };

    public static readonly HashSet<string> NormalAttacks = new(GameNameComparer.Instance)
    {
        "Return", "Frustration", "Hyper Voice", "Double-Edge", "Body Slam", "Facade", "Extreme Speed",
        "Quick Attack", "Tackle", "Slash", "Take Down", "Strength", "Tri Attack", "Boomburst", "Last Resort"
    };

    // Contact moves for Tough Claws validation
    public static readonly HashSet<string> ContactMoves = new(GameNameComparer.Instance)
    {
        "Accelerock", "Acrobatics", "Aqua Jet", "Aqua Step", "Aqua Tail", "Axe Kick", "Behemoth Blade",
        "Bitter Blade", "Blaze Kick", "Body Slam", "Bolt Strike", "Brave Bird", "Bullet Punch",
        "Circle Throw", "Close Combat", "Collision Course", "Crabhammer", "Cross Chop", "Cross Poison",
        "Crunch", "Darkest Lariat", "Dire Claw", "Double-Edge", "Double Iron Bash", "Dragon Ascent",
        "Dragon Claw", "Dragon Hammer", "Dragon Rush", "Drain Punch", "Drill Peck", "Drill Run",
        "Dual Wingbeat", "Dynamic Punch", "Earthquake", "Electro Drift", "Extreme Speed", "Facade",
        "Fake Out", "False Surrender", "Fire Fang", "Fire Lash", "Fire Punch", "First Impression",
        "Flare Blitz", "Flip Turn", "Flower Trick", "Fly", "Focus Punch", "Foul Play", "Giga Impact",
        "Glaive Rush", "Glacial Lance", "Hammer Arm", "Head Charge", "Head Smash", "Headlong Rush",
        "Heat Crash", "Heavy Slam", "High Horsepower", "High Jump Kick", "Hyper Drill",
        "Ice Fang", "Ice Hammer", "Ice Punch", "Ice Spinner", "Icicle Crash", "Iron Head", "Iron Tail",
        "Jaw Lock", "Jet Punch", "Knock Off", "Kowtow Cleave", "Lash Out", "Last Resort", "Leaf Blade",
        "Leech Life", "Liquidation", "Low Kick", "Lunge", "Mach Punch", "Megahorn", "Meteor Mash",
        "Mighty Cleave", "Mountain Gale", "Night Slash", "Outrage", "Play Rough", "Poison Fang",
        "Poison Jab", "Population Bomb", "Pounce", "Power Whip", "Precipice Blades", "Psyblade",
        "Psychic Fangs", "Psycho Cut", "Pyro Ball", "Quick Attack", "Rage Fist", "Raging Fury",
        "Rapid Spin", "Razor Shell", "Sacred Sword", "Scale Shot", "Seed Bomb", "Shadow Claw",
        "Shadow Sneak", "Slash", "Solar Blade", "Spirit Break", "Spirit Shackle", "Stone Axe",
        "Stomping Tantrum", "Sucker Punch", "Sunsteel Strike", "Supercell Slam", "Superpower",
        "Surging Strikes", "Tachyon Cutter", "Temper Flare", "Throat Chop", "Thunder Fang",
        "Thunder Punch", "Thunderous Kick", "Triple Axel", "Triple Dive", "Trop Kick", "U-turn",
        "Volt Tackle", "Waterfall", "Wave Crash", "Wicked Blow", "Wild Charge", "Wood Hammer",
        "X-Scissor", "Zen Headbutt", "Zing Zap"
    };

    // Status moves for Prankster, Assault Vest, Choice item validation
    public static readonly HashSet<string> UtilityStatusMoves = new(GameNameComparer.Instance)
    {
        "Stealth Rock", "Spikes", "Toxic Spikes", "Sticky Web", "Encore", "Glare", "Toxic", "Thunder Wave",
        "Will-O-Wisp", "Leech Seed", "Protect", "Spiky Shield", "King's Shield", "Burning Bulwark", "Silk Trap",
        "Taunt", "Wish", "Whirlwind", "Roar", "Light Screen", "Reflect", "Aurora Veil", "Trick Room",
        "Sleep Powder", "Spore", "Yawn", "Lovely Kiss", "Substitute", "Baton Pass", "Destiny Bond",
        "Healing Wish", "Lunar Dance", "Memento", "Tailwind", "Gravity", "Detect", "Baneful Bunker",
        "Rest", "Sleep Talk", "Shed Tail", "Disable", "Nuzzle", "Hone Claws"
    };

    public static readonly string[] MainShopItems =
    {
        "Poké Ball", "Great Ball", "Ultra Ball", "Quick Ball", "Full Restore", "Max Revive", "Max Elixir",
        "PP Max", "HP Up", "Protein", "Iron", "Carbos", "Calcium", "Zinc", "Ability Capsule",
        "Health Wing", "Muscle Wing", "Resist Wing", "Genius Wing", "Clever Wing", "Swift Wing",
        "Sitrus Berry", "Lum Berry", "Figy Berry", "Wiki Berry", "Mago Berry", "Aguav Berry", "Iapapa Berry",
        "Key Stone", "Bottle Cap", "Gold Bottle Cap", "Adrenaline Orb", "Max Repel", "Escape Rope"
    };

    public static readonly string[] CompetitiveShopItems =
    {
        "Air Balloon", "Assault Vest", "Choice Band", "Choice Scarf", "Choice Specs", "Eviolite",
        "Expert Belt", "Focus Sash", "Heavy-Duty Boots", "Leftovers", "Life Orb", "Loaded Dice",
        "Mental Herb", "Power Herb", "Rocky Helmet"
    };

    public static readonly string[] BerryShopItems =
    {
        "Pomeg Berry", "Kelpsy Berry", "Qualot Berry", "Hondew Berry", "Grepa Berry", "Tamato Berry",
        "Occa Berry", "Passho Berry", "Wacan Berry", "Rindo Berry", "Yache Berry", "Chople Berry",
        "Kebia Berry", "Shuca Berry", "Coba Berry", "Payapa Berry", "Tanga Berry", "Charti Berry",
        "Kasib Berry", "Haban Berry", "Colbur Berry", "Babiri Berry", "Chilan Berry",
        "Liechi Berry", "Ganlon Berry", "Petaya Berry", "Apicot Berry", "Salac Berry",
        "Lansat Berry", "Starf Berry", "Enigma Berry", "Micle Berry", "Custap Berry",
        "Jaboca Berry", "Rowap Berry"
    };

    public static readonly string[] TypeBoostShopItems =
    {
        "Silver Powder", "Metal Coat", "Soft Sand", "Hard Stone", "Miracle Seed", "Black Glasses",
        "Magnet", "Mystic Water", "Sharp Beak", "Poison Barb", "Never-Melt Ice", "Spell Tag",
        "Twisted Spoon", "Charcoal", "Dragon Fang", "Silk Scarf", "Fairy Feather"
    };

    public static readonly string[] PlateShopItems =
    {
        "Flame Plate", "Splash Plate", "Zap Plate", "Meadow Plate", "Icicle Plate", "Fist Plate",
        "Toxic Plate", "Earth Plate", "Sky Plate", "Mind Plate", "Insect Plate", "Stone Plate",
        "Spooky Plate", "Draco Plate", "Dread Plate", "Iron Plate"
    };

    public static readonly string[] EvoStoneShopItems =
    {
        "Sun Stone", "Moon Stone", "Fire Stone", "Water Stone", "Thunder Stone", "Leaf Stone",
        "Shiny Stone", "Dusk Stone", "Dawn Stone", "Ice Stone"
    };

    public static readonly string[] EvoItemShopItems =
    {
        "Oval Stone", "Razor Claw", "Razor Fang", "Auspicious Armor", "Black Augurite",
        "Chipped Pot", "Cracked Pot", "Galarica Cuff", "Galarica Wreath", "Malicious Armor",
        "Masterpiece Cup", "Unremarkable Cup", "Peat Block", "Sweet Apple", "Tart Apple",
        "Syrupy Apple", "Strawberry Sweet", "Love Sweet", "Berry Sweet", "Clover Sweet",
        "Flower Sweet", "Star Sweet", "Ribbon Sweet", "Leader's Crest", "Gimmighoul Coin"
    };

    public static readonly string[] MegaStoneShopXYORAS =
    {
        "Venusaurite", "Charizardite X", "Charizardite Y", "Blastoisinite", "Alakazite", "Gengarite",
        "Kangaskhanite", "Pinsirite", "Gyaradosite", "Aerodactylite", "Mewtwonite X", "Mewtwonite Y",
        "Ampharosite", "Scizorite", "Heracronite", "Houndoominite", "Tyranitarite", "Blazikenite",
        "Gardevoirite", "Mawilite", "Aggronite", "Medichamite", "Banettite", "Absolite", "Latiasite",
        "Latiosite", "Garchompite", "Lucarionite", "Abomasite", "Beedrillite", "Pidgeotite",
        "Slowbronite", "Steelixite", "Sceptilite", "Swampertite", "Sablenite", "Sharpedonite",
        "Cameruptite", "Altarianite", "Glalitite", "Salamencite", "Metagrossite", "Lopunnite",
        "Galladite", "Audinite", "Diancite"
    };

    public static readonly string[] MegaStoneShopZA =
    {
        "Clefablite", "Victreebelite", "Starminite", "Dragoninite", "Meganiumite", "Feraligite",
        "Skarmorite", "Froslassite", "Emboarite", "Excadrite", "Scolipite", "Scraftinite",
        "Eelektrossite", "Chandelurite", "Chesnaughtite", "Delphoxite", "Greninjite", "Pyroarite",
        "Floettite", "Malamarite", "Barbaracite", "Dragalgite", "Hawluchanite", "Zygardite",
        "Drampanite", "Falinksite", "Raichunite X", "Raichunite Y", "Chimechite", "Absolite Z",
        "Staraptite", "Garchompite Z", "Lucarionite Z", "Heatranite", "Darkranite", "Golurkite",
        "Meowsticite", "Crabominite", "Golisopite", "Magearnite", "Zeraorite", "Scovillainite",
        "Glimmoranite", "Tatsugirinite", "Baxcalibrite"
    };

    public static readonly string[] LegendaryItemShop =
    {
        "Blue Orb", "Red Orb", "Adamant Orb", "Adamant Crystal", "Lustrous Orb", "Lustrous Globe",
        "Griseous Orb", "Griseous Core", "DNA Splicers", "Reveal Glass", "Prison Bottle",
        "Zygarde Cube", "N-Solarizer", "N-Lunarizer", "Soul Dew", "Shock Drive", "Burn Drive",
        "Chill Drive", "Douse Drive", "Rusted Sword", "Rusted Shield", "Darkness Scroll",
        "Scroll of Waters", "Reins of Unity", "Wellspring Mask", "Hearthflame Mask",
        "Cornerstone Mask", "Booster Energy", "Eternatusite", "Tera Crystal"
    };

    public static readonly string[] FossilShopItems =
    {
        "Root Fossil", "Claw Fossil", "Helix Fossil", "Dome Fossil", "Old Amber",
        "Armor Fossil", "Skull Fossil", "Cover Fossil", "Plume Fossil", "Jaw Fossil", "Sail Fossil"
    };

    public static readonly string[] MiscShopItems =
    {
        "Ability Shield", "Absorb Bulb", "Black Sludge", "Blunder Policy", "Clear Amulet",
        "Covert Cloak", "Damp Rock", "Electric Seed", "Eject Button", "Eject Pack",
        "Flame Orb", "Grassy Seed", "Heat Rock", "Icy Rock", "Iron Ball", "Lagging Tail",
        "Luminous Moss", "Metronome", "Mirror Herb", "Misty Seed", "Muscle Band",
        "Protective Pads", "Psychic Seed", "Punching Glove", "Ring Target", "Red Card",
        "Room Service", "Safety Goggles", "Shell Bell", "Smooth Rock", "Snowball",
        "Sticky Barb", "Terrain Extender", "Throat Spray", "Toxic Orb", "Utility Umbrella",
        "Weakness Policy", "White Herb", "Wide Lens", "Wise Glasses", "Zoom Lens"
    };

    public static readonly string[] GemShopItems =
    {
        "Normal Gem", "Fire Gem", "Water Gem", "Electric Gem", "Grass Gem", "Ice Gem",
        "Fighting Gem", "Poison Gem", "Ground Gem", "Flying Gem", "Psychic Gem", "Bug Gem",
        "Rock Gem", "Ghost Gem", "Dragon Gem", "Dark Gem", "Steel Gem", "Fairy Gem"
    };

    // All rotating shop lists for assignment to the 20 non-main shops
    public static readonly string[][] RotatingShopLists =
    {
        CompetitiveShopItems, BerryShopItems, TypeBoostShopItems, PlateShopItems,
        EvoStoneShopItems, EvoItemShopItems, MegaStoneShopXYORAS, MegaStoneShopZA,
        LegendaryItemShop, FossilShopItems, MiscShopItems, GemShopItems
        // TM shops are generated dynamically based on game TM count
    };

    public static readonly HashSet<string> CompetitiveMoves = new(GameNameComparer.Instance)
    {
        "Accelerock", "Acid Armor", "Acid Spray", "Acrobatics", "Aeroblast", "Agility", "Air Slash", "Alluring Voice", "Ally Switch",
        "Apple Acid", "Aqua Cutter", "Aqua Jet", "Aqua Step", "Aqua Tail", "Armor Cannon", "Astral Barrage", "Attack Order",
        "Aura Sphere", "Aura Wheel", "Aurora Veil", "Autotomize", "Avalanche", "Axe Kick", "Baneful Bunker", "Barb Barrage",
        "Baton Pass", "Beak Blast", "Beat Up", "Behemoth Bash", "Behemoth Blade", "Belly Drum", "Bitter Blade", "Bitter Malice",
        "Blaze Kick", "Bleakwind Storm", "Blizzard", "Blood Moon", "Blue Flare", "Body Press", "Body Slam", "Bolt Strike",
        "Boomburst", "Brave Bird", "Bug Bite", "Bug Buzz", "Bulk Up", "Bullet Punch", "Bullet Seed", "Burning Bulwark",
        "Burning Jealousy", "Calm Mind", "Ceaseless Edge", "Chilly Reception", "Chloroblast", "Circle Throw", "Clanging Scales",
        "Clangorous Soul", "Clear Smog", "Close Combat", "Coil", "Collision Course", "Cosmic Power", "Cotton Guard", "Counter",
        "Crabhammer", "Cross Chop", "Crunch", "Curse", "Dark Pulse", "Darkest Lariat", "Dazzling Gleam", "Defend Order",
        "Destiny Bond", "Detect", "Diamond Storm", "Dire Claw", "Disable", "Discharge", "Doom Desire", "Double-Edge",
        "Draco Meteor", "Dragon Ascent", "Dragon Claw", "Dragon Dance", "Dragon Darts", "Dragon Energy", "Dragon Hammer",
        "Dragon Pulse", "Dragon Rush", "Dragon Tail", "Drain Punch", "Draining Kiss", "Drill Peck", "Drill Run", "Drum Beating",
        "Dual Wingbeat", "Dynamax Cannon", "Dynamic Punch", "Earth Power", "Earthquake", "Eerie Spell", "Electro Drift",
        "Electro Shot", "Encore", "Endeavor", "Energy Ball", "Eruption", "Esper Wing", "Expanding Force", "Explosion",
        "Extrasensory", "Extreme Speed", "Facade", "Fake Out", "False Surrender", "Feint", "Fickle Beam", "Fiery Dance",
        "Fiery Wrath", "Fillet Away", "Final Gambit", "Fire Blast", "Fire Fang", "Fire Lash", "Fire Punch", "First Impression",
        "Flame Charge", "Flamethrower", "Flare Blitz", "Flash Cannon", "Fleur Cannon", "Fling", "Flip Turn", "Flower Trick",
        "Focus Blast", "Foul Play", "Freeze-Dry", "Freezing Glare", "Frost Breath", "Fusion Bolt",
        "Fusion Flare", "Future Sight", "Giga Drain", "Giga Impact", "Gigaton Hammer", "Glacial Lance", "Glaive Rush", "Glare",
        "Grass Knot", "Grassy Glide", "Grav Apple", "Gravity", "Growth", "Gunk Shot", "Gyro Ball", "Hammer Arm", "Head Smash",
        "Headlong Rush", "Healing Wish", "Heat Crash", "Heat Wave", "Heavy Slam", "Hex", "High Horsepower", "High Jump Kick",
        "Hone Claws", "Horn Drill", "Horn Leech", "Howl", "Hurricane", "Hydro Pump", "Hydro Steam", "Hyper Drill", "Hyper Voice",
        "Hyperspace Hole", "Ice Beam", "Ice Fang", "Ice Hammer", "Ice Punch", "Ice Shard", "Ice Spinner",
        "Icicle Crash", "Icicle Spear", "Infernal Parade", "Infestation", "Iron Defense", "Iron Head", "Iron Tail", "Ivy Cudgel",
        "Jaw Lock", "Jet Punch", "Judgment", "King's Shield", "Knock Off", "Kowtow Cleave", "Lash Out", "Last Resort",
        "Last Respects", "Lava Plume", "Leaf Blade", "Leaf Storm", "Leech Life", "Leech Seed", "Light Screen", "Liquidation",
        "Low Kick", "Lovely Kiss", "Lumina Crash", "Lunar Blessing", "Lunar Dance", "Lunge", "Luster Purge", "Mach Punch",
        "Magma Storm", "Make It Rain", "Malignant Chain", "Matcha Gotcha", "Megahorn", "Memento", "Metal Burst", "Meteor Beam",
        "Meteor Mash", "Mighty Cleave", "Milk Drink", "Minimize", "Mirror Coat", "Mist Ball", "Misty Explosion", "Moonblast",
        "Moongeist Beam", "Moonlight", "Morning Sun", "Mortal Spin", "Mountain Gale", "Mud Shot", "Muddy Water", "Mystical Fire",
        "Mystical Power", "Nasty Plot", "Night Daze", "Night Shade", "Night Slash", "No Retreat", "Nuzzle", "Oblivion Wing",
        "Origin Pulse", "Outrage", "Overdrive", "Overheat", "Parabolic Charge", "Parting Shot", "Payback", "Petal Blizzard",
        "Phantom Force", "Photon Geyser", "Pin Missile", "Play Rough", "Poison Fang", "Poison Jab", "Pollen Puff", "Poltergeist",
        "Population Bomb", "Pounce", "Power Gem", "Power Trip", "Power Whip", "Precipice Blades", "Protect", "Psyblade",
        "Psychic", "Psychic Fangs", "Psychic Noise", "Psycho Boost", "Psycho Cut", "Psyshield Bash", "Psyshock", "Psystrike",
        "Pyro Ball", "Quick Attack", "Quiver Dance", "Rage Fist", "Raging Fury", "Rapid Spin", "Razor Shell", "Recover",
        "Reflect", "Rest", "Revelation Dance", "Reversal", "Rising Voltage", "Roar", "Rock Blast", "Rock Polish", "Rock Slide",
        "Rock Tomb", "Roost", "Ruination", "Sacred Fire", "Sacred Sword", "Salt Cure", "Sandsear Storm", "Scald", "Scale Shot",
        "Scorching Sands", "Secret Sword", "Searing Shot", "Seed Bomb", "Seed Flare", "Seismic Toss", "Self-Destruct",
        "Shadow Ball", "Shadow Claw", "Shadow Force", "Shadow Sneak", "Shed Tail", "Shell Side Arm", "Shell Smash", "Shift Gear",
        "Shore Up", "Silk Trap", "Slack Off", "Sleep Powder", "Sleep Talk", "Sludge Bomb", "Sludge Wave", "Smack Down",
        "Snarl", "Snipe Shot", "Soft-Boiled", "Solar Beam", "Solar Blade", "Spacial Rend", "Sparkling Aria", "Spikes",
        "Spiky Shield", "Spin Out", "Spirit Break", "Spirit Shackle", "Spore", "Springtide Storm", "Stealth Rock",
        "Steam Eruption", "Steel Beam", "Sticky Web", "Stomping Tantrum", "Stone Axe", "Stone Edge", "Stored Power",
        "Strange Steam", "Strength Sap", "Substitute", "Sucker Punch", "Sunsteel Strike", "Super Fang", "Supercell Slam",
        "Superpower", "Surf", "Surging Strikes", "Switcheroo", "Swords Dance", "Synthesis", "Tachyon Cutter", "Tail Glow",
        "Tail Slap", "Tailwind", "Take Heart", "Taunt", "Temper Flare", "Thief", "Thousand Arrows", "Thousand Waves",
        "Throat Chop", "Thunder", "Thunder Cage", "Thunder Fang", "Thunder Punch", "Thunder Wave", "Thunderbolt", "Thunderclap",
        "Thunderous Kick", "Tidy Up", "Torch Song", "Toxic", "Toxic Spikes", "Trailblaze", "Tri Attack", "Trick Room",
        "Triple Arrows", "Trick", "Triple Axel", "Triple Dive", "Trop Kick", "Twin Beam", "U-turn", "Vacuum Wave",
        "Victory Dance", "Volt Switch", "Volt Tackle", "Water Pulse", "Water Shuriken", "Water Spout", "Waterfall", "Wave Crash",
        "Weather Ball", "Whirlwind", "Wicked Blow", "Wild Charge", "Wildbolt Storm", "Will-O-Wisp", "Wish", "Wood Hammer",
        "X-Scissor", "Yawn", "Zen Headbutt", "Zing Zap", "Ally Switch", "Follow Me", "Helping Hand", "Jungle Healing",
        "Life Dew", "Arm Thrust", "Charge Beam", "Dual Chop", "Happy Hour", "Celebrate", "Hold Hands", "Trick-or-Treat"
    };

    public static readonly HashSet<string> SituationalMoves = new(GameNameComparer.Instance)
    {
        "Focus Punch", "Freeze Shock", "Ice Burn"
    };

    // Primal reversion species (not Mega but similar)
    public static readonly Dictionary<int, string> PrimalReversionItems = new()
    {
        { 382, "Blue Orb" },  // Kyogre
        { 383, "Red Orb" },   // Groudon
    };

    // Healing berries for general use
    public static readonly string[] HealingBerries =
    {
        "Aguav Berry", "Iapapa Berry", "Mago Berry", "Wiki Berry", "Figy Berry", "Sitrus Berry"
    };

    // Two-turn charge moves for Power Herb
    public static readonly HashSet<string> TwoTurnMoves = new(GameNameComparer.Instance)
    {
        "Electro Shot", "Freeze Shock", "Geomancy", "Ice Burn", "Meteor Beam",
        "Phantom Force", "Shadow Force", "Sky Attack"
    };

    // Stat-drop moves for White Herb / Contrary validation
    public static readonly HashSet<string> StatDropMoves = new(GameNameComparer.Instance)
    {
        "Superpower", "Leaf Storm", "Draco Meteor", "Overheat", "Ice Hammer", "Hammer Arm",
        "Close Combat", "Dragon Ascent", "Armor Cannon", "V-create", "Headlong Rush",
        "Fleur Cannon", "Make It Rain", "Psycho Boost", "Spin Out"
    };
}
