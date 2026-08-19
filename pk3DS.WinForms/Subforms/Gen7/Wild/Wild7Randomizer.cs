using System;
using System.Collections.Generic;
using System.Linq;

using pk3DS.Core;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public class Wild7Randomizer
{
    public SpeciesRandomizer RandSpec { get; set; }
    public FormRandomizer RandForm { get; set; }

    public int WildPokemonMode { get; set; } = 1; // 1: Random, 2: Area 1-to-1, 3: Global 1-to-1
    public int TableRandomizationOption { get; set; } = 0; // 0: All, 1: Regular Only, 2: SOS Only, 3: Regular Copy to SOS
    public decimal LevelAmplifier { get; set; } = 1.0m;
    public bool ModifyLevel { get; set; } = false;
    public bool AllCanCallAllies { get; set; } = false;

    /// <summary>
    /// How widely one original species may be replaced by different things.
    /// </summary>
    public int ReplacementsPerSpecies { get; set; } = 0;

    /// <summary>Give each encounter type in a set its own mapping (One Per Map only).</summary>
    public bool SplitByEncounterType { get; set; } = false;

    /// <summary>0 None, 1 Random Zone Themes, 2 Keep Primary Type.</summary>
    public int TypeRestriction { get; set; } = 0;

    /// <summary>Keep a zone's own shared type instead of rolling a new theme.</summary>
    public bool KeepZoneThemes { get; set; } = false;

    /// <summary>0 None, 1 Only Basic Pokemon, 2 Same Evolution Stage.</summary>
    public int EvolutionRestriction { get; set; } = 0;

    /// <summary>Map a family onto one replacement family, keeping stage relations.</summary>
    public bool KeepEvolutionRelations { get; set; } = false;

    /// <summary>
    /// Prefer a species the current encounter table does not already hold.
    /// </summary>
    public bool AvoidRepeats { get; set; } = false;

    /// <summary>Needed by the type and evolution restrictions; leave null to disable both.</summary>
    public GameConfig Config { get; set; }

    /// <summary>Species already placed in the encounter table being filled.</summary>
    private HashSet<int> TableSpecies;

    private WildRestrictions Restrictions;

    /// <summary>Scope names, matching <see cref="ReplacementsPerSpecies"/>.</summary>
    public const int ScopeMaximum = 0;
    public const int ScopePerEncounterSet = 1;
    public const int ScopePerMap = 2;
    public const int ScopePerNamedLocation = 3;
    public const int ScopeWholeGame = 4;

    /// <summary>One species-to-species mapping, for whichever scope is currently in force.</summary>
    private Dictionary<int, int> ScopeMap;

    /// <summary>Whole-game scope keeps one map for the entire run.</summary>
    private readonly Dictionary<int, int> GameMap = [];

    /// <summary>Named-location scope keeps one map per location name.</summary>
    private readonly Dictionary<string, Dictionary<int, int>> LocationMaps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Family root to family root, for Keep Evolution Relations.</summary>
    private readonly Dictionary<int, int> FamilyMap = [];

    /// <summary>Theme type currently in force, or -1 for none.</summary>
    private int CurrentTheme = -1;

    private bool RestrictionsActive =>
        Restrictions != null && (TypeRestriction != WildRestrictions.TypeNone ||
                                 EvolutionRestriction != WildRestrictions.EvoNone ||
                                 KeepEvolutionRelations);

    /// <summary>
    /// The scope actually in force, reconciling the new setting with the old mode radio buttons.
    /// </summary>
    private int EffectiveScope
    {
        get
        {
            if (ReplacementsPerSpecies != ScopeMaximum) return ReplacementsPerSpecies;
            return WildPokemonMode switch
            {
                3 => ScopeWholeGame,
                2 => ScopePerMap,
                _ => ScopeMaximum,
            };
        }
    }

    /// <summary>
    /// Picks a replacement for one species under the current scope, theme and restrictions.
    /// </summary>
    private int GetSpecies(int oldSpecies)
    {
        if (oldSpecies == 0) return 0;

        if (EffectiveScope == ScopeWholeGame && !RestrictionsActive && CurrentTheme < 0 && !AvoidRepeats)
            return Remember(RandSpec.GetMappedSpecies(oldSpecies));

        var map = ScopeMap;
        if (map != null && map.TryGetValue(oldSpecies, out int mapped))
            return Remember(mapped);   // a decided mapping wins over the no-repeat preference

        int picked = PickConstrained(oldSpecies);
        if (map != null) map[oldSpecies] = picked;
        return Remember(picked);
    }

    /// <summary>Records what this table now holds, and passes the species straight through.</summary>
    private int Remember(int species)
    {
        if (species > 0) TableSpecies?.Add(species);
        return species;
    }

    /// <summary>
    /// One draw that satisfies the type and evolution restrictions, or the best available.
    /// </summary>
    private int PickConstrained(int oldSpecies)
    {
        if (KeepEvolutionRelations && Restrictions != null)
        {
            int root = Restrictions.RootOf(oldSpecies);
            if (!FamilyMap.TryGetValue(root, out int newRoot))
            {
                newRoot = DrawSatisfying(root, requireTheme: true, requireEvo: true);
                FamilyMap[root] = newRoot;
            }
            // Walk the replacement family to the same depth the original sat at, so a first-stage
            // stays a first-stage even though the family it belongs to changed.
            return AtStage(newRoot, Restrictions.StageOf(oldSpecies));
        }

        return DrawSatisfying(oldSpecies, requireTheme: true, requireEvo: true);
    }

    private int DrawSatisfying(int oldSpecies, bool requireTheme, bool requireEvo)
    {
        int fallback = RandSpec.GetRandomSpecies(oldSpecies);
        bool wantFresh = AvoidRepeats && TableSpecies != null;

        if (Restrictions == null && !wantFresh) return fallback;

        bool Fresh(int c) => !wantFresh || !TableSpecies.Contains(c);
        bool Ok(int c, bool theme, bool evo, bool fresh) =>
            c > 0 &&
            (!fresh || Fresh(c)) &&
            (Restrictions == null ||
             ((!theme || Restrictions.TypeAllows(TypeRestriction, oldSpecies, c, CurrentTheme)) &&
              (!evo || Restrictions.EvolutionAllows(EvolutionRestriction, oldSpecies, c))));

        if (Ok(fallback, requireTheme, requireEvo, true)) return fallback;

        // Everything asked for.
        for (int i = 0; i < 120; i++)
        {
            int c = RandSpec.GetRandomSpecies(oldSpecies);
            if (Ok(c, requireTheme, requireEvo, true)) return c;
        }

        // Drop the theme, which is decoration; the evolution rule is about pacing and matters more.
        if (requireTheme && requireEvo && Restrictions != null)
        {
            for (int i = 0; i < 80; i++)
            {
                int c = RandSpec.GetRandomSpecies(oldSpecies);
                if (Ok(c, theme: false, evo: true, fresh: true)) return c;
            }
        }

        if (wantFresh)
        {
            for (int i = 0; i < 60; i++)
            {
                int c = RandSpec.GetRandomSpecies(oldSpecies);
                if (Ok(c, requireTheme, requireEvo, fresh: false)) return c;
            }
        }

        return fallback;
    }

    /// <summary>Walks a family down to the requested stage, stopping at whatever it can reach.</summary>
    private int AtStage(int root, int wantedStage)
    {
        var evos = Config?.Evolutions;
        if (evos == null || wantedStage <= 0) return root;

        int at = root;
        for (int step = 0; step < wantedStage; step++)
        {
            if (at >= evos.Length) break;
            var next = evos[at]?.PossibleEvolutions?.FirstOrDefault(e => e != null && e.Species > 0 && e.Species != at);
            if (next == null) break;   // family is shorter than the original's; stop where it ends
            at = next.Species;
        }
        return at;
    }

    private void RandomizeTable7(EncounterTable Table, int slotStart, int slotStop)
    {
        int end = slotStop < 0 ? Table.Encounter7s.Length : slotStop;

        // 1. Regular encounters (slot 0)
        if (slotStart <= 0 && end > 0)
        {
            var regularSlots = Table.Encounter7s[0];
            for (int i = 0; i < regularSlots.Length; i++)
            {
                var enc = regularSlots[i];
                if (enc.Species != 0)
                {
                    enc.Species = (uint)GetSpecies((int)enc.Species);
                    enc.Forme = (uint)RandForm.GetRandomForme((int)enc.Species);
                }
            }
        }

        var outerScope = ScopeMap;
        if (SplitByEncounterType && ScopeMap != null && EffectiveScope == ScopePerMap)
            ScopeMap = [];

        for (int s = Math.Max(1, slotStart); s < Math.Min(8, end); s++)
        {
            var sosSet = Table.Encounter7s[s];
            for (int i = 0; i < sosSet.Length; i++)
            {
                var baseEnc = Table.Encounter7s[0][i];
                var sosEnc = sosSet[i];
                if (sosEnc.Species != 0)
                {
                    sosEnc.Species = (uint)GetSpecies((int)sosEnc.Species);
                    sosEnc.Forme = (uint)RandForm.GetRandomForme((int)sosEnc.Species);
                }
                else if (baseEnc.Species != 0 && AllCanCallAllies)
                {
                    sosEnc.Species = (uint)GetSpecies((int)baseEnc.Species);
                    sosEnc.Forme = (uint)RandForm.GetRandomForme((int)sosEnc.Species);
                }
            }
        }

        // 3. Additional SOS / Weather slots (slot 8 = AdditionalSOS, 6 entries)
        if (slotStart <= 8 && end > 8)
        {
            for (int i = 0; i < Table.AdditionalSOS.Length; i++)
            {
                var weatherEnc = Table.AdditionalSOS[i];
                if (weatherEnc.Species != 0)
                {
                    weatherEnc.Species = (uint)GetSpecies((int)weatherEnc.Species);
                    weatherEnc.Forme = (uint)RandForm.GetRandomForme((int)weatherEnc.Species);
                }
                else if (AllCanCallAllies)
                {
                    weatherEnc.Species = (uint)GetSpecies(1);
                    weatherEnc.Forme = (uint)RandForm.GetRandomForme((int)weatherEnc.Species);
                }
            }
        }

        ScopeMap = outerScope;
    }

    /// <summary>Every species a table currently holds, for reading its vanilla theme.</summary>
    private static IEnumerable<int> SpeciesIn(EncounterTable table)
    {
        foreach (var set in table.Encounter7s)
            foreach (var e in set)
                if (e.Species != 0) yield return (int)e.Species;
        foreach (var e in table.AdditionalSOS)
            if (e.Species != 0) yield return (int)e.Species;
    }

    public void Execute(IEnumerable<Area7> Areas, LazyGARCFile encdata)
    {
        GetTableRandSettings((RandOption)TableRandomizationOption, out int slotStart, out int slotStop, out bool copy);

        Restrictions = Config != null ? new WildRestrictions(Config) : null;
        int scope = EffectiveScope;

        foreach (var Map in Areas)
        {
            string locationName = Map.Zones?.FirstOrDefault()?.LocationName;
            Dictionary<int, int> mapScope = scope switch
            {
                ScopeWholeGame => GameMap,
                ScopePerNamedLocation when !string.IsNullOrEmpty(locationName) =>
                    LocationMaps.TryGetValue(locationName, out var existing)
                        ? existing
                        : LocationMaps[locationName] = [],
                ScopePerNamedLocation => [],   // unnamed: treat as its own place rather than pooling
                ScopePerMap => [],
                _ => null,                     // per-set and maximum are decided below
            };

            // A zone theme covers the whole map, so it is chosen once here rather than per table -
            // otherwise each encounter set in a route would get a different "zone" theme.
            if (TypeRestriction == WildRestrictions.TypeRandomZoneThemes && Restrictions != null)
            {
                CurrentTheme = -1;
                if (KeepZoneThemes)
                    CurrentTheme = Restrictions.SharedType(Map.Tables.SelectMany(SpeciesIn));
                if (CurrentTheme < 0)
                    CurrentTheme = Util.Rand.Next(pk3DS.Core.Randomizers.TypeEffectivenessTable.TypeCount);
            }
            else
            {
                CurrentTheme = -1;
            }

            foreach (var Table in Map.Tables)
            {
                if (ModifyLevel)
                {
                    Table.MinLevel = Randomizer.GetModifiedLevel(Table.MinLevel, LevelAmplifier);
                    Table.MaxLevel = Randomizer.GetModifiedLevel(Table.MaxLevel, LevelAmplifier);
                    if (Table.MinLevel > Table.MaxLevel)
                        Table.MaxLevel = Table.MinLevel;
                }

                ScopeMap = scope == ScopePerEncounterSet ? [] : mapScope;

                // Repeats are judged per encounter table: two routes holding the same species is
                // normal and expected, one route holding it four times is what this is for.
                TableSpecies = AvoidRepeats ? [] : null;

                RandomizeTable7(Table, slotStart, slotStop);
                if (copy) // copy row 0 to rest (including weather)
                    Table.CopySlotsToSOS(true);

                Table.Write();
            }
            encdata[Map.FileNumber] = Area7.GetDayNightTableBinary(Map.Tables);
        }
    }

    private static void GetTableRandSettings(RandOption option, out int slotStart, out int slotStop, out bool copy)
    {
        copy = false;
        switch (option)
        {
            default: // All
                slotStart = 0;
                slotStop = -1;
                break;
            case RandOption.Regular_Only:
                slotStart = 0;
                slotStop = 1;
                break;
            case RandOption.SOS_Only:
                slotStart = 1;
                slotStop = -1;
                break;
            case RandOption.Regular_CopySOS:
                slotStart = 0;
                slotStop = 1;
                copy = true;
                break;
        }
    }

    private enum RandOption
    {
        All = 0,
        Regular_Only = 1,
        SOS_Only = 2,
        Regular_CopySOS = 3,
    }
}
