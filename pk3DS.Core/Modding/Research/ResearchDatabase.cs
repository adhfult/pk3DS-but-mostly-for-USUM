using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// The whole ARM research corpus, parsed once and queryable by target binary.
/// <para>
/// Nothing here is hardcoded to a particular ROM: addresses, function names, free space and
/// relocations all come from the workbooks, so re-pointing a table or moving a function is a
/// spreadsheet edit rather than a code change.
/// </para>
/// </summary>
public sealed class ResearchDatabase
{
    public List<ResearchSheet> Sheets { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public DateTime LoadedUtc { get; private set; }
    public string SourceFolder { get; private set; } = "";
    public string Version { get; private set; } = "UM";

    /// <summary>Folder name -> category label. Anything else is scanned as "Root".</summary>
    private static readonly (string Sub, string Category)[] CategoryFolders =
    [
        ("Move Edits", "Move"),
        ("Ability Edits", "Ability"),
        ("Item Edits", "Item"),
        ("Generic Functions", "Generic"),
        ("AI Edits", "AI"),
        ("Other Mechanics", "Other"),
        ("Field Effects", "Field"),
        ("Research", "Research"),
    ];

    public static ResearchDatabase Load(string armResearchFolder, string version = "UM", Action<string> log = null)
    {
        log ??= _ => { };
        var db = new ResearchDatabase { SourceFolder = armResearchFolder ?? "", Version = version, LoadedUtc = DateTime.UtcNow };

        if (string.IsNullOrWhiteSpace(armResearchFolder) || !Directory.Exists(armResearchFolder))
        {
            db.Diagnostics.Add($"research folder not found: {armResearchFolder}");
            return db;
        }

        foreach (var (sub, category) in CategoryFolders)
        {
            string full = Path.Combine(armResearchFolder, sub);
            if (!Directory.Exists(full)) continue;
            db.LoadFolder(full, category, SearchOption.AllDirectories, log);
        }

        db.LoadFolder(armResearchFolder, "Root", SearchOption.TopDirectoryOnly, log);

        log($"  parsed {db.Sheets.Count} sheets; " +
            $"{db.AllPatches.Count()} patches, {db.AllRelocations.Count()} relocations, " +
            $"{db.AllFunctions.Count()} functions, {db.AllBodies.Count()} routines, " +
            $"{db.AllFreeSpace.Count()} free-space rows, " +
            $"{db.AllTables.Count()} tables, {db.Timings.Count} timings");
        return db;
    }

    /// <summary>
    /// Parses every workbook in a folder, in parallel, appending the results in file order.
    /// </summary>
    private void LoadFolder(string folder, string category, SearchOption option, Action<string> log)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.xlsx", option)
                .Where(f => !Path.GetFileName(f).StartsWith("~$"))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) { Diagnostics.Add($"{folder}: {ex.Message}"); return; }

        if (files.Length == 0) return;
        log($"  {category}: {files.Length} file(s)");

        var sheets = new List<ResearchSheet>[files.Length];
        var diags = new List<string>[files.Length];

