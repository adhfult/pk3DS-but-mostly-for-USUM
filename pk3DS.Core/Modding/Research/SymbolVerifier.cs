#nullable enable

using System;
using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Checks that an address resolved from the research corpus actually lands on code in the binary
/// being patched.
/// </summary>
public static class SymbolVerifier
{
    /// <summary>How confident we are that an offset is a real function entry.</summary>
    public enum Verdict
    {
        /// <summary>Looks like a function prologue.</summary>
        Good,

        /// <summary>Decodes as an instruction but is not a recognisable entry point.</summary>
        Weak,

        /// <summary>Padding, data, or outside the file. Almost certainly the wrong build.</summary>
        Bad,
    }

    public sealed class Check
    {
        public string Name { get; init; } = "";
        public uint Offset { get; init; }
        public Verdict Verdict { get; init; }
        public string Reason { get; init; } = "";
    }

    /// <summary>
    /// Inspects the word at <paramref name="offset"/> and judges whether it starts a routine.
    /// </summary>
    public static Check Inspect(byte[] binary, string name, uint offset)
    {
        Check Fail(string why) => new() { Name = name, Offset = offset, Verdict = Verdict.Bad, Reason = why };

        if (binary == null || binary.Length == 0)
            return new Check { Name = name, Offset = offset, Verdict = Verdict.Weak, Reason = "no binary loaded to check against" };

        if ((offset & 3) != 0)
            return Fail("not 4-byte aligned, so it cannot be an ARM instruction boundary");

        if (offset + 4 > binary.Length)
            return Fail($"past the end of the file (0x{binary.Length:X} bytes)");

        uint word = BitConverter.ToUInt32(binary, (int)offset);

        if (word == 0x00000000) return Fail("target is zeroed - padding or unmapped space");
        if (word == 0xFFFFFFFF) return Fail("target is 0xFF filler");

        // Condition 0xF is not a conditional instruction in ARMv5+; seeing it at a routine's first
        // word means this is data being read as code.
        if ((word >> 28) == 0xF)
            return Fail("first word has condition 0xF - this is data, not a routine entry");

        // STMFD SP!, {..., lr}  =  PUSH {..., lr}
        if ((word & 0xFFFF_4000) == 0xE92D_4000)
            return new Check { Name = name, Offset = offset, Verdict = Verdict.Good, Reason = "opens with PUSH {..., lr}" };

        // Other plausible openings: SUB SP, SP, #n / MOV r12, sp / any STMFD.
        if ((word & 0xFFF0_0000) == 0xE240_0000 && ((word >> 12) & 0xF) == 13)
            return new Check { Name = name, Offset = offset, Verdict = Verdict.Good, Reason = "opens by reserving stack" };
        if (word == 0xE1A0_C00D)
            return new Check { Name = name, Offset = offset, Verdict = Verdict.Good, Reason = "opens with MOV r12, sp" };
        if ((word & 0x0FFF_0000) == 0x092D_0000)
            return new Check { Name = name, Offset = offset, Verdict = Verdict.Good, Reason = "opens with a stack push" };

        return new Check
        {
            Name = name,
            Offset = offset,
            Verdict = Verdict.Weak,
            Reason = $"decodes as an instruction (0x{word:X8}) but not a usual prologue - it may be a "
                   + "leaf routine, or the wrong build",
        };
    }

    /// <summary>Result of confirming a documented offset against a specific binary.</summary>
    public sealed class Relocation
    {
        public string Name { get; init; } = "";
        public uint Documented { get; init; }
        public uint Actual { get; init; }
        public bool Found { get; init; }
        public bool Moved => Found && Actual != Documented;
        public string Note { get; init; } = "";
    }

    /// <summary>Bytes of a routine used as its fingerprint. 24 is enough to be unique in practice.</summary>
    public const int SignatureLength = 24;

    /// <summary>
    /// Confirms a documented offset against the binary being patched, and finds the routine if it
    /// has moved.
    /// </summary>
    public static Relocation Locate(byte[] reference, byte[] target, string name, uint documented)
    {
        if (reference == null || target == null)
            return new Relocation { Name = name, Documented = documented, Actual = documented, Found = false, Note = "no binary to compare" };

        if (documented + SignatureLength > reference.Length)
            return new Relocation { Name = name, Documented = documented, Actual = documented, Found = false, Note = "past the end of the reference" };

        // Fast path: the bytes are already there, which is the overwhelmingly common case.
        if (documented + SignatureLength <= target.Length
            && Matches(target, (int)documented, reference, (int)documented, SignatureLength))
        {
            return new Relocation { Name = name, Documented = documented, Actual = documented, Found = true, Note = "confirmed in place" };
        }

        var pattern = new byte[SignatureLength];
        Array.Copy(reference, documented, pattern, 0, SignatureLength);

        uint first = 0;
        int count = 0;
        for (int i = 0; i + SignatureLength <= target.Length; i += 4)
        {
            if (!Matches(target, i, pattern, 0, SignatureLength)) continue;
            if (count == 0) first = (uint)i;
            if (++count > 1) break;
        }

        if (count == 1)
            return new Relocation { Name = name, Documented = documented, Actual = first, Found = true, Note = $"relocated by {(long)first - documented:+#;-#;0} bytes" };

        if (count > 1)
            return new Relocation { Name = name, Documented = documented, Actual = documented, Found = false, Note = "fingerprint is not unique - keeping the documented offset" };

        return new Relocation { Name = name, Documented = documented, Actual = documented, Found = false, Note = "not present in this binary" };
    }

    private static bool Matches(byte[] a, int ao, byte[] b, int bo, int len)
    {
        if (ao + len > a.Length || bo + len > b.Length) return false;
        for (int i = 0; i < len; i++)
            if (a[ao + i] != b[bo + i]) return false;
        return true;
    }

    /// <summary>Inspects several at once and summarises how the set looks.</summary>
    public static (List<Check> Checks, int Good, int Weak, int Bad) InspectAll(
        byte[] binary, IEnumerable<(string Name, uint Offset)> symbols)
    {
        var list = new List<Check>();
        int good = 0, weak = 0, bad = 0;

        foreach (var (name, offset) in symbols)
        {
            var c = Inspect(binary, name, offset);
            list.Add(c);
            switch (c.Verdict)
            {
                case Verdict.Good: good++; break;
                case Verdict.Weak: weak++; break;
                default: bad++; break;
            }
        }
        return (list, good, weak, bad);
    }

    /// <summary>
    /// A sentence for the log describing whether the corpus matches the binary.
    /// </summary>
    public static string Summarise(int good, int weak, int bad, string version)
    {
        int total = good + weak + bad;
        if (total == 0) return "no symbols to verify.";

        if (bad == 0 && weak == 0)
            return $"all {total} resolved address(es) land on routine entries - the {version} corpus "
                 + "matches this binary.";

        if (bad >= Math.Max(2, total / 2))
            return $"{bad} of {total} resolved addresses do not point at code. That is the signature of "
                 + $"a build mismatch: the corpus is written for a different ROM than the one loaded. "
                 + $"Check the Game selector, and whether these notes cover retail or an expanded build.";

        return $"{good} good, {weak} unclear, {bad} bad of {total}. Individual failures are usually a "
             + "sheet error; several together mean the wrong build.";
    }
}
