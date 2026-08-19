#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>Where one sheet's recorded writes belong, and how confident that is.</summary>
public sealed class ResolvedSheetTarget
{
    public required ResearchSheet Sheet { get; init; }
    public ResearchTarget Target { get; set; }
    public List<ResearchPatch> Originals { get; } = [];
    public List<ResearchPatch> Edits { get; } = [];

    /// <summary>Originals confirmed present in the chosen binary.</summary>
    public int OriginalsMatched { get; set; }
    public string Reason { get; set; } = "";

    /// <summary>
    /// Amount to add to every recorded offset to reach the real file position.
    /// </summary>
    public uint OffsetDelta { get; set; }

    /// <summary>
    /// Extra per-row shift on top of <see cref="OffsetDelta"/>, for a feature that did not move as
    /// one block between games. Null means every row shifts alike.
    /// </summary>
    public Func<uint, int>? PerRowShift { get; set; }

    /// <summary>Only a verified original set proves the sheet is aimed at this build.</summary>
    public bool Verified => Originals.Count > 0 && OriginalsMatched == Originals.Count;

    /// <summary>
    /// True when the target was chosen only because the offsets fit, with no evidence behind it.
    /// </summary>
    public bool ResolvedByFit { get; set; }

    /// <summary>Workbook this sheet came from, so siblings can be grouped.</summary>
    public string SourceFile => Sheet?.SourceFile ?? "";

    /// <summary>Whether every one of this sheet's writes physically fits in a binary.</summary>
    public bool FitsIn(ResearchTarget target, IReadOnlyDictionary<ResearchTarget, byte[]>? binaries = null)
    {
        if (binaries == null || !binaries.TryGetValue(target, out var bin)) return true;
        return Edits.All(p => p.Offset + OffsetDelta + (ulong)p.Bytes.Length <= (ulong)bin.Length);
    }
}

/// <summary>
/// Turns a research workbook into an applicable edit: which binary each sheet belongs to, which of
/// its rows are the "before" state and which are the writes.
/// </summary>
public static class CorpusFeature
{
    /// <summary>A row is the "before" state when its sheet or its note says so.</summary>
    public static bool IsOriginal(ResearchSheet sheet, ResearchPatch patch) =>
        (sheet.SheetName ?? "").Contains("original", StringComparison.OrdinalIgnoreCase) ||
        (patch.Note ?? "").TrimStart().StartsWith("Original", StringComparison.OrdinalIgnoreCase);

    private static bool Matches(byte[] bin, ResearchPatch p, uint delta = 0)
    {
        long at = (long)p.Offset + delta;
        if (at >= 0 && at + p.Bytes.Length <= bin.Length &&
            Enumerable.Range(0, p.Bytes.Length).All(k => bin[at + k] == p.Bytes[k]))
            return true;

        return AssemblyAgrees(bin, p, delta);
    }

    /// <summary>
    /// Whether a row's assembly text describes the instruction actually at its offset, even though
    /// its hex column does not.
    /// </summary>
    private static bool AssemblyAgrees(byte[] bin, ResearchPatch p, uint delta)
    {
        string text = p.Assembly;
        if (string.IsNullOrWhiteSpace(text) || p.Bytes.Length != 4) return false;

        long at = (long)p.Offset + delta;
        if (at < 0 || at + 4 > bin.Length) return false;

        uint word = BitConverter.ToUInt32(bin, (int)at);
        string actual;
        try { actual = ARMCodec.DisassembleWord(word, (uint)at); }
        catch { return false; }

        static string Norm(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            int c = s.IndexOf(';'); if (c >= 0) s = s[..c];
            c = s.IndexOf('@'); if (c >= 0) s = s[..c];
            if (s.Contains(':')) s = s[(s.LastIndexOf(':') + 1)..];
            var keep = s.Where(ch => char.IsLetterOrDigit(ch)).ToArray();
            s = new string(keep);
            // Branch targets are written with and without leading zeroes; compare numerically.
            return s.TrimStart('0').Length == 0 ? s : s;
        }

        string a = Norm(text), b = Norm(actual);
        if (a.Length < 4 || b.Length < 4) return false;

        // Mnemonic plus operands must agree once formatting is stripped. Leading zeroes on an
        // address are the common cosmetic difference, so compare with them removed too.
        if (a == b) return true;
        static string StripZeros(string s) => System.Text.RegularExpressions.Regex.Replace(s, "0+([0-9a-f])", "$1");
        return StripZeros(a) == StripZeros(b);
    }

    /// <summary>
    /// A sheet's edits with <see cref="ResolvedSheetTarget.OffsetDelta"/> already folded in, ready
    /// to write. Rebasing here rather than at the call site means an unshifted sheet and a shifted
    /// one are applied by exactly the same code.
    /// </summary>
    public static List<ResearchPatch> RebasedEdits(ResolvedSheetTarget r)
    {
        if (r.OffsetDelta == 0 && r.PerRowShift == null) return r.Edits;

        return [.. r.Edits.Select(p => new ResearchPatch
        {
            Offset = (uint)(p.Offset + r.OffsetDelta + (r.PerRowShift?.Invoke(p.Offset) ?? 0)),
            Bytes = p.Bytes,
            Assembly = p.Assembly,
            Note = p.Note,
            Origin = p.Origin,
        })];
    }

