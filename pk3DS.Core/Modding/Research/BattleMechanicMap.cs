using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>One mechanic, fully resolved: what it is, what runs, and when.</summary>
public sealed class MappedMechanic
{
    public CustomMechanicKind Kind { get; init; }
    public uint Id { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Offset of this entry's 8-byte record in the master table.</summary>
    public uint EntryOffset { get; init; }
    /// <summary>Where the master table points.</summary>
    public uint HandlerOffset { get; init; }
    public ResolvedChain Chain { get; init; }

    public bool HasTimingTable => Chain?.HasTimingTable == true;
    public IReadOnlyList<TimingSlot> Slots => Chain?.Slots ?? (IReadOnlyList<TimingSlot>)Array.Empty<TimingSlot>();

    public override string ToString() =>
        $"{Kind} 0x{Id:X3} {Name} -> 0x{HandlerOffset:X6} ({Chain?.Shape}, {Slots.Count} timing slot(s))";
}

/// <summary>
/// A Battle.cro resolved end to end: master tables located by content, handlers walked through to
/// their timing tables and effect functions.
/// <para>
/// This is the entry point everything else should build on. It deliberately does not depend on
/// <see cref="CRODecompiler"/>'s own table discovery, which was verified to misidentify the tables
/// (reporting zero moves, and an ability table 0x2D18 away from the real one) on stock US/UM as
/// well as on modified ROMs. Locating by id fingerprint instead works on any build, and was
/// checked against vanilla US, vanilla UM, and an Expansion Pack ROM whose tables sit 0x20000
/// further in.
/// </para>
/// </summary>
public sealed class BattleMechanicMap
{
    public byte[] Rom { get; }
    public DecompiledCRO Cro { get; }
    public List<LocatedMechanicTable> Tables { get; } = [];
    public List<MappedMechanic> Mechanics { get; } = [];
    public List<string> Diagnostics { get; } = [];

    private readonly Dictionary<uint, uint> _slotToTarget = [];

    private BattleMechanicMap(byte[] rom, DecompiledCRO cro)
    {
        Rom = rom;
        Cro = cro;
    }

    /// <summary>Absolute address a relocation writes into <paramref name="slot"/>, or 0.</summary>
    public uint ResolveSlot(uint slot) => _slotToTarget.TryGetValue(slot, out var v) ? v : 0;

    /// <summary>All pointer slots filled by relocations.</summary>
    public IReadOnlyCollection<uint> RelocatedSlots => _slotToTarget.Keys;

    /// <summary>Builds the map for a Battle.cro image.</summary>
    public static BattleMechanicMap Build(byte[] rom, ResearchDatabase db, string sourcePath = null)
    {
        if (rom == null) throw new ArgumentNullException(nameof(rom));

        DecompiledCRO cro;
        try { cro = CRODecompiler.DecompileStructure((byte[])rom.Clone(), sourcePath); }
        catch (Exception ex)
        {
            var failed = new BattleMechanicMap(rom, null);
            failed.Diagnostics.Add($"CRO structure could not be read: {ex.GetType().Name}: {ex.Message}");
            return failed;
        }

        var map = new BattleMechanicMap(rom, cro);
        uint[] segs = cro.SegmentStarts;
        foreach (var r in cro.Relocations)
            map._slotToTarget[r.AbsoluteWriteTo(segs)] = r.AbsoluteTarget(segs);

        if (db == null)
        {
            map.Diagnostics.Add("no research database supplied; master tables cannot be located");
            return map;
        }

        var slots = new HashSet<uint>(map._slotToTarget.Keys);
        map.Tables.AddRange(MechanicTableLocator.LocateAll(rom, db, slots));

        if (map.Tables.Count == 0)
            map.Diagnostics.Add("no master tables matched their documented id fingerprints in this ROM");

        foreach (var table in map.Tables)
        {
            if (table.Kind == null)
            {
                map.Diagnostics.Add($"'{table.Name}' located but is not a move/ability/item table; skipped");
                continue;
            }

            foreach (var (id, name, entryOffset, handler) in
                     MechanicTableLocator.ReadEntries(rom, table, map.ResolveSlot))
            {
                map.Mechanics.Add(new MappedMechanic
                {
                    Kind = table.Kind.Value,
                    Id = id,
                    Name = name,
                    EntryOffset = entryOffset,
                    HandlerOffset = handler,
                    Chain = handler == 0 ? null : MechanicChainResolver.Resolve(rom, handler, map.ResolveSlot),
                });
            }
        }

        return map;
    }

    #region Queries

    public IEnumerable<MappedMechanic> OfKind(CustomMechanicKind kind) => Mechanics.Where(m => m.Kind == kind);

    public MappedMechanic Find(CustomMechanicKind kind, uint id) =>
        Mechanics.FirstOrDefault(m => m.Kind == kind && m.Id == id);

    public MappedMechanic Find(CustomMechanicKind kind, string name) =>
        Mechanics.FirstOrDefault(m => m.Kind == kind && !string.IsNullOrEmpty(m.Name) &&
                                      m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every timing byte in use, with how often it appears — the practical vocabulary.</summary>
    public Dictionary<byte, int> TimingHistogram()
    {
        var hist = new Dictionary<byte, int>();
        foreach (var slot in Mechanics.SelectMany(m => m.Slots))
            hist[slot.Timing] = hist.GetValueOrDefault(slot.Timing) + 1;
        return hist;
    }

    public string Summary()
    {
        var lines = new List<string>();
        foreach (var t in Tables)
            lines.Add($"{t}");
        foreach (var kind in new[] { CustomMechanicKind.Move, CustomMechanicKind.Ability, CustomMechanicKind.Item })
        {
            var all = OfKind(kind).ToList();
            if (all.Count == 0) continue;
            int withTable = all.Count(m => m.HasTimingTable);
            int direct = all.Count(m => m.Chain?.Shape == HandlerShape.DirectRoutine);
            int unresolved = all.Count(m => m.Chain == null || m.Chain.Shape == HandlerShape.Unknown);
            lines.Add($"{kind}: {all.Count} entries - {withTable} with timing tables, {direct} direct routines, {unresolved} unresolved, {all.Sum(m => m.Slots.Count)} timing slots");
        }
        lines.AddRange(Diagnostics.Select(d => "! " + d));
        return string.Join(Environment.NewLine, lines);
    }

    #endregion
}
