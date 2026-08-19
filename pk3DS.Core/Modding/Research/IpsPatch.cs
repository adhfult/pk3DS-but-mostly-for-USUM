#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace pk3DS.Core.Modding.Research;

/// <summary>One write recorded by an IPS patch.</summary>
public readonly record struct IpsRecord(int Offset, byte[] Bytes, bool Rle)
{
    public int End => Offset + Bytes.Length;
}

/// <summary>
/// Reads and applies IPS patches, the format the community's loose code.bin patches ship in.
/// </summary>
public static class IpsPatch
{
    /// <summary>Largest offset a 24-bit IPS record can address.</summary>
    public const int MaxOffset = 0xFFFFFF;

    private static readonly byte[] Magic = "PATCH"u8.ToArray();
    private static readonly byte[] Eof = "EOF"u8.ToArray();

    /// <summary>Parses a patch file into its records. Throws on a malformed file.</summary>
    public static List<IpsRecord> Read(byte[] patch)
    {
        if (patch == null || patch.Length < 8) throw new InvalidDataException("not an IPS file (too short)");
        if (!patch.Take(5).SequenceEqual(Magic)) throw new InvalidDataException("not an IPS file (no PATCH header)");

        var records = new List<IpsRecord>();
        int i = 5;
        while (i + 3 <= patch.Length)
        {
            if (patch[i] == Eof[0] && i + 3 <= patch.Length &&
                patch[i + 1] == Eof[1] && patch[i + 2] == Eof[2])
                return records;

            if (i + 5 > patch.Length) throw new InvalidDataException("truncated record header");
            int offset = (patch[i] << 16) | (patch[i + 1] << 8) | patch[i + 2];
            int length = (patch[i + 3] << 8) | patch[i + 4];
            i += 5;

            if (length == 0)
            {
                if (i + 3 > patch.Length) throw new InvalidDataException("truncated RLE record");
                int run = (patch[i] << 8) | patch[i + 1];
                byte value = patch[i + 2];
                i += 3;
                records.Add(new IpsRecord(offset, Enumerable.Repeat(value, run).ToArray(), true));
                continue;
            }

            if (i + length > patch.Length) throw new InvalidDataException("truncated data record");
            records.Add(new IpsRecord(offset, patch.Skip(i).Take(length).ToArray(), false));
            i += length;
        }
        throw new InvalidDataException("no EOF marker");
    }

    /// <summary>Applies records to <paramref name="target"/> in place. Returns bytes written.</summary>
    public static int Apply(byte[] target, IEnumerable<IpsRecord> records)
    {
        int written = 0;
        foreach (var r in records)
        {
            if (r.Offset < 0 || r.End > target.Length)
                throw new InvalidDataException(
                    $"record at 0x{r.Offset:X6} (+{r.Bytes.Length}) runs past the end of a " +
                    $"{target.Length:N0}-byte file - this patch is for a different build");
            r.Bytes.CopyTo(target, r.Offset);
            written += r.Bytes.Length;
        }
        return written;
    }

    /// <summary>
    /// Addresses more than one selected patch writes to, which is the one thing worth knowing
    /// before applying several at once.
    /// </summary>
    public static List<(int Offset, string[] Patches)> FindConflicts(
        IReadOnlyList<(string Name, List<IpsRecord> Records)> patches)
    {
        var owners = new Dictionary<int, List<string>>();
        foreach (var (name, records) in patches)
            foreach (var r in records)
                for (int a = r.Offset; a < r.End; a++)
                {
                    if (!owners.TryGetValue(a, out var l)) owners[a] = l = [];
                    if (!l.Contains(name)) l.Add(name);
                }

        // Report one entry per contiguous clashing run rather than one per byte.
        var clashes = owners.Where(kv => kv.Value.Count > 1).OrderBy(kv => kv.Key).ToList();
        var result = new List<(int, string[])>();
        int last = int.MinValue;
        string lastKey = "";
        foreach (var kv in clashes)
        {
            string key = string.Join("|", kv.Value);
            if (kv.Key == last + 1 && key == lastKey) { last = kv.Key; continue; }
            result.Add((kv.Key, kv.Value.ToArray()));
            last = kv.Key;
            lastKey = key;
        }
        return result;
    }

    /// <summary>A short description of what a patch touches, for the detail pane.</summary>
    public static string Describe(IReadOnlyList<IpsRecord> records)
    {
        if (records.Count == 0) return "no records";
        int bytes = records.Sum(r => r.Bytes.Length);
        int lo = records.Min(r => r.Offset), hi = records.Max(r => r.End);
        int rle = records.Count(r => r.Rle);
        return $"{records.Count} record(s), {bytes} byte(s), 0x{lo:X6}..0x{hi:X6}" +
               (rle > 0 ? $", {rle} run-length" : "");
    }
}
