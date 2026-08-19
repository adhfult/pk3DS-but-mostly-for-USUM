using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// The 18x18 attack/defence multiplier grid, as it is stored inside code.bin.
/// </summary>
public sealed class TypeEffectivenessTable
{
    public const int TypeCount = 18;

    public const byte Immune = 0;
    public const byte NotVeryEffective = 2;
    public const byte Neutral = 4;
    public const byte SuperEffective = 8;

    /// <summary>Every value the game recognises, in ascending multiplier order.</summary>
    public static readonly byte[] AllValues = [Immune, NotVeryEffective, Neutral, SuperEffective];

    /// <summary>
    /// Bytes immediately preceding the chart in code.bin.
    /// </summary>
    public static readonly byte[] Signature =
    [
        0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
        0xC3, 0x00, 0x00, 0x00, 0xCB, 0x00, 0x00, 0x00, 0xD3, 0x00, 0x00, 0x00, 0xDB, 0x00, 0x00, 0x00,
        0xF3, 0x00, 0x00, 0x00, 0xFB, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
        0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
    ];

    private readonly byte[] Chart;

    private TypeEffectivenessTable(byte[] chart) => Chart = chart;

    public byte this[int attacker, int defender]
    {
        get => Chart[(attacker * TypeCount) + defender];
        set => Chart[(attacker * TypeCount) + defender] = value;
    }

    public TypeEffectivenessTable Clone() => new((byte[])Chart.Clone());

    /// <summary>A grid that is neutral everywhere, the starting point for a full re-roll.</summary>
    public static TypeEffectivenessTable Neutral18()
    {
        var chart = new byte[TypeCount * TypeCount];
        Array.Fill(chart, Neutral);
        return new TypeEffectivenessTable(chart);
    }

    /// <summary>Where the chart begins, or -1 when the signature is not present.</summary>
    public static int Locate(byte[] code)
    {
        if (code == null || code.Length < 0x400000 + Signature.Length) return -1;
        int at = Util.IndexOfBytes(code, Signature, 0x400000, 0);
        if (at < 0) return -1;
        at += Signature.Length;
        return at + (TypeCount * TypeCount) <= code.Length ? at : -1;
    }

    public static TypeEffectivenessTable Read(byte[] code, int offset)
    {
        var chart = new byte[TypeCount * TypeCount];
        Array.Copy(code, offset, chart, 0, chart.Length);
        return new TypeEffectivenessTable(chart);
    }

    public void WriteTo(byte[] code, int offset) => Chart.CopyTo(code, offset);

    /// <summary>Defender types this attacker hits for <paramref name="value"/>.</summary>
    public int CountWhenAttacking(int attacker, byte value)
    {
        int n = 0;
        for (int d = 0; d < TypeCount; d++)
            if (this[attacker, d] == value) n++;
        return n;
    }

    /// <summary>Attacker types that hit this defender for <paramref name="value"/>.</summary>
    public int CountWhenDefending(int defender, byte value)
    {
        int n = 0;
        for (int a = 0; a < TypeCount; a++)
            if (this[a, defender] == value) n++;
        return n;
    }

    /// <summary>Total cells holding <paramref name="value"/>.</summary>
    public int Count(byte value) => Chart.Count(b => b == value);

    /// <summary>Anything the game would not recognise, for a sanity check after a rewrite.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        for (int i = 0; i < Chart.Length; i++)
        {
            if (Array.IndexOf(AllValues, Chart[i]) < 0)
                problems.Add($"cell [{i / TypeCount:00}x{i % TypeCount:00}] holds {Chart[i]}, which is not 0/2/4/8");
        }
        return problems;
    }
}

/// <summary>
/// Rewrites the type chart, ported from the Universal Pokemon Randomizer FVX.
/// </summary>
public static class TypeEffectivenessRandomizer
{
    /// <summary>Modes offered on the Type Effectiveness group, in UI order.</summary>
    public const int Unchanged = 0;
    public const int Random = 1;
    public const int RandomBalanced = 2;
    public const int KeepTypeIdentities = 3;
    public const int Inverse = 4;

    /// <summary>Placement attempts before Random gives up, matching FVX's budget.</summary>
    private const int MaxPlacementTries = 10000;

    /// <summary>Successful column swaps Keep Type Identities performs, matching FVX's budget.</summary>
    private const int KeepIdentitySwaps = 10000;

    /// <summary>
    /// Values re-placed by <see cref="Random"/>. Neutral is what every unplaced cell already is, so
    /// placing it too would be a no-op that only slows the search down.
    /// </summary>
    private static readonly byte[] Placed =
    [
        TypeEffectivenessTable.Immune,
        TypeEffectivenessTable.NotVeryEffective,
        TypeEffectivenessTable.SuperEffective,
    ];

    /// <summary>
    /// Applies <paramref name="mode"/> to <paramref name="original"/> and returns the new chart.
    /// </summary>
    /// <param name="addRandomImmunities">
    /// Inverse only: turn as many of the newly-resisted matchups into outright immunities as the
    /// original chart had, so inverting does not simply delete every immunity from the game.
    /// </param>
    /// <param name="log">Receives one line describing what happened, including any shortfall.</param>
    public static TypeEffectivenessTable Apply(TypeEffectivenessTable original, int mode,
                                               bool addRandomImmunities, List<string> log)
    {
        return mode switch
        {
            Random => Reroll(original, balanced: false, log),
            RandomBalanced => Reroll(original, balanced: true, log),
            KeepTypeIdentities => KeepIdentities(original, log),
            Inverse => Invert(original, addRandomImmunities, log),
            _ => original,
        };
    }

