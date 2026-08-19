using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// A code.bin edit anchored to an exported symbol rather than an address.
/// <para>
/// code.bin has no relocation table to walk and no id fingerprints to match, so nothing about it
/// can be re-derived from its own contents. What it does have is <c>static.crs</c>, which maps
/// every exported symbol to its address in that build. Anchoring to a symbol plus an offset is
/// therefore the only form of code.bin edit that survives a rebuild.
/// </para>
/// </summary>
public sealed class CodeBinAnchoredPatch
{
    public string Name { get; set; } = "";
    /// <summary>Mangled export the address is measured from.</summary>
    public string Symbol { get; set; } = "";
    /// <summary>Byte offset from the symbol's address.</summary>
    public int Offset { get; set; }
    /// <summary>Bytes expected before patching; the run aborts if they differ.</summary>
    public string Original { get; set; } = "";
    /// <summary>Literal replacement, hex. Ignored when <see cref="BranchTo"/> is set.</summary>
    public string Patched { get; set; }
    /// <summary>Name of a <see cref="CodeBinBlock"/> to branch to instead of writing literal bytes.</summary>
    public string BranchTo { get; set; }
    /// <summary>
    /// Byte offset into <see cref="BranchTo"/>'s block. A hook usually enters partway in — the mint
    /// dispatcher sits 0xB4 bytes inside the region that also holds the routine it calls, and the
    /// two have to move together to keep their relative branches intact.
    /// </summary>
    public int BranchOffset { get; set; }
    /// <summary>Use BL rather than B.</summary>
    public bool Link { get; set; }

    public byte[] OriginalBytes => Convert.FromHexString(Original);
    public byte[] PatchedBytes => string.IsNullOrEmpty(Patched) ? null : Convert.FromHexString(Patched);
}

/// <summary>A routine placed in code.bin's free space, rebased on arrival.</summary>
public sealed class CodeBinBlock
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>Address the bytes were assembled for, as "0x005B9AAC".</summary>
    public string SourceBase { get; set; } = "0x0";
    /// <summary>
    /// Symbols this block calls. Each is resolved on the target and the matching call re-encoded,
    /// so a block that calls FlipTokuseiIndex keeps working when that routine moves.
    /// </summary>
    public List<string> CallsSymbols { get; set; } = [];

    public byte[] CodeBytes => Convert.FromHexString(Code);
    public uint SourceBaseValue => Convert.ToUInt32(SourceBase, 16);
}

/// <summary>The code.bin half of a port.</summary>
public sealed class CodeBinPort
{
    /// <summary>Where code.bin is mapped at run time. 0x00100000 for USUM.</summary>
    public uint LoadBase { get; set; } = 0x00100000;
    public List<CodeBinBlock> Blocks { get; set; } = [];
    public List<CodeBinAnchoredPatch> Patches { get; set; } = [];
}

