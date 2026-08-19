#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>One value the author supplies when instantiating a template.</summary>
public sealed class TemplateParameter
{
    /// <summary>Placeholder token as it appears in the body, without braces.</summary>
    public string Key { get; init; } = "";

    public string Label { get; init; } = "";
    public string Default { get; init; } = "";
    public string Help { get; init; } = "";
}

/// <summary>
/// A starting point for a custom function: the shape of a common effect, with the parts that vary
/// left as parameters and the parts that must come from research left as marked gaps.
/// </summary>
public sealed class FunctionTemplate
{
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
    public CustomMechanicKind Kind { get; init; }
    public ResearchTarget Target { get; init; } = ResearchTarget.BattleCro;

    /// <summary>Workbook that documents the real implementation of this shape.</summary>
    public string CorpusReference { get; init; } = "";

    /// <summary>Suggested timing, and why. Empty when it must be looked up.</summary>
    public string TimingHint { get; init; } = "";

    /// <summary>
    /// The stock element this shape reproduces, so the timing can be read from the ROM instead of
    /// chosen by the author. Null for shapes that are not modelled on one particular element.
    /// </summary>
    public TemplateModel? ModelledOn { get; init; }

    /// <summary>
    /// The timing this shape must use, where the engine itself settles it rather than a stock
    /// element does.
    /// </summary>
    public byte? FixedTiming { get; init; }

    public List<TemplateParameter> Parameters { get; init; } = [];

    /// <summary>ARM body with <c>{Key}</c> placeholders.</summary>
    public List<string> Body { get; init; } = [];

    /// <summary>What the author must confirm before this can work.</summary>
    public List<string> Verify { get; init; } = [];

    /// <summary>
    /// Loads an arbitrary 32-bit constant into a register using only instructions this assembler
    /// supports.
    /// </summary>
    public static List<string> EmitLoadConstant(string reg, uint value)
    {
        var outp = new List<string>();
        if (value == 0) { outp.Add($"MOV  {reg}, #0"); return outp; }

        // Split into byte-aligned chunks at even rotations, which is exactly what the encoding
        // can express in one operation each.
        var parts = new List<uint>();
        for (int shift = 0; shift < 32 && parts.Count < 4; shift += 8)
        {
            uint chunk = value & (0xFFu << shift);
            if (chunk != 0) parts.Add(chunk);
        }

        // One MOV when the value fits an 8-bit rotated immediate; otherwise say why it does not,
        // because a two-instruction load looks arbitrary next to a one-instruction one.
        if (parts.Count == 1)
        {
            outp.Add($"MOV  {reg}, #0x{parts[0]:X}              @ = {value}");
            return outp;
        }

        outp.Add($"@ 0x{value:X} ({value}) spans more than 8 bits, so it is built up in {parts.Count} steps");
        outp.Add($"MOV  {reg}, #0x{parts[0]:X}");
        for (int i = 1; i < parts.Count; i++)
            outp.Add($"ORR  {reg}, {reg}, #0x{parts[i]:X}       @ now 0x{parts.Take(i + 1).Aggregate(0u, (a, b) => a | b):X}");
        return outp;
    }

    /// <summary>
    /// Multiplies <paramref name="reg"/> by a fraction, exactly, with no division instruction.
    /// </summary>
    public static List<string> EmitScale(string reg, string scratch, double factor)
    {
        uint q12 = (uint)Math.Round(factor * 4096.0);

        var outp = new List<string>
        {
            $"@ multiply by {factor:0.###}: {q12} (0x{q12:X}) / 4096",
            $"@ LSR #12 below is the divide by 4096, since 2^12 = 4096",
        };
        outp.AddRange(EmitLoadConstant(scratch, q12));

        // Destination differs from the multiplicand: MUL with Rd == Rm is only defined from ARMv6,
        // and writing it this way is correct on every core regardless.
        outp.Add($"MUL  {scratch}, {reg}, {scratch}   @ {reg} * {q12}");
        outp.Add($"LSR  {reg}, {scratch}, #12         @ / 4096 -> {reg} * {factor:0.###}");
        return outp;
    }

    /// <summary>
    /// Emits the paired type and flag test used by abilities that qualify a move two ways.
    /// </summary>
    public static List<string> EmitMatch(string mode, int type, int bit)
    {
        bool wantType = type is >= 0 and < 255;
        bool wantFlag = bit is >= 0 and < 32;
        bool both = !string.Equals(mode, "either", StringComparison.OrdinalIgnoreCase);

        var outp = new List<string>();
        if (!wantType && !wantFlag)
        {
            outp.Add("@ no type or flag restriction - every move qualifies");
            return outp;
        }

        if (wantType && wantFlag)
        {
            outp.Add(both
                ? "@ qualifies when the move is BOTH the type and in the flag category"
                : "@ qualifies when the move is EITHER the type or in the flag category");
        }

        if (wantType)
        {
            outp.Add($"LDRB r4, [r1, #{{off:move.type}}]");
            outp.Add($"CMP  r4, #{type}");
            if (wantFlag && !both)
                outp.Add("BEQ  matched                        @ type alone is enough");
            else
                outp.Add("BNE  nomatch");
        }

        if (wantFlag)
        {
            outp.Add($"LDR  r4, [r1, #{{off:move.flags}}]");
            outp.Add($"TST  r4, #0x{1u << bit:X}");
            outp.Add("BEQ  nomatch");
        }

        if (wantType && wantFlag && !both)
            outp.Add("matched:");

        return outp;
    }

    /// <summary>Fills the placeholders and produces a definition ready to edit and dry-run.</summary>
    public CustomFunctionDefinition Build(string functionName, IDictionary<string, string>? values = null)
    {
        var v = values ?? new Dictionary<string, string>();

        string Raw(string key)
        {
            var p = Parameters.FirstOrDefault(x => x.Key == key);
            if (p == null) return "";
            return v.TryGetValue(key, out var got) && !string.IsNullOrWhiteSpace(got) ? got.Trim() : p.Default;
        }

        int Num(string key, int fallback)
            => int.TryParse(Raw(key), out int n) ? n : fallback;

        string Fill(string line)
        {
            // {BITMASK} is computed, not substituted: the assembler's ParseImmediate accepts only
            // a literal, so "#(1 << 17)" would not parse. The mask is resolved here instead.
            if (line.Contains("{BITMASK}"))
            {
                int bit = Math.Clamp(Num("BIT", 17), 0, 31);
                line = line.Replace("{BITMASK}", $"0x{1u << bit:X}");
            }

            foreach (var p in Parameters)
                line = line.Replace("{" + p.Key + "}", Raw(p.Key));

            return line;
        }

        // Comments use "@", and must not contain a semicolon.
        //
        // The assembler splits on ";" before it strips comments, so a semicolon anywhere on a line -
        // including inside what is obviously prose - starts a new statement. "@ keep this; restore
        // later" becomes a second line reading "restore later", which then fails as an unknown
        // mnemonic. Every comment this produces is sanitised rather than relying on remembering.
        static string SafeComment(string s) => s.Replace(';', ',');

        var body = new List<string>
        {
            $"@ {SafeComment(Name)}",
            $"@ {SafeComment(Summary)}",
            $"@ Reference: {SafeComment(CorpusReference)}",
            "@",
        };
        // CHECK, not TODO. A Verify note asks you to confirm something about your build; a TODO in
        // the body marks code that is genuinely absent. Labelling both the same way made a template
        // with three cautions look as unfinished as one with three holes in it.
        body.AddRange(Verify.Select(v2 => "@ CHECK: " + SafeComment(v2)));
        if (Verify.Count > 0) body.Add("@");

        foreach (string line in Body)
        {
            string marker = line.Trim();

            // {SCALE} expands to a complete, working multiply-by-fraction sequence.
            if (marker == "{SCALE}")
            {
                int num = Num("NUM", 3), den = Num("DEN", 2);
                if (den == 0) den = 1;
                body.AddRange(EmitScale("r0", "r4", (double)num / den));
                continue;
            }

            // {SCALE2} is the second multiplier on a template that applies two.
            if (marker == "{SCALE2}")
            {
                int num = Num("NUM2", 1), den = Num("DEN2", 1);
                if (den == 0) den = 1;
                body.AddRange(EmitScale("r0", "r4", (double)num / den));
                continue;
            }

            // {TERRAIN} picks a named per-terrain setter instead of passing an id.
            //
            // The corpus documents a separate routine for each terrain, and separate move and
            // ability variants of several - "Set Grassy Terrain (Move)", "Set Psychic Terrain
            // (Ability)" and so on. Naming the one you want removes the guesswork about id order
            // entirely, and keeps whatever special-casing the ability path carries.
            if (marker == "{TERRAIN}")
            {
                string which = Raw("TERRAIN").ToLowerInvariant();
                string kind = Raw("SETTER").StartsWith("abil", StringComparison.OrdinalIgnoreCase)
                    ? "Ability" : "Move";

                string name = which switch
                {
                    var s when s.StartsWith("grass") => "Set Grassy Terrain",
                    var s when s.StartsWith("mist") => "Set Misty Terrain",
                    var s when s.StartsWith("psy") => "Set Psychic Terrain",
                    _ => "Set Electric Terrain",
                };

                // Only Electric and Psychic have a documented ability variant; the others fall back
                // to the move setter rather than resolving to nothing.
                bool hasAbilityVariant = name is "Set Electric Terrain" or "Set Psychic Terrain";
                string suffix = kind == "Ability" && hasAbilityVariant ? "(Ability)" : "(Move)";

                body.Add($"BL   {{sym:{name} {suffix}}}");
                continue;
            }

            if (marker == "{HAZARD}")
            {
                bool toxic = Raw("WHICH").StartsWith("tox", StringComparison.OrdinalIgnoreCase);
                body.Add(toxic
                    ? "BL   {sym:Set Toxic Spikes}"
                    : "BL   {sym:Set Spikes}");
                continue;
            }

            if (marker == "{HPCUT}")
            {
                int pinch = Math.Max(Num("PINCH", Num("THRESHOLD", 3)), 1);
                uint cut = (uint)(4096 / pinch);
                body.Add($"@ 1/{pinch} of max HP as Q12 = {cut}");
                body.AddRange(EmitLoadConstant("r4", cut));
                continue;
            }

            if (marker == "{HEAVIERBR}")
            {
                bool strict = !Raw("STRICT").StartsWith("n", StringComparison.OrdinalIgnoreCase);
                body.Add(strict
                    ? "BLE  done                           @ target not strictly heavier, do nothing"
                    : "BLT  done                           @ target lighter, do nothing (equal weight passes)");
                continue;
            }

            if (marker == "{SLEEPRESULT}")
            {
                bool invert = Raw("INVERT").StartsWith("y", StringComparison.OrdinalIgnoreCase);
                body.Add(invert
                    ? "MOVEQ r0, #0                        @ not asleep - allow"
                    : "MOVEQ r0, #1                        @ not asleep - report failure");
                body.Add(invert
                    ? "MOVNE r0, #1                        @ asleep - report failure"
                    : "MOVNE r0, #0                        @ asleep - allow");
                continue;
            }

            if (marker == "{SCREEN}")
            {
                string w = Raw("WHICH").ToLowerInvariant();
                bool veil = w.StartsWith("veil") || w.StartsWith("aurora");
                body.Add(veil
                    ? "BL   {sym:Aurora Veil set Screen}   @ hail-gated, reduces both attack types"
                    : "BL   {sym:Set Reflect 0xC0}         @ no weather precondition");
                continue;
            }

            if (marker == "{MATCH}")
            {
                body.AddRange(EmitMatch(Raw("MATCH"), Num("TYPE", 255), Math.Clamp(Num("BIT", 255), 0, 255)));
                continue;
            }

            body.Add(SafeComment(Fill(line)));
        }

        return new CustomFunctionDefinition
        {
            Name = string.IsNullOrWhiteSpace(functionName) ? Name.Replace(" ", "") : functionName,
            Description = Summary,
            Mechanic = Kind,
            Target = Target,
            MechanicIndex = -1,
            Assembly = body,
        };
    }
}

