using System;
using System.IO;
using System.Security.Cryptography;

namespace pk3DS.Core.CTR;

/// <summary>
/// Repairs the RomFS superblock hash that 3dstool records in an NCCH header.
/// </summary>
public static class NcchRomFsHash
{
    private const int MediaUnit = 0x200;

    // NCCH header field offsets, relative to the start of the partition.
    private const int RomFsOffsetField = 0x1B0;
    private const int RomFsSizeField = 0x1B4;
    private const int RomFsHashRegionField = 0x1B8;
    private const int RomFsSuperblockHashField = 0x1E0;

    /// <summary>A CCI keeps a copy of partition 0's NCCH header here, which needs the same repair.</summary>
    private const long CciEmbeddedHeaderCopy = 0x1000;

    public sealed record Result(bool Changed, string Message)
    {
        public static Result No(string why) => new(false, why);
    }

    /// <summary>Repairs a .3ds/.cci or a bare .cxi in place.</summary>
    public static Result Fix(string romPath)
    {
        try
        {
            using var fs = new FileStream(romPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            long partition = FindFirstPartition(fs, out bool isCci);
            if (partition < 0) return Result.No("not an NCSD or NCCH image; nothing to repair");

            uint romfsOffMU = ReadU32(fs, partition + RomFsOffsetField);
            uint romfsSizeMU = ReadU32(fs, partition + RomFsSizeField);
            uint storedRegionMU = ReadU32(fs, partition + RomFsHashRegionField);
            if (romfsOffMU == 0 || romfsSizeMU == 0) return Result.No("partition has no RomFS");

            long romfs = partition + ((long)romfsOffMU * MediaUnit);

            int correctRegion = HashRegionBytes(fs, romfs);
            if (correctRegion <= 0) return Result.No("RomFS has no readable IVFC header");
            uint correctRegionMU = (uint)(correctRegion / MediaUnit);

            byte[] region = Read(fs, romfs, correctRegion);
            byte[] hash = SHA256.HashData(region);

            byte[] storedHash = Read(fs, partition + RomFsSuperblockHashField, 0x20);
            bool sizeOk = storedRegionMU == correctRegionMU;
            bool hashOk = storedHash.AsSpan().SequenceEqual(hash);
            if (sizeOk && hashOk)
                return Result.No($"RomFS hash already correct ({correctRegionMU} media unit(s))");

            WriteU32(fs, partition + RomFsHashRegionField, correctRegionMU);
            Write(fs, partition + RomFsSuperblockHashField, hash);

            // Keep the CCI's embedded copy of the header in step with the real one.
            if (isCci && partition != CciEmbeddedHeaderCopy)
            {
                if (ReadU32(fs, CciEmbeddedHeaderCopy + RomFsOffsetField) == romfsOffMU)
                {
                    WriteU32(fs, CciEmbeddedHeaderCopy + RomFsHashRegionField, correctRegionMU);
                    Write(fs, CciEmbeddedHeaderCopy + RomFsSuperblockHashField, hash);
                }
            }

            fs.Flush();
            return new Result(true,
                $"RomFS hash region {storedRegionMU} -> {correctRegionMU} media unit(s); superblock hash recomputed over 0x{correctRegion:X} bytes");
        }
        catch (Exception ex)
        {
            return Result.No($"could not repair the RomFS hash ({ex.GetType().Name}: {ex.Message})");
        }
    }

    /// <summary>
    /// How many bytes the RomFS superblock hash must cover, from the IVFC header itself.
    /// </summary>
    private const int IvfcHeaderSize = 0x60;

    private static int HashRegionBytes(FileStream fs, long romfs)
    {
        byte[] ivfc = Read(fs, romfs, IvfcHeaderSize);
        if (ivfc[0] != (byte)'I' || ivfc[1] != (byte)'V' || ivfc[2] != (byte)'F' || ivfc[3] != (byte)'C')
            return -1;

        uint masterHashSize = BitConverter.ToUInt32(ivfc, 0x08);
        if (masterHashSize is 0 or > 0x10000) return -1;

        long total = IvfcHeaderSize + masterHashSize;
        long aligned = (total + MediaUnit - 1) / MediaUnit * MediaUnit;
        return aligned is > 0 and <= 0x10000 ? (int)aligned : -1;
    }

    /// <summary>Offset of the first NCCH partition, whether the file is a CCI or a bare CXI.</summary>
    private static long FindFirstPartition(FileStream fs, out bool isCci)
    {
        isCci = false;
        byte[] magic = Read(fs, 0x100, 4);
        string m = System.Text.Encoding.ASCII.GetString(magic);

        if (m == "NCCH") return 0;
        if (m != "NCSD") return -1;

        isCci = true;
        // Partition table at 0x120: eight (offset, size) pairs in media units.
        for (int i = 0; i < 8; i++)
        {
            uint off = ReadU32(fs, 0x120 + (i * 8));
            uint size = ReadU32(fs, 0x124 + (i * 8));
            if (off == 0 || size == 0) continue;
            long p = (long)off * MediaUnit;
            if (System.Text.Encoding.ASCII.GetString(Read(fs, p + 0x100, 4)) == "NCCH")
                return p;
        }
        return -1;
    }

    private static byte[] Read(FileStream fs, long offset, int length)
    {
        fs.Position = offset;
        byte[] b = new byte[length];
        int done = 0;
        while (done < length)
        {
            int n = fs.Read(b, done, length - done);
            if (n <= 0) break;
            done += n;
        }
        return b;
    }

    private static void Write(FileStream fs, long offset, byte[] data)
    {
        fs.Position = offset;
        fs.Write(data, 0, data.Length);
    }

    private static uint ReadU32(FileStream fs, long offset) => BitConverter.ToUInt32(Read(fs, offset, 4), 0);

    private static void WriteU32(FileStream fs, long offset, uint value) => Write(fs, offset, BitConverter.GetBytes(value));
}
