using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>A branch planted over an existing instruction, diverting into new code.</summary>
public sealed class CodeHook
{
    public string Name { get; set; } = "";
    /// <summary>Instruction replaced by the branch.</summary>
    public uint Site { get; set; }
    /// <summary>
    /// Body of the diverted-to block, one instruction per line. Assembled at the address the block
    /// actually lands on, so absolute branch targets written by hand resolve correctly.
    /// </summary>
    public List<string> Assembly { get; set; } = [];
    /// <summary>Raw machine code, as an alternative to <see cref="Assembly"/>.</summary>
    public string HexCode { get; set; }
    /// <summary>Use BL rather than B, when the block is meant to return to the site.</summary>
    public bool Link { get; set; }
}

/// <summary>A run of instructions to neutralise.</summary>
public sealed class CodeNop
{
    public string Name { get; set; } = "";
    public uint Offset { get; set; }
    /// <summary>Bytes to blank; must be a multiple of 4.</summary>
    public int Length { get; set; }
    /// <summary>When set, the bytes must currently match or the patch is refused.</summary>
    public byte[] ExpectedOriginal { get; set; }
}

/// <summary>Outcome of a hook / nop / corpus patch run.</summary>
public sealed class CodePatchResult
{
    public bool Success { get; set; } = true;
    public List<string> Log { get; } = [];
    public List<string> Errors { get; } = [];
    public uint BlockOffset { get; set; }
    public int BlockLength { get; set; }
    public int Applied { get; set; }
    public int Skipped { get; set; }

    internal void Say(string s) => Log.Add(s);
    internal void Fail(string s) { Errors.Add(s); Success = false; }

    public string Describe() =>
        string.Join(Environment.NewLine, Log.Concat(Errors.Select(e => "ERROR: " + e)));
}

/// <summary>
/// Applies the two kinds of patch that are not master-table work: neutralising existing code, and
/// hooking a site so it detours through new code in the reserve.
/// <para>
/// This is the shape almost every behavioural tweak takes. Turning Hail into Snow, for instance, is
/// three NOPs over the chip-damage call plus a branch at the damage-calculation site into a short
/// block that adds the Ice Defense boost — no new mechanic, no table growth, just a redirect. Doing
/// that by hand means assembling the block, working out where it landed, and encoding a branch with
/// the right displacement; all three are done here so the author only writes the instructions.
/// </para>
/// </summary>
public static class CodePatchInstaller
{
    /// <summary>
    /// Writes patches the research notes recorded, straight into a binary.
    /// </summary>
    public static CodePatchResult ApplyRecorded(
        byte[] rom, IEnumerable<ResearchPatch> patches, bool expectOriginal = false, byte[] original = null)
    {
        var result = new CodePatchResult();
        if (rom == null) { result.Fail("no binary supplied"); return result; }

        int skipped = 0;
        foreach (var p in patches ?? [])
        {
            if (p?.Bytes == null || p.Bytes.Length == 0) continue;

            if (p.Offset + p.Bytes.Length > rom.Length)
            {
                result.Fail($"0x{p.Offset:X6}+{p.Bytes.Length} runs past the end of this binary " +
                            $"(0x{rom.Length:X}) - the notes describe a different file");
                continue;
            }

            // Already applied? Then this is a no-op, not a failure.
            bool same = true;
            for (int i = 0; i < p.Bytes.Length; i++)
                if (rom[p.Offset + i] != p.Bytes[i]) { same = false; break; }
            if (same) { skipped++; continue; }

            if (expectOriginal && original is { Length: > 0 })
            {
                bool matchesStock = p.Offset + p.Bytes.Length <= original.Length;
                if (matchesStock)
                {
                    for (int i = 0; i < p.Bytes.Length; i++)
                        if (rom[p.Offset + i] != original[p.Offset + i]) { matchesStock = false; break; }
                }
                if (!matchesStock)
                {
                    result.Fail($"0x{p.Offset:X6} already differs from the stock binary - " +
                                "something else changed it, so this patch is not being written over the top");
                    continue;
                }
            }

            p.Bytes.CopyTo(rom, (int)p.Offset);
            result.Applied++;
            result.Say($"0x{p.Offset:X6}  {p.Bytes.Length} byte(s)" +
                       (string.IsNullOrWhiteSpace(p.Note) ? "" : $"  {Flatten(p.Note)}"));
        }

        if (skipped > 0) result.Say($"{skipped} patch(es) were already present and left alone");
        return result;
    }

