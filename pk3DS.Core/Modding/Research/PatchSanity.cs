#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>One thing wrong with a set of writes, found before any of them are made.</summary>
public sealed record SanityFinding(bool Fatal, string Message);

/// <summary>
/// Reads a pending set of writes as ARM code and asks whether it hangs together.
/// </summary>
public static class PatchSanity
{
    /// <summary>An unconditional or linked branch, and where it goes.</summary>
    private static bool IsBranch(uint word, uint at, out long target)
    {
        target = 0;
        if ((word & 0x0E00_0000) != 0x0A00_0000) return false;   // not B / BL
        int imm = (int)(word & 0x00FF_FFFF);
        if ((imm & 0x0080_0000) != 0) imm |= unchecked((int)0xFF00_0000);   // sign extend
        target = at + 8 + ((long)imm * 4);
        return true;
    }

    /// <summary>
    /// Checks a set of writes against the binary they are about to be applied to.
    /// </summary>
    /// <param name="binary">The file as it stands now.</param>
    /// <param name="writes">Offset/bytes pairs the patch will apply.</param>
    /// <param name="label">Name used in the messages.</param>
    public static List<SanityFinding> Check(byte[] binary, IReadOnlyList<(uint Offset, byte[] Bytes)> writes, string label)
    {
        var findings = new List<SanityFinding>();
        if (binary == null || writes == null || writes.Count == 0) return findings;

        uint codeStart = BitConverter.ToUInt32(binary, 0xB0);
        uint codeSize = BitConverter.ToUInt32(binary, 0xB4);
        uint codeEnd = codeStart + codeSize;

        // The file as it will be, so a branch into another part of the same patch resolves.
        var after = (byte[])binary.Clone();
        var written = new HashSet<uint>();
        foreach (var (off, bytes) in writes)
        {
            if (off + bytes.Length > after.Length) continue;
            bytes.CopyTo(after, off);
            for (uint k = 0; k < bytes.Length; k++) written.Add(off + k);
        }

        bool Blank(long at)
        {
            // Four words of zeroes with nothing written over them: padding, not a routine.
            for (long i = at; i < at + 16 && i + 4 <= after.Length; i += 4)
                if (BitConverter.ToUInt32(after, (int)i) != 0) return false;
            return true;
        }

        // 1. Every branch this patch introduces must arrive somewhere that holds code.
        foreach (var (off, bytes) in writes)
        {
            for (int i = 0; i + 4 <= bytes.Length; i += 4)
            {
                uint at = off + (uint)i;
                uint w = BitConverter.ToUInt32(bytes, i);
                if (!IsBranch(w, at, out long tgt)) continue;

                if (tgt < codeStart || tgt >= codeEnd)
                {
                    findings.Add(new(true,
                        $"{label}: the branch written at 0x{at:X6} goes to 0x{tgt:X6}, which is outside " +
                        $"the code segment (0x{codeStart:X6}..0x{codeEnd:X6})"));
                    continue;
                }

                if (Blank(tgt))
                {
                    findings.Add(new(true,
                        $"{label}: the branch written at 0x{at:X6} goes to 0x{tgt:X6}, which is blank " +
                        "padding that this patch never writes - the routine it should reach is missing"));
                }
            }
        }

        var starts = writes
            .Where(w => w.Bytes.Length > 4)          // a single word is a hook, not a replacement
            .Select(w => w.Offset).ToHashSet();

        // Contiguous runs count too: a workbook records one instruction per row, so a replaced
        // routine arrives as many 4-byte writes rather than one long one.
        var byOffset = writes.Select(w => w.Offset).OrderBy(x => x).ToList();
        for (int i = 0; i < byOffset.Count; i++)
        {
            int run = 1;
            while (i + run < byOffset.Count && byOffset[i + run] == byOffset[i] + (uint)(run * 4)) run++;
            if (run > 1) starts.Add(byOffset[i]);
            i += run - 1;
        }

        foreach (uint start in starts)
        {
            if (start + 4 > binary.Length) continue;
            if (BitConverter.ToUInt32(binary, (int)start) == 0) continue;   // was blank; nothing lost

            var callers = new List<uint>();
            for (uint at = codeStart; at + 4 <= codeEnd && at + 4 <= binary.Length; at += 4)
            {
                if (written.Contains(at)) continue;                   // the patch rewrites this caller
                uint w = BitConverter.ToUInt32(binary, (int)at);
                if (IsBranch(w, at, out long t) && t == start) callers.Add(at);
            }

            if (callers.Count > 0)
            {
                findings.Add(new(false,
                    $"{label}: 0x{start:X6} already holds code that {callers.Count} other place(s) branch to " +
                    $"({string.Join(", ", callers.Take(3).Select(c => $"0x{c:X6}"))}), and this patch overwrites it " +
                    "without changing them"));
            }
        }

        return findings;
    }
}