/// <summary>The built-in template library.</summary>
public static class FunctionTemplates
{
    public static IReadOnlyList<FunctionTemplate> All => Templates;

    public static IEnumerable<FunctionTemplate> OfKind(CustomMechanicKind kind) =>
        Templates.Where(t => t.Kind == kind);

    public static FunctionTemplate? ByName(string name) =>
        Templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static readonly List<FunctionTemplate> Templates =
    [
        // ---------------------------------------------------------------- abilities
        new()
        {
            Name = "Ability: boost moves in a flag category",
            Summary = "Multiplies power for moves carrying a chosen flag, as Iron Fist does for Punch.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Iron Fist.xlsx",
            ModelledOn = new() { Name = "Iron Fist", Slot = 0x47 },
            TimingHint = "Power-calculation timing; Iron Fist's own slot is the one to copy.",
            Parameters =
            [
                new() { Key = "BIT", Label = "Flag bit", Default = "17",
                        Help = "17 is F18, the first unused bit. Must match the flag ticked on the moves." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3",
                        Help = "3/2 = 1.5x. Integer maths only - there is no FPU here." },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify =
            [
                "Confirm r0 holds the base power and r1 the move pointer at your chosen timing.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "LDR  r4, [r1, #{off:move.flags}]   @ move's flag word",
                "TST  r4, #{BITMASK}                 @ is it in the category?",
                "BEQ  done",
                "{SCALE}",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: immunity to a flag category",
            Summary = "Incoming moves carrying a flag fail outright, as Wind Rider does for Wind.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/Immune to Attacking Type.xlsx",
            ModelledOn = new() { Name = "Levitate", Slot = 0x20 },
            TimingHint = "Must resolve BEFORE damage - an immunity that runs late reduces damage instead of preventing it.",
            Parameters =
            [
                new() { Key = "BIT", Label = "Flag bit", Default = "18", Help = "18 is F19." },
            ],
            Verify =
            [
                "Confirm the return convention: non-zero in r0 usually means 'immune, stop here'.",
                "Pick a timing that runs before damage is applied.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "LDR  r4, [r1, #{off:move.flags}]",
                "TST  r4, #{BITMASK}",
                "MOVNE r0, #1                        @ immune",
                "MOVEQ r0, #0                        @ not immune, carry on",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: react to being hit",
            Summary = "Runs when the holder is hit, optionally only by moves in a flag category. Flame Body / Wind Power shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Flame Body.xlsx",
            ModelledOn = new() { Name = "Flame Body", Slot = 0x60 },
            TimingHint = "The on-being-hit slot; Flame Body's own timing is the reference.",
            Parameters =
            [
                new() { Key = "BIT", Label = "Flag bit to require", Default = "18",
                        Help = "Set to -1 and delete the TST/BEQ pair to react to every hit." },
                new() { Key = "STATUS", Label = "Status id to apply", Default = "0",
                        Help = "Passed to the stock status helper." },
            ],
            Verify =
            [
                "Confirm r0 is the defender pointer at this timing.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "LDR  r4, [r1, #{off:move.flags}]",
                "TST  r4, #{BITMASK}",
                "BEQ  done",
                "MOV  r2, #{STATUS}",
                "BL   {sym:Roll and inflict Status}                 @ r0 = target, r1 = base pointer, r2 = status",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: raise a stat on trigger",
            Summary = "Raises one of the holder's stats when a condition is met. Anger Point / Berserk shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Anger Point.xlsx",
            ModelledOn = new() { Name = "Anger Point", Slot = 0x60 },
            Parameters =
            [
                new() { Key = "STAT", Label = "Stat index", Default = "1",
                        Help = "1 Attack, 2 Defence, 3 Sp. Atk, 4 Sp. Def, 5 Speed. This routine has no HP slot." },
                new() { Key = "STAGES", Label = "Stages to raise", Default = "1" },
            ],
            Verify =
            [
                "Confirm r0 is the holder pointer at this timing.",
            ],
            Body =
            [
                "PUSH {lr}",
                "MOV  r2, #{STAT}                    @ 1-5 = Atk/Def/SpA/SpD/Spe",
                "MOV  r3, #{STAGES}",
                "BL   {sym:stat stage change}",
                "POP  {pc}",
            ],
        },

        // ---------------------------------------------------------------- items
        new()
        {
            Name = "Item: boost moves in a flag category",
            Summary = "Held item multiplies power for a flag category. Punching Glove shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Soul Dew.xlsx",
            ModelledOn = new() { Name = "Soul Dew", Slot = 0x47 },
            Parameters =
            [
                new() { Key = "BIT", Label = "Flag bit", Default = "17" },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify =
            [
                "Confirm the held item has already been checked before this runs, or add the check.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "LDR  r4, [r1, #{off:move.flags}]",
                "TST  r4, #{BITMASK}",
                "BEQ  done",
                "{SCALE}",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Item: reduce incoming damage",
            Summary = "Held item scales damage taken, optionally only from one category. Eviolite shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Eviolite.xlsx",
            ModelledOn = new() { Name = "Eviolite", Slot = 0x4B },
            Parameters =
            [
                new() { Key = "NUM", Label = "Numerator", Default = "2", Help = "2/3 = two thirds damage taken." },
                new() { Key = "DEN", Label = "Denominator", Default = "3" },
            ],
            Verify =
            [
                "Confirm r0 holds the damage value at this timing.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "{SCALE}",
                "POP  {r4, pc}",
            ],
        },

        // ---------------------------------------------------------------- moves
        new()
        {
            Name = "Move: side effect after damage",
            Summary = "A damaging move that also changes field or side state. Genesis Supernova / Ceaseless Edge shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Genesis Supernova - set Type-matching Terrain.xlsx",
            ModelledOn = new() { Kind = CustomMechanicKind.Move, Name = "Grassy Terrain", Slot = 0xBD },
            TimingHint = "After damage resolves - too early and the hit has not landed yet.",
            Parameters =
            [
                new() { Key = "SLOT", Label = "Side-state slot", Default = "0",
                        Help = "Which hazard/screen slot to touch. Defog.xlsx enumerates them." },
                new() { Key = "CAP", Label = "Maximum layers", Default = "1",
                        Help = "Spikes caps at 3, Toxic Spikes at 2, screens at 1." },
            ],
            Verify =
            [
                "r0 must already hold the side-state pointer when this runs. It is the value the "
                + "field-effect timing hands you; do not call a setter first and use its return, "
                + "because the setters report success rather than a pointer.",
                "Confirm you are writing to the target's side, not the user's - this is the usual bug.",
                "For Spikes or Toxic Spikes specifically, prefer \"Move: lay an entry hazard after "
                + "damage\": their own setters know their caps and print the right message. This "
                + "template is for side state those setters do not cover.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "@ r0 = side-state pointer, supplied by the caller at this timing",
                "LDRB r4, [r0, #{SLOT}]              @ current layer count",
                "CMP  r4, #{CAP}",
                "BGE  done                           @ already at the cap",
                "ADD  r4, r4, #1",
                "STRB r4, [r0, #{SLOT}]",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Move: scale power from a condition",
            Summary = "Power varies with HP, weight, stat stages or similar. Crush Grip / Power Whip shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Crush Grip.xlsx",
            ModelledOn = new() { Name = "Crush Grip", Slot = 0x46 },
            Parameters =
            [
                new() { Key = "MAX", Label = "Maximum power", Default = "120" },
            ],
            Verify =
            [
                "Confirm r0 is the base power on entry and the returned power on exit.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "BL   {sym:percent of HP to recover}                       @ r0 = remaining-HP ratio, Q12",
                "MOV  r4, #{MAX}",
                "MUL  r5, r0, r4                     @ ratio * max power",
                "LSR  r0, r5, #12                    @ back out of Q12",
                "CMP  r0, #1",
                "MOVLT r0, #1                        @ never return zero power",
                "POP  {r4, r5, pc}",
            ],
        },

        // ---------------------------------------------------------------- more abilities
        new()
        {
            Name = "Ability: absorb a move type",
            Summary = "Moves of one type do nothing and heal or boost the holder instead. Volt Absorb / Flash Fire shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/Immune to Attacking Type.xlsx",
            ModelledOn = new() { Name = "Storm Drain", Slot = 0x32 },
            TimingHint = "Before damage - the move must fail, not land for reduced damage.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Type index to absorb", Default = "12",
                        Help = "0 Normal, 1 Fighting, 2 Flying, 3 Poison, 4 Ground, 5 Rock, 6 Bug, 7 Ghost, "
                             + "8 Steel, 9 Fire, 10 Water, 11 Grass, 12 Electric, 13 Psychic, 14 Ice, "
                             + "15 Dragon, 16 Dark, 17 Fairy." },
                new() { Key = "PERCENT", Label = "HP to restore (percent)", Default = "25",
                        Help = "The routine takes a percent, not a denominator. 25 is a quarter, as Volt Absorb." },
            ],
            Verify = ["Confirm r1 points at the incoming move record at your chosen timing."],
            Body =
            [
                "PUSH {r4, lr}",
                "LDRB r4, [r1, #{off:move.type}]",
                "CMP  r4, #{TYPE}",
                "BNE  notabsorbed",
                "@ absorbed: heal and report immunity",
                "MOV  r2, #{PERCENT}                 @ percent of max HP",
                "BL   {sym:percent of HP to recover}",
                "MOV  r0, #1",
                "POP  {r4, pc}",
                "notabsorbed:",
                "MOV  r0, #0",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: punish contact",
            Summary = "Attackers making contact take damage or a status. Rough Skin / Flame Body shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/Inflict Status on Any Contact.xlsx",
            ModelledOn = new() { Name = "Rough Skin", Slot = 0x60 },
            TimingHint = "After the hit resolves, so the attacker is known and still on the field.",
            Parameters =
            [
                new() { Key = "STATUS", Label = "Status id to inflict", Default = "0",
                        Help = "0 to skip the status and only check contact." },
            ],
            Verify =
            [
                "Confirm r0 is the ATTACKER at this timing - punishing your own holder is the usual slip.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "LDR  r4, [r1, #{off:move.flags}]",
                "TST  r4, #0x1                       @ MakesContact is bit 0",
                "BEQ  done",
                "MOV  r2, #{STATUS}",
                "BL   {sym:Inflicts Input Status on contact}",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: block stat drops",
            Summary = "The holder's stats cannot be lowered by an opponent. Clear Body / Big Pecks shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Clear Body.xlsx",
            ModelledOn = new() { Name = "Clear Body", Slot = 0x72 },
            TimingHint = "The stat-change hook, before the change is applied.",
            Parameters =
            [
                new() { Key = "STAT", Label = "Stat to protect", Default = "255",
                        Help = "255 protects every stat; 1 protects Attack only, as Hyper Cutter does." },
            ],
            Verify = ["Confirm r1 is the stat index and r2 the signed number of stages."],
            Body =
            [
                "PUSH {lr}",
                "CMP  r2, #0",
                "BGE  allow                          @ a raise is never blocked",
                "MOV  r0, #{STAT}",
                "CMP  r0, #255",
                "BEQ  block                          @ protecting everything",
                "CMP  r0, r1",
                "BNE  allow                          @ a different stat, let it through",
                "block:",
                "MOV  r0, #1                         @ blocked",
                "POP  {pc}",
                "allow:",
                "MOV  r0, #0",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Ability: modify accuracy",
            Summary = "Scales the holder's accuracy. Compound Eyes / Hustle shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Keen Eye.xlsx",
            ModelledOn = new() { Name = "Snow Cloak", Slot = 0x41 },
            TimingHint = "The accuracy-calculation hook.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "13",
                        Help = "13/10 = 1.3x, which is Compound Eyes." },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "10" },
            ],
            Verify = ["Confirm r0 holds accuracy on entry and on exit at this timing."],
            Body =
            [
                "PUSH {r4, lr}",
                "{SCALE}",
                "MOV  r4, #100",
                "CMP  r0, r4",
                "MOVGT r0, r4                        @ clamp at 100",
                "POP  {r4, pc}",
            ],
        },

        // ---------------------------------------------------------------- more items
        new()
        {
            Name = "Item: trigger below an HP threshold",
            Summary = "Held item activates once the holder drops below a fraction of max HP. Pinch berry shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Generic Functions/Return Multiplier based on HP left.xlsx",
            ModelledOn = new() { Name = "Sitrus Berry", Slot = 0xAF },
            TimingHint = "End of turn, or after damage, depending on whether it should react instantly.",
            Parameters =
            [
                new() { Key = "THRESHOLD", Label = "Trigger at 1/N max HP", Default = "4",
                        Help = "4 fires at a quarter HP, as most pinch berries do." },
                new() { Key = "PERCENT", Label = "HP to restore (percent)", Default = "25" },
            ],
            Verify =
            [
                "Confirm r0 is the holder pointer, and that the item has already been matched.",
                "Add consumption after the effect if the item should be used up.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:percent of HP to recover}",
                "@ result is Q12: 4096 is full HP",
                "{HPCUT}",
                "CMP  r0, r4",
                "BGT  done                           @ still above the threshold",
                "MOV  r2, #{PERCENT}                 @ percent of max HP",
                "BL   {sym:percent of HP to recover}",
                "done:",
                "POP  {r4, pc}",
            ],
        },

        // ---------------------------------------------------------------- more moves
        new()
        {
            Name = "Move: fixed number of hits",
            Summary = "Always strikes a set number of times. Population Bomb / Triple Kick shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Scale Shot.xlsx",
            ModelledOn = new() { Kind = CustomMechanicKind.Move, Name = "Triple Kick", Slot = 0x43 },
            TimingHint = "The hit-count hook, before damage is rolled.",
            Parameters =
            [
                new() { Key = "HITS", Label = "Number of hits", Default = "3",
                        Help = "Returned as-is, so the move always strikes exactly this many times. "
                             + "For the 2-5 random spread, use the move record's own multi-hit field "
                             + "instead - it needs no code." },
            ],
            Verify =
            [
                "Timing 0x43 is the hit-count hook. Triple Kick and Battle Bond are the two stock "
                + "users, and Battle Bond's whole effect is changing Water Shuriken's hit count - "
                + "which is what confirms what this timing is for.",
                "Each hit rolls damage separately, so a fixed count multiplies total damage. Drop "
                + "the move's base power accordingly or it will hit far harder than intended.",
            ],
            Body =
            [
                "MOV  r0, #{HITS}",
                "BX   lr",
            ],
        },
        new()
        {
            Name = "Move: bonus power under a condition",
            Summary = "Power increases when a check passes - a weather, a status, a field state. Facade / Brine shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Weather Ball.xlsx",
            ModelledOn = new() { Name = "Crush Grip", Slot = 0x46 },
            TimingHint = "The power-calculation hook.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "2" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "1" },
            ],
            Verify =
            [
                "Replace the CONDITION branch with the check you want - the corpus sheet shows how "
                + "the stock version reads weather.",
                "Confirm r0 is base power on entry and returned power on exit.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ keep base power; the call returns in r0",
                "BL   {sym:Roll and inflict Status}",
                "CMP  r0, #0",
                "MOV  r0, r5                         @ restore base power either way",
                "BEQ  done                           @ condition not met, power unchanged",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },

        // ------------------------------------------------------- abilities that do two things
        new()
        {
            Name = "Ability: boost moves by type and/or flag",
            Summary = "Scales power or accuracy for moves matching a type, a flag category, or both. "
                    + "Steelworker, Sharpness and 'Wind moves never miss' are all this one shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Iron Fist.xlsx",
            ModelledOn = new() { Name = "Iron Fist", Slot = 0x47 },
            TimingHint = "Power or accuracy calculation, depending on which value you are scaling.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Move type (255 = any)", Default = "255",
                        Help = "0 Normal, 2 Flying, 8 Steel, 9 Fire, 12 Electric... 255 skips the type test." },
                new() { Key = "BIT", Label = "Flag bit (255 = any)", Default = "17",
                        Help = "17 is F18. 255 skips the flag test." },
                new() { Key = "MATCH", Label = "Require both, or either", Default = "both",
                        Help = "\"both\" needs the type AND the flag. \"either\" accepts one." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify = ["Confirm r0 is the value being scaled and r1 the move record at this timing."],
            Body =
            [
                "PUSH {r4, lr}",
                "{MATCH}",
                "{SCALE}",
                "nomatch:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: immunity plus a stat boost",
            Summary = "Absorbs a type or flag category outright AND raises a stat for it. "
                    + "Wind Rider, Motor Drive, Sap Sipper and Lightning Rod shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/Immune to Attacking Type.xlsx",
            ModelledOn = new() { Name = "Storm Drain", Slot = 0x32 },
            TimingHint = "Before damage. Both halves fire here: the boost is part of the absorb, not a separate hook.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Move type (255 = any)", Default = "255" },
                new() { Key = "BIT", Label = "Flag bit (255 = any)", Default = "18",
                        Help = "18 is F19 - Wind, if you bound it there." },
                new() { Key = "MATCH", Label = "Require both, or either", Default = "both" },
                new() { Key = "STAT", Label = "Stat to raise", Default = "1",
                        Help = "1 Attack, 2 Defence, 3 Sp. Atk, 4 Sp. Def, 5 Speed. This routine has no HP slot." },
                new() { Key = "STAGES", Label = "Stages", Default = "1" },
            ],
            Verify =
            [
                "Confirm returning 1 in r0 means 'immune, stop here' at your chosen timing.",
                "r0 is reused for the holder pointer before the stat call - check that matches the helper.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ keep the holder pointer",
                "{MATCH}",
                "@ first function: raise the stat",
                "MOV  r0, r5",
                "MOV  r2, #{STAT}                    @ 1-5 = Atk/Def/SpA/SpD/Spe",
                "MOV  r3, #{STAGES}",
                "BL   {sym:stat stage change}",
                "@ second function: report the move as having no effect",
                "MOV  r0, #1",
                "POP  {r4, r5, pc}",
                "nomatch:",
                "MOV  r0, #0",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Ability: one stat up, another down",
            Summary = "Trades one stat against another. Hustle, Gorilla Tactics and Slow Start shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Defeatist-Invictist.xlsx",
            ModelledOn = new() { Name = "Defeatist", Slot = 0x4A },
            TimingHint = "The stat-calculation hook; r1 usually says which stat is being asked for.",
            Parameters =
            [
                new() { Key = "UPSTAT", Label = "Stat to raise", Default = "1", Help = "1 Attack." },
                new() { Key = "NUM", Label = "Raise numerator", Default = "3" },
                new() { Key = "DEN", Label = "Raise denominator", Default = "2" },
                new() { Key = "DOWNSTAT", Label = "Stat to lower", Default = "6",
                        Help = "6 for accuracy, as Hustle does." },
                new() { Key = "NUM2", Label = "Lower numerator", Default = "4" },
                new() { Key = "DEN2", Label = "Lower denominator", Default = "5" },
            ],
            Verify = ["Confirm r0 is the stat value and r1 the stat index at this timing."],
            Body =
            [
                "PUSH {r4, lr}",
                "CMP  r1, #{UPSTAT}",
                "BNE  checkdown",
                "@ first function: the upside",
                "{SCALE}",
                "POP  {r4, pc}",
                "checkdown:",
                "CMP  r1, #{DOWNSTAT}",
                "BNE  done",
                "@ second function: the cost",
                "{SCALE2}",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: pinch boost for one type",
            Summary = "Below a HP threshold, moves of one type gain power. Overgrow, Blaze, Torrent, Swarm shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/Boost input Type damage based on HP.xlsx",
            ModelledOn = new() { Name = "Blaze", Slot = 0x4A },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Move type", Default = "11", Help = "11 Grass, 9 Fire, 10 Water, 6 Bug." },
                new() { Key = "BIT", Label = "Flag bit (255 = any)", Default = "255" },
                new() { Key = "MATCH", Label = "Require both, or either", Default = "both" },
                new() { Key = "PINCH", Label = "Trigger at 1/N max HP", Default = "3",
                        Help = "3 is the usual pinch threshold." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify =
            [
                "Load the holder's Pokemon pointer into r6 before the HP block. At power-calculation "
                + "timing it is not in a register, so it has to come from the caller's frame.",
                "Current HP is read through the routine Innards Out uses, which is a direct reader - "
                + "the generic accessor at 0x02FA84 would also work but the corpus does not document "
                + "its field selector.",
            ],
            Body =
            [
                "PUSH {r4, r5, r6, lr}",
                "MOV  r5, r0                         @ base power, across the HP call",
                "{MATCH}",
                "@ matched - now the HP test",
                "@ r6 must hold the holder's Pokemon pointer. At power-calculation timing it is not",
                "@ in a register, so fetch it with the generic accessor first - see Verify.",
                "MOV  r0, r6",
                "MOV  r1, #{PINCH}                   @ routine returns MaxHP / r1",
                "BL   {sym:Max HP/r1, of pointer Pokemon in r0}",
                "MOV  r4, r0                         @ threshold",
                "MOV  r0, r6",
                "BL   {sym:Innards Out Get Remaining HP 0x88}",
                "@ r0 is now current HP",
                "CMP  r0, r4",
                "MOV  r0, r5                         @ restore base power",
                "BGT  nomatch                        @ above the threshold, no boost",
                "{SCALE}",
                "nomatch:",
                "POP  {r4, r5, r6, pc}",
            ],
        },
        new()
        {
            Name = "Ability: boost on knockout",
            Summary = "Raises a stat each time the holder faints an opponent. Moxie, Beast Boost, Chilling Neigh shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Berserk.xlsx",
            ModelledOn = new() { Name = "Moxie", Slot = 0x9E },
            TimingHint = "After the target faints, while the holder is still the acting Pokemon.",
            Parameters =
            [
                new() { Key = "STAT", Label = "Stat to raise", Default = "1" },
                new() { Key = "STAGES", Label = "Stages", Default = "1" },
            ],
            Verify =
            [
                "Confirm the target has actually fainted at this timing rather than merely been hit.",
                "Confirm r0 is the holder, not the target.",
            ],
            Body =
            [
                "PUSH {lr}",
                "MOV  r2, #{STAT}                    @ 1-5 = Atk/Def/SpA/SpD/Spe",
                "MOV  r3, #{STAGES}",
                "BL   {sym:stat stage change}",
                "POP  {pc}",
            ],
        },

        // ------------------------------------------------------- items that do two things
        new()
        {
            Name = "Item: boost one type, weaken another",
            Summary = "A held item with an upside and a drawback. Type-gem-with-a-cost shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Soul Dew.xlsx",
            ModelledOn = new() { Name = "Soul Dew", Slot = 0x47 },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Type to boost", Default = "9" },
                new() { Key = "BIT", Label = "Flag bit (255 = any)", Default = "255" },
                new() { Key = "MATCH", Label = "Require both, or either", Default = "both" },
                new() { Key = "NUM", Label = "Boost numerator", Default = "3" },
                new() { Key = "DEN", Label = "Boost denominator", Default = "2" },
                new() { Key = "PENALTYTYPE", Label = "Type to weaken", Default = "10" },
                new() { Key = "NUM2", Label = "Penalty numerator", Default = "1" },
                new() { Key = "DEN2", Label = "Penalty denominator", Default = "2" },
            ],
            Verify = ["Confirm the held item has already been matched before this runs."],
            Body =
            [
                "PUSH {r4, lr}",
                "{MATCH}",
                "@ first function: the boost",
                "{SCALE}",
                "POP  {r4, pc}",
                "nomatch:",
                "@ second function: the drawback, on a different type",
                "LDRB r4, [r1, #{off:move.type}]",
                "CMP  r4, #{PENALTYTYPE}",
                "BNE  done",
                "{SCALE2}",
                "done:",
                "POP  {r4, pc}",
            ],
        },

        new()
        {
            Name = "Move: set weather after damage",
            Summary = "A damaging move that also sets weather. Sandsear Storm / Bleakwind Storm shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Weather Setting Moves.xlsx",
            ModelledOn = new() { Kind = CustomMechanicKind.Move, Name = "Grassy Terrain", Slot = 0xBD },
            TimingHint = "After damage resolves. Setting weather before the hit lets it change the damage the move itself deals.",
            Parameters =
            [
                new() { Key = "WEATHER", Label = "Weather id", Default = "1",
                        Help = "Engine order, commonly 1 Sun, 2 Rain, 3 Sand, 4 Hail/Snow. Confirm against the sheet." },
                new() { Key = "TURNS", Label = "Duration in turns", Default = "5",
                        Help = "5 is the move default; extending rocks usually push this to 8." },
            ],
            Verify =
            [
                "Confirm the weather setter's argument order - some builds take (id, turns), others (turns, id).",
                "Decide whether an extending rock should apply. If so, read the holder's item and branch before the call.",
            ],
            Body =
            [
                "PUSH {lr}",
                "MOV  r0, #{WEATHER}",
                "MOV  r1, #{TURNS}",
                "BL   {sym:Set Weather/Terrain}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: set terrain after damage",
            Summary = "A damaging move that also sets terrain. Genesis Supernova shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Genesis Supernova - set Type-matching Terrain.xlsx",
            ModelledOn = new() { Name = "Grassy Terrain", Slot = 0xBD },
            TimingHint = "After damage resolves.",
            Parameters =
            [
                new() { Key = "TERRAIN", Label = "Terrain", Default = "electric",
                        Help = "electric, grassy, misty or psychic - each is its own routine." },
                new() { Key = "SETTER", Label = "Move or ability variant", Default = "move" },
            ],
            Verify =
            [
                "Genesis Supernova picks its terrain from the move's type. To copy that, read the type "
                + "with LDRB r0, [r1, #0x00] and branch to the matching setter instead of a fixed one.",
            ],
            Body =
            [
                "PUSH {lr}",
                "{TERRAIN}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: lay an entry hazard after damage",
            Summary = "A damaging move that also lays a hazard on the target's side. "
                    + "Ceaseless Edge / Stone Axe shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Defog.xlsx",
            ModelledOn = new() { Name = "Spikes", Slot = 0xC0 },
            TimingHint = "After damage resolves, so the hit has landed and the defending side is known.",
            Parameters =
            [
                new() { Key = "SLOT", Label = "Hazard slot", Default = "0",
                        Help = "Defog.xlsx enumerates the slots in order while clearing them." },
                new() { Key = "CAP", Label = "Maximum layers", Default = "3",
                        Help = "Spikes 3, Toxic Spikes 2, Stealth Rock and Sticky Web 1." },
            ],
            Verify =
            [
                "Confirm the state pointer returned is the DEFENDER's side. Laying hazards on your own "
                + "side is the classic failure here and looks like the move doing nothing.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "BL   {sym:Set Weather/Terrain}",
                "MOV  r5, r0                         @ side state block",
                "LDRB r4, [r5, #{SLOT}]",
                "CMP  r4, #{CAP}",
                "BGE  done                           @ already at the cap, do nothing",
                "ADD  r4, r4, #1",
                "STRB r4, [r5, #{SLOT}]",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Move: set or cancel a room",
            Summary = "Trick Room, Magic Room and Wonder Room shape - using it again while active turns it off.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Field Effects/Inverse Room.xlsx",
            ModelledOn = new() { Name = "Trick Room", Slot = 0xBD },
            TimingHint = "On use. Rooms are field-wide, so there is no attacker or defender side to pick.",
            Parameters =
            [
                new() { Key = "SLOT", Label = "Room state slot", Default = "0" },
                new() { Key = "TURNS", Label = "Duration in turns", Default = "5" },
            ],
            Verify =
            [
                "Confirm the room's counter lives at this slot and that zero means inactive.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "BL   {sym:Set Weather/Terrain}",
                "MOV  r5, r0                         @ field state block",
                "LDRB r4, [r5, #{SLOT}]",
                "CMP  r4, #0",
                "BNE  cancel                         @ already up, so this use ends it",
                "MOV  r4, #{TURNS}",
                "STRB r4, [r5, #{SLOT}]",
                "POP  {r4, r5, pc}",
                "cancel:",
                "MOV  r4, #0",
                "STRB r4, [r5, #{SLOT}]",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Move: clear field or side effects",
            Summary = "Removes hazards, screens or terrain. Defog / Rapid Spin / Ice Spinner shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Defog.xlsx",
            ModelledOn = new() { Name = "Defog", Slot = 0xBF },
            TimingHint = "After damage for Rapid Spin and Ice Spinner; on use for Defog.",
            Parameters =
            [
                new() { Key = "FIRST", Label = "First slot to clear", Default = "0" },
                new() { Key = "COUNT", Label = "How many consecutive slots", Default = "4",
                        Help = "Hazard slots are contiguous, so a short run clears them all." },
            ],
            Verify =
            [
                "Confirm the slots you are clearing are contiguous in this build - Defog.xlsx lists them.",
                "Decide whether this clears the user's side, the opponent's, or both. Defog does both.",
            ],
            Body =
            [
                "PUSH {r4, r5, r6, lr}",
                "BL   {sym:Set Weather/Terrain}",
                "MOV  r5, r0                         @ state block",
                "ADD  r5, r5, #{FIRST}               @ walk a pointer instead of indexing",
                "MOV  r4, #{COUNT}                   @ counting down avoids a second compare",
                "MOV  r6, #0",
                "clearloop:",
                "STRB r6, [r5, #0]",
                "ADD  r5, r5, #1",
                "SUBS r4, r4, #1",
                "BNE  clearloop",
                "POP  {r4, r5, r6, pc}",
            ],
        },
        new()
        {
            Name = "Ability: set a field effect on entry",
            Summary = "Weather or terrain set when the holder switches in. Drizzle, Drought, Electric Surge shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Move Edits/Weather Setting Moves.xlsx",
            ModelledOn = new() { Name = "Drought", Slot = 0xA7 },
            TimingHint = "The switch-in hook, the same one Intimidate uses.",
            Parameters =
            [
                new() { Key = "EFFECT", Label = "Weather or terrain id", Default = "2" },
                new() { Key = "TURNS", Label = "Duration in turns", Default = "5",
                        Help = "Ability-set weather ran indefinitely before Gen 6 and 5 turns after." },
            ],
            Verify =
            [
                "Confirm the state slot matches what you are setting - weather and terrain differ.",
                "Consider whether it should overwrite weather another ability just set.",
            ],
            Body =
            [
                "PUSH {lr}",
                "MOV  r0, #{EFFECT}",
                "MOV  r1, #{TURNS}",
                "BL   {sym:Set Weather/Terrain}",
                "POP  {pc}",
            ],
        },

        // ---- built on routines confirmed present in the corpus ----------------------------
        new()
        {
            Name = "Move: lay Spikes or Toxic Spikes",
            Summary = "A damaging move that lays a specific hazard, using the game's own setter rather "
                    + "than writing the side-state byte by hand.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Defog.xlsx",
            ModelledOn = new() { Name = "Spikes", Slot = 0xC0 },
            TimingHint = "After damage resolves.",
            Parameters =
            [
                new() { Key = "WHICH", Label = "Hazard", Default = "spikes",
                        Help = "\"spikes\" or \"toxic\". Each has its own routine, so the layer cap and "
                             + "the Poison-type absorb rule come for free." },
            ],
            Verify = ["Confirm the routine targets the defending side at this timing."],
            Body =
            [
                "PUSH {lr}",
                "{HAZARD}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Ability: prevent critical hits",
            Summary = "Incoming attacks can never land a critical. Battle Armor / Shell Armor shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Battle Armor.xlsx",
            ModelledOn = new() { Name = "Battle Armor", Slot = 0x44 },
            TimingHint = "The critical-hit check, before damage.",
            Parameters = [],
            Verify = ["Confirm the routine reads the DEFENDER's ability, not the attacker's."],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Prevent incoming critical hits}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Item: scale by the held argument",
            Summary = "Reads the item's own HeldArgument as a percentage, so one routine serves a whole "
                    + "family of items whose only difference is a number.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Soul Dew.xlsx",
            ModelledOn = new() { Name = "Soul Dew", Slot = 0x47 },
            TimingHint = "Power or damage calculation.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Type to affect (255 = any)", Default = "255" },
                new() { Key = "BIT", Label = "Flag bit (255 = any)", Default = "255" },
                new() { Key = "MATCH", Label = "Require both, or either", Default = "both" },
            ],
            Verify =
            [
                "This reads HeldArgument from the item record, so set that field in the Item Editor "
                + "rather than editing the routine per item.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ value being scaled",
                "{MATCH}",
                "BL   {sym:Returns value from Held Argument}",
                "@ r0 is now the numerator over 100",
                "MUL  r4, r5, r0",
                "MOV  r0, #100",
                "@ divide by 100 - the engine's own fraction helper already did this above,",
                "@ so prefer calling it once rather than dividing here",
                "MOV  r0, r4",
                "nomatch:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Ability: act when the holder faints something",
            Summary = "Fires only when the holder's attack actually knocked a Pokemon out. "
                    + "Uses the game's own check rather than inferring it from damage.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Berserk.xlsx",
            ModelledOn = new() { Name = "Moxie", Slot = 0x9E },
            TimingHint = "After damage, while the holder is still the acting Pokemon.",
            Parameters =
            [
                new() { Key = "STAT", Label = "Stat to raise", Default = "1",
                        Help = "1 Attack, 2 Defence, 3 Sp. Atk, 4 Sp. Def, 5 Speed." },
                new() { Key = "STAGES", Label = "Stages", Default = "1" },
            ],
            Verify =
            [
                "This uses the routine that asks whether a specific Pokemon caused the knockout, so it "
                + "does not fire on an ally's. The looser check at 0x061538 is the one shared by Battle "
                + "Bond and Soul Heart, where an undocumented parameter selects between the two meanings.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ keep the holder",
                "MOV  r2, r5                         @ routine takes the Pokemon in r2",
                "BL   {sym:just made any other Pokemon's HP become 0 by attacking}",
                "CMP  r0, #0",
                "BEQ  done",
                "MOV  r0, r5",
                "MOV  r2, #{STAT}                    @ 1-5 = Atk/Def/SpA/SpD/Spe",
                "MOV  r3, #{STAGES}",
                "BL   {sym:stat stage change}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Ability: change the holder's Speed",
            Summary = "Scales Speed, correctly accounting for paralysis and Trick Room by using the "
                    + "engine's true-Speed routine. Swift Swim / Chlorophyll / Slush Rush shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Hydration.xlsx",
            ModelledOn = new() { Name = "Swift Swim", Slot = 0x13 },
            TimingHint = "The Speed-calculation hook.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "2" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "1" },
            ],
            Verify =
            [
                "Add the weather or terrain condition before the scale - as written this doubles Speed "
                + "unconditionally.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:Return true Speed}",
                "{SCALE}",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Move: power from the target's weight",
            Summary = "Heavier targets take more. Heavy Slam, Grass Knot and Low Kick shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Crush Grip.xlsx",
            ModelledOn = new() { Name = "Low Kick", Slot = 0x46 },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "CUT1", Label = "Weight for the first step (kg)", Default = "25" },
                new() { Key = "POW1", Label = "Power below that", Default = "40" },
                new() { Key = "POW2", Label = "Power above it", Default = "80" },
            ],
            Verify =
            [
                "The routine returns decigrams - 10 per kg - so the thresholds here are multiplied by 10 "
                + "before comparing. Add further steps by repeating the compare.",
                "Confirm r0 is the target when the weight call is made.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "BL   {sym:Returns weight in r0}",
                "MOV  r4, #{CUT1}",
                "MOV  r5, #10",
                "MUL  r4, r4, r5                     @ kg to decigrams",
                "CMP  r0, r4",
                "MOVLT r0, #{POW1}",
                "MOVGE r0, #{POW2}",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Ability: change damage by type effectiveness",
            Summary = "Alters damage only when the hit is resisted or super-effective. "
                    + "Tinted Lens, Filter, Solid Rock and Neuroforce shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/Get Type Effectiveness of r0 on r1.xlsx",
            ModelledOn = new() { Name = "Solid Rock", Slot = 0x5B },
            TimingHint = "Damage calculation, after effectiveness is known.",
            Parameters =
            [
                new() { Key = "WHEN", Label = "Effectiveness value to react to", Default = "4",
                        Help = "The routine returns 0-0xE. Compare against the value that means resisted "
                             + "or super-effective in this build before relying on a number here." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "2" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "1" },
            ],
            Verify =
            [
                "Map the 0-0xE scale before setting WHEN - it is not a plain multiplier.",
                "The call takes a pointer in r0 and a party index in r1, so preserve the damage value.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ keep damage across the call",
                "BL   {sym:Returns number in [0,0xE] for the Type Effectiveness}",
                "CMP  r0, #{WHEN}",
                "MOV  r0, r5                         @ restore damage",
                "BNE  done",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Ability: take the target's item",
            Summary = "Steals a held item when the holder connects, if the holder has none. "
                    + "Magician and Thief shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Item Edits/Knock Off Remove Item.xlsx",
            ModelledOn = new() { Name = "Magician", Slot = 0x9E },
            TimingHint = "After the hit resolves.",
            Parameters = [],
            Verify =
            [
                "Both halves are here: Knock Off's remover takes the item off the target, Bestow's "
                + "routine hands it over. Confirm Bestow's argument order - it is written for a move "
                + "where the user gives its own item away, so the roles may be reversed.",
                "Confirm the no-item check is reading the ATTACKER at your timing.",
                "Trick/Switcheroo at 0x0C1E34 is the routine to look at if you want a swap rather "
                + "than a one-way take.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:Check If Attacker No Item}",
                "CMP  r0, #0",
                "BEQ  done                           @ already holding something",
                "BL   {sym:Symbiosis check/return target held item}",
                "MOV  r4, r0                         @ what the target is holding",
                "CMP  r4, #0",
                "BEQ  done                           @ target has nothing to take",
                "BL   {sym:Knock Off Remove Item}     @ strip it from the target",
                "MOV  r0, r4                         @ the item taken",
                "BL   {sym:Bestow}                    @ hand it to the holder",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Move: power from party members left",
            Summary = "Stronger with a fuller team, or weaker. Beat Up and Retaliate shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Crush Grip.xlsx",
            ModelledOn = new() { Name = "Reversal", Slot = 0x46 },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "PER", Label = "Power per surviving member", Default = "20" },
            ],
            Verify =
            [
                "The routine counts non-fainted Pokemon on the field; the waiting-party count is a "
                + "different routine at 0x0611C0 if that is what you want.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:Returns count of non-fainted Pokemon on the field}",
                "MOV  r4, #{PER}",
                "MUL  r0, r4, r0",
                "CMP  r0, #1",
                "MOVLT r0, #1                        @ never zero power",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: react to a volatile status",
            Summary = "Fires when the holder carries Confusion, Leech Seed, Perish Song, Ingrain and "
                    + "similar - the conditions that are not the main status byte.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Hydration.xlsx",
            ModelledOn = new() { Name = "Hydration", Slot = 0x8E },
            TimingHint = "End of turn, or the switch-in hook for a Natural Cure style effect.",
            Parameters =
            [
                new() { Key = "WHICH", Label = "Volatile id to test", Default = "0",
                        Help = "The routine covers Confusion, Heal Block, Leech Seed, Embargo, Perish "
                             + "Song, Ingrain, trapping and grounding - confirm the id order first." },
                new() { Key = "STAT", Label = "Stat to raise while afflicted", Default = "1",
                        Help = "1 Attack, 2 Defence, 3 Sp. Atk, 4 Sp. Def, 5 Speed. A Guts-style "
                             + "payoff for carrying the condition." },
                new() { Key = "STAGES", Label = "Stages", Default = "1" },
            ],
            Verify =
            [
                "Confirm the id order the routine expects before trusting WHICH.",
                "The body raises a stat as a worked example of reacting to the condition. Replace that "
                + "call with whatever your ability should do - the detection above it is the reusable part.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "MOV  r4, r0                         @ keep the holder",
                "MOV  r0, #{WHICH}",
                "BL   {sym:Returns true if r0 is Confusion, HealBlock, LeechSeed}",
                "CMP  r0, #0",
                "BEQ  done                           @ condition not present",
                "MOV  r0, r4",
                "MOV  r2, #{STAT}                    @ 1-5 = Atk/Def/SpA/SpD/Spe",
                "MOV  r3, #{STAGES}",
                "BL   {sym:stat stage change}",
                "done:",
                "POP  {r4, pc}",
            ],
        },

        // ================================================================= abilities, second set
        new()
        {
            Name = "Ability: raise a stat if the target is heavier",
            Summary = "Compares the target's weight against the holder's and raises a stat when the "
                    + "target is heavier. The shape for any 'compare two Pokemon, then act' ability.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Generic Functions/return arbitrary pokemon data.xlsx",
            ModelledOn = new() { Name = "Justified", Slot = 0x60 },
            TimingHint = "Switch-in for an Intimidate-style read, or on-hit to react to who you just met.",
            Parameters =
            [
                new() { Key = "STAT", Label = "Stat to raise", Default = "1",
                        Help = "1 Attack, 2 Defence, 3 Sp. Atk, 4 Sp. Def, 5 Speed." },
                new() { Key = "STAGES", Label = "Stages", Default = "1" },
                new() { Key = "STRICT", Label = "Strictly heavier?", Default = "yes",
                        Help = "\"yes\" needs the target strictly heavier; anything else accepts equal." },
            ],
            Verify =
            [
                "Put the holder's pointer in r5 and the target's in r6 before the comparison. Which "
                + "register each arrives in depends on the timing you attach to.",
                "The weight routine returns decigrams, so both sides are in the same unit and no "
                + "conversion is needed - only the comparison matters.",
            ],
            Body =
            [
                "PUSH {r4, r5, r6, lr}",
                "@ r5 = holder, r6 = target (see Verify)",
                "MOV  r0, r6",
                "BL   {sym:Returns weight in r0}",
                "MOV  r4, r0                         @ target weight, decigrams",
                "MOV  r0, r5",
                "BL   {sym:Returns weight in r0}",
                "@ r0 = holder weight, r4 = target weight",
                "CMP  r4, r0",
                "{HEAVIERBR}",
                "MOV  r0, r5                         @ raise the holder's stat",
                "MOV  r2, #{STAT}                    @ 1-5 = Atk/Def/SpA/SpD/Spe",
                "MOV  r3, #{STAGES}",
                "BL   {sym:stat stage change}",
                "done:",
                "POP  {r4, r5, r6, pc}",
            ],
        },
        new()
        {
            Name = "Ability: set terrain on entry",
            Summary = "Terrain set when the holder switches in, using the ability-specific setter. "
                    + "Electric Surge, Grassy Surge, Misty Surge, Psychic Surge shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Move Edits/Weather Setting Moves.xlsx",
            ModelledOn = new() { Name = "Electric Surge", Slot = 0xA7 },
            TimingHint = "The switch-in hook Intimidate uses.",
            Parameters =
            [
                new() { Key = "TERRAIN", Label = "Terrain", Default = "electric",
                        Help = "electric, grassy, misty or psychic. Each has its own routine, so there "
                             + "is no id order to get wrong." },
                new() { Key = "SETTER", Label = "Move or ability variant", Default = "ability",
                        Help = "Electric and Psychic have documented ability variants; the others use "
                             + "the move setter." },
            ],
            Verify = [],
            Body =
            [
                "PUSH {lr}",
                "{TERRAIN}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Ability: react to the last move used",
            Summary = "Branches on what the Pokemon used previously. Stakeout, Analytic and "
                    + "consecutive-use abilities read this.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Other Mechanics/Check for not twice in a row moves.xlsx",
            ModelledOn = new() { Kind = CustomMechanicKind.Ability, Name = "Dancer", Slot = 0x2A },
            TimingHint = "Power calculation, or the start-of-turn hook.",
            Parameters =
            [
                new() { Key = "MOVEID", Label = "Move id to react to", Default = "0",
                        Help = "0 means react to any repeat rather than one specific move." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify = ["The routine reads pointer + 0x24C, so pass the Pokemon whose history you want."],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ value being scaled",
                "BL   {sym:Returns last move used by the Pokemon}",
                "CMP  r0, #{MOVEID}",
                "MOV  r0, r5",
                "BNE  done",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Ability: change forme on a condition",
            Summary = "Switches the holder to another forme. Stance Change, Zen Mode, Schooling shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Battle Bond.xlsx",
            ModelledOn = new() { Name = "Stance Change", Slot = 0x27 },
            TimingHint = "After the triggering event - a hit taken, an HP threshold crossed, a move chosen.",
            Parameters =
            [
                new() { Key = "FORME", Label = "Forme index to switch to", Default = "1" },
            ],
            Verify =
            [
                "Add the trigger condition before the call - as written this changes forme every time.",
                "Forme changes usually need the ability and stats refreshed afterwards; see "
                + "\"Forme Change - Ability Updates or Not.xlsx\" in Other Mechanics.",
            ],
            Body =
            [
                "PUSH {lr}",
                "MOV  r1, #{FORME}",
                "BL   {sym:Set Pokemon Pointer r0 to forme r1}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Ability: act on grounding",
            Summary = "Branches on whether a Pokemon is grounded. Levitate-adjacent effects, and "
                    + "anything that should ignore Ground moves or terrain.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/Levitate.xlsx",
            ModelledOn = new() { Name = "Levitate", Slot = 0x12 },
            TimingHint = "Before damage for an immunity, or wherever terrain eligibility is decided.",
            Parameters = [],
            Verify =
            [
                "Returning 1 usually means immune. Confirm the convention at your timing.",
                "0x0CE660 \"Is Grounded\" is the definite one and is what this calls. The corpus also "
                + "lists 0x082114 as \"returns UnGroundedness?\" - with the question mark, meaning the "
                + "research itself is unsure of its sense. Avoid it unless you verify it yourself.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Is Grounded}",
                "CMP  r0, #0",
                "MOVEQ r0, #1                        @ not grounded",
                "MOVNE r0, #0",
                "POP  {pc}",
            ],
        },

        // ================================================================= moves, second set
        new()
        {
            Name = "Move: multiply power via the engine helper",
            Summary = "Scales power using the game's own multiplier entry point rather than doing the "
                    + "arithmetic inline, so any clamping it applies is preserved.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Crush Grip.xlsx",
            ModelledOn = new() { Name = "Crush Grip", Slot = 0x46 },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify = ["The helper takes the multiplier in r0; confirm the scale it expects (Q12 here)."],
            Body =
            [
                "PUSH {r4, lr}",
                "MOV  r0, #0",
                "{SCALE}",
                "BL   {sym:Multiply Move Power by r0}",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Move: power depends on the weather",
            Summary = "Stronger under one weather. Solar Beam, Thunder and Blizzard read this.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Weather Ball.xlsx",
            ModelledOn = new() { Name = "Weather Ball", Slot = 0x46 },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "WEATHER", Label = "Weather id to look for", Default = "1" },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify = ["Confirm the weather ids - the AI has its own reader at 0x07B834 which may differ."],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ base power",
                "BL   {sym:Returns current weather as r0}",
                "CMP  r0, #{WEATHER}",
                "MOV  r0, r5",
                "BNE  done",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Move: only works on a sleeping target",
            Summary = "Fails unless the target is asleep. Dream Eater and Wake-Up Slap shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Hax.xlsx",
            ModelledOn = new() { Name = "Dream Eater", Slot = 0x38 },
            TimingHint = "The can-this-move-run check, before damage.",
            Parameters =
            [
                new() { Key = "INVERT", Label = "Fail if asleep instead?", Default = "no" },
            ],
            Verify =
            [
                "The routine also returns true for Comatose, which is usually what you want - Dream "
                + "Eater works on a Komala.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Is asleep or Comatose}",
                "CMP  r0, #0",
                "{SLEEPRESULT}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: stronger against grounded targets",
            Summary = "Power changes based on whether the target is on the ground.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Ice Spinner.xlsx",
            ModelledOn = new() { Name = "Low Kick", Slot = 0x46 },
            TimingHint = "Power calculation.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "2" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "1" },
            ],
            Verify = ["Pass the TARGET to the grounding check, not the user."],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ base power",
                "BL   {sym:Is Grounded}",
                "CMP  r0, #0",
                "MOV  r0, r5",
                "BEQ  done                           @ airborne, unchanged",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Move: recoil a share of damage dealt",
            Summary = "The user takes back part of what it dealt. Double-Edge and Head Smash shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Egg Bomb.xlsx",
            ModelledOn = new() { Kind = CustomMechanicKind.Item, Name = "Life Orb", Slot = 0xA0 },
            TimingHint = "After damage, when the amount actually dealt is known.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Recoil numerator", Default = "1" },
                new() { Key = "DEN", Label = "Recoil denominator", Default = "3" },
            ],
            Verify =
            [
                "The move record already has a Recoil field at 0x12 - use that for a plain fraction. "
                + "This is for recoil the table cannot express, such as a conditional share.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "{SCALE}",
                "BL   {sym:Recoil handling}",
                "POP  {r4, pc}",
            ],
        },

        // ================================================================= items, second set
        new()
        {
            Name = "Item: soften a super-effective hit",
            Summary = "Reduces damage from a super-effective move once, then is consumed. "
                    + "Resist-berry shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Eviolite.xlsx",
            ModelledOn = new() { Name = "Chople Berry", Slot = 0x5B },
            TimingHint = "Damage calculation, before the value is applied.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Damage numerator", Default = "1" },
                new() { Key = "DEN", Label = "Damage denominator", Default = "2" },
            ],
            Verify =
            [
                "The berry family shares one reducer at 0x0610F4 that each berry calls, so match its "
                + "expectations rather than replacing it.",
                "Add consumption afterwards or the item will keep working every turn.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:SE Hit Reducing Berries (Called by the function for each Berry)}",
                "CMP  r0, #0",
                "BEQ  done                           @ not a super-effective hit",
                "{SCALE}",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Item: cure a status and be used up",
            Summary = "Heals one status condition then consumes itself. Status-berry shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Nature Mints.xlsx",
            ModelledOn = new() { Name = "Cheri Berry", Slot = 0xAF },
            TimingHint = "End of turn, or immediately after the status is applied.",
            Parameters =
            [
                new() { Key = "STATUS", Label = "Status id to cure", Default = "0" },
            ],
            Verify = ["The routine takes the status in r3 and handles consumption in the same call."],
            Body =
            [
                "PUSH {lr}",
                "MOV  r3, #{STATUS}",
                "BL   {sym:Heal Status in r3 and Consume Berry}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Item: change the holder's forme",
            Summary = "A held item that puts the holder into another forme. Mega stone and "
                    + "Rusted Sword/Shield shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Mega Stone Beast Boost.xlsx",
            ModelledOn = new() { Name = "Red Orb", Slot = 0xCB },
            TimingHint = "Switch-in, or whenever the forme condition is evaluated.",
            Parameters =
            [
                new() { Key = "FORME", Label = "Forme index", Default = "1" },
            ],
            Verify =
            [
                "0x06AF98 reports whether the held item grants an inherent forme change - check that "
                + "first so this does not fight the stock mega/primal path.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:Returns 1 if has inherent forme change via current held item}",
                "CMP  r0, #0",
                "BNE  done                           @ the stock path already handles it",
                "MOV  r1, #{FORME}",
                "BL   {sym:Set Pokemon Pointer r0 to forme r1}",
                "done:",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Item: react to what the target is holding",
            Summary = "Behaviour depends on the opponent's item. Symbiosis-style read, and the basis "
                    + "for anything that punishes or copies a held item.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Knock Off Remove Item.xlsx",
            ModelledOn = new() { Kind = CustomMechanicKind.Item, Name = "Muscle Band", Slot = 0x47 },
            TimingHint = "Power or damage calculation.",
            Parameters =
            [
                new() { Key = "ITEM", Label = "Item id to react to (0 = none held)", Default = "0" },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3" },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2" },
            ],
            Verify = ["Confirm the routine returns the TARGET's item at your timing, not the holder's."],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ value being scaled",
                "BL   {sym:Symbiosis check/return target held item}",
                "CMP  r0, #{ITEM}",
                "MOV  r0, r5",
                "BNE  done",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Item: power up at a cost in HP",
            Summary = "Boosts damage and takes a share of the holder's HP each time. Life Orb shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Loaded Dice.xlsx",
            ModelledOn = new() { Name = "Life Orb", Slot = 0xA0 },
            TimingHint = "Power calculation for the boost; the cost is applied after the hit.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Power numerator", Default = "13" },
                new() { Key = "DEN", Label = "Power denominator", Default = "10" },
            ],
            Verify =
            [
                "Both halves are here, but they belong at different timings: the boost during power "
                + "calculation, the cost after the hit lands. Split them into two definitions if your "
                + "hook only fires at one of those - a boost without the cost is a free Life Orb.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "@ first half: the power boost",
                "{SCALE}",
                "@ second half: the HP cost. Move this to an after-damage hook if the timing differs.",
                "BL   {sym:Life Orb Recoil}",
                "POP  {r4, pc}",
            ],
        },

        // ============================================================== abilities, third set
        new()
        {
            Name = "Ability: trap grounded opponents",
            Summary = "The foe cannot switch out or flee while it is on the ground. Arena Trap shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/071 - Arena Trap.txt",
            ModelledOn = new() { Name = "Arena Trap", Slot = 0x0C },
            TimingHint = "Timing 0x0C, where the engine asks whether switching is allowed.",
            Verify =
            [
                "Arena Trap's own routine already does the grounding test, so calling it is both "
                + "shorter and correct for Flying types, Levitate and Air Balloon at once.",
                "Trapping is checked from the OPPONENT's switch attempt, so the Pokemon being tested "
                + "is not the ability holder - do not swap in the holder's pointer here.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Trap Grounded Foes 0x0C (Arena Trap)}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Ability: change incoming accuracy",
            Summary = "Moves aimed at the holder become more or less likely to connect. "
                    + "Snow Cloak and Sand Veil shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/081 - Snow Cloak.txt",
            ModelledOn = new() { Name = "Snow Cloak", Slot = 0x41 },
            TimingHint = "Accuracy calculation, before the roll.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Accuracy numerator", Default = "4",
                        Help = "4/5 is the stock Snow Cloak and Sand Veil figure: incoming accuracy x0.8." },
                new() { Key = "DEN", Label = "Accuracy denominator", Default = "5",
                        Help = "Below 1 makes the holder harder to hit; above 1 makes it easier." },
            ],
            Verify =
            [
                "The stock evasion abilities are weather-gated. This template is not: it applies "
                + "unconditionally, which is a straight evasion boost. Add a weather check first if "
                + "that is not what you want.",
                "Accuracy here is the incoming move's, so a numerator below the denominator makes "
                + "the holder harder to hit - the opposite of a damage multiplier.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "{SCALE}",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: convert the user's move type",
            Summary = "Rewrites the type of the moves the holder uses. Aerilate, Pixilate and "
                    + "Galvanize shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/184 - Aerilate.txt",
            ModelledOn = new() { Name = "Aerilate", Slot = 0x2D },
            TimingHint = "Before the move's type is read for effectiveness and STAB.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Type id to convert to", Default = "2",
                        Help = "Gen 6+ order: 0 Normal, 1 Fighting, 2 Flying, 3 Poison, 4 Ground, 5 Rock, "
                             + "6 Bug, 7 Ghost, 8 Steel, 9 Fire, 10 Water, 11 Grass, 12 Electric, "
                             + "13 Psychic, 14 Ice, 15 Dragon, 16 Dark, 17 Fairy. 2 is Aerilate's Flying." },
            ],
            Verify =
            [
                "The -ate abilities also apply a power multiplier (x1.2 from Gen 7). The type change "
                + "and the boost are separate steps; this covers the type change only.",
                "Aerilate's routine converts Normal-type moves specifically. If you want a different "
                + "source type, check the move's type yourself first rather than assuming.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "MOV  r4, #{TYPE}",
                "BL   {sym:Aerilate Type Change}",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Ability: cancel damage from one type",
            Summary = "Attacks of a chosen type do nothing at all to the holder.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/026 - Levitate.txt",
            ModelledOn = new() { Name = "Levitate", Slot = 0x20 },
            TimingHint = "Damage calculation, before the value is applied.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Type id to negate", Default = "12",
                        Help = "12 is Electric, giving a Volt Absorb-style wall without the healing. "
                             + "Same type ids as everywhere else: 4 Ground reproduces Levitate." },
            ],
            Verify =
            [
                "The routine takes the type in r2, not r0 or r1.",
                "This cancels the damage but does not print an immunity message or stop the move's "
                + "secondary effects. For a full immunity, see \"Ability: absorb a move type\", "
                + "which goes through the immunity path instead.",
            ],
            Body =
            [
                "PUSH {lr}",
                "MOV  r2, #{TYPE}",
                "BL   {sym:Negate attack of Type in r2}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Ability: draw in a move type and gain from it",
            Summary = "Moves of one type are pulled to the holder, absorbed, and turned into a stat "
                    + "boost. Storm Drain and Lightning Rod shape.",
            Kind = CustomMechanicKind.Ability,
            CorpusReference = "ARM Functions/Ability Edits/114 - Storm Drain.txt",
            ModelledOn = new() { Name = "Storm Drain", Slot = 0x32 },
            TimingHint = "Redirection runs before targeting is fixed; the immunity at timing 0x32.",
            Verify =
            [
                "Redirection and absorption are two different hooks. This is the absorb-and-boost "
                + "half; without the redirect half the ability only works when the move was already "
                + "aimed at the holder.",
                "In singles the redirect half is invisible, so test this in a double battle.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Storm Drain (Immunity/Boost)}",
                "POP  {pc}",
            ],
        },

        // ================================================================== moves, third set
        new()
        {
            Name = "Move: break through Protect",
            Summary = "Ignores Protect, Detect and their relatives, and strips the guard. Feint and "
                    + "Hyperspace Fury shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Feint.xlsx",
            FixedTiming = 0x9F,
            ModelledOn = new() { Name = "Feint" },
            TimingHint = "Timing 0x9F, before the protect check would reject the move.",
            Verify =
            [
                "Breaking the guard and ignoring it are separate: this removes the protection so "
                + "later hits land too. A move that should only ignore it for itself wants the "
                + "\"Ignore Protect\" routines instead.",
                "Max Guard and the Z-move guards are not covered by this path in USUM.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Break Protection (called by Feint, Shadow Force, etc. at timing 0x9F)}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: bind the target for several turns",
            Summary = "Applies a trapping effect that chips the target each turn and stops it "
                    + "fleeing. Wrap and Fire Spin shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Whirlpool.xlsx",
            ModelledOn = new() { Name = "Whirlpool", Slot = 0x7B },
            TimingHint = "Timing 0x7B, after the hit connects.",
            Verify =
            [
                "The setter and the message are two calls at two timings (0x7B and 0x78). Setting "
                + "the effect without the text leaves a trapped target and no explanation on screen.",
                "Duration and chip damage come from the trapping-move table, not from here.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Set Trapping Move (needed for end of turn text) 0x7B}",
                "@ the on-screen message lives at timing 0x78:",
                "@   BL {sym:Print the extra text from setting Trapping move 0x78}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: swap held items with the target",
            Summary = "Trades the user's item for the target's. Trick and Switcheroo shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Trick.xlsx",
            ModelledOn = new() { Name = "Trick", Slot = 0xBF },
            TimingHint = "After the move is confirmed to hit.",
            Verify =
            [
                "The routine returns 0 for failure - it refuses in wild battles outside specific "
                + "trainer indices, which is the stock anti-item-theft rule. Branch on the result "
                + "rather than assuming the swap happened.",
                "Sticky Hold, Mega Stones and Z-Crystals are rejected inside the routine.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Trick/Switcheroo, checks tye of battle, returns 0 (failure) if wild battle and trainer index is not 1 or 3}",
                "CMP  r0, #0",
                "BEQ  failed",
                "@ swap succeeded",
                "failed:",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: put up a screen on your side",
            Summary = "Sets a damage-reducing screen for the user's side. Reflect, Light Screen and "
                    + "Aurora Veil shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Aurora Veil.xlsx",
            ModelledOn = new() { Name = "Aurora Veil", Slot = 0xC0 },
            TimingHint = "Timing 0xC0, on use.",
            Parameters =
            [
                new() { Key = "WHICH", Label = "reflect or veil", Default = "reflect",
                        Help = "Which setter to call. \"veil\" (or \"aurora\") emits the Aurora Veil "
                             + "routine, anything else emits Set Reflect." },
            ],
            Verify =
            [
                "Aurora Veil's setter refuses outside hail in the stock game; Reflect's does not. "
                + "Pick the one whose preconditions you want, because they are inside the routine.",
                "The reduction itself is a separate routine that runs during damage calculation - "
                + "see \"Item: reduce damage behind a screen\" for that half.",
            ],
            Body =
            [
                "PUSH {lr}",
                "{SCREEN}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: fail unless an ally is on the field",
            Summary = "A doubles-only move: it does nothing in singles. Helping Hand and Ally Switch "
                    + "shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Helping Hand.xlsx",
            FixedTiming = 0x25,
            ModelledOn = new() { Name = "Helping Hand" },
            TimingHint = "On use, before any effect is applied.",
            Parameters =
            [
                new() { Key = "MINALLIES", Label = "Allies required", Default = "1",
                        Help = "1 means \"needs a partner\", which is what every doubles-only move wants. "
                             + "Higher values only ever succeed in a Battle Royal." },
            ],
            Verify =
            [
                "The ally counter counts living allies, so a fainted partner does not satisfy this.",
                "\"Move Fails if no ally\" prints the failure message as well as returning; do not "
                + "print your own on top of it.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:return_ally_count_r0}",
                "CMP  r0, #{MINALLIES}",
                "BGE  ok",
                "BL   {sym:Move Fails if no ally}",
                "POP  {r4, pc}",
                "ok:",
                "@ the doubles-only effect goes here",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Move: never miss",
            Summary = "Skips the accuracy roll entirely, ignoring evasion boosts and accuracy drops.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Pursuit.xlsx",
            ModelledOn = new() { Name = "Pursuit", Slot = 0x3F },
            TimingHint = "Timing 0x3F, at the accuracy check.",
            Verify =
            [
                "The move record can already express this: accuracy 0 means \"always hits\" and "
                + "needs no code at all. Use this only for a conditional bypass, where the move "
                + "should sometimes still roll.",
                "Semi-invulnerable turns (Fly, Dig) are handled elsewhere and are not bypassed here.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Pursuit ignore Accuracy/Evasion 0x3F}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Move: change the target's type",
            Summary = "Overwrites what the target is, so later attacks hit it differently. "
                    + "Soak and Reflect Type shape.",
            Kind = CustomMechanicKind.Move,
            CorpusReference = "ARM Functions/Move Edits/Soak.xlsx",
            ModelledOn = new() { Name = "Soak", Slot = 0xBF },
            TimingHint = "Timing 0xBF, after the move connects.",
            Verify =
            [
                "Reflect Type copies the USER's types onto the target. For a fixed type instead, "
                + "set the type bytes directly rather than calling this.",
                "Arceus, Silvally and Terastal-style forme locks resist type changes - "
                + "0x0B6DA4 tests for the first two if you need to refuse politely.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Reflect Type 0xBF}",
                "POP  {pc}",
            ],
        },

        // ================================================================== items, third set
        new()
        {
            Name = "Item: add a flinch chance",
            Summary = "Gives the holder's moves a chance to make the target flinch. King's Rock and "
                    + "Razor Fang shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Kings Rock.xlsx",
            ModelledOn = new() { Name = "King's Rock", Slot = 0x83 },
            TimingHint = "Timing 0x83, after damage.",
            Verify =
            [
                "The stock routine only applies when the move has no secondary chance of its own, "
                + "which is why King's Rock does nothing on moves that already flinch.",
                "Inner Focus and a substitute both block the flinch inside the effect, so this "
                + "does not need to test for them.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:10% Flinch Chance if move chance is 0% (King's Rock, Razor Fang) 0x83}",
                "POP  {pc}",
            ],
        },
        new()
        {
            Name = "Item: reduce damage behind a screen",
            Summary = "Applies the Reflect/Light Screen reduction, so an item can extend or replace "
                    + "screen behaviour.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Light Clay.xlsx",
            ModelledOn = new() { Name = "Light Clay", Slot = 0xBE },
            TimingHint = "Damage calculation.",
            Parameters =
            [
                new() { Key = "NUM", Label = "Extra reduction numerator", Default = "1",
                        Help = "1/1 leaves the engine's own screen reduction alone, which is the safe default." },
                new() { Key = "DEN", Label = "Extra reduction denominator", Default = "1",
                        Help = "Raise this only to stack a further cut on top of the screen." },
            ],
            Verify =
            [
                "This is the reduction half only - it assumes a screen is already up. Calling it "
                + "with no screen set does nothing.",
                "The stock reduction is already applied by the engine. Stacking this on top halves "
                + "damage twice, so leave the multiplier at 1/1 unless that is the intent.",
            ],
            Body =
            [
                "PUSH {r4, lr}",
                "BL   {sym:Reflect/Light Screen Damage Reduction}",
                "{SCALE}",
                "POP  {r4, pc}",
            ],
        },
        new()
        {
            Name = "Item: act on how heavy the holder is",
            Summary = "Reads the holder's weight and scales an effect by it. The basis for a "
                    + "weight-gated berry or a Heavy-Duty-Boots-style item.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Generic Functions/Weight.xlsx",
            ModelledOn = new() { Name = "Float Stone", Slot = 0x93 },
            TimingHint = "Anywhere the holder's own record is available.",
            Parameters =
            [
                new() { Key = "KG", Label = "Weight threshold in kg", Default = "100",
                        Help = "In kilograms. The body converts to decigrams for you, so enter 100 for 100 kg. "
                             + "Keep it at or below 255: it is loaded as an ARM immediate." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "3",
                        Help = "3/2 = 1.5x, applied only when the holder is at or above the threshold." },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "2",
                        Help = "Integer maths only - the fraction becomes a Q12 multiply and a shift." },
            ],
            Verify =
            [
                "Weight comes back in DECIGRAMS: 100 kg is 1000, not 100. The comparison below "
                + "multiplies the parameter by 10 for you.",
                "Autotomize and Heavy Metal change the value the routine returns, which is normally "
                + "what you want - it is the battle weight, not the Pokedex weight.",
            ],
            Body =
            [
                "PUSH {r4, r5, lr}",
                "MOV  r5, r0                         @ value being scaled",
                "BL   {sym:Returns weight in r0 (in decigrams 10 decigrams = 1 kg)}",
                "MOV  r1, #{KG}",
                "ADD  r1, r1, r1, LSL #2             @ r1 = KG * 5",
                "MOV  r1, r1, LSL #1                 @ r1 = KG * 10, now decigrams",
                "CMP  r0, r1",
                "MOV  r0, r5",
                "BLT  done                           @ lighter than the threshold",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, pc}",
            ],
        },
        new()
        {
            Name = "Item: only work for one type",
            Summary = "The item does nothing unless the holder is a given type. Type-plate and "
                    + "type-gem shape.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Type Plates.xlsx",
            ModelledOn = new() { Name = "Soul Dew", Slot = 0x47 },
            TimingHint = "Power or damage calculation.",
            Parameters =
            [
                new() { Key = "TYPE", Label = "Required holder type", Default = "10",
                        Help = "10 is Water. Matches either slot of a dual type, so a Water/Flying "
                             + "holder qualifies." },
                new() { Key = "NUM", Label = "Multiplier numerator", Default = "6",
                        Help = "6/5 = 1.2x, the stock type-plate figure." },
                new() { Key = "DEN", Label = "Multiplier denominator", Default = "5",
                        Help = "Type gems used 3/2 before Gen 6 and 13/10 after." },
            ],
            Verify =
            [
                "The type test takes the Pokemon pointer in r0 and the type in r1, and it matches "
                + "EITHER of a dual type's slots.",
                "Preserve the value being scaled before the call - the test returns in r0 and would "
                + "otherwise destroy it.",
            ],
            Body =
            [
                "PUSH {r4, r5, r6, lr}",
                "MOV  r5, r0                         @ value being scaled",
                "MOV  r6, r1                         @ holder pointer supplied by the caller",
                "MOV  r0, r6",
                "MOV  r1, #{TYPE}",
                "BL   {sym:Boolean Pokemon r0 has Type r1}",
                "CMP  r0, #0",
                "MOV  r0, r5",
                "BEQ  done                           @ wrong type, unchanged",
                "{SCALE}",
                "done:",
                "POP  {r4, r5, r6, pc}",
            ],
        },
        new()
        {
            Name = "Item: switch the holder out after attacking",
            Summary = "The holder leaves the field once its move lands. Eject Button and "
                    + "Eject Pack shape, built on the U-turn path.",
            Kind = CustomMechanicKind.Item,
            CorpusReference = "ARM Functions/Item Edits/Eject Button.xlsx",
            FixedTiming = 0xA2,
            ModelledOn = new() { Name = "Eject Button" },
            TimingHint = "After damage, once the hit is confirmed.",
            Verify =
            [
                "0x0E12B0 answers whether switching out is even allowed - trapping abilities, Mean "
                + "Look and Ingrain all refuse. Skipping that check produces a switch the engine "
                + "then has to undo, which is where soft-locks come from.",
                "This is the self-switch path. An Eject Button that fires when the holder is HIT "
                + "belongs at a damage-taken timing instead.",
            ],
            Body =
            [
                "PUSH {lr}",
                "BL   {sym:Determines if can switch out?}",
                "CMP  r0, #0",
                "BEQ  done                           @ trapped, do nothing",
                "BL   {sym:U-Turn/Volt Switch/Parting Shot}",
                "done:",
                "POP  {pc}",
            ],
        },
    ];
}
