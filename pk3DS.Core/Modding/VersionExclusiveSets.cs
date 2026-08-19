#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace pk3DS.Core.Modding;

/// <summary>One named list of species, as supplied by the user for a particular version.</summary>
public sealed class ExclusiveSet
{
    /// <summary>Heading the user gave this set, e.g. "Ultra Sun" or "My Hack - Red Version".</summary>
    public string Header { get; set; } = "";

    /// <summary>Species names exactly as they were read from the file.</summary>
    public List<string> Species { get; set; } = [];

    /// <summary>Where it came from, kept so the guide can say what produced the list.</summary>
    public string Source { get; set; } = "";

    public DateTime Imported { get; set; } = DateTime.Now;
}

/// <summary>
/// User-supplied version-exclusive lists, imported from a Poképaste, text, JSON or CSV file.
/// </summary>
public static class VersionExclusiveSets
{
    private static readonly List<ExclusiveSet> Sets = [];
    private static string _path = "";
    private static bool _loaded;

    public static void SetStorePath(string path)
    {
        if (_path == path) return;
        _path = path;
        _loaded = false;
        Sets.Clear();
    }

    public static IReadOnlyList<ExclusiveSet> All
    {
        get { EnsureLoaded(); return Sets; }
    }

    /// <summary>Adds a set, replacing any existing one with the same heading.</summary>
    public static void Add(ExclusiveSet set)
    {
        EnsureLoaded();
        Sets.RemoveAll(s => string.Equals(s.Header, set.Header, StringComparison.OrdinalIgnoreCase));
        Sets.Add(set);
        Save();
    }

    public static bool Remove(string header)
    {
        EnsureLoaded();
        int n = Sets.RemoveAll(s => string.Equals(s.Header, header, StringComparison.OrdinalIgnoreCase));
        if (n > 0) Save();
        return n > 0;
    }

    /// <summary>
    /// Reads species names out of a file, choosing the reader by extension and, for text, by shape.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <param name="unmatched">Lines that produced no name, for reporting back.</param>
    public static List<string> Parse(string path, out List<string> unmatched)
    {
        unmatched = [];
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string text = File.ReadAllText(path);

        return ext switch
        {
            ".json" => ParseJson(text, unmatched),
            ".csv" => ParseCsv(text),
            _ => ParseText(text),
        };
    }

    /// <summary>JSON as either a bare array of names or an object whose values are arrays.</summary>
    private static List<string> ParseJson(string text, List<string> unmatched)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
                return [.. root.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? "")];

            if (root.ValueKind == JsonValueKind.Object)
            {
                var names = new List<string>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                        names.AddRange(prop.Value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? ""));
                    else if (prop.Value.ValueKind == JsonValueKind.String)
                        names.Add(prop.Value.GetString() ?? "");
                }
                return names;
            }
        }
        catch (JsonException ex)
        {
            unmatched.Add($"JSON could not be parsed: {ex.Message}");
        }
        return [];
    }

    /// <summary>CSV taking the first column of each row, skipping an obvious header row.</summary>
    private static List<string> ParseCsv(string text)
    {
        var names = new List<string>();
        bool first = true;
        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            string s = line.Trim();
            if (s.Length == 0) continue;

            string cell = s.Split(',')[0].Trim().Trim('"');
            if (first)
            {
                first = false;
                // Skip a heading row rather than importing the word "Species" as a Pokémon.
                if (cell.Equals("species", StringComparison.OrdinalIgnoreCase)
                    || cell.Equals("name", StringComparison.OrdinalIgnoreCase)
                    || cell.Equals("pokemon", StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            if (cell.Length > 0) names.Add(cell);
        }
        return names;
    }

    /// <summary>
    /// Plain text or a Poképaste.
    /// <para>
    /// A paste puts the species on the first line of each block, optionally as "Name @ Item" and
    /// optionally with a nickname as "Nickname (Species)". Lines starting with a set's keywords are
    /// skipped so only the species survives. A plain one-name-per-line list also works.
    /// </para>
    /// </summary>
    private static List<string> ParseText(string text)
    {
        var names = new List<string>();
        bool expectSpecies = true;

        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) { expectSpecies = true; continue; }

            // Set detail lines - never a species.
            if (line.StartsWith("-") || line.StartsWith("=")
                || line.StartsWith("Ability:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("EVs:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("IVs:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Shiny:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Happiness:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Tera Type:", StringComparison.OrdinalIgnoreCase)
                || line.EndsWith(" Nature", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!expectSpecies) continue;

            string name = line;
            int at = name.IndexOf('@');
            if (at > 0) name = name[..at].Trim();          // "Pikachu @ Light Ball"

            int open = name.IndexOf('(');
            int close = name.IndexOf(')');
            if (open >= 0 && close > open)
            {
                string inner = name[(open + 1)..close].Trim();
                // "Nickname (Species)" - but not the gender marker in "Species (M)".
                if (inner.Length > 1) name = inner;
                else name = name[..open].Trim();
            }

            name = name.Trim();
            if (name.Length > 0) names.Add(name);
            expectSpecies = false;
        }
        return names;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Sets.Clear();
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<ExclusiveSet>>(File.ReadAllText(_path));
            if (list != null) Sets.AddRange(list);
        }
        catch { /* a damaged store starts empty rather than blocking the guide */ }
    }

    private static void Save()
    {
        if (string.IsNullOrEmpty(_path)) return;
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(Sets, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* still live for this session */ }
    }
}
