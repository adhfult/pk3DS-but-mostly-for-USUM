using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace pk3DS.Core.Modding.Research;

/// <summary>A value the installer asks for, or fills in from a default, before anything is applied.</summary>
public sealed class PatchParameter
{
    /// <summary>What it means, shown to whoever is choosing.</summary>
    public string Description { get; set; } = "";

    /// <summary>item | move | ability | number | text. Drives validation and the editor widget.</summary>
    public string Type { get; set; } = "number";

    /// <summary>Used when the caller supplies nothing.</summary>
    public string Default { get; set; }

    /// <summary>For item/move/ability: the name the id must already carry, as a safety check.</summary>
    public string ExpectName { get; set; }

    /// <summary>When true the id may point at an unnamed slot; otherwise ExpectName must match.</summary>
    public bool AllowUnnamed { get; set; } = true;
}

/// <summary>
/// A list of numbers written into a placed block, so data a routine reads can be chosen rather
/// than baked into the block's bytes.
/// <para>
/// The mint id table is the motivating case: twenty-one item ids that a routine walks, sitting
/// inside a code block. Without this they would be fixed at export time — which is precisely the
/// set of ids someone applying the package is most likely to need to move.
/// </para>
/// </summary>
public sealed class PatchDataTable
{
    /// <summary>Which binary the block lives in: "Battle.cro", "Bag.cro", "code.bin".</summary>
    public string Target { get; set; } = "Battle.cro";
    /// <summary>Name of the block this table sits inside.</summary>
    public string Block { get; set; } = "";
    /// <summary>Byte offset from the start of that block.</summary>
    public string Offset { get; set; } = "0x0";
    /// <summary>1, 2, or 4 bytes per entry.</summary>
    public int ElementSize { get; set; } = 2;
    /// <summary>Comma-separated values, usually a parameter reference.</summary>
    public string Values { get; set; } = "";
    /// <summary>Write a zero entry after the values, for routines that walk until zero.</summary>
    public bool Terminate { get; set; } = true;
    /// <summary>Space the block reserves, in entries. A longer list is refused rather than truncated.</summary>
    public int Capacity { get; set; }

    public uint OffsetValue => Convert.ToUInt32(Offset.Trim(), Offset.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16 : 10);
}

/// <summary>Item data to write, expressed relative to an existing item rather than as raw bytes.</summary>
public sealed class PatchItemData
{
    /// <summary>Item whose data block is copied as the starting point.</summary>
    public string CloneFrom { get; set; }
    public string Name { get; set; }
    /// <summary>Held-effect selector. Omit to leave the clone's value; 0 means "no code.bin behaviour".</summary>
    public string HeldEffect { get; set; }
    public string FlingPower { get; set; }
    public string Price { get; set; }
}

