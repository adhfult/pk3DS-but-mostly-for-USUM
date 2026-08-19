using System;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding;

public static class MoveEngine
{
    private static string GetCodePath()
    {
        // Actually, let's just look for code.bin in the working directory or RomFS parent.
        // Better: pk3DS handles this in Main. We'll use a hack or just pass the path.
        // For now, let's assume the ExeFS path is available via a temporary measure.
        return Path.Combine(Directory.GetCurrentDirectory(), "code.bin"); // Placeholder logic, will refine.
    }

    public static void ExpandMoveLimit(int newCount, string codePath, string battlePath)
    {
        if (!File.Exists(codePath) || !File.Exists(battlePath)) return;

        byte[] code = File.ReadAllBytes(codePath);
        byte[] battle = File.ReadAllBytes(battlePath);

        bool mCode = false;
        bool mBattle = false;

        // 1. Patch Code.bin limits
        int[] codeOfs = { 0x226680, 0x2267D4, 0x226B04, 0x2D2C28 };
        foreach (int ofs in codeOfs)
        {
            if (PatchLimitCheck(code, ofs, newCount)) mCode = true;
        }

        // 2. Patch Battle.cro limits
        int[] battleOfs = { 0x092D70, 0x093644 };
        foreach (int ofs in battleOfs)
        {
            if (PatchLimitCheck(battle, ofs, newCount)) mBattle = true;
        }

        // 3. Metronome Special Patch
        if (PatchMetronome(battle, 0x0B0884, newCount)) mBattle = true;

        // 4. Jump Table Expansion
        if (ExpandJumpTable(ref battle, newCount)) mBattle = true;

        bool wroteCode = !mCode || BinaryWriteGuard.TryWrite(codePath, code,
            "Expand the engine's move-count limit",
            $"Raises the limit to {newCount} at code.bin offsets "
            + string.Join(", ", Array.ConvertAll(codeOfs, o => $"0x{o:X6}")) + ".");

        bool wroteBattle = !mBattle || BinaryWriteGuard.TryWrite(battlePath, battle,
            "Expand the engine's move-count limit",
            $"Raises the limit to {newCount} in Battle.cro, including the jump table and Metronome.");

        // Only claim the patch was applied if it actually reached disk; recording it regardless
        // would make a later run skip work that never happened.
        if ((mCode && !wroteCode) || (mBattle && !wroteBattle))
            return;

        ProjectState.Instance.MoveCount = newCount;
        ProjectState.Instance.AppliedPatches.Add("MoveCountExpansion");
        ProjectState.Instance.Save();
    }

    private static bool PatchLimitCheck(byte[] data, int offset, int newLimit)
    {
        if (offset < 0 || offset + 4 > data.Length) return false;
        if (data[offset + 3] == 0xE3 && (data[offset + 2] & 0xF0) == 0x50)
        {
            byte[] newInst = GetCmpInstruction(data[offset + 2] & 0x0F, newLimit);
            if (newInst != null)
            {
                newInst.CopyTo(data, offset);
                return true;
            }
        }
        return false;
    }

    private static bool PatchMetronome(byte[] data, int offset, int newLimit) => PatchLimitCheck(data, offset, newLimit);

    private static bool ExpandJumpTable(ref byte[] data, int newCount)
    {
        byte[] sig = { 0x07, 0xF1, 0x2F, 0x90 };
        int idx = Util.IndexOfBytes(data, sig, 0, data.Length);
        if (idx < 0) return false;

        int tableOfs = idx + 8;
        int stockCount = 721;
        int toAdd = (newCount + 1 - stockCount) * 4;
        if (toAdd <= 0) return false;

        data = CROUtil.ExpandSegment(data, 'c', toAdd, tableOfs + (stockCount * 4));
        return true;
    }

    private static byte[] GetCmpInstruction(int reg, int val)
    {
        uint uval = (uint)val;
        for (int shift = 0; shift < 32; shift += 2)
        {
            uint v = ((uval << shift) & 0xFFFFFFFF) | (uval >> (32 - shift));
            if (v <= 0xFF) return new byte[] { (byte)v, (byte)(shift / 2), (byte)(0x50 + reg), 0xE3 };
        }
        return null;
    }

    public static bool ApplyRelocationPatch(MoveModernBase move, string battlePath)
    {
        if (!File.Exists(battlePath)) return false;
        byte[] data = File.ReadAllBytes(battlePath);
        bool modified = false;

        const int TableBase = 0x000D6E10; // USUM UM v1.0 Move Logic Table Base
        
        foreach (var p in move.Patches)
        {
            try
            {
                if (p.Offset.Length != 8 || !p.Logic.All(c => "0123456789ABCDEFabcdef".Contains(c))) continue;
                int offset = Convert.ToInt32(p.Offset, 16);
                
                // If the user selected a target Move ID, and this patch is a table entry, redirect it.
                if (move.CurrentID > 0)
                {
                    int relShift = offset - TableBase;
                    if (relShift >= 0 && relShift % 4 == 0 && relShift < 4000) // Within move table range (roughly 1000 moves)
                    {
                        offset = TableBase + (move.CurrentID * 4);
                    }
                }

                if (offset < 0 || offset + 4 > data.Length) continue;

                // Handle Hex Data (4 bytes)
                if (p.Logic.Length == 8)
                {
                    uint val = Convert.ToUInt32(p.Logic, 16);
                    byte[] bytes = BitConverter.GetBytes(val);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    bytes.CopyTo(data, offset);
                    modified = true;
                }
            }
            catch { continue; }
        }

        if (modified)
        {
            pk3DS.Core.CTR.CROUtil.SaveCro(battlePath, data);
            ProjectState.Instance.AppliedPatches.Add($"{move.Name} -> Move ID {move.CurrentID}");
            ProjectState.Instance.Save();
            return true;
        }
        return false;
    }
}
