using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.Core.CTR
{
    /// <summary>
    /// Decompiles a CRO binary into a fully navigable DecompiledCRO object model.
    /// Discovers move/ability/item tables, timing entries, function pointers,
    /// and all relocation patch chains.
    /// </summary>
    public static class CRODecompiler
    {

        // USUM Battle.cro known offsets (Vanilla)
        private const uint VANILLA_MOVE_DISPATCHER_SIG = 0xE92D4070;   // PUSH {R4-R6, LR}
        private const uint VANILLA_ABILITY_DISPATCHER_SIG = 0xE92D4030; // PUSH {R4, R5, LR}

        /// <summary>
        /// Decompile a CRO binary into a structured DecompiledCRO object.
        /// </summary>
        public static DecompiledCRO Decompile(byte[] data, string sourcePath = null, string[] moveNames = null, string[] abilityNames = null, string[] itemNames = null)
        {
            var cro = new DecompiledCRO
            {
                RawData = data,
                SourcePath = sourcePath
            };

            // 1. Parse header
            ParseHeader(data, cro.Header);

            // 2. Parse segment table
            ParseSegments(data, cro);

            // 3. Detect expansion state
            cro.IsExpanded = data.Length >= 0x110000; // Expanded Battle.cro is >= 0x110000

            // 4. Parse all relocation patches
            ParseRelocations(data, cro);

            // 5. Discover mechanic tables by scanning relocation patterns
            var claimedClusters = new HashSet<int>();
            var claimedJumpTables = new HashSet<uint>();

            cro.MoveTable = DiscoverMechanicTable(data, cro, MechanicType.Move, moveNames, claimedClusters, claimedJumpTables);
            cro.AbilityTable = DiscoverMechanicTable(data, cro, MechanicType.Ability, abilityNames, claimedClusters, claimedJumpTables);
            cro.ItemTable = DiscoverMechanicTable(data, cro, MechanicType.Item, itemNames, claimedClusters, claimedJumpTables);

            // 6. Discover stock functions from the code segment
            DiscoverStockFunctions(data, cro);

            return cro;
        }

        /// <summary>
        /// Lightweight decompile that only parses header, segments, and relocations (no table discovery).
        /// </summary>
        public static DecompiledCRO DecompileStructure(byte[] data, string sourcePath = null)
        {
            var cro = new DecompiledCRO { RawData = data, SourcePath = sourcePath };
            ParseHeader(data, cro.Header);
            ParseSegments(data, cro);
            cro.IsExpanded = data.Length >= 0x110000;
            ParseRelocations(data, cro);
            return cro;
        }

        #region Header & Segments
        private static void ParseHeader(byte[] data, CROHeader h)
        {
            if (data.Length < 0x130) throw new InvalidDataException("Data too small for CRO header");

            Array.Copy(data, 0, h.SHA256Hashes, 0, 0x80);
            h.Magic = BitConverter.ToUInt32(data, 0x80);
            if (h.Magic != 0x304F5243) // "CRO0" LE
                throw new InvalidDataException($"Invalid CRO magic: 0x{h.Magic:X8}");

            h.NameOffset = BitConverter.ToUInt32(data, 0x84);
            h.FileSize = BitConverter.ToUInt32(data, 0x90);
            h.BSSSize = BitConverter.ToUInt32(data, 0x94);
            h.CodeStart = BitConverter.ToUInt32(data, 0xB0);
            h.CodeSize = BitConverter.ToUInt32(data, 0xB4);
            h.DataStart = BitConverter.ToUInt32(data, 0xB8);
            h.DataSize = BitConverter.ToUInt32(data, 0xBC);
            h.SegmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
            h.PatchTableOffset = BitConverter.ToUInt32(data, 0x128);
            h.PatchTableCount = BitConverter.ToUInt32(data, 0x12C);
        }

        private static void ParseSegments(byte[] data, DecompiledCRO cro)
        {
            uint segOfs = cro.Header.SegmentTableOffset;
            for (int i = 0; i < 4; i++)
            {
                int off = (int)(segOfs + i * 12);
                if (off + 12 > data.Length) break;
                cro.Segments[i] = new CROSegmentEntry
                {
                    Index = i,
                    Start = BitConverter.ToUInt32(data, off),
                    Size = BitConverter.ToUInt32(data, off + 4),
                    ID = BitConverter.ToUInt32(data, off + 8)
                };
            }
        }
        #endregion

        #region Relocation Patches
        private static void ParseRelocations(byte[] data, DecompiledCRO cro)
        {
            uint ptOfs = cro.Header.PatchTableOffset;
            uint ptCnt = cro.Header.PatchTableCount;

            for (int i = 0; i < (int)ptCnt; i++)
            {
                int off = (int)(ptOfs + i * 12);
                if (off + 12 > data.Length) break;
                cro.Relocations.Add(CRORelocationEntry.FromBytes(data, off, i));
            }
        }
        #endregion

        #region Mechanic Table Discovery
        /// <summary>
        /// Discover a mechanic table by scanning .rodata (segment 1) for dense clusters
        /// of relocation patches pointing back to .code (segment 0).
        /// USUM dispatches via LDR PC, [PC, Rn, LSL #2] pointer tables, so the actual
        /// function pointer array lives in .rodata and each entry has an RPT.
        /// </summary>
        private static MechanicTable DiscoverMechanicTable(byte[] data, DecompiledCRO cro, MechanicType type, string[] names, HashSet<int> claimedClusters, HashSet<uint> claimedJumpTables)
        {
            var table = new MechanicTable { Type = type };
            var starts = cro.SegmentStarts;

            int expectedMin, expectedMax;
            switch (type)
            {
                case MechanicType.Move:    expectedMin = 300; expectedMax = 2500; break;
                case MechanicType.Ability: expectedMin = 100; expectedMax = 1000; break;
                case MechanicType.Item:    expectedMin = 50;  expectedMax = 2500; break;
                default: return table;
            }

            // Build a sorted set of all write-to offsets in seg 1 that target seg 0
            uint seg1Start = starts[1];
            uint seg1End = seg1Start + (cro.Segments[1]?.Size ?? 0);

            var seg1ToCode = new List<(uint writeToAbs, int rptIdx, uint targetAbs)>();
            for (int i = 0; i < cro.Relocations.Count; i++)
            {
                var r = cro.Relocations[i];
                uint wAbs = r.AbsoluteWriteTo(starts);
                // Write location in seg 1, target in seg 0
                if (wAbs >= seg1Start && wAbs < seg1End && r.TargetSegment == 0)
                {
                    seg1ToCode.Add((wAbs, i, r.AbsoluteTarget(starts)));
                }
            }

            seg1ToCode.Sort((a, b) => a.writeToAbs.CompareTo(b.writeToAbs));

            // Find contiguous clusters: consecutive 4-byte-spaced write-to offsets
            var clusters = new List<(uint startOfs, int count, int firstRptIdx)>();
            int ci = 0;
            while (ci < seg1ToCode.Count)
            {
                uint clusterStart = seg1ToCode[ci].writeToAbs;
                int firstRpt = seg1ToCode[ci].rptIdx;
                int count = 1;
                int j = ci + 1;
                while (j < seg1ToCode.Count && seg1ToCode[j].writeToAbs == clusterStart + (uint)(count * 4))
                {
                    count++;
                    j++;
                }
                if (count >= 20) // Minimum cluster size to be interesting
                    clusters.Add((clusterStart, count, firstRpt));
                ci = j;
            }

            // Sort clusters by size descending — largest is likely move table
            clusters.Sort((a, b) => b.count.CompareTo(a.count));

            // Match cluster to expected table size range
            MechanicTable TryClaimCluster()
            {
                for (int ci2 = 0; ci2 < clusters.Count; ci2++)
                {
                    if (claimedClusters.Contains(ci2)) continue;
                    var (clOfs, clCount, clFirstRpt) = clusters[ci2];

                    if (clCount >= expectedMin && clCount <= expectedMax)
                    {
                        claimedClusters.Add(ci2);
                        var t = new MechanicTable
                        {
                            Type = type,
                            TableOffset = clOfs,
                            EntryCount = clCount,
                            EntrySize = 4, // Each entry is a single 4-byte relocated pointer
                            Segment = 1,
                            SegmentRelative = clOfs - seg1Start
                        };
                        return t;
                    }
                }
                return null;
            }

            var found = TryClaimCluster();
            if (found != null)
            {
                table = found;
                // Parse entries — each is a 4-byte function pointer slot
                ParsePointerTableEntries(data, cro, table, names, seg1ToCode);
            }
            else
            {
                // Fallback: scan for LDR PC, [PC, Rn, LSL #2] jump tables with nearby CMP
                DiscoverViaJumpTable(data, cro, table, expectedMin, expectedMax, names, claimedJumpTables);
            }

            return table;
        }

        private static void ParsePointerTableEntries(byte[] data, DecompiledCRO cro, MechanicTable table,
            string[] names, List<(uint writeToAbs, int rptIdx, uint targetAbs)> seg1ToCode)
        {
            var starts = cro.SegmentStarts;
            var rptLookup = seg1ToCode.ToDictionary(x => x.writeToAbs, x => (x.rptIdx, x.targetAbs));

            for (int idx = 0; idx < table.EntryCount; idx++)
            {
                uint entryOfs = table.TableOffset + (uint)(idx * table.EntrySize);

                var entry = new MechanicEntry
                {
                    Index = idx,
                    Type = table.Type,
                    MasterTableEntryOffset = entryOfs,
                    Name = names != null && idx < names.Length ? names[idx] : $"{table.Type} #{idx}"
                };

                // Each entry is a single 4-byte relocated function pointer
                if (rptLookup.TryGetValue(entryOfs, out var rptInfo))
                {
                    entry.AllRelocationIndices.Add(rptInfo.rptIdx);
                    uint funcAddr = rptInfo.targetAbs;

                    // The function at funcAddr is the move/ability/item handler
                    if (funcAddr > 0 && funcAddr < data.Length)
                    {
                        int funcSize = EstimateFunctionSize(data, funcAddr);
                        byte[] funcBytes = new byte[funcSize];
                        Array.Copy(data, (int)funcAddr, funcBytes, 0,
                            Math.Min(funcSize, data.Length - (int)funcAddr));

                        entry.CallFunc = new CallFunction
                        {
                            Offset = funcAddr,
                            RawBytes = funcBytes,
                            Disassembly = ARMCodec.Disassemble(funcBytes, funcAddr),
                            RelocationIndex = rptInfo.rptIdx,
                            FunctionCount = 1
                        };

                        // Scan function for BL targets (sub-function calls)
                        var subCalls = new List<uint>();
                        for (int si = 0; si < funcSize; si += 4)
                        {
                            uint word = BitConverter.ToUInt32(data, (int)(funcAddr + si));
                            if (ARMCodec.IsBranchLink(word))
                                subCalls.Add(ARMCodec.DecodeBranchTarget(word, funcAddr + (uint)si));
                        }
                        entry.CallFunc.InternalCalls = subCalls;
                    }
                }

                table.Entries.Add(entry);
            }
        }

        private static void DiscoverViaJumpTable(byte[] data, DecompiledCRO cro, MechanicTable table,
            int expectedMin, int expectedMax, string[] names, HashSet<uint> claimedJumpTables)
        {
            uint codeStart = cro.Header.CodeStart;
            uint codeEnd = codeStart + cro.Header.CodeSize;

            // Find LDR PC, [PC, Rn, LSL #2] instructions
            for (uint i = codeStart; i < codeEnd && i + 4 <= data.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)i);

                // LDR PC, [PC, Rn, LSL #2] = E79FF10n
                if ((word & 0x0FFFFFF0) != 0x079FF100) continue;

                if (claimedJumpTables.Contains(i)) continue;

                // Look backwards for a CMP Rn, #N with N in range
                for (int scan = -40; scan < 0; scan += 4)
                {
                    int checkOfs = (int)i + scan;
                    if (checkOfs < (int)codeStart) continue;
                    uint checkWord = BitConverter.ToUInt32(data, checkOfs);
                    if ((checkWord & 0x0FF00000) != 0x03500000) continue;

                    uint val = ARMCodec.DecodeImm8r4((checkWord >> 8) & 0xF, checkWord & 0xFF);
                    if (val >= (uint)expectedMin && val <= (uint)expectedMax)
                    {
                        claimedJumpTables.Add(i);
                        // Found a dispatch. The pointer table starts at i + 4
                        table.TableOffset = i + 4;
                        table.EntryCount = (int)val;
                        table.EntrySize = 4;
                        table.Segment = 0;
                        table.SegmentRelative = (i + 4) - codeStart;
                        return;
                    }
                }
            }
        }

        private static void ParseMechanicEntries(byte[] data, DecompiledCRO cro, MechanicTable table, string[] names)
        {
            var starts = cro.SegmentStarts;

            for (int idx = 0; idx < table.EntryCount; idx++)
            {
                uint entryOfs = table.TableOffset + (uint)(idx * table.EntrySize);
                if (entryOfs + table.EntrySize > data.Length) break;

                var entry = new MechanicEntry
                {
                    Index = idx,
                    Type = table.Type,
                    MasterTableEntryOffset = entryOfs,
                    Name = names != null && idx < names.Length ? names[idx] : $"{table.Type} #{idx}"
                };

                // Word 0: pointer to call function (relocated)
                // Word 1: reserved / additional data
                int rptCallFunc = FindRelocationForWriteTo(cro, entryOfs);
                if (rptCallFunc >= 0)
                {
                    var rpt = cro.Relocations[rptCallFunc];
                    uint callFuncAddr = rpt.AbsoluteTarget(starts);
                    entry.AllRelocationIndices.Add(rptCallFunc);

                    entry.CallFunc = ParseCallFunction(data, cro, callFuncAddr, starts);
                    if (entry.CallFunc != null)
                    {
                        entry.CallFunc.RelocationIndex = rptCallFunc;

                        // Trace to timing table
                        if (entry.CallFunc.TimingTablePointerOffset > 0)
                        {
                            int rptTiming = FindRelocationForWriteTo(cro, entry.CallFunc.TimingTablePointerOffset);
                            if (rptTiming >= 0)
                            {
                                var rptT = cro.Relocations[rptTiming];
                                uint timingAddr = rptT.AbsoluteTarget(starts);
                                entry.AllRelocationIndices.Add(rptTiming);

                                entry.Timings = ParseTimingTableFromData(data, cro, timingAddr, entry.CallFunc.FunctionCount, starts);
                                if (entry.Timings != null)
                                {
                                    entry.Timings.RelocationIndex = rptTiming;
                                    foreach (var t in entry.Timings.Entries)
                                    {
                                        if (t.RelocationIndex >= 0)
                                            entry.AllRelocationIndices.Add(t.RelocationIndex);
                                    }
                                }
                            }
                        }
                    }
                }

                table.Entries.Add(entry);
            }
        }

        private static CallFunction ParseCallFunction(byte[] data, DecompiledCRO cro, uint offset, uint[] starts)
        {
            if (offset + 4 > data.Length || offset < cro.Header.CodeStart) return null;

            var cf = new CallFunction { Offset = offset };

            int maxSize = 0x80; // Max scan distance
            int funcEnd = (int)offset;
            for (int scan = 0; scan < maxSize && (int)offset + scan + 4 <= data.Length; scan += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)offset + scan);
                // POP {..., PC}
                if ((word & 0x0FFF0000) == 0x08BD0000 && (word & 0x8000) != 0)
                { funcEnd = (int)offset + scan + 4; break; }
                // BX LR
                if ((word & 0x0FFFFFFF) == 0x012FFF1E)
                { funcEnd = (int)offset + scan + 4; break; }
            }
            if (funcEnd <= (int)offset) funcEnd = (int)offset + maxSize;

            int size = funcEnd - (int)offset;
            cf.RawBytes = new byte[size];
            Array.Copy(data, (int)offset, cf.RawBytes, 0, Math.Min(size, data.Length - (int)offset));
            cf.Disassembly = ARMCodec.Disassemble(cf.RawBytes, offset);

            // Find LDR [PC, #imm] within the call function → timing table pointer
            for (int scan = 0; scan < size; scan += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)offset + scan);
                if ((word & 0x0FFF0000) == 0x059F0000) // LDR Rx, [PC, #imm]
                {
                    uint pcImm = word & 0xFFF;
                    uint literalAddr = offset + (uint)scan + 8 + pcImm;
                    if (literalAddr + 4 <= data.Length)
                    {
                        cf.TimingTablePointerOffset = literalAddr;
                        break;
                    }
                }
            }

            // Find the entry count: typically a MOV Rx, #N or CMP Rx, #N near the loop
            for (int scan = 0; scan < size; scan += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)offset + scan);
                // MOV Rx, #N → E3A0x0NN
                if ((word & 0x0FF00000) == 0x03A00000)
                {
                    uint imm8 = word & 0xFF;
                    uint rot = (word >> 8) & 0xF;
                    uint val = ARMCodec.DecodeImm8r4(rot, imm8);
                    if (val > 0 && val < 64) // Reasonable timing entry count
                    {
                        cf.FunctionCount = (int)val;
                        break;
                    }
                }
                // CMP Rx, #N → E35x00NN
                if ((word & 0x0FF00000) == 0x03500000)
                {
                    uint imm8 = word & 0xFF;
                    uint rot = (word >> 8) & 0xF;
                    uint val = ARMCodec.DecodeImm8r4(rot, imm8);
                    if (val > 0 && val < 64)
                    {
                        cf.FunctionCount = (int)val;
                        break;
                    }
                }
            }

            if (cf.FunctionCount == 0) cf.FunctionCount = 1; // Fallback

            return cf;
        }

        private static TimingTable ParseTimingTableFromData(byte[] data, DecompiledCRO cro, uint offset, int count, uint[] starts)
        {
            if (offset + count * 8 > data.Length) return null;

            var tt = new TimingTable
            {
                Offset = offset,
                Segment = offset >= starts[2] ? 2 : (offset >= starts[1] ? 1 : 0)
            };
            tt.SegmentRelative = offset - starts[tt.Segment];

            for (int i = 0; i < count; i++)
            {
                uint entryOfs = offset + (uint)(i * 8);
                if (entryOfs + 8 > data.Length) break;

                var te = new TimingEntry
                {
                    Index = i,
                    TimingByte = data[entryOfs],
                    Reserved = new byte[] { data[entryOfs + 1], data[entryOfs + 2], data[entryOfs + 3] },
                    FunctionPointerSlot = BitConverter.ToUInt32(data, (int)(entryOfs + 4))
                };

                // Resolve function pointer via relocation
                int rptFunc = FindRelocationForWriteTo(cro, entryOfs + 4);
                if (rptFunc >= 0)
                {
                    var rpt = cro.Relocations[rptFunc];
                    te.ResolvedFunctionOffset = rpt.AbsoluteTarget(starts);
                    te.RelocationIndex = rptFunc;

                    // Read function bytes
                    if (te.ResolvedFunctionOffset > 0 && te.ResolvedFunctionOffset < data.Length)
                    {
                        int funcSize = EstimateFunctionSize(data, te.ResolvedFunctionOffset);
                        te.FunctionSize = funcSize;
                        te.FunctionBytes = new byte[funcSize];
                        Array.Copy(data, (int)te.ResolvedFunctionOffset, te.FunctionBytes, 0,
                            Math.Min(funcSize, data.Length - (int)te.ResolvedFunctionOffset));
                        te.Disassembly = ARMCodec.Disassemble(te.FunctionBytes, te.ResolvedFunctionOffset);
                    }
                }

                tt.Entries.Add(te);
            }

            return tt;
        }

        /// <summary>
        /// Estimate function size by scanning for a return instruction (POP {.., PC} or BX LR).
        /// </summary>
        private static int EstimateFunctionSize(byte[] data, uint offset)
        {
            int maxScan = 0x400; // 1KB max function size
            for (int i = 0; i < maxScan && (int)offset + i + 4 <= data.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)offset + i);

                // POP {..., PC} — common function return
                if ((word & 0x0FFF0000) == 0x08BD0000 && (word & 0x8000) != 0)
                    return i + 4;

                // BX LR
                if ((word & 0x0FFFFFFF) == 0x012FFF1E)
                    return i + 4;

                // Unconditional B to outside (simple tail-call return)
                if ((word & 0xFF000000) == 0xEA000000)
                {
                    uint target = ARMCodec.DecodeBranchTarget(word, offset + (uint)i);
                    // If target is far away from current function, treat as end
                    if (target < offset || target > offset + (uint)maxScan)
                        return i + 4;
                }
            }
            return Math.Min(maxScan, (int)(data.Length - offset));
        }
        #endregion

        #region Stock Function Discovery
        private static void DiscoverStockFunctions(byte[] data, DecompiledCRO cro)
        {
            var starts = cro.SegmentStarts;

            // Scan for functions called by multiple mechanic entries (BL targets that appear frequently)
            var blTargetCounts = new Dictionary<uint, int>();

            uint codeStart = cro.Header.CodeStart;
            uint codeEnd = codeStart + cro.Header.CodeSize;

            for (uint i = codeStart; i < codeEnd && i + 4 <= data.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)i);
                if (ARMCodec.IsBranchLink(word))
                {
                    uint target = ARMCodec.DecodeBranchTarget(word, i);
                    if (target >= codeStart && target < codeEnd)
                    {
                        blTargetCounts.TryGetValue(target, out int count);
                        blTargetCounts[target] = count + 1;
                    }
                }
            }

            // Functions called 5+ times are likely stock/utility functions
            var stockCandidates = blTargetCounts
                .Where(kv => kv.Value >= 5)
                .OrderByDescending(kv => kv.Value)
                .Take(200);

            foreach (var (addr, callCount) in stockCandidates)
            {
                if (addr + 4 > data.Length) continue;
                int size = EstimateFunctionSize(data, addr);
                var code = new byte[size];
                Array.Copy(data, (int)addr, code, 0, Math.Min(size, data.Length - (int)addr));

                var sf = new StockFunction
                {
                    Offset = addr,
                    Size = size,
                    Code = code,
                    Name = $"sub_{addr:X}",
                    Category = CategorizeFunction(data, addr, size),
                    Disassembly = ARMCodec.Disassemble(code, addr),
                    Description = $"Called {callCount} times"
                };

                // Find internal BL calls
                for (int j = 0; j < size; j += 4)
                {
                    uint word = BitConverter.ToUInt32(code, j);
                    if (ARMCodec.IsBranchLink(word))
                    {
                        uint target = ARMCodec.DecodeBranchTarget(word, addr + (uint)j);
                        sf.InternalCalls.Add(target);
                    }
                }

                cro.StockFunctions.Add(sf);
            }
        }

        private static string CategorizeFunction(byte[] data, uint offset, int size)
        {
            // Heuristic categorization based on internal patterns
            for (int i = 0; i < size && (int)offset + i + 4 <= data.Length; i += 4)
            {
                uint word = BitConverter.ToUInt32(data, (int)offset + i);

                // Look for damage calculation signatures
                if ((word & 0x0FF00FFF) == 0x00000090) return "Damage"; // MUL
                // Look for stat modification signatures (CMP with stat IDs 1-7)
                if ((word & 0x0FFF0F00) == 0x03500100) return "Stat";
                // Weather check (CMP with weather constants)
                if ((word & 0x0FFF0FFF) == 0x03500005 || (word & 0x0FFF0FFF) == 0x03500006) return "Weather";
            }
            return "Other";
        }
        #endregion

        /// <summary>
        /// Public wrapper for EstimateFunctionSize.
        /// </summary>
        public static int EstimateFunctionSizePublic(byte[] data, uint offset) => EstimateFunctionSize(data, offset);

        #region Helpers
        private static int FindRelocationForWriteTo(DecompiledCRO cro, uint absoluteWriteTo)
        {
            var starts = cro.SegmentStarts;
            for (int i = 0; i < cro.Relocations.Count; i++)
            {
                if (cro.Relocations[i].AbsoluteWriteTo(starts) == absoluteWriteTo)
                    return i;
            }
            return -1;
        }
        #endregion
    }
}
