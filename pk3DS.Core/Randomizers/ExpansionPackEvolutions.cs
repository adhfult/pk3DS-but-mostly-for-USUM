using System;
using System.Linq;
using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// The Expansion Pack mod (which adds Gen 8/9 species 808-1025, plus Galarian/Hisuian regional
/// forms and White-Striped Basculin, to a Gen 6/7 ROM) doesn't give a number of these Pokemon a
/// working evolution method. This adds one for each, every time a seed is generated — matching
/// their real mainline-game evolution method wherever the ROM's evolution format can represent
/// it (Method 4 = level up, 8 = use item, 21 = level up knowing a move, 22 = level up with a
/// specific species in the party, 33 = level up at night), and falling back to an equivalent
/// level requirement where it can't (move-use counters, step counters, battle counters, etc. —
/// each such substitution is called out below).
///
/// Every species/move/item is resolved BY NAME at runtime against this ROM's own text tables
/// rather than hardcoded IDs, since Expansion Pack species IDs can vary slightly by build. The
/// one exception is species from Rellor (species ~953) onward: pk3DS's own Gen8/9 name table
/// (Gen89SpeciesNames.Names808_1025) has a known off-by-one gap starting at Rellor's slot, so
/// name lookup for species at/after that point falls back to a hardcoded National Dex number.
/// Regional-form entries assume the added form is form index 1 (2 for White-Striped Basculin,
/// since Basculin already has 2 vanilla forms) — this matches how every existing Gen 7 regional
/// form (Alolan Raichu, etc.) is indexed, but wasn't verifiable against a live ROM; spot-check
/// in the Evolution Editor if an entry looks off.
/// </summary>
public static class ExpansionPackEvolutions
{
    private sealed class EvoDef
    {
        public string SourceName, TargetName;
        public int SourceForm = -1;
        public int Method, Level;
        public string ArgMoveName, ArgItemName, ArgPartySpeciesName;
        public int FallbackSourceId = -1, FallbackTargetId = -1; // used only if name lookup fails
    }

    private const int MethodLevelUp = 4;
    private const int MethodUseItem = 8;
    private const int MethodLevelUpKnowingMove = 21;
    private const int MethodLevelUpWithPartySpecies = 22;
    private const int MethodLevelUpAtNight = 33;

