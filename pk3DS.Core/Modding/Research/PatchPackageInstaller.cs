using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>What one package would do, or did.</summary>
public sealed class PackageResult
{
    public string Package { get; init; } = "";
    public bool Success { get; set; } = true;
    public bool Skipped { get; set; }
    public List<string> Log { get; } = [];
    public List<string> Errors { get; } = [];
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal void Say(string s) => Log.Add(s);
    internal void Fail(string s) { Errors.Add(s); Success = false; }
    public string Describe() => string.Join(Environment.NewLine,
        Log.Select(l => "  " + l).Concat(Errors.Select(e => "  ERROR: " + e)));
}

/// <summary>
/// Applies <see cref="PatchPackage"/>s to a ROM, resolving each package's parameters first.
/// <para>
/// Everything a package touches goes through the installers that already exist — the master-table
/// path for mechanics, the site path for CROs, the symbol-anchored path for code.bin — so a package
/// gains all of their checking rather than a second, weaker copy of it. What this adds is the layer
/// above: choosing ids, ordering by dependency, and refusing a package whose chosen id names
/// something else in the target.
/// </para>
/// </summary>
public static class PatchPackageInstaller
{
    /// <summary>
    /// Validates a package against a ROM without writing: are the parameters resolvable, do the ids
    /// name what they claim, is anything already present.
    /// </summary>
    public static PackageResult Preview(
        PatchPackage package,
        IDictionary<string, string> supplied,
        BattleMechanicMap map,
        Dictionary<CustomMechanicKind, string[]> nameTables = null)
    {
        var result = new PackageResult { Package = package.Name };
        var problems = new List<string>();
        var values = PatchParameters.Resolve(package, supplied, problems);
        foreach (string p in problems) result.Fail(p);
        foreach (var kv in values) result.Values[kv.Key] = kv.Value;
        if (!result.Success) return result;

        result.Say($"{package.Name}: " + string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value}")));

        var bound = PatchParameters.Bind(package, values, problems);
        foreach (string p in problems.Distinct()) result.Fail(p);
        if (!result.Success) return result;

        // Every chosen id must either name the thing being installed or be an unclaimed slot.
        foreach (var (key, param) in package.Parameters ?? [])
        {
            if (param.Type is not ("item" or "move" or "ability")) continue;
            if (!values.TryGetValue(key, out string raw) || !uint.TryParse(raw, out uint id)) continue;

            var kind = param.Type switch
            {
                "item" => CustomMechanicKind.Item,
                "move" => CustomMechanicKind.Move,
                _ => CustomMechanicKind.Ability,
            };

            if (nameTables != null && nameTables.TryGetValue(kind, out var table) && id < table.Length)
            {
                string named = (table[id] ?? "").Trim();
                bool blank = named is "" or "???" or "？？？" or "(?)" or "-----" or "—";
                string want = param.ExpectName ?? package.Name;

                if (!blank && !named.Replace('’', '\'').Equals(want.Replace('’', '\''), StringComparison.OrdinalIgnoreCase))
                    result.Fail($"{key}={id} is '{named}' in this build, not '{want}'");
                else if (blank && !param.AllowUnnamed)
                    result.Fail($"{key}={id} is an unnamed slot and this package needs a named one");
                else
                    result.Say($"  {key}={id} -> '{(blank ? "unnamed slot" : named)}'");
            }

            if (map?.Find(kind, id) != null)
                result.Say($"  {kind} {id} already has an effect entry — that part will be skipped");
        }

        int edits = bound.Mechanics.Count + bound.SitePatches.Count + bound.Blocks.Count
                  + (bound.CodeBin?.Patches.Count ?? 0)
                  + bound.OtherCros.Sum(c => c.Value.SitePatches.Count)
                  + bound.ItemData.Count + bound.ItemNames.Count;
        result.Say($"  {edits} edit(s): {bound.Mechanics.Count} mechanic(s), {bound.SitePatches.Count} site patch(es), " +
                   $"{bound.Blocks.Count} block(s), {bound.OtherCros.Count} other CRO(s), " +
                   $"{bound.ItemData.Count} item data, {bound.ItemNames.Count} name(s)");
        return result;
    }

