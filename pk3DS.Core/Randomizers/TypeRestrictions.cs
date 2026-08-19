using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.Randomizers.Competitive;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Type-shaped constraints shared by the starter, trainer and wild randomizers.
/// </summary>
public static class TypeRestrictions
{
    /// <summary>Number of types the games actually use.</summary>
    public const int TypeCount = 18;

    /// <summary>The two type slots of a species, deduplicated for mono-types.</summary>
    public static int[] TypesOf(GameConfig config, int species)
    {
        try
        {
            var p = config?.Personal?[species];
            if (p == null) return [];
            int t1 = p.Types[0];
            int t2 = p.Types.Length > 1 ? p.Types[1] : t1;
            return t1 == t2 ? [t1] : [t1, t2];
        }
        catch { return []; }
    }

    public static bool IsMonoType(GameConfig config, int species) => TypesOf(config, species).Length == 1;

    /// <summary>Whether two species share any type at all.</summary>
    public static bool SharesType(GameConfig config, int a, int b)
    {
        var ta = TypesOf(config, a);
        var tb = TypesOf(config, b);
        return ta.Any(tb.Contains);
    }

    /// <summary>Whether a species carries a given type in either slot.</summary>
    public static bool HasType(GameConfig config, int species, int type) => TypesOf(config, species).Contains(type);

    /// <summary>Types this one hits super-effectively, excluding itself.</summary>
    private static List<int> SuperEffectiveAgainst(int attacking)
    {
        var list = new List<int>();
        for (int d = 0; d < TypeCount; d++)
        {
            if (d == attacking) continue;
            if (TypeEffectivenessChart.GetEffectiveness(attacking, d) > 1f) list.Add(d);
        }
        return list;
    }

    /// <summary>
    /// Every distinct trio of types where each beats the next, wrapping around.
    /// </summary>
    public static List<int[]> FindTypeTriangles()
    {
        var found = new List<int[]>();
        var seen = new HashSet<string>();

        for (int one = 0; one < TypeCount; one++)
        {
            foreach (int two in SuperEffectiveAgainst(one))
            {
                foreach (int three in SuperEffectiveAgainst(two))
                {
                    if (three == one) continue;
                    if (TypeEffectivenessChart.GetEffectiveness(three, one) <= 1f) continue;

                    int[] triangle = [three, two, one];
                    if (seen.Add($"{three},{two},{one}")) found.Add(triangle);
                }
            }
        }
        return found;
    }

    /// <summary>One triangle at random, or the classic trio if the chart yields none.</summary>
    public static int[] PickTypeTriangle(Random rand)
    {
        var all = FindTypeTriangles();
        return all.Count == 0
            ? [PokemonTypes.Fire, PokemonTypes.Water, PokemonTypes.Grass]
            : all[rand.Next(all.Count)];
    }

    /// <summary>
    /// The most type-varied subset the picker can manage, used for "force diverse types".
    /// </summary>
    public static bool AddsNewType(GameConfig config, IEnumerable<int> chosen, int candidate)
    {
        var have = new HashSet<int>();
        foreach (int s in chosen)
            foreach (int t in TypesOf(config, s)) have.Add(t);

        return TypesOf(config, candidate).Any(t => !have.Contains(t));
    }
}
