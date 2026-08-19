using System;
using System.Collections.Generic;
using System.IO;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding
{
    public static class CodeBinPatcher
    {
        /// <summary>
        /// Encodes a 32-bit ARM Branch (B) or Branch-Link (BL) instruction.
        /// </summary>
        public static uint SynthesizeBranch(uint fromVA, uint toVA, bool link = true)
        {
            // ARM Branch instruction calculation:
            // PC is current instruction + 8
            long delta = (long)toVA - ((long)fromVA + 8);
            long imm24 = delta >> 2;

            uint opcode = link ? 0xEB000000u : 0xEA000000u; // BL or B (Always condition 0xE)
            uint instruction = opcode | ((uint)imm24 & 0x00FFFFFFu);
            return instruction;
        }

        /// <summary>
        /// Injects a Trampoline Branch into code.bin and appends the custom ARM payload to the end of the binary.
        /// </summary>
        public static uint InjectTrampolinePayload(DecompiledCodeBin codeBin, uint hookVirtualAddress, byte[] payloadBytes, bool link = true)
        {
            uint hookFileOffset = hookVirtualAddress - codeBin.BaseVirtualAddress;
            if (hookFileOffset + 4 > codeBin.RawData.Length)
                throw new ArgumentOutOfRangeException(nameof(hookVirtualAddress), "Hook address is out of binary bounds.");

            // 1. Determine payload location at end of file (aligned to 4 bytes)
            byte[] raw = codeBin.RawData;
            uint payloadFileOffset = (uint)raw.Length;
            if (payloadFileOffset % 4 != 0)
            {
                int pad = 4 - (int)(payloadFileOffset % 4);
                Array.Resize(ref raw, (int)(payloadFileOffset + pad));
                payloadFileOffset = (uint)raw.Length;
            }

            uint payloadVA = codeBin.BaseVirtualAddress + payloadFileOffset;

            // 2. Synthesize Trampoline Branch instruction (B or BL)
            uint branchInst = SynthesizeBranch(hookVirtualAddress, payloadVA, link);
            byte[] branchBytes = BitConverter.GetBytes(branchInst);

            // 3. Overwrite hook site
            Array.Copy(branchBytes, 0, raw, hookFileOffset, 4);

            // 4. Append payload bytes to end of binary
            int oldLen = raw.Length;
            Array.Resize(ref raw, oldLen + payloadBytes.Length);
            Array.Copy(payloadBytes, 0, raw, oldLen, payloadBytes.Length);

            codeBin.RawData = raw;

            // Update metadata
            codeBin.TrampolineHooks.Add(hookVirtualAddress);
            return payloadVA;
        }

        /// <summary>
        /// Encodes assembly string into bytes and injects as a trampoline payload.
        /// </summary>
        public static uint InjectAssemblyPayload(DecompiledCodeBin codeBin, uint hookVirtualAddress, string assemblyText, bool link = true)
        {
            byte[] payloadBytes = ARMCodec.Assemble(assemblyText, hookVirtualAddress);
            return InjectTrampolinePayload(codeBin, hookVirtualAddress, payloadBytes, link);
        }

        /// <summary>
        /// Re-compiles DecompiledCodeBin into a final byte array.
        /// Re-compresses with LZSS if requested or if original binary was compressed.
        /// </summary>
        public static byte[] Compile(DecompiledCodeBin codeBin, bool compress = true)
        {
            byte[] data = codeBin.RawData;
            if (compress)
            {
                using var inStream = new MemoryStream(data);
                using var outStream = new MemoryStream();
                LZSS.Compress(inStream, data.Length, outStream, true);
                data = outStream.ToArray();
            }
            return data;
        }
    }
}