        System.Threading.Tasks.Parallel.For(0, files.Length, i =>
        {
            string file = files[i];
            var mySheets = sheets[i] = [];
            var myDiags = diags[i] = [];

            // A whole workbook failing must not take the corpus with it.
            List<string> sheetNames;
            try { sheetNames = ResearchXlsxReader.GetSheetNames(file, myDiags); }
            catch (Exception ex)
            {
                myDiags.Add($"{Path.GetFileName(file)}: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            foreach (string sheetName in sheetNames)
            {
                ResearchSheet sheet;
                try
                {
                    sheet = ResearchSheetReader.Read(file, sheetName, category, Version);
                }
                catch (Exception ex)
                {
                    // One malformed sheet must never abort the corpus, and must never be silent.
                    myDiags.Add($"{Path.GetFileName(file)}[{sheetName}]: {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                foreach (var d in sheet.Diagnostics)
                    myDiags.Add($"{Path.GetFileName(file)}[{sheetName}]: {d}");

                mySheets.Add(sheet);
            }
        });

        for (int i = 0; i < files.Length; i++)
        {
            if (sheets[i] != null) Sheets.AddRange(sheets[i]);
            if (diags[i] != null) Diagnostics.AddRange(diags[i]);
        }
    }

    #region Queries

    public IEnumerable<ResearchPatch> AllPatches => Sheets.SelectMany(s => s.Patches);
    public IEnumerable<ResearchRelocation> AllRelocations => Sheets.SelectMany(s => s.Relocations);
    public IEnumerable<ResearchFunction> AllFunctions => Sheets.SelectMany(s => s.Functions);
    public IEnumerable<ResearchFunctionBody> AllBodies => Sheets.SelectMany(s => s.Bodies);
    public IEnumerable<ResearchFreeSpace> AllFreeSpace => Sheets.SelectMany(s => s.FreeSpace);
    public IEnumerable<ResearchTableLocation> AllTables => Sheets.SelectMany(s => s.Tables);

    /// <summary>
    /// Documented master index tables, best copy per name. These carry the id fingerprints used
    /// by <see cref="MechanicTableLocator"/> to find the tables inside an actual ROM.
    /// </summary>
    public List<ResearchMechanicIndex> MechanicIndexes =>
        Sheets.SelectMany(s => s.MechanicIndexes)
              .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
              .Select(g => g.OrderByDescending(m => m.Entries.Count).First())
              .ToList();

    /// <summary>
    /// Ready-to-place routines for a binary, largest first. These are the candidates a
    /// "add a custom function" flow offers the user.
    /// </summary>
    public List<ResearchFunctionBody> BodiesFor(ResearchTarget target, int minimumSize = 8) =>
        AllBodies.Where(b => b.Target == target && b.Size >= minimumSize)
                 .OrderByDescending(b => b.Size)
                 .ToList();

    public IEnumerable<ResearchSheet> SheetsOfKind(ResearchSheetKind kind) => Sheets.Where(s => s.Kind == kind);

    /// <summary>Applyable effect sheets (patch lists that actually carry bytes), by category.</summary>
    public IEnumerable<ResearchSheet> EffectSheets =>
        Sheets.Where(s => s.Kind == ResearchSheetKind.PatchList && s.Patches.Count > 0);

    /// <summary>
    /// Function symbol table for one binary, keyed by in-file offset. Later duplicates lose to
    /// the first entry that carries a name, so the richest documentation wins.
    /// </summary>
    public Dictionary<uint, ResearchFunction> FunctionSymbols(ResearchTarget target)
    {
        var map = new Dictionary<uint, ResearchFunction>();
        foreach (var f in AllFunctions.Where(f => f.Target == target))
        {
            if (!map.TryGetValue(f.Offset, out var existing))
            { map[f.Offset] = f; continue; }
            if (string.IsNullOrWhiteSpace(existing.Name) && !string.IsNullOrWhiteSpace(f.Name))
                map[f.Offset] = f;
        }
        return map;
    }

    /// <summary>Timing byte -> meaning, merged across every sheet that documents them.</summary>
    public IReadOnlyDictionary<byte, ResearchTiming> Timings
    {
        get
        {
            var map = new Dictionary<byte, ResearchTiming>();
            foreach (var t in Sheets.SelectMany(s => s.Timings))
                map.TryAdd(t.Value, t);
            return map;
        }
    }

    /// <summary>
    /// Regions with room to spare in a given binary, largest first — the allocator's preferred
    /// source of truth over scanning the file for 0xCC runs.
    /// </summary>
    public List<ResearchFreeSpace> FreeSpaceFor(ResearchTarget target, int minimumRoom = 4) =>
        AllFreeSpace
            .Where(f => f.Target == target && f.Room >= minimumRoom)
            .OrderByDescending(f => f.Room)
            .ToList();

    /// <summary>
    /// The master index tables, de-duplicated by name. Several workbooks carry their own copy of
    /// this registry; the copy that documents the most (expansion counts and relocated addresses)
    /// wins, so a stale duplicate can't mask a maintained one.
    /// </summary>
    public List<ResearchTableLocation> TableRegistry =>
        AllTables
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(t => t.EditedTableData != 0 ? 1 : 0)
                .ThenByDescending(t => t.EditedEntryCount)
                .ThenByDescending(t => t.OriginalEntryCount)
                .First())
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public ResearchTableLocation FindTable(string nameFragment) =>
        TableRegistry.FirstOrDefault(t => t.Name != null &&
            t.Name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Persistence

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public void SaveJson(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(new Snapshot(this), JsonOpts));

    /// <summary>
    /// Options for the cache round-trip.
    /// </summary>
    private static readonly JsonSerializerOptions CacheOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    /// <summary>Default place the parsed corpus is cached.</summary>
    public static string DefaultCachePath => CacheFile("research_cache.json");

    /// <summary>
    /// Resolves a cache file name to somewhere it can actually be written.
    /// </summary>
    public static string CacheFile(string name)
    {
        string beside = AppDomain.CurrentDomain.BaseDirectory;
        if (_cacheDir == null)
        {
            _cacheDir = beside;
            try
            {
                string probe = Path.Combine(beside, ".pk3ds_write_probe");
                File.WriteAllText(probe, "");
                File.Delete(probe);
            }
            catch
            {
                try
                {
                    string local = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pk3DS");
                    Directory.CreateDirectory(local);
                    _cacheDir = local;
                }
                catch { /* stay with BaseDirectory; the write will fail and be reported as before */ }
            }
        }
        return Path.Combine(_cacheDir, name);
    }

    private static string _cacheDir;

    /// <summary>
    /// Cheap signature of the source folder: how many workbooks, their total size, and the newest
    /// timestamp. Enough to notice an edit, an addition or a deletion without reading any of them.
    /// </summary>
    public static string Fingerprint(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return "";
        try
        {
            long count = 0, bytes = 0;
            long newest = 0;
            foreach (string f in Directory.EnumerateFiles(folder, "*.xlsx", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(f);
                count++;
                bytes += fi.Length;
                long t = fi.LastWriteTimeUtc.Ticks;
                if (t > newest) newest = t;
            }
            return $"{count}:{bytes}:{newest}";
        }
        catch { return ""; }
    }

    /// <summary>
    /// Loads the corpus, reusing a cached parse when the source folder has not changed.
    /// </summary>
    public static ResearchDatabase LoadCached(string armResearchFolder, string version = "UM",
                                              string cachePath = null, Action<string> log = null)
    {
        log ??= _ => { };
        cachePath ??= DefaultCachePath;
        string want = Fingerprint(armResearchFolder);

        lock (MemoLock)
        {
            if (_memo != null && _memoKey == MemoKeyFor(armResearchFolder, version, want))
            {
                log($"  research corpus already loaded ({_memo.Sheets.Count} sheets)");
                return _memo;
            }
        }

        if (want.Length > 0 && File.Exists(cachePath))
        {
            try
            {
                var snap = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(cachePath), CacheOpts);
                if (snap != null && snap.Fingerprint == want && snap.Version == version)
                {
                    var cached = new ResearchDatabase
                    {
                        SourceFolder = armResearchFolder,
                        Version = version,
                        LoadedUtc = snap.LoadedUtc,
                    };
                    if (snap.Sheets != null) cached.Sheets.AddRange(snap.Sheets);
                    if (snap.Diagnostics != null) cached.Diagnostics.AddRange(snap.Diagnostics);
                    log($"  research corpus loaded from cache ({cached.Sheets.Count} sheets)");
                    return Memoise(armResearchFolder, version, want, cached);
                }
                log("  research cache is stale; reparsing");
            }
            catch (Exception ex)
            {
                // A corrupt or older-format cache must never block startup - just rebuild it.
                log($"  research cache unreadable ({ex.Message}); reparsing");
            }
        }

        var db = Load(armResearchFolder, version, log);

        if (want.Length > 0 && db.Sheets.Count > 0)
        {
            try
            {
                File.WriteAllText(cachePath, JsonSerializer.Serialize(new Snapshot(db) { Fingerprint = want }, CacheOpts));
                log($"  research cache written to {cachePath}");
            }
            catch (Exception ex) { log($"  could not write research cache: {ex.Message}"); }
        }
        return Memoise(armResearchFolder, version, want, db);
    }

    private static readonly object MemoLock = new();
    private static ResearchDatabase _memo;
    private static string _memoKey;

    /// <summary>Identity of a parsed corpus: where it came from, which game, and its signature.</summary>
    private static string MemoKeyFor(string folder, string version, string fingerprint) =>
        string.Join('|', folder, version, fingerprint);

    private static ResearchDatabase Memoise(string folder, string version, string fingerprint, ResearchDatabase db)
    {
        lock (MemoLock)
        {
            _memo = db;
            _memoKey = MemoKeyFor(folder, version, fingerprint);
        }
        return db;
    }

    /// <summary>
    /// The parsed corpus that ships inside the program, for when the research folder is not present.
    /// </summary>
    public static ResearchDatabase LoadEmbedded(string version = "UM", Action<string> log = null)
    {
        log ??= _ => { };
        string key = $"research_corpus_{version}.json.gz";

        lock (MemoLock)
        {
            if (_memo != null && _memoKey == "embedded:" + key) return _memo;
        }

        try
        {
            var asm = typeof(ResearchDatabase).Assembly;
            string name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(key, StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                log($"  no corpus is embedded for {version}");
                return new ResearchDatabase { Version = version };
            }

            using var raw = asm.GetManifestResourceStream(name);
            using var gz = new System.IO.Compression.GZipStream(raw!, System.IO.Compression.CompressionMode.Decompress);
            using var mem = new MemoryStream();
            gz.CopyTo(mem);
            mem.Position = 0;

            var snap = JsonSerializer.Deserialize<Snapshot>(mem, CacheOpts);
            var db = new ResearchDatabase { SourceFolder = "(built in)", Version = version, LoadedUtc = snap?.LoadedUtc ?? DateTime.UtcNow };
            if (snap?.Sheets != null) db.Sheets.AddRange(snap.Sheets);
            log($"  using the built-in research corpus ({db.Sheets.Count} sheets)");

            lock (MemoLock) { _memo = db; _memoKey = "embedded:" + key; }
            return db;
        }
        catch (Exception ex)
        {
            log($"  the built-in corpus could not be read: {ex.GetType().Name}");
            return new ResearchDatabase { Version = version };
        }
    }

    /// <summary>
    /// Parses the raw ARM research workbooks and writes an optimized, compressed JSON archive.
    /// </summary>
    public static void BuildEmbeddedCorpus(string armResearchFolder, string outputFile, string version = "UM", Action<string> log = null)
    {
        log ??= _ => { };
        var db = Load(armResearchFolder, version, log);
        var snap = new Snapshot(db) { Fingerprint = Fingerprint(armResearchFolder) };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snap, CacheOpts);

        string dir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var fs = File.Create(outputFile);
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal);
        gz.Write(json, 0, json.Length);
        log($"  embedded corpus built for {version}: {outputFile} ({db.Sheets.Count} sheets, {json.Length} uncompressed bytes)");
    }

    /// <summary>
    /// The corpus for a version: the folder when there is one, the built-in copy otherwise.
    /// </summary>
    public static ResearchDatabase LoadBest(string armResearchFolder, string version,
                                            string cachePath = null, Action<string> log = null)
    {
        bool haveFolder = !string.IsNullOrWhiteSpace(armResearchFolder) && Directory.Exists(armResearchFolder);
        if (haveFolder)
        {
            var db = LoadCached(armResearchFolder, version, cachePath, log);
            if (db.Sheets.Count > 0) return db;
        }
        return LoadEmbedded(version, log);
    }

    /// <summary>Discards the cache so the next load reparses.</summary>
    public static void ClearCache(string cachePath = null)
    {
        lock (MemoLock) { _memo = null; _memoKey = null; }
        try
        {
            cachePath ??= DefaultCachePath;
            if (File.Exists(cachePath)) File.Delete(cachePath);
        }
        catch { /* nothing depends on the delete succeeding */ }
    }

    /// <summary>Flat, human-diffable projection — handy for reviewing what the parser understood.</summary>
    private sealed class Snapshot
    {
        /// <summary>Source-folder signature this snapshot was produced from; empty when unknown.</summary>
        public string Fingerprint { get; set; } = "";

        public string SourceFolder { get; set; }
        public string Version { get; set; }
        public DateTime LoadedUtc { get; set; }
        public int SheetCount { get; set; }
        public Dictionary<string, int> KindCounts { get; set; }
        public List<string> Diagnostics { get; set; }
        public List<ResearchSheet> Sheets { get; set; }

        public Snapshot() { }
        public Snapshot(ResearchDatabase db)
        {
            SourceFolder = db.SourceFolder;
            Version = db.Version;
            LoadedUtc = db.LoadedUtc;
            SheetCount = db.Sheets.Count;
            KindCounts = db.Sheets.GroupBy(s => s.Kind.ToString())
                                  .ToDictionary(g => g.Key, g => g.Count());
            Diagnostics = db.Diagnostics;
            Sheets = db.Sheets;
        }
    }

    #endregion
}