    private static readonly EvoDef[] Evolutions =
    [
        // --- Non-form species (official method matched where representable) ---
        new() { SourceName = "Primeape", TargetName = "Annihilape", Method = MethodLevelUp, Level = 35, FallbackSourceId = 57, FallbackTargetId = 979 }, // official: use Rage Fist 20 times (not trackable) — level substitute
        new() { SourceName = "Girafarig", TargetName = "Farigiraf", Method = MethodLevelUpWithPartySpecies, ArgPartySpeciesName = "Dudunsparce", Level = 1, FallbackSourceId = 203, FallbackTargetId = 981 },
        new() { SourceName = "Dunsparce", TargetName = "Dudunsparce", Method = MethodLevelUp, Level = 32, FallbackSourceId = 206, FallbackTargetId = 982 }, // per request: Lv32 instead of knowing Hyper Drill
        new() { SourceName = "Stantler", TargetName = "Wyrdeer", Method = MethodLevelUpKnowingMove, ArgMoveName = "Psyshield Bash", Level = 1, FallbackSourceId = 234, FallbackTargetId = 899 },
        new() { SourceName = "Meltan", TargetName = "Melmetal", Method = MethodLevelUp, Level = 40, FallbackSourceId = 808, FallbackTargetId = 809 }, // per request: Lv40 instead of 400 Meltan candy
        new() { SourceName = "Toxel", TargetName = "Toxtricity", Method = MethodLevelUp, Level = 30, FallbackSourceId = 848, FallbackTargetId = 849 },
        new() { SourceName = "Clobbopus", TargetName = "Grapploct", Method = MethodLevelUpKnowingMove, ArgMoveName = "Taunt", Level = 1, FallbackSourceId = 852, FallbackTargetId = 853 },
        new() { SourceName = "Snom", TargetName = "Frosmoth", Method = MethodLevelUpAtNight, Level = 20, FallbackSourceId = 872, FallbackTargetId = 873 },
        new() { SourceName = "Duraludon", TargetName = "Archaludon", Method = MethodLevelUp, Level = 35, FallbackSourceId = 884, FallbackTargetId = 1018 }, // per request: Lv35
        new() { SourceName = "Lechonk", TargetName = "Oinkologne", Method = MethodLevelUp, Level = 18, FallbackSourceId = 915, FallbackTargetId = 916 },
        new() { SourceName = "Pawmo", TargetName = "Pawmot", Method = MethodLevelUp, Level = 25, FallbackSourceId = 922, FallbackTargetId = 923 }, // per request: Lv25
        new() { SourceName = "Tandemaus", TargetName = "Maushold", Method = MethodLevelUp, Level = 25, FallbackSourceId = 924, FallbackTargetId = 925 }, // official: hidden after-battle counter (not trackable) — level substitute
        new() { SourceName = "Tadbulb", TargetName = "Bellibolt", Method = MethodLevelUp, Level = 30, FallbackSourceId = 938, FallbackTargetId = 939 },
        new() { SourceName = "Bramblin", TargetName = "Brambleghast", Method = MethodLevelUp, Level = 25, FallbackSourceId = 946, FallbackTargetId = 947 }, // per request: Lv25 instead of 1000 steps
        new() { SourceName = "Capsakid", TargetName = "Scovillain", Method = MethodLevelUp, Level = 30, FallbackSourceId = 950, FallbackTargetId = 951 }, // official: use a Fire/Grass Tera Shard (not representable) — level substitute
        new() { SourceName = "Rellor", TargetName = "Rabsca", Method = MethodLevelUp, Level = 25, FallbackSourceId = 953, FallbackTargetId = 954 }, // per request: Lv25 instead of 1000 steps
        new() { SourceName = "Finizen", TargetName = "Palafin", Method = MethodLevelUp, Level = 38, FallbackSourceId = 963, FallbackTargetId = 964 }, // per request: Lv38 (already matches official single-player level)
        new() { SourceName = "Greavard", TargetName = "Houndstone", Method = MethodLevelUpAtNight, Level = 30, FallbackSourceId = 971, FallbackTargetId = 972 },
        new() { SourceName = "Cetoddle", TargetName = "Cetitan", Method = MethodLevelUp, Level = 30, FallbackSourceId = 974, FallbackTargetId = 975 },
        new() { SourceName = "Dipplin", TargetName = "Hydrapple", Method = MethodLevelUpKnowingMove, ArgMoveName = "Dragon Cheer", Level = 1, FallbackSourceId = 986, FallbackTargetId = 1019 },

        // --- Regional / alternate forms (Form index assumed — see class remarks) ---
        new() { SourceName = "Ponyta", SourceForm = 1, TargetName = "Rapidash", Method = MethodLevelUp, Level = 40 },
        new() { SourceName = "Farfetch'd", SourceForm = 1, TargetName = "Sirfetch'd", Method = MethodLevelUp, Level = 25 }, // per request: Lv25
        new() { SourceName = "Zigzagoon", SourceForm = 1, TargetName = "Linoone", Method = MethodLevelUp, Level = 20 },
        new() { SourceName = "Linoone", SourceForm = 1, TargetName = "Obstagoon", Method = MethodLevelUpAtNight, Level = 35 },
        new() { SourceName = "Darumaka", SourceForm = 1, TargetName = "Darmanitan", Method = MethodUseItem, ArgItemName = "Ice Stone", Level = 0 },
        new() { SourceName = "Yamask", SourceForm = 1, TargetName = "Runerigus", Method = MethodLevelUp, Level = 30 }, // per request: Lv30
        new() { SourceName = "Growlithe", SourceForm = 1, TargetName = "Arcanine", Method = MethodUseItem, ArgItemName = "Fire Stone", Level = 0 },
        new() { SourceName = "Voltorb", SourceForm = 1, TargetName = "Electrode", Method = MethodUseItem, ArgItemName = "Leaf Stone", Level = 0 },
        new() { SourceName = "Qwilfish", SourceForm = 1, TargetName = "Overqwil", Method = MethodLevelUpKnowingMove, ArgMoveName = "Barb Barrage", Level = 1 },
        new() { SourceName = "Sneasel", SourceForm = 1, TargetName = "Sneasler", Method = MethodUseItem, ArgItemName = "Razor Claw", Level = 0 },
        new() { SourceName = "Zorua", SourceForm = 1, TargetName = "Zoroark", Method = MethodLevelUp, Level = 30 },
        new() { SourceName = "Sliggoo", SourceForm = 1, TargetName = "Goodra", Method = MethodLevelUp, Level = 50 },
        new() { SourceName = "Basculin", SourceForm = 2, TargetName = "Basculegion", Method = MethodLevelUp, Level = 30 }, // per request: Lv30
    ];

    /// <summary>
    /// Adds the missing evolution methods to <paramref name="config"/>'s in-memory evolution sets.
    /// </summary>
    /// <returns>How many entries were added. Zero means nothing needed changing.</returns>
    public static int Apply(GameConfig config) => Apply(config, config?.Evolutions);

