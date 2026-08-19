using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>Which binary a record targets. Resolved per-sheet, never guessed per-row.</summary>
public enum ResearchTarget { Unknown = 0, BattleCro, BagCro, ShopCro, BoxCro, StatusCro, FieldRoCro, CodeBin, EvolutionCro }

/// <summary>The kinds of sheet the research workbooks actually contain.</summary>
public enum ResearchSheetKind
{
    Unknown = 0,
    PatchList,          // offset + hex/assembly -> raw byte writes
    RelocationTracker,  // Custom Relocation Patches: patch address / write-to / pointer / segments
    FunctionIndex,      // Table of stock functions: offset -> name/description
    TableRegistry,      // Table locations and sizes: original vs edited table addresses + counts
    FreeSpaceRegistry,  // Table of my custom functions: offset / length / room
    TimingFlags,        // Flags sheet: timing byte -> meaning
    MechanicIndex,      // Move/Ability/Item Index: the master table, entry by entry
    Reference,          // type charts, text dumps, notes - kept for lookup, never applied
}

/// <summary>Where a record came from, so anything surprising can be traced back to a cell.</summary>
public readonly record struct ResearchOrigin(string File, string Sheet, int Row)
{
    public override string ToString() =>
        $"{System.IO.Path.GetFileName(File)}[{Sheet}]:{Row + 1}";
}

/// <summary>A raw byte write at a fixed offset.</summary>
public sealed class ResearchPatch
{
    public uint Offset { get; set; }
    public byte[] Bytes { get; set; } = [];
    public string Assembly { get; set; }
    public string Note { get; set; }
    public ResearchOrigin Origin { get; set; }
    public string HexBytes => Bytes == null ? "" : Convert.ToHexString(Bytes);
}

/// <summary>
/// One CRO relocation patch: "write the address of <see cref="Pointer"/> into <see cref="WriteTo"/>".
/// This is the unit ABZB's workflow is built on and the thing the old cache never captured.
/// </summary>
public sealed class ResearchRelocation
{
    public string Category { get; set; }        // "Table Pointer", "Function Table Pointer", ...
    public string Specific { get; set; }        // "Light Ball", "Ignorable Abilities", ...
    public string TargetNote { get; set; }
    public uint PatchAddress { get; set; }      // where the 0xC-byte patch entry itself lives
    public uint WriteTo { get; set; }           // address the pointer gets written to
    public uint Pointer { get; set; }           // address being pointed at
    public int WriteSegment { get; set; } = -1;
    public int PointerSegment { get; set; } = -1;
    public bool IsBss { get; set; }
    public ResearchTarget Target { get; set; }
    public ResearchOrigin Origin { get; set; }
}

/// <summary>A documented function in a binary: the symbol table for disassembly.</summary>
public sealed class ResearchFunction
{
    public uint Offset { get; set; }            // in-file offset
    public uint LoadedAddress { get; set; }     // runtime address, when documented
    public string Name { get; set; }
    public string Details { get; set; }
    public ResearchTarget Target { get; set; }
    public ResearchOrigin Origin { get; set; }
}

/// <summary>
/// A master index table (Move/Ability/Item/Field-Effect) — where the bounds-check lives, where the
/// table data lives, and how many entries it holds before vs. after expansion.
/// <para>
/// Adding a wholly new move/ability/item means appending to the table data AND raising the entry
/// count the bounds-check compares against, so both halves are tracked together here.
/// </para>
/// </summary>
public sealed class ResearchTableLocation
{
    public string Name { get; set; }                 // "Ability Index", "Move Index", ...

    /// <summary>Address of the bounds-check instruction that gates this table.</summary>
    public uint CodeLocation { get; set; }
    /// <summary>Address holding the comparison operand (the entry-count immediate).</summary>
    public uint EntryParamAddress { get; set; }
    /// <summary>Conditional used by the bounds check ("bcc", "bhi", ...), for reference.</summary>
    public string BoundsCondition { get; set; }

    /// <summary>Entry count in an untouched ROM.</summary>
    public int OriginalEntryCount { get; set; } = -1;
    /// <summary>Entry count after expansion, when the sheet records one.</summary>
    public int EditedEntryCount { get; set; } = -1;

    /// <summary>Table data address in an untouched ROM.</summary>
    public uint OriginalTableData { get; set; }
    /// <summary>Table data address after relocation, when the sheet records one.</summary>
    public uint EditedTableData { get; set; }

    public ResearchTarget Target { get; set; }
    public ResearchOrigin Origin { get; set; }

    /// <summary>Where the table data actually lives now — edited address when set, else original.</summary>
    public uint EffectiveTableData => EditedTableData != 0 ? EditedTableData : OriginalTableData;
    /// <summary>How many entries it actually holds now — edited count when set, else original.</summary>
    public int EffectiveEntryCount => EditedEntryCount >= 0 ? EditedEntryCount : OriginalEntryCount;
}

