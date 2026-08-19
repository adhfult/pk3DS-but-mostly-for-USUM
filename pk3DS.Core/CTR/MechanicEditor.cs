using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.Core.CTR
{
    /// <summary>
    /// High-level mechanic editing operations: add/edit/delete functions in move/ability/item tables.
    /// Implements the five operations from the ARM Research guide:
    ///   I.   Setup — Expansion and Preparation
    ///   II.  Finding Relevant Patches
    ///   III. Adding Functions to Existing Mechanics
    ///   IV.  Adding Entirely New Mechanics
    ///   V.   BSS Allocation
    /// </summary>
    public static class MechanicEditor
    {
        #region I. Setup — Expansion & Preparation

        /// <summary>
        /// Ensure the CRO has enough free space in the specified segment.
        /// Creates a backup before expanding.
        /// </summary>
        public static byte[] EnsureExpanded(byte[] data, string filePath, char segment, int bytesToAdd)
        {
            if (!string.IsNullOrEmpty(filePath))
                CROBackup.CreateBackup(filePath);

            return CROUtil.ExpandSegment(data, segment, bytesToAdd);
        }

        /// <summary>
        /// Ensure there are enough empty slots in the relocation patch table.
        /// If needed, expands the file to accommodate more entries.
        /// </summary>
        public static byte[] EnsureRelocationCapacity(byte[] data, int extraEntries)
        {
            uint ptOfs = BitConverter.ToUInt32(data, 0x128);
            uint ptCnt = BitConverter.ToUInt32(data, 0x12C);
            uint ptEnd = ptOfs + ptCnt * 12;

            // Check if there are 0xCC-filled slots at the end
            int freeSlots = 0;
            for (uint i = ptEnd; i + 12 <= data.Length; i += 12)
            {
                bool isFree = true;
                for (int j = 0; j < 12; j++)
                {
                    if (data[i + j] != 0xCC && data[i + j] != 0x00) { isFree = false; break; }
                }
                if (!isFree) break;
                freeSlots++;
            }

            if (freeSlots >= extraEntries) return data;

            // Need to expand. The patch table is typically at the end of the file,
            // so we can simply resize the file.
            int needed = (extraEntries - freeSlots) * 12;
            int newSize = data.Length + needed;
            // Align to 0x1000 boundary
            newSize = (newSize + 0xFFF) & ~0xFFF;

            Array.Resize(ref data, newSize);
            // Fill new space with 0xCC
            for (int i = data.Length - needed; i < data.Length; i++)
                data[i] = 0xCC;

            // Update file size in header
            BitConverter.GetBytes((uint)newSize).CopyTo(data, 0x90);

            return data;
        }

        #endregion

        #region II. Finding Relevant Patches (Relocation Chain Tracing)

        /// <summary>
        /// Complete relocation chain for a mechanic entry.
        /// </summary>
        public class RelocationChain
        {
            public int MechanicIndex;
            public MechanicType Type;

            // Level 1: Master table entry → call function pointer
            public int MasterToCallFuncRPT = -1;
            public uint CallFunctionOffset;

            // Level 2: Call function → timing table pointer
            public int CallFuncToTimingRPT = -1;
            public uint TimingTableOffset;

            // Level 3: Each timing entry → actual function pointer
            public List<(int RPTIndex, byte TimingByte, uint FunctionOffset)> TimingToFunctionRPTs = new();

            public bool IsValid => MasterToCallFuncRPT >= 0;
            public int TotalRelocations => 1 + (CallFuncToTimingRPT >= 0 ? 1 : 0) + TimingToFunctionRPTs.Count;
        }

        /// <summary>
        /// Trace the complete relocation chain for a mechanic entry.
        /// </summary>
        public static RelocationChain TraceRelocationChain(DecompiledCRO cro, int mechanicIndex, MechanicType type)
        {
            var chain = new RelocationChain { MechanicIndex = mechanicIndex, Type = type };

            MechanicTable table = type switch
            {
                MechanicType.Move => cro.MoveTable,
                MechanicType.Ability => cro.AbilityTable,
                MechanicType.Item => cro.ItemTable,
                _ => null
            };

            if (table == null || mechanicIndex >= table.Entries.Count) return chain;

            var entry = table.Entries[mechanicIndex];
            if (entry.CallFunc != null)
            {
                chain.MasterToCallFuncRPT = entry.CallFunc.RelocationIndex;
                chain.CallFunctionOffset = entry.CallFunc.Offset;
            }
            if (entry.Timings != null)
            {
                chain.CallFuncToTimingRPT = entry.Timings.RelocationIndex;
                chain.TimingTableOffset = entry.Timings.Offset;
                foreach (var te in entry.Timings.Entries)
                {
                    if (te.RelocationIndex >= 0)
                    {
                        chain.TimingToFunctionRPTs.Add((te.RelocationIndex, te.TimingByte, te.ResolvedFunctionOffset));
                    }
                }
            }

            return chain;
        }

        #endregion

        #region III. Adding Functions to Existing Mechanics

        /// <summary>
        /// Add a new timing function to an existing mechanic (move, ability, or item).
        /// This is the core "add a function to an existing move" operation.
        /// </summary>
        public static bool AddFunctionToMechanic(
            DecompiledCRO cro,
            int mechanicIndex,
            MechanicType type,
            byte timingByte,
            byte[] functionCode,
            Action<string> log = null)
        {
            log ??= _ => { };
            byte[] data = cro.RawData;
            var starts = cro.SegmentStarts;

            MechanicTable table = type switch
            {
                MechanicType.Move => cro.MoveTable,
                MechanicType.Ability => cro.AbilityTable,
                MechanicType.Item => cro.ItemTable,
                _ => null
            };

            if (table == null || mechanicIndex >= table.Entries.Count)
            {
                log($"Invalid mechanic index {mechanicIndex} for {type}");
                return false;
            }

            var entry = table.Entries[mechanicIndex];
            if (entry.CallFunc == null || entry.Timings == null)
            {
                log($"{type} #{mechanicIndex} has no call function or timing table");
                return false;
            }

            // 1. Write the new function code to free space in .code
            uint funcOffset = cro.FindFreeCodeSpace(functionCode.Length + 16);
            if (funcOffset == 0)
            {
                log("No free code space. Expand the CRO first.");
                return false;
            }
            Array.Copy(functionCode, 0, data, (int)funcOffset, functionCode.Length);
            log($"  Wrote {functionCode.Length} bytes of function code at 0x{funcOffset:X}");

            // 2. Create new timing table with extra entry
            var oldTimings = entry.Timings;
            int oldCount = oldTimings.Entries.Count;
            int newCount = oldCount + 1;
            int newTableSize = newCount * 8;

            // Find space in .data or .rodata for the new timing table
            uint newTimingOfs = cro.FindFreeDataSpace(newTableSize + 16);
            if (newTimingOfs == 0)
            {
                // Try code segment as fallback (timing tables can live anywhere)
                newTimingOfs = cro.FindFreeCodeSpace(newTableSize + 16);
                if (newTimingOfs == 0)
                {
                    log("No free space for new timing table.");
                    return false;
                }
            }

            // 3. Copy existing timing entries to new location
            for (int i = 0; i < oldCount; i++)
            {
                uint destOfs = newTimingOfs + (uint)(i * 8);
                var te = oldTimings.Entries[i];
                data[(int)destOfs] = te.TimingByte;
                data[(int)destOfs + 1] = te.Reserved[0];
                data[(int)destOfs + 2] = te.Reserved[1];
                data[(int)destOfs + 3] = te.Reserved[2];
                // Function pointer slot — will be resolved by relocation
                BitConverter.GetBytes(te.FunctionPointerSlot).CopyTo(data, (int)(destOfs + 4));
            }

            // 4. Append new timing entry
            uint newEntryOfs = newTimingOfs + (uint)(oldCount * 8);
            data[(int)newEntryOfs] = timingByte;
            data[(int)newEntryOfs + 1] = 0;
            data[(int)newEntryOfs + 2] = 0;
            data[(int)newEntryOfs + 3] = 0;
            // Function pointer placeholder
            BitConverter.GetBytes((uint)0).CopyTo(data, (int)(newEntryOfs + 4));

            log($"  Created new timing table at 0x{newTimingOfs:X} ({newCount} entries)");

            // 5. Create relocation patch for the new function pointer
            int funcSeg = CROUtil.GetSegmentForAddress(funcOffset, data);
            uint funcAddend = funcOffset - starts[funcSeg];
            // Determine what segment the new timing entry function pointer is in
            int timingSeg = CROUtil.GetSegmentForAddress(newEntryOfs + 4, data);
            uint timingWriteOff = (newEntryOfs + 4) - starts[timingSeg];

            var newRpt = CRORelocationEntry.Create(timingSeg, timingWriteOff, funcSeg, funcAddend);
            newRpt.Index = cro.Relocations.Count;
            cro.Relocations.Add(newRpt);

            // 6. Update existing relocation patches for moved timing entries
            for (int i = 0; i < oldCount; i++)
            {
                var te = oldTimings.Entries[i];
                if (te.RelocationIndex >= 0 && te.RelocationIndex < cro.Relocations.Count)
                {
                    // The old RPT wrote to oldTimings.Offset + i*8 + 4
                    // Now it needs to write to newTimingOfs + i*8 + 4
                    uint newWriteTo = newTimingOfs + (uint)(i * 8) + 4;
                    int wSeg = CROUtil.GetSegmentForAddress(newWriteTo, data);
                    uint wOff = newWriteTo - starts[wSeg];

                    var rpt = cro.Relocations[te.RelocationIndex];
                    rpt.RawWord0 = (wOff << 4) | (uint)(wSeg & 0xF);
                }
            }

            // 7. Update the call function's timing table pointer to point to new table
            if (oldTimings.RelocationIndex >= 0 && oldTimings.RelocationIndex < cro.Relocations.Count)
            {
                int tblSeg = CROUtil.GetSegmentForAddress(newTimingOfs, data);
                uint tblAddend = newTimingOfs - starts[tblSeg];
                cro.Relocations[oldTimings.RelocationIndex].Addend = tblAddend;
                cro.Relocations[oldTimings.RelocationIndex].RawWord1 =
                    (uint)(cro.Relocations[oldTimings.RelocationIndex].PatchType | (tblSeg << 8));
            }

            // 8. Update the call function's entry count
            // Patch the MOV/CMP instruction in the call function that holds the count
            PatchFunctionCount(data, entry.CallFunc, newCount);

            // 9. Update the patch table count in header
            cro.Header.PatchTableCount = (uint)cro.Relocations.Count;

            // 10. Fill old timing table location with 0xCC
            for (int i = 0; i < oldCount * 8; i++)
            {
                int oldLoc = (int)(oldTimings.Offset + i);
                if (oldLoc < data.Length) data[oldLoc] = 0xCC;
            }

            log($"  Added timing entry (timing=0x{timingByte:X2}) → function at 0x{funcOffset:X}");
            return true;
        }

        private static void PatchFunctionCount(byte[] data, CallFunction cf, int newCount)
        {
            // Find the MOV Rx, #oldCount or CMP Rx, #oldCount in the call function
            for (int i = 0; i < cf.RawBytes.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)(cf.Offset + i));

                // MOV Rx, #N
                if ((word & 0x0FF00000) == 0x03A00000)
                {
                    uint imm = ARMCodec.DecodeImm8r4((word >> 8) & 0xF, word & 0xFF);
                    if (imm == cf.FunctionCount)
                    {
                        var enc = ARMCodec.EncodeImm8r4((uint)newCount);
                        if (enc != null)
                        {
                            uint newWord = (word & 0xFFFFF000) | (enc.Value.rot << 8) | enc.Value.imm8;
                            BitConverter.GetBytes(newWord).CopyTo(data, (int)(cf.Offset + i));
                            return;
                        }
                    }
                }

                // CMP Rx, #N
                if ((word & 0x0FF00000) == 0x03500000)
                {
                    uint imm = ARMCodec.DecodeImm8r4((word >> 8) & 0xF, word & 0xFF);
                    if (imm == cf.FunctionCount)
                    {
                        var enc = ARMCodec.EncodeImm8r4((uint)newCount);
                        if (enc != null)
                        {
                            uint newWord = (word & 0xFFFFF000) | (enc.Value.rot << 8) | enc.Value.imm8;
                            BitConverter.GetBytes(newWord).CopyTo(data, (int)(cf.Offset + i));
                            return;
                        }
                    }
                }
            }
        }

        #endregion

        #region IV. Adding Entirely New Mechanics

        /// <summary>
        /// Add a completely new mechanic entry (new move, ability, or item).
        /// Creates the call function, timing table, and all required relocation patches.
        /// </summary>
        public static bool AddNewMechanic(
            DecompiledCRO cro,
            MechanicType type,
            int newIndex,
            byte[] callFunctionCode,
            (byte timingByte, byte[] functionCode)[] timings,
            string name = null,
            Action<string> log = null)
        {
            log ??= _ => { };
            byte[] data = cro.RawData;
            var starts = cro.SegmentStarts;

            MechanicTable table = type switch
            {
                MechanicType.Move => cro.MoveTable,
                MechanicType.Ability => cro.AbilityTable,
                MechanicType.Item => cro.ItemTable,
                _ => null
            };

            if (table == null)
            {
                log($"No {type} table found");
                return false;
            }

            // 1. Write call function to free code space
            int totalCodeNeeded = callFunctionCode.Length;
            foreach (var t in timings) totalCodeNeeded += t.functionCode.Length + 16;

            uint callFuncOfs = cro.FindFreeCodeSpace(totalCodeNeeded + 0x100);
            if (callFuncOfs == 0)
            {
                log("No free code space for new mechanic.");
                return false;
            }

            Array.Copy(callFunctionCode, 0, data, (int)callFuncOfs, callFunctionCode.Length);
            log($"  Call function at 0x{callFuncOfs:X}");

            // 2. Write each timing function
            uint currentCodeOfs = callFuncOfs + (uint)callFunctionCode.Length;
            // Align
            currentCodeOfs = (currentCodeOfs + 3) & ~3u;

            var funcOffsets = new uint[timings.Length];
            for (int i = 0; i < timings.Length; i++)
            {
                funcOffsets[i] = currentCodeOfs;
                Array.Copy(timings[i].functionCode, 0, data, (int)currentCodeOfs, timings[i].functionCode.Length);
                currentCodeOfs += (uint)timings[i].functionCode.Length;
                currentCodeOfs = (currentCodeOfs + 3) & ~3u;
            }

            // 3. Create timing table in data segment
            int timingTableSize = timings.Length * 8;
            uint timingTableOfs = cro.FindFreeDataSpace(timingTableSize + 16);
            if (timingTableOfs == 0) timingTableOfs = cro.FindFreeCodeSpace(timingTableSize + 16);
            if (timingTableOfs == 0)
            {
                log("No free space for timing table.");
                return false;
            }

            for (int i = 0; i < timings.Length; i++)
            {
                uint entryOfs = timingTableOfs + (uint)(i * 8);
                data[(int)entryOfs] = timings[i].timingByte;
                data[(int)entryOfs + 1] = 0;
                data[(int)entryOfs + 2] = 0;
                data[(int)entryOfs + 3] = 0;
                // Placeholder for function pointer (will be resolved by relocation)
                BitConverter.GetBytes((uint)0).CopyTo(data, (int)(entryOfs + 4));

                // Create RPT for this function pointer
                int teSeg = CROUtil.GetSegmentForAddress(entryOfs + 4, data);
                uint teOff = (entryOfs + 4) - starts[teSeg];
                int fSeg = CROUtil.GetSegmentForAddress(funcOffsets[i], data);
                uint fAdd = funcOffsets[i] - starts[fSeg];

                var rpt = CRORelocationEntry.Create(teSeg, teOff, fSeg, fAdd);
                rpt.Index = cro.Relocations.Count;
                cro.Relocations.Add(rpt);
            }

            log($"  Timing table at 0x{timingTableOfs:X} ({timings.Length} entries)");

            // 4. Add entry to master table
            if (newIndex >= table.EntryCount)
            {
                // Need to expand the master table — this is complex as it requires
                // moving the table if there's no contiguous space
                log($"  Index {newIndex} exceeds table size {table.EntryCount}. Table expansion needed.");
                // For now, update the CMP limit instruction
                table.EntryCount = newIndex + 1;
            }

            uint masterEntryOfs = table.TableOffset + (uint)(newIndex * table.EntrySize);
            if (masterEntryOfs + table.EntrySize > data.Length)
            {
                log($"  Master table entry offset 0x{masterEntryOfs:X} out of bounds.");
                return false;
            }

            // Write placeholder to master table entry
            BitConverter.GetBytes((uint)0).CopyTo(data, (int)masterEntryOfs);       // call func ptr (resolved by RPT)
            BitConverter.GetBytes((uint)0).CopyTo(data, (int)(masterEntryOfs + 4)); // reserved

            // Create RPT: master table → call function
            int mSeg = CROUtil.GetSegmentForAddress(masterEntryOfs, data);
            uint mOff = masterEntryOfs - starts[mSeg];
            int cfSeg = CROUtil.GetSegmentForAddress(callFuncOfs, data);
            uint cfAdd = callFuncOfs - starts[cfSeg];

            var masterRpt = CRORelocationEntry.Create(mSeg, mOff, cfSeg, cfAdd);
            masterRpt.Index = cro.Relocations.Count;
            cro.Relocations.Add(masterRpt);

            // Update header
            cro.Header.PatchTableCount = (uint)cro.Relocations.Count;

            log($"  Added new {type} #{newIndex} '{name ?? "unnamed"}'");
            return true;
        }

        #endregion

        #region V. BSS Allocation

        /// <summary>
        /// Allocate space in the BSS segment for runtime state storage.
        /// </summary>
        public static BSSSlot AllocateBSS(DecompiledCRO cro, string name, int size, bool perPokemon)
        {
            uint currentBSSEnd = cro.Header.BSSSize;
            // Align to 4 bytes
            uint alignedOffset = (currentBSSEnd + 3) & ~3u;
            uint totalSize = perPokemon ? (uint)(size * 24) : (uint)size; // 24 slots for per-Pokemon

            var slot = new BSSSlot
            {
                Name = name,
                Offset = alignedOffset,
                Size = totalSize,
                PerPokemon = perPokemon
            };

            // Update BSS size
            cro.Header.BSSSize = alignedOffset + totalSize;
            BitConverter.GetBytes(cro.Header.BSSSize).CopyTo(cro.RawData, 0x94);

            // Update segment table BSS entry
            if (cro.Segments[3] != null)
            {
                cro.Segments[3].Size = cro.Header.BSSSize;
                uint segOfs = cro.Header.SegmentTableOffset;
                BitConverter.GetBytes(cro.Header.BSSSize).CopyTo(cro.RawData, (int)(segOfs + 3 * 12 + 4));
            }

            cro.BSSAllocations.Add(slot);
            return slot;
        }

        #endregion

        #region Stock Function Composition

        /// <summary>
        /// Clone an existing function's machine code for reuse in a new mechanic.
        /// The cloned code has BL targets adjusted for the new location.
        /// </summary>
        public static byte[] CloneFunction(DecompiledCRO cro, uint sourceOffset, uint destOffset)
        {
            byte[] data = cro.RawData;
            int size = CRODecompiler.EstimateFunctionSizePublic(data, sourceOffset);
            byte[] clone = new byte[size];
            Array.Copy(data, (int)sourceOffset, clone, 0, size);

            // Fix up BL targets: recalculate relative offsets for new location
            for (int i = 0; i < size; i += 4)
            {
                uint word = BitConverter.ToUInt32(clone, i);
                if (ARMCodec.IsBranchLink(word))
                {
                    uint originalTarget = ARMCodec.DecodeBranchTarget(word, sourceOffset + (uint)i);
                    byte[] newBL = ARMCodec.EncodeBranchLink(destOffset + (uint)i, originalTarget);
                    Array.Copy(newBL, 0, clone, i, 4);
                }
                else if (ARMCodec.IsBranch(word))
                {
                    uint originalTarget = ARMCodec.DecodeBranchTarget(word, sourceOffset + (uint)i);
                    // Only fix branches that go outside the function
                    if (originalTarget < sourceOffset || originalTarget >= sourceOffset + (uint)size)
                    {
                        byte[] newB = ARMCodec.EncodeBranch(destOffset + (uint)i, originalTarget);
                        Array.Copy(newB, 0, clone, i, 4);
                    }
                    else
                    {
                        // Internal branch — offset stays the same
                    }
                }
            }

            return clone;
        }

        /// <summary>
        /// Compose a new function by combining pieces of stock functions.
        /// Each piece is a (sourceFunction, startOffset, length) tuple specifying
        /// which bytes to extract. The pieces are concatenated and BL fixups applied.
        /// A return instruction (POP {PC} or BX LR) is appended if not present.
        /// </summary>
        public static byte[] ComposeFunction(
            DecompiledCRO cro,
            uint destOffset,
            (StockFunction source, int startRelative, int length)[] pieces,
            bool addReturn = true)
        {
            var result = new List<byte>();
            uint currentOfs = destOffset;

            foreach (var (source, startRel, length) in pieces)
            {
                if (startRel + length > source.Size) continue;
                byte[] chunk = new byte[length];
                Array.Copy(source.Code, startRel, chunk, 0, length);

                // Fix BL targets
                for (int i = 0; i < length; i += 4)
                {
                    uint word = BitConverter.ToUInt32(chunk, i);
                    if (ARMCodec.IsBranchLink(word))
                    {
                        uint origPc = source.Offset + (uint)startRel + (uint)i;
                        uint target = ARMCodec.DecodeBranchTarget(word, origPc);
                        byte[] newBL = ARMCodec.EncodeBranchLink(currentOfs + (uint)i, target);
                        Array.Copy(newBL, 0, chunk, i, 4);
                    }
                }

                result.AddRange(chunk);
                currentOfs += (uint)length;
            }

            // Add return if needed
            if (addReturn && result.Count >= 4)
            {
                uint lastWord = BitConverter.ToUInt32(result.ToArray(), result.Count - 4);
                bool hasReturn = (lastWord & 0x0FFF0000) == 0x08BD0000 && (lastWord & 0x8000) != 0; // POP {.., PC}
                hasReturn |= (lastWord & 0x0FFFFFFF) == 0x012FFF1E; // BX LR

                if (!hasReturn)
                {
                    result.AddRange(BitConverter.GetBytes(0xE12FFF1Eu)); // BX LR
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Generate a standard consumable item function.
        /// Pattern from the ARM Research guide.
        /// </summary>
        public static byte[] GenerateConsumableItemFunction(int itemId, uint blTarget1, uint blTarget2, uint funcOffset)
        {
            var asm = new List<byte>();

            // PUSH {R4, LR}
            asm.AddRange(BitConverter.GetBytes(0xE92D4010u));

            // MOV R0, #itemId
            var enc = ARMCodec.EncodeImm8r4((uint)itemId);
            if (enc != null)
                asm.AddRange(BitConverter.GetBytes(0xE3A00000u | (enc.Value.rot << 8) | enc.Value.imm8));
            else
            {
                // Two-instruction MOV for large immediates: MOVW R0, #lo16
                asm.AddRange(BitConverter.GetBytes(0xE3000000u | (uint)((itemId & 0xF000) << 4) | (uint)(itemId & 0xFFF)));
            }

            // BL blTarget1 (e.g., consume item function)
            asm.AddRange(ARMCodec.EncodeBranchLink(funcOffset + (uint)asm.Count, blTarget1));

            // BL blTarget2 (e.g., apply effect function)
            asm.AddRange(ARMCodec.EncodeBranchLink(funcOffset + (uint)asm.Count, blTarget2));

            // POP {R4, PC}
            asm.AddRange(BitConverter.GetBytes(0xE8BD8010u));

            return asm.ToArray();
        }

        #endregion
    }

    /// <summary>
    /// File backup utility for CRO/code.bin modifications.
    /// </summary>
    public static class CROBackup
    {
        public static void CreateBackup(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            string bakPath = filePath + ".bak";
            if (!File.Exists(bakPath))
                File.Copy(filePath, bakPath);
        }

        public static bool RestoreBackup(string filePath)
        {
            string bakPath = filePath + ".bak";
            if (!File.Exists(bakPath)) return false;
            File.Copy(bakPath, filePath, true);
            return true;
        }

        public static bool HasBackup(string filePath) => File.Exists(filePath + ".bak");
    }
}