    /// <summary>
    /// As above, against an explicit set of evolutions rather than the one on the config.
    /// </summary>
    public static int Apply(GameConfig config, EvolutionSet[] evolutions)
    {
        int added = 0;
        if (config == null || evolutions == null) return 0;
        // Only meaningful once the Expansion Pack's Gen 8/9 species are actually present.
        if (config.MaxSpeciesID < 1025) return 0;

        string[] speciesNames = config.GetText(TextName.SpeciesNames);
        string[] moveNames = config.GetText(TextName.MoveNames);
        string[] itemNames = config.GetText(TextName.ItemNames);

        int FindByName(string[] names, string target)
        {
            if (names == null || string.IsNullOrEmpty(target)) return -1;
            return Array.FindIndex(names, n => n != null && n.Equals(target, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var def in Evolutions)
        {
            int sourceId = FindByName(speciesNames, def.SourceName);
            if (sourceId <= 0) sourceId = def.FallbackSourceId;
            int targetId = FindByName(speciesNames, def.TargetName);
            if (targetId <= 0) targetId = def.FallbackTargetId;
            if (sourceId <= 0 || targetId <= 0 || sourceId >= evolutions.Length) continue;

            int argument = 0;
            if (def.Method == MethodLevelUpKnowingMove)
            {
                int moveId = FindByName(moveNames, def.ArgMoveName);
                if (moveId <= 0) continue; // can't represent this evolution without the move — skip rather than guess
                argument = moveId;
            }
            else if (def.Method == MethodUseItem)
            {
                int itemId = FindByName(itemNames, def.ArgItemName);
                if (itemId <= 0) continue;
                argument = itemId;
            }
            else if (def.Method == MethodLevelUpWithPartySpecies)
            {
                int partyId = FindByName(speciesNames, def.ArgPartySpeciesName);
                if (partyId <= 0) continue;
                argument = partyId;
            }

            var evoSet = evolutions[sourceId];
            if (evoSet?.PossibleEvolutions == null) continue;

            int requiredForm = def.SourceForm;

            // Don't duplicate if this exact evolution is already present (e.g. re-running on an
            // already-patched ROM), and only write into a genuinely empty (Method == 0) slot.
            bool alreadyPresent = evoSet.PossibleEvolutions.Any(e =>
                e != null && e.Method == def.Method && e.Species == targetId && e.Form == requiredForm);
            if (alreadyPresent) continue;

            int emptySlot = Array.FindIndex(evoSet.PossibleEvolutions, e => e != null && e.Method == 0);
            if (emptySlot < 0) continue; // no room — leave the species' existing evolutions untouched

            evoSet.PossibleEvolutions[emptySlot] = new EvolutionMethod
            {
                Method = def.Method,
                Argument = argument,
                Species = targetId,
                Form = requiredForm,
                Level = def.Level,
            };
            added++;
        }

        return added;
    }

    /// <summary>Writes the in-memory evolution sets back to the ROM's evolution GARC.</summary>
    public static bool Save(GameConfig config)
    {
        try
        {
            var g = config?.GetGARCData("evolution");
            if (g?.Files == null || config.Evolutions == null) return false;

            int n = Math.Min(g.Files.Length, config.Evolutions.Length);
            for (int i = 0; i < n; i++)
                g.Files[i] = config.Evolutions[i].Write();

            g.Save();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Applies the missing methods and writes them out in one step.
    /// </summary>
    /// <returns>How many entries were added and saved.</returns>
    public static int ApplyAndSave(GameConfig config)
    {
        int added = Apply(config);
        if (added > 0) Save(config);
        return added;
    }

    /// <summary>
    /// How many of these evolutions are missing from the ROM right now, without changing anything.
    /// </summary>
    public static int CountMissing(GameConfig config) => CountMissing(config, config?.Evolutions);

    /// <summary>As above, against an explicit set of evolutions.</summary>
    public static int CountMissing(GameConfig config, EvolutionSet[] evolutions)
    {
        if (config == null || evolutions == null || config.MaxSpeciesID < 1025) return 0;

        string[] speciesNames = config.GetText(TextName.SpeciesNames);
        int missing = 0;

        foreach (var def in Evolutions)
        {
            int sourceId = Array.FindIndex(speciesNames ?? [],
                n => n != null && n.Equals(def.SourceName, StringComparison.OrdinalIgnoreCase));
            if (sourceId <= 0) sourceId = def.FallbackSourceId;
            int targetId = Array.FindIndex(speciesNames ?? [],
                n => n != null && n.Equals(def.TargetName, StringComparison.OrdinalIgnoreCase));
            if (targetId <= 0) targetId = def.FallbackTargetId;
            if (sourceId <= 0 || targetId <= 0 || sourceId >= evolutions.Length) continue;

            var evoSet = evolutions[sourceId];
            if (evoSet?.PossibleEvolutions == null) continue;

            bool present = evoSet.PossibleEvolutions.Any(e =>
                e != null && e.Method == def.Method && e.Species == targetId && e.Form == def.SourceForm);
            if (!present) missing++;
        }
        return missing;
    }

    /// <summary>Every species this class can add an evolution for, for reporting.</summary>
    public static int DefinedCount => Evolutions.Length;
}
