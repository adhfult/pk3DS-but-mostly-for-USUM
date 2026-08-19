#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.Core.Modding;

/// <summary>
/// Makes the game act on the bindings held in <see cref="AbilityMoveFlags"/>.
/// </summary>
public static class AbilityFlagPatcher
{
    /// <summary>Fixed-point scale for the multiplier; ARM here has no floating point available.</summary>
    public const int FixedPointShift = 12;

    /// <summary>
    /// Marker written ahead of the table so a previous patch can be found and replaced.
    /// <para>
    /// Ends in a version digit. Records gained a trigger-kind field when items were added, so a
    /// table written by the earlier build has a different record size - reading one with the new
    /// layout would misinterpret every field after the first. The differing signature makes an old
    /// table simply not match rather than be parsed wrongly.
    /// </para>
    /// </summary>
    private static readonly byte[] Signature = "PK3DSAF2"u8.ToArray();

    /// <summary>Bytes per record: bit, trigger kind, trigger id, multiplier.</summary>
    private const int RecordSize = 16;

    /// <summary>What a patch attempt produced.</summary>
    public sealed class Result
    {
        public bool Applied { get; init; }
        public string Message { get; init; } = "";
        public int BindingCount { get; init; }
        public uint TableOffset { get; init; }
    }

    /// <summary>
    /// Builds the binding table: a count followed by one 12-byte record per bound flag.
    /// Each record is bit index, ability id, and the multiplier in <see cref="FixedPointShift"/>
    /// fixed point, all little-endian 32-bit.
    /// </summary>
    public static byte[] BuildBindingTable()
    {
        var bound = AbilityMoveFlags.All.Where(b => b.IsBound).OrderBy(b => b.Bit).ToList();

        var bytes = new List<byte>();
        bytes.AddRange(Signature);
        bytes.AddRange(BitConverter.GetBytes(bound.Count));

        foreach (var b in bound)
        {
            bytes.AddRange(BitConverter.GetBytes(b.Bit));
            bytes.AddRange(BitConverter.GetBytes((int)b.Trigger));   // 1 = ability, 2 = held item
            bytes.AddRange(BitConverter.GetBytes(b.TriggerId));
            // Rounded to the nearest fixed-point step so 1.3 survives the round trip predictably.
            int scaled = (int)Math.Round(b.Multiplier * (1 << FixedPointShift));
            bytes.AddRange(BitConverter.GetBytes(scaled));
        }
        return [.. bytes];
    }

    /// <summary>
    /// The contract the engine-side routine must satisfy, documented for whoever writes it.
    /// <para>
    /// Deliberately not generated here. Emitting the hook needs the address of the power
    /// calculation in this specific Battle.cro and the register layout at that point; neither can
    /// be derived from the table, and a branch written to a guessed address corrupts the module
    /// exactly the way this tool's approval gate exists to prevent.
    /// </para>
    /// </summary>
    public const string RoutineContract =
        "Entry: r0 = move flag word, r1 = attacker ability id, r2 = table address,\n"
        + "       r3 = attacker held item id (-1 when none).\n"
        + "Exit:  r0 = power multiplier in Q12 fixed point (0x1000 = unchanged).\n"
        + "Table: 8-byte signature 'PK3DSAF2', int32 count, then count records of\n"
        + "       { int32 bitIndex, int32 triggerKind, int32 triggerId, int32 multiplierQ12 }.\n"
        + "       triggerKind: 1 = ability, 2 = held item.\n"
        + "Match: a record applies when bit bitIndex is set in r0 and triggerId equals r1\n"
        + "       (kind 1) or r3 (kind 2). Multiple matches multiply together.";

    /// <summary>A run of unused bytes in a binary, offered as somewhere the table could live.</summary>
    public sealed class FreeRegion
    {
        public uint Offset { get; init; }
        public int Length { get; init; }

        /// <summary>The byte the run is filled with - 0x00 padding or 0xFF erased space.</summary>
        public byte Filler { get; init; }

        /// <summary>True when the run reaches the end of the file, the safest kind to use.</summary>
        public bool AtEndOfFile { get; init; }

        /// <summary>Already holds a table written by this tool, so reusing it is free.</summary>
        public bool IsExistingTable { get; init; }

        public override string ToString()
        {
            string kind = IsExistingTable ? "previous table"
                        : AtEndOfFile ? "trailing padding"
                        : $"0x{Filler:X2} run";
            return $"0x{Offset:X6}  {Length,7:N0} bytes  ({kind})";
        }
    }

    /// <summary>
    /// Finds places in a binary big enough to hold the table.
    /// </summary>
    public static List<FreeRegion> FindFreeRegions(string path, int minLength)
    {
        var found = new List<FreeRegion>();
        if (!File.Exists(path)) return found;

        byte[] data;
        try { data = File.ReadAllBytes(path); }
        catch { return found; }

        // An existing table is the obvious answer - reusing it changes nothing else.
        for (uint probe = 0; probe + Signature.Length < data.Length; probe += 4)
        {
            if (!HasSignature(data, probe)) continue;
            found.Add(new FreeRegion
            {
                Offset = probe,
                Length = MeasureRun(data, probe + (uint)Signature.Length, minLength),
                Filler = 0x00,
                IsExistingTable = true,
                AtEndOfFile = false,
            });
            break;
        }

        foreach (byte filler in new byte[] { 0x00, 0xFF })
        {
            int i = 0;
            while (i < data.Length)
            {
                if (data[i] != filler) { i++; continue; }

                int start = i;
                while (i < data.Length && data[i] == filler) i++;
                int length = i - start;
                if (length < minLength) continue;

                // Start on a 4-byte boundary; ARM reads here are word-aligned.
                int aligned = (start + 3) & ~3;
                length -= aligned - start;
                if (length < minLength) continue;

                found.Add(new FreeRegion
                {
                    Offset = (uint)aligned,
                    Length = length,
                    Filler = filler,
                    AtEndOfFile = i >= data.Length,
                });
            }
        }

        return [.. found
            .OrderByDescending(r => r.IsExistingTable)
            .ThenByDescending(r => r.AtEndOfFile)
            .ThenByDescending(r => r.Length)];
    }

