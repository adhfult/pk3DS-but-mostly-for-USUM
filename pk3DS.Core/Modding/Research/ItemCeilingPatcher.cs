using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>One item-ID bounds check found in a code binary.</summary>
public sealed class ItemCeilingSite
{
    public int Offset { get; init; }
    public int Register { get; init; }
    public uint Ceiling { get; init; }
    /// <summary>Instruction immediately after, for context in reports.</summary>
    public string FollowedBy { get; init; } = "";
    /// <summary>Why this offset was accepted as an item check.</summary>
    public string Reason { get; init; } = "";
    public uint NewCeiling { get; set; }
    public bool Patched { get; set; }

    public override string ToString() =>
        $"0x{Offset:X7} CMP R{Register}, #0x{Ceiling:X}" +
        (Patched ? $" -> #0x{NewCeiling:X}" : "") +
        (string.IsNullOrEmpty(FollowedBy) ? "" : $"   ({FollowedBy})") +
        (string.IsNullOrEmpty(Reason) ? "" : $"  [{Reason}]");
}

/// <summary>Outcome of a ceiling raise.</summary>
public sealed class ItemCeilingResult
{
    public List<ItemCeilingSite> Sites { get; } = [];
    public List<string> Notes { get; } = [];
    public uint AppliedCeiling { get; set; }
    public int PatchedCount => Sites.Count(s => s.Patched);

    /// <summary>Sites that already allowed the requested ceiling, so needed no change.</summary>
    public int AlreadySufficient =>
        Sites.Count(s => !s.Patched && s.Ceiling >= AppliedCeiling && AppliedCeiling > 0);

    /// <summary>
    /// True when the ROM ends up accepting the requested item ids - whether this run widened the
    /// checks or a previous one already had.
    /// </summary>
    public bool Success => PatchedCount > 0 || AlreadySufficient > 0;

