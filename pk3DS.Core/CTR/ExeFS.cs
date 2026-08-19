using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace pk3DS.Core.CTR;

public class ExeFS
{
    public byte[] Data;
    public readonly byte[] SuperBlockHash;

    // Return an object with data stored in a byte array
    public ExeFS(string path)
    {
        if (Directory.Exists(path))
        {
            var files = new DirectoryInfo(path).GetFiles()
                .Where(f => IsExeFSSection(f.Name))
                .Select(f => f.FullName).ToArray();
            SetData(files);
        }
        else if (File.Exists(path))
        {
            Data = File.ReadAllBytes(path);
        }
        else
        {
            throw new FileNotFoundException("File not found.", path);
        }
        SuperBlockHash = SHA256.HashData(Data.AsSpan(0, 200));
    }

    /// <summary>The ExeFS code segment, under any of the names this program gives it.</summary>
    public static bool IsCodeSection(string path)
    {
        string n = Path.GetFileName(path);
        return n.Equals(".code.bin", StringComparison.OrdinalIgnoreCase)
            || n.Equals("code.bin", StringComparison.OrdinalIgnoreCase)
            || n.Equals(".code", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The ExeFS code image on disk, under whichever of its two names is actually there.
    /// </summary>
    public static string ResolveCodeBin(string exefsFolder)
    {
        string canonical = Path.Combine(exefsFolder ?? "", ".code.bin");
        string plain = Path.Combine(exefsFolder ?? "", "code.bin");

        bool haveCanonical = File.Exists(canonical);
        bool havePlain = File.Exists(plain);

        if (haveCanonical && !havePlain) return canonical;
        if (havePlain && !haveCanonical) return plain;
        if (!haveCanonical) return canonical; // neither exists; canonical name for creation

        var ci = new FileInfo(canonical);
        var pi = new FileInfo(plain);
        if (ci.Length == pi.Length && FilesMatch(canonical, plain)) return canonical;
        return pi.LastWriteTimeUtc > ci.LastWriteTimeUtc ? plain : canonical;
    }

    /// <summary>Byte comparison used only to decide whether two code images are the same file.</summary>
    private static bool FilesMatch(string a, string b)
    {
        try
        {
            using var fa = File.OpenRead(a);
            using var fb = File.OpenRead(b);
            var ba = new byte[64 * 1024];
            var bb = new byte[64 * 1024];
            while (true)
            {
                int na = fa.Read(ba, 0, ba.Length);
                int nb = fb.Read(bb, 0, bb.Length);
                if (na != nb) return false;
                if (na == 0) return true;
                if (!ba.AsSpan(0, na).SequenceEqual(bb.AsSpan(0, nb))) return false;
            }
        }
        catch { return false; }
    }

    /// <summary>Whether a file in an ExeFS folder is one of its sections.</summary>
    private static bool IsExeFSSection(string name) =>
        !name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
        !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
        !name.Equals("Header.bin", StringComparison.OrdinalIgnoreCase) &&
        (!name.StartsWith(".") || IsCodeSection(name));

    /// <summary>
    /// Sections in packing order: the code segment first, and only once.
    /// </summary>
    private static string[] OrderSections(string[] files)
    {
        string code = files.FirstOrDefault(f => Path.GetFileName(f).Equals(".code.bin", StringComparison.OrdinalIgnoreCase))
                   ?? files.FirstOrDefault(IsCodeSection);
        var rest = files.Where(f => !IsCodeSection(f)).ToArray();
        if (code == null) return rest;

        var ordered = new string[1 + rest.Length];
        ordered[0] = code;
        Array.Copy(rest, 0, ordered, 1, rest.Length);
        return ordered;
    }

    // Overall R/W files (wrapped)
    public static bool UnpackExeFS(string inFile, string outPath)
    {
        try
        {
            byte[] data = File.ReadAllBytes(inFile);
            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);
            for (int i = 0; i < 10; i++)
            {
                // Get File Name String; if exists we have a file to extract.
                string fileName = Encoding.ASCII.GetString(data.Skip(0x10 * i).Take(0x8).ToArray()).TrimEnd((char)0);
                if (fileName.Length > 0)
                {
                    File.WriteAllBytes(
                        // New File Path
                        outPath + Path.DirectorySeparatorChar + fileName + ".bin",
                        // Get New Data from Offset after 0x200 Header.
                        data.Skip(0x200 + BitConverter.ToInt32(data, 0x8 + (0x10 * i))).Take(BitConverter.ToInt32(data, 0xC + (0x10 * i))).ToArray()
                    );
                }
            }
            return true;
        }
        catch { return false; }
    }

    public static string[] GetExeFSFiles(string path)
    {
        return OrderSections(new DirectoryInfo(path).GetFiles()
            .Where(f => IsExeFSSection(f.Name))
            .Select(f => f.FullName).ToArray());
    }

    public static bool PackExeFS(string[] files, string outFile)
    {
        files = files.Where(f => IsExeFSSection(Path.GetFileName(f))).ToArray();

        // .code is always index 0, and appears once however many names it goes by on disk.
        files = OrderSections(files);

        if (files.Length > 10) { Console.WriteLine("Cannot package more than 10 files to exefs."); return false; }

        try
        {
            // Set up the Header
            byte[] headerData = new byte[0x200];
            uint offset = 0;

            // Get the Header
            for (int i = 0; i < files.Length; i++)
            {
                // Do the Top (File Info)
                string fileName = Path.GetFileNameWithoutExtension(files[i]);
                if (fileName.Equals("code", StringComparison.OrdinalIgnoreCase)) fileName = ".code";
                byte[] nameData = Encoding.ASCII.GetBytes(fileName); Array.Resize(ref nameData, 0x8);
                Array.Copy(nameData, 0, headerData, i * 0x10, 0x8);

                var fi = new FileInfo(files[i]);
                uint size = (uint)fi.Length;
                Array.Copy(BitConverter.GetBytes(offset), 0, headerData, 0x8 + (i * 0x10), 0x4);
                Array.Copy(BitConverter.GetBytes(size), 0, headerData, 0xC + (i * 0x10), 0x4);
                offset += 0x200 - (size % 0x200) + size;

                // Do the Bottom (Hashes)
                // UPR-ZX: The first file's hash is at the bottom (0x1E0), second is at 0x1C0, etc.
                byte[] hash = SHA256.HashData(File.ReadAllBytes(files[i]));
                Array.Copy(hash, 0, headerData, 0x200 - (0x20 * (i + 1)), 0x20);
            }

            // Set in the Data
            using var newFile = new MemoryStream();
            newFile.Write(headerData);
            foreach (string s in files)
            {
                using (var loadFile = new MemoryStream(File.ReadAllBytes(s)))
                    loadFile.CopyTo(newFile);
                var tail = new byte[0x200 - (newFile.Length % 0x200)];
                newFile.Write(tail);
            }

            File.WriteAllBytes(outFile, newFile.ToArray());
            return true;
        }
        catch { return false; }
    }

    public void SetData(string[] files)
    {
        // .code is always index 0, and appears once however many names it goes by on disk.
        files = OrderSections(files);

        // Set up the Header
        byte[] headerData = new byte[0x200];
        uint offset = 0;

        // Get the Header
        for (int i = 0; i < files.Length; i++)
        {
            // Do the Top (File Info)
            string fileName = IsCodeSection(files[i]) ? ".code" : Path.GetFileNameWithoutExtension(files[i]);
            byte[] nameData = Encoding.ASCII.GetBytes(fileName); Array.Resize(ref nameData, 0x8);
            Array.Copy(nameData, 0, headerData, i * 0x10, 0x8);

            var fi = new FileInfo(files[i]);
            uint size = (uint)fi.Length;
            Array.Copy(BitConverter.GetBytes(offset), 0, headerData, 0x8 + (i * 0x10), 0x4);
            Array.Copy(BitConverter.GetBytes(size), 0, headerData, 0xC + (i * 0x10), 0x4);
            offset += 0x200 - (size % 0x200) + size;

            // Do the Bottom (Hashes)
            byte[] hash = SHA256.HashData(File.ReadAllBytes(files[i]));
            Array.Copy(hash, 0, headerData, 0x200 - (0x20 * (i + 1)), 0x20);
        }

        // Set in the Data
        using var newFile = new MemoryStream();
        newFile.Write(headerData);
        foreach (string s in files)
        {
            using var loadFile = File.OpenRead(s);
            loadFile.CopyTo(newFile);
            var tail = new byte[0x200 - (newFile.Length % 0x200)];
            newFile.Write(tail);
        }

        Data = newFile.ToArray();
    }
}