    private static int MeasureRun(byte[] data, uint from, int fallback)
    {
        int n = 0;
        for (uint i = from; i < data.Length && n < fallback * 4; i++, n++) { }
        return Math.Max(n, fallback);
    }

    /// <summary>Bytes the table currently needs, so a caller can size its search.</summary>
    public static int RequiredBytes() =>
        Signature.Length + 4 + (AbilityMoveFlags.All.Count(b => b.IsBound) * RecordSize);

    /// <summary>
    /// Writes the binding table into Battle.cro, replacing any table this wrote before.
    /// </summary>
    /// <param name="battleCroPath">Path to Battle.cro.</param>
    /// <param name="reserveOffset">
    /// File offset of a region known to be free in this build. Nothing is guessed: if the region is
    /// not blank, the patch is refused rather than written over something.
    /// </param>
    /// <param name="reserveLength">How many bytes are available there.</param>
    public static Result ApplyTable(string battleCroPath, uint reserveOffset, int reserveLength)
    {
        if (!File.Exists(battleCroPath))
            return new Result { Message = "Battle.cro was not found." };

        byte[] table = BuildBindingTable();
        int bindingCount = AbilityMoveFlags.All.Count(b => b.IsBound);

        if (bindingCount == 0)
            return new Result { Message = "No flags are bound to an ability, so there is nothing to patch." };

        if (table.Length > reserveLength)
            return new Result
            {
                Message = $"The binding table needs {table.Length} bytes but only {reserveLength} are reserved.",
                BindingCount = bindingCount,
            };

        byte[] data = File.ReadAllBytes(battleCroPath);
        if (reserveOffset + reserveLength > data.Length)
            return new Result { Message = "The reserved region lies outside Battle.cro.", BindingCount = bindingCount };

        // Only write into space that is either blank or already holds one of our tables. Anything
        // else means the offset is wrong for this build, and writing would corrupt real code.
        bool blank = true;
        for (int i = 0; i < reserveLength; i++)
        {
            if (data[reserveOffset + i] != 0x00) { blank = false; break; }
        }
        bool ours = HasSignature(data, reserveOffset);

        if (!blank && !ours)
        {
            return new Result
            {
                Message = $"The region at 0x{reserveOffset:X} is neither blank nor a table written by this tool. "
                        + "Refusing to overwrite it - the offset is probably wrong for this build.",
                BindingCount = bindingCount,
            };
        }

        Array.Clear(data, (int)reserveOffset, reserveLength);
        table.CopyTo(data, (int)reserveOffset);

        string detail = string.Join(Environment.NewLine,
            AbilityMoveFlags.All.Where(b => b.IsBound).OrderBy(b => b.Bit)
                .Select(b => $"  F{b.Bit + 1} -> {b.Trigger.ToString().ToLowerInvariant()} {b.TriggerName} (x{b.Multiplier:0.##})"));

        bool written = BinaryWriteGuard.TryWrite(battleCroPath, data,
            $"Write the ability/move-flag table ({bindingCount} binding(s))",
            $"Table occupies {table.Length} bytes at 0x{reserveOffset:X} in Battle.cro."
            + Environment.NewLine + detail);

        return new Result
        {
            Applied = written,
            BindingCount = bindingCount,
            TableOffset = reserveOffset,
            Message = written
                ? $"Wrote {bindingCount} binding(s) to 0x{reserveOffset:X}."
                : "The write was declined, so Battle.cro is unchanged.",
        };
    }

    /// <summary>Reads back what is currently in the ROM, to confirm a patch took.</summary>
    public static string ReadBack(string battleCroPath, uint reserveOffset)
    {
        try
        {
            if (!File.Exists(battleCroPath)) return "Battle.cro was not found.";
            byte[] data = File.ReadAllBytes(battleCroPath);
            if (reserveOffset + 12 > data.Length) return "The offset lies outside Battle.cro.";
            if (!HasSignature(data, reserveOffset)) return "No binding table is present at that offset.";

            int count = BitConverter.ToInt32(data, (int)reserveOffset + Signature.Length);
            var lines = new List<string> { $"{count} binding(s) at 0x{reserveOffset:X}:" };

            int p = (int)reserveOffset + Signature.Length + 4;
            for (int i = 0; i < count && p + RecordSize <= data.Length; i++, p += RecordSize)
            {
                int bit = BitConverter.ToInt32(data, p);
                int kind = BitConverter.ToInt32(data, p + 4);
                int id = BitConverter.ToInt32(data, p + 8);
                int scaled = BitConverter.ToInt32(data, p + 12);
                string kindName = kind == (int)FlagTrigger.Item ? "item" : "ability";
                lines.Add($"  F{bit + 1} -> {kindName} #{id} (x{(double)scaled / (1 << FixedPointShift):0.##})");
            }
            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex) { return "Could not read Battle.cro: " + ex.Message; }
    }

    private static bool HasSignature(byte[] data, uint offset)
    {
        if (offset + Signature.Length > data.Length) return false;
        for (int i = 0; i < Signature.Length; i++)
        {
            if (data[offset + i] != Signature[i]) return false;
        }
        return true;
    }
}
