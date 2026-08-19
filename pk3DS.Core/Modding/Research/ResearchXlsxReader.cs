using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Reads .xlsx worksheets into a raw <see cref="ResearchGrid"/>, reporting problems instead of
/// swallowing them.
/// </summary>
public static class ResearchXlsxReader
{
    /// <summary>Sheet names in workbook order.</summary>
    public static List<string> GetSheetNames(string xlsxPath, IList<string> diagnostics = null)
    {
        var names = new List<string>();
        if (!File.Exists(xlsxPath)) { diagnostics?.Add($"missing file: {xlsxPath}"); return names; }
        try
        {
            using var zip = ZipFile.OpenRead(xlsxPath);
            foreach (var (name, _) in EnumerateSheets(zip)) names.Add(name);
        }
        catch (Exception ex)
        {
            diagnostics?.Add($"{Path.GetFileName(xlsxPath)}: workbook unreadable ({ex.GetType().Name}: {ex.Message})");
        }
        return names;
    }

    /// <summary>
    /// Reads one sheet into a grid. Returns null (and records a diagnostic) when the sheet can't
    /// be read at all; an empty grid is a legitimate result for a genuinely empty sheet.
    /// </summary>
    public static ResearchGrid ReadSheet(string xlsxPath, string sheetName, IList<string> diagnostics = null)
    {
        if (!File.Exists(xlsxPath)) { diagnostics?.Add($"missing file: {xlsxPath}"); return null; }

        try
        {
            using var zip = ZipFile.OpenRead(xlsxPath);
            var shared = ReadSharedStrings(zip);

            string sheetPath = null;
            foreach (var (name, relId) in EnumerateSheets(zip))
            {
                if (!string.Equals(name, sheetName, StringComparison.Ordinal)) continue;
                sheetPath = ResolveSheetPath(zip, relId);
                break;
            }
            if (sheetPath == null)
            {
                diagnostics?.Add($"{Path.GetFileName(xlsxPath)}[{sheetName}]: sheet part not found");
                return null;
            }

            var entry = zip.GetEntry(sheetPath);
            if (entry == null)
            {
                diagnostics?.Add($"{Path.GetFileName(xlsxPath)}[{sheetName}]: part '{sheetPath}' missing");
                return null;
            }

            var grid = new ResearchGrid { SourceFile = xlsxPath, SheetName = sheetName };

            using var s = entry.Open();
            var doc = new XmlDocument();
            doc.Load(s);

            var rowNodes = doc.GetElementsByTagName("row");
            // Spreadsheet rows are 1-based and may be sparse; materialize gaps so row indices in
            // the grid stay aligned with what a person sees in Excel.
            int expectedRow = 1;
            foreach (XmlNode rowNode in rowNodes)
            {
                int rowIndex = expectedRow;
                if (int.TryParse(rowNode.Attributes?["r"]?.Value, out int declared) && declared > 0)
                    rowIndex = declared;

                while (expectedRow < rowIndex) { grid.AddRow([]); expectedRow++; }

                var cells = new List<string>();
                var cellNodes = rowNode.SelectNodes("*[local-name()='c']");
                for (int ci = 0; ci < (cellNodes?.Count ?? 0); ci++)
                {
                    XmlNode cell = cellNodes[ci];
                    int colIndex = cells.Count;
                    string cellRef = cell.Attributes?["r"]?.Value;
                    if (!string.IsNullOrEmpty(cellRef))
                    {
                        int parsed = ColumnIndexFromRef(cellRef);
                        if (parsed >= 0) colIndex = parsed;
                    }
                    while (cells.Count <= colIndex) cells.Add("");
                    cells[colIndex] = GetCellText(cell, shared);
                }

                grid.AddRow(cells.ToArray());
                expectedRow++;
            }

            return grid;
        }
        catch (Exception ex)
        {
            diagnostics?.Add($"{Path.GetFileName(xlsxPath)}[{sheetName}]: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<(string Name, string RelId)> EnumerateSheets(ZipArchive zip)
    {
        var wb = zip.GetEntry("xl/workbook.xml");
        if (wb == null) yield break;

        var doc = new XmlDocument();
        using (var s = wb.Open()) doc.Load(s);

        foreach (XmlNode sheet in doc.GetElementsByTagName("sheet"))
        {
            string name = sheet.Attributes?["name"]?.Value;
            if (name == null) continue;
            // r:id is namespace-prefixed; fall back to a scan so we don't depend on the prefix.
            string relId = sheet.Attributes?["r:id"]?.Value;
            if (relId == null && sheet.Attributes != null)
            {
                foreach (XmlAttribute a in sheet.Attributes)
                    if (a.LocalName == "id") { relId = a.Value; break; }
            }
            yield return (name, relId);
        }
    }

    private static string ResolveSheetPath(ZipArchive zip, string relId)
    {
        const string fallback = "xl/worksheets/sheet1.xml";
        if (relId == null) return fallback;

        var rels = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (rels == null) return fallback;

        var doc = new XmlDocument();
        using (var s = rels.Open()) doc.Load(s);

        foreach (XmlNode rel in doc.GetElementsByTagName("Relationship"))
        {
            if (rel.Attributes?["Id"]?.Value != relId) continue;
            string target = (rel.Attributes?["Target"]?.Value ?? "").TrimStart('/');
            if (target.Length == 0) return fallback;
            return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
        }
        return fallback;
    }

    /// <summary>
    /// Reads sharedStrings.xml, grouping by &lt;si&gt; entry.
    /// <para>
    /// This is deliberately not a flat scan of every &lt;t&gt; node. A shared string containing rich
    /// text is stored as multiple runs — <c>&lt;si&gt;&lt;r&gt;&lt;t&gt;Foo&lt;/t&gt;&lt;/r&gt;&lt;r&gt;&lt;t&gt;Bar&lt;/t&gt;&lt;/r&gt;&lt;/si&gt;</c>
    /// is ONE string "FooBar". Treating each &lt;t&gt; as its own entry (as the previous reader did)
    /// both splits that value and shifts the index of every subsequent shared string, so any sheet
    /// with a single bit of bold/coloured text silently reads whole columns of wrong values from
    /// then on. Several research workbooks use coloured runs heavily, so this mattered.
    /// </para>
    /// </summary>
    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var list = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return list;

        var doc = new XmlDocument();
        using (var s = entry.Open()) doc.Load(s);

        foreach (XmlNode si in doc.GetElementsByTagName("si"))
        {
            // Concatenate every <t> beneath this <si>, skipping phonetic guides (<rPh>).
            var parts = new List<string>();
            CollectText(si, parts);
            list.Add(string.Concat(parts));
        }
        return list;
    }

    private static void CollectText(XmlNode node, List<string> into)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.LocalName == "rPh") continue;      // phonetic, not displayed
            if (child.LocalName == "t") { into.Add(child.InnerText); continue; }
            if (child.HasChildNodes) CollectText(child, into);
        }
    }

    private static string GetCellText(XmlNode cell, List<string> shared)
    {
        string type = cell.Attributes?["t"]?.Value;

        if (type == "inlineStr")
        {
            var parts = new List<string>();
            var isNode = cell.SelectSingleNode("*[local-name()='is']");
            if (isNode != null) CollectText(isNode, parts);
            return string.Concat(parts);
        }

        var v = cell.SelectSingleNode("*[local-name()='v']");
        if (v == null) return "";
        string raw = v.InnerText;

        if (type == "s")
        {
            return int.TryParse(raw, out int idx) && idx >= 0 && idx < shared.Count
                ? shared[idx]
                : "";
        }
        if (type == "b")
            return raw == "1" ? "TRUE" : "FALSE";

        return raw;
    }

    private static int ColumnIndexFromRef(string cellRef)
    {
        int index = 0, seen = 0;
        foreach (char c in cellRef)
        {
            if (!char.IsLetter(c)) break;
            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            seen++;
        }
        return seen == 0 ? -1 : index - 1;
    }
}
