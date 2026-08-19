using System;
using System.Collections.Generic;

namespace pk3DS.Core.CTR
{
    public enum MechanicType : byte { Move = 0, Ability = 1, Item = 2 }

    public class CROHeader
    {
        public byte[] SHA256Hashes = new byte[0x80]; // 4x SHA-256 at 0x00-0x7F
        public uint Magic;          // 0x80: "CRO0" = 0x304F5243
        public uint NameOffset;     // 0x84
        public uint FileSize;       // 0x90
        public uint BSSSize;        // 0x94
        public uint CodeStart;      // 0xB0
        public uint CodeSize;       // 0xB4
        public uint DataStart;      // 0xB8
        public uint DataSize;       // 0xBC
        public uint SegmentTableOffset; // 0xC8
        public uint PatchTableOffset;   // 0x128
        public uint PatchTableCount;    // 0x12C
    }

    public class CROSegmentEntry
    {
        public int Index;
        public uint Start;
        public uint Size;
        public uint ID;
    }

    public class CRORelocationEntry
    {
        public int Index;
        public uint RawWord0;       // WriteSeg(low 4 bits) | WriteOffset(>>4)
        public uint RawWord1;       // PatchType(byte0), TargetSeg(byte1), ...
        public uint RawWord2;       // Addend

        public int WriteSegment => (int)(RawWord0 & 0xF);
        public uint WriteOffset => RawWord0 >> 4;
        public byte PatchType => (byte)(RawWord1 & 0xFF);
        public int TargetSegment => (int)((RawWord1 >> 8) & 0xFF);
        public uint Addend { get => RawWord2; set => RawWord2 = value; }

        public uint AbsoluteWriteTo(uint[] segStarts) =>
            WriteOffset + (WriteSegment < segStarts.Length ? segStarts[WriteSegment] : 0);

        public uint AbsoluteTarget(uint[] segStarts) =>
            Addend + (TargetSegment < segStarts.Length ? segStarts[TargetSegment] : 0);

        public byte[] ToBytes()
        {
            var b = new byte[12];
            BitConverter.GetBytes(RawWord0).CopyTo(b, 0);
            BitConverter.GetBytes(RawWord1).CopyTo(b, 4);
            BitConverter.GetBytes(RawWord2).CopyTo(b, 8);
            return b;
        }

        public static CRORelocationEntry FromBytes(byte[] data, int offset, int index)
        {
            return new CRORelocationEntry
            {
                Index = index,
                RawWord0 = BitConverter.ToUInt32(data, offset),
                RawWord1 = BitConverter.ToUInt32(data, offset + 4),
                RawWord2 = BitConverter.ToUInt32(data, offset + 8)
            };
        }

        public static CRORelocationEntry Create(int writeSeg, uint writeOff, int targetSeg, uint addend, byte patchType = 2)
        {
            return new CRORelocationEntry
            {
                RawWord0 = (writeOff << 4) | (uint)(writeSeg & 0xF),
                RawWord1 = (uint)(patchType | (targetSeg << 8)),
                RawWord2 = addend
            };
        }
    }

    public class TimingEntry
    {
        public int Index;
        public byte TimingByte;
        public byte[] Reserved = new byte[3]; // 3 zero bytes after timing
        public uint FunctionPointerSlot;       // The 4-byte slot where relocation writes the pointer
        public uint ResolvedFunctionOffset;    // Absolute file offset of the function (resolved via RPT)
        public int FunctionSize;               // Estimated size in bytes
        public byte[] FunctionBytes;           // Raw machine code
        public string Disassembly;             // Human-readable ARM disassembly
        public int RelocationIndex = -1;       // Index into RPT for this function pointer
    }

    public class TimingTable
    {
        public uint Offset;         // Absolute file offset of this timing table
        public int Segment;         // Which segment it lives in (typically 2 = .data)
        public uint SegmentRelative;// Offset relative to segment start
        public List<TimingEntry> Entries = new();
        public int RelocationIndex = -1; // RPT index for the pointer TO this table
    }

    public class CallFunction
    {
        public uint Offset;         // Absolute file offset of the call setup function
        public int FunctionCount;   // Number of timing entries (first byte at timing table pointer)
        public byte[] RawBytes;     // Raw call function bytes
        public string Disassembly;
        public uint TimingTablePointerOffset; // Where in the call function the timing table pointer lives
        public int RelocationIndex = -1;      // RPT index for pointer to this call function
        public List<uint> InternalCalls = new(); // BL targets within this function
    }

