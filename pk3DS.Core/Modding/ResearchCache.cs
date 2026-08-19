using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.Core.Modding
{
    /// <summary>
    /// Cached patch data extracted from an XLSX research sheet.
    /// </summary>
    public class CachedPatchSet
    {
        public string Name { get; set; }
        public string Category { get; set; }        // "Move", "Ability", "Item", "Generic", "AI", "Other", "Field"
        public string SourceFile { get; set; }       // XLSX path
        public string SourceSheet { get; set; }      // Sheet name
        public string TargetFile { get; set; }       // "Battle.cro", "code.bin", etc.
        public string Description { get; set; }
        public List<CachedPatch> Patches { get; set; } = new();
        public List<CachedRelocation> Relocations { get; set; } = new();
    }

    public class CachedPatch
    {
        public uint Offset { get; set; }
        public string HexBytes { get; set; }         // Hex string of patch bytes
        public string Assembly { get; set; }          // Original ARM assembly text (if available)
        public string Note { get; set; }
    }

    public class CachedRelocation
    {
        public uint WriteToOffset { get; set; }
        public uint TargetOffset { get; set; }
        public int Segment { get; set; }
        public string Note { get; set; }
    }

    public class CachedStockFunction
    {
        public string Name { get; set; }
        public uint Offset { get; set; }
        public string HexBytes { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// Persistent cache of all ARM Research XLSX data.
    /// Parses every XLSX in the research folder tree and makes the data queryable.
    /// Persists to a JSON sidecar file for fast reload.
    /// </summary>
    public class ResearchCache
    {
        public List<CachedPatchSet> MoveEdits { get; set; } = new();
        public List<CachedPatchSet> AbilityEdits { get; set; } = new();
        public List<CachedPatchSet> ItemEdits { get; set; } = new();
        public List<CachedPatchSet> GenericFunctions { get; set; } = new();
        public List<CachedPatchSet> AIEdits { get; set; } = new();
        public List<CachedPatchSet> OtherMechanics { get; set; } = new();
        public List<CachedPatchSet> FieldEffects { get; set; } = new();
        public List<CachedPatchSet> Research { get; set; } = new();
        public List<CachedPatchSet> RootSheets { get; set; } = new();
        public List<CachedStockFunction> StockFunctions { get; set; } = new();

        public DateTime LastParsed { get; set; }
        public int TotalSheetsParsed { get; set; }
        public int TotalPatchesCached { get; set; }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Load all XLSX files from an ARM Research folder tree.
        /// </summary>
        public void LoadAll(string armResearchFolder, string version = "UM", Action<string> log = null)
        {
            log ??= _ => { };
            if (!Directory.Exists(armResearchFolder))
            {
                log($"Research folder not found: {armResearchFolder}");
                return;
            }

            int sheetsTotal = 0;
            int patchesTotal = 0;

            // Subfolder mapping
            var folderMap = new (string subdir, string category, List<CachedPatchSet> target)[]
            {
                ("Move Edits", "Move", MoveEdits),
                ("Ability Edits", "Ability", AbilityEdits),
                ("Item Edits", "Item", ItemEdits),
                ("Generic Functions", "Generic", GenericFunctions),
                ("AI Edits", "AI", AIEdits),
                ("Other Mechanics", "Other", OtherMechanics),
                ("Field Effects", "Field", FieldEffects),
                ("Research", "Research", Research),
            };

            foreach (var (subdir, category, target) in folderMap)
            {
                string fullPath = Path.Combine(armResearchFolder, subdir);
                if (!Directory.Exists(fullPath)) continue;

                var xlsxFiles = Directory.GetFiles(fullPath, "*.xlsx", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("~$")) // Skip temp files
                    .ToArray();

                log($"  {category}: {xlsxFiles.Length} files");

                foreach (string xlsx in xlsxFiles)
                {
                    var (sets, sheets, patches) = ParseXlsxFile(xlsx, category, version, log);
                    target.AddRange(sets);
                    sheetsTotal += sheets;
                    patchesTotal += patches;
                }
            }

            // Root-level XLSX files
            var rootXlsx = Directory.GetFiles(armResearchFolder, "*.xlsx", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("~$"))
                .ToArray();

            log($"  Root: {rootXlsx.Length} files");
            foreach (string xlsx in rootXlsx)
            {
                var (sets, sheets, patches) = ParseXlsxFile(xlsx, "Root", version, log);
                RootSheets.AddRange(sets);
                sheetsTotal += sheets;
                patchesTotal += patches;
            }

            LastParsed = DateTime.UtcNow;
            TotalSheetsParsed = sheetsTotal;
            TotalPatchesCached = patchesTotal;

            log($"  Total: {sheetsTotal} sheets, {patchesTotal} patches cached");
        }

        private (List<CachedPatchSet> sets, int sheets, int patches) ParseXlsxFile(
            string xlsxPath, string category, string version, Action<string> log)
        {
            var sets = new List<CachedPatchSet>();
            int sheetCount = 0, patchCount = 0;

            try
            {
                var sheetNames = XlsxResearchParser.GetSheetNames(xlsxPath);
                string baseName = Path.GetFileNameWithoutExtension(xlsxPath);

                foreach (string sheetName in sheetNames)
                {
                    sheetCount++;
                    var rows = XlsxResearchParser.ReadSheet(xlsxPath, sheetName);
                    if (rows == null || rows.Count == 0) continue;

                    // Detect target file from content
                    string targetFile = DetectTargetFile(rows, baseName);

                    // Extract patches
                    var patchEntries = XlsxResearchParser.ExtractPatchEntries(rows, targetFile, version);

                    if (patchEntries.Count == 0) continue;

                    var patchSet = new CachedPatchSet
                    {
                        Name = sheetName == "Sheet1" ? baseName : $"{baseName} - {sheetName}",
                        Category = category,
                        SourceFile = xlsxPath,
                        SourceSheet = sheetName,
                        TargetFile = targetFile,
                        Description = $"{patchEntries.Count} patches from {Path.GetFileName(xlsxPath)}"
                    };

                    foreach (var (offset, bytes) in patchEntries)
                    {
                        patchSet.Patches.Add(new CachedPatch
                        {
                            Offset = offset,
                            HexBytes = BitConverter.ToString(bytes).Replace("-", "")
                        });
                        patchCount++;
                    }

                    // Also extract relocation entries if present
                    ExtractRelocationEntries(rows, patchSet);

                    sets.Add(patchSet);
                }
            }
            catch (Exception ex)
            {
                log($"    Error parsing {Path.GetFileName(xlsxPath)}: {ex.Message}");
            }

            return (sets, sheetCount, patchCount);
        }

        private static string DetectTargetFile(List<Dictionary<string, string>> rows, string baseName)
        {
            // Check if any row contains explicit target file references
            foreach (var row in rows)
            {
                foreach (var val in row.Values)
                {
                    if (val == null) continue;
                    string v = val.ToLowerInvariant();
                    if (v.Contains("battle.cro")) return "Battle.cro";
                    if (v.Contains("bag.cro")) return "Bag.cro";
                    if (v.Contains("box.cro")) return "Box.cro";
                    if (v.Contains("shop.cro")) return "Shop.cro";
                    if (v.Contains("code.bin")) return "code.bin";
                }
            }

            // Infer from folder/filename
            string lower = baseName.ToLowerInvariant();
            if (lower.Contains("code") || lower.Contains("trampoline")) return "code.bin";
            if (lower.Contains("bag")) return "Bag.cro";
            if (lower.Contains("shop")) return "Shop.cro";

            return "Battle.cro"; // Default
        }

        private static void ExtractRelocationEntries(List<Dictionary<string, string>> rows, CachedPatchSet patchSet)
        {
            foreach (var row in rows)
            {
                // Look for "write-to" or "RPT" columns
                string writeToKey = row.Keys.FirstOrDefault(k =>
                    k.IndexOf("write-to", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    k.IndexOf("WriteTo", StringComparison.OrdinalIgnoreCase) >= 0);

                string targetKey = row.Keys.FirstOrDefault(k =>
                    k.IndexOf("pointer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    k.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0);

                if (writeToKey == null || targetKey == null) continue;
                if (!row.ContainsKey(writeToKey) || !row.ContainsKey(targetKey)) continue;

                string wStr = row[writeToKey]?.Replace("0x", "").Replace("0X", "").Trim();
                string tStr = row[targetKey]?.Replace("0x", "").Replace("0X", "").Trim();

                if (string.IsNullOrEmpty(wStr) || string.IsNullOrEmpty(tStr)) continue;

                if (uint.TryParse(wStr, System.Globalization.NumberStyles.HexNumber, null, out uint writeOfs) &&
                    uint.TryParse(tStr, System.Globalization.NumberStyles.HexNumber, null, out uint targetOfs))
                {
                    string segKey = row.Keys.FirstOrDefault(k =>
                        k.IndexOf("segment", StringComparison.OrdinalIgnoreCase) >= 0);
                    int seg = 0;
                    if (segKey != null && row.ContainsKey(segKey))
                        int.TryParse(row[segKey], out seg);

                    patchSet.Relocations.Add(new CachedRelocation
                    {
                        WriteToOffset = writeOfs,
                        TargetOffset = targetOfs,
                        Segment = seg
                    });
                }
            }
        }

        #region Queries

        /// <summary>Get all cached patch sets across all categories.</summary>
        public IEnumerable<CachedPatchSet> AllPatchSets =>
            MoveEdits.Concat(AbilityEdits).Concat(ItemEdits)
                .Concat(GenericFunctions).Concat(AIEdits)
                .Concat(OtherMechanics).Concat(FieldEffects)
                .Concat(Research).Concat(RootSheets);

        /// <summary>Find a cached patch set by name (case-insensitive substring match).</summary>
        public CachedPatchSet FindByName(string name) =>
            AllPatchSets.FirstOrDefault(p =>
                p.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Get all patch sets for a specific move name.</summary>
        public List<CachedPatchSet> GetMoveEdits(string moveName) =>
            MoveEdits.Where(p => p.Name.IndexOf(moveName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        /// <summary>Get all patch sets for a specific ability name.</summary>
        public List<CachedPatchSet> GetAbilityEdits(string abilityName) =>
            AbilityEdits.Where(p => p.Name.IndexOf(abilityName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        /// <summary>Get all patch sets for a specific item name.</summary>
        public List<CachedPatchSet> GetItemEdits(string itemName) =>
            ItemEdits.Where(p => p.Name.IndexOf(itemName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        /// <summary>Get a generic/stock function by name.</summary>
        public CachedPatchSet GetGenericFunction(string name) =>
            GenericFunctions.FirstOrDefault(p =>
                p.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Get all patch sets targeting a specific file.</summary>
        public List<CachedPatchSet> GetPatchesForFile(string targetFile) =>
            AllPatchSets.Where(p =>
                p.TargetFile.Equals(targetFile, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>Search all patch sets by keyword.</summary>
        public List<CachedPatchSet> Search(string keyword) =>
            AllPatchSets.Where(p =>
                (p.Name?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Description?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Category?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

        #endregion

        #region Persistence

        /// <summary>Save the cache to a JSON file.</summary>
        public void SaveCache(string jsonPath)
        {
            try
            {
                string json = JsonSerializer.Serialize(this, JsonOpts);
                File.WriteAllText(jsonPath, json);
            }
            catch { }
        }

        /// <summary>Load a previously saved cache.</summary>
        public static ResearchCache LoadCache(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return null;
            try
            {
                string json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<ResearchCache>(json, JsonOpts);
            }
            catch { return null; }
        }

        /// <summary>Check if cache is stale (research folder modified after last parse).</summary>
        public bool IsStale(string armResearchFolder)
        {
            if (!Directory.Exists(armResearchFolder)) return true;
            var newest = Directory.GetFiles(armResearchFolder, "*.xlsx", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("~$"))
                .Select(f => File.GetLastWriteTimeUtc(f))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
            return newest > LastParsed;
        }

        #endregion
    }
}
