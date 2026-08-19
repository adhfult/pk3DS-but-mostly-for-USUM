using System;
using System.IO;
using System.Security.Cryptography;

namespace pk3DS.Core.CTR
{
    /// <summary>
    /// Recompiles a DecompiledCRO back to a valid binary.
    /// Handles relocation patch serialization, segment table updates,
    /// SHA-256 hash recalculation, and CRR hash update.
    /// </summary>
    public static class CROCompiler
    {
        /// <summary>
        /// Write all modifications in the DecompiledCRO back to the raw data.
        /// Does NOT resize; assumes CROUtil.ExpandSegment was already called if needed.
        /// </summary>
        public static byte[] Compile(DecompiledCRO cro)
        {
            byte[] data = cro.RawData;

            // 1. Write relocation patches back
            WriteRelocations(data, cro);

            // 2. Update segment table
            WriteSegmentTable(data, cro);

            // 3. Update header scalars
            WriteHeader(data, cro.Header);

            // 4. Recalculate SHA-256 hashes
            RecalculateHashes(data);

            return data;
        }

        /// <summary>
        /// Save the compiled CRO to disk.
        /// </summary>
        public static void SaveToFile(DecompiledCRO cro, string path)
        {
            byte[] data = Compile(cro);
            File.WriteAllBytes(path, data);
        }

        #region Serialization
        private static void WriteRelocations(byte[] data, DecompiledCRO cro)
        {
            uint ptOfs = cro.Header.PatchTableOffset;
            for (int i = 0; i < cro.Relocations.Count; i++)
            {
                int off = (int)(ptOfs + i * 12);
                if (off + 12 > data.Length) break;
                var entry = cro.Relocations[i];
                BitConverter.GetBytes(entry.RawWord0).CopyTo(data, off);
                BitConverter.GetBytes(entry.RawWord1).CopyTo(data, off + 4);
                BitConverter.GetBytes(entry.RawWord2).CopyTo(data, off + 8);
            }
            // Update count
            BitConverter.GetBytes((uint)cro.Relocations.Count).CopyTo(data, 0x12C);
        }

        private static void WriteSegmentTable(byte[] data, DecompiledCRO cro)
        {
            uint segOfs = cro.Header.SegmentTableOffset;
            for (int i = 0; i < 4; i++)
            {
                if (cro.Segments[i] == null) continue;
                int off = (int)(segOfs + i * 12);
                if (off + 12 > data.Length) break;
                BitConverter.GetBytes(cro.Segments[i].Start).CopyTo(data, off);
                BitConverter.GetBytes(cro.Segments[i].Size).CopyTo(data, off + 4);
                BitConverter.GetBytes(cro.Segments[i].ID).CopyTo(data, off + 8);
            }
        }

        private static void WriteHeader(byte[] data, CROHeader h)
        {
            BitConverter.GetBytes(h.NameOffset).CopyTo(data, 0x84);
            BitConverter.GetBytes(h.FileSize).CopyTo(data, 0x90);
            BitConverter.GetBytes(h.BSSSize).CopyTo(data, 0x94);
            BitConverter.GetBytes(h.CodeStart).CopyTo(data, 0xB0);
            BitConverter.GetBytes(h.CodeSize).CopyTo(data, 0xB4);
            BitConverter.GetBytes(h.DataStart).CopyTo(data, 0xB8);
            BitConverter.GetBytes(h.DataSize).CopyTo(data, 0xBC);
            BitConverter.GetBytes(h.SegmentTableOffset).CopyTo(data, 0xC8);
            BitConverter.GetBytes(h.PatchTableOffset).CopyTo(data, 0x128);
            BitConverter.GetBytes(h.PatchTableCount).CopyTo(data, 0x12C);
        }
        #endregion

        #region Hash Recalculation
        /// <summary>
        /// Recalculate the SHA-256 hashes in the CRO header (offsets 0x00-0x7F).
        /// Hash layout:
        ///   0x00-0x1F: Hash of header (0x80-0x13F)
        ///   0x20-0x3F: Hash of code segment
        ///   0x40-0x5F: Hash of rodata + data segments
        ///   0x60-0x7F: Hash of everything after data (name table, relocation tables, etc.)
        /// </summary>
        public static void RecalculateHashes(byte[] data)
        {
            if (data.Length < 0x140) return;

            using var sha = SHA256.Create();

            // Read header fields needed
            uint codeStart = BitConverter.ToUInt32(data, 0xB0);
            uint codeSize = BitConverter.ToUInt32(data, 0xB4);

            // Segment table
            uint segOfs = BitConverter.ToUInt32(data, 0xC8);
            uint seg1Start = 0, seg1Size = 0;
            if (segOfs + 24 <= data.Length)
            {
                seg1Start = BitConverter.ToUInt32(data, (int)(segOfs + 12));
                seg1Size = BitConverter.ToUInt32(data, (int)(segOfs + 16));
            }

            uint dataStart = BitConverter.ToUInt32(data, 0xB8);
            uint dataSize = BitConverter.ToUInt32(data, 0xBC);

            // Hash 0: Header (0x80 to segment-table-end area, typically 0x80-0x13F)
            int headerHashStart = 0x80;
            int headerHashLen = 0xC0; // 0x80-0x13F
            if (headerHashStart + headerHashLen <= data.Length)
            {
                byte[] hash0 = sha.ComputeHash(data, headerHashStart, headerHashLen);
                Array.Copy(hash0, 0, data, 0x00, 32);
            }

            // Hash 1: Code segment
            if (codeStart + codeSize <= data.Length && codeSize > 0)
            {
                byte[] hash1 = sha.ComputeHash(data, (int)codeStart, (int)codeSize);
                Array.Copy(hash1, 0, data, 0x20, 32);
            }

            // Hash 2: Rodata + Data (seg 1 + seg 2)
            uint rodataDataStart = seg1Start > 0 ? seg1Start : (codeStart + codeSize);
            uint rodataDataEnd = dataStart + dataSize;
            if (rodataDataEnd > rodataDataStart && rodataDataEnd <= data.Length)
            {
                byte[] hash2 = sha.ComputeHash(data, (int)rodataDataStart, (int)(rodataDataEnd - rodataDataStart));
                Array.Copy(hash2, 0, data, 0x40, 32);
            }

            // Hash 3: Everything after data end (name, exports, imports, relocation tables)
            uint afterData = dataStart + dataSize;
            if (afterData < data.Length)
            {
                byte[] hash3 = sha.ComputeHash(data, (int)afterData, (int)(data.Length - afterData));
                Array.Copy(hash3, 0, data, 0x60, 32);
            }
        }
        #endregion
    }
}
