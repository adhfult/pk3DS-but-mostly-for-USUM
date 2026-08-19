using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>Address-anchored edits to a CRO that has no master tables of its own.</summary>
public sealed class CroSitePort
{
    /// <summary>Routines placed in the CRO's own free space; hooks branch into these.</summary>
    public List<PortedBlock> Blocks { get; set; } = [];

    /// <summary>Fixed-address edits, each carrying the bytes that were there before.</summary>
    public List<PortedSitePatch> SitePatches { get; set; } = [];
}

/// <summary>
/// Applies a <see cref="CroSitePort"/> to a CRO such as Bag.cro.
/// <para>
/// Simpler than the Battle.cro path because there is nothing to re-derive: no tables to locate, no
/// routines to resolve through a donor. That makes the recorded originals the whole safety net, so
/// a build that differs at any anchor is refused outright rather than half-patched.
/// </para>
/// </summary>
public static class PortCroSiteInstaller
{
    public static PortResult Apply(byte[] croBytes, CroSitePort port, string label = "CRO", Action<string> log = null)
    {
        var result = new PortResult();
        log ??= _ => { };
        if (croBytes == null || port == null) { result.Fail($"{label}: no bytes or port supplied"); return result; }

        DecompiledCRO cro;
        try { cro = CRODecompiler.DecompileStructure((byte[])croBytes.Clone()); }
        catch (Exception ex) { result.Fail($"{label}: cannot read the CRO ({ex.Message})"); return result; }

        // ---- verify every anchor before writing a byte ----
        var todo = new List<PortedSitePatch>();
        int already = 0;
        foreach (var p in port.SitePatches)
        {
            if (p.OffsetValue + p.OriginalBytes.Length > cro.RawData.Length)
            { result.Fail($"{label}: 0x{p.OffsetValue:X6} is past the end"); continue; }

            var actual = new byte[p.OriginalBytes.Length];
            Array.Copy(cro.RawData, (int)p.OffsetValue, actual, 0, actual.Length);

            if (actual.SequenceEqual(p.OriginalBytes)) { todo.Add(p); continue; }

            // Re-runnable: a site already holding its own result, or a hook site already branching,
            // was done by an earlier pass rather than being a mismatched build.
            bool done = actual.SequenceEqual(p.PatchedBytes) ||
                        (p.IsHook && actual.Length == 4 && ARMCodec.IsBranch(BitConverter.ToUInt32(actual, 0)));
            if (done) { already++; continue; }

            result.Fail($"{label}: 0x{p.OffsetValue:X6} holds {Convert.ToHexString(actual)}, " +
                        $"expected {Convert.ToHexString(p.OriginalBytes)}");
        }
        result.Say($"{label}: {todo.Count} site(s) to apply" + (already > 0 ? $", {already} already applied" : ""));
        if (!result.Success) { result.Say($"{label}: refusing to write — the build differs at an anchor"); return result; }
        if (todo.Count == 0 && port.Blocks.Count == 0) { result.Output = cro.RawData; return result; }

        // ---- place the blocks in this CRO's own reserve ----
        var reserve = CodeRelocator.FindReserve(cro);
        if (port.Blocks.Count > 0 && reserve.Offset == 0)
        { result.Fail($"{label}: no free space for {port.Blocks.Count} block(s)"); return result; }
        uint bump = reserve.Offset;
        if (port.Blocks.Count > 0)
            result.Say($"{label}: reserve 0x{reserve.Offset:X6} +{reserve.Length}");

        foreach (var b in port.Blocks)
        {
            byte[] body = b.CodeBytes;
            uint at = (bump + 3u) & ~3u;
            if (at + body.Length > reserve.Offset + reserve.Length)
            { result.Fail($"{label}: block '{b.Name}' does not fit"); return result; }

            var (rebased, rep) = CodeRelocator.RebaseCode(body, b.SourceBaseValue, at);
            rebased.CopyTo(cro.RawData, (int)at);
            bump = at + (uint)rebased.Length;
            result.Placed[b.Name] = at;
            result.Say($"  block '{b.Name}' 0x{b.SourceBaseValue:X6} -> 0x{at:X6} " +
                       $"({rep.ExternalBranchesFixed} external, {rep.InternalBranchesKept} internal)");
            foreach (var w in rep.Warnings) result.Say($"    ! {w}");
        }

        // ---- write the sites, re-encoding hooks against where the blocks actually landed ----
        foreach (var p in todo)
        {
            if (p.IsHook && !string.IsNullOrEmpty(p.HookTarget))
            {
                if (!result.Placed.TryGetValue(p.HookTarget, out uint dest))
                {
                    uint authored = p.PatchedBytes.Length == 4
                        ? ARMCodec.DecodeBranchTarget(BitConverter.ToUInt32(p.PatchedBytes, 0), p.OffsetValue)
                        : 0;
                    var owner = port.Blocks.FirstOrDefault(b => authored >= b.SourceBaseValue &&
                                                                authored < b.SourceBaseValue + b.CodeBytes.Length);
                    if (owner == null || !result.Placed.TryGetValue(owner.Name, out uint placedAt))
                    {
                        result.Fail($"{label}: hook at 0x{p.OffsetValue:X6} targets '{p.HookTarget}', which is " +
                                    "neither a block in this package nor an address inside one");
                        continue;
                    }
                    dest = placedAt + (authored - owner.SourceBaseValue);
                }

                uint word = p.PatchedBytes.Length == 4 ? BitConverter.ToUInt32(p.PatchedBytes, 0) : 0xEA000000u;
                uint cond = (word >> 28) & 0xF;
                uint op = ARMCodec.IsBranchLink(word) ? 0x0B000000u : 0x0A000000u;
                uint imm = (uint)((int)(dest - p.OffsetValue - 8) >> 2) & 0x00FFFFFF;
                BitConverter.GetBytes((cond << 28) | op | imm).CopyTo(cro.RawData, (int)p.OffsetValue);
                result.Say($"  hook 0x{p.OffsetValue:X6} -> 0x{dest:X6} ({p.HookTarget}" +
                           $"{(op == 0x0B000000u ? ", BL" : "")})");
            }
            else
            {
                p.PatchedBytes.CopyTo(cro.RawData, (int)p.OffsetValue);
                result.Say($"  patched 0x{p.OffsetValue:X6} ({p.PatchedBytes.Length} bytes)");
            }
        }

        byte[] output;
        try { output = CROCompiler.Compile(cro); }
        catch (Exception ex) { result.Fail($"{label}: could not rebuild ({ex.Message})"); return result; }

        var verify = CroVerifier.Verify(CRODecompiler.DecompileStructure((byte[])output.Clone()));
        result.Say($"{label}: verify {verify.RelocationsChecked} relocations, ok={verify.Ok}");
        if (!verify.Ok)
        {
            foreach (var f in verify.Findings.Where(f => f.Severity == PlanSeverity.Error).Take(5))
                result.Fail($"{label}: {f}");
            return result;
        }

        result.Output = output;
        return result;
    }
}
