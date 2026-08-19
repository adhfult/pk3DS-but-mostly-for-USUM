using System;
using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Where the item IDs backing the Expansion Pack's extra TMs come from.
/// <para>
/// Expanding USUM from 100 TMs to 128 needs 28 item IDs that do not already mean something. The
/// obvious approach — scavenging slots whose name still reads "???" — no longer works on a real
/// Expansion Pack build: only nine such slots remain, and the fallback for the rest handed out
/// 960 upward, which on the checked UM Expansion ROM is Booster Energy, Eternatusite, Tera Crystal,
/// the Rusted Sword/Shield, Reins of Unity, the Ogerpon masks and a dozen more. Saving the TM
/// editor would have renamed those to "TM108".."TM128" and overwritten their item records with
/// clones of TM01, destroying twenty-one real items.
/// </para>
/// <para>
/// So the IDs are allocated as a fresh contiguous block above everything that exists, rather than
/// scavenged. <see cref="DefaultStartId"/> is the first ID past the Expansion Pack's own range.
/// </para>
/// </summary>
public static class ExpandedTMItems
{
    /// <summary>TMs a retail USUM build has. The game's own bound reads <c>CMP R0, #100</c>.</summary>
    public const int VanillaTMCount = 100;

    /// <summary>TM count the expansion targets.</summary>
    public const int ExpandedTMCount = 128;

    /// <summary>How many brand-new item IDs the expansion needs: TM101 through TM128.</summary>
    public const int NewItemCount = ExpandedTMCount - VanillaTMCount;

    /// <summary>
    /// Fallback start ID, used only when the loaded game's item table cannot be read.
    /// <para>
    /// 1024 is correct for the checked UM Expansion build — its own items run through 1023 with
    /// Darkranite last, and the item name table holds exactly 1024 entries. It is wrong for
    /// anything else, so prefer <see cref="ResolveStartId"/>, which reads the table instead of
    /// assuming this.
    /// </para>
    /// </summary>
    public const int DefaultStartId = 1024;

