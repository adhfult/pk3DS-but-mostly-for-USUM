#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>How a recipe puts its effect into the ROM.</summary>
public enum RecipeEffectKind
{
    /// <summary>A hand-written Battle.cro patch that already exists in <see cref="ResearchEngine"/>.</summary>
    ItemPatch,

    /// <summary>One of the built-in <see cref="FunctionTemplates"/>, installed as a custom function.</summary>
    Template,

    /// <summary>Nothing is patched: the entry is data only (names, descriptions, table rows).</summary>
    DataOnly,

    /// <summary>
    /// The level cap: <see cref="LevelCapPatch"/> assembles its routine and table and hooks both
    /// the experience path and the Rare Candy path.
    /// </summary>
    LevelCap,

    /// <summary>
    /// The byte writes a research sheet already records, applied straight into its target binary.
    /// </summary>
    CorpusPatch,

    /// <summary>
    /// A finished <see cref="PatchPackage"/>: mechanic slots, code blocks, site hooks and item data,
    /// applied by <see cref="PatchPackageInstaller"/>.
    /// </summary>
    Package,

    /// <summary>
    /// A loose <c>.ips</c> patch against code.bin, of the kind the community distributes.
    /// </summary>
    IpsPatch,

    /// <summary>
    /// A handful of literal byte writes into code.bin, with the expected before-value recorded.
    /// </summary>
    ByteEdit,
}

/// <summary>
/// One literal byte write, with the value it is expected to replace.
/// </summary>
/// <param name="Target">
/// Which binary this byte belongs to. Null means the recipe's own <see cref="Recipe.Target"/>, which
/// is what every single-file recipe wants. Set it when one feature spans two files - a hook in one
/// binary reaching a routine in another is a normal shape, and splitting that across two recipes
/// would let a user install half of it.
/// </param>
public sealed record ByteEdit(uint Offset, byte From, byte To, ResearchTarget? Target = null);

/// <summary>
/// A precondition: <paramref name="Bytes"/> must already be at <paramref name="Offset"/> in
/// <paramref name="Target"/> for the recipe to be applicable to this build.
/// </summary>
/// <param name="Describes">What its presence proves, for the refusal message.</param>
/// <param name="Remedy">
/// What the reader should do about it. Optional; without one the refusal falls back to a generic
/// "built for a different binary" line, which is right for a wrong-build anchor and wrong for an
/// anchor that is really saying "you already have this".
/// </param>
public sealed record RecipeAnchor(ResearchTarget Target, uint Offset, byte[] Bytes, string Describes,
                                  string? Remedy = null)
{
    /// <summary>Whether the binary holds this anchor.</summary>
    public bool Present(byte[] bin) =>
        bin != null && Offset + Bytes.Length <= bin.Length &&
        System.Linq.Enumerable.Range(0, Bytes.Length).All(k => bin[Offset + k] == Bytes[k]);
}

/// <summary>
/// How far a region of code.bin moved between games: rows at or after <paramref name="From"/> shift
/// by <paramref name="Delta"/>.
/// </summary>
public sealed record CodeBinShift(uint From, int Delta);

/// <summary>
/// A complete, one-click addition to a ROM.
/// </summary>
public sealed class Recipe
{
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
    public CustomMechanicKind Kind { get; init; } = CustomMechanicKind.Item;

    /// <summary>Ids this recipe occupies. Most need one; the mints need twenty-one.</summary>
    public int SlotCount => Entries.Count;

    /// <summary>Name and description for each slot, in order.</summary>
    public List<RecipeEntry> Entries { get; init; } = [];

    public RecipeEffectKind EffectKind { get; init; } = RecipeEffectKind.DataOnly;

    /// <summary>For <see cref="RecipeEffectKind.ItemPatch"/>: the patch name ResearchEngine knows.</summary>
    public string? PatchName { get; init; }

    /// <summary>For <see cref="RecipeEffectKind.Template"/>: the template name, and its timing.</summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// Parameter values for <see cref="TemplateName"/>, keyed by the template's parameter keys.
    /// </summary>
    public Dictionary<string, string> TemplateValues { get; init; } = [];

    /// <summary>
    /// Timing byte to install at, when the recipe knows it. Null asks <see cref="TemplateTiming"/>
    /// to resolve one from the template's model.
    /// </summary>
    public byte? Timing { get; init; }

    /// <summary>Mechanic the template attaches to, by name. Null uses the assigned id itself.</summary>
    public string? AttachTo { get; init; }

    /// <summary>For <see cref="RecipeEffectKind.Package"/>: the loaded package itself.</summary>
    public PatchPackage? Package { get; init; }

    /// <summary>Where the package was loaded from, for the detail pane.</summary>
    public string? PackagePath { get; init; }

    /// <summary>For <see cref="RecipeEffectKind.IpsPatch"/>: full path to the .ips file.</summary>
    public string? IpsPath { get; init; }

    /// <summary>For <see cref="RecipeEffectKind.ByteEdit"/>: the writes, already version-selected.</summary>
    public List<ByteEdit> ByteEdits { get; init; } = [];

    /// <summary>
    /// Bytes that must already be present before this recipe is allowed to write.
    /// </summary>
    public List<RecipeAnchor> Anchors { get; init; } = [];

    /// <summary>
    /// Offset to add to a workbook's code.bin rows for a given game, keyed by "US"/"UM".
    /// </summary>
    public Dictionary<string, List<CodeBinShift>> CodeBinDeltaByVersion { get; init; } = [];

