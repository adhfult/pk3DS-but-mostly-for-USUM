using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Turns raw addresses into names, using the documented function indexes from the research
/// corpus (~1300 routines for Battle.cro alone).
/// <para>
/// Without this, the editor shows chains like <c>BL 0x00087B58</c> and every decision about which
/// stock routine to call means cross-referencing a spreadsheet by hand. With it the same line
/// reads <c>BL 0x00087B58 ; Get battle index for parameter</c>, which is the difference between
/// the tooling being usable and being a hex editor with extra steps.
/// </para>
/// </summary>
public sealed class ArmSymbolTable
{
    private readonly Dictionary<uint, ResearchFunction> _byOffset;
    private readonly List<(uint Offset, ResearchFunction Fn)> _sorted;

    public ResearchTarget Target { get; }
    public int Count => _byOffset.Count;

    public ArmSymbolTable(ResearchDatabase db, ResearchTarget target)
    {
        Target = target;
        _byOffset = db?.FunctionSymbols(target) ?? [];
        _sorted = _byOffset.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    /// <summary>Exact symbol at an offset, or null.</summary>
    public ResearchFunction Exact(uint offset) => _byOffset.TryGetValue(offset, out var f) ? f : null;

    /// <summary>
    /// Finds a routine by name, so assembly can call a stock function without naming an address.
    /// </summary>
    public IReadOnlyList<(uint Offset, ResearchFunction Fn)> ByName(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return [];
        string needle = Normalise(fragment);

        return [.. _sorted
            .Where(e => e.Fn?.Name != null && Normalise(e.Fn.Name).Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Select(e => (e.Offset, e.Fn))];
    }

    /// <summary>The single routine matching a name, or null when absent or ambiguous.</summary>
    public (uint Offset, ResearchFunction Fn)? UniqueByName(string fragment, out string problem)
    {
        problem = null;
        var hits = ByName(fragment);

        if (hits.Count == 0) { problem = $"no routine in {Target} matches \"{fragment}\""; return null; }
        if (hits.Count > 1)
        {
            problem = $"\"{fragment}\" matches {hits.Count} routines in {Target}"
                    + $" (e.g. {string.Join("; ", hits.Take(3).Select(h => $"0x{h.Offset:X6} {Flatten(h.Fn.Name)}"))})"
                    + " - use a longer fragment";
            return null;
        }
        return hits[0];
    }

    private static string Normalise(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");

    private static string Flatten(string s) =>
        s == null ? "" : Normalise(s).Replace("\n", " ");

    /// <summary>
    /// The symbol containing an offset: the nearest documented routine at or before it, provided
    /// it's within <paramref name="maxDistance"/>. Offsets often land mid-routine (a branch into
    /// the middle of a handler), so an exact-match-only lookup would report almost nothing.
    /// <para>
    /// The default window is deliberately tight. Battle.cro has ~1300 documented entry points, so
    /// a target more than a couple of hundred bytes past the nearest one is far more likely to be
    /// undocumented than to be deep inside the previous routine — and in a tool where people
    /// choose what to patch, a confidently wrong label is worse than no label.
    /// </para>
    /// </summary>
    public (ResearchFunction Fn, uint Delta) Containing(uint offset, uint maxDistance = 0x100)
    {
        if (_sorted.Count == 0) return (null, 0);

        int lo = 0, hi = _sorted.Count - 1, best = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (_sorted[mid].Offset <= offset) { best = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        if (best < 0) return (null, 0);

        uint delta = offset - _sorted[best].Offset;
        return delta <= maxDistance ? (_sorted[best].Fn, delta) : (null, 0);
    }

    /// <summary>Display label for an address: "Name" or "Name+0x1C", else null.</summary>
    public string Label(uint offset)
    {
        var exact = Exact(offset);
        if (exact != null && !string.IsNullOrWhiteSpace(exact.Name)) return Clean(exact.Name);

        var (fn, delta) = Containing(offset);
        if (fn == null || string.IsNullOrWhiteSpace(fn.Name)) return null;
        return delta == 0 ? Clean(fn.Name) : $"{Clean(fn.Name)}+0x{delta:X}";
    }

    private static readonly Regex AddressToken = new(@"0x([0-9A-Fa-f]{6,8})", RegexOptions.Compiled);

    /// <summary>
    /// Appends "; symbol" to any disassembly line referencing a documented address. Lines whose
    /// addresses are unknown are returned untouched, so annotation never hides information.
    /// </summary>
    public string Annotate(string disassembly)
    {
        if (string.IsNullOrEmpty(disassembly) || _sorted.Count == 0) return disassembly;

        var sb = new StringBuilder(disassembly.Length + 64);
        foreach (var line in disassembly.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            sb.Append(trimmed);

            // Skip the leading "0x........:" location stamp; annotate operands only.
            int colon = trimmed.IndexOf(':');
            string operands = colon >= 0 && colon + 1 < trimmed.Length ? trimmed[(colon + 1)..] : trimmed;

            var labels = new List<string>();
            foreach (Match m in AddressToken.Matches(operands))
            {
                if (!uint.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out uint addr))
                    continue;
                string label = Label(addr);
                if (label != null && !labels.Contains(label)) labels.Add(label);
            }

            if (labels.Count > 0) sb.Append("   ; ").Append(string.Join(", ", labels));
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Collapses whitespace/newlines so a multi-line sheet note stays on one line.</summary>
    private static string Clean(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var flat = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (flat.Contains("  ")) flat = flat.Replace("  ", " ");
        return flat.Length <= 80 ? flat : flat[..77] + "...";
    }

    /// <summary>Documented routines whose name or details match a search term.</summary>
    public List<ResearchFunction> Search(string term, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];
        return _sorted
            .Select(x => x.Fn)
            .Where(f =>
                (f.Name != null && f.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (f.Details != null && f.Details.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .ToList();
    }
}
