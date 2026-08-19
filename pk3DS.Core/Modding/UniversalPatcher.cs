using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding
{
    public static class UniversalPatcher
    {
        public static string GetFilePath(string target, string romfs, string exefs)
        {
            if (string.IsNullOrEmpty(target)) return null;

            // 1. Try Root Match (The most common layout for clean extractions)
            string rootPath = Path.Combine(romfs, target);
            if (File.Exists(rootPath)) return rootPath;

            // 2. Try Standard Gen 7 Subfolder Patterns (folder/file.cro)
            string name = Path.GetFileNameWithoutExtension(target);
            string subfolderPath = Path.Combine(romfs, name, target);
            if (File.Exists(subfolderPath)) return subfolderPath;

            // 3. Specific Logic for Code.bin (ExeFS)
            if (target.Equals("code.bin", StringComparison.OrdinalIgnoreCase))
            {
                string p1 = Path.Combine(exefs, "code.bin");
                if (File.Exists(p1)) return p1;
                string p2 = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(exefs);
                if (File.Exists(p2)) return p2;
            }

            // 4. Handle slash-based explicit paths
            if (target.Contains("/"))
                return Path.Combine(romfs, target.Replace("/", Path.DirectorySeparatorChar.ToString()));

            return rootPath; // Fallback to root path even if not found, to trigger manual checks later
        }

        public static bool ApplyPatchGroup(PatchGroup group, string romfs, string exefs)
        {
            var info = AnalyzePatchGroup(group, romfs, exefs);
            if (!info.FileExists) return false;

            byte[] data = File.ReadAllBytes(info.AbsolutePath);
            bool modified = false;

            foreach (var p in info.AppliedPatches)
            {
                try
                {
                    if (!p.Success) continue;
                    if (info.AbsolutePath.EndsWith(".cro", StringComparison.OrdinalIgnoreCase))
                    {
                        data = CROUtil.InjectSandboxPatch(data, (uint)p.Offset, p.Data);
                    }
                    else
                    {
                        if (p.Offset + p.Data.Length > data.Length) continue;
                        Array.Copy(p.Data, 0, data, p.Offset, p.Data.Length);
                    }
                    modified = true;
                }
                catch { continue; }
            }

            if (modified)
            {
                File.WriteAllBytes(info.AbsolutePath, data);
                ProjectState.Instance.AppliedPatches.Add($"{group.Module} - {group.Sheet}");
                ProjectState.Instance.Save();
                return true;
            }
            return false;
        }

        public static AnalyzePatchInfo AnalyzePatchGroup(PatchGroup group, string romfs, string exefs)
        {
            var info = new AnalyzePatchInfo { AbsolutePath = GetFilePath(group.Target, romfs, exefs) };
            if (!File.Exists(info.AbsolutePath)) return info;

            info.FileExists = true;
            byte[] data = File.ReadAllBytes(info.AbsolutePath);
            if (info.AbsolutePath.EndsWith(".cro", StringComparison.OrdinalIgnoreCase))
                info.CROAudit = CROUtil.AuditIntegrity(data);

            foreach (var p in group.Patches)
            {
                string hexStr = p.Hex;
                foreach (var param in group.Parameters)
                    hexStr = hexStr.Replace(param.Key, param.Value);

                byte[] patchData = Util.StringToByteArray(hexStr);
                int offset = -1;

                if (!string.IsNullOrEmpty(p.Signature))
                {
                    byte[] sig = Util.StringToByteArray(p.Signature);
                    offset = Util.IndexOfBytes(data, sig, 0, data.Length);
                }
                else if (!string.IsNullOrEmpty(p.Offset))
                {
                    offset = (int)Convert.ToUInt32(p.Offset, 16);
                }

                info.AppliedPatches.Add(new PatchAnalysis { 
                    Offset = offset, 
                    Data = patchData, 
                    Success = offset >= 0,
                    Note = p.Note
                });
            }
            return info;
        }

        private static byte[] GetBInstruction(int from, int to)
        {
            int offset = (to - from - 8) >> 2;
            byte[] result = BitConverter.GetBytes(offset);
            result[3] = 0xEA;
            return result;
        }

        public static List<PatchGroup> LoadPatchGroups(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return new List<PatchGroup>();
            try
            {
                string json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<List<PatchGroup>>(json) ?? new List<PatchGroup>();
            }
            catch { return new List<PatchGroup>(); }
        }
    }

    public class AnalyzePatchInfo
    {
        public string AbsolutePath { get; set; }
        public bool FileExists { get; set; }
        public AuditReport CROAudit { get; set; }
        public List<PatchAnalysis> AppliedPatches { get; set; } = new();
    }

    public class PatchAnalysis
    {
        public int Offset { get; set; }
        public byte[] Data { get; set; }
        public bool Success { get; set; }
        public string Note { get; set; }
    }
}
