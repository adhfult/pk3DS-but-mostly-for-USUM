#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>What one binary's half of the level cap install did.</summary>
public sealed record LevelCapSite(string Binary, bool Applied, uint BlockOffset, uint HookOffset, string Detail)
{
    public override string ToString() =>
        Applied
            ? $"{Binary}: function at 0x{BlockOffset:X6}, hook at 0x{HookOffset:X6} - {Detail}"
            : $"{Binary}: not applied - {Detail}";
}

/// <summary>
/// Installs the level cap: a routine that answers "may this Pokemon reach this level yet?", plus
/// the two call sites that ask.
/// </summary>
public static class LevelCapPatch
{
    /// <summary>Address of the story-flag block in the loaded save data.</summary>
    public const uint SaveFlagBase = 0x330138D0;

    /// <summary>Bytes of routine before the table starts.</summary>
    public const int PrologueSize = 0x6C;

    /// <summary>Entry point for the battle path, relative to the block.</summary>
    private const int EntryBattle = 0x00;

    /// <summary>Entry point for the Rare Candy path, relative to the block.</summary>
    private const int EntryCandy = 0x0C;

    // The routine, as assembled words. Displacements are fixed because the table always begins at
    // PrologueSize and the literal always sits four bytes before it.
    private static readonly uint[] Routine =
    [
        0xE2800001, // entry_battle: add  r0, r0, #1        ; ask about the level it would reach
        0xE92D407E, //               push {r1-r6, lr}
        0xEA000003, //               b    body
        0xE92D407E, // entry_candy:  push {r1-r6, lr}
        0xE1550000, //               cmp  r5, r0            ; new level == old level?
        0x0A000010, //               beq  deny
        0xE1A00005, //               mov  r0, r5
        0xE3500064, // body:         cmp  r0, #100
        0x8A00000D, //               bhi  deny
        0xE59F403C, //               ldr  r4, [pc, #0x3C]   ; r4 = SaveFlagBase
        0xE28F503C, //               add  r5, pc, #0x3C     ; r5 = table
        0xE5D51000, // loop:         ldrb r1, [r5, #0]      ; flag offset
        0xE7D41001, //               ldrb r1, [r4, r1]      ; flag byte
        0xE5D56001, //               ldrb r6, [r5, #1]      ; flag bit
        0xE0111006, //               ands r1, r1, r6
        0x12855003, //               addne r5, r5, #3       ; set: this rung is behind us
        0x1AFFFFF9, //               bne  loop
        0xE5D56002, //               ldrb r6, [r5, #2]      ; cap of the first unset rung
        0xE1500006, //               cmp  r0, r6
        0x8A000002, //               bhi  deny
        0xE3A00001, //               mov  r0, #1
        0xE3500000, //               cmp  r0, #0            ; allow: Z clear
        0xE8BD807E, //               pop  {r1-r6, pc}
        0xE3A00000, // deny:         mov  r0, #0
        0xE3500000, //               cmp  r0, #0            ; deny: Z set
        0xE8BD807E, //               pop  {r1-r6, pc}
        SaveFlagBase,
    ];

    /// <summary>The three words the battle hook expects to find before it is patched.</summary>
    private static readonly uint[] BattleOriginal =
    [
        0xE3500064, // cmp r0, #100
        0xE320F000, // nop
        0xAA000004, // bge +0x10
    ];

    /// <summary>The four words the Rare Candy hook expects to find before it is patched.</summary>
    private static readonly uint[] CandyOriginal =
    [
        0xE3550064, // cmp   r5, #100
        0x83A05064, // movhi r5, #100
        0xE1550000, // cmp   r5, r0      <- the only word this patch replaces
        0x0A00001E, // beq   +0x78
    ];

    /// <summary>Offset of the exp-calculation hook in Battle.cro. Identical in all four builds.</summary>
    public const uint BattleHook = 0x015AD4;

    /// <summary>Offset of the Rare Candy hook in code.bin. Identical in all four builds.</summary>
    public const uint CandyHook = 0x225ACC;

    /// <summary>The routine plus the table, ready to drop into a binary.</summary>
    public static byte[] BuildBlock(LevelCapTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var tableBytes = table.ToBytes();
        var block = new byte[PrologueSize + tableBytes.Length];
        for (int i = 0; i < Routine.Length; i++)
            BitConverter.GetBytes(Routine[i]).CopyTo(block, i * 4);
        tableBytes.CopyTo(block, PrologueSize);
        return block;
    }

    /// <summary>
    /// Installs both halves. Either may be skipped without affecting the other; passing null for a
    /// binary skips it deliberately.
    /// </summary>
    public static List<LevelCapSite> Install(byte[]? battleCro, byte[]? codeBin, LevelCapTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var problems = table.Validate();
        if (problems.Any(p => p.Contains("outside 1-100") || p.Contains("not a single bit")))
            return [new LevelCapSite("(table)", false, 0, 0, "table is not valid: " + problems[0])];

        var block = BuildBlock(table);
        var sites = new List<LevelCapSite>();

        if (battleCro != null)
            sites.Add(InstallBattle(battleCro, block));
        if (codeBin != null)
            sites.Add(InstallCandy(codeBin, block));

        return sites;
    }

