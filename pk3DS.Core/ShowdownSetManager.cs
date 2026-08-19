using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace pk3DS.WinForms
{
    public static class ShowdownSetManager
    {
        public const int MaxCapacity = 1500;
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "showdown_sets.json");
        public static List<ShowdownSet> Sets = new List<ShowdownSet>();

        public static void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    Sets = JsonSerializer.Deserialize<List<ShowdownSet>>(json) ?? new List<ShowdownSet>();
                }
                catch { Sets = new List<ShowdownSet>(); }
            }
        }

        public static void Save()
        {
            string json = JsonSerializer.Serialize(Sets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static bool AddSet(string content, string nickname = "")
        {
            if (Sets.Count >= MaxCapacity) return false;
            Sets.Add(new ShowdownSet { Content = content, Nickname = nickname, Timestamp = DateTime.Now });
            Save();
            return true;
        }

        public static void RemoveSet(int index)
        {
            if (index >= 0 && index < Sets.Count)
            {
                Sets.RemoveAt(index);
                Save();
            }
        }

        public static void ClearAll()
        {
            Sets.Clear();
            Save();
        }

        public static string[] GetSetListStrings()
        {
            return Sets.Select((s, i) => 
            {
                string firstLine = s.Content.Split('\n')[0].Trim();
                string nick = s.Nickname;
                string species = "";
                
                if (firstLine.Contains("@")) firstLine = firstLine.Split('@')[0].Trim();
                
                if (firstLine.Contains("(") && firstLine.Contains(")"))
                {
                    int start = firstLine.IndexOf("(");
                    int end = firstLine.LastIndexOf(")");
                    species = firstLine.Substring(start + 1, end - start - 1);
                    if (string.IsNullOrWhiteSpace(nick))
                        nick = firstLine.Substring(0, start).Trim();
                }
                else
                {
                    species = firstLine;
                }

                if (string.IsNullOrWhiteSpace(nick) || nick.Equals(species, StringComparison.OrdinalIgnoreCase))
                    return $"({species}) [{i + 1}]";
                return $"[{nick}] ({species})";
            }).ToArray();
        }

        public static string GetNickname(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            string firstLine = content.Split('\n')[0].Trim();
            if (firstLine.Contains("@")) firstLine = firstLine.Split('@')[0].Trim();
            if (firstLine.Contains("(") && firstLine.Contains(")"))
            {
                int start = firstLine.IndexOf("(");
                return firstLine.Substring(0, start).Trim();
            }
            return string.Empty;
        }

        public static string GetSetText(int index)
        {
            if (index >= 0 && index < Sets.Count)
                return Sets[index].Content;
            return string.Empty;
        }
    }

    public class ShowdownSet
    {
        public string Nickname { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
