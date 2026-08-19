using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>What happened while moving one block of code or data.</summary>
public sealed class RelocationReport
{
    public uint From { get; init; }
    public uint To { get; init; }
    public int Length { get; init; }
    /// <summary>Branches whose destination lay outside the block and were re-encoded.</summary>
    public int ExternalBranchesFixed { get; set; }
    /// <summary>Branches entirely inside the block; their relative encoding still holds.</summary>
    public int InternalBranchesKept { get; set; }
    /// <summary>CRO relocation patches whose write slot moved with the block.</summary>
    public int SlotsRepointed { get; set; }
    /// <summary>CRO relocation patches that pointed <em>at</em> the block and now point at its new home.</summary>
    public int TargetsRepointed { get; set; }
    public List<string> Warnings { get; } = [];

    public override string ToString() =>
        $"0x{From:X6} -> 0x{To:X6} ({Length}B): {ExternalBranchesFixed} branch(es) re-encoded, " +
        $"{InternalBranchesKept} kept, {SlotsRepointed} slot(s) + {TargetsRepointed} target(s) repointed" +
        (Warnings.Count == 0 ? "" : $", {Warnings.Count} warning(s)");
}

/// <summary>
/// Moves ARM code and pointer tables inside a CRO while keeping every reference to them valid.
/// <para>
/// Two independent kinds of reference have to survive a move. Branches encoded in the instructions
/// themselves are PC-relative, so a branch that leaves the block has to be re-encoded from its new
/// address while one that stays inside it must be left alone — re-encoding those too would double
/// the correction and send them into the weeds. Separately, the CRO relocation table holds pointer
/// slots that are blank on disk and filled at load time; when a block containing such a slot moves,
/// the patch that fills it has to be told the slot's new address, and when a block that is
/// <em>pointed at</em> moves, every patch aiming at it needs its addend updated.
/// </para>
/// <para>
/// This is the machinery that makes a master table growable: the move-effect table has real data
/// immediately behind it, so adding an entry means relocating the whole table and repointing all
/// of its per-entry handler patches, not appending in place.
/// </para>
/// </summary>
public static class CodeRelocator
{
    /// <summary>
    /// Copies <paramref name="length"/> bytes from <paramref name="from"/> to <paramref name="to"/>
    /// and re-encodes any branch that would otherwise be left pointing at the wrong place.
    /// <para>
    /// The source bytes are left untouched. Callers that want the original gone should blank it
    /// afterwards; leaving it in place is usually the safer choice, since anything still branching
    /// to the old copy keeps working rather than executing whatever lands there next.
    /// </para>
    /// </summary>
    public static RelocationReport MoveCode(byte[] rom, uint from, int length, uint to)
    {
        ArgumentNullException.ThrowIfNull(rom);
        var report = new RelocationReport { From = from, To = to, Length = length };

        if (length <= 0) { report.Warnings.Add("nothing to move"); return report; }
        if (from + length > rom.Length || to + length > rom.Length)
        { report.Warnings.Add("move would run past the end of the binary"); return report; }

        Array.Copy(rom, (int)from, rom, (int)to, length);

        // Re-encode branches now that the copy sits at a different PC.
        for (int i = 0; i + 4 <= length; i += 4)
        {
            uint word = BitConverter.ToUInt32(rom, (int)to + i);
            if (!ARMCodec.IsBranch(word)) continue;

            uint oldPc = from + (uint)i;
            uint newPc = to + (uint)i;
            uint target = ARMCodec.DecodeBranchTarget(word, oldPc);

            // A branch that stays within the block keeps its relative distance, so the bytes we
            // just copied are already correct.
            if (target >= from && target < from + length) { report.InternalBranchesKept++; continue; }

            // Preserve condition and the link bit; only the 24-bit displacement changes.
            int delta = (int)(target - (newPc + 8)) >> 2;
            if (delta > 0x7FFFFF || delta < -0x800000)
            {
                report.Warnings.Add($"+0x{i:X}: target 0x{target:X6} is out of ARM branch range from 0x{newPc:X6}");
                continue;
            }
            uint rebuilt = (word & 0xFF000000) | ((uint)delta & 0x00FFFFFF);
            BitConverter.GetBytes(rebuilt).CopyTo(rom, (int)to + i);
            report.ExternalBranchesFixed++;
        }

        return report;
    }

