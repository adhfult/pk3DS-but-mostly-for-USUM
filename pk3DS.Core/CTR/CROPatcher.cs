using System;
using System.IO;
using System.Linq;
using pk3DS.Core.Modding;

namespace pk3DS.Core.CTR
{
    public static class CROPatcher
    {
        /// <summary>
        /// Applies an IPS patch payload to a CRO file (e.g., Battle.cro).
        /// Handles expansion if required and updates CRO hashes.
        /// </summary>
        public static byte[] PatchCRO(byte[] originalCro, byte[] ipsPatch, bool isExpansionPack = false)
        {
            if (originalCro == null || ipsPatch == null || ipsPatch.Length < 8)
                return originalCro;

            byte[] cro = (byte[])originalCro.Clone();

            // If USUM Expansion Pack is selected and file hasn't been expanded yet
            if (isExpansionPack && cro.Length <= 1425408)
            {
                // Expand .code segment 'c' by 4000 bytes for Expansion Pack additions
                cro = CROUtil.ExpandSegment(cro, 'c', 4000);
            }

            // Apply IPS patch
            cro = IPSPatchGenerator.ApplyIPS(cro, ipsPatch);

            // Re-hash CRO headers
            CROUtil.UpdateHashes(cro);

            return cro;
        }

        /// <summary>
        /// Injects raw hex bytes directly into a CRO file at a specific offset.
        /// </summary>
        public static byte[] InjectHexPayload(byte[] originalCro, uint offset, byte[] payload)
        {
            if (originalCro == null || payload == null || payload.Length == 0) return originalCro;
            if (offset + payload.Length > originalCro.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Payload exceeds CRO file bounds.");

            byte[] cro = (byte[])originalCro.Clone();
            Array.Copy(payload, 0, cro, offset, payload.Length);
            
            // Re-hash CRO headers
            CROUtil.UpdateHashes(cro);
            return cro;
        }

        /// <summary>
        /// Assembles an ARM string and injects the resulting bytecode directly into a CRO file.
        /// </summary>
        public static byte[] InjectAssemblyPayload(byte[] originalCro, uint offset, uint virtualAddress, string assemblyText)
        {
            byte[] payload = ARMCodec.Assemble(assemblyText, virtualAddress);
            return InjectHexPayload(originalCro, offset, payload);
        }

        /// <summary>
        /// Patches a CRO file on disk and writes the updated binary.
        /// </summary>
        public static bool PatchFile(string croPath, string ipsPath, string outputPath = null, bool isExpansionPack = false)
        {
            if (!File.Exists(croPath) || !File.Exists(ipsPath))
                return false;

            try
            {
                byte[] cro = File.ReadAllBytes(croPath);
                byte[] ips = File.ReadAllBytes(ipsPath);

                byte[] patched = PatchCRO(cro, ips, isExpansionPack);

                string target = outputPath ?? croPath;
                return pk3DS.Core.Modding.BinaryWriteGuard.TryWrite(target, patched,
                    "Apply an IPS patch to a CRO module",
                    $"Source patch: {System.IO.Path.GetFileName(ipsPath)}"
                    + (isExpansionPack ? " (Expansion Pack offsets)." : "."));
            }
            catch
            {
                return false;
            }
        }
    }
}
