using System;
using System.Collections.Generic;
using System.Linq;

using pk3DS.Core.Structures;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Reads the level distribution out of the ROM's own trainer data.
/// </summary>
public static class TrainerLevelSampler
{
    /// <summary>
    /// The highest level on each trainer's team, ascending. Empty when trainer data is unreadable.
    /// </summary>
    public static List<int> Collect(GameConfig config)
    {
        var levels = new List<int>();
        if (config == null) return levels;

        try
        {
            var trdata = config.GetGARCData("trdata");
            var trpoke = config.GetGARCData("trpoke");
            if (trdata?.Files == null || trpoke?.Files == null) return levels;

            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;

            for (int i = 1; i < trd.Length && i < trp.Length; i++)
            {
                if (trp[i] == null || trp[i].Length < TrainerPoke7.SIZE) continue;
                TrainerData7 t;
                try { t = new TrainerData7(trd[i], trp[i]); }
                catch { continue; }
                if (t.Pokemon is not { Count: > 0 }) continue;

                int top = t.Pokemon.Max(p => p.Level);
                // Level 0 is an empty slot, and 100+ is either a post-game special or corrupt data;
                // both would distort the percentiles the curve is read off.
                if (top is > 0 and <= 100) levels.Add(top);
            }
        }
        catch { return levels; }

        levels.Sort();
        return levels;
    }

    /// <summary>The value at a fraction through a sorted list, by nearest rank.</summary>
    public static int Percentile(IReadOnlyList<int> sorted, double fraction)
    {
        if (sorted == null || sorted.Count == 0) return 0;
        int idx = (int)Math.Round(Math.Clamp(fraction, 0, 1) * (sorted.Count - 1));
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }
}