    /// <summary>
    /// Returns a copy of <paramref name="body"/> valid at <paramref name="newBase"/>, given that it
    /// was assembled to run at <paramref name="originalBase"/>.
    /// <para>
    /// This is what makes the research corpus usable. Those routines are recorded as machine code
    /// at the address their author placed them, so every <c>BL</c> to a stock helper and every
    /// internal jump is encoded relative to that address; dropping the bytes anywhere else sends
    /// each of them off by exactly the distance moved. Rebasing preserves both meanings — calls out
    /// to the game keep their absolute destination, jumps within the routine keep their relative
    /// one — so a documented effect can be installed wherever there is room rather than only in the
    /// gap its author happened to use.
    /// </para>
    /// </summary>
    public static (byte[] Code, RelocationReport Report) RebaseCode(byte[] body, uint originalBase, uint newBase)
    {
        ArgumentNullException.ThrowIfNull(body);
        var report = new RelocationReport { From = originalBase, To = newBase, Length = body.Length };
        var copy = (byte[])body.Clone();
        if (originalBase == newBase) return (copy, report);

        for (int i = 0; i + 4 <= copy.Length; i += 4)
        {
            uint word = BitConverter.ToUInt32(copy, i);
            if (!ARMCodec.IsBranch(word)) continue;

            uint oldPc = originalBase + (uint)i;
            uint target = ARMCodec.DecodeBranchTarget(word, oldPc);

            if (target >= originalBase && target < originalBase + body.Length)
            { report.InternalBranchesKept++; continue; }

            int delta = (int)(target - (newBase + (uint)i + 8)) >> 2;
            if (delta > 0x7FFFFF || delta < -0x800000)
            {
                report.Warnings.Add($"+0x{i:X}: 0x{target:X6} is out of branch range from 0x{newBase + (uint)i:X6}");
                continue;
            }
            BitConverter.GetBytes((word & 0xFF000000) | ((uint)delta & 0x00FFFFFF)).CopyTo(copy, i);
            report.ExternalBranchesFixed++;
        }

        // PC-relative loads reaching outside the block would need their literal copied too. Flag
        // them rather than guess: a silently wrong literal is far worse than a refusal to install.
        for (int i = 0; i + 4 <= copy.Length; i += 4)
        {
            uint word = BitConverter.ToUInt32(copy, i);
            if ((word & 0x0F7F0000) != 0x051F0000) continue; // LDR Rd, [PC, #+/-imm12]
            int imm = (int)(word & 0xFFF);
            if ((word & 0x00800000) == 0) imm = -imm;
            long literal = originalBase + i + 8 + imm;
            if (literal < originalBase || literal >= originalBase + body.Length)
                report.Warnings.Add($"+0x{i:X}: loads a literal at 0x{literal:X6}, outside the block; verify it after rebasing");
        }

        return (copy, report);
    }

    /// <summary>One piece of a routine being relocated as part of a set.</summary>
    public sealed class CodeBlock
    {
        public string Name { get; set; } = "";
        /// <summary>Address the bytes were assembled for.</summary>
        public uint OriginalBase { get; set; }
        public byte[] Body { get; set; } = [];
        /// <summary>Address it will occupy; set before calling <see cref="RebaseGroup"/>.</summary>
        public uint NewBase { get; set; }
        /// <summary>Data blocks are copied verbatim; nothing in them is a branch.</summary>
        public bool IsData { get; set; }

        public byte[] Rebased { get; internal set; }
        public bool Contains(uint address) => address >= OriginalBase && address < OriginalBase + Body.Length;
    }

