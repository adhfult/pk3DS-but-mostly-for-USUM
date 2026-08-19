using System;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// How much room is left in code.bin for new executable code, and who is competing for it.
/// </summary>
public static class CodeSpaceBudget
{
    /// <summary>
    /// Bytes the level cap routine occupies with the default table.
    /// </summary>
    public const int LevelCapBytes = 216;

    /// <summary>Bytes the TM expansion's routines occupy.</summary>
    public const int TMExpansionBytes = 285;

    public sealed record Report(int Offset, int Free)
    {
        public bool Fits(int need) => Free >= need;
    }

    /// <summary>Measures the executable pool as it stands right now.</summary>
    public static Report Measure(byte[] code)
    {
        var (offset, free) = ResearchEngine.FindFreeExecutableSpace(code);
        return new Report(offset, Math.Max(0, free));
    }

    /// <summary>A one-line statement of the pool, for logs and status bars.</summary>
    public static string Describe(byte[] code)
    {
        var r = Measure(code);
        if (r.Free <= 0) return "code.bin has no executable space left in .text.";

        return $"code.bin executable space: {r.Free} byte(s) free at 0x{r.Offset:X}. " +
               $"Level caps need {LevelCapBytes}, the TM expansion needs {TMExpansionBytes}.";
    }

    /// <summary>
    /// Why <paramref name="need"/> bytes cannot be placed, in terms someone can act on.
    /// </summary>
    public static string ExplainShortfall(byte[] code, int need, string feature)
    {
        var r = Measure(code);
        string basis =
            $"{feature} needs {need} bytes of executable space in code.bin and only {r.Free} are free.";

        // On a build with room for one but not both, naming the other feature is the whole answer.
        int both = LevelCapBytes + TMExpansionBytes;
        if (r.Free + LevelCapBytes >= need && need != LevelCapBytes)
        {
            return basis + Environment.NewLine +
                   "The level cap routine is using that space. This ROM has room for level caps OR " +
                   "the TM expansion, not both - remove one to install the other, or start from a " +
                   "code.bin without either.";
        }
        if (r.Free + TMExpansionBytes >= need && need != TMExpansionBytes)
        {
            return basis + Environment.NewLine +
                   "The TM expansion is using that space. This ROM has room for the TM expansion OR " +
                   "level caps, not both - remove one to install the other.";
        }

        return basis + Environment.NewLine +
               $"A clean code.bin has about 1552 bytes here and an Expansion Pack build about 330, " +
               $"which is why both features together ({both} bytes) only fit on vanilla.";
    }
}