    private static LevelCapSite InstallBattle(byte[] cro, byte[] block)
    {
        if (!WordsMatch(cro, BattleHook, BattleOriginal, out string why))
            return new("Battle.cro", false, 0, BattleHook, why + " - this is not a build the exp hook was written for");

        uint textEnd = CroTextEnd(cro);
        uint? spot = FindFreeSpace(cro, block.Length, textEnd, BattleHook, textEnd);
        if (spot is not { } at)
            return new("Battle.cro", false, 0, BattleHook, $"no run of {block.Length} free bytes inside the code segment");

        block.CopyTo(cro, at);

        // bl entry_battle ; cmp r0,#1 ; bne +0x10
        WriteWord(cro, BattleHook, BranchLink(BattleHook, at + EntryBattle));
        WriteWord(cro, BattleHook + 4, 0xE3500001);
        WriteWord(cro, BattleHook + 8, 0x1A000004);

        return new("Battle.cro", true, at, BattleHook, $"{block.Length} bytes, exp gain now asks the cap");
    }

    private static LevelCapSite InstallCandy(byte[] code, byte[] block)
    {
        if (!WordsMatch(code, CandyHook, CandyOriginal, out string why))
            return new("code.bin", false, 0, CandyHook, why + " - this is not a build the Rare Candy hook was written for");

        uint? spot = FindFreeSpace(code, block.Length, CodeTextEnd(code), CandyHook, 0);
        if (spot is not { } at)
            return new("code.bin", false, 0, CandyHook, $"no run of {block.Length} free bytes inside .text");

        block.CopyTo(code, at);

        // Only the third word changes: the clamp above it and the branch below it already say
        // exactly what is wanted, and the routine returns with Z set when that branch should fire.
        WriteWord(code, CandyHook + 8, BranchLink(CandyHook + 8, at + EntryCandy));

        return new("code.bin", true, at, CandyHook + 8, $"{block.Length} bytes, Rare Candy now asks the cap");
    }

    /// <summary>End of the executable segment of a CRO, from its own segment table.</summary>
    private static uint CroTextEnd(byte[] cro)
    {
        if (cro.Length < 0xD0 || BitConverter.ToUInt32(cro, 0x80) != 0x304F5243) return (uint)cro.Length;

        uint segTable = BitConverter.ToUInt32(cro, 0xC8);
        uint segCount = BitConverter.ToUInt32(cro, 0xCC);
        for (uint i = 0; i < segCount; i++)
        {
            long e = segTable + (i * 12);
            if (e + 12 > cro.Length) break;
            if (BitConverter.ToUInt32(cro, (int)(e + 8)) != 0) continue;   // id 0 == .text
            uint off = BitConverter.ToUInt32(cro, (int)e);
            uint size = BitConverter.ToUInt32(cro, (int)(e + 4));
            if (off + size <= cro.Length) return off + size;
        }
        return (uint)cro.Length;
    }

    /// <summary>
    /// End of .text in an ExeFS .code, found by its page padding rather than by reading ExHeader.
    /// </summary>
    private static uint CodeTextEnd(byte[] code)
    {
        const long MinPadding = 0x100;

        for (long page = 0x1000; page < code.Length; page += 0x1000)
        {
            if (code[page] == 0) continue;              // next section must start here
            long i = page - 1;
            while (i >= 0 && code[i] == 0) i--;
            if (page - (i + 1) >= MinPadding) return (uint)page;
        }
        return (uint)code.Length;
    }

    /// <summary>
    /// Somewhere in <paramref name="bin"/> to put the block: a 4-byte-aligned run of zero fill,
    /// large enough, after <paramref name="after"/>, and inside the executable region.
    /// </summary>
    private static uint? FindFreeSpace(byte[] bin, int need, uint limit, uint after, uint preferredEnd)
    {
        int want = (need + 3) & ~3;

        var runs = new List<(uint Start, uint End)>();
        long runStart = -1;
        for (long i = 0; i <= limit; i++)
        {
            if (i < limit && bin[i] == 0)
            {
                if (runStart < 0) runStart = i;
                continue;
            }
            if (runStart >= 0)
            {
                long start = (runStart + 3) & ~3L;
                if (start > after && i - start >= want) runs.Add(((uint)start, (uint)i));
            }
            runStart = -1;
        }
        if (runs.Count == 0) return null;

        foreach (var r in runs)
            if (EndsFunction(bin, r.Start)) return r.Start;

        foreach (var r in runs)
            if (r.End == preferredEnd) return r.Start;

        foreach (var r in runs)
            if ((r.End & 0xFFF) == 0) return r.Start;

        return runs.OrderByDescending(r => r.End - r.Start).First().Start;
    }

    /// <summary>Whether the word just before <paramref name="at"/> returns from a function.</summary>
    private static bool EndsFunction(byte[] bin, uint at)
    {
        if (at < 4) return false;
        uint w = BitConverter.ToUInt32(bin, (int)at - 4);
        if (w == 0xE12FFF1E) return true;                    // bx lr
        if (w == 0xE1A0F00E) return true;                    // mov pc, lr
        return (w & 0x0FFF8000) == 0x08BD8000;               // pop {..., pc}, any condition
    }

    private static bool WordsMatch(byte[] bin, uint at, uint[] expected, out string why)
    {
        why = "";
        if (at + (expected.Length * 4) > bin.Length)
        {
            why = $"0x{at:X6} is past the end of the file";
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            uint got = BitConverter.ToUInt32(bin, (int)at + (i * 4));
            if (got == expected[i]) continue;
            why = $"0x{at + (i * 4):X6} holds {got:X8}, expected {expected[i]:X8}";
            return false;
        }
        return true;
    }

    private static uint BranchLink(uint from, uint to)
    {
        int delta = (int)(to - (from + 8));
        return 0xEB000000u | ((uint)(delta >> 2) & 0x00FFFFFF);
    }

    private static void WriteWord(byte[] bin, uint at, uint word) =>
        BitConverter.GetBytes(word).CopyTo(bin, at);
}
