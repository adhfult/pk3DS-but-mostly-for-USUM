using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.Core.CTR
{
    public class CodeBinSection
    {
        public string Name { get; set; }
        public uint VirtualAddress { get; set; }
        public uint FileOffset { get; set; }
        public uint Size { get; set; }
        public byte[] RawBytes { get; set; }
    }

    public class DecompiledCodeBin
    {
        public string FilePath { get; set; }
        public byte[] RawData { get; set; }
        public bool IsCompressed { get; set; }
        public uint BaseVirtualAddress { get; set; } = 0x00100000; // Standard 3DS main exefs base address

        public List<CodeBinSection> Sections { get; set; } = new();
        public List<uint> TrampolineHooks { get; set; } = new();
        public uint PayloadSectionVirtualAddress { get; set; }
        public uint PayloadSectionFileOffset { get; set; }
    }

    public static class CodeBinDecompiler
    {
        /// <summary>
        /// Decompiles code.bin binary into a structured DecompiledCodeBin model.
        /// Decompresses LZSS if compressed, and identifies virtual addresses.
        /// </summary>
        public static DecompiledCodeBin Decompile(byte[] data, string filePath = null)
        {
            var codeBin = new DecompiledCodeBin
            {
                FilePath = filePath,
                RawData = data
            };

            // 1. Check if compressed (LZSS format used in 3DS ExeFS code.bin)
            if (IsLZSSCompressed(data))
            {
                codeBin.IsCompressed = true;
                codeBin.RawData = LZSS.Decompress(data);
            }

            uint fileSize = (uint)codeBin.RawData.Length;

            // 2. Map standard 3DS sections
            // Default 3DS code.bin segment alignment: .text starts at offset 0x0 (VA 0x00100000)
            var textSection = new CodeBinSection
            {
                Name = ".text",
                FileOffset = 0,
                VirtualAddress = codeBin.BaseVirtualAddress,
                Size = fileSize,
                RawBytes = codeBin.RawData
            };
            codeBin.Sections.Add(textSection);

            // 3. Mark start of expansion payload section at EOF
            codeBin.PayloadSectionFileOffset = fileSize;
            codeBin.PayloadSectionVirtualAddress = codeBin.BaseVirtualAddress + fileSize;

            return codeBin;
        }

        private static bool IsLZSSCompressed(byte[] data)
        {
            if (data == null || data.Length < 8) return false;
            // Check for typical 3DS LZSS footer signature (magic footer at end of file)
            uint decompLen = BitConverter.ToUInt32(data, data.Length - 4);
            return decompLen > (uint)data.Length && decompLen < 0x04000000; // Less than 64MB
        }
    }
}
