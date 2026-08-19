using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// The difficulty offsets the Level Caps tab offers, in levels.
/// </summary>
public static class LevelCapShifts
{
    public static readonly int[] All = [-10, -5, -3, 0, 3, 5, 10];

    /// <summary>Index of the unshifted curve, used as the default selection.</summary>
    public static readonly int StandardIndex = Array.IndexOf(All, 0);

    public static string Describe(int shift) => shift switch
    {
        0 => "Standard  (research values)",
        > 0 => $"Relaxed  (+{shift} levels)",
        _ => $"Strict  ({shift} levels)",
    };
}

/// <summary>
/// Builds a full 27-checkpoint <see cref="LevelCapTable"/> from two plain choices.
/// </summary>
public static class LevelCapPresets
{
    /// <summary>
    /// The stock curve rescaled so the last checkpoint lands on <paramref name="finalCap"/>, then
    /// shifted by <paramref name="shift"/> levels.
    /// </summary>
    public static LevelCapTable Build(int shift, byte finalCap, bool ultraMoon = false)
    {
        if (finalCap < 5) finalCap = 5;
        if (finalCap > LevelCapTable.HardCeiling) finalCap = LevelCapTable.HardCeiling;

        var basis = LevelCapTable.Default(ultraMoon);
        double scale = finalCap / (double)LevelCapTable.HardCeiling;

        var entries = new List<LevelCapEntry>(basis.Entries.Count);
        foreach (var e in basis.Entries)
        {
            int cap = (int)Math.Round(e.Cap * scale) + shift;
            entries.Add(e with { Cap = (byte)Math.Clamp(cap, 2, finalCap) });
        }

        // Rounding and clamping can make a later checkpoint lower than an earlier one, which would
        // read as the cap going backwards partway through the game.
        for (int i = 1; i < entries.Count; i++)
        {
            if (entries[i].Cap < entries[i - 1].Cap)
                entries[i] = entries[i] with { Cap = entries[i - 1].Cap };
        }

        var trimmed = new List<LevelCapEntry>(entries.Count) { entries[0] };
        for (int i = 1; i < entries.Count; i++)
        {
            if (entries[i].Cap != trimmed[^1].Cap)
                trimmed.Add(entries[i]);
        }

        return new LevelCapTable { Entries = trimmed };
    }

    /// <summary>
    /// A curve whose shape comes from the ROM's own trainer levels rather than the research values.
    /// </summary>
    public static LevelCapTable BuildFromTrainerLevels(IReadOnlyList<int> sortedLevels, byte finalCap,
                                                       bool ultraMoon = false)
    {
        var basis = LevelCapTable.Default(ultraMoon);
        if (sortedLevels is not { Count: >= 20 }) return Build(0, finalCap, ultraMoon);

        if (finalCap < 5) finalCap = 5;
        if (finalCap > LevelCapTable.HardCeiling) finalCap = LevelCapTable.HardCeiling;

        int n = basis.Entries.Count;
        var raw = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            // (i+1)/n so the last rung lands on the top of the distribution rather than one short
            // of it; the rescale below then pins it to finalCap exactly.
            raw.Add(TrainerLevelSampler.Percentile(sortedLevels, (i + 1) / (double)n));
        }

        int top = raw[^1] > 0 ? raw[^1] : 1;
        var entries = new List<LevelCapEntry>(n);
        for (int i = 0; i < n; i++)
        {
            int cap = (int)Math.Round(raw[i] * (finalCap / (double)top));
            entries.Add(basis.Entries[i] with { Cap = (byte)Math.Clamp(cap, 2, finalCap) });
        }

        for (int i = 1; i < entries.Count; i++)
            if (entries[i].Cap < entries[i - 1].Cap)
                entries[i] = entries[i] with { Cap = entries[i - 1].Cap };

        var trimmed = new List<LevelCapEntry>(entries.Count) { entries[0] };
        for (int i = 1; i < entries.Count; i++)
            if (entries[i].Cap != trimmed[^1].Cap)
                trimmed.Add(entries[i]);

        return new LevelCapTable { Entries = trimmed };
    }

    /// <summary>A one-line summary of a built table, for the tab to show live.</summary>
    public static string Summarise(LevelCapTable table)
    {
        if (table?.Entries is not { Count: > 0 }) return "no checkpoints";

        var first = table.Entries[0];
        var last = table.Entries[^1];
        return $"{table.Entries.Count} checkpoints - Lv{first.Cap} at \"{first.Label}\" " +
               $"rising to Lv{last.Cap} at \"{last.Label}\"";
    }

    /// <summary>The caps in order, for a compact preview.</summary>
    public static string CapSequence(LevelCapTable table) =>
        table?.Entries is not { Count: > 0 } ? "" : string.Join(", ", table.Entries.Select(e => e.Cap));
}
