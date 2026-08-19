#nullable enable

using System;
using System.Collections.Generic;

namespace pk3DS.Core.Structures;

/// <summary>
/// What each animation index in the move-animation GARC (a/0/8/8) actually plays.
/// </summary>
public static class MoveAnimationNames
{
    /// <summary>Highest animation index the vanilla GARC contains.</summary>
    public const int MaxIndex = 1236;

    /// <summary>Animation indices below this are the move table; at or above it, engine effects.</summary>
    public const int FirstEngineIndex = 729;

    /// <summary>
    /// First index the battle engine owns. Writing at or above this breaks the game.
    /// </summary>
    public const int FirstReservedIndex = 801;

    /// <summary>Whether writing a move's animation at this index would clobber the engine's own.</summary>
    public static bool IsEngineReserved(int index) => index >= FirstReservedIndex && index <= MaxIndex;

    /// <summary>
    /// A readable label for an animation index.
    /// </summary>
    /// <param name="index">Index into the animation GARC.</param>
    /// <param name="moveNames">The ROM's move names, used for indices that are moves.</param>
    public static string Describe(int index, string[]? moveNames)
    {
        if (index < 0 || index > MaxIndex) return $"{index} - (out of range)";

        if (index < FirstEngineIndex)
        {
            string? nm = moveNames != null && index < moveNames.Length ? moveNames[index] : null;
            if (!string.IsNullOrWhiteSpace(nm) && nm != "—" && nm != "———")
                return $"{index} - {nm}";
            return $"{index} - (unused move slot)";
        }

        return Engine.TryGetValue(index, out string? d) ? $"{index} - {d}" : $"{index} - (unused)";
    }

    /// <summary>Every index that is not a move, in order, for a picker.</summary>
    public static IEnumerable<KeyValuePair<int, string>> EngineAnimations => Engine;

    /// <summary>
    /// The engine's own animations, by index.
    /// </summary>
    private static readonly Dictionary<int, string> Engine = Build();