    /// <summary>
    /// First free item ID in the loaded game, so the block lands after whatever that ROM actually
    /// has rather than at a number fixed for one build.
    /// </summary>
    public static int ResolveStartId(GameConfig cfg, int fallback = DefaultStartId)
    {
        string[] names = null;
        try { names = cfg?.GetText(TextName.ItemNames); }
        catch { /* a language with no item archive falls through to the fallback */ }

        if (names == null || names.Length == 0) return fallback;

        // Already allocated on a previous run? Reuse that block.
        string firstName = GetTMName(VanillaTMCount); // "TM101"
        for (int i = names.Length - 1; i >= 0; i--)
        {
            if (string.Equals(names[i]?.Trim(), firstName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        // Otherwise the first index past the last item that actually has a name.
        int last = names.Length - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(names[last])) last--;

        int start = last + 1;
        return start > 0 ? start : fallback;
    }

    /// <summary>
    /// Whether a start ID can be used without overwriting an item that already means something.
    /// </summary>
    public static bool IsSafeStartId(GameConfig cfg, int startId)
    {
        if (startId <= 0) return false;

        string[] names = null;
        try { names = cfg?.GetText(TextName.ItemNames); }
        catch { /* unreadable: fall through */ }

        // Nothing to check against; the caller's fallback is as good as it gets.
        if (names == null || names.Length == 0) return true;

        for (int i = 0; i < NewItemCount; i++)
        {
            int id = startId + i;
            if (id >= names.Length) continue;      // past the table: free by definition

            string name = names[id]?.Trim();
            if (string.IsNullOrEmpty(name)) continue;                 // unnamed slot
            if (name.Equals(GetTMName(VanillaTMCount + i), StringComparison.OrdinalIgnoreCase))
                continue;                                             // already ours
            if (name is "???" or "-----") continue;                   // placeholder rows

            return false;   // a real item lives here
        }
        return true;
    }

    /// <summary>
    /// Explains what <see cref="ResolveStartId"/> decided and why, for the preflight report.
    /// </summary>
    public static string DescribeAllocation(GameConfig cfg)
    {
        string[] names = null;
        try { names = cfg?.GetText(TextName.ItemNames); }
        catch { /* reported below as unreadable */ }

        if (names == null || names.Length == 0)
            return $"Item name table unreadable; falling back to the fixed start ID {DefaultStartId}.";

        int start = ResolveStartId(cfg);
        string firstName = GetTMName(VanillaTMCount);

        bool reused = start < names.Length
                      && string.Equals(names[start]?.Trim(), firstName, StringComparison.OrdinalIgnoreCase);

        int named = names.Length - 1;
        while (named >= 0 && string.IsNullOrWhiteSpace(names[named])) named--;

        return reused
            ? $"Reusing the existing block: {firstName} is already at ID {start}, so TM101-TM128 "
              + $"keep IDs {start}-{HighestId(start)}."
            : $"Item table holds {names.Length} rows, {named + 1} of them named. "
              + $"Allocating TM101-TM128 at IDs {start}-{HighestId(start)}.";
    }

    /// <summary>Highest ID handed out when starting from <paramref name="startId"/>.</summary>
    public static int HighestId(int startId = DefaultStartId) => startId + NewItemCount - 1;

    /// <summary>
    /// Item ID for a table index, or 0 when the index is not one of the new slots.
    /// <para>
    /// Index 100 is TM101. That boundary is 100 and not 107: the seven entries at 100-106 in the
    /// stock table are the leftover HM moves (Cut, Fly, Surf, Strength, Waterfall, Rock Smash,
    /// Dive), and USUM has no HMs — Ride Pager replaced them. The expanded table is relocated
    /// wholesale into free space by
    /// <see cref="pk3DS.Core.Modding.ResearchEngine.ApplyExpandedTMCodePatch"/>, so the original
    /// 107-entry table stays intact in place for anything else that reads it, and the new one is
    /// pure TMs. Counting from 107 instead would yield only 21 new slots, not 28, and would label
    /// them TM108-TM128.
    /// </para>
    /// </summary>
    public static int GetItemId(int tableIndex, int startId = DefaultStartId)
    {
        if (tableIndex < VanillaTMCount || tableIndex >= ExpandedTMCount) return 0;
        return startId + (tableIndex - VanillaTMCount);
    }

    /// <summary>True when <paramref name="tableIndex"/> is one of the 28 expansion slots.</summary>
    public static bool IsNewSlot(int tableIndex) =>
        tableIndex >= VanillaTMCount && tableIndex < ExpandedTMCount;

    /// <summary>Display name for a slot: index 100 is "TM101".</summary>
    public static string GetTMName(int tableIndex) => $"TM{tableIndex + 1:D3}";

    /// <summary>
    /// Overwrites the expansion slots of <paramref name="itemlist"/> with the new block, leaving
    /// TM01-TM100 on their stock item IDs. Returns the IDs assigned.
    /// </summary>
    public static IReadOnlyList<ushort> AssignTo(ushort[] itemlist, int startId = DefaultStartId)
    {
        var assigned = new List<ushort>();
        if (itemlist == null) return assigned;

        int end = Math.Min(itemlist.Length, ExpandedTMCount);
        for (int i = VanillaTMCount; i < end; i++)
        {
            var id = (ushort)GetItemId(i, startId);
            itemlist[i] = id;
            assigned.Add(id);
        }
        return assigned;
    }

    /// <summary>
    /// The stock item IDs for TM01-TM100 plus the HM block, as the unexpanded table holds them.
    /// Used as the fallback when a ROM carries no relocation patch to read real values from.
    /// </summary>
    public static ushort[] GetVanillaItemTable()
    {
        var items = new ushort[107];
        for (int i = 0; i < 92; i++) items[i] = (ushort)(328 + i);   // TM01-TM92
        for (int i = 92; i < 95; i++) items[i] = (ushort)(618 + (i - 92));  // TM93-TM95
        for (int i = 95; i < 100; i++) items[i] = (ushort)(690 + (i - 95)); // TM96-TM100
        for (int i = 100; i < 106; i++) items[i] = (ushort)(420 + (i - 100)); // HM01-HM06
        items[106] = 737; // HM07 Dive
        return items;
    }

    /// <summary>
    /// Full item table for <paramref name="count"/> slots: stock IDs below 100, then the new block
    /// when <paramref name="expanded"/>. When not expanded the legacy HM entries are kept so an
    /// unexpanded ROM still reads the way it always did.
    /// </summary>
    public static ushort[] BuildItemTable(int count, bool expanded, int startId = DefaultStartId)
    {
        var vanilla = GetVanillaItemTable();
        var items = new ushort[Math.Max(count, 0)];

        for (int i = 0; i < items.Length; i++)
        {
            if (expanded && IsNewSlot(i)) items[i] = (ushort)GetItemId(i, startId);
            else if (i < vanilla.Length) items[i] = vanilla[i];
            else items[i] = 0;
        }
        return items;
    }
}
