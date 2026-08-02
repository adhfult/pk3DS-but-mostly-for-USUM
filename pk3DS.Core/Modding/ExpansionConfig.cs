using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace pk3DS.Core.Modding
{
    public class ExpansionConfig
    {
        public int MaxSpecies { get; set; } = 1025;
        public int MaxMoves { get; set; } = 920;
        public int MaxItems { get; set; } = 1024;
        public int MaxAbilities { get; set; } = 320;
        public bool Enable16BitAbilities { get; set; } = true;
        public Dictionary<string, string> CustomOffsets { get; set; } = new();

        public static ExpansionConfig Load(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return new ExpansionConfig();
            try
            {
                string json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<ExpansionConfig>(json) ?? new ExpansionConfig();
            }
            catch
            {
                return new ExpansionConfig();
            }
        }

        public void Save(string jsonPath)
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, json);
            }
            catch { }
        }
    }
}