    /// <summary>
    /// Rebases several blocks that refer to one another, as a set.
    /// <para>
    /// Rebasing them one at a time is wrong whenever one calls another: an effect function whose
    /// <c>BL</c> targets a helper sitting in a sibling block would be treated as an external call
    /// and kept pointing at the helper's <em>old</em> address — which, for corpus material, is
    /// occupied by unrelated code in the ROM being patched. Loaded Dice is exactly this shape: two
    /// effect functions and a shared "is this a multi-hit move" helper, all moved together.
    /// </para>
    /// </summary>
    public static RelocationReport RebaseGroup(IList<CodeBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var report = new RelocationReport();

        foreach (var block in blocks)
        {
            var copy = (byte[])block.Body.Clone();
            block.Rebased = copy;
            if (block.IsData || block.NewBase == block.OriginalBase) continue;

            for (int i = 0; i + 4 <= copy.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(copy, i);
                if (!ARMCodec.IsBranch(word)) continue;

                uint oldPc = block.OriginalBase + (uint)i;
                uint target = ARMCodec.DecodeBranchTarget(word, oldPc);

                // Where does this land once everything has moved?
                uint newTarget;
                var host = blocks.FirstOrDefault(b => b.Contains(target));
                if (host == null)
                {
                    newTarget = target;                        // out to the game: unchanged
                }
                else if (host == block)
                {
                    report.InternalBranchesKept++;             // within this block: still correct
                    continue;
                }
                else
                {
                    newTarget = host.NewBase + (target - host.OriginalBase);
                    report.Warnings.Add($"{block.Name}+0x{i:X}: cross-block call to {host.Name} " +
                                        $"re-aimed 0x{target:X6} -> 0x{newTarget:X6}");
                }

                int delta = (int)(newTarget - (block.NewBase + (uint)i + 8)) >> 2;
                if (delta > 0x7FFFFF || delta < -0x800000)
                {
                    report.Warnings.Add($"{block.Name}+0x{i:X}: 0x{newTarget:X6} out of branch range");
                    continue;
                }
                BitConverter.GetBytes((word & 0xFF000000) | ((uint)delta & 0x00FFFFFF)).CopyTo(copy, i);
                report.ExternalBranchesFixed++;
            }
        }

        return report;
    }

    /// <summary>
    /// Moves a block and updates the CRO relocation table alongside it, so pointer slots inside the
    /// block and pointers aimed at it both follow.
    /// </summary>
    /// <param name="fixInstructionBranches">
    /// False for pure data (a master table, a timing table) — running the branch fixer over data
    /// would rewrite any word that happens to look like a branch.
    /// </param>
    public static RelocationReport MoveBlock(
        DecompiledCRO cro, uint from, int length, uint to, bool fixInstructionBranches = true)
    {
        ArgumentNullException.ThrowIfNull(cro);

        RelocationReport report;
        if (fixInstructionBranches)
        {
            report = MoveCode(cro.RawData, from, length, to);
        }
        else
        {
            report = new RelocationReport { From = from, To = to, Length = length };
            if (length <= 0 || from + length > cro.RawData.Length || to + length > cro.RawData.Length)
            { report.Warnings.Add("move would run past the end of the binary"); return report; }
            Array.Copy(cro.RawData, (int)from, cro.RawData, (int)to, length);
        }

        report.SlotsRepointed = RepointSlotsIn(cro, from, length, to, report);
        report.TargetsRepointed = RepointTargetsIn(cro, from, length, to, report);
        return report;
    }

    /// <summary>
    /// Re-addresses every relocation whose write slot lies in the moved range. Without this the
    /// game would keep filling the pointers at the block's old address and leave the new copy blank.
    /// </summary>
    public static int RepointSlotsIn(DecompiledCRO cro, uint from, int length, uint to, RelocationReport report = null)
    {
        uint[] segs = cro.SegmentStarts;
        int moved = 0;

        foreach (var r in cro.Relocations)
        {
            uint slot = r.AbsoluteWriteTo(segs);
            if (slot < from || slot >= from + length) continue;

            uint newSlot = to + (slot - from);
            int seg = CROUtil.GetSegmentForAddress(newSlot, cro.RawData);
            if (seg < 0 || seg >= segs.Length)
            {
                report?.Warnings.Add($"slot 0x{newSlot:X6} is outside every segment; relocation {r.Index} left alone");
                continue;
            }
            r.RawWord0 = ((newSlot - segs[seg]) << 4) | (uint)(seg & 0xF);
            moved++;
        }
        return moved;
    }

    /// <summary>Re-addresses every relocation that points into the moved range.</summary>
    public static int RepointTargetsIn(DecompiledCRO cro, uint from, int length, uint to, RelocationReport report = null)
    {
        uint[] segs = cro.SegmentStarts;
        int moved = 0;

        foreach (var r in cro.Relocations)
        {
            uint target = r.AbsoluteTarget(segs);
            if (target < from || target >= from + length) continue;

            uint newTarget = to + (target - from);
            int seg = CROUtil.GetSegmentForAddress(newTarget, cro.RawData);
            if (seg < 0 || seg >= segs.Length)
            {
                report?.Warnings.Add($"target 0x{newTarget:X6} is outside every segment; relocation {r.Index} left alone");
                continue;
            }
            // Segment lives in byte 1 of word 1; the addend is relative to that segment's start.
            r.RawWord1 = (r.RawWord1 & ~0x0000FF00u) | ((uint)(seg & 0xFF) << 8);
            r.Addend = newTarget - segs[seg];
            moved++;
        }
        return moved;
    }

