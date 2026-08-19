using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>A single verification finding.</summary>
public sealed class VerifyFinding
{
    public PlanSeverity Severity { get; init; } = PlanSeverity.Warning;
    public string Check { get; init; } = "";
    public string Message { get; init; } = "";
    public override string ToString() => $"[{Check}] {Severity.ToString().ToUpperInvariant()}: {Message}";
}

/// <summary>Outcome of verifying a CRO.</summary>
public sealed class VerifyReport
{
    public List<VerifyFinding> Findings { get; } = [];
    public int RelocationsChecked { get; set; }
    public int MechanicsChecked { get; set; }
    public int ChainsBroken { get; set; }

    public bool Ok => Findings.All(f => f.Severity != PlanSeverity.Error);
    public IEnumerable<VerifyFinding> Errors => Findings.Where(f => f.Severity == PlanSeverity.Error);

    internal void Add(PlanSeverity sev, string check, string msg) =>
        Findings.Add(new VerifyFinding { Severity = sev, Check = check, Message = msg });

    public string Describe()
    {
        var lines = new List<string>
        {
            $"relocations checked: {RelocationsChecked}",
            $"mechanics checked:   {MechanicsChecked} ({ChainsBroken} with broken chains)",
            $"result:              {(Ok ? "OK" : "FAILED")}",
        };
        lines.AddRange(Findings.Select(f => "  " + f));
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Structural checks for a CRO, intended to run immediately after any write.
/// <para>
/// The point is to catch a bad patch here rather than as a crash in Citra, where the failure gives
/// no indication of which edit caused it. Everything checked is an invariant of the format itself,
/// so it holds for vanilla and heavily-modded ROMs alike.
/// </para>
/// </summary>
public static class CroVerifier
{
    /// <summary>Validates relocation entries and mechanic chains.</summary>
    public static VerifyReport Verify(DecompiledCRO cro, int maxFindings = 50)
    {
        var report = new VerifyReport();
        if (cro?.RawData == null)
        {
            report.Add(PlanSeverity.Error, "input", "no CRO supplied");
            return report;
        }

        byte[] data = cro.RawData;
        uint[] starts = cro.SegmentStarts;

        // --- Header/segment sanity ---
        if (cro.Header.FileSize != 0 && cro.Header.FileSize != data.Length)
            report.Add(PlanSeverity.Error, "header",
                $"header file size 0x{cro.Header.FileSize:X} != actual 0x{data.Length:X}");

        for (int i = 0; i < 4; i++)
        {
            var seg = cro.Segments[i];
            if (seg == null) continue;
            // BSS (segment 3) is not backed by file bytes, so it legitimately exceeds the file.
            if (i == 3) continue;
            if (seg.Start + seg.Size > data.Length)
                report.Add(PlanSeverity.Error, "segments",
                    $"segment {i} spans 0x{seg.Start:X}..0x{seg.Start + seg.Size:X}, past end of file 0x{data.Length:X}");
        }

        if (cro.Segments[0] != null && cro.Header.CodeStart != 0 && cro.Segments[0].Start != cro.Header.CodeStart)
            report.Add(PlanSeverity.Error, "header",
                $"header code offset 0x{cro.Header.CodeStart:X} disagrees with segment 0 at 0x{cro.Segments[0].Start:X}");

        if (cro.Segments[2] != null && cro.Segments[2].Size != 0 && cro.Header.DataStart != 0 &&
            cro.Segments[2].Start != cro.Header.DataStart)
            report.Add(PlanSeverity.Error, "header",
                $"header data offset 0x{cro.Header.DataStart:X} disagrees with segment 2 at 0x{cro.Segments[2].Start:X}; " +
                "both records describe the data segment and must be moved together");

        {
            uint tableStart = cro.Header.PatchTableOffset;
            uint tableEnd = tableStart + (cro.Header.PatchTableCount * 12);

            if (tableStart != 0 && tableEnd > data.Length)
                report.Add(PlanSeverity.Error, "relocations",
                    $"the relocation table spans 0x{tableStart:X}..0x{tableEnd:X}, past end of file 0x{data.Length:X}");

            for (int i = 0; i < 4 && tableStart != 0; i++)
            {
                var seg = cro.Segments[i];
                if (seg == null || seg.Size == 0 || i == 3) continue;   // BSS has no file backing
                if (tableStart >= seg.Start + seg.Size || tableEnd <= seg.Start) continue;

                report.Add(PlanSeverity.Error, "relocations",
                    $"the relocation table (0x{tableStart:X}..0x{tableEnd:X}, {cro.Header.PatchTableCount} entries) " +
                    $"overruns segment {i} at 0x{seg.Start:X}..0x{seg.Start + seg.Size:X} by " +
                    $"{Math.Min(tableEnd, seg.Start + seg.Size) - Math.Max(tableStart, seg.Start)} byte(s); " +
                    "move that segment or the table before adding more relocations");
            }
        }

        // --- Relocations ---
        int bad = 0;
        foreach (var rel in cro.Relocations)
        {
            report.RelocationsChecked++;
            if (report.Findings.Count >= maxFindings) break;

            if (rel.WriteSegment > 3)
            { report.Add(PlanSeverity.Error, "relocation", $"#{rel.Index}: write segment {rel.WriteSegment} is invalid"); bad++; continue; }
            if (rel.TargetSegment > 3)
            { report.Add(PlanSeverity.Error, "relocation", $"#{rel.Index}: target segment {rel.TargetSegment} is invalid"); bad++; continue; }

            uint writeTo = rel.AbsoluteWriteTo(starts);
            if (writeTo + 4 > data.Length)
            { report.Add(PlanSeverity.Error, "relocation", $"#{rel.Index}: writes to 0x{writeTo:X}, past end of file"); bad++; continue; }

            // A target in BSS is an offset into unbacked memory, so only file-backed segments are
            // range-checked here.
            if (rel.TargetSegment != 3)
            {
                uint target = rel.AbsoluteTarget(starts);
                if (target > data.Length)
                { report.Add(PlanSeverity.Warning, "relocation", $"#{rel.Index}: points at 0x{target:X}, past end of file"); bad++; }
            }
        }
        if (bad == 0)
            report.Add(PlanSeverity.Info, "relocation", $"all {report.RelocationsChecked} entries reference valid locations");

        if (cro.Header.PatchTableCount != cro.Relocations.Count)
            report.Add(PlanSeverity.Error, "relocation",
                $"header count {cro.Header.PatchTableCount} != {cro.Relocations.Count} parsed entries");

        // --- Mechanic chains ---
        foreach (var table in new[] { cro.MoveTable, cro.AbilityTable, cro.ItemTable })
        {
            if (table == null) continue;
            foreach (var entry in table.Entries)
            {
                report.MechanicsChecked++;
                if (entry.CallFunc == null) continue; // vanilla-empty slot: legitimate

                if (entry.Timings == null)
                {
                    report.ChainsBroken++;
                    if (report.Findings.Count < maxFindings)
                        report.Add(PlanSeverity.Warning, "chain",
                            $"{table.Type} #{entry.Index} has a call function but no timing table");
                    continue;
                }

                foreach (var te in entry.Timings.Entries)
                {
                    if (te.ResolvedFunctionOffset == 0)
                    {
                        report.ChainsBroken++;
                        if (report.Findings.Count < maxFindings)
                            report.Add(PlanSeverity.Warning, "chain",
                                $"{table.Type} #{entry.Index} timing 0x{te.TimingByte:X2} has an unresolved function pointer");
                    }
                    else if (te.ResolvedFunctionOffset > data.Length)
                    {
                        report.ChainsBroken++;
                        if (report.Findings.Count < maxFindings)
                            report.Add(PlanSeverity.Error, "chain",
                                $"{table.Type} #{entry.Index} timing 0x{te.TimingByte:X2} points to 0x{te.ResolvedFunctionOffset:X}, past end of file");
                    }
                }
            }
        }

        return report;
    }

    /// <summary>
    /// Round-trip check: recompiling an unmodified decompile must reproduce the original bytes.
    /// A mismatch means the decompile/compile pair is lossy, and any edit made through it risks
    /// corrupting parts of the file nobody touched.
    /// </summary>
    public static VerifyReport VerifyRoundTrip(byte[] original, string sourcePath = null)
    {
        var report = new VerifyReport();
        if (original == null || original.Length == 0)
        {
            report.Add(PlanSeverity.Error, "roundtrip", "no data supplied");
            return report;
        }

        DecompiledCRO cro;
        try { cro = CRODecompiler.DecompileStructure((byte[])original.Clone(), sourcePath); }
        catch (Exception ex)
        {
            report.Add(PlanSeverity.Error, "roundtrip", $"decompile threw {ex.GetType().Name}: {ex.Message}");
            return report;
        }

        byte[] rebuilt;
        try { rebuilt = CROCompiler.Compile(cro); }
        catch (Exception ex)
        {
            report.Add(PlanSeverity.Error, "roundtrip", $"compile threw {ex.GetType().Name}: {ex.Message}");
            return report;
        }

        if (rebuilt.Length != original.Length)
        {
            report.Add(PlanSeverity.Error, "roundtrip",
                $"size changed: 0x{original.Length:X} -> 0x{rebuilt.Length:X}");
            return report;
        }

        // Hashes at 0x00-0x7F are recomputed by design, so differences there are expected.
        var diffs = new List<int>();
        for (int i = 0x80; i < original.Length; i++)
        {
            if (original[i] != rebuilt[i]) { diffs.Add(i); if (diffs.Count > 16) break; }
        }

        if (diffs.Count == 0)
            report.Add(PlanSeverity.Info, "roundtrip", "byte-identical outside the hash block");
        else
            report.Add(PlanSeverity.Error, "roundtrip",
                $"{(diffs.Count > 16 ? ">16" : diffs.Count.ToString())} byte(s) differ, first at 0x{diffs[0]:X} " +
                $"(orig 0x{original[diffs[0]]:X2} -> rebuilt 0x{rebuilt[diffs[0]]:X2})");

        return report;
    }
}
