using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pk3DS.Core.CTR;

public static class ExternalRebuilder
{
    public static string FindTool(string toolName)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string cwd = Directory.GetCurrentDirectory();

        string lower = toolName.ToLowerInvariant();
        string[] aliases = lower switch
        {
            "makerom.exe" or "makerom" => ["makerom.exe", "MakeRom64.exe", "MakeRom32.exe"],
            "3dstool.exe" or "3dstool" => ["3dstool.exe"],
            "ctrtool.exe" or "ctrtool" => ["ctrtool.exe", "CtrTool64.exe", "CtrTool32.exe"],
            _ => [toolName]
        };

        string[] searchDirs =
        [
            Path.Combine(baseDir, "PackEnglishV9"),
            Path.Combine(cwd, "PackEnglishV9"),
            Path.Combine(baseDir, "DotNet.3DS.Toolkit.v1.4.6"),
            Path.Combine(cwd, "DotNet.3DS.Toolkit.v1.4.6"),
            baseDir,
            cwd
        ];

        foreach (string dir in searchDirs)
        {
            foreach (string alias in aliases)
            {
                string candidate = Path.Combine(dir, alias);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    public static bool RebuildRomFS(string romfsFolder, string outputRomfsBin, Action<string> statusCallback = null)
    {
        string tool = FindTool("3dstool.exe");
        if (tool == null)
        {
            statusCallback?.Invoke("3dstool.exe not found in tools folder. Falling back to internal RomFS builder...");
            return false;
        }

        string args = $"-cvtf romfs \"{outputRomfsBin}\" --romfs-dir \"{romfsFolder}\"";
        return RunProcess(tool, args, statusCallback);
    }

    public static bool RebuildExeFS(string exefsFolder, string outputExefsBin, string headerBin = null, Action<string> statusCallback = null)
    {
        try
        {
            var files = ExeFS.GetExeFSFiles(exefsFolder);
            bool success = ExeFS.PackExeFS(files, outputExefsBin);
            if (success)
            {
                statusCallback?.Invoke("ExeFS internally rebuilt successfully.");
                return true;
            }
            statusCallback?.Invoke("Failed to pack ExeFS internally.");
            return false;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"Error packing ExeFS: {ex.Message}");
            return false;
        }
    }

    public static bool BuildCIA(string romfsBin, string exefsBin, string exhBin, string outputCia, Action<string> statusCallback = null)
    {
        string tool = FindTool("makerom.exe");
        if (tool == null)
        {
            statusCallback?.Invoke("makerom.exe not found in tools folder.");
            return false;
        }

        string args = $"-f cia -o \"{outputCia}\" -target t -exefslogo";
        if (File.Exists(romfsBin)) args += $" -romfs \"{romfsBin}\"";
        if (File.Exists(exefsBin)) args += $" -exefs \"{exefsBin}\"";
        if (File.Exists(exhBin)) args += $" -exh \"{exhBin}\"";

        return RunProcess(tool, args, statusCallback);
    }

    public static bool Build3DS(string romfsBin, string exefsBin, string exhBin, string output3ds, Action<string> statusCallback = null)
    {
        string tool = FindTool("makerom.exe");
        if (tool == null)
        {
            statusCallback?.Invoke("makerom.exe not found in tools folder.");
            return false;
        }

        string args = $"-f cci -o \"{output3ds}\" -target t -exefslogo";
        if (File.Exists(romfsBin)) args += $" -romfs \"{romfsBin}\"";
        if (File.Exists(exefsBin)) args += $" -exefs \"{exefsBin}\"";
        if (File.Exists(exhBin)) args += $" -exh \"{exhBin}\"";

        return RunProcess(tool, args, statusCallback);
    }

    public static bool RebuildFull3DS(string romfsFolder, string exefsFolder, string exhPath, string output3dsPath, Action<string> statusCallback = null)
    {
        string tempRomfs = Path.Combine(Path.GetTempPath(), $"temp_romfs_{Guid.NewGuid():N}.bin");
        string tempExefs = Path.Combine(Path.GetTempPath(), $"temp_exefs_{Guid.NewGuid():N}.bin");
        string tempExh = Path.Combine(Path.GetTempPath(), $"temp_exh_{Guid.NewGuid():N}.bin");

        try
        {
            if (!string.IsNullOrEmpty(romfsFolder) && Directory.Exists(romfsFolder))
            {
                statusCallback?.Invoke("Building RomFS binary...");
                if (!RebuildRomFS(romfsFolder, tempRomfs, statusCallback))
                    return false;
            }

            if (!string.IsNullOrEmpty(exefsFolder) && Directory.Exists(exefsFolder))
            {
                statusCallback?.Invoke("Building ExeFS binary...");
                string headerBin = Path.Combine(exefsFolder, "Header.bin");
                if (!File.Exists(headerBin)) headerBin = null;
                if (!RebuildExeFS(exefsFolder, tempExefs, headerBin, statusCallback))
                    return false;
            }

            string actualExhPath = exhPath;
            if (File.Exists(exhPath))
            {
                byte[] exhBytes = File.ReadAllBytes(exhPath);
                string codeBinPath = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(exefsFolder);
                if (exhBytes.Length >= 0x800 && File.Exists(codeBinPath))
                {
                    uint codeBinSize = (uint)new FileInfo(codeBinPath).Length;
                    uint textSize = BitConverter.ToUInt32(exhBytes, 0x18);
                    uint roSize   = BitConverter.ToUInt32(exhBytes, 0x28);
                    uint dataSize = BitConverter.ToUInt32(exhBytes, 0x38);
                    uint declared = textSize + roSize + dataSize;

                    if (codeBinSize > declared)
                    {
                        uint delta = codeBinSize - declared;
                        uint newDataSize = dataSize + delta;
                        uint newDataPages = (newDataSize + 0xFFF) / 0x1000;

                        statusCallback?.Invoke($"Patching ExHeader for makerom: code.bin size 0x{codeBinSize:X} > declared 0x{declared:X}. Updating .data segment pages to {newDataPages}.");

                        // Patch SCI
                        Array.Copy(BitConverter.GetBytes(newDataSize), 0, exhBytes, 0x38, 4);
                        Array.Copy(BitConverter.GetBytes(newDataPages), 0, exhBytes, 0x34, 4);

                        // Patch AccessDescriptor (ACID)
                        Array.Copy(BitConverter.GetBytes(newDataSize), 0, exhBytes, 0x400 + 0x38, 4);
                        Array.Copy(BitConverter.GetBytes(newDataPages), 0, exhBytes, 0x400 + 0x34, 4);

                        File.WriteAllBytes(tempExh, exhBytes);
                        actualExhPath = tempExh;
                    }
                }
            }

            statusCallback?.Invoke("Building .3ds CCI ROM with makerom...");
            return Build3DS(tempRomfs, tempExefs, actualExhPath, output3dsPath, statusCallback);
        }
        finally
        {
            if (File.Exists(tempRomfs)) try { File.Delete(tempRomfs); } catch { }
            if (File.Exists(tempExefs)) try { File.Delete(tempExefs); } catch { }
            if (File.Exists(tempExh)) try { File.Delete(tempExh); } catch { }
        }
    }

    public static string LaunchExternalBatchRebuild(string romfsFolder, string exefsFolder, string exhPath, string outputPath, out string sentinelDone, out string sentinelError, Action<string> statusCallback = null, bool trim = false)
    {
        string tool3ds = FindTool("3dstool.exe");
        string toolConsole = FindTool("ToolkitConsole.exe");

        if (tool3ds == null && toolConsole == null)
        {
            statusCallback?.Invoke("Neither 3dstool.exe nor ToolkitConsole.exe were found in tools directory.");
            sentinelDone = null;
            sentinelError = null;
            return null;
        }

        string gameParentDir = !string.IsNullOrEmpty(romfsFolder) 
            ? Path.GetDirectoryName(romfsFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) 
            : null;

        if (string.IsNullOrEmpty(gameParentDir) || !Directory.Exists(gameParentDir))
        {
            statusCallback?.Invoke("Game parent directory not found for rebuild.");
            sentinelDone = null;
            sentinelError = null;
            return null;
        }

        string romfsDirName = !string.IsNullOrEmpty(romfsFolder)
            ? Path.GetFileName(romfsFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : "RomFS";
        string exefsDirName = !string.IsNullOrEmpty(exefsFolder)
            ? Path.GetFileName(exefsFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : "ExeFS";

        // Synchronize ExeFS code.bin and .code.bin based on ResolveCodeBin
        if (!string.IsNullOrEmpty(exefsFolder) && Directory.Exists(exefsFolder))
        {
            try
            {
                string resolvedCode = ExeFS.ResolveCodeBin(exefsFolder);
                string dotCode = Path.Combine(exefsFolder, ".code.bin");
                string plainCode = Path.Combine(exefsFolder, "code.bin");
                if (File.Exists(resolvedCode))
                {
                    if (resolvedCode.Equals(dotCode, StringComparison.OrdinalIgnoreCase))
                        File.Copy(dotCode, plainCode, overwrite: true);
                    else
                        File.Copy(plainCode, dotCode, overwrite: true);
                }

                string bBin = Path.Combine(exefsFolder, "banner.bin");
                string bBnr = Path.Combine(exefsFolder, "banner.bnr");
                if (File.Exists(bBin) && !File.Exists(bBnr)) File.Copy(bBin, bBnr, overwrite: true);
                else if (File.Exists(bBnr) && !File.Exists(bBin)) File.Copy(bBnr, bBin, overwrite: true);

                string iBin = Path.Combine(exefsFolder, "icon.bin");
                string iIcn = Path.Combine(exefsFolder, "icon.icn");
                if (File.Exists(iBin) && !File.Exists(iIcn)) File.Copy(iBin, iIcn, overwrite: true);
                else if (File.Exists(iIcn) && !File.Exists(iBin)) File.Copy(iIcn, iBin, overwrite: true);
            }
            catch { }
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"pk3DS_rebuild_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        sentinelDone = Path.Combine(tempDir, "_rebuild_done.flag");
        sentinelError = Path.Combine(tempDir, "_rebuild_error.flag");
        string batPath = Path.Combine(tempDir, "rebuild_game.bat");

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("title pk3DS Direct 3DS ROM Rebuilder");
        sb.AppendLine("color 0A");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine("echo      pk3DS - External 3DS ROM Rebuilder");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine("echo.");

        if (tool3ds != null)
        {
            sb.AppendLine($"cd /d \"{gameParentDir}\"");
            sb.AppendLine("if not exist temp_build mkdir temp_build");
            sb.AppendLine();

            sb.AppendLine("echo [1/5] Building Custom RomFS...");
            sb.AppendLine($"\"{tool3ds}\" -cvtf romfs temp_build\\CustomRomFS.bin --romfs-dir \"{romfsDirName}\"");
            sb.AppendLine("if %errorlevel% neq 0 goto ERROR");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();

            sb.AppendLine("echo [2/5] Building Custom ExeFS...");
            sb.AppendLine($"if exist \"{exefsDirName}\\.code.bin\" copy /y \"{exefsDirName}\\.code.bin\" \"{exefsDirName}\\code.bin\" >nul");
            sb.AppendLine($"if not exist \"{exefsDirName}\\.code.bin\" if exist \"{exefsDirName}\\code.bin\" copy /y \"{exefsDirName}\\code.bin\" \"{exefsDirName}\\.code.bin\" >nul");
            sb.AppendLine($"if exist \"{exefsDirName}\\banner.bin\" copy /y \"{exefsDirName}\\banner.bin\" \"{exefsDirName}\\banner.bnr\" >nul");
            sb.AppendLine($"if not exist \"{exefsDirName}\\banner.bin\" if exist \"{exefsDirName}\\banner.bnr\" copy /y \"{exefsDirName}\\banner.bnr\" \"{exefsDirName}\\banner.bin\" >nul");
            sb.AppendLine($"if exist \"{exefsDirName}\\icon.bin\" copy /y \"{exefsDirName}\\icon.bin\" \"{exefsDirName}\\icon.icn\" >nul");
            sb.AppendLine($"if not exist \"{exefsDirName}\\icon.bin\" if exist \"{exefsDirName}\\icon.icn\" copy /y \"{exefsDirName}\\icon.icn\" \"{exefsDirName}\\icon.bin\" >nul");

            string exefsCmd = $"\"{tool3ds}\" -cvtf exefs temp_build\\CustomExeFS.bin --exefs-dir \"{exefsDirName}\"";
            if (File.Exists(Path.Combine(gameParentDir, "HeaderExeFS.bin"))) exefsCmd += " --header HeaderExeFS.bin";
            sb.AppendLine(exefsCmd);
            sb.AppendLine("if %errorlevel% neq 0 goto ERROR");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();

            sb.AppendLine("echo [3/5] Building Optional Partitions...");
            bool hasManual = Directory.Exists(Path.Combine(gameParentDir, "Manual")) && File.Exists(Path.Combine(gameParentDir, "HeaderNCCH1.bin"));
            bool hasDLP = Directory.Exists(Path.Combine(gameParentDir, "DownloadPlay")) && File.Exists(Path.Combine(gameParentDir, "HeaderNCCH2.bin"));
            bool hasN3DSUpdate = Directory.Exists(Path.Combine(gameParentDir, "N3DSUpdate")) && File.Exists(Path.Combine(gameParentDir, "HeaderNCCH6.bin"));
            bool hasO3DSUpdate = Directory.Exists(Path.Combine(gameParentDir, "O3DSUpdate")) && File.Exists(Path.Combine(gameParentDir, "HeaderNCCH7.bin"));

            if (hasManual)
                sb.AppendLine($"\"{tool3ds}\" -cvtf romfs temp_build\\CustomManual.bin --romfs-dir Manual");
            if (hasDLP)
                sb.AppendLine($"\"{tool3ds}\" -cvtf romfs temp_build\\CustomDownloadPlay.bin --romfs-dir DownloadPlay");
            if (hasN3DSUpdate)
                sb.AppendLine($"\"{tool3ds}\" -cvtf romfs temp_build\\CustomN3DSUpdate.bin --romfs-dir N3DSUpdate");
            if (hasO3DSUpdate)
                sb.AppendLine($"\"{tool3ds}\" -cvtf romfs temp_build\\CustomO3DSUpdate.bin --romfs-dir O3DSUpdate");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();

            sb.AppendLine("echo [4/5] Constructing Partition 0 (CXI)...");
            string exhFile = !string.IsNullOrEmpty(exhPath) && File.Exists(exhPath)
                ? Path.GetFileName(exhPath)
                : (File.Exists(Path.Combine(gameParentDir, "DecryptedExHeader.bin")) ? "DecryptedExHeader.bin" : "ExHeader.bin");
            string header0File = File.Exists(Path.Combine(gameParentDir, "HeaderNCCH0.bin"))
                ? "HeaderNCCH0.bin"
                : (File.Exists(Path.Combine(gameParentDir, "HeaderNCCH.bin")) ? "HeaderNCCH.bin" : "HeaderNCCH0.bin");

            string cxiCmd = $"\"{tool3ds}\" -cvtf cxi temp_build\\CustomPartition0.bin --header {header0File} --exh {exhFile} --exefs temp_build\\CustomExeFS.bin --romfs temp_build\\CustomRomFS.bin";
            string logoFile = Directory.GetFiles(gameParentDir).Select(Path.GetFileName).FirstOrDefault(f => f.StartsWith("logo", StringComparison.OrdinalIgnoreCase));
            if (logoFile != null) cxiCmd += $" --logo {logoFile}";
            string plainFile = Directory.GetFiles(gameParentDir).Select(Path.GetFileName).FirstOrDefault(f => f.StartsWith("plain", StringComparison.OrdinalIgnoreCase));
            if (plainFile != null) cxiCmd += $" --plain {plainFile}";

            sb.AppendLine(cxiCmd);
            sb.AppendLine("if %errorlevel% neq 0 goto ERROR");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();

            sb.AppendLine("echo Constructing remaining NCCH partitions...");
            if (hasManual)
                sb.AppendLine($"if exist temp_build\\CustomManual.bin \"{tool3ds}\" -cvtf cfa temp_build\\CustomPartition1.bin --header HeaderNCCH1.bin --romfs temp_build\\CustomManual.bin");
            if (hasDLP)
                sb.AppendLine($"if exist temp_build\\CustomDownloadPlay.bin \"{tool3ds}\" -cvtf cfa temp_build\\CustomPartition2.bin --header HeaderNCCH2.bin --romfs temp_build\\CustomDownloadPlay.bin");
            if (hasN3DSUpdate)
                sb.AppendLine($"if exist temp_build\\CustomN3DSUpdate.bin \"{tool3ds}\" -cvtf cfa temp_build\\CustomPartition6.bin --header HeaderNCCH6.bin --romfs temp_build\\CustomN3DSUpdate.bin");
            if (hasO3DSUpdate)
                sb.AppendLine($"if exist temp_build\\CustomO3DSUpdate.bin \"{tool3ds}\" -cvtf cfa temp_build\\CustomPartition7.bin --header HeaderNCCH7.bin --romfs temp_build\\CustomO3DSUpdate.bin");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();

            sb.AppendLine("echo [5/5] Assembling final .3ds ROM...");
            string finalHeader = File.Exists(Path.Combine(gameParentDir, "HeaderNCCH.bin")) ? "HeaderNCCH.bin" : "HeaderNCCH0.bin";
            string mergeCmd = $"\"{tool3ds}\" -cvtf 3ds \"{outputPath}\" --not-pad --header {finalHeader} -0 temp_build\\CustomPartition0.bin";
            if (hasManual) mergeCmd += " -1 temp_build\\CustomPartition1.bin";
            if (hasDLP) mergeCmd += " -2 temp_build\\CustomPartition2.bin";
            if (hasN3DSUpdate) mergeCmd += " -6 temp_build\\CustomPartition6.bin";
            if (hasO3DSUpdate) mergeCmd += " -7 temp_build\\CustomPartition7.bin";
            sb.AppendLine(mergeCmd);
            sb.AppendLine($"if not exist \"{outputPath}\" goto ERROR");
        }
        else
        {
            sb.AppendLine("echo Fallback: Building 3DS ROM via Toolkit Console...");
            sb.AppendLine($"\"{toolConsole}\" \"{gameParentDir}\" \"{outputPath}\"");
            sb.AppendLine($"if not exist \"{outputPath}\" goto ERROR");
        }

        if (trim && tool3ds != null)
        {
            sb.AppendLine();
            sb.AppendLine("echo Trimming final .3ds ROM using 3dstool...");
            sb.AppendLine($"\"{tool3ds}\" -r -t cci -f \"{outputPath}\"");
            sb.AppendLine("if %errorlevel% neq 0 echo Warning: Trimming failed, keeping untrimmed ROM.");
        }

        sb.AppendLine("if exist temp_build rmdir /s /q temp_build >nul 2>&1");
        sb.AppendLine($"if exist \"{exefsDirName}\\.code.bin\" if exist \"{exefsDirName}\\code.bin\" del /f /q \"{exefsDirName}\\code.bin\" >nul 2>&1");
        sb.AppendLine($"if exist \"{exefsDirName}\\banner.bin\" if exist \"{exefsDirName}\\banner.bnr\" del /f /q \"{exefsDirName}\\banner.bnr\" >nul 2>&1");
        sb.AppendLine($"if exist \"{exefsDirName}\\icon.bin\" if exist \"{exefsDirName}\\icon.icn\" del /f /q \"{exefsDirName}\\icon.icn\" >nul 2>&1");
        sb.AppendLine();
        sb.AppendLine(":SUCCESS");
        sb.AppendLine("echo.");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine("echo SUCCESS: ROM rebuild finished cleanly!");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine($"echo SUCCESS > \"{sentinelDone}\"");
        sb.AppendLine("echo.");
        sb.AppendLine("echo You can close this window now.");
        sb.AppendLine("pause");
        sb.AppendLine("exit /b 0");
        sb.AppendLine();
        sb.AppendLine(":ERROR");
        sb.AppendLine("if exist temp_build rmdir /s /q temp_build >nul 2>&1");
        sb.AppendLine("echo.");
        sb.AppendLine("echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! ");
        sb.AppendLine("echo ERROR: ROM rebuilding failed. Check output above.");
        sb.AppendLine("echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! ");
        sb.AppendLine($"echo FAILED > \"{sentinelError}\"");
        sb.AppendLine("pause");
        sb.AppendLine("exit /b 1");

        File.WriteAllText(batPath, sb.ToString());

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
            Process.Start(psi);
            statusCallback?.Invoke("Launched external direct 3dstool script in CMD window. Waiting for completion...");
            return batPath;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"Failed to launch batch script: {ex.Message}");
            return null;
        }
    }

    public static string LaunchExternalRomFSBatchRebuild(string romfsFolder, string outputRomfsBin, out string sentinelDone, out string sentinelError, Action<string> statusCallback = null)
    {
        string tool3ds = FindTool("3dstool.exe");
        if (tool3ds == null)
        {
            statusCallback?.Invoke("3dstool.exe not found in tools directory.");
            sentinelDone = null;
            sentinelError = null;
            return null;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"pk3DS_romfs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        sentinelDone = Path.Combine(tempDir, "_rebuild_done.flag");
        sentinelError = Path.Combine(tempDir, "_rebuild_error.flag");
        string batPath = Path.Combine(tempDir, "rebuild_romfs.bat");

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("title pk3DS External RomFS Rebuilder");
        sb.AppendLine("color 0A");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine("echo      pk3DS - External RomFS Rebuilding Process");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine("echo.");
        sb.AppendLine("echo Building RomFS binary via 3dstool...");
        sb.AppendLine($"\"{tool3ds}\" -cvtf romfs \"{outputRomfsBin}\" --romfs-dir \"{romfsFolder}\"");
        sb.AppendLine("if %errorlevel% neq 0 goto ERROR");
        sb.AppendLine("echo.");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine("echo SUCCESS: RomFS binary rebuild finished!");
        sb.AppendLine("echo ========================================================");
        sb.AppendLine($"echo SUCCESS > \"{sentinelDone}\"");
        sb.AppendLine("echo.");
        sb.AppendLine("echo You can close this window now.");
        sb.AppendLine("pause");
        sb.AppendLine("exit /b 0");
        sb.AppendLine();
        sb.AppendLine(":ERROR");
        sb.AppendLine("echo.");
        sb.AppendLine("echo ERROR: RomFS rebuilding failed.");
        sb.AppendLine($"echo FAILED > \"{sentinelError}\"");
        sb.AppendLine("pause");
        sb.AppendLine("exit /b 1");

        File.WriteAllText(batPath, sb.ToString());

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
            Process.Start(psi);
            statusCallback?.Invoke("Launched external 3dstool RomFS script in CMD window. Waiting for completion...");
            return batPath;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"Failed to launch batch script: {ex.Message}");
            return null;
        }
    }

    private static bool RunProcess(string exePath, string args, Action<string> statusCallback)
    {
        try
        {
            statusCallback?.Invoke($"Executing: {Path.GetFileName(exePath)} {args}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) statusCallback?.Invoke(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) statusCallback?.Invoke(e.Data); };

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"Error executing {Path.GetFileName(exePath)}: {ex.Message}");
            return false;
        }
    }

    public static bool LaunchToolkitGUI(Action<string> statusCallback = null)
    {
        string toolkitForm = FindTool("ToolkitForm.exe");
        if (toolkitForm == null || !File.Exists(toolkitForm))
        {
            statusCallback?.Invoke("ToolkitForm.exe not found in tools directory.");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = toolkitForm,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(toolkitForm)
            });
            statusCallback?.Invoke("Launched DotNet 3DS Toolkit GUI.");
            return true;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"Failed to launch ToolkitForm.exe: {ex.Message}");
            return false;
        }
    }
}