    /// <summary>
    /// Game this recipe is valid for — "US", "UM", or null for anything.
    /// </summary>
    public string? ForVersion { get; init; }

    /// <summary>
    /// Which shelf this belongs on, for grouping the list.
    /// </summary>
    public string Category => EffectKind switch
    {
        RecipeEffectKind.Package => "Items & abilities",
        RecipeEffectKind.ItemPatch => "Items & abilities",
        RecipeEffectKind.IpsPatch => "IPS patches",
        RecipeEffectKind.Template => "Templates",
        RecipeEffectKind.DataOnly => "Data only",
        _ => "Battle mechanics",
    };

    /// <summary>For <see cref="RecipeEffectKind.CorpusPatch"/>: the source workbook, e.g. "Nature Mints.xlsx".</summary>
    public string? SheetFile { get; init; }

    /// <summary>Binary the recorded patches are written into.</summary>
    public ResearchTarget Target { get; init; } = ResearchTarget.BattleCro;

    /// <summary>
    /// Every file this recipe writes to, as plain names.
    /// </summary>
    public List<string> TargetFiles { get; init; } = [];

    /// <summary>The files this recipe changes, for display. Never empty.</summary>
    public IReadOnlyList<string> ResolvedTargets =>
        TargetFiles.Count > 0 ? TargetFiles : [FileNameOf(Target)];

    /// <summary>The RomFS/ExeFS file a target names.</summary>
    public static string FileNameOf(ResearchTarget t) => t switch
    {
        ResearchTarget.BagCro => "Bag.cro",
        ResearchTarget.ShopCro => "Shop.cro",
        ResearchTarget.BoxCro => "Box.cro",
        ResearchTarget.StatusCro => "Status.cro",
        ResearchTarget.FieldRoCro => "FieldRo.cro",
        ResearchTarget.EvolutionCro => "Evolution.cro",
        ResearchTarget.CodeBin => "code.bin",
        ResearchTarget.BattleCro => "Battle.cro",
        _ => "(unknown)",
    };

    /// <summary>How many byte writes the sheet holds, for display before installing.</summary>
    public int PatchCount { get; set; }

    /// <summary>
    /// Things this recipe cannot do by itself, stated up front rather than discovered afterwards.
    /// </summary>
    public List<string> Caveats { get; init; } = [];

    public override string ToString() => $"{Name} ({SlotCount} id{(SlotCount == 1 ? "" : "s")})";
}

/// <summary>One id a recipe occupies, with the text that should appear in game.</summary>
public sealed class RecipeEntry
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>Id chosen by the author. -1 until assigned.</summary>
    public int Id { get; set; } = -1;
}

/// <summary>The built-in recipe book.</summary>
public static class Recipes
{
    /// <summary>The twenty-one nature mints, in the order the game's own dispatcher walks them.</summary>
    private static readonly (string Nature, string Raises, string Lowers)[] Mints =
    [
        ("Lonely",  "Attack",    "Defense"),
        ("Adamant", "Attack",    "Sp. Atk"),
        ("Naughty", "Attack",    "Sp. Def"),
        ("Brave",   "Attack",    "Speed"),
        ("Bold",    "Defense",   "Attack"),
        ("Impish",  "Defense",   "Sp. Atk"),
        ("Lax",     "Defense",   "Sp. Def"),
        ("Relaxed", "Defense",   "Speed"),
        ("Modest",  "Sp. Atk",   "Attack"),
        ("Mild",    "Sp. Atk",   "Defense"),
        ("Rash",    "Sp. Atk",   "Sp. Def"),
        ("Quiet",   "Sp. Atk",   "Speed"),
        ("Calm",    "Sp. Def",   "Attack"),
        ("Gentle",  "Sp. Def",   "Defense"),
        ("Careful", "Sp. Def",   "Sp. Atk"),
        ("Sassy",   "Sp. Def",   "Speed"),
        ("Timid",   "Speed",     "Attack"),
        ("Hasty",   "Speed",     "Defense"),
        ("Jolly",   "Speed",     "Sp. Atk"),
        ("Naive",   "Speed",     "Sp. Def"),
        ("Serious", "nothing",   "nothing"),
    ];

    /// <summary>
    /// Whether a loaded package covers <paramref name="feature"/>, by name or in its item names.
    /// </summary>
    private static bool HasPackageFor(string feature)
    {
        try
        {
            var packages = LoadPackages(out _);
            return packages.Any(p =>
                (p.Name ?? "").Contains(feature, StringComparison.OrdinalIgnoreCase) ||
                (p.ItemNames?.Values ?? Enumerable.Empty<string>())
                    .Any(n => (n ?? "").Contains(feature.TrimEnd('s'), StringComparison.OrdinalIgnoreCase)));
        }
        catch { return false; }
    }

