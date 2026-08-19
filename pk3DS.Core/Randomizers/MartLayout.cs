#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;
using pk3DS.Core.Modding;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Where every shop's item list actually lives in Shop.cro, and how many slots it has.
/// </summary>
public sealed class MartLayout
{
    /// <summary>Relocation patch address for each regular shop's list, in editor order.</summary>
    public static readonly uint[] MartPatchAddrs =
    [
        0x594, 0x5A0, 0x5AC, 0x5B8, 0x5C4, 0x5D0, 0x5DC, 0x5E8, // trial stages 0-7
        0x5F4, // Konikoni Incense
        0x3F0, // Konikoni Herb
        0x42C, // Hau'oli X Items
        0x438, // Route 2 Misc
        0x444, // Heahea TM
        0x450, // Royal Avenue TMs
        0x45C, // Route 8
        0x474, // Paniola Town
        0x48C, // Malie City TMs
        0x4B0, // Mount Hokulani
        0x4BC, // Seafolk Village TMs
        0x4F8, // Konikoni City TMs
        0x3FC, // Konikoni City Stones
        0x408, // Thrifty Megamart Left
        0x414, // Thrifty Megamart Middle
        0x420, // Thrifty Megamart Right
        0x480, // Route 5 X Items
        0x468, // Konikoni City X Items
        0x498, // Tapu Village X Items
        0x4A4, // Mount Lanakila X Items
    ];

    public static readonly uint[] BPPatchAddrs =
    [
        0x504, 0x510, 0x51C,  // Battle Royale left/middle/right
        0x528, 0x534, 0x540,  // Battle Tree left/middle/right
        0x54C,                // Big Wave Beach
    ];

    /// <summary>Relocation patch holding the regular slot-count table.</summary>
    private const uint CountsPatch = 0x024;

    /// <summary>Relocation patch holding the BP slot-count table.</summary>
    private const uint CountsBPPatch = 0x030;

    public int CountsOffset { get; private set; } = -1;
    public int CountsBPOffset { get; private set; } = -1;

    /// <summary>File offset of each regular shop's list; -1 when it could not be resolved.</summary>
    public int[] ShopOffsets { get; private set; } = [];
    public int[] ShopCounts { get; private set; } = [];
    public int[] BPOffsets { get; private set; } = [];
    public int[] BPCounts { get; private set; } = [];

    public bool Valid => CountsOffset > 0 && ShopOffsets.Any(o => o > 0);

    /// <summary>Reads the layout out of a Shop.cro.</summary>
    public static MartLayout Read(byte[] cro)
    {
        var l = new MartLayout();
        if (cro == null || cro.Length < 0x200) return l;

        l.CountsOffset = ResearchEngine.GetRelocationPatchTarget(cro, CountsPatch);
        l.CountsBPOffset = ResearchEngine.GetRelocationPatchTarget(cro, CountsBPPatch);

        l.ShopOffsets = [.. MartPatchAddrs.Select(a => ResearchEngine.GetRelocationPatchTarget(cro, a))];
        l.BPOffsets = [.. BPPatchAddrs.Select(a => ResearchEngine.GetRelocationPatchTarget(cro, a))];

        l.ShopCounts = new int[MartPatchAddrs.Length];
        if (l.CountsOffset > 0)
            for (int i = 0; i < l.ShopCounts.Length && l.CountsOffset + i < cro.Length; i++)
                l.ShopCounts[i] = cro[l.CountsOffset + i];

        l.BPCounts = new int[BPPatchAddrs.Length];
        if (l.CountsBPOffset > 0)
            for (int i = 0; i < l.BPCounts.Length && l.CountsBPOffset + i < cro.Length; i++)
                l.BPCounts[i] = cro[l.CountsBPOffset + i];

        return l;
    }

    /// <summary>Problems that would make a write unsafe, as plain sentences. Empty when it is sound.</summary>
    public List<string> Validate(byte[] cro)
    {
        var bad = new List<string>();
        if (CountsOffset <= 0) bad.Add("the slot-count table could not be located");

        if (cro.Length % 0x1000 != 0)
            bad.Add($"the file is {cro.Length} bytes, which is not a whole number of 0x1000 pages");
        if (cro.Length >= 0x94 && BitConverter.ToUInt32(cro, 0x90) != (uint)cro.Length)
            bad.Add($"the header says the file is 0x{BitConverter.ToUInt32(cro, 0x90):X} bytes but it is 0x{cro.Length:X}");

        for (int i = 0; i < ShopOffsets.Length; i++)
        {
            if (ShopOffsets[i] <= 0) { bad.Add($"shop {i}: list offset unresolved"); continue; }
            if (ShopCounts[i] <= 0) { bad.Add($"shop {i}: count is {ShopCounts[i]}"); continue; }
            string? why = Placement(cro, ShopOffsets[i], ShopCounts[i] * 2);
            if (why != null) bad.Add($"shop {i}: {ShopCounts[i]} slots at 0x{ShopOffsets[i]:X} {why}");
        }
        for (int i = 0; i < BPOffsets.Length; i++)
        {
            if (BPOffsets[i] <= 0) { bad.Add($"BP shop {i}: list offset unresolved"); continue; }
            if (BPCounts[i] <= 0) { bad.Add($"BP shop {i}: count is {BPCounts[i]}"); continue; }
            string? why = Placement(cro, BPOffsets[i], BPCounts[i] * 4);
            if (why != null) bad.Add($"BP shop {i}: {BPCounts[i]} slots at 0x{BPOffsets[i]:X} {why}");
        }
        return bad;
    }

