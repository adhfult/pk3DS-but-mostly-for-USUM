#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Resolves named references in assembly to concrete addresses, just before it is assembled.
/// </summary>
public static class SymbolSubstitution
{
    private static readonly Regex Token = new(@"\{(sym|off):([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Offsets that come from the record layouts this program already models, so they need no
    /// research lookup and cannot drift without the structures changing too.
    /// </summary>
    public static readonly Dictionary<string, uint> KnownOffsets = new(StringComparer.OrdinalIgnoreCase)
    {
        // Move record (Move7, 0x28 bytes/entry) - see pk3DS.Core/Structures/Moves/Move7.cs
        ["move.flags"] = 0x24,
        ["move.type"] = 0x00,
        ["move.category"] = 0x02,
        ["move.power"] = 0x03,
        ["move.accuracy"] = 0x04,
        ["move.pp"] = 0x05,
        ["move.priority"] = 0x06,
        ["move.inflict"] = 0x08,
        ["move.effectsequence"] = 0x10,
        ["move.target"] = 0x14,

        // Personal record (PersonalInfoSM, 0x54 bytes/entry)
        ["personal.tmhm"] = 0x28,
        ["personal.tutors"] = 0x38,

        // Item record (Item6)
        ["item.heldeffect"] = 0x00,
        ["item.heldargument"] = 0x01,
    };

    /// <summary>Outcome of a substitution pass.</summary>
    public sealed class Result
    {
        public string Text { get; init; } = "";
        public List<string> Resolved { get; } = [];
        public List<string> Errors { get; } = [];
        public bool Success => Errors.Count == 0;
    }

    /// <summary>
    /// Replaces every <c>{sym:...}</c> and <c>{off:...}</c> token in <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">Assembly text, possibly containing tokens.</param>
    /// <param name="symbols">Symbol table for the target binary, or null when unavailable.</param>
    public static Result Apply(string assembly, ArmSymbolTable? symbols) =>
        Apply(assembly, symbols, null, null);

    /// <summary>
    /// As above, but also checks each resolved address actually lands on code in
    /// <paramref name="binary"/>.
    /// </summary>
    /// <summary>
    /// Reference binary the corpus was written against, when one is available.
    /// </summary>
    public static byte[]? ReferenceBinary { get; set; }

    public static Result Apply(string assembly, ArmSymbolTable? symbols, byte[]? binary, string? version)
    {
        var errors = new List<string>();
        var resolved = new List<string>();
        var located = new List<(string Name, uint Offset)>();

        string output = Token.Replace(assembly, m =>
        {
            string kind = m.Groups[1].Value;
            string name = m.Groups[2].Value.Trim();

            if (kind == "off")
            {
                if (KnownOffsets.TryGetValue(name, out uint off))
                {
                    resolved.Add($"{{off:{name}}} -> 0x{off:X}");
                    return $"0x{off:X}";
                }
                errors.Add($"unknown offset name \"{name}\". Known: {string.Join(", ", KnownOffsets.Keys.OrderBy(k => k))}");
                return m.Value;
            }

            if (symbols == null)
            {
                errors.Add($"cannot resolve {{sym:{name}}} - no symbol table for this target");
                return m.Value;
            }

            var hit = symbols.UniqueByName(name, out string? problem);
            if (hit == null)
            {
                errors.Add(problem ?? $"could not resolve \"{name}\"");
                return m.Value;
            }

            uint offset = hit.Value.Offset;

            if (ReferenceBinary is { Length: > 0 } && binary is { Length: > 0 })
            {
                var moved = SymbolVerifier.Locate(ReferenceBinary, binary, name, offset);
                if (moved.Found && moved.Moved)
                {
                    offset = moved.Actual;
                    resolved.Add($"{{sym:{name}}} -> 0x{offset:X6} ({moved.Note})");
                    located.Add((name, offset));
                    return $"0x{offset:X}";
                }
                if (!moved.Found && moved.Note == "not present in this binary")
                    errors.Add($"\"{name}\" is not in this binary - the corpus describes a different build");
            }

            resolved.Add($"{{sym:{name}}} -> 0x{offset:X6}");
            located.Add((name, offset));
            return $"0x{offset:X}";
        });

        var r = new Result { Text = output };
        r.Resolved.AddRange(resolved);
        r.Errors.AddRange(errors);

        if (binary is { Length: > 0 } && located.Count > 0)
        {
            var (checks, good, weak, bad) = SymbolVerifier.InspectAll(binary, located);
            r.Resolved.Add(SymbolVerifier.Summarise(good, weak, bad, version ?? "selected"));

            foreach (var c in checks)
            {
                if (c.Verdict == SymbolVerifier.Verdict.Bad)
                    r.Errors.Add($"\"{c.Name}\" resolved to 0x{c.Offset:X6}, which is not code: {c.Reason}");
                else if (c.Verdict == SymbolVerifier.Verdict.Weak)
                    r.Resolved.Add($"  note: \"{c.Name}\" at 0x{c.Offset:X6} - {c.Reason}");
            }
        }

        return r;
    }

    /// <summary>True when the text still holds unresolved tokens.</summary>
    public static bool HasTokens(string assembly) => Token.IsMatch(assembly ?? "");
}