    /// <summary>
    /// Applies the Battle.cro half of a bound package. The caller keeps ownership of the other
    /// binaries, because a package that spans files must not leave some of them written and the
    /// rest not.
    /// </summary>
    public static PackageResult ApplyBattleCro(
        PatchPackage package,
        IDictionary<string, string> supplied,
        byte[] targetRom,
        ResearchDatabase db,
        Dictionary<CustomMechanicKind, string[]> nameTables,
        out byte[] output)
    {
        output = null;
        var result = new PackageResult { Package = package.Name };
        var problems = new List<string>();
        var values = PatchParameters.Resolve(package, supplied, problems);
        foreach (string p in problems) result.Fail(p);
        if (!result.Success) return result;

        var bound = PatchParameters.Bind(package, values, problems);
        foreach (string p in problems.Distinct()) result.Fail(p);
        if (!result.Success) return result;

        if (bound.Mechanics.Count == 0 && bound.SitePatches.Count == 0 && bound.Blocks.Count == 0)
        { result.Skipped = true; result.Say("nothing for Battle.cro"); output = targetRom; return result; }

        var manifest = new PortManifest
        {
            Description = package.Name,
            Mechanics = bound.Mechanics,
            Blocks = bound.Blocks,
            SitePatches = bound.SitePatches,
        };

        var port = PortInstaller.Apply(targetRom, manifest, db, nameTables);
        foreach (string l in port.Log) result.Say(l);
        foreach (string e in port.Errors) result.Fail(e);
        if (port.Success) output = port.Output;
        return result;
    }

