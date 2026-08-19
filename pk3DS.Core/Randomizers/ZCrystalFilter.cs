#nullable enable

using System;
using System.Collections.Generic;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Which items are Z-Crystals, so nothing outside a trainer battle can hand one out.
/// </summary>
public sealed class ZCrystalFilter
{
    private readonly HashSet<int> _banned = [];

    /// <summary>Ids that must not be given out. Empty when the item table could not be read.</summary>
    public IReadOnlySet<int> BannedIds => _banned;

    public int Count => _banned.Count;

    public bool IsZCrystal(int itemId) => _banned.Contains(itemId);

    /// <summary>Whether a name is a Z-Crystal, independent of any loaded ROM.</summary>
    public static bool IsZCrystalName(string? name)
    {
        string n = (name ?? "").Trim();
        if (n.Length < 5) return false;
        return n.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            && n.Contains("ium", StringComparison.OrdinalIgnoreCase);
    }

    public ZCrystalFilter(string[]? itemNames)
    {
        if (itemNames == null) return;
        for (int i = 1; i < itemNames.Length; i++)
            if (IsZCrystalName(itemNames[i]))
                _banned.Add(i);
    }

    /// <summary>Picks with <paramref name="roll"/> until it returns something allowed.</summary>
    public int PickAllowed(Func<int> roll, int attempts = 64)
    {
        int last = roll();
        for (int i = 0; i < attempts && IsZCrystal(last); i++) last = roll();
        return last;
    }
}