    /// <summary>Replaces instructions with <c>MOV R0,R0</c>, optionally verifying what was there.</summary>
    public static CodePatchResult Nop(byte[] rom, IEnumerable<CodeNop> nops)
    {
        var result = new CodePatchResult();
        if (rom == null) { result.Fail("no binary supplied"); return result; }

        foreach (var n in nops ?? [])
        {
            if (n.Length <= 0 || n.Length % 4 != 0)
            { result.Fail($"{n.Name}: length {n.Length} is not a whole number of ARM instructions"); continue; }
            if (n.Offset + n.Length > rom.Length)
            { result.Fail($"{n.Name}: 0x{n.Offset:X6}+{n.Length} runs past the end of the binary"); continue; }

            if (n.ExpectedOriginal is { Length: > 0 })
            {
                var actual = rom.Skip((int)n.Offset).Take(n.ExpectedOriginal.Length).ToArray();
                if (!actual.SequenceEqual(n.ExpectedOriginal))
                {
                    result.Fail($"{n.Name}: 0x{n.Offset:X6} holds {Convert.ToHexString(actual)}, " +
                                $"expected {Convert.ToHexString(n.ExpectedOriginal)} - refusing to patch");
                    continue;
                }
            }

            string was = ARMCodec.Disassemble(rom.Skip((int)n.Offset).Take(n.Length).ToArray(), n.Offset);
            var nop = ARMCodec.EncodeNOP();
            for (int i = 0; i < n.Length; i += 4) nop.CopyTo(rom, (int)n.Offset + i);

            result.Applied++;
            result.Say($"{n.Name}: NOP x{n.Length / 4} at 0x{n.Offset:X6} (was: {Flatten(was)})");
        }
        return result;
    }

    /// <summary>
    /// Places <paramref name="hook"/>'s block in the reserve and branches to it from the hook site.
    /// <para>
    /// The block is assembled at its final address rather than at zero, because these blocks are
    /// written with absolute targets — a hand-authored <c>bne 0x0001FA68</c> means that address in
    /// the ROM, and only assembling in place makes the displacement come out right.
    /// </para>
    /// </summary>
    public static CodePatchResult InstallHook(
        byte[] rom, CodeHook hook, (uint Offset, int Length) reserve, ref uint bump)
    {
        var result = new CodePatchResult();
        if (rom == null || hook == null) { result.Fail("no binary or hook supplied"); return result; }
        if (reserve.Offset == 0 || reserve.Length <= 0) { result.Fail("no code reserve available"); return result; }
        if (hook.Site + 4 > rom.Length) { result.Fail($"hook site 0x{hook.Site:X6} is past the end of the binary"); return result; }

        if (bump < reserve.Offset) bump = reserve.Offset;
        uint block = (bump + 3u) & ~3u;

        byte[] code;
        if (!string.IsNullOrWhiteSpace(hook.HexCode))
        {
            string hex = hook.HexCode.Replace(" ", "").Replace("-", "").Replace("\n", "").Replace("\r", "");
            try { code = Convert.FromHexString(hex); }
            catch (Exception ex) { result.Fail($"{hook.Name}: HexCode could not be parsed: {ex.Message}"); return result; }
        }
        else if (hook.Assembly is { Count: > 0 })
        {
            try { code = ARMCodec.Assemble(string.Join(Environment.NewLine, hook.Assembly), block); }
            catch (Exception ex) { result.Fail($"{hook.Name}: assembly failed: {ex.Message}"); return result; }
        }
        else { result.Fail($"{hook.Name}: neither Assembly nor HexCode supplied"); return result; }

        if (code == null || code.Length == 0) { result.Fail($"{hook.Name}: assembled to nothing"); return result; }
        if (block + code.Length > reserve.Offset + reserve.Length)
        {
            result.Fail($"{hook.Name}: needs {code.Length} bytes, only {reserve.Offset + reserve.Length - block} left in the reserve");
            return result;
        }

        code.CopyTo(rom, (int)block);

        uint displaced = BitConverter.ToUInt32(rom, (int)hook.Site);
        var branch = hook.Link ? ARMCodec.EncodeBranchLink(hook.Site, block) : ARMCodec.EncodeBranch(hook.Site, block);
        branch.CopyTo(rom, (int)hook.Site);

        bump = block + (uint)code.Length;
        result.BlockOffset = block;
        result.BlockLength = code.Length;
        result.Applied = 1;
        result.Say($"{hook.Name}: block at 0x{block:X6} ({code.Length} bytes); " +
                   $"0x{hook.Site:X6} {(hook.Link ? "BL" : "B")} 0x{block:X6} " +
                   $"(displaced: {Flatten(ARMCodec.DisassembleWord(displaced, hook.Site))})");
        return result;
    }

