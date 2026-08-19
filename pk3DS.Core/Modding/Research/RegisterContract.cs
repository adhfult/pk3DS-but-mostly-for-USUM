#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// What a routine expects to be handed, worked out by reading its code.
/// </summary>
public sealed class RegisterUsage
{
    /// <summary>Argument registers read before being written.</summary>
    public SortedSet<int> Inputs { get; } = [];

    /// <summary>Register number to the record type it was dereferenced as, where recognisable.</summary>
    public Dictionary<int, string> Roles { get; } = [];

    /// <summary>Instructions decoded before the routine returned or the budget ran out.</summary>
    public int Words { get; set; }

    /// <summary>True when a return was reached, so the whole routine was seen.</summary>
    public bool Complete { get; set; }

    public string Describe() =>
        Inputs.Count == 0 ? "no arguments" : string.Join(", ", Inputs.Select(r => $"r{r}"));
}

/// <summary>Reads ARM routines to recover their calling convention.</summary>
public static class RegisterContract
{
    /// <summary>
    /// The calling convention every stock timing handler in Battle.cro follows, recovered by
    /// disassembling known handlers (Iron Fist, Muscle Band, Low Kick, Rough Skin, Gravity,
    /// Mean Look) rather than from documentation.
    /// </summary>
    public const string EngineAbi =
        "r0 = subject id, r1 = battler context, r2 = event token (gate on it via Returns Battle "
        + "State data, or the handler fires on every event). Modifiers are reported by tail-calling "
        + "0x083108 with r0 = 0x35 and the value in r1, not by returning in r0.";

    /// <summary>Struct offsets that identify what a base register points at.</summary>
    private static readonly Dictionary<uint, string> RoleByOffset = BuildRoles();

    private static Dictionary<uint, string> BuildRoles()
    {
        var byOffset = new Dictionary<uint, List<string>>();
        foreach (var (name, off) in SymbolSubstitution.KnownOffsets)
        {
            if (!byOffset.TryGetValue(off, out var list)) byOffset[off] = list = [];
            list.Add(name);
        }

        var roles = new Dictionary<uint, string>();
        foreach (var (off, names) in byOffset)
        {
            // Only keep offsets that identify one record type unambiguously.
            var types = names.Select(n => n.Split('.')[0]).Distinct().ToList();
            if (types.Count == 1 && off != 0) roles[off] = types[0] + " record";
        }
        return roles;
    }

    /// <summary>
    /// Walks a routine from <paramref name="offset"/>, recording which argument registers it
    /// consumes and what they point at.
    /// </summary>
    public static RegisterUsage Analyse(byte[] image, uint offset, int maxWords = 64)
    {
        var usage = new RegisterUsage();
        if (image == null || offset + 4 > image.Length) return usage;

        var written = new bool[16];

        void Read(int reg)
        {
            if (reg <= 3 && !written[reg]) usage.Inputs.Add(reg);
        }

        for (int i = 0; i < maxWords; i++)
        {
            uint at = offset + (uint)(i * 4);
            if (at + 4 > image.Length) break;
            uint w = BitConverter.ToUInt32(image, (int)at);
            usage.Words = i + 1;

            uint cond = w >> 28;
            if (cond == 0xF) continue;                       // unconditional-space encodings

            uint kind = (w >> 26) & 0x3;

            if (kind == 0)                                   // data processing / multiply
            {
                bool isMul = (w & 0x0FC000F0) == 0x00000090;
                if (isMul)
                {
                    Read((int)(w & 0xF));                    // Rm
                    Read((int)((w >> 8) & 0xF));             // Rs
                    written[(w >> 16) & 0xF] = true;         // Rd
                    continue;
                }

                uint op = (w >> 21) & 0xF;
                bool immediate = ((w >> 25) & 1) != 0;
                uint rn = (w >> 16) & 0xF, rd = (w >> 12) & 0xF;

                bool usesRn = op is not (0xD or 0xF);        // MOV and MVN ignore Rn
                bool writesRd = op is not (0x8 or 0x9 or 0xA or 0xB);   // TST TEQ CMP CMN

                if (usesRn) Read((int)rn);
                if (!immediate) Read((int)(w & 0xF));        // Rm
                if (writesRd) written[rd] = true;
                continue;
            }

            if (kind == 1)                                   // load / store
            {
                uint rn = (w >> 16) & 0xF, rd = (w >> 12) & 0xF;
                bool load = ((w >> 20) & 1) != 0;
                bool immediate = ((w >> 25) & 1) == 0;

                Read((int)rn);

                // The offset names the field, and the field names the structure.
                if (immediate && rn <= 3 && !usage.Roles.ContainsKey((int)rn))
                {
                    uint imm = w & 0xFFF;
                    if (RoleByOffset.TryGetValue(imm, out string? role)) usage.Roles[(int)rn] = role;
                }

                if (load) written[rd] = true; else Read((int)rd);
                continue;
            }

            if (kind == 2)                                   // block transfer / branch
            {
                if (((w >> 25) & 1) != 0)                    // B / BL
                {
                    if (((w >> 24) & 1) != 0)                // BL destroys r0-r3
                        for (int r = 0; r <= 3; r++) written[r] = true;
                    continue;
                }

                uint list = w & 0xFFFF;
                bool load = ((w >> 20) & 1) != 0;
                Read((int)((w >> 16) & 0xF));

                if (load)
                {
                    for (int r = 0; r <= 3; r++) if ((list & (1u << r)) != 0) written[r] = true;
                    if ((list & 0x8000) != 0) { usage.Complete = true; return usage; }   // POP {..., pc}
                }
                else
                {
                    for (int r = 0; r <= 3; r++) if ((list & (1u << r)) != 0) Read(r);
                }
                continue;
            }

            // BX lr
            if ((w & 0x0FFFFFFF) == 0x012FFF1E) { usage.Complete = true; return usage; }
        }

        return usage;
    }

    /// <summary>An observation about how a routine's arguments compare to its timing's.</summary>
    public sealed record Finding(string Message);

    /// <summary>
    /// Compares a routine against the stock handlers that run at the same timing.
    /// </summary>
    public static List<Finding> Compare(RegisterUsage mine, IReadOnlyList<RegisterUsage> stock)
    {
        var findings = new List<Finding>();
        if (stock.Count == 0)
        {
            findings.Add(new Finding("no stock routine runs at this timing, so there is nothing to compare against"));
            return findings;
        }

        var always = new SortedSet<int>(stock[0].Inputs);
        var ever = new SortedSet<int>();
        foreach (var s in stock) { always.IntersectWith(s.Inputs); ever.UnionWith(s.Inputs); }

        var unmatched = mine.Inputs.Where(r => !always.Contains(r)).ToList();

        if (unmatched.Count == 0)
        {
            findings.Add(new Finding(
                $"takes {mine.Describe()}, which every one of the {stock.Count} stock routine(s) at this timing also takes"));
            return findings;
        }

        foreach (int r in unmatched)
        {
            findings.Add(ever.Contains(r)
                ? new Finding($"r{r} is read here and by some but not all stock routines at this timing - worth confirming")
                : new Finding($"r{r} is read here and by none of the stock routines this analysis could read at this timing - " +
                              "either the argument is wrong, or the analysis missed it (it reads straight-line only)"));
        }
        return findings;
    }
}
