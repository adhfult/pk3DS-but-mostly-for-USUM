using System;
using System.Collections.Generic;
using System.IO;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding
{
    public static class PokemonPlusPatcher
    {
        public static bool ApplyPokemonPlusPatch(string romfsPath, string exefsPath, string patchSourceDir, string xdeltaPatchPath, out string statusMessage)
        {
            statusMessage = "";
            if (!Directory.Exists(patchSourceDir))
            {
                statusMessage = $"Pokemon+ source directory not found: {patchSourceDir}";
                return false;
            }

            try
            {
                int copiedFiles = 0;

                // 1. Copy ExeFS files (recursively copy everything pasted into patchSourceDir/exefs or root)
                string patchExeFS = Path.Combine(patchSourceDir, "exefs");
                if (Directory.Exists(patchExeFS) && Directory.Exists(exefsPath))
                {
                    foreach (string file in Directory.GetFiles(patchExeFS, "*", SearchOption.AllDirectories))
                    {
                        FileInfo fi = new FileInfo(file);
                        if (!RomFS.ShouldIncludeFile(fi)) continue;

                        string relPath = Path.GetRelativePath(patchExeFS, file);
                        string targetPath = Path.Combine(exefsPath, relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        File.Copy(file, targetPath, true);
                        copiedFiles++;
                        ProjectState.Instance.RecordModifiedFile($"exefs/{relPath}");
                    }
                }

                // If code.bin is directly in root patchSourceDir
                string patchRootCodeBin = Path.Combine(patchSourceDir, "code.bin");
                if (File.Exists(patchRootCodeBin) && Directory.Exists(exefsPath))
                {
                    string targetCodeBin = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(exefsPath);
                    File.Copy(patchRootCodeBin, targetCodeBin, true);
                    copiedFiles++;
                    ProjectState.Instance.RecordModifiedFile("exefs/code.bin");
                }

                // Apply form reversion fix if code.bin exists in exefsPath
                if (Directory.Exists(exefsPath) && File.Exists(pk3DS.Core.CTR.ExeFS.ResolveCodeBin(exefsPath)))
                {
                    ApplyFormReversionFix(pk3DS.Core.CTR.ExeFS.ResolveCodeBin(exefsPath), out _);
                    ProjectState.Instance.RecordModifiedFile("exefs/code.bin");
                }

                // 2. Copy RomFS files (recursively copy everything pasted into patchSourceDir/romfs)
                string patchRomfs = Path.Combine(patchSourceDir, "romfs");
                if (Directory.Exists(patchRomfs) && Directory.Exists(romfsPath))
                {
                    foreach (string file in Directory.GetFiles(patchRomfs, "*", SearchOption.AllDirectories))
                    {
                        FileInfo fi = new FileInfo(file);
                        if (!RomFS.ShouldIncludeFile(fi)) continue;
                        if (fi.Directory != null && !RomFS.ShouldIncludeDir(fi.Directory)) continue;

                        string relPath = Path.GetRelativePath(patchRomfs, file);
                        string targetPath = Path.Combine(romfsPath, relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        File.Copy(file, targetPath, true);
                        copiedFiles++;
                        ProjectState.Instance.RecordModifiedFile($"romfs/{relPath}");
                    }
                }

                // Also check if patchSourceDir has a direct 'a' folder or '*.cro' files at root
                string patchDirectA = Path.Combine(patchSourceDir, "a");
                if (Directory.Exists(patchDirectA) && Directory.Exists(romfsPath))
                {
                    foreach (string file in Directory.GetFiles(patchDirectA, "*", SearchOption.AllDirectories))
                    {
                        FileInfo fi = new FileInfo(file);
                        if (!RomFS.ShouldIncludeFile(fi)) continue;

                        string relPath = Path.GetRelativePath(patchDirectA, file);
                        string targetPath = Path.Combine(romfsPath, "a", relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        File.Copy(file, targetPath, true);
                        copiedFiles++;
                        ProjectState.Instance.RecordModifiedFile($"romfs/a/{relPath}");
                    }
                }

                foreach (string file in Directory.GetFiles(patchSourceDir, "*.cro", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    string targetPath = Path.Combine(romfsPath, fileName);
                    File.Copy(file, targetPath, true);
                    copiedFiles++;
                    ProjectState.Instance.RecordModifiedFile($"romfs/{fileName}");
                }

                // Duplicate text GARC across all language variants (a/0/3/0 .. a/0/3/8) if text GARC present
                string textGarcSource = Path.Combine(romfsPath, "a", "0", "3", "2");
                if (File.Exists(textGarcSource))
                {
                    for (int l = 0; l <= 8; l++)
                    {
                        if (l == 2) continue;
                        string langTarget = Path.Combine(romfsPath, "a", "0", "3", l.ToString());
                        File.Copy(textGarcSource, langTarget, true);
                        ProjectState.Instance.RecordModifiedFile($"romfs/a/0/3/{l}");
                    }
                }

                // 3. Apply 4.xdelta model expansion patch to a/0/9/4 if provided
                if (!string.IsNullOrEmpty(xdeltaPatchPath) && File.Exists(xdeltaPatchPath))
                {
                    string modelGarc = Path.Combine(romfsPath, "a", "0", "9", "4");
                    if (File.Exists(modelGarc))
                    {
                        string tempPatched = modelGarc + ".patched";
                        bool xdeltaSuccess = XDeltaUtil.ApplyPatch(modelGarc, xdeltaPatchPath, tempPatched);
                        if (xdeltaSuccess && File.Exists(tempPatched))
                        {
                            File.Copy(tempPatched, modelGarc, true);
                            File.Delete(tempPatched);
                            copiedFiles++;
                            ProjectState.Instance.RecordModifiedFile("romfs/a/0/9/4");
                        }
                    }
                }

                // Mark Project State
                ProjectState.Instance.AppliedPatches.Add($"Pokemon+ Patch ({Path.GetFileName(patchSourceDir)})");
                ProjectState.Instance.Save();

                statusMessage = $"Successfully applied patch ({copiedFiles} files updated from '{Path.GetFileName(patchSourceDir)}').";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = $"Error applying patch: {ex.Message}";
                return false;
            }
        }

        public static bool ExportLayeredFSModPackage(string romfsPath, string exefsPath, string outputDir, out string statusMessage)
        {
            statusMessage = "";
            try
            {
                // Detect Game Title ID (Ultra Sun = 00040000001B5000, Ultra Moon = 00040000001B5100)
                string titleId = "00040000001B5000";
                if (!string.IsNullOrEmpty(romfsPath) && (romfsPath.Contains("UM", StringComparison.OrdinalIgnoreCase) || romfsPath.Contains("Moon", StringComparison.OrdinalIgnoreCase)))
                {
                    titleId = "00040000001B5100";
                }

                string lumaTarget = Path.Combine(outputDir, "luma", "titles", titleId);
                string outRomfs = Path.Combine(lumaTarget, "romfs");
                string outExefs = Path.Combine(lumaTarget, "exefs");

                Directory.CreateDirectory(outRomfs);
                Directory.CreateDirectory(outExefs);

                int copiedFiles = 0;
                long totalBytes = 0;

                HashSet<string> filesToExport = new(StringComparer.OrdinalIgnoreCase);

                // 1. Gather all files explicitly tracked in ProjectState
                foreach (string file in ProjectState.Instance.ModifiedFiles)
                {
                    filesToExport.Add(file);
                }

                // 2. Scan US/UM patch directories for any files pasted by user
                string[] patchFolders = ["US", "UM"];
                foreach (string pf in patchFolders)
                {
                    string baseCand = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pf);
                    string cwdCand = Path.Combine(Directory.GetCurrentDirectory(), pf);
                    List<string> cands = new();
                    if (Directory.Exists(baseCand)) cands.Add(baseCand);
                    if (Directory.Exists(cwdCand)) cands.Add(cwdCand);

                    foreach (string cand in cands)
                    {
                        string pRomfs = Path.Combine(cand, "romfs");
                        if (Directory.Exists(pRomfs))
                        {
                            foreach (string f in Directory.GetFiles(pRomfs, "*", SearchOption.AllDirectories))
                            {
                                string rel = Path.GetRelativePath(pRomfs, f);
                                filesToExport.Add($"romfs/{rel}");
                            }
                        }
                        string pExe = Path.Combine(cand, "exefs");
                        if (Directory.Exists(pExe))
                        {
                            foreach (string f in Directory.GetFiles(pExe, "*", SearchOption.AllDirectories))
                            {
                                string rel = Path.GetRelativePath(pExe, f);
                                filesToExport.Add($"exefs/{rel}");
                            }
                        }
                        // Direct 'a' or '*.cro' at root of patch folder
                        string pA = Path.Combine(cand, "a");
                        if (Directory.Exists(pA))
                        {
                            foreach (string f in Directory.GetFiles(pA, "*", SearchOption.AllDirectories))
                            {
                                string rel = Path.GetRelativePath(pA, f);
                                filesToExport.Add($"romfs/a/{rel}");
                            }
                        }
                        foreach (string f in Directory.GetFiles(cand, "*.cro", SearchOption.TopDirectoryOnly))
                        {
                            filesToExport.Add($"romfs/{Path.GetFileName(f)}");
                        }
                        if (File.Exists(pk3DS.Core.CTR.ExeFS.ResolveCodeBin(cand)))
                        {
                            filesToExport.Add("exefs/code.bin");
                        }
                    }
                }

                // 3. Always include code.bin if present in exefsPath and patches were applied
                if (Directory.Exists(exefsPath) && File.Exists(pk3DS.Core.CTR.ExeFS.ResolveCodeBin(exefsPath)))
                {
                    filesToExport.Add("exefs/code.bin");
                }

                // 4. Always include any CRO files present in romfsPath
                if (Directory.Exists(romfsPath))
                {
                    foreach (string croFile in Directory.GetFiles(romfsPath, "*.cro", SearchOption.TopDirectoryOnly))
                    {
                        filesToExport.Add($"romfs/{Path.GetFileName(croFile)}");
                    }
                }

                // Copy only the modified / pasted files
                foreach (string relFile in filesToExport)
                {
                    string normalized = relFile.Replace('\\', '/').TrimStart('/');
                    if (normalized.StartsWith("exefs/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = normalized.Substring("exefs/".Length);
                        string src = Path.Combine(exefsPath ?? "", sub.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(src))
                        {
                            string dest = Path.Combine(outExefs, sub.Replace('/', Path.DirectorySeparatorChar));
                            Directory.CreateDirectory(Path.GetDirectoryName(dest));
                            File.Copy(src, dest, true);
                            copiedFiles++;
                            totalBytes += new FileInfo(dest).Length;
                        }
                    }
                    else if (normalized.StartsWith("romfs/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = normalized.Substring("romfs/".Length);
                        string src = Path.Combine(romfsPath ?? "", sub.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(src))
                        {
                            string dest = Path.Combine(outRomfs, sub.Replace('/', Path.DirectorySeparatorChar));
                            Directory.CreateDirectory(Path.GetDirectoryName(dest));
                            File.Copy(src, dest, true);
                            copiedFiles++;
                            totalBytes += new FileInfo(dest).Length;
                        }
                    }
                }

                double mb = (double)totalBytes / (1024 * 1024);
                statusMessage = $"Exported compact Luma3DS / LayeredFS mod package ({copiedFiles} files, {mb:F1} MB) to:\n{lumaTarget}\n\nCopy the 'luma' folder directly to your 3DS SD card or Citra load/mods folder!";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = $"Error exporting Luma3DS patch: {ex.Message}";
                return false;
            }
        }

        public static bool ApplyFormReversionFix(string codeBinPath, out string message)
        {
            message = "";
            if (!File.Exists(codeBinPath))
            {
                message = "code.bin does not exist.";
                return false;
            }

            try
            {
                byte[] codeData = File.ReadAllBytes(codeBinPath);
                string version = ResearchEngine.DetectGameVersion(codeData);
                int offset = -1;

                if (version == "US" && codeData.Length > 0x00344C7F && codeData[0x00344C7F] == 0x0A)
                {
                    offset = 0x00344C7F;
                }
                else if (version == "UM" && codeData.Length > 0x00344C83 && codeData[0x00344C83] == 0x0A)
                {
                    offset = 0x00344C83;
                }
                else
                {
                    if (codeData.Length > 0x00344C83 && codeData[0x00344C83] == 0x0A)
                        offset = 0x00344C83;
                    else if (codeData.Length > 0x00344C7F && codeData[0x00344C7F] == 0x0A)
                        offset = 0x00344C7F;
                }

                if (offset != -1 && offset < codeData.Length)
                {
                    byte oldByte = codeData[offset];
                    codeData[offset] = 0xEA;
                    if (!BinaryWriteGuard.TryWrite(codeBinPath, codeData,
                            "Apply the form reversion fix",
                            $"Changes one byte at 0x{offset:X} from 0x{oldByte:X2} to 0xEA."))
                    {
                        message = "Form reversion fix was not applied - the write was declined.";
                        return false;
                    }
                    message = $"Form reversion fix applied to code.bin at 0x{offset:X} (old: 0x{oldByte:X2}, new: 0xEA).";
                    return true;
                }

                message = "Could not locate form reversion instruction offset in code.bin.";
                return false;
            }
            catch (Exception ex)
            {
                message = $"Error applying form reversion fix: {ex.Message}";
                return false;
            }
        }
    }
}
