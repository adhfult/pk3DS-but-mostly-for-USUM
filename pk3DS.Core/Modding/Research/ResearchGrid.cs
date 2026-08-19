using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// A worksheet as a raw 2D grid of cell text, with no assumption that row 0 is a header.
/// <para>
/// The previous reader (<see cref="XlsxResearchParser.ReadSheet"/>) collapsed each sheet into
/// <c>List&lt;Dictionary&lt;string,string&gt;&gt;</c> keyed by row 1's text. That loses information the
/// research workbooks actually depend on:
/// <list type="bullet">
/// <item>several sheets have a title/banner row above the real header row,</item>
/// <item>several have duplicate or blank header texts, so dictionary keys silently overwrite
/// each other and whole columns disappear,</item>
/// <item>column position (which the relocation tracker relies on) is unrecoverable afterwards.</item>
/// </list>
/// Keeping the raw grid means each schema can decide for itself where its header lives and can
/// address columns positionally when the text is ambiguous.
/// </para>
/// </summary>
public sealed class ResearchGrid
{
    public string SourceFile { get; init; } = "";
    public string SheetName { get; init; } = "";

    private readonly List<string[]> _rows = new();

    public int RowCount => _rows.Count;
    public int ColumnCount { get; private set; }

    internal void AddRow(string[] cells)
    {
        _rows.Add(cells);
        if (cells.Length > ColumnCount) ColumnCount = cells.Length;
    }

    /// <summary>Cell text, or "" when out of range. Never returns null.</summary>
    public string this[int row, int col]
    {
        get
        {
            if (row < 0 || row >= _rows.Count) return "";
            var r = _rows[row];
            if (col < 0 || col >= r.Length) return "";
            return r[col] ?? "";
        }
    }

    public IEnumerable<string> Row(int row)
    {
        if (row < 0 || row >= _rows.Count) yield break;
        foreach (var c in _rows[row]) yield return c ?? "";
    }

    public bool IsRowEmpty(int row)
    {
        for (int c = 0; c < ColumnCount; c++)
            if (!string.IsNullOrWhiteSpace(this[row, c])) return false;
        return true;
    }

    /// <summary>
    /// Finds the row index whose cells best match the supplied header names, scanning only the
    /// first <paramref name="searchDepth"/> rows. Returns -1 when no row matches at least
    /// <paramref name="minimumMatches"/> of them. Matching is case-insensitive and substring-based
    /// because the workbooks are inconsistent ("Write-to" vs "write to" vs "WriteTo").
    /// </summary>
    public int FindHeaderRow(IReadOnlyList<string> anyOfThese, int minimumMatches = 2, int searchDepth = 12)
    {
        int bestRow = -1, bestScore = 0;
        int depth = Math.Min(searchDepth, RowCount);
        for (int r = 0; r < depth; r++)
        {
            int score = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                string cell = this[r, c];
                if (string.IsNullOrWhiteSpace(cell)) continue;
                if (anyOfThese.Any(h => cell.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    score++;
            }
            if (score > bestScore) { bestScore = score; bestRow = r; }
        }
        return bestScore >= minimumMatches ? bestRow : -1;
    }

    /// <summary>
    /// Maps a header row into {normalized header text -> column index}. Later duplicates are kept
    /// under a suffixed key rather than overwriting, so no column is ever lost.
    /// </summary>
    public Dictionary<string, int> HeaderMap(int headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < ColumnCount; c++)
        {
            string raw = this[headerRow, c].Trim();
            if (raw.Length == 0) continue;
            string key = raw;
            int dup = 2;
            while (map.ContainsKey(key)) key = $"{raw}#{dup++}";
            map[key] = c;
        }
        return map;
    }

    /// <summary>Column index whose header contains any of the given fragments, else -1.</summary>
    public static int ColumnOf(Dictionary<string, int> headers, params string[] fragments)
    {
        foreach (var frag in fragments)
        {
            foreach (var kv in headers)
                if (kv.Key.Contains(frag, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
        }
        return -1;
    }
}