    private static Recipe NatureMints() => new()
    {
        Name = "Nature Mints",
        Summary = "The twenty-one mints. Using one rewrites a Pokemon's nature, so its stats grow "
                + "as that nature's would. Needs twenty-one consecutive free item ids.",
        Kind = CustomMechanicKind.Item,
        EffectKind = RecipeEffectKind.CorpusPatch,
        SheetFile = "Nature Mints.xlsx",
        Target = ResearchTarget.BagCro,
        Entries = [.. Mints.Select(m => new RecipeEntry
        {
            Name = $"{m.Nature} Mint",
            Description = m.Serious()
                ? "A mint with a mysterious aroma. When used, it makes a Pokémon's stats grow evenly."
                : $"A mint with a mysterious aroma. When used, it raises {m.Raises} and lowers {m.Lowers}.",
        })],
        Caveats =
        [
            "The mints need twenty-one CONSECUTIVE item ids: the game's dispatcher walks the block "
            + "by offset from the first, so a gap silently shifts every mint after it.",
            "The handler is meant to live in Bag.cro, and Battle.cro has nothing to do with mints. "
            + "But the sheet is not finished: all 36 of its recorded steps carry assembly text and "
            + "no assembled bytes, with unresolved branch targets (bl 0x0), offsets relative to "
            + "three unnamed functions rather than the file, and a few truncated instructions. "
            + "Installing this adds the 21 names and descriptions and writes no code.",
            "code.bin supplies the routine that actually writes the nature (0x2212B0, 'Write Nature "
            + "r1 to loaded pokemon pointer r0'); Bag.cro calls into it, so that binary must be the "
            + "matching build.",
        ],
    };

    private static bool Serious(this (string Nature, string Raises, string Lowers) m) =>
        m.Raises == "nothing";

    /// <summary>
    /// The Gen 8/9 competitive items, each labelled by its own name rather than by patch jargon.
    /// </summary>
    private static readonly (string Item, string Desc)[] CompetitiveItems =
    [
        ("Clear Amulet",     "An item to be held by a Pokémon. This clear, curved charm prevents other Pokémon from lowering the holder's stats."),
        ("Ability Shield",   "An item to be held by a Pokémon. This sturdy shield prevents the holder's Ability from being changed or suppressed."),
        ("Loaded Dice",      "An item to be held by a Pokémon. These tricky dice make multistrike moves hit more times than usual."),
        ("Covert Cloak",     "An item to be held by a Pokémon. This cloak protects the holder from the additional effects of moves."),
        ("Throat Spray",     "An item to be held by a Pokémon. It raises Sp. Atk when the holder uses a sound-based move."),
        ("Utility Umbrella", "An item to be held by a Pokémon. This sturdy umbrella protects the holder from the effects of sun and rain."),
        ("Ability Patch",    "An item that changes a Pokémon with a regular Ability to one with a rare Ability."),
        ("Room Service",     "An item to be held by a Pokémon. It lowers the holder's Speed when Trick Room takes effect."),
        ("Punching Glove",   "An item to be held by a Pokémon. This glove boosts punching moves and stops them making contact."),
        ("Mirror Herb",      "An item to be held by a Pokémon. This herb lets the holder copy an opponent's stat boosts, once."),
        ("Booster Energy",   "An item to be held by a Pokémon. This capsule activates Protosynthesis or Quark Drive once."),
        ("Blunder Policy",   "An item to be held by a Pokémon. It sharply raises Speed when the holder misses with a move."),
    ];

    private static readonly Lazy<List<Recipe>> All = new(Build);

    private static List<Recipe> Build()
    {
        var list = new List<Recipe>();
        if (!HasPackageFor("Nature Mints") && !HasPackageFor("Ability Patch")) list.Add(NatureMints());

        foreach (var (item, desc) in CompetitiveItems)
        {
            if (HasPackageFor(item)) continue;
            var build = MapToTemplate(item);

            list.Add(new Recipe
            {
                Name = item,
                Summary = build == null
                    ? $"Adds {item} as an item with its name and description. No behaviour yet."
                    : $"Adds {item} and installs its behaviour as a custom function.",
                Kind = CustomMechanicKind.Item,
                EffectKind = build == null ? RecipeEffectKind.ItemPatch : RecipeEffectKind.Template,
                PatchName = MapToPatchName(item),
                TemplateName = build?.Template,
                TemplateValues = build?.Values ?? [],
                Timing = build?.Timing,
                Entries = [new RecipeEntry { Name = item, Description = desc }],
                Caveats = build?.Caveats
                    ?? ["No template covers this one yet: the item and its text are added, but it "
                        + "will have no effect until a routine is written for it."],
            });
        }

        return list;
    }

    /// <summary>A template plus the parameter values that make it into one specific item.</summary>
    private sealed record TemplateBuild(string Template, Dictionary<string, string> Values,
                                        byte Timing, List<string> Caveats);

    /// <summary>
    /// The Gen 8/9 items whose behaviour an existing template genuinely produces.
    /// </summary>
    private static TemplateBuild? MapToTemplate(string item) => item switch
    {
        // Clear Body's routine IS Clear Amulet's mechanic: refuse an opponent's stat drop, allow
        // raises. STAT=255 protects every stat, which is what the item does.
        "Clear Amulet" => new(
            "Ability: block stat drops",
            new() { ["STAT"] = "255" },
            0x72,
            ["Blocks drops from any source, including the holder's own moves. The vanilla Clear "
             + "Body routine this copies makes no distinction between self-inflicted and opposing "
             + "drops, so Close Combat's own drop is blocked too."]),

        // Iron Fist's power-scaling shape, applied as an item at 1.1x.
        "Punching Glove" => new(
            "Item: boost moves in a flag category",
            new() { ["BIT"] = "17", ["NUM"] = "11", ["DEN"] = "10" },
            0x47,
            ["The flag bit is F18, which is unused in a stock ROM - it does not already mean "
             + "'punching move'. Tick F18 on the punching moves in the Move editor, or this boosts "
             + "nothing.",
             "Only the power boost is installed. The real item also stops punching moves making "
             + "contact, which needs a separate routine on the contact check."]),

        _ => null,
    };

