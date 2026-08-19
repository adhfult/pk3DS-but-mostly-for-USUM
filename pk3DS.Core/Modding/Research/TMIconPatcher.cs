using System;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Gives the expansion's new TM items a TM icon instead of a blank one.
/// <para>
/// The Expansion Pack replaced the item-icon lookup with a small added routine. It maps item IDs
/// 960-1023 arithmetically onto its added icons, and sends everything at or above 1024 to a single
/// hard-coded fallback:
/// </para>
/// <code>
///   CMP    R2, #0x3C0     ; item &gt;= 960?
///   BCC    legacy         ;   no  -&gt; normal icon table
///   CMP    R2, #0x400     ; item &gt;= 1024?
///   SUBCC  R1, R2, #0xBF  ;   960..1023 -&gt; added icon (769..832)
///   BXCC   LR
///   MOV    R1, #0x300     ;   &gt;= 1024 -&gt; icon 768
///   BX     LR
/// </code>
/// <para>
/// Icon 768 is what item 0 - the "nothing" item - uses, i.e. the blank slot, so TM101-TM128 would
/// appear in the bag with no icon at all. Retargeting that one immediate at a real TM icon costs a
/// single word and cannot affect any item below 1024, because the instruction is only reached once
/// both preceding comparisons have already excluded them.
/// </para>
/// </summary>
public static class TMIconPatcher
{
    /// <summary>Icon index the normal table gives a status TM (item 328, TM01).</summary>
    public const uint StatusTMIcon = 312;

    /// <summary>Icon index for a physical TM (item 329, TM02).</summary>
    public const uint PhysicalTMIcon = 310;

    /// <summary>Icon index for a special TM (item 330, TM03).</summary>
    public const uint SpecialTMIcon = 311;

    /// <summary>The blank icon, used by item 0 and by the untouched fallback.</summary>
    public const uint BlankIcon = 0x300;

    /// <summary>Offset of the fallback instruction. Same address in both US and UM builds.</summary>
    private const int FallbackOffset = 0x4B9B54;

    /// <summary>What the fallback must currently be for the patch to apply: <c>MOV R1, #0x300</c>.</summary>
    private const uint ExpectedFallback = 0xE3A01C03;

    public sealed class Result
    {
        public bool Applied { get; init; }
        public string Message { get; init; } = "";
        public uint PreviousIcon { get; init; }
        public uint NewIcon { get; init; }
        public override string ToString() => Message;
    }

    /// <summary>
    /// Points the "item id 1024 or above" icon fallback at <paramref name="icon"/>. Mutates
    /// <paramref name="code"/> in place. Verified three ways before writing: the instruction must
    /// be exactly the expected MOV, the requested icon must be ARM-encodable, and the rebuilt word
    /// must decode back to the requested value.
    /// </summary>
    public static Result Retarget(byte[] code, uint icon = StatusTMIcon)
    {
        if (code == null || FallbackOffset + 4 > code.Length)
            return new Result { Message = "code binary too small to contain the icon lookup" };

        uint current = BitConverter.ToUInt32(code, FallbackOffset);
        if (current != ExpectedFallback)
        {
            // Already retargeted by a previous run is not a failure; anything else is.
            if ((current & 0xFFFFF000) == (ExpectedFallback & 0xFFFFF000))
            {
                uint existing = ARMCodec.DecodeImm8r4((current >> 8) & 0xF, current & 0xFF);
                return existing == icon
                    ? new Result { Applied = false, PreviousIcon = existing, NewIcon = icon, Message = $"icon fallback already points at {icon}" }
                    : new Result { Applied = false, PreviousIcon = existing, NewIcon = icon, Message = $"icon fallback holds an unexpected value ({existing}); left alone" };
            }
            return new Result { Message = $"no icon fallback at 0x{FallbackOffset:X}; this build's lookup differs" };
        }

        var enc = ARMCodec.EncodeImm8r4(icon);
        if (enc == null || ARMCodec.DecodeImm8r4(enc.Value.rot, enc.Value.imm8) != icon)
            return new Result { Message = $"icon {icon} is not encodable as an ARM immediate" };

        uint rebuilt = (current & 0xFFFFF000) | ((enc.Value.rot & 0xF) << 8) | (enc.Value.imm8 & 0xFF);
        if (ARMCodec.DecodeImm8r4((rebuilt >> 8) & 0xF, rebuilt & 0xFF) != icon)
            return new Result { Message = $"rebuilt instruction does not decode to {icon}; refused" };

        BitConverter.GetBytes(rebuilt).CopyTo(code, FallbackOffset);
        return new Result
        {
            Applied = true,
            PreviousIcon = BlankIcon,
            NewIcon = icon,
            Message = $"TM101-128 icon fallback: {BlankIcon} (blank) -> {icon}",
        };
    }
}
