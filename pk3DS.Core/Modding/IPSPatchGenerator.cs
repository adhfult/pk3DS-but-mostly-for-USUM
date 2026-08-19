using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace pk3DS.Core.Modding
{
    public static class IPSPatchGenerator
    {
        public static byte[] CreateIPS(IEnumerable<(uint Offset, byte[] Bytes)> patchEntries)
        {
            if (patchEntries == null || !patchEntries.Any())
                return Array.Empty<byte>();

            // Sort entries by offset and merge overlapping/contiguous entries
            var sorted = patchEntries
                .Where(e => e.Bytes != null && e.Bytes.Length > 0)
                .OrderBy(e => e.Offset)
                .ToList();

            var merged = MergeContiguous(sorted);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Write Header: "PATCH"
            bw.Write(Encoding.ASCII.GetBytes("PATCH"));

            foreach (var (offset, bytes) in merged)
            {
                uint curOffset = offset;
                int remaining = bytes.Length;
                int srcIdx = 0;

                while (remaining > 0)
                {
                    // Avoid exact offset matching ASCII "EOF" (0x454F46)
                    if (curOffset == 0x454F46)
                    {
                        // Write 1 byte chunk or handle edge case
                        bw.Write((byte)0x45);
                        bw.Write((byte)0x4F);
                        bw.Write((byte)0x46);
                        bw.Write((byte)0x00);
                        bw.Write((byte)0x01);
                        bw.Write(bytes[srcIdx]);

                        curOffset += 1;
                        srcIdx += 1;
                        remaining -= 1;
                        continue;
                    }

                    int chunkSize = Math.Min(remaining, 0xFFFF);

                    // Offset: 3-byte Big Endian
                    bw.Write((byte)((curOffset >> 16) & 0xFF));
                    bw.Write((byte)((curOffset >> 8) & 0xFF));
                    bw.Write((byte)(curOffset & 0xFF));

                    // Size: 2-byte Big Endian
                    bw.Write((byte)((chunkSize >> 8) & 0xFF));
                    bw.Write((byte)(chunkSize & 0xFF));

                    // Payload
                    bw.Write(bytes, srcIdx, chunkSize);

                    curOffset += (uint)chunkSize;
                    srcIdx += chunkSize;
                    remaining -= chunkSize;
                }
            }

            // Write Footer: "EOF"
            bw.Write(Encoding.ASCII.GetBytes("EOF"));

            return ms.ToArray();
        }

        private static List<(uint Offset, byte[] Bytes)> MergeContiguous(List<(uint Offset, byte[] Bytes)> entries)
        {
            if (entries.Count <= 1) return entries;

            var result = new List<(uint Offset, byte[] Bytes)>();
            uint curOffset = entries[0].Offset;
            var curBytes = new List<byte>(entries[0].Bytes);

            for (int i = 1; i < entries.Count; i++)
            {
                uint nextOffset = entries[i].Offset;
                byte[] nextBytes = entries[i].Bytes;

                if (nextOffset <= curOffset + curBytes.Count)
                {
                    // Contiguous or overlapping
                    int overlap = (int)(curOffset + curBytes.Count - nextOffset);
                    if (overlap < nextBytes.Length)
                    {
                        curBytes.AddRange(nextBytes.Skip(overlap));
                    }
                }
                else
                {
                    result.Add((curOffset, curBytes.ToArray()));
                    curOffset = nextOffset;
                    curBytes = new List<byte>(nextBytes);
                }
            }
            result.Add((curOffset, curBytes.ToArray()));

            return result;
        }

        /// <summary>
        /// Applies an IPS patch binary payload onto a target byte array (e.g. Battle.cro or code.bin).
        /// </summary>
        public static byte[] ApplyIPS(byte[] original, byte[] ipsData)
        {
            if (original == null || ipsData == null || ipsData.Length < 8)
                return original;

            string header = Encoding.ASCII.GetString(ipsData, 0, 5);
            if (header != "PATCH")
                throw new InvalidDataException("Invalid IPS header.");

            var buffer = (byte[])original.Clone();
            int idx = 5;

            while (idx < ipsData.Length - 3)
            {
                string eof = Encoding.ASCII.GetString(ipsData, idx, 3);
                if (eof == "EOF") break;

                if (idx + 5 > ipsData.Length) break;

                uint offset = (uint)((ipsData[idx] << 16) | (ipsData[idx + 1] << 8) | ipsData[idx + 2]);
                ushort size = (ushort)((ipsData[idx + 3] << 8) | ipsData[idx + 4]);
                idx += 5;

                if (size == 0) // RLE
                {
                    if (idx + 3 > ipsData.Length) break;
                    ushort rleSize = (ushort)((ipsData[idx] << 8) | ipsData[idx + 1]);
                    byte rleVal = ipsData[idx + 2];
                    idx += 3;

                    if (offset + rleSize > buffer.Length)
                    {
                        Array.Resize(ref buffer, (int)(offset + rleSize));
                    }

                    for (int i = 0; i < rleSize; i++)
                        buffer[offset + i] = rleVal;
                }
                else
                {
                    if (idx + size > ipsData.Length) break;

                    if (offset + size > buffer.Length)
                    {
                        Array.Resize(ref buffer, (int)(offset + size));
                    }

                    Array.Copy(ipsData, idx, buffer, offset, size);
                    idx += size;
                }
            }

            return buffer;
        }
    }
}