    /// <summary>Why a list of this size at this offset would not be readable, or null if it is fine.</summary>
    private static string? Placement(byte[] cro, int offset, int length)
    {
        if (offset + length > cro.Length)
            return "runs past the end of the file";

        uint tbl = BitConverter.ToUInt32(cro, 0xC8);
        uint n = BitConverter.ToUInt32(cro, 0xCC);
        for (uint i = 0; i < n; i++)
        {
            int e = (int)(tbl + (i * 12));
            if (e + 12 > cro.Length) break;
            uint o = BitConverter.ToUInt32(cro, e), s = BitConverter.ToUInt32(cro, e + 4);
            if (o == 0 || s == 0) continue;
            if (offset >= o && offset + length <= o + s) return null;
        }
        return "is not inside any segment the loader maps";
    }

    /// <summary>
    /// Points a shop at a new list and records its new length.
    /// </summary>
    public static bool Repoint(byte[] cro, uint patchAddr, int newOffset, int intoSegment = -1)
    {
        try
        {
            uint rpt = BitConverter.ToUInt32(cro, 0x128);
            uint entry = rpt + patchAddr;
            if (entry + 12 > cro.Length) return false;

            int targetSeg = intoSegment >= 0 ? intoSegment : cro[entry + 5];
            uint segTable = BitConverter.ToUInt32(cro, 0xC8);
            int baseField = (int)segTable + (targetSeg * 12);
            if (baseField + 4 > cro.Length) return false;

            uint segStart = BitConverter.ToUInt32(cro, baseField);
            if (newOffset < segStart) return false;

            // The segment a patch resolves against is part of the entry, so moving a list into a
            // different segment means writing that too.
            cro[entry + 5] = (byte)targetSeg;
            BitConverter.GetBytes((uint)(newOffset - segStart)).CopyTo(cro, (int)entry + 8);
            return true;
        }
        catch { return false; }
    }

    /// <summary>The segment the shop lists themselves resolve against, taken from their own patches.</summary>
    private static int ListSegment(byte[] cro)
    {
        uint rpt = BitConverter.ToUInt32(cro, 0x128);
        foreach (uint a in MartPatchAddrs)
        {
            uint e = rpt + a;
            if (e + 12 > cro.Length) continue;
            int seg = cro[e + 5];
            if (seg is >= 0 and < 8) return seg;
        }
        return -1;
    }

    /// <summary>A run of unused bytes at the tail of a segment.</summary>
    private readonly record struct FreeSpace(int Segment, uint SegStart, int Start, int End)
    {
        public int Size => End - Start;
    }

    /// <summary>
    /// The unused tail of the segment the shop lists live in.
    /// </summary>
    private static FreeSpace FindFreeSpace(byte[] cro, int segIdx)
    {
        uint tbl = BitConverter.ToUInt32(cro, 0xC8);
        uint n = BitConverter.ToUInt32(cro, 0xCC);
        if (segIdx < 0 || segIdx >= n) return default;

        int entry = (int)(tbl + (segIdx * 12));
        if (entry + 12 > cro.Length) return default;

        uint segStart = BitConverter.ToUInt32(cro, entry);
        uint segSize = BitConverter.ToUInt32(cro, entry + 4);
        if (segStart == 0) return default;

        int start = (int)((segStart + segSize + 3) & ~3);
        int end = cro.Length;

        // Every offset the header records is something real; the pool may not reach any of them.
        var fields = new List<int> { 0x84, 0xB0, 0xB8 };
        for (int x = 0; x < 15; x++) fields.Add(0xC0 + (x * 8));
        foreach (int f in fields)
        {
            if (f + 4 > cro.Length) continue;
            uint v = BitConverter.ToUInt32(cro, f);
            if (v > start && v < end) end = (int)v;
        }

        // ...and stop at the first byte that is not padding, whatever the header claims.
        int free = start;
        while (free < end && (cro[free] == 0x00 || cro[free] == 0xCC)) free++;

        return new FreeSpace(segIdx, segStart, start, free);
    }