    /// <summary>
    /// Recipe names <see cref="ResearchEngine.ApplyItemPatch"/> has a working handler for.
    /// </summary>
    private static string? MapToPatchName(string item) => null;

    public static IReadOnlyList<Recipe> Book => All.Value;

    /// <summary>
    /// The recipes shown in the Recipes tab: the Gen 8/9 items, plus the mints.
    /// </summary>
    /// <param name="version">
    /// "US" or "UM". IPS patches are offered only for the matching build; anything else and none
    /// are offered at all, because guessing wrong writes a UM patch into a US binary.
    /// </param>
    public static List<Recipe> Discover(ResearchDatabase? db, string? version = null)
    {
        var list = new List<Recipe>();
        list.AddRange(DiscoverIps(version));
        list.AddRange(ByteEditRecipes(version));
        list.AddRange(OtherMechanics(version));

        // Packages first: where one exists for an item, it is the real implementation and the
        // built-in entry for the same name is only its text.
        var packages = LoadPackages(out string? from);
        foreach (var p in packages.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            list.Add(FromPackage(p, from));

        var claimed = new HashSet<string>(list.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var r in All.Value)
            if (!claimed.Contains(r.Name)) list.Add(r);

        return list;
    }

    /// <summary>Mechanics that are literally a byte, with their per-version offsets.</summary>
    /// <summary>
    /// The Rare Candy in-bag evolution patch, corrected and relocated.
    /// </summary>
    private static Recipe RareCandyInBagEvolution()
    {
        // Where each trampoline lives now. C keeps the workbook's address (already blank); A and B
        // move off the live routine into the same free run, packed after C.
        const uint TrampC = 0x006A70;   // serves the hook at 0x000B5C
        const uint TrampA = 0x006A90;   // serves the hook at 0x000AC8
        const uint TrampB = 0x006AB0;   // serves the hook at 0x000978

        const uint Blank = 0x00000000;

        // The three hooked prologues, and where each trampoline rejoins the function it came from.
        var edits = new List<ByteEdit>();
        edits.AddRange(Words(
            (0x000978, 0xE92D43F8, Branch(0x000978, TrampB)),
            (0x000AC8, 0xE92D40F8, Branch(0x000AC8, TrampA)),
            (0x000B5C, 0xE92D40F8, Branch(0x000B5C, TrampC))));

        // Trampoline C - hook 0x000B5C displaced PUSH / MOV R5,R1 / MOV R4,R0, resumes at 0x000B68.
        edits.AddRange(Words(
            (TrampC + 0x00, Blank, 0xE1D120B8),   // LDRH R2, [R1, #0x8]      species id
            (TrampC + 0x04, Blank, 0xE3003327),   // MOVW R3, #0x327          807
            (TrampC + 0x08, Blank, 0xE1520003),   // CMP  R2, R3
            (TrampC + 0x0C, Blank, 0x812FFF1E),   // BXHI LR                  above 807: skip
            (TrampC + 0x10, Blank, 0xE92D40F8),   // PUSH {R3,R4,R5,R6,R7,LR}
            (TrampC + 0x14, Blank, 0xE1A05001),   // MOV  R5, R1
            (TrampC + 0x18, Blank, 0xE1A04000),   // MOV  R4, R0
            (TrampC + 0x1C, Blank, Branch(TrampC + 0x1C, 0x000B68))));

        // Trampoline A - hook 0x000AC8 displaced the same three, resumes at 0x000AD4.
        edits.AddRange(Words(
            (TrampA + 0x00, Blank, 0xE1D120B8),
            (TrampA + 0x04, Blank, 0xE3003327),
            (TrampA + 0x08, Blank, 0xE1520003),
            (TrampA + 0x0C, Blank, 0x812FFF1E),
            (TrampA + 0x10, Blank, 0xE92D40F8),
            (TrampA + 0x14, Blank, 0xE1A05001),
            (TrampA + 0x18, Blank, 0xE1A04000),
            (TrampA + 0x1C, Blank, Branch(TrampA + 0x1C, 0x000AD4))));

        // Trampoline B - hook 0x000978 displaced four instructions and uses R12 for the constant,
        // because R3 is one of the arguments this function keeps. Resumes at 0x000984, as authored.
        edits.AddRange(Words(
            (TrampB + 0x00, Blank, 0xE1D130B8),   // LDRH R3, [R1, #0x8]
            (TrampB + 0x04, Blank, 0xE300C327),   // MOVW R12, #0x327
            (TrampB + 0x08, Blank, 0xE153000C),   // CMP  R3, R12
            (TrampB + 0x0C, Blank, 0x812FFF1E),   // BXHI LR
            (TrampB + 0x10, Blank, 0xE92D43F8),   // PUSH {R3,R4,R5,R6,R7,R8,R9,LR}
            (TrampB + 0x14, Blank, 0xE1A05001),   // MOV  R5, R1
            (TrampB + 0x18, Blank, 0xE1A06002),   // MOV  R6, R2
            (TrampB + 0x1C, Blank, 0xE1A04000),   // MOV  R4, R0
            (TrampB + 0x20, Blank, Branch(TrampB + 0x20, 0x000984))));

        return new Recipe
        {
            Name = "Rare Candy evolves at Lv100 (Instant In-Bag)",
            Summary = "A Rare Candy used on a maxed Pokemon evolves it from the bag. Gen 8/9 species "
                    + "skip the evolution cutscene, which has no model for them.",
            Kind = CustomMechanicKind.Item,
            EffectKind = RecipeEffectKind.ByteEdit,
            Target = ResearchTarget.EvolutionCro,
            TargetFiles = ["Evolution.cro"],
            Entries = [],
            ByteEdits = edits,
            Anchors =
            [
                new(ResearchTarget.EvolutionCro, 0x001918, [0x70, 0x40, 0x2D, 0xE9],
                    "a stock, unhooked evolution routine",
                    "Your ROM already has this feature. The Expansion Pack installs its own version - "
                    + "it hooks Evolution.cro at 0x001918 to a species check that skips the cutscene "
                    + "above species 807 - so there is nothing to add and applying this would replace "
                    + "a working implementation with a second one."),
            ],
            Caveats =
            [
                "For a STOCK Evolution.cro only. The Expansion Pack already implements this - it "
                + "hooks 0x001918 to its own species>807 cutscene skip - so this refuses there "
                + "rather than overwriting a working implementation with a second one.",
                "Every write is checked against the value it replaces, so it refuses rather than "
                + "half-applying on a build it does not fit.",
                "Corrected from the research workbook, whose hook at 0x000978 pointed at blank space "
                + "and whose trampolines overlapped the routine at 0x006A20.",
                "UNTESTED in game. Its trampolines read the species as [R1+8] - one dereference from "
                + "the second argument - while the game's own equivalent check needs three from the "
                + "first. If it faults on a stock build, that is the thing to look at.",
            ],
        };
    }

    /// <summary>
    /// The Expansion Pack's own Rare Candy / evolution-cutscene-skip feature, recorded as byte edits
    /// so it can be installed on demand instead of only arriving bundled with an Expansion build.
    /// </summary>
    private static Recipe ExpansionRareCandyEvolution()
    {
        const uint Blank = 0x00000000;
        const ResearchTarget Bag = ResearchTarget.BagCro;
        const ResearchTarget Evo = ResearchTarget.EvolutionCro;

        var edits = new List<ByteEdit>();

        // Bag.cro: the hook, then the Rare Candy usability handler it reaches.
        edits.AddRange(In(Bag, Words(
            (0x00C324, 0x0A00016Eu, 0x0A002705u))));                 // B(EQ) -> 0x015F40
        edits.AddRange(In(Bag, Words(
            (0x015F40, Blank, 0xE3550032u),    // CMP  R5, #0x32         item 50, Rare Candy
            (0x015F44, Blank, 0x1AFFDA66u),    // BNE  0x00C8E4          not the candy: original path
            (0x015F48, Blank, 0xE594008Cu),    // LDR  R0, [R4, #0x8C]
            (0x015F4C, Blank, 0xE59D1044u),    // LDR  R1, [SP, #0x44]
            (0x015F50, Blank, 0xEBFFAA18u),    // BL   0x0007B8
            (0x015F54, Blank, 0xE3500000u),    // CMP  R0, #0
            (0x015F58, Blank, 0x0AFFDA61u),    // BEQ  0x00C8E4          refused
            (0x015F5C, Blank, 0xEAFFD926u)))); // B    0x00C3FC          allowed

        // Evolution.cro: the hook, then the cutscene-skip trampoline.
        edits.AddRange(In(Evo, Words(
            (0x001918, 0xE92D4070u, 0xEA001440u))));                 // B 0x006A20
        edits.AddRange(In(Evo, Words(
            (0x006A20, Blank, 0xE92D4070u),    // PUSH {R4,R5,R6,LR}     displaced prologue
            (0x006A24, Blank, 0xE1A04000u),    // MOV  R4, R0
            (0x006A28, Blank, 0xE5940038u),    // LDR  R0, [R4, #0x38]
            (0x006A2C, Blank, 0xE3500000u),    // CMP  R0, #0
            (0x006A30, Blank, 0x1A00000Au),    // BNE  0x006A60          step already resolved
            (0x006A34, Blank, 0xE5940030u),    // LDR  R0, [R4, #0x30]
            (0x006A38, Blank, 0xE590003Cu),    // LDR  R0, [R0, #0x3C]
            (0x006A3C, Blank, 0xE1D000B8u),    // LDRH R0, [R0, #0x8]    species id
            (0x006A40, Blank, 0xE3001327u),    // MOVW R1, #0x327        807
            (0x006A44, Blank, 0xE1500001u),    // CMP  R0, R1
            (0x006A48, Blank, 0x9A000004u),    // BLS  0x006A60          vanilla species: play it
            (0x006A4C, Blank, 0xE1A00004u),    // MOV  R0, R4
            (0x006A50, Blank, 0xEBFFEC71u),    // BL   0x001C1C          finish without the scene
            (0x006A54, Blank, 0xE3A00006u),    // MOV  R0, #6
            (0x006A58, Blank, 0xE5840038u),    // STR  R0, [R4, #0x38]   mark the step done
            (0x006A5C, Blank, 0xE8BD8070u),    // POP  {R4,R5,R6,PC}
            (0x006A60, Blank, 0xE594004Cu),    // LDR  R0, [R4, #0x4C]   displaced third instruction
            (0x006A64, Blank, 0xEAFFEBAEu)))); // B    0x001924          resume

        return new Recipe
        {
            Name = "Rare Candy evolves at Lv100 (Expansion Pack build)",
            Summary = "A Rare Candy used on a maxed Pokemon evolves it from the bag, and species "
                    + "above 807 skip the evolution cutscene they have no model for.",
            Kind = CustomMechanicKind.Item,
            EffectKind = RecipeEffectKind.ByteEdit,
            Target = Bag,
            TargetFiles = ["Bag.cro", "Evolution.cro"],
            Entries = [],
            ByteEdits = edits,
            Anchors =
            [
                new(Bag, 0x0000B4, [0x80, 0x8E, 0x01, 0x00],
                    "an Expansion Pack Bag.cro",
                    "This is the Expansion Pack's own implementation, at the addresses its build "
                    + "uses. Your Bag.cro is a stock one, where those addresses hold different code, "
                    + "so it is refused rather than written into the wrong place. Install the "
                    + "Expansion Pack first."),
            ],
            Caveats =
            [
                "Writes to two files: the hook and item check go into Bag.cro, the cutscene skip "
                + "into Evolution.cro. Both or neither - it refuses before writing if either half "
                + "does not match.",
                "For an Expansion Pack build only. Most Expansion builds ship with this already "
                + "applied, in which case it reports every byte as already in place and changes "
                + "nothing.",
                "The bytes are read back out of a working Expansion build rather than reconstructed, "
                + "and are identical on US and UM.",
            ],
        };
    }

    /// <summary>Assigns a binary to a run of edits, so one recipe can span files.</summary>
    private static IEnumerable<ByteEdit> In(ResearchTarget t, IEnumerable<ByteEdit> edits)
    {
        foreach (var e in edits) yield return e with { Target = t };
    }

    /// <summary>Encodes an ARM <c>B</c> at <paramref name="at"/> reaching <paramref name="target"/>.</summary>
    private static uint Branch(uint at, uint target)
    {
        long off = ((long)target - (at + 8)) / 4;
        return 0xEA000000u | (uint)(off & 0x00FFFFFF);
    }

    /// <summary>Turns whole-word writes into the per-byte edits <see cref="ByteEdit"/> records.</summary>
    private static IEnumerable<ByteEdit> Words(params (uint At, uint From, uint To)[] rows)
    {
        foreach (var (at, from, to) in rows)
        {
            var f = BitConverter.GetBytes(from);
            var t = BitConverter.GetBytes(to);
            for (uint k = 0; k < 4; k++)
                yield return new ByteEdit(at + k, f[k], t[k]);
        }
    }

    private static IEnumerable<Recipe> ByteEditRecipes(string? version)
    {
        string v = (version ?? "").Trim().ToUpperInvariant();
        if (v is not ("US" or "UM")) yield break;

        yield return ExpansionRareCandyEvolution();

        yield return new Recipe
        {
            Name = "Mega Evolution keeps its forme",
            Summary = "Mega-capable Pokemon revert to the forme they entered battle in, not forme 0.",
            Kind = CustomMechanicKind.Ability,
            EffectKind = RecipeEffectKind.ByteEdit,
            Target = ResearchTarget.CodeBin,
            ForVersion = v,
            Entries = [],
            ByteEdits = [new ByteEdit(v == "UM" ? 0x344C83u : 0x344C7Fu, 0x0A, 0xEA)],
            Caveats =
            [
                "One byte in code.bin: the conditional branch that skips the forme-0 revert becomes "
                + "unconditional (BEQ -> B).",
                v == "UM"
                    ? "UM offset 0x344C83. The US build uses 0x344C7F; only the matching one is offered."
                    : "US offset 0x344C7F. The UM build uses 0x344C83; only the matching one is offered.",
            ],
        };

        yield return new Recipe
        {
            Name = "Mega Evolution without the event flag",
            Summary = "Mega Evolution works from the start, instead of waiting for the story flag "
                    + "that normally unlocks it.",
            Kind = CustomMechanicKind.Ability,
            EffectKind = RecipeEffectKind.ByteEdit,
            Target = ResearchTarget.BattleCro,
            TargetFiles = ["Battle.cro"],
            ForVersion = v,
            Entries = [],
            ByteEdits =
            [
                new ByteEdit(0x9CE24, 0x01, 0x00),
                new ByteEdit(0x9CE25, 0x00, 0xF0),
                new ByteEdit(0x9CE26, 0x00, 0x20),
                new ByteEdit(0x9CE27, 0x0A, 0xE3),
            ],
            Caveats =
            [
                "One instruction in Battle.cro at 0x9CE24: the branch that answers \"no\" when the "
                + "Mega Evolution event flag is unset becomes a NOP, so the check falls through to "
                + "the \"yes\" path.",
                "The same offset and the same original bytes (0A000001) on vanilla US, vanilla UM "
                + "and both Expansion builds, so it is offered for every one of them.",
            ],
        };
    }

    /// <summary>
    /// Whole-mechanic edits recorded in the "Other Mechanics" workbooks.
    /// </summary>
    private static IEnumerable<Recipe> OtherMechanics(string? version)
    {
        (string Sheet, string Name, string Summary, string[] Caveats)[] features =
        [
            ("Vs Seeker Implementation.xlsx",
             "Vs Seeker",
             "Adds a working Vs Seeker: a held/bag item that flags rebattleable trainers.",
             ["All 11 recorded originals verified in code.bin on US.",
              "Spans three binaries - Bag.cro (text and the toggle), FieldRo.cro (the check call) "
              + "and code.bin (the battleable test). The workbook records them on one sheet; the "
              + "installer places each row in the binary it fits.",
              "Ported to UM by shifting only the code.bin rows +8; the CROs are byte-identical "
              + "between the two games, so they need no adjustment."]),

            ("Move Effectiveness Text (add 4x weakness and resistance).xlsx",
             "Move Effectiveness Text (4x)",
             "Battle text distinguishes 4x weakness and 4x resistance from the ordinary cases.",
             ["Every one of the 276 recorded original instructions was found in this ROM's Battle.cro, so the sheet matches this build exactly."]),

            ("Move Relearner Replaces Cafe.xlsx",
             "Move Relearner Replaces Cafe",
             "Turns the cafe NPC into a move relearner.",
             ["All 10 recorded originals verified in code.bin.",
              "The workbook labels these edits Battle.cro; they are code.bin offsets, and the installer corrects that from where the originals actually are."]),

            ("Frostbite.xlsx",
             "Frostbite",
             "Replaces the freeze status with Gen 9's frostbite: chip damage and halved Sp. Atk instead of a full lock.",
             ["All 37 recorded originals verified in Battle.cro on both US and UM.",
              "Two of the six sheets record no original bytes, so their 36 writes cannot be checked before being made. They are aimed at Battle.cro with the rest of the workbook - on their own they would have been resolved by size and landed in FieldRo.cro."]),

        ];

        var umShift = new Dictionary<string, List<CodeBinShift>>(StringComparer.OrdinalIgnoreCase)
        {
            // 11/11 anchors, unanimous.
            ["Vs Seeker Implementation.xlsx"] = [new(0, 8)],

            // 10/10 anchors, two regions: the Café NPC hook does not move, the relearner display
            // function and everything after it is 4 bytes later.
            ["Move Relearner Replaces Cafe.xlsx"] = [new(0, 0), new(0x341654, 4)],
        };

        yield return new Recipe
        {
            Name = "Level Cap",
            Summary = "Caps how far Pokemon can level until the story advances. Editable: pick which "
                    + "story flags raise the cap and to what.",
            Kind = CustomMechanicKind.Move,
            EffectKind = RecipeEffectKind.LevelCap,
            Entries = [],
            ForVersion = version,
            TargetFiles = ["Battle.cro", "code.bin"],
            Caveats =
            [
                "Both hook sites are byte-identical across vanilla US, vanilla UM and both Expansion "
                + "builds, and are verified before anything is written.",
                "Keyed on story flags, not the battle counter the original research also supported - "
                + "a counter can be inflated with the Vs Seeker, which would raise the cap without the "
                + "story moving.",
                "Rare Candy is capped too, but only when the ExeFS is open; with RomFS alone the "
                + "experience path is capped and candies are not.",
            ],
        };

        // Which files each of these actually writes, where it is more than the one Target says.
        var spans = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vs Seeker Implementation.xlsx"] = ["Bag.cro", "FieldRo.cro", "code.bin"],
            ["Move Relearner Replaces Cafe.xlsx"] = ["code.bin"],
            ["Move Effectiveness Text (add 4x weakness and resistance).xlsx"] = ["Battle.cro"],
            ["Frostbite.xlsx"] = ["Battle.cro"],
            ["Rare Candy on Level 100 Pokemon triggers evolution.xlsx"] = ["Bag.cro", "Evolution.cro"],
        };

        // Preconditions for workbooks that record no original bytes, so there is otherwise nothing
        // to tell whether their offsets suit the loaded build.
        var anchors = new Dictionary<string, List<RecipeAnchor>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Rare Candy on Level 100 Pokemon triggers evolution.xlsx"] =
            [
                new(ResearchTarget.BagCro, 0x00C324, [0x05, 0x27, 0x00, 0x0A],
                    "the Expansion Pack's Rare Candy usability handler in Bag.cro"),
            ],
        };