/// <summary>A region known to be free for custom code/data, from the custom-function tracker.</summary>
public sealed class ResearchFreeSpace
{
    public uint Offset { get; set; }
    public int Length { get; set; }
    public int Room { get; set; }               // bytes still unused at this offset
    public string Function { get; set; }        // what already lives there, when anything does
    public string Details { get; set; }
    public ResearchTarget Target { get; set; }
    public ResearchOrigin Origin { get; set; }
}

/// <summary>
/// A self-contained ARM routine written with offsets relative to its own start (the sheets write
/// these as an address column restarting at 0), so it can be placed at whatever address the
/// allocator picks.
/// <para>
/// These are the raw material for "add a custom function": the research sheets already contain
/// dozens of complete, working routines that were previously discarded outright because their
/// address column read 0 and the old parser only understood absolute patches.
/// </para>
/// </summary>
public sealed class ResearchFunctionBody
{
    public string Name { get; set; }
    /// <summary>Machine code, contiguous from relative offset 0.</summary>
    public byte[] Code { get; set; } = [];
    /// <summary>Per-instruction disassembly text as written by the researcher, if present.</summary>
    public List<string> Assembly { get; } = [];
    public ResearchTarget Target { get; set; }
    public ResearchOrigin Origin { get; set; }
    public int Size => Code?.Length ?? 0;
}

/// <summary>One row of a master index table: which move/ability/item, and its handler.</summary>
public sealed class ResearchMechanicEntry
{
    /// <summary>Move/ability/item ID this entry handles (the first u32 of the 8-byte entry).</summary>
    public uint Id { get; set; }
    public string Name { get; set; }
    /// <summary>In-file offset of the handler, as documented for the reference ROM.</summary>
    public uint HandlerFileOffset { get; set; }
    /// <summary>In-file offset of this 8-byte entry, as documented for the reference ROM.</summary>
    public uint EntryFileOffset { get; set; }
}

/// <summary>
/// A whole master index table as documented, entry by entry.
/// <para>
/// The entries are 8 bytes — <c>[id:u32][handler pointer:u32]</c> — and while the pointers are
/// relocated (so they differ between ROMs), the <em>sequence of ids</em> is stable. That makes the
/// id sequence a fingerprint which locates the same table in any Battle.cro regardless of how the
/// file has been expanded or shifted, without depending on any hardcoded address.
/// </para>
/// </summary>
public sealed class ResearchMechanicIndex
{
    public string Name { get; set; }                    // "Move Index", "Ability Index", ...
    public CustomMechanicKind? Kind { get; set; }
    public uint DocumentedTableOffset { get; set; }     // where it sat in the reference ROM
    public List<ResearchMechanicEntry> Entries { get; } = [];
    public ResearchTarget Target { get; set; }
    public ResearchOrigin Origin { get; set; }

    /// <summary>The id sequence used as the search fingerprint.</summary>
    public uint[] Fingerprint => Entries.Select(e => e.Id).ToArray();
}

/// <summary>A timing byte and what it means, for the function-table entries.</summary>
public sealed class ResearchTiming
{
    public byte Value { get; set; }
    public string Meaning { get; set; }
    public string Examples { get; set; }
    public ResearchOrigin Origin { get; set; }
}

/// <summary>Everything extracted from one worksheet, with its detected kind.</summary>
public sealed class ResearchSheet
{
    public string SourceFile { get; set; }
    public string SheetName { get; set; }
    public string DisplayName { get; set; }
    public string Category { get; set; }        // folder-derived: Move/Ability/Item/Generic/...
    public ResearchSheetKind Kind { get; set; }
    public ResearchTarget Target { get; set; }
    public double Confidence { get; set; }      // 0..1 for the kind classification

    public List<ResearchPatch> Patches { get; } = [];
    public List<ResearchRelocation> Relocations { get; } = [];
    public List<ResearchFunction> Functions { get; } = [];
    public List<ResearchFunctionBody> Bodies { get; } = [];
    public List<ResearchTableLocation> Tables { get; } = [];
    public List<ResearchFreeSpace> FreeSpace { get; } = [];
    public List<ResearchTiming> Timings { get; } = [];
    public List<ResearchMechanicIndex> MechanicIndexes { get; } = [];
    public List<string> Diagnostics { get; } = [];

    public int RecordCount =>
        Patches.Count + Relocations.Count + Functions.Count + Bodies.Count +
        Tables.Count + FreeSpace.Count + Timings.Count +
        MechanicIndexes.Sum(m => m.Entries.Count);
}