    /// <summary>
    /// Grows shops to the requested sizes and rewrites every list, returning the new Shop.cro.
    /// </summary>
    public static byte[] Rebuild(byte[] cro, IReadOnlyList<int[]> regularLists, IReadOnlyList<int[]> bpLists,
                                 out List<string> log, ushort bpPrice = 0)
    {
        log = [];
        var layout = Read(cro);
        if (!layout.Valid) { log.Add("layout could not be read; nothing was changed"); return cro; }

        // Which shops need more room than they have. Everything else is written in place.
        var grow = new List<(int Idx, bool IsBP, int[] List)>();
        for (int i = 0; i < regularLists.Count && i < layout.ShopOffsets.Length; i++)
            if (regularLists[i] != null && regularLists[i].Length > layout.ShopCounts[i])
                grow.Add((i, false, regularLists[i]));
        for (int i = 0; i < bpLists.Count && i < layout.BPOffsets.Length; i++)
            if (bpLists[i] != null && bpLists[i].Length > layout.BPCounts[i])
                grow.Add((i, true, bpLists[i]));

        byte[] data = (byte[])cro.Clone();
        if (grow.Count > 0)
        {
            int segIdx = ListSegment(data);
            var pool = FindFreeSpace(data, segIdx);
            if (pool.Size <= 0)
            {
                log.Add("no free space at the tail of the shop segment; every shop kept its original size");
            }
            else
            {
                log.Add($"free space 0x{pool.Start:X}..0x{pool.End:X} ({pool.Size} bytes) at the tail of segment {segIdx}");

                var after = Read(data);
                int cursor = pool.Start;
                foreach (var (idx, isBP, list) in grow)
                {
                    int width = isBP ? 4 : 2;
                    int need = list.Length * width;
                    if (cursor + need > pool.End)
                    {
                        log.Add($"shop {idx}{(isBP ? " (BP)" : "")}: {list.Length} slots need {need} bytes and only {pool.End - cursor} are left; kept at its original size");
                        continue;
                    }

                    uint patch = isBP ? BPPatchAddrs[idx] : MartPatchAddrs[idx];
                    if (!Repoint(data, patch, cursor, segIdx))
                    { log.Add($"shop {idx}{(isBP ? " (BP)" : "")}: could not be repointed; left at its old size"); continue; }

                    after.SetCount(data, idx, list.Length, isBP);
                    log.Add($"shop {idx}{(isBP ? " (BP)" : "")}: {list.Length} slots at 0x{cursor:X}");
                    cursor = (cursor + need + 3) & ~3;
                }

                // The segment has to admit to owning the bytes now written into its tail.
                if (cursor > pool.Start)
                {
                    uint tbl = BitConverter.ToUInt32(data, 0xC8);
                    int entry = (int)(tbl + (segIdx * 12));
                    uint newSize = (uint)cursor - pool.SegStart;
                    BitConverter.GetBytes(newSize).CopyTo(data, entry + 4);
                    log.Add($"segment {segIdx} now 0x{pool.SegStart:X}..0x{pool.SegStart + newSize:X}");
                }
            }
        }

        // Write every list at wherever it now lives.
        var final = Read(data);
        for (int i = 0; i < regularLists.Count && i < final.ShopOffsets.Length; i++)
        {
            var list = regularLists[i];
            if (list == null || final.ShopOffsets[i] <= 0) continue;
            int n = Math.Min(list.Length, final.ShopCounts[i]);
            for (int k = 0; k < n; k++)
            {
                int o = final.ShopOffsets[i] + (k * 2);
                if (o + 1 >= data.Length) break;
                BitConverter.GetBytes((ushort)list[k]).CopyTo(data, o);
            }
        }
        for (int i = 0; i < bpLists.Count && i < final.BPOffsets.Length; i++)
        {
            var list = bpLists[i];
            if (list == null || final.BPOffsets[i] <= 0) continue;
            int n = Math.Min(list.Length, final.BPCounts[i]);
            for (int k = 0; k < n; k++)
            {
                int o = final.BPOffsets[i] + (k * 4);
                if (o + 3 >= data.Length) break;
                BitConverter.GetBytes((ushort)list[k]).CopyTo(data, o);
                BitConverter.GetBytes(bpPrice).CopyTo(data, o + 2);
            }
        }

        if (data.Length != cro.Length)
        {
            log.Add($"internal error: rebuild changed the file from {cro.Length} to {data.Length} bytes; nothing was changed");
            return cro;
        }

        return data;
    }

    /// <summary>Writes a shop's slot count into the table the game reads.</summary>
    public bool SetCount(byte[] cro, int shopIdx, int count, bool isBP = false)
    {
        int table = isBP ? CountsBPOffset : CountsOffset;
        if (table <= 0 || shopIdx < 0 || count is < 0 or > 255) return false;
        if (table + shopIdx >= cro.Length) return false;
        cro[table + shopIdx] = (byte)count;
        if (isBP) BPCounts[shopIdx] = count; else ShopCounts[shopIdx] = count;
        return true;
    }
}