/// <summary>Applies a <see cref="CodeBinPort"/> to a target build's code.bin.</summary>
public static class PortCodeBinInstaller
{
    /// <summary>
    /// Resolves symbols, places blocks in free space, then writes the anchored patches.
    /// Nothing is written unless every patch's recorded original still matches.
    /// </summary>
    public static PortResult Apply(byte[] codeBin, CodeBinPort port, string romFsPath, Action<string> log = null)
    {
        var result = new PortResult();
        log ??= _ => { };
        if (codeBin == null || port == null) { result.Fail("no code.bin or port supplied"); return result; }

        // --- symbols ---
        var needed = port.Patches.Select(p => p.Symbol)
            .Concat(port.Blocks.SelectMany(b => b.CallsSymbols))
            .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var syms = PortDataInstaller.ResolveStaticSymbols(romFsPath, needed);
        result.Say($"symbols: {syms.Count}/{needed.Count} resolved from static.crs");
        foreach (string s in needed.Where(s => !syms.ContainsKey(s)))
            result.Fail($"symbol not exported by this build: {s}");
        if (!result.Success) return result;

        uint File2Load(uint file) => file + port.LoadBase;
        uint Load2File(uint load) => load - port.LoadBase;

        // --- verify every anchor before touching anything ---
        var todo = new List<CodeBinAnchoredPatch>();
        foreach (var p in port.Patches)
        {
            uint at = Load2File(syms[p.Symbol] + (uint)p.Offset);
            var want = p.OriginalBytes;
            if (at + want.Length > codeBin.Length) { result.Fail($"{p.Name}: past the end of code.bin"); continue; }

            var actual = new byte[want.Length];
            Array.Copy(codeBin, (int)at, actual, 0, want.Length);
            if (actual.SequenceEqual(want)) { todo.Add(p); continue; }

            if (!string.IsNullOrEmpty(p.BranchTo) && want.Length == 4 &&
                ARMCodec.IsBranch(BitConverter.ToUInt32(actual, 0)))
            {
                result.Say($"  {p.Name}: {p.Symbol}+0x{p.Offset:X} already hooked " +
                           $"(-> 0x{ARMCodec.DecodeBranchTarget(BitConverter.ToUInt32(actual, 0), syms[p.Symbol] + (uint)p.Offset):X8}) - skipped");
                continue;
            }

            result.Fail($"{p.Name}: at {p.Symbol}+0x{p.Offset:X} the build holds {Convert.ToHexString(actual)}, " +
                        $"expected {Convert.ToHexString(want)}");
        }
        if (!result.Success) { result.Say("refusing to write: this build differs at an anchor"); return result; }
        if (todo.Count == 0) { result.Say("  code.bin: nothing to do - every anchor is already patched"); result.Output = codeBin; return result; }

        // --- free space for the blocks ---
        var work = (byte[])codeBin.Clone();
        uint free = FindFreeSpace(work, port.Blocks.Sum(b => (b.CodeBytes.Length + 3) & ~3));
        if (free == 0 && port.Blocks.Count > 0) { result.Fail("no contiguous free space in code.bin for the blocks"); return result; }
        uint bump = free;

        foreach (var b in port.Blocks)
        {
            byte[] body = b.CodeBytes;
            uint at = (bump + 3u) & ~3u;
            uint loadAt = File2Load(at);

            // Rebase, treating the source's own symbol calls as external so they re-aim correctly.
            var (rebased, rep) = CodeRelocator.RebaseCode(body, b.SourceBaseValue, loadAt);
            rebased.CopyTo(work, (int)at);
            bump = at + (uint)rebased.Length;
            result.Placed[b.Name] = loadAt;
            result.Say($"  block '{b.Name}' 0x{b.SourceBaseValue:X8} -> 0x{loadAt:X8} " +
                       $"({rep.ExternalBranchesFixed} external, {rep.InternalBranchesKept} internal)");
            foreach (var w in rep.Warnings) result.Say($"    ! {w}");
        }

        // --- patches ---
        foreach (var p in todo)
        {
            uint fileAt = Load2File(syms[p.Symbol] + (uint)p.Offset);
            uint loadAt = File2Load(fileAt);

            if (!string.IsNullOrEmpty(p.BranchTo))
            {
                if (!result.Placed.TryGetValue(p.BranchTo, out uint blockAt))
                { result.Fail($"{p.Name}: block '{p.BranchTo}' was not placed"); continue; }
                uint dest = blockAt + (uint)p.BranchOffset;
                var br = p.Link ? ARMCodec.EncodeBranchLink(loadAt, dest) : ARMCodec.EncodeBranch(loadAt, dest);
                br.CopyTo(work, (int)fileAt);
                result.Say($"  {p.Name}: {p.Symbol}+0x{p.Offset:X} -> {(p.Link ? "BL" : "B")} 0x{dest:X8}");
            }
            else
            {
                var bytes = p.PatchedBytes;
                if (bytes == null) { result.Fail($"{p.Name}: neither Patched nor BranchTo"); continue; }
                bytes.CopyTo(work, (int)fileAt);
                result.Say($"  {p.Name}: {p.Symbol}+0x{p.Offset:X} <- {Convert.ToHexString(bytes)}");
            }
        }

        result.Output = work;
        return result;
    }

    /// <summary>
    /// Largest run of zero bytes able to hold <paramref name="need"/>, searched from the end so new
    /// code lands after everything the game uses rather than in a gap between functions.
    /// </summary>
    public static uint FindFreeSpace(byte[] code, int need, int alignment = 4)
    {
        if (need <= 0) return 0;
        int run = 0;
        for (int i = code.Length - 1; i >= 0; i--)
        {
            if (code[i] == 0) { run++; continue; }
            if (run >= need + alignment)
            {
                uint start = (uint)(i + 1);
                return (start + (uint)alignment - 1) & ~((uint)alignment - 1);
            }
            run = 0;
        }
        return 0;
    }
}