/// <summary>
/// One self-contained, reusable element: what it is, what it needs choosing, and every edit it
/// makes across the binaries and the data.
/// <para>
/// The point of the parameters is that nothing here is tied to the ids this was developed against.
/// A package says "an item, defaulting to 865, called Room Service"; the person applying it can put
/// it wherever their build has room. Ids appear as <c>${name}</c> and are substituted before any
/// installer sees them.
/// </para>
/// </summary>
public sealed class PatchPackage
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";

    /// <summary>Other packages that must be applied first, by name.</summary>
    public List<string> Requires { get; set; } = [];

    public Dictionary<string, PatchParameter> Parameters { get; set; } = [];

    /// <summary>Master-table entries to add to Battle.cro.</summary>
    public List<PortedMechanic> Mechanics { get; set; } = [];
    /// <summary>Free-standing routines placed in Battle.cro's reserve.</summary>
    public List<PortedBlock> Blocks { get; set; } = [];
    /// <summary>Address-anchored edits to Battle.cro.</summary>
    public List<PortedSitePatch> SitePatches { get; set; } = [];
    /// <summary>Edits to other CROs, keyed by file name.</summary>
    public Dictionary<string, CroSitePort> OtherCros { get; set; } = [];
    /// <summary>Symbol-anchored edits to code.bin.</summary>
    public CodeBinPort CodeBin { get; set; }

    /// <summary>Value lists written into placed blocks after they land.</summary>
    public List<PatchDataTable> DataTables { get; set; } = [];

    /// <summary>Item data to write, keyed by id (may be a parameter reference).</summary>
    public Dictionary<string, PatchItemData> ItemData { get; set; } = [];
    /// <summary>Names to write into every language, keyed by id.</summary>
    public Dictionary<string, string> ItemNames { get; set; } = [];

    /// <summary>
    /// Bag descriptions to write into every language, keyed by id.
    /// </summary>
    public Dictionary<string, string> ItemDescriptions { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);
    public static PatchPackage FromJson(string json) => JsonSerializer.Deserialize<PatchPackage>(json, Options);
    public static PatchPackage Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) => File.WriteAllText(path, ToJson());

    /// <summary>Loads every package in a folder, newest schema first, reporting anything unreadable.</summary>
    public static List<PatchPackage> LoadFolder(string folder, IList<string> problems = null)
    {
        var list = new List<PatchPackage>();
        if (!Directory.Exists(folder)) return list;
        foreach (string f in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
        {
            try
            {
                var p = Load(f);
                if (p != null && !string.IsNullOrWhiteSpace(p.Name)) list.Add(p);
            }
            catch (Exception ex) { problems?.Add($"{Path.GetFileName(f)}: {ex.Message}"); }
        }
        return list;
    }

    /// <summary>Orders packages so anything a package requires comes before it.</summary>
    public static List<PatchPackage> InDependencyOrder(IEnumerable<PatchPackage> packages, IList<string> problems = null)
    {
        var byName = new Dictionary<string, PatchPackage>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<PatchPackage>();
        foreach (var p in packages)
        {
            if (byName.TryAdd(p.Name ?? "", p)) continue;
            duplicates.Add(p);
            problems?.Add($"more than one package is named '{p.Name}'; only the first is used");
        }
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<PatchPackage>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(PatchPackage p)
        {
            if (done.Contains(p.Name)) return;
            if (!visiting.Add(p.Name))
            { problems?.Add($"'{p.Name}' takes part in a requirement cycle"); return; }

            foreach (string need in p.Requires ?? [])
            {
                if (byName.TryGetValue(need, out var dep)) Visit(dep);
                else problems?.Add($"'{p.Name}' requires '{need}', which is not present");
            }
            visiting.Remove(p.Name);
            done.Add(p.Name);
            order.Add(p);
        }

        // The duplicates are skipped, not visited: they carry the same name as one already in the
        // order, so visiting them would add a second copy of the same install to the run.
        foreach (var p in packages)
            if (!duplicates.Contains(p)) Visit(p);
        return order;
    }
}

/// <summary>Substitutes <c>${name}</c> references and turns a package into concrete edits.</summary>
public static class PatchParameters
{
    /// <summary>
    /// <c>${name}</c>, and <c>${name[i]}</c> for one element of a list parameter.
    /// </summary>
    private static readonly Regex Ref = new(@"\$\{([A-Za-z0-9_]+)(?:\[(\d+)\])?\}", RegexOptions.Compiled);

