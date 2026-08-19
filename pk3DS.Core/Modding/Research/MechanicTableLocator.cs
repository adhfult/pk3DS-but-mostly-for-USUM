using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>A master table located inside a specific ROM.</summary>
public sealed class LocatedMechanicTable
{
    public string Name { get; init; } = "";
    public CustomMechanicKind? Kind { get; init; }
    /// <summary>In-file offset of the first 8-byte entry in THIS ROM.</summary>
    public uint TableOffset { get; init; }
    /// <summary>Entries actually present in this ROM (may exceed the documented count).</summary>
    public int EntryCount { get; init; }
    /// <summary>How many of the compared ids matched, and how many were compared.</summary>
    public int Matched { get; init; }
    public int Compared { get; init; }
    /// <summary>Offset documented for the reference ROM, for comparison.</summary>
    public uint DocumentedOffset { get; init; }
    /// <summary>Shift between the documented and actual location.</summary>
    public long Shift => (long)TableOffset - DocumentedOffset;

    public double Confidence => Compared == 0 ? 0 : (double)Matched / Compared;

    /// <summary>id -> documented display name, for labelling entries in the editor.</summary>
    public Dictionary<uint, string> Names { get; init; } = [];

    public override string ToString() =>
        $"{Name} @0x{TableOffset:X} ({EntryCount} entries, {Matched}/{Compared} id match, shift {(Shift >= 0 ? "+" : "-")}0x{Math.Abs(Shift):X})";
}

/// <summary>
/// Finds the Move/Ability/Item master tables inside a Battle.cro by content, not by address.
/// <para>
/// Each entry is <c>[id:u32][handler:u32]</c>. Handler pointers are relocated and therefore differ
/// between builds, but the id column is game data and stays put, so the documented id sequence
/// acts as a fingerprint. Searching for it locates the table wherever a particular build happens
/// to have put it — verified to land on the documented offsets in vanilla US/UM, and to track the
/// Expansion Pack's +0x20000 shift automatically.
/// </para>
/// </summary>
public static class MechanicTableLocator
{
    /// <summary>Number of leading ids compared. Long enough to be unique, short enough to be quick.</summary>
    private const int FingerprintLength = 48;

    /// <summary>Fraction of compared ids that must match for a hit to be accepted.</summary>
    private const double AcceptThreshold = 0.75;

    /// <summary>
    /// Locates every documented master table within <paramref name="rom"/>.
    /// <para>
    /// Supplying <paramref name="relocatedSlots"/> — the set of absolute addresses written by
    /// relocation patches — makes the entry count exact. Handler pointers are zero on disk and
    /// filled in at load time, so "this slot has a relocation" is the only reliable test of
    /// whether an 8-byte record is a real table entry or the data that happens to follow it.
    /// </para>
    /// </summary>
    public static List<LocatedMechanicTable> LocateAll(byte[] rom, ResearchDatabase db, ISet<uint> relocatedSlots = null)
    {
        var results = new List<LocatedMechanicTable>();
        if (rom == null || db == null) return results;

        foreach (var index in db.MechanicIndexes)
        {
            var found = Locate(rom, index, relocatedSlots);
            if (found != null) results.Add(found);
        }
        return results;
    }

    /// <summary>Locates one documented table, or null when it can't be found confidently.</summary>
    public static LocatedMechanicTable Locate(byte[] rom, ResearchMechanicIndex index, ISet<uint> relocatedSlots = null)
    {
        if (rom == null || index == null || index.Entries.Count == 0) return null;

        uint[] want = index.Fingerprint;
        int n = Math.Min(FingerprintLength, want.Length);
        if (n < 8) return null; // too short to identify anything reliably

        int need = (int)Math.Ceiling(n * AcceptThreshold);
        uint first = want[0];

        uint bestAt = 0;
        int bestMatched = -1;

        // Anchor on the first id to avoid scoring every 4-byte position in a 1.4 MB file.
        for (int at = 0; at + n * 8 <= rom.Length; at += 4)
        {
            if (BitConverter.ToUInt32(rom, at) != first) continue;

            int matched = 0;
            for (int k = 0; k < n; k++)
                if (BitConverter.ToUInt32(rom, at + k * 8) == want[k]) matched++;

            if (matched > bestMatched) { bestMatched = matched; bestAt = (uint)at; }
            if (matched == n) break; // exact - stop looking
        }

        if (bestMatched < need) return null;

        return new LocatedMechanicTable
        {
            Name = index.Name,
            Kind = index.Kind,
            TableOffset = bestAt,
            EntryCount = CountEntries(rom, bestAt, index, relocatedSlots),
            Matched = bestMatched,
            Compared = n,
            DocumentedOffset = index.DocumentedTableOffset,
            Names = BuildNameMap(index),
        };
    }