    /// <summary>
    /// Writes a package's data tables into blocks that have already been placed.
    /// <para>
    /// Must run after the blocks land, because a table is positioned relative to its block, not to
    /// the file. <paramref name="placed"/> is the block-name to address map the CRO or code.bin
    /// installer produced.
    /// </para>
    /// </summary>
    public static PackageResult ApplyDataTables(
        PatchPackage bound, string target, byte[] binary, IReadOnlyDictionary<string, uint> placed)
    {
        var result = new PackageResult { Package = bound.Name };
        foreach (var t in bound.DataTables ?? [])
        {
            if (!string.Equals(t.Target, target, StringComparison.OrdinalIgnoreCase)) continue;

            if (!placed.TryGetValue(t.Block, out uint block))
            { result.Fail($"data table names block '{t.Block}', which was not placed in {target}"); continue; }

            var parts = (t.Values ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var vals = new List<uint>();
            foreach (string p in parts)
            {
                if (!uint.TryParse(p, out uint v) &&
                    !(p.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                      uint.TryParse(p[2..], System.Globalization.NumberStyles.HexNumber, null, out v)))
                { result.Fail($"'{p}' is not a number"); continue; }
                vals.Add(v);
            }
            if (!result.Success) continue;

            // A list longer than the block reserved would run into whatever follows it, so it is
            // refused rather than silently truncated - the routine would then read half a table.
            int needed = vals.Count + (t.Terminate ? 1 : 0);
            if (t.Capacity > 0 && needed > t.Capacity)
            { result.Fail($"{t.Block}+0x{t.OffsetValue:X}: {vals.Count} value(s) need {needed} slot(s), block reserves {t.Capacity}"); continue; }

            uint at = block + t.OffsetValue;
            if (at + needed * t.ElementSize > binary.Length)
            { result.Fail($"{t.Block}+0x{t.OffsetValue:X} runs past the end of {target}"); continue; }

            uint max = t.ElementSize switch { 1 => byte.MaxValue, 2 => ushort.MaxValue, _ => uint.MaxValue };
            foreach (uint v in vals)
                if (v > max) { result.Fail($"{v} does not fit in {t.ElementSize} byte(s)"); }
            if (!result.Success) continue;

            for (int i = 0; i < vals.Count; i++) WriteAt(binary, at + (uint)(i * t.ElementSize), vals[i], t.ElementSize);
            if (t.Terminate) WriteAt(binary, at + (uint)(vals.Count * t.ElementSize), 0, t.ElementSize);

            result.Say($"{target} {t.Block}+0x{t.OffsetValue:X}: wrote {vals.Count} value(s)" +
                       (t.Terminate ? " and a terminator" : "") + $" at 0x{at:X6}");
        }
        return result;
    }

    private static void WriteAt(byte[] b, uint at, uint value, int size)
    {
        switch (size)
        {
            case 1: b[at] = (byte)value; break;
            case 2: BitConverter.GetBytes((ushort)value).CopyTo(b, (int)at); break;
            default: BitConverter.GetBytes(value).CopyTo(b, (int)at); break;
        }
    }

    /// <summary>Writes a package's item data and names, using the game's own tables.</summary>
    public static PackageResult ApplyData(PatchPackage package, IDictionary<string, string> supplied, GameConfig config)
    {
        var result = new PackageResult { Package = package.Name };
        var problems = new List<string>();
        var values = PatchParameters.Resolve(package, supplied, problems);
        foreach (string p in problems) result.Fail(p);
        if (!result.Success) return result;
        var bound = PatchParameters.Bind(package, values, problems);
        if (!result.Success) return result;

        if (bound.ItemData.Count == 0 && bound.ItemNames.Count == 0 && bound.ItemDescriptions.Count == 0)
        { result.Skipped = true; return result; }

        var garc = config.GetGARCData("item");
        var files = garc.Files;

        foreach (var (idText, data) in bound.ItemData)
        {
            if (!uint.TryParse(idText, out uint id) || id >= files.Length)
            { result.Fail($"item id '{idText}' is out of range"); continue; }

            if (!string.IsNullOrWhiteSpace(data.CloneFrom))
            {
                if (!uint.TryParse(data.CloneFrom, out uint from) || from >= files.Length)
                { result.Fail($"cloneFrom '{data.CloneFrom}' is out of range"); continue; }
                files[id] = (byte[])files[from].Clone();
                result.Say($"item {id}: data cloned from {from}");
            }

            var it = new pk3DS.Core.Structures.Item(files[id]);
            if (uint.TryParse(data.HeldEffect, out uint he)) { it.HeldEffect = (byte)he; result.Say($"item {id}: heldEffect {he}"); }
            if (uint.TryParse(data.FlingPower, out uint fp)) it.FlingPower = (byte)fp;
            files[id] = it.Write();
            if (uint.TryParse(data.Price, out uint pr)) BitConverter.GetBytes((ushort)pr).CopyTo(files[id], 0);
        }
        garc.Save();

        int langs = 0, descLangs = 0;
        for (int lang = 0; lang <= 9; lang++)
        {
            try
            {
                var c = new GameConfig(config.Version);
                c.Initialize(config.RomFS, config.ExeFS, lang);

                var n = c.GetText(TextName.ItemNames);
                if (n != null)
                {
                    var doable = bound.ItemNames
                        .Where(kv => uint.TryParse(kv.Key, out uint i) && i < n.Length)
                        .ToList();
                    if (doable.Count > 0)
                    {
                        foreach (var kv in doable) n[uint.Parse(kv.Key)] = kv.Value;
                        c.SetText(TextName.ItemNames, n);
                        c.SaveText(TextName.ItemNames);
                        langs++;
                    }
                }

                var d = c.GetText(TextName.ItemFlavor);
                if (d != null)
                {
                    var doable = bound.ItemDescriptions
                        .Where(kv => uint.TryParse(kv.Key, out uint i) && i < d.Length)
                        .ToList();
                    if (doable.Count > 0)
                    {
                        foreach (var kv in doable) d[uint.Parse(kv.Key)] = kv.Value;
                        c.SetText(TextName.ItemFlavor, d);
                        c.SaveText(TextName.ItemFlavor);
                        descLangs++;
                    }
                }
            }
            catch { }
        }
        if (bound.ItemNames.Count > 0) result.Say($"names written to {langs} language(s)");
        if (bound.ItemDescriptions.Count > 0) result.Say($"descriptions written to {descLangs} language(s)");
        return result;
    }
}
