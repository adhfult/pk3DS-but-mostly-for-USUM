using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace pk3DS.Core.Modding.Research;

/// <summary>The item/move/symbol half of a port, as written alongside the mechanic manifest.</summary>
public sealed class PortDataFile
{
    public List<PortedItem> Items { get; set; } = [];
    public List<PortedMove> Moves { get; set; } = [];
    public List<string> CodeBinSymbols { get; set; } = [];
}

/// <summary>
/// Every part of a port, loaded from a folder.
/// <para>
/// The three files describe different binaries — Battle.cro, the item/move GARCs, and code.bin —
/// so they are classified by what they contain rather than by filename, and applied in an order
/// that respects their dependencies.
/// </para>
/// </summary>
public sealed class PortBundle
{
    public string Folder { get; private set; } = "";
    public PortManifest Manifest { get; private set; }
    public PortDataFile Data { get; private set; }
    public CodeBinPort CodeBin { get; private set; }

    /// <summary>
    /// Definitions authored in the Research Center's Custom function tab.
    /// <para>
    /// These are the fourth file shape a port folder can hold. They were classified as "not
    /// recognised" and dropped, because the bundle keys off the property names the other three use
    /// and a definition shares none of them — so a perfectly valid function saved from the editor
    /// went silently missing from its own port.
    /// </para>
    /// </summary>
    public List<CustomFunctionDefinition> CustomFunctions { get; } = [];

