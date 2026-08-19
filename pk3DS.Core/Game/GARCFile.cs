using System;
using System.IO;
using pk3DS.Core.CTR;

namespace pk3DS.Core;

public class GARCFile(GARC.MemGARC g, GARCReference r, string p)
{
    // Shorthand Alias
    public byte[] GetFile(int file, int subfile = 0) { return g.GetFile(file, subfile); }
    public byte[][] Files { get => g.Files; set => g.Files = value; }
    public int FileCount => g.FileCount;

    public void Save()
    {
        if (g.Files != null)
            g.Files = g.Files;
        if (g.Data != null && !string.IsNullOrEmpty(p))
        {
            File.WriteAllBytes(p, g.Data);
            Console.WriteLine($"Wrote {r.Name} to {r.Reference}");
        }
    }
}

public class LazyGARCFile(GARC.LazyGARC g, GARCReference r, string p)
{
    public int FileCount => g.FileCount;

    private byte[][] _cachedFiles;
    public byte[][] Files
    {
        get
        {
            if (_cachedFiles != null) return _cachedFiles;
            _cachedFiles = g.Files;
            return _cachedFiles;
        }
        set
        {
            for (int i = 0; i < value.Length; i++)
                g[i] = value[i];
            _cachedFiles = value;
        }
    }

    public byte[] this[int file]
    {
        get => g[file];
        set => g[file] = value;
    }

    public void Save()
    {
        g.Save(p);
        Console.WriteLine($"Wrote {r.Name} to {r.Reference}");
    }
}