    public class MechanicEntry
    {
        public int Index;           // Move/Ability/Item index (e.g., Move 0 = Pound)
        public string Name;         // Human-readable name (from game text)
        public MechanicType Type;
        public uint MasterTableEntryOffset; // Absolute offset of the 8-byte entry in the master table
        public CallFunction CallFunc;
        public TimingTable Timings;
        public List<int> AllRelocationIndices = new(); // Every RPT index in the chain
    }

    public class MechanicTable
    {
        public MechanicType Type;
        public uint TableOffset;    // Absolute offset of the master index table
        public int Segment;
        public uint SegmentRelative;
        public int EntryCount;
        public int EntrySize = 8;   // Each entry is XX XX 00 00 | 00 00 00 00 (8 bytes)
        public List<MechanicEntry> Entries = new();
        public int SizePointerOffset = -1; // Offset in CRO header where table size is stored
    }

    public class BSSSlot
    {
        public string Name;
        public uint Offset;         // Offset within BSS segment
        public uint Size;
        public bool PerPokemon;     // 24-slot indexed (0x00-0x17)
    }

    public class StockFunction
    {
        public string Name;
        public string Category;     // "Damage", "Status", "Stat", "Weather", "Terrain", "Type", "Heal", "Other"
        public uint Offset;         // Absolute file offset
        public int Size;
        public byte[] Code;
        public string Disassembly;
        public string Description;
        public List<uint> InternalCalls = new(); // BL targets within this function
        public List<string> Parameters = new();  // "r0 = attacker index", "r1 = defender index", etc.
    }

    public class DecompiledCRO
    {
        public string SourcePath;
        public bool IsExpanded;
        public CROHeader Header = new();
        public CROSegmentEntry[] Segments = new CROSegmentEntry[4];
        public byte[] RawData;

        public MechanicTable MoveTable;
        public MechanicTable AbilityTable;
        public MechanicTable ItemTable;

        public List<CRORelocationEntry> Relocations = new();
        public List<BSSSlot> BSSAllocations = new();
        public List<StockFunction> StockFunctions = new();

        // Segment starts for quick lookup
        public uint[] SegmentStarts => new[]
        {
            Segments[0]?.Start ?? 0,
            Segments[1]?.Start ?? 0,
            Segments[2]?.Start ?? 0,
            Segments[3]?.Start ?? 0
        };

        public uint CodeEnd => Header.CodeStart + Header.CodeSize;
        public uint RodataStart => CodeEnd;
        public uint RodataEnd => Header.DataStart;

        public int FindRelocationByWriteTo(uint absoluteWriteTo)
        {
            var starts = SegmentStarts;
            for (int i = 0; i < Relocations.Count; i++)
            {
                if (Relocations[i].AbsoluteWriteTo(starts) == absoluteWriteTo)
                    return i;
            }
            return -1;
        }

        public int FindRelocationByTarget(uint absoluteTarget)
        {
            var starts = SegmentStarts;
            for (int i = 0; i < Relocations.Count; i++)
            {
                if (Relocations[i].AbsoluteTarget(starts) == absoluteTarget)
                    return i;
            }
            return -1;
        }

        public List<int> FindAllRelocationsByTarget(uint absoluteTarget)
        {
            var results = new List<int>();
            var starts = SegmentStarts;
            for (int i = 0; i < Relocations.Count; i++)
            {
                if (Relocations[i].AbsoluteTarget(starts) == absoluteTarget)
                    results.Add(i);
            }
            return results;
        }

        public uint FindFreeCodeSpace(int bytesNeeded)
        {
            uint searchStart = Header.CodeStart + Header.CodeSize - (uint)bytesNeeded - 0x100;
            // Scan backwards from code end for 0xCC-filled region
            for (uint i = searchStart; i > Header.CodeStart; i -= 4)
            {
                bool free = true;
                for (int j = 0; j < bytesNeeded && i + j < RawData.Length; j++)
                {
                    if (RawData[i + j] != 0xCC && RawData[i + j] != 0x00)
                    { free = false; break; }
                }
                if (free) return i;
            }
            return 0; // No free space found
        }

        public uint FindFreeDataSpace(int bytesNeeded)
        {
            uint searchStart = Header.DataStart + Header.DataSize - (uint)bytesNeeded;
            for (uint i = searchStart; i > Header.DataStart; i -= 4)
            {
                bool free = true;
                for (int j = 0; j < bytesNeeded && i + j < RawData.Length; j++)
                {
                    if (RawData[i + j] != 0xCC && RawData[i + j] != 0x00)
                    { free = false; break; }
                }
                if (free) return i;
            }
            return 0;
        }
    }
}
