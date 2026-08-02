using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace pk3DS.Core.Modding;

public class ProjectState
{
    private static ProjectState _instance;
    public static ProjectState Instance => _instance ??= Load();

    private static string _romfs;
    public static void SetRomFS(string path) => _romfs = path;

    public int MoveCount { get; set; } = 721;
    public int SpeciesCount { get; set; } = 807;
    public int AbilityCount { get; set; } = 233;
    public int TutorCodeOffset { get; set; } = 0;

    public Dictionary<string, int> RelocatedOffsets { get; set; } = new();
    public List<string> AppliedPatches { get; set; } = new();
    public HashSet<string> ModifiedFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ModdingProject CurrentModdingProject { get; set; } = new();

    public void RecordModifiedFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        ModifiedFiles.Add(normalized);
        Save();
    }

    private static string GetConfigPath()
    {
        if (string.IsNullOrEmpty(_romfs)) return null;
        return Path.Combine(_romfs, "pk3ds_project.json");
    }

    public static ProjectState Load()
    {
        string path = GetConfigPath();
        if (path == null || !File.Exists(path))
            return new ProjectState();

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProjectState>(json) ?? new ProjectState();
        }
        catch
        {
            return new ProjectState();
        }
    }

    public void Save()
    {
        string path = GetConfigPath();
        if (path == null) return;

        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public int GetOffset(string key, int defaultValue)
    {
        if (RelocatedOffsets.TryGetValue(key, out int offset))
            return offset;
        return defaultValue;
    }

    public void SetOffset(string key, int offset)
    {
        RelocatedOffsets[key] = offset;
        Save();
    }
}