    /// <summary>
    /// Scatters the original chart's non-neutral cells over a blank grid.
    /// </summary>
    private static TypeEffectivenessTable Reroll(TypeEffectivenessTable original, bool balanced, List<string> log)
    {
        var table = TypeEffectivenessTable.Neutral18();

        var maxAttacking = new Dictionary<byte, int>();
        var maxDefending = new Dictionary<byte, int>();
        if (balanced)
        {
            foreach (byte v in Placed)
            {
                int ma = 0, md = 0;
                for (int t = 0; t < TypeEffectivenessTable.TypeCount; t++)
                {
                    ma = Math.Max(ma, original.CountWhenAttacking(t, v));
                    md = Math.Max(md, original.CountWhenDefending(t, v));
                }
                maxAttacking[v] = ma;
                maxDefending[v] = md;
            }
        }

        int tries = 0;
        int unplaced = 0;
        foreach (byte v in Placed)
        {
            int remaining = original.Count(v);
            while (remaining > 0 && tries <= MaxPlacementTries)
            {
                int attacker = Util.Rand.Next(TypeEffectivenessTable.TypeCount);
                int defender = Util.Rand.Next(TypeEffectivenessTable.TypeCount);
                tries++;

                if (table[attacker, defender] != TypeEffectivenessTable.Neutral) continue;
                if (balanced)
                {
                    if (table.CountWhenAttacking(attacker, v) >= maxAttacking[v]) continue;
                    if (table.CountWhenDefending(defender, v) >= maxDefending[v]) continue;
                }

                table[attacker, defender] = v;
                remaining--;
            }
            unplaced += remaining;
        }

        string label = balanced ? " (balanced)" : "";
        log?.Add(unplaced == 0
            ? $"Type effectiveness randomized{label}: {original.Count(TypeEffectivenessTable.Immune)} immunities, {original.Count(TypeEffectivenessTable.NotVeryEffective)} resists and {original.Count(TypeEffectivenessTable.SuperEffective)} weaknesses re-placed."
            : $"Type effectiveness randomized{label}, but {unplaced} matchup(s) had nowhere left to go and stayed neutral.");
        return table;
    }

    /// <summary>
    /// Shuffles the chart without changing any single type's defensive profile.
    /// </summary>
    private static TypeEffectivenessTable KeepIdentities(TypeEffectivenessTable original, List<string> log)
    {
        var table = original.Clone();
        int swaps = 0, guard = 0;
        const int maxGuard = KeepIdentitySwaps * 20;

        while (swaps < KeepIdentitySwaps && guard < maxGuard)
        {
            guard++;
            int colA = Util.Rand.Next(TypeEffectivenessTable.TypeCount);
            int colB = Util.Rand.Next(TypeEffectivenessTable.TypeCount);
            if (colA == colB) continue;

            int chunkSize = Util.Rand.Next(TypeEffectivenessTable.TypeCount);
            var chunk = new HashSet<int>();
            int chunkGuard = 0;
            while (chunk.Count < chunkSize && chunkGuard++ < 200)
                chunk.Add(Util.Rand.Next(TypeEffectivenessTable.TypeCount));

            if (!ChunkCanSwap(table, colA, colB, chunk)) continue;

            foreach (int row in chunk)
                (table[row, colA], table[row, colB]) = (table[row, colB], table[row, colA]);
            swaps++;
        }

        log?.Add($"Type effectiveness shuffled with type identities kept ({swaps} column swaps).");
        return table;
    }

    private static bool ChunkCanSwap(TypeEffectivenessTable table, int colA, int colB, HashSet<int> chunk)
    {
        foreach (byte v in TypeEffectivenessTable.AllValues)
        {
            int a = 0, b = 0;
            foreach (int row in chunk)
            {
                if (table[row, colA] == v) a++;
                if (table[row, colB] == v) b++;
            }
            if (a != b) return false;
        }
        return true;
    }

    /// <summary>
    /// Turns resists into weaknesses and weaknesses into resists.
    /// </summary>
    private static TypeEffectivenessTable Invert(TypeEffectivenessTable original, bool addRandomImmunities, List<string> log)
    {
        var table = original.Clone();
        int immunities = 0;
        var newlyResisted = new List<(int Attacker, int Defender)>();

        for (int a = 0; a < TypeEffectivenessTable.TypeCount; a++)
        {
            for (int d = 0; d < TypeEffectivenessTable.TypeCount; d++)
            {
                switch (original[a, d])
                {
                    case TypeEffectivenessTable.Immune:
                        table[a, d] = TypeEffectivenessTable.SuperEffective;
                        immunities++;
                        break;
                    case TypeEffectivenessTable.NotVeryEffective:
                        table[a, d] = TypeEffectivenessTable.SuperEffective;
                        break;
                    case TypeEffectivenessTable.SuperEffective:
                        table[a, d] = TypeEffectivenessTable.NotVeryEffective;
                        newlyResisted.Add((a, d));
                        break;
                }
            }
        }

        if (!addRandomImmunities)
        {
            log?.Add("Type effectiveness inverted (no immunities remain; tick Add Random Immunities to keep some).");
            return table;
        }

        int wanted = Math.Min(immunities, newlyResisted.Count);
        for (int i = 0; i < wanted; i++)
        {
            int pick = Util.Rand.Next(newlyResisted.Count);
            var cell = newlyResisted[pick];
            newlyResisted.RemoveAt(pick);
            table[cell.Attacker, cell.Defender] = TypeEffectivenessTable.Immune;
        }

        log?.Add(wanted == immunities
            ? $"Type effectiveness inverted, with {wanted} random immunities added back."
            : $"Type effectiveness inverted, but only {wanted} of {immunities} immunities could be added back.");
        return table;
    }
}