    /// <summary>
    /// Registers a new load-time pointer: <paramref name="slot"/> will be filled with
    /// <paramref name="target"/>. Both are absolute file offsets; the segment split is worked out
    /// here so callers never assemble raw relocation words themselves.
    /// </summary>
    public static bool AddPointer(DecompiledCRO cro, uint slot, uint target, Action<string> log = null)
    {
        uint[] segs = cro.SegmentStarts;
        int slotSeg = CROUtil.GetSegmentForAddress(slot, cro.RawData);
        int tgtSeg = CROUtil.GetSegmentForAddress(target, cro.RawData);

        if (slotSeg < 0 || tgtSeg < 0 || slotSeg >= segs.Length || tgtSeg >= segs.Length)
        {
            log?.Invoke($"cannot add pointer 0x{slot:X6} -> 0x{target:X6}: outside the segment map");
            return false;
        }

        var entry = CRORelocationEntry.Create(slotSeg, slot - segs[slotSeg], tgtSeg, target - segs[tgtSeg]);
        entry.Index = cro.Relocations.Count;
        cro.Relocations.Add(entry);
        cro.Header.PatchTableCount = (uint)cro.Relocations.Count;

        // The slot is blank on disk; the loader writes it. Zeroing makes that explicit and stops a
        // stale value from being mistaken for a resolved pointer when the file is re-read.
        BitConverter.GetBytes(0u).CopyTo(cro.RawData, (int)slot);
        return true;
    }

    /// <summary>
    /// True when nothing in <paramref name="cro"/> refers to the range and the bytes are blank —
    /// the test for whether a table can simply grow in place instead of being relocated.
    /// </summary>
    public static bool IsRangeFree(DecompiledCRO cro, uint start, int length)
    {
        if (length <= 0) return true;
        if (start + length > cro.RawData.Length) return false;

        for (uint i = start; i < start + length; i++)
            if (cro.RawData[i] != 0x00 && cro.RawData[i] != 0xCC) return false;

        uint[] segs = cro.SegmentStarts;
        return !cro.Relocations.Any(r =>
        {
            uint slot = r.AbsoluteWriteTo(segs);
            uint tgt = r.AbsoluteTarget(segs);
            return (slot >= start && slot < start + length) || (tgt >= start && tgt < start + length);
        });
    }

    /// <summary>
    /// Finds the largest usable run of blank bytes in the code segment — the space an earlier
    /// <see cref="CROUtil.ExpandSegment"/> left behind, and where custom routines belong.
    /// <para>
    /// Scanning the whole segment matters rather than just checking the tail: an expansion inserts
    /// its room at the insertion point, not at the end, so the code that followed gets pushed
    /// beyond it. On the Expansion Pack build the real 30 KB arena sits mid-segment at 0x115154
    /// while the tail holds only a 2.4 KB alignment gap — enough to fool a tail-only scan into
    /// reporting a reserve far too small to relocate a master table into.
    /// </para>
    /// <para>
    /// A run only counts when nothing points into it. Blank bytes that some relocation targets are
    /// reserved-but-unfilled space belonging to someone else, and handing them out would quietly
    /// overwrite it.
    /// </para>
    /// </summary>
    public static (uint Offset, int Length) FindReserve(DecompiledCRO cro, int minimum = 0x40)
    {
        uint start = cro.Header.CodeStart;
        uint end = Math.Min(cro.CodeEnd, (uint)cro.RawData.Length);
        if (end <= start) return (0, 0);

        byte[] rom = cro.RawData;
        uint[] segs = cro.SegmentStarts;

        // Addresses anything refers to, so occupied-but-blank regions are not handed out.
        var referenced = new SortedSet<uint>();
        foreach (var r in cro.Relocations)
        {
            referenced.Add(r.AbsoluteWriteTo(segs));
            referenced.Add(r.AbsoluteTarget(segs));
        }

        (uint Offset, int Length) best = (0, 0);
        uint runStart = start;
        bool inRun = false;

        for (uint p = start; p <= end; p++)
        {
            bool blank = p < end && (rom[p] == 0x00 || rom[p] == 0xCC);
            if (blank)
            {
                if (!inRun) { runStart = p; inRun = true; }
                continue;
            }
            if (!inRun) continue;

            Consider(runStart, p);
            inRun = false;
        }

        return best.Length >= minimum ? best : (0, 0);

        void Consider(uint from, uint to)
        {
            uint cursor = from;
            foreach (uint hit in referenced.GetViewBetween(from, to == 0 ? 0 : to - 1))
            {
                Take(cursor, hit);
                cursor = hit + 4;   // skip the referenced word itself
            }
            Take(cursor, to);
        }

        void Take(uint from, uint to)
        {
            uint aligned = (from + 3u) & ~3u;
            if (to <= aligned) return;
            int length = (int)(to - aligned);
            if (length > best.Length) best = (aligned, length);
        }
    }