        foreach (var (sheet, name, summary, caveats) in features)
        {
            var deltas = new Dictionary<string, List<CodeBinShift>>(StringComparer.OrdinalIgnoreCase)
            {
                ["US"] = [new(0, 0)],
            };
            if (umShift.TryGetValue(sheet, out var d)) deltas["UM"] = d;

            yield return new Recipe
            {
                Name = name,
                Summary = summary,
                Kind = CustomMechanicKind.Move,
                EffectKind = RecipeEffectKind.CorpusPatch,
                SheetFile = sheet,
                Entries = [],
                CodeBinDeltaByVersion = deltas,
                TargetFiles = spans.TryGetValue(sheet, out var files) ? [.. files] : [],
                Anchors = anchors.TryGetValue(sheet, out var anch) ? [.. anch] : [],
                ForVersion = version,
                Caveats = [.. caveats],
            };
        }
    }

    /// <summary>
    /// Loose .ips patches for the loaded build, from the version's own folder only.
    /// </summary>
    private static List<Recipe> DiscoverIps(string? version)
    {
        var list = new List<Recipe>();

        string v = (version ?? "").Trim().ToUpperInvariant();
        if (v is not ("US" or "UM")) return list;

        foreach (string root in IpsFolders())
        {
            string folder = Path.Combine(root, $"{v}_patches");
            if (!Directory.Exists(folder)) continue;

            var files = Directory.EnumerateFiles(folder, "*.ips", SearchOption.AllDirectories)
                                 .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                                 .ToList();
            if (files.Count == 0) continue;

            foreach (string f in files)
            {
                string label = Path.GetFileNameWithoutExtension(f);
                string summary;
                try
                {
                    var recs = IpsPatch.Read(File.ReadAllBytes(f));
                    summary = $"{v} code.bin patch - {IpsPatch.Describe(recs)}";
                }
                catch (Exception ex) { summary = $"{v} code.bin patch - unreadable: {ex.Message}"; }

                list.Add(new Recipe
                {
                    Name = label,
                    Summary = summary,
                    Kind = CustomMechanicKind.Item,
                    EffectKind = RecipeEffectKind.IpsPatch,
                    IpsPath = f,
                    ForVersion = v,
                    Target = ResearchTarget.CodeBin,
                    Entries = [],
                    Caveats =
                    [
                        $"Built for {v}. The other version's copy of this patch uses different "
                        + "offsets, so only the matching set is ever listed.",
                        "Writes straight into code.bin. Select several to apply them together - "
                        + "that is exactly what merging them into one .ips would do, and any "
                        + "address two of them share is reported before anything is written.",
                    ],
                });
            }
            break;   // nearest folder wins
        }
        return list;
    }

    /// <summary>Folders searched for the loose .ips patch sets.</summary>
    public static IEnumerable<string> IpsFolders()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        yield return Path.Combine(baseDir, "other-ips");
        yield return Path.Combine(baseDir, "Resources", "other-ips");

        var up = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent;
        if (up != null) yield return Path.Combine(up.FullName, "other-ips");
    }

    /// <summary>Folders searched for packages, nearest first.</summary>
    public static IEnumerable<string> PackageFolders()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        yield return Path.Combine(baseDir, "patch-packages");
        yield return Path.Combine(baseDir, "Resources", "patch-packages");

        // One level out of bin\Debug\net8.0-windows, for a working copy.
        var up = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent;
        if (up != null) yield return Path.Combine(up.FullName, "patch-packages");
    }

    public static List<PatchPackage> LoadPackages(out string? loadedFrom)
    {
        loadedFrom = null;
        foreach (string folder in PackageFolders())
        {
            if (!Directory.Exists(folder)) continue;
            var problems = new List<string>();
            var found = PatchPackage.LoadFolder(folder, problems);
            if (found.Count == 0) continue;
            loadedFrom = folder;
            return PatchPackage.InDependencyOrder(found, problems);
        }
        return [];
    }

    private static Recipe FromPackage(PatchPackage p, string? from)
    {
        var slots = p.Mechanics?.SelectMany(m => m.Slots ?? []).ToList() ?? [];
        int reused = slots.Count(s => s.Reuse != null);
        int written = slots.Count - reused;

        var bits = new List<string>();
        if (slots.Count > 0) bits.Add($"{slots.Count} slot(s): {reused} reused, {written} new");
        if (p.Blocks?.Count > 0) bits.Add($"{p.Blocks.Count} code block(s)");
        if (p.SitePatches?.Count > 0) bits.Add($"{p.SitePatches.Count} site hook(s)");

        // A package names its own ids through parameters, so the recipe's id boxes describe those
        // rather than a block of consecutive slots.
        var entries = (p.ItemNames ?? [])
            .Select(kv => new RecipeEntry { Name = kv.Value, Description = p.Description })
            .ToList();

        ResearchTarget target = ResearchTarget.BattleCro;
        if (p.OtherCros != null && p.OtherCros.ContainsKey("Bag.cro") && (p.Mechanics == null || p.Mechanics.Count == 0) && (p.Blocks == null || p.Blocks.Count == 0) && (p.SitePatches == null || p.SitePatches.Count == 0))
        {
            target = ResearchTarget.BagCro;
        }
        else if (p.OtherCros != null && p.OtherCros.Count > 0 && (p.Mechanics == null || p.Mechanics.Count == 0))
        {
            string first = p.OtherCros.Keys.First();
            if (first.Contains("Bag", StringComparison.OrdinalIgnoreCase)) target = ResearchTarget.BagCro;
            else if (first.Contains("Field", StringComparison.OrdinalIgnoreCase)) target = ResearchTarget.FieldRoCro;
        }
        else if (p.CodeBin != null && (p.Mechanics == null || p.Mechanics.Count == 0))
        {
            target = ResearchTarget.CodeBin;
        }

        var files = new List<string>();
        if ((p.Mechanics?.Count ?? 0) > 0 || (p.Blocks?.Count ?? 0) > 0 || (p.SitePatches?.Count ?? 0) > 0)
            files.Add("Battle.cro");
        if (p.OtherCros != null)
            files.AddRange(p.OtherCros.Keys);
        if (p.CodeBin != null && ((p.CodeBin.Patches?.Count ?? 0) > 0 || (p.CodeBin.Blocks?.Count ?? 0) > 0))
            files.Add("code.bin");

        return new Recipe
        {
            Name = p.Name,
            Summary = p.Description + (bits.Count > 0 ? "  (" + string.Join("; ", bits) + ")" : ""),
            Kind = CustomMechanicKind.Item,
            EffectKind = RecipeEffectKind.Package,
            Package = p,
            PackagePath = from,
            Entries = entries,
            Target = target,
            TargetFiles = [.. files.Distinct(StringComparer.OrdinalIgnoreCase)],
        };
    }

    public static Recipe? ByName(string name) =>
        All.Value.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Recipes whose effect is actually implemented, as opposed to text-only.</summary>
    public static IEnumerable<Recipe> Working =>
        All.Value.Where(r => r.EffectKind != RecipeEffectKind.ItemPatch || r.PatchName != null);
}