    /// <summary>
    /// Counts entries actually present, allowing a ROM to hold more than the reference did (which
    /// is exactly what an expansion mod produces). Stops at the first entry that looks like it
    /// isn't part of the table any more.
    /// </summary>
    /// <summary>
    /// Counts entries actually present, allowing a ROM to hold more than the reference did.
    /// <para>
    /// Termination is by <em>duplicate id</em>, not by running out of relocations. A master index
    /// maps each move/ability/item at most once, whereas the data immediately following it — the
    /// timing tables — is also made of 8-byte records with a relocated pointer at +4. Walking on
    /// "the pointer slot is relocated" alone therefore runs straight off the end of the table and
    /// into the timing data: measured against stock UM that inflated abilities from 226 entries to
    /// 403 and moves from 343 to 421, and the bogus tail entries took their "ids" from timing
    /// bytes. Stopping at the first repeat gives exactly the real table on every ROM checked.
    /// </para>
    /// </summary>
    private static int CountEntries(byte[] rom, uint tableOffset, ResearchMechanicIndex index, ISet<uint> relocatedSlots)
    {
        int max = Math.Min(Math.Max(index.Entries.Count * 4, 64), (rom.Length - (int)tableOffset) / 8);
        var seen = new HashSet<uint>();

        int count = 0;
        for (int k = 0; k < max; k++)
        {
            uint at = tableOffset + (uint)(k * 8);
            uint id = BitConverter.ToUInt32(rom, (int)at);

            // Ids are 16-bit in practice; anything larger is not an index entry.
            if (id > 0xFFFF) break;
            // An id that has already appeared means we've left the table.
            if (!seen.Add(id)) break;
            // When relocation data is available, a real entry always has its handler slot filled.
            if (relocatedSlots != null && !relocatedSlots.Contains(at + 4)) break;

            count++;
        }
        return count;
    }

    /// <summary>
    /// Reads a located table out of a ROM, resolving each entry's handler through the relocation
    /// that fills its pointer slot.
    /// </summary>
    /// <param name="resolveHandler">
    /// Maps the absolute address of a pointer slot to the address it will hold at run time —
    /// normally a lookup into the CRO's relocation table.
    /// </param>
    public static List<(uint Id, string Name, uint EntryOffset, uint Handler)> ReadEntries(
        byte[] rom, LocatedMechanicTable table, Func<uint, uint> resolveHandler = null)
    {
        var list = new List<(uint, string, uint, uint)>();
        if (rom == null || table == null) return list;

        for (int k = 0; k < table.EntryCount; k++)
        {
            uint at = table.TableOffset + (uint)(k * 8);
            if (at + 8 > rom.Length) break;

            uint id = BitConverter.ToUInt32(rom, (int)at);
            uint handler = resolveHandler?.Invoke(at + 4) ?? BitConverter.ToUInt32(rom, (int)at + 4);
            table.Names.TryGetValue(id, out string name);
            list.Add((id, name ?? "", at, handler));
        }
        return list;
    }

    private static Dictionary<uint, string> BuildNameMap(ResearchMechanicIndex index)
    {
        var map = new Dictionary<uint, string>();
        foreach (var e in index.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.Name)) continue;
            map.TryAdd(e.Id, e.Name.Trim());
        }
        return map;
    }
}