    /// <summary>
    /// Makes room for <paramref name="extraRelocations"/> more patch entries by sliding the data
    /// segment forward, and returns true when the image was changed.
    /// <para>
    /// Stock Battle.cro leaves <em>zero</em> slack here: the relocation table ends on the exact byte
    /// where .data begins. Adding even one relocation therefore writes patch records over the game's
    /// globals, which loads without complaint and then fails in battle. The fix is to move .data,
    /// and the trap is that its address is recorded in two places — the segment table and the header
    /// field at 0xB8. Updating only the segment table leaves the loader reading relocation records
    /// as globals, which is a harder crash than the one it was meant to fix.
    /// </para>
    /// </summary>
    public static bool EnsureRelocationHeadroom(DecompiledCRO cro, int extraRelocations, Action<string> log = null)
    {
        log ??= _ => { };
        byte[] rom = cro.RawData;
        if (rom == null || extraRelocations <= 0) return false;

        uint U(int o) => BitConverter.ToUInt32(rom, o);
        uint segTab = U(0xC8), segNum = U(0xCC);
        if (segNum < 3) { log("no data segment to move"); return false; }

        int seg2 = (int)segTab + 24;                 // segment 2 = .data
        uint dataStart = U(seg2), dataSize = U(seg2 + 4);
        uint hdrData = U(0xB8);
        uint ipt = U(0x128), num = U(0x12C);
        uint end = ipt + num * 12;

        if (dataStart != hdrData)
            log($"note: segment table says .data 0x{dataStart:X6}, header says 0x{hdrData:X6} - they disagree");

        uint need = ipt + (num + (uint)extraRelocations) * 12;
        if (need <= dataStart)
        {
            log($"relocation headroom: {(dataStart - end) / 12} spare entr(ies); no move needed");
            return false;
        }

        // Slide far enough, rounded up, and only if it still fits inside the existing file.
        uint shift = ((need - dataStart) + 0x1F) & ~0x1Fu;
        uint newStart = dataStart + shift;
        if (newStart + dataSize > rom.Length)
        {
            log($"cannot make room: .data would need to end at 0x{newStart + dataSize:X6}, past the file (0x{rom.Length:X6})");
            return false;
        }

        var data = new byte[dataSize];
        Array.Copy(rom, (int)dataStart, data, 0, (int)dataSize);
        Array.Clear(rom, (int)dataStart, (int)Math.Min(dataSize + shift, (uint)rom.Length - dataStart));
        data.CopyTo(rom, (int)newStart);

        BitConverter.GetBytes(newStart).CopyTo(rom, seg2);   // segment table bytes
        BitConverter.GetBytes(newStart).CopyTo(rom, 0xB8);   // header bytes - both must agree
        cro.Header.DataStart = newStart;

        if (cro.Segments.Length > 2 && cro.Segments[2] != null)
            cro.Segments[2].Start = newStart;

        log($"moved .data 0x{dataStart:X6} -> 0x{newStart:X6} (+0x{shift:X}); " +
            $"room for {(newStart - end) / 12} more relocation(s)");
        return true;
    }

    /// <summary>Blank run at the very end of the code segment. Kept for callers that want only that.</summary>
    public static (uint Offset, int Length) FindTailReserve(DecompiledCRO cro, int minimum = 0x40)
    {
        uint start = cro.Header.CodeStart;
        uint end = Math.Min(cro.CodeEnd, (uint)cro.RawData.Length);
        if (end <= start) return (0, 0);

        uint p = end;
        while (p > start && (cro.RawData[p - 1] == 0x00 || cro.RawData[p - 1] == 0xCC)) p--;

        uint reserve = (p + 3u) & ~3u;
        int length = (int)(end - reserve);
        return length >= minimum ? (reserve, length) : (0, 0);
    }
}