    /// <summary>
    /// Checks a corpus patch list against the binary without changing anything — the "does this ROM
    /// look like the one the notes were written against" test that has to pass before applying.
    /// </summary>
    public static (int Matched, int Total, List<string> Mismatches) Verify(byte[] rom, IEnumerable<ResearchPatch> expected)
    {
        var mismatches = new List<string>();
        int matched = 0, total = 0;

        foreach (var p in expected ?? [])
        {
            if (p.Bytes is not { Length: > 0 }) continue;
            total++;
            if (p.Offset + p.Bytes.Length > rom.Length)
            { mismatches.Add($"0x{p.Offset:X6}: past the end of the binary"); continue; }

            var actual = new byte[p.Bytes.Length];
            Array.Copy(rom, (int)p.Offset, actual, 0, actual.Length);
            if (actual.SequenceEqual(p.Bytes)) matched++;
            else mismatches.Add($"0x{p.Offset:X6}: has {Convert.ToHexString(actual)}, expected {Convert.ToHexString(p.Bytes)}");
        }
        return (matched, total, mismatches);
    }

    /// <summary>
    /// Writes a corpus patch list. When <paramref name="expected"/> is supplied every site must
    /// currently hold its documented original, so a ROM the notes do not describe is rejected
    /// outright instead of being half-patched.
    /// </summary>
    public static CodePatchResult Apply(
        byte[] rom, IEnumerable<ResearchPatch> patches, IEnumerable<ResearchPatch> expected = null, bool requireAllExpected = true)
    {
        var result = new CodePatchResult();
        if (rom == null) { result.Fail("no binary supplied"); return result; }

        var list = (patches ?? []).Where(p => p.Bytes is { Length: > 0 }).ToList();
        if (list.Count == 0) { result.Fail("patch list carries no bytes to write"); return result; }

        if (expected != null)
        {
            var (matched, total, mismatches) = Verify(rom, expected);
            result.Say($"pre-check: {matched}/{total} documented original site(s) match");
            if (requireAllExpected && matched != total)
            {
                foreach (var m in mismatches.Take(8)) result.Say("  " + m);
                if (mismatches.Count > 8) result.Say($"  ... and {mismatches.Count - 8} more");
                result.Fail("this binary does not match the documented original state; refusing to apply");
                return result;
            }
        }

        // Overlapping writes mean the notes contradict themselves; applying both would leave a
        // result that matches neither.
        var overlaps = FindOverlaps(list);
        foreach (var o in overlaps) result.Say("! " + o);
        if (overlaps.Count > 0) { result.Fail($"{overlaps.Count} overlapping write(s) in the patch list"); return result; }

        foreach (var p in list)
        {
            if (p.Offset + p.Bytes.Length > rom.Length)
            { result.Fail($"0x{p.Offset:X6}: past the end of the binary"); continue; }
            p.Bytes.CopyTo(rom, (int)p.Offset);
            result.Applied++;
        }

        result.Say($"applied {result.Applied} patch(es), {list.Sum(p => p.Bytes.Length)} bytes");
        return result;
    }

    private static List<string> FindOverlaps(List<ResearchPatch> list)
    {
        var sorted = list.OrderBy(p => p.Offset).ToList();
        var found = new List<string>();
        for (int i = 1; i < sorted.Count; i++)
        {
            uint prevEnd = sorted[i - 1].Offset + (uint)sorted[i - 1].Bytes.Length;
            if (sorted[i].Offset < prevEnd)
                found.Add($"0x{sorted[i - 1].Offset:X6}+{sorted[i - 1].Bytes.Length} overlaps 0x{sorted[i].Offset:X6} " +
                          $"({sorted[i - 1].Origin} vs {sorted[i].Origin})");
        }
        return found;
    }

    private static string Flatten(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var f = s.Replace('\r', ' ').Replace('\n', ';').Trim();
        while (f.Contains("  ")) f = f.Replace("  ", " ");
        return f.Length <= 120 ? f : f[..117] + "...";
    }
}