    private static Dictionary<int, string> Build()
    {
        var d = new Dictionary<int, string>
        {
            [801] = "Damage sound, no visual",
            [802] = "Pokemon level-up glow",
            [803] = "Shiny Pokemon sparkle",
            [804] = "Trainer uses item on Pokemon",
            [805] = "Pokemon uses own held item",
            [806] = "Pokemon eats held berry",
            [808] = "Pokemon transforms into target",
            [809] = "Pokemon transforms into target",
            [810] = "Pokemon transforms into target",
            [811] = "Hearts for Trainer come out of Pokemon",
            [812] = "Pokemon wiggles a bit; likes Trainer",
            [813] = "Pokemon hops up and down; loves Trainer",
            [814] = "Pokemon looks back at beloved Trainer",
            [815] = "Pokemon hit by weather",
            [816] = "Healing item sound, no visual",
            [817] = "Water+Fire Pledge: rainbow",
            [818] = "Fire+Grass Pledge: sea of fire",
            [819] = "Water+Grass Pledge: swamp",
            [820] = "Terrain effect vanishes",
            [824] = "Confusion hit",
            [825] = "Damage when battle scene turned off",
            [826] = "Ability trigger (fwoosh-ding)",
            [827] = "Ability trigger (fwoosh-ding)",
            [828] = "Poke Ball thrown & encloses target",
            [829] = "Poke Ball first wiggle",
            [830] = "Poke Ball second wiggle",
            [831] = "Poke Ball third wiggle",
            [832] = "Poke Ball successful catch click",
            [833] = "Poke Ball escaped before wiggle 1",
            [834] = "Poke Ball escaped after wiggle 1",
            [835] = "Poke Ball escaped after wiggle 2",
            [836] = "Poke Ball escaped after wiggle 3",
            [837] = "Poke Ball crit-thrown & wiggles once",
            [838] = "Poke Ball clicks for crit-capture",
            [839] = "Poke Ball escaped for crit-capture",
            [840] = "Poke Ball blocked by other Trainer",
            [843] = "Sleep ailment Z's",
            [844] = "Poison ailment bubbles",
            [845] = "Burn ailment flame",
            [846] = "Freeze ailment ice",
            [847] = "Paralyze ailment sparks",
            [848] = "Confuse ailment chirpies",
            [849] = "Attract ailment hearts",
            [850] = "Stat up",
            [851] = "Stat down",
            [852] = "Healing",
            [853] = "Substitute replaced by Pokemon",
            [854] = "Pokemon replaced by Substitute",
            [857] = "Rainy weather starts",
            [858] = "Hailing weather starts",
            [859] = "Sandstorm weather starts",
            [860] = "Sunny weather starts",
            [863] = "Pokemon creates Substitute",
            [864] = "Substitute vanishes",
            [867] = "Pokemon charging up",
            [868] = "Curse condition damage",
            [869] = "Nightmare condition damage",
            [870] = "Leech Seed condition damage",
            [871] = "Bind condition damage",
            [872] = "Wrap condition damage",
            [873] = "Fire Spin condition damage",
            [874] = "Magma Storm condition damage",
            [875] = "Clamp condition damage",
            [876] = "Whirlpool condition damage",
            [877] = "Sand Tomb condition damage",
            [878] = "Ingrain condition healing",
            [879] = "Infestation condition damage",
            [881] = "Healing Wish",
            [882] = "Lunar Dance",
            [883] = "Automatic center (triple battles)",
            [885] = "Pokemon jumps or is flung away",
            [893] = "Pokemon removed via Roar",
            [894] = "Pokemon removed via Dragon Tail",
            [896] = "Resets camera",
            [897] = "Resets camera over 9 frames",
            [898] = "Weather returns to normal",
            [899] = "Confusion hit (alt)",
            [900] = "Pokemon hit by Self-Destruct/Explosion",
            [901] = "Pokemon damaged by Powder",
            [902] = "Grassy Terrain appears",
            [903] = "Misty Terrain appears",
            [904] = "Electric Terrain appears",
            [905] = "Kyogre's heavy rain starts",
            [906] = "Groudon's drought starts",
            [907] = "Rayquaza's strong winds start",
            [908] = "Kyogre's Primal Reversion",
            [909] = "Groudon's Primal Reversion",
            [910] = "Fiery effect on user (Turboblaze?)",
            [911] = "Fire Trap laid",
            [912] = "Psychic Terrain appears",
            [913] = "Kyogre appearance",
            [914] = "Zygarde appearance",
            [915] = "Wishiwashi's Schooling activates",
            [916] = "Wishiwashi's Schooling deactivates",
            [917] = "Minior's Shields Down",
            [918] = "Mimikyu's Disguise busted",
            [919] = "Move Protected/Detected",
            [920] = "Spectral Thief stat steal",
            [921] = "Ability trigger (fwoosh-ding)",
            [922] = "Ability trigger (fwoosh-ding)",
            [923] = "Necrozma's Ultra Burst",
            [924] = "Rotom Power unlocked",
            [925] = "Z-Powered friendship support",
            [926] = "Healing",
            [927] = "Z-Move title appears",
            [928] = "Z-Move dance setup",
            [947] = "Pokemon Z-Move charge, no Trainer dance",
            [1012] = "Trainer brings back Pokemon",
            [1013] = "Trainer's Pokemon faints",
            [1014] = "Wild Pokemon faints",
            [1015] = "Trainer's Pokemon faints in Battle Royal",
            [1016] = "Trainer sends out Pokemon",
            [1017] = "Trainer sends out Pokemon (alt)",
            [1018] = "Trainer sends out Pokemon (with Trainer animation)",
            [1019] = "Trainer NPC loses battle",
            [1020] = "Razor Wind hit",
            [1021] = "Fly hit",
            [1022] = "Solar Beam hit",
            [1023] = "Dig hit",
            [1024] = "Bide hit",
            [1025] = "Skull Bash hit",
            [1026] = "Sky Attack hit",
            [1027] = "Explosion hit",
            [1028] = "Ghost-type Curse",
            [1029] = "Future Sight hit",
            [1030] = "Brick Break hits Reflect/Light Screen",
            [1031] = "Dive hit",
            [1032] = "Weather Ball - sunny",
            [1033] = "Weather Ball - hail",
            [1034] = "Weather Ball - sandstorm",
            [1035] = "Weather Ball - rain",
            [1036] = "Bounce hit",
            [1037] = "Covet steal",
            [1038] = "Doom Desire hit",
            [1039] = "Shadow Force hit",
            [1040] = "Sky Drop hit",
            [1041] = "Techno Blast - Water",
            [1042] = "Techno Blast - Electric",
            [1043] = "Techno Blast - Fire",
            [1044] = "Techno Blast - Ice",
            [1045] = "Freeze Shock hit",
            [1046] = "Ice Burn hit",
            [1047] = "Fusion Flare powered-up hit",
            [1048] = "Fusion Bolt powered-up hit",
            [1049] = "Phantom Force hit",
            [1050] = "Geomancy hit",
            [1051] = "Precipice Blades (alt)",
            [1052] = "Dragon Ascent (alt)",
            [1053] = "Baneful Bunker poison",
            [1054] = "Solar Blade hit",
            [1055] = "Pollen Puff heal",
            [1056] = "Psychic Fangs hits Reflect/Light Screen",
            [1057] = "Water Shuriken (Battle Bond)",
            [1058] = "Darkest Lariat (alt)",
            [1059] = "Sunsteel Strike (alt)",
            [1060] = "Moongeist Beam (alt)",
            [1061] = "Spirit Shackle (alt)",
            [1062] = "Sunsteel Strike (alt 2)",
            [1063] = "Moongeist Beam (alt 2)",
            [1067] = "Totem Pokemon intro",
            [1071] = "Ultra Beast intro",
            [1073] = "Totem Pokemon intro (alt)",
            [1074] = "Player sends out first Pokemon",
            [1075] = "Player sends out 2 Pokemon",
            [1076] = "Multi Battle - player's team sends out",
            [1082] = "SOS call",
            [1083] = "SOS ally appears",
            [1084] = "SOS ally doesn't appear",
            [1085] = "SOS call (alt)",
            [1086] = "Trainer battle intro",
            [1087] = "Double Trainer battle intro",
            [1088] = "Blue background Trainer battle intro",
            [1089] = "Blue background double Trainer battle intro",
            [1090] = "Hau battle intro",
            [1117] = "Kahili's battle intro",
            [1118] = "Acerola's battle intro",
            [1119] = "Multiplayer battle intro",
            [1120] = "Multiplayer Multi Battle intro",
            [1121] = "Wild intro - green grass",
            [1122] = "Wild intro - water",
            [1123] = "Wild intro - rocky",
            [1124] = "Wild intro - desert",
            [1125] = "Wild intro - other water",
            [1126] = "Wild intro - yellow flowers",
            [1127] = "Wild intro - red flowers",
            [1128] = "Wild intro - icy cave",
            [1129] = "Wild intro - dry grass",
            [1130] = "Wild intro - multicolour flowers",
            [1131] = "Trainer sends out 1st Pokemon",
            [1132] = "Trainer sends out Pokemon, not in Poke Ball",
            [1133] = "1 Trainer sends out 2 Pokemon",
            [1134] = "Player & ally send out 1st Pokemon",
            [1150] = "Sends out Pokemon without Poke Ball",
            [1161] = "Battle Royal intro (before Pokemon sent out)",
            [1162] = "Battle Royal intro (after Pokemon sent out)",
            [1163] = "Pokemon comes out of Poke Ball",
            [1164] = "Pokemon comes out of Poke Ball",
            [1165] = "Pokemon turns around to start Pokemon Refresh",
            [1166] = "Pokemon turns around after winning a Totem battle",
            [1167] = "Camera work",
            [1216] = "Reset basically everything",
            [1217] = "Rotomdex has never seen this Pokemon",
            [1218] = "Show the player Trainer posing",
            [1221] = "Ultra Necrozma's battle intro",
            [1228] = "Sends out Pokemon without Poke Ball",
            [1233] = "Plasma Fists (alt)",
            [1235] = "Mind Blown (alt)",
        };

        // Damage-sound-only entries, which are scattered but all say the same thing.
        foreach (int i in new[]
        {
            807, 821, 822, 823, 841, 842, 855, 856, 861, 862, 865, 866,
            880, 884, 886, 887, 888, 889, 890, 891, 892, 895,
        })
            d.TryAdd(i, "Damage sound, no visual");

        // The eighteen type Z-Move dances, 929..946, in the game's type order.
        string[] zTypes =
        [
            "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel",
            "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark", "Fairy",
        ];
        for (int i = 0; i < zTypes.Length; i++)
            d[929 + i] = $"{zTypes[i]} Z-Move dance + charge up";

        // The eighteen generic Z-Moves, 948..965.
        string[] zMoves =
        [
            "Breakneck Blitz", "All-Out Pummeling", "Supersonic Skystrike", "Acid Downpour",
            "Tectonic Rage", "Continental Crush", "Savage Spin-Out", "Never-Ending Nightmare",
            "Corkscrew Crash", "Inferno Overdrive", "Hydro Vortex", "Bloom Doom", "Gigavolt Havoc",
            "Shattered Psyche", "Subzero Slammer", "Devastating Drake", "Black Hole Eclipse",
            "Twinkle Tackle",
        ];
        for (int i = 0; i < zMoves.Length; i++)
            d[948 + i] = zMoves[i];

        // The exclusive Z-Moves, each a dance/charge index followed by the move itself.
        (int Index, string Name)[] exclusive =
        [
            (966, "Catastropika"), (968, "Extreme Evoboost"), (970, "Pulverizing Pancake"),
            (972, "Genesis Supernova"), (974, "Sinister Arrow Raid"), (976, "Malicious Moonsault"),
            (978, "Oceanic Operetta"), (980, "Stoked Sparksurfer"), (982, "Guardian of Alola"),
            (984, "Soul-Stealing 7-Star Strike"), (986, "10,000,000 Volt Thunderbolt"),
            (988, "Let's Snuggle Forever"), (990, "Let's Snuggle Forever (busted)"),
            (992, "Clangorous Soulblaze"), (994, "Clangorous Soulblaze (alt)"),
            (996, "Splintered Stormshards (day)"), (998, "Splintered Stormshards (night)"),
            (1000, "Splintered Stormshards (midday)"), (1002, "Menacing Moonraze Maelstrom"),
            (1004, "Menacing Moonraze Maelstrom (alt)"), (1006, "Searing Sunraze Smash"),
            (1008, "Searing Sunraze Smash (alt)"), (1010, "Light That Burns the Sky"),
        ];
        foreach (var (idx, name) in exclusive)
        {
            d[idx] = $"{name} dance + charge up";
            d[idx + 1] = name;
        }

        // Blocks that repeat one label. Written as ranges so a typo in the middle is impossible.
        void Fill(int from, int to, string label)
        {
            for (int i = from; i <= to; i++) d.TryAdd(i, label);
        }

        Fill(1064, 1073, "Wild Pokemon intro");
        Fill(1077, 1081, "Player/opponent sends out Pokemon");
        Fill(1091, 1116, "Trainer battle intro");
        Fill(1135, 1149, "Sends out 1st Pokemon");
        Fill(1151, 1160, "Trainer animation + camera work");
        Fill(1168, 1215, "Mega Evolution sequence");
        Fill(1219, 1232, "Battle intro");
        Fill(1234, 1236, "Wild Pokemon battle intro");

        return d;
    }
}