    public string Describe()
    {
        string headline = PatchedCount > 0
            ? $"item ceiling raised to 0x{AppliedCeiling:X} ({AppliedCeiling}) at {PatchedCount} site(s):"
            : AlreadySufficient > 0
                ? $"item ceiling already at or above 0x{AppliedCeiling:X} ({AppliedCeiling}) at {AlreadySufficient} site(s) - nothing to change:"
                : "no item ceiling check could be located:";
        var lines = new List<string> { headline };
        lines.AddRange(Sites.Select(s => "  " + s));
        lines.AddRange(Notes.Select(n => "  ! " + n));
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Raises the item-ID bounds checks in a Gen 7 code binary so item IDs above the stock limit are
/// accepted.
/// <para>
/// Retail USUM rejects anything at or above 0x3C0 (960), so creating items beyond that — the 28
/// TM items backing TM101-128, for instance — needs these comparisons widened or the game will
/// treat the new IDs as invalid. The Expansion Pack's own notes raise them to 0x400 (1023 max),
/// but that patch is not present in every build: the UM Expansion ROM checked here still held the
/// retail value at all three documented sites, so relying on it would have produced items the game
/// silently refuses.
/// </para>
/// <para>
/// Sites are found by scanning rather than by fixed offset, so this works regardless of build or
/// region. Two guards keep it honest: only <em>unconditional</em> CMPs are considered, and PC
/// (R15) is never accepted as the compared register — both of those signatures come from data
/// being disassembled as code, which is where the false positives in this binary live.
/// </para>
/// </summary>
public static class ItemCeilingPatcher
{
    /// <summary>Stock USUM item ceiling: IDs 0..959 are valid.</summary>
    public const uint RetailCeiling = 0x3C0;

    /// <summary>Ceiling used by Expansion Pack builds that carry the item-limit patch.</summary>
    public const uint ExpansionCeiling = 0x400;

    /// <summary>
    /// Offsets the Expansion Pack notes identify as item-limit checks. Used only as candidates —
    /// each is byte-verified to actually hold a CMP against a known ceiling before being touched,
    /// so a wrong offset (US vs UM layouts differ by a few bytes) is skipped rather than corrupting
    /// whatever happens to live there.
    /// </summary>
    private static readonly int[] DocumentedSites =
    [
        0x276B90,           // item records (same offset in both builds)
        0x2D2B60, 0x2D2B64, // item name text (US / UM)
        0x3BABFC, 0x3BAC04, // field held items (US / UM)
    ];

    /// <summary>
    /// Finds the item bounds checks.
    /// <para>
    /// Only the retail ceiling 0x3C0 is safe to scan for by value. The expanded ceiling 0x400 is
    /// 1024 — far too common a constant to attribute to items: a scan for it across this binary
    /// returns thirty-odd hits that are buffer sizes, alignment tests and unrelated range checks,
    /// and widening those would corrupt logic that has nothing to do with items. So 0x400 sites
    /// are only accepted at offsets the Expansion Pack notes explicitly identify.
    /// </para>
    /// </summary>
    public static List<ItemCeilingSite> Analyze(byte[] code)
    {
        var sites = new List<ItemCeilingSite>();
        if (code == null) return sites;

        var taken = new HashSet<int>();

        for (int i = 0; i + 8 <= code.Length; i += 4)
        {
            if (!TryReadCeiling(code, i, out int rn, out uint imm)) continue;
            if (imm != RetailCeiling) continue;
            taken.Add(i);
            bool added = IsInsideAddedCode((uint)i);
            sites.Add(Make(code, i, rn, imm,
                added ? "inside Expansion Pack added code - not a bounds check, left alone"
                      : "retail ceiling found by scan"));
        }

        foreach (int ofs in DocumentedSites)
        {
            if (ofs + 8 > code.Length || taken.Contains(ofs)) continue;
            if (!TryReadCeiling(code, ofs, out int rn, out uint imm)) continue;
            if (imm <= RetailCeiling) continue;
            taken.Add(ofs);
            sites.Add(Make(code, ofs, rn, imm,
                imm == ExpansionCeiling
                    ? "documented item-limit site"
                    : $"documented item-limit site, already raised to 0x{imm:X}"));
        }

        return sites.OrderBy(s => s.Offset).ToList();
    }

    /// <summary>
    /// Whether an offset lies in a region the code map records as space the Expansion Pack wrote
    /// new code into. Such a region is hand-written logic whose constants mean whatever its author
    /// intended, so no value-based rule may be applied to it.
    /// </summary>
    private static bool IsInsideAddedCode(uint offset)
    {
        if (offset is >= 0x4B9B40 and < 0x4B9BA8) return true;

        try
        {
            foreach (var entry in ExpansionCodeMap.GetEntriesForFile("code.bin"))
            {
                if (entry.IsAddedSpace && entry.Contains(offset)) return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>Reads an unconditional CMP Rn,#imm that isn't comparing PC.</summary>
    private static bool TryReadCeiling(byte[] code, int offset, out int rn, out uint imm)
    {
        rn = 0; imm = 0;
        uint w = BitConverter.ToUInt32(code, offset);
        // Condition must be AL (0xE); a conditional CMP here is data disassembled as code.
        if ((w & 0xFFF00000) != 0xE3500000) return false;
        rn = (int)((w >> 16) & 0xF);
        if (rn == 15) return false; // comparing PC: data, not a real check
        imm = ARMCodec.DecodeImm8r4((w >> 8) & 0xF, w & 0xFF);
        return true;
    }

    private static ItemCeilingSite Make(byte[] code, int offset, int rn, uint imm, string why) => new()
    {
        Offset = offset,
        Register = rn,
        Ceiling = imm,
        Reason = why,
        FollowedBy = ARMCodec.DisassembleWord(BitConverter.ToUInt32(code, offset + 4), (uint)offset + 4),
    };

    /// <summary>
    /// Encodes <paramref name="value"/> as an ARM 8-bit rotated constant, verifying by decoding the
    /// result back.
    /// <para>
    /// The round-trip is not redundant. ARM rotations are by an even number of bits, so a value
    /// whose lowest set bit sits at an odd position — 1056 (0x420), bits 10 and 5 — cannot be
    /// represented at all, yet <see cref="ARMCodec.EncodeImm8r4"/> returns a result for it. Writing
    /// that unverified produced <c>CMP R0, #0x20000004</c> in place of the intended ceiling.
    /// Accepting only encodings that decode back to the original value sidesteps that entirely.
    /// </para>
    /// </summary>
    public static (uint rot, uint imm8)? TryEncode(uint value)
    {
        var enc = ARMCodec.EncodeImm8r4(value);
        if (enc == null) return null;
        return ARMCodec.DecodeImm8r4(enc.Value.rot, enc.Value.imm8) == value ? enc : null;
    }

    /// <summary>
    /// Smallest immediate at least <paramref name="minimum"/> that is genuinely encodable. CMP
    /// cannot hold arbitrary values: covering item id 1051 needs a ceiling of 1052, which is not
    /// representable (its set bits span 9 positions), so the search settles on 1056 — 0x420 is
    /// 0x42 rotated right by 28, which fits the 8-bit-plus-even-rotation form.
    /// </summary>
    public static uint SmallestEncodableAtLeast(uint minimum)
    {
        for (uint v = minimum; v < minimum + 0x2000; v++)
            if (TryEncode(v) != null) return v;
        return 0;
    }

    /// <summary>
    /// Widens the item bounds checks in <paramref name="code"/> so <paramref name="requiredMaxItemId"/>
    /// is a valid ID. Mutates the array in place. Sites already high enough are left alone.
    /// </summary>
    public static ItemCeilingResult Raise(byte[] code, int requiredMaxItemId)
    {
        var result = new ItemCeilingResult();
        if (code == null) { result.Notes.Add("no code supplied"); return result; }
        if (requiredMaxItemId < 0) { result.Notes.Add("required item id must be non-negative"); return result; }

        // The checks reject IDs >= ceiling, so the ceiling must exceed the highest ID in use.
        uint needed = (uint)requiredMaxItemId + 1;
        uint ceiling = SmallestEncodableAtLeast(needed);
        if (ceiling == 0)
        {
            result.Notes.Add($"no ARM-encodable immediate at or above {needed}");
            return result;
        }
        if (ceiling != needed)
            result.Notes.Add($"{needed} is not encodable as an ARM immediate; using {ceiling} (0x{ceiling:X}) instead");

        result.AppliedCeiling = ceiling;

        var encoded = TryEncode(ceiling);
        if (encoded == null) { result.Notes.Add($"0x{ceiling:X} could not be encoded as an ARM immediate"); return result; }

        foreach (var site in Analyze(code))
        {
            result.Sites.Add(site);

            if (IsInsideAddedCode((uint)site.Offset))
            {
                result.Notes.Add($"0x{site.Offset:X7} is inside Expansion Pack added code; not widened " +
                                 "(its comparison selects behaviour, it does not reject out-of-range IDs)");
                continue;
            }

            if (site.Ceiling >= ceiling)
            {
                result.Notes.Add($"0x{site.Offset:X7} already allows up to {site.Ceiling - 1}; left unchanged");
                continue;
            }

            uint w = BitConverter.ToUInt32(code, site.Offset);
            uint rebuilt = (w & 0xFFFFF000) | ((encoded.Value.rot & 0xF) << 8) | (encoded.Value.imm8 & 0xFF);

            // Re-decode the instruction we are about to write and confirm it still says what we
            // mean. A bad immediate here would silently turn a bounds check into nonsense.
            uint check = ARMCodec.DecodeImm8r4((rebuilt >> 8) & 0xF, rebuilt & 0xFF);
            if (check != ceiling)
            {
                result.Notes.Add($"0x{site.Offset:X7} SKIPPED: rebuilt instruction decodes to 0x{check:X}, not 0x{ceiling:X}");
                continue;
            }

            BitConverter.GetBytes(rebuilt).CopyTo(code, site.Offset);
            site.NewCeiling = ceiling;
            site.Patched = true;
        }

        if (result.PatchedCount == 0 && result.Sites.Count == 0)
            result.Notes.Add("no item bounds checks were found; the binary may already use a different ceiling");

        return result;
    }
}
