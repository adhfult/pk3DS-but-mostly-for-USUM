using System.Collections.Generic;
using System.Linq;

using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Evolution-stage and family facts the wild encounter options need.
/// </summary>
public sealed class WildRestrictions
{
    /// <summary>Modes for <see cref="UniversalSettings.WildEvolutionRestriction"/>.</summary>
    public const int EvoNone = 0;
    public const int EvoOnlyBasic = 1;
    public const int EvoSameStage = 2;

    /// <summary>Modes for <see cref="UniversalSettings.WildTypeRestriction"/>.</summary>
    public const int TypeNone = 0;
    public const int TypeRandomZoneThemes = 1;
    public const int TypeKeepPrimary = 2;

    private readonly GameConfig Config;
    private readonly Dictionary<int, int> PreEvolution = [];
    private readonly Dictionary<int, int> Stage = [];

    public WildRestrictions(GameConfig config)
    {
        Config = config;
        BuildFamilies();
    }

    private void BuildFamilies()
    {
        var evos = Config?.Evolutions;
        if (evos == null) return;

        int max = Config.MaxSpeciesID;
        for (int from = 1; from <= max && from < evos.Length; from++)
        {
            var set = evos[from];
            if (set?.PossibleEvolutions == null) continue;
            foreach (var e in set.PossibleEvolutions)
            {
                int to = e?.Species ?? 0;
                if (to > 0 && to <= max && to != from && !PreEvolution.ContainsKey(to))
                    PreEvolution[to] = from;
            }
        }
    }

    /// <summary>Whether nothing evolves into this species.</summary>
    public bool IsBasic(int species) => !PreEvolution.ContainsKey(species);

    /// <summary>
    /// How many evolutions deep a species sits: 0 for a basic, 1 for a first evolution, and so on.
    /// </summary>
    public int StageOf(int species)
    {
        if (Stage.TryGetValue(species, out int cached)) return cached;

        int n = 0, at = species;
        var seen = new HashSet<int> { at };
        while (PreEvolution.TryGetValue(at, out int parent) && n < 10 && seen.Add(parent))
        {
            at = parent;
            n++;
        }

        Stage[species] = n;
        return n;
    }

    /// <summary>The basic form at the root of this species' family.</summary>
    public int RootOf(int species)
    {
        int at = species;
        var seen = new HashSet<int> { at };
        while (PreEvolution.TryGetValue(at, out int parent) && seen.Add(parent))
            at = parent;
        return at;
    }

    /// <summary>Whether a candidate satisfies the chosen evolution restriction.</summary>
    public bool EvolutionAllows(int mode, int original, int candidate) => mode switch
    {
        EvoOnlyBasic => IsBasic(candidate),
        EvoSameStage => StageOf(candidate) == StageOf(original),
        _ => true,
    };

    /// <summary>Whether a candidate satisfies the chosen type restriction.</summary>
    /// <param name="theme">Zone theme type, or -1 when the mode does not use one.</param>
    public bool TypeAllows(int mode, int original, int candidate, int theme)
    {
        switch (mode)
        {
            case TypeRandomZoneThemes:
                return theme < 0 || TypeRestrictions.TypesOf(Config, candidate).Contains(theme);

            case TypeKeepPrimary:
            {
                var was = TypeRestrictions.TypesOf(Config, original).ToList();
                if (was.Count == 0) return true;
                return TypeRestrictions.TypesOf(Config, candidate).Contains(was[0]);
            }

            default:
                return true;
        }
    }

    /// <summary>
    /// A type every one of these species shares, or -1 when they share none.
    /// </summary>
    public int SharedType(IEnumerable<int> species)
    {
        HashSet<int> shared = null;
        foreach (int s in species)
        {
            if (s <= 0) continue;
            var types = TypeRestrictions.TypesOf(Config, s).ToHashSet();
            if (types.Count == 0) return -1;
            if (shared == null) shared = types;
            else shared.IntersectWith(types);
            if (shared.Count == 0) return -1;
        }
        return shared is { Count: > 0 } ? shared.Min() : -1;
    }
}