    private static ResearchTarget? NameHint(string? sheetName)
    {
        string n = (sheetName ?? "").ToLowerInvariant();
        bool bag = n.Contains("bag");
        bool code = n.Contains("code.bin") || n.Contains("codebin");
        bool battle = n.Contains("battle");
        bool evo = n.Contains("evolution");
        bool field = n.Contains("field");
        bool shop = n.Contains("shop");
        bool box = n.Contains("box");
        bool status = n.Contains("status");

        if (new[] { bag, code, battle, evo, field, shop, box, status }.Count(x => x) != 1) return null;
        if (bag) return ResearchTarget.BagCro;
        if (code) return ResearchTarget.CodeBin;
        if (evo) return ResearchTarget.EvolutionCro;
        if (field) return ResearchTarget.FieldRoCro;
        if (shop) return ResearchTarget.ShopCro;
        if (box) return ResearchTarget.BoxCro;
        if (status) return ResearchTarget.StatusCro;
        return ResearchTarget.BattleCro;
    }

    /// <summary>
    /// Resolves every sheet of a workbook against the loaded binaries.
    /// </summary>
    /// <param name="binaries">The candidate files, by target. Missing entries are simply not chosen.</param>
    public static List<ResolvedSheetTarget> Resolve(
        IEnumerable<ResearchSheet> sheets,
        IReadOnlyDictionary<ResearchTarget, byte[]> binaries)
    {
        var result = new List<ResolvedSheetTarget>();

        foreach (var s in sheets)
        {
            var rows = s.Patches.Where(p => p.Bytes is { Length: > 0 }).ToList();
            if (rows.Count == 0) continue;

            var r = new ResolvedSheetTarget { Sheet = s };
            foreach (var p in rows) (IsOriginal(s, p) ? r.Originals : r.Edits).Add(p);

            foreach (var b in binaries.Values)
            {
                var contested = r.Originals.GroupBy(p => p.Offset)
                    .Where(g => g.Select(p => Convert.ToHexString(p.Bytes)).Distinct().Count() > 1)
                    .ToList();
                if (contested.Count == 0) continue;

                bool moved = false;
                foreach (var g in contested)
                {
                    var present = g.Where(p => Matches(b, p)).ToList();
                    if (present.Count != 1) continue;   // ambiguous in this binary; leave alone

                    foreach (var p in g.Where(p => !ReferenceEquals(p, present[0])))
                    {
                        r.Originals.Remove(p);
                        r.Edits.Add(p);
                        moved = true;
                    }
                }
                if (moved) break;   // one binary explained it; no need to try the others
            }

            if (r.Originals.Count > 0)
            {
                var best = binaries
                    .SelectMany(b => new uint[] { 0u, 0x0010_0000u }.Select(d => (
                        Target: b.Key,
                        Delta: d,
                        Hits: r.Originals.Count(p => Matches(b.Value, p, d)))))
                    .OrderByDescending(x => x.Hits)
                    .ThenBy(x => x.Delta)
                    .FirstOrDefault();

                if (best.Hits > 0)
                {
                    r.Target = best.Target;
                    r.OffsetDelta = best.Delta;
                    r.OriginalsMatched = best.Hits;
                    r.Reason = $"{best.Hits}/{r.Originals.Count} recorded originals found in {best.Target}" +
                               (best.Delta != 0 ? $" at +0x{best.Delta:X}" : "");
                    result.Add(r);
                    continue;
                }
            }

            // 2. Otherwise the sheet's own name, if it names exactly one binary.
            var hint = NameHint(s.SheetName);
            if (hint.HasValue && binaries.ContainsKey(hint.Value))
            {
                r.Target = hint.Value;
                r.Reason = $"sheet name points at {hint.Value}";
                result.Add(r);
                continue;
            }

            var fits = binaries
                .Where(b => rows.All(p => p.Offset + p.Bytes.Length <= b.Value.Length))
                .OrderBy(b => b.Value.Length)
                .ToList();

            if (fits.Count == 0) continue;
            r.Target = fits[0].Key;
            r.Reason = $"unverified: no originals recorded, offsets fit {fits[0].Key}";
            r.ResolvedByFit = true;
            result.Add(r);
        }

        AdoptSiblingTarget(result, binaries);
        return result;
    }

    /// <summary>
    /// Re-points sheets that were resolved by size alone onto the binary their siblings proved.
    /// </summary>
    private static void AdoptSiblingTarget(List<ResolvedSheetTarget> resolved,
                                           IReadOnlyDictionary<ResearchTarget, byte[]> binaries)
    {
        foreach (var group in resolved.GroupBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase))
        {
            // The target that carried the most verified originals in this workbook.
            var proven = group
                .Where(x => !x.ResolvedByFit && x.OriginalsMatched > 0)
                .GroupBy(x => x.Target)
                .OrderByDescending(g => g.Sum(x => x.OriginalsMatched))
                .FirstOrDefault();
            if (proven == null) continue;

            foreach (var x in group.Where(x => x.ResolvedByFit && x.Target != proven.Key).ToList())
            {
                if (!x.FitsIn(proven.Key, binaries)) continue;
                var was = x.Target;
                x.Target = proven.Key;
                x.ResolvedByFit = false;
                x.Reason = $"no originals recorded; follows the rest of this workbook into {proven.Key} " +
                           $"(size alone would have put it in {was})";
            }
        }
    }

    /// <summary>Splits a sheet spanning two binaries by which file each offset can fit in.</summary>
    public static Dictionary<ResearchTarget, List<ResearchPatch>> SplitByFit(
        IEnumerable<ResearchPatch> patches, IReadOnlyDictionary<ResearchTarget, byte[]> binaries)
    {
        var map = new Dictionary<ResearchTarget, List<ResearchPatch>>();
        foreach (var p in patches)
        {
            var fits = binaries.Where(b => p.Offset + p.Bytes.Length <= b.Value.Length)
                               .OrderBy(b => b.Value.Length).ToList();
            if (fits.Count == 0) continue;
            var t = fits[0].Key;
            if (!map.TryGetValue(t, out var l)) map[t] = l = [];
            l.Add(p);
        }
        return map;
    }
}