    /// <summary>Fills in defaults for anything the caller did not supply, and reports what is missing.</summary>
    public static Dictionary<string, string> Resolve(
        PatchPackage package, IDictionary<string, string> supplied, IList<string> problems = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, param) in package.Parameters ?? [])
        {
            if (supplied != null && supplied.TryGetValue(key, out string given) && !string.IsNullOrWhiteSpace(given))
            { values[key] = given.Trim(); continue; }

            if (!string.IsNullOrWhiteSpace(param.Default)) { values[key] = param.Default.Trim(); continue; }
            problems?.Add($"{package.Name}: '{key}' has no value and no default ({param.Description})");
        }
        return values;
    }

    /// <summary>Expands every reference in a string. Unknown names are left alone and reported.</summary>
    public static string Expand(string text, IDictionary<string, string> values, IList<string> problems = null)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Ref.Replace(text, m =>
        {
            string key = m.Groups[1].Value;
            if (!values.TryGetValue(key, out string v))
            {
                problems?.Add($"no value for '{key}'");
                return m.Value;
            }

            if (!m.Groups[2].Success) return v;

            // Indexed reference into a comma-separated list parameter.
            int idx = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var parts = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (idx >= 0 && idx < parts.Length) return parts[idx];

            problems?.Add($"'{key}' has {parts.Length} item(s), so [{idx}] does not exist");
            return m.Value;
        });
    }

    /// <summary>Reads a possibly-parameterised number, in decimal or 0x form.</summary>
    public static bool TryNumber(string text, IDictionary<string, string> values, out uint result, IList<string> problems = null)
    {
        result = 0;
        string s = Expand(text, values, problems)?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        return uint.TryParse(s, out result);
    }

    /// <summary>
    /// Produces a copy of the package with every reference expanded, leaving the original untouched
    /// so one package can be applied more than once with different values.
    /// </summary>
    public static PatchPackage Bind(PatchPackage package, IDictionary<string, string> values, IList<string> problems = null)
    {
        // Round-tripping through JSON is the simplest deep copy that also catches a package whose
        // own serialisation is broken, before any of it reaches a binary.
        var copy = PatchPackage.FromJson(package.ToJson());

        foreach (var m in copy.Mechanics)
        {
            m.Name = Expand(m.Name, values, problems);
            if (TryNumber(m.IdText ?? m.Id.ToString(), values, out uint id, problems)) m.Id = id;
            foreach (var s in m.Slots)
            {
                s.Timing = Expand(s.Timing, values, problems);
                s.SourceBase = Expand(s.SourceBase, values, problems);
                if (s.Reuse != null)
                {
                    s.Reuse.Timing = Expand(s.Reuse.Timing, values, problems);
                    if (TryNumber(s.Reuse.IdText ?? s.Reuse.Id.ToString(), values, out uint rid, problems)) s.Reuse.Id = rid;
                }
            }
        }
        foreach (var b in copy.Blocks) { b.Name = Expand(b.Name, values, problems); b.SourceBase = Expand(b.SourceBase, values, problems); }
        foreach (var p in copy.SitePatches)
        {
            p.Offset = Expand(p.Offset, values, problems);
            p.Original = Expand(p.Original, values, problems);
            p.Patched = Expand(p.Patched, values, problems);
            p.HookTarget = Expand(p.HookTarget, values, problems);
        }

        foreach (var t in copy.DataTables ?? [])
        {
            t.Block = Expand(t.Block, values, problems);
            t.Offset = Expand(t.Offset, values, problems);
            t.Values = Expand(t.Values, values, problems);
        }

        copy.ItemNames = (copy.ItemNames ?? []).ToDictionary(
            kv => Expand(kv.Key, values, problems), kv => Expand(kv.Value, values, problems));
        copy.ItemDescriptions = (copy.ItemDescriptions ?? []).ToDictionary(
            kv => Expand(kv.Key, values, problems), kv => Expand(kv.Value, values, problems));
        copy.ItemData = (copy.ItemData ?? []).ToDictionary(
            kv => Expand(kv.Key, values, problems),
            kv => new PatchItemData
            {
                CloneFrom = Expand(kv.Value.CloneFrom, values, problems),
                Name = Expand(kv.Value.Name, values, problems),
                HeldEffect = Expand(kv.Value.HeldEffect, values, problems),
                FlingPower = Expand(kv.Value.FlingPower, values, problems),
                Price = Expand(kv.Value.Price, values, problems),
            });

        return copy;
    }
}