    public List<string> Notes { get; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads every .json in a folder, working out what each one is from its contents.</summary>
    public static PortBundle Load(string folder)
    {
        var bundle = new PortBundle { Folder = folder };
        if (!Directory.Exists(folder)) { bundle.Notes.Add($"folder not found: {folder}"); return bundle; }

        foreach (string file in Directory.GetFiles(folder, "*.json").OrderBy(f => f))
        {
            string name = Path.GetFileName(file);
            string text;
            try { text = File.ReadAllText(file); }
            catch (Exception ex) { bundle.Notes.Add($"{name}: unreadable ({ex.Message})"); continue; }

            // Classify by the keys present, so renaming a file does not break the port.
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                bool Has(string key) => root.ValueKind == JsonValueKind.Object &&
                    root.EnumerateObject().Any(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

                if (Has("battleCroMechanics"))
                {
                    bundle.Manifest = PortManifest.FromJson(text);
                    bundle.Notes.Add($"{name}: Battle.cro manifest — {bundle.Manifest.Mechanics.Count} mechanic(s), " +
                                     $"{bundle.Manifest.Blocks.Count} block(s), {bundle.Manifest.SitePatches.Count} site patch(es)");
                }
                else if (Has("items") || Has("moves"))
                {
                    bundle.Data = JsonSerializer.Deserialize<PortDataFile>(text, Options);
                    bundle.Notes.Add($"{name}: game data — {bundle.Data.Items.Count} item(s), {bundle.Data.Moves.Count} move(s)");
                }
                else if (Has("patches") || Has("blocks"))
                {
                    bundle.CodeBin = JsonSerializer.Deserialize<CodeBinPort>(text, Options);
                    bundle.Notes.Add($"{name}: code.bin — {bundle.CodeBin.Blocks.Count} block(s), {bundle.CodeBin.Patches.Count} patch(es)");
                }
                else if (Has("mechanic") || Has("assembly") || Has("hexCode") || Has("reuseFunctionNamed"))
                {
                    // A single definition, or an array of them - LoadFolder's own format accepts
                    // both, so a port folder should too.
                    var defs = root.ValueKind == JsonValueKind.Array
                        ? JsonSerializer.Deserialize<List<CustomFunctionDefinition>>(text, Options) ?? []
                        : [CustomFunctionDefinition.FromJson(text)];

                    foreach (var d in defs.Where(d => d != null))
                        bundle.CustomFunctions.Add(d);

                    bundle.Notes.Add($"{name}: custom function(s) — {defs.Count}: "
                                     + string.Join(", ", defs.Where(d => d != null).Select(d => d.Name)));
                }
                else bundle.Notes.Add($"{name}: not recognised — ignored");
            }
            catch (Exception ex) { bundle.Notes.Add($"{name}: not valid JSON ({ex.Message})"); }
        }

        if (bundle.Manifest == null && bundle.Data == null && bundle.CodeBin == null
            && bundle.CustomFunctions.Count == 0)
            bundle.Notes.Add("nothing usable found in this folder");
        return bundle;
    }

    /// <summary>
    /// Applies everything present, in dependency order: Battle.cro, then code.bin, then game data.
    /// <para>
    /// Nothing is written unless <paramref name="commit"/> is set, and a failure in any stage stops
    /// the later ones — a half-applied port is worse than none, because the parts that did land are
    /// no longer described by anything.
    /// </para>
    /// </summary>
    public PortResult ApplyAll(GameConfig cfg, ResearchDatabase db, bool commit,
                               string battleCroPath = null, string codeBinPath = null,
                               Dictionary<CustomMechanicKind, string[]> nameTables = null)
    {
        var result = new PortResult();
        foreach (string n in Notes) result.Say(n);
        result.Say("");

        // ---- Battle.cro ----
        if (Manifest != null)
        {
            battleCroPath ??= Path.Combine(cfg.RomFS, "Battle.cro");
            if (!File.Exists(battleCroPath)) { result.Fail($"Battle.cro not found at {battleCroPath}"); return result; }

            int dropped = Manifest.RemoveStructural();
            if (dropped > 0) result.Say($"Battle.cro: ignoring {dropped} structural entr(ies)");

            var r = PortInstaller.Apply(File.ReadAllBytes(battleCroPath), Manifest, db, nameTables);
            foreach (string l in r.Log) result.Say("  " + l);
            foreach (string e in r.Errors) result.Fail("Battle.cro: " + e);
            if (!r.Success) { result.Say("stopping: Battle.cro stage failed"); return result; }

            if (commit)
            {
                File.Copy(battleCroPath, battleCroPath + ".preport", true);
                if (!Modding.BinaryWriteGuard.TryWrite(battleCroPath, r.Output,
                        "Apply a ported Battle.cro manifest",
                        $"{Manifest.Mechanics.Count} mechanic(s), {Manifest.Blocks.Count} block(s), "
                        + $"{Manifest.SitePatches.Count} site patch(es)."))
                {
                    result.Fail("the write was declined; Battle.cro is unchanged");
                    return result;
                }
                bool ok = PortInstaller.VerifyOnDisk(battleCroPath, Manifest, db, s => result.Say("  " + s));
                if (!ok) { result.Fail("Battle.cro failed verification after writing — restore the .preport copy"); return result; }
                result.Say("  Battle.cro written and verified from disk");
            }
            result.Say("");
        }

        if (CustomFunctions.Count > 0)
        {
            battleCroPath ??= Path.Combine(cfg.RomFS, "Battle.cro");
            if (!File.Exists(battleCroPath)) { result.Fail($"Battle.cro not found at {battleCroPath}"); return result; }

            byte[] cro = File.ReadAllBytes(battleCroPath);
            var map = BattleMechanicMap.Build(cro, db, battleCroPath);
            if (map?.Cro == null) { result.Fail("could not map Battle.cro for the custom functions"); return result; }

            bool anyCommitted = false;
            foreach (var def in CustomFunctions)
            {
                string[] names = nameTables != null && nameTables.TryGetValue(def.Mechanic, out var n) ? n : null;

                var plan = CustomFunctionInstaller.Plan(def, map.Cro, db, names, map);
                foreach (var step in plan.Steps) result.Say("  " + step);

                if (plan.HasErrors)
                {
                    result.Fail($"custom function '{def.Name}': plan has errors");
                    return result;
                }
                if (!commit) continue;

                if (!CustomFunctionInstaller.Commit(plan, map.Cro, s => result.Say("  " + s)))
                {
                    result.Fail($"custom function '{def.Name}': install did not complete");
                    return result;
                }
                anyCommitted = true;
            }

            if (commit && anyCommitted)
            {
                File.Copy(battleCroPath, battleCroPath + ".preport", true);
                if (!Modding.BinaryWriteGuard.TryWrite(battleCroPath, map.Cro.RawData,
                        $"Install {CustomFunctions.Count} ported custom function(s)",
                        string.Join(", ", CustomFunctions.Select(f => f.Name))))
                {
                    result.Fail("the write was declined; Battle.cro is unchanged");
                    return result;
                }
                result.Say("  Battle.cro written with the ported custom functions");
            }
            result.Say("");
        }

        // ---- code.bin ----
        if (CodeBin != null)
        {
            codeBinPath ??= FindCodeBin(cfg.ExeFS);
            if (codeBinPath == null) { result.Fail("code.bin not found in the ExeFS folder"); return result; }

            var r = PortCodeBinInstaller.Apply(File.ReadAllBytes(codeBinPath), CodeBin, cfg.RomFS, s => result.Say("  " + s));
            foreach (string l in r.Log) result.Say("  " + l);
            foreach (string e in r.Errors) result.Fail("code.bin: " + e);
            if (!r.Success) { result.Say("stopping: code.bin stage failed"); return result; }

            if (commit)
            {
                // Backups must not live in ExeFS: pk3DS rejects that folder above six files, which
                // silently disables the icon, the ExeFS tab and every rebuild option.
                string backup = Path.Combine(Path.GetDirectoryName(cfg.ExeFS) ?? ".", ".code.bin.preport");
                File.Copy(codeBinPath, backup, true);
                if (!Modding.BinaryWriteGuard.TryWrite(codeBinPath, r.Output,
                        "Apply a ported code.bin patch set",
                        $"{CodeBin.Blocks.Count} block(s), {CodeBin.Patches.Count} patch(es)."))
                {
                    result.Fail("the write was declined; code.bin is unchanged");
                    return result;
                }
                result.Say($"  code.bin written; previous state saved beside ExeFS as {Path.GetFileName(backup)}");
            }
            result.Say("");
        }

        // ---- game data ----
        if (Data != null)
        {
            if (Data.Items.Count > 0) PortDataInstaller.ApplyItems(cfg, Data.Items, commit, s => result.Say("  " + s));
            if (Data.Moves.Count > 0) PortDataInstaller.ApplyMoves(cfg, Data.Moves, commit, s => result.Say("  " + s));

            if (Data.CodeBinSymbols.Count > 0)
            {
                var syms = PortDataInstaller.ResolveStaticSymbols(cfg.RomFS, Data.CodeBinSymbols);
                result.Say($"  symbols: {syms.Count}/{Data.CodeBinSymbols.Count} resolve on this build");
                foreach (string s in Data.CodeBinSymbols.Where(s => !syms.ContainsKey(s)))
                    result.Say($"    unresolved: {s}");
            }
        }

        result.Say("");
        result.Say(commit ? "port complete." : "preview only — nothing was written.");
        return result;
    }

    private static string FindCodeBin(string exeFs)
    {
        if (string.IsNullOrEmpty(exeFs)) return null;
        foreach (string n in new[] { ".code.bin", "code.bin" })
        {
            string p = Path.Combine(exeFs, n);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
