// Rules use null to mean "this condition does not apply", which is the distinction the whole table
// turns on, so it is worth having the compiler enforce it.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.WinForms;

/// <summary>
/// Which shop item turns which mascot into which form.
/// <para>
/// This replaces a hand-written chain of "if species == X and the player owns Y then form = N".
/// That chain could express only the simplest case, and three of the transforms asked for are not
/// that shape: Zygardite is inert until the Zygarde Cube has already been bought, the Reins of
/// Unity asks which rider the player wants, and two species change on accumulated friendship with
/// no item involved at all. Encoding the conditions as data keeps all of them in one readable
/// table instead of spreading special cases through the update path.
/// </para>
/// <para>
/// Rules are evaluated in order and the last match wins, so a chained transform lists its stages
/// in ascending order: Zygarde reaches Complete via the Cube, and only then can Zygardite take it
/// to Mega.
/// </para>
/// </summary>
public static class MascotTransforms
{
    /// <summary>Friendship at which the two friendship-gated forms unlock.</summary>
    public const int FriendshipFormThreshold = 200;

    /// <summary>A transform from one mascot form to another.</summary>
    public sealed class Rule
    {
        public int Species { get; init; }

        /// <summary>Item that triggers it, or null when the trigger is friendship alone.</summary>
        public string? Item { get; init; }

        /// <summary>Item that MUST already be owned for <see cref="Item"/> to do anything.</summary>
        public string? Requires { get; init; }

        /// <summary>Minimum friendship, or 0 when friendship is irrelevant.</summary>
        public int MinFriendship { get; init; }

        /// <summary>Form the mascot becomes.</summary>
        public int Form { get; init; }

        /// <summary>Only applies when the mascot is already in this form; -1 means any form.</summary>
        public int FromForm { get; init; } = -1;

        /// <summary>Human-readable, used for the "what did this do" line in the shop.</summary>
        public string Describe => Item == null
            ? $"at {MinFriendship} friendship -> form {Form}"
            : $"{Item}{(Requires == null ? "" : $" (needs {Requires})")} -> form {Form}";
    }

    // Species numbers are National Dex. Form indices follow the games' own ordering.
    private const int Groudon = 383, Kyogre = 382, Rayquaza = 384, Deoxys = 386;
    private const int Latias = 380, Latios = 381, Hoopa = 720, Diancie = 719;
    private const int Sceptile = 254, Blaziken = 257, Swampert = 260;
    private const int Metagross = 376, Salamence = 373, Zygarde = 718;
    private const int Necrozma = 800, Magearna = 801, Zeraora = 807;
    private const int Zacian = 888, Zamazenta = 889, Eternatus = 890;
    private const int Calyrex = 898, Zarude = 893, Baxcalibur = 998, Terapagos = 1024;
    private const int Ogerpon = 1017;

    private static readonly Rule[] Rules =
    [
        // --- kept from the original chain ---
        new() { Species = Groudon,  Item = "Red Orb",       Form = 1 },
        new() { Species = Kyogre,   Item = "Blue Orb",      Form = 1 },
        new() { Species = Rayquaza, Item = "Meteorite",     Form = 1 },
        new() { Species = Latias,   Item = "Latiasite",     Form = 1 },
        new() { Species = Latios,   Item = "Latiosite",     Form = 1 },
        new() { Species = Hoopa,    Item = "Prison Bottle", Form = 1 },
        new() { Species = Diancie,  Item = "Diancite",      Form = 1 },

        // --- new mega stones ---
        new() { Species = Sceptile,   Item = "Sceptilite",   Form = 1 },
        new() { Species = Blaziken,   Item = "Blazikenite",  Form = 1 },
        new() { Species = Swampert,   Item = "Swampertite",  Form = 1 },
        new() { Species = Metagross,  Item = "Metagrossite", Form = 1 },
        new() { Species = Salamence,  Item = "Salamencite",  Form = 1 },
        new() { Species = Magearna,   Item = "Magearnite",   Form = 2 },
        new() { Species = Zeraora,    Item = "Zeraorite",    Form = 1 },
        new() { Species = Baxcalibur, Item = "Baxcalibrite", Form = 1 },

        // --- chained: the Cube first, and only then the stone ---
        new() { Species = Zygarde, Item = "Zygarde Cube", Form = 4 },
        new() { Species = Zygarde, Item = "Zygardite", Requires = "Zygarde Cube", Form = 5 },

        // --- Necrozma: fusion picks the form, then the Z-crystal overrides both ---
        new() { Species = Necrozma, Item = "N-Solarizer", Form = 1 }, // Dusk Mane
        new() { Species = Necrozma, Item = "N-Lunarizer", Form = 2 }, // Dawn Wings
        new() { Species = Necrozma, Item = "Ultranecrozium Z", Requires = "N-Solarizer", Form = 3 },
        new() { Species = Necrozma, Item = "Ultranecrozium Z", Requires = "N-Lunarizer", Form = 3 },

        // --- crowned / eternamax ---
        new() { Species = Zacian,    Item = "Rusted Sword",  Form = 1 },
        new() { Species = Zamazenta, Item = "Rusted Shield", Form = 1 },
        new() { Species = Eternatus, Item = "Dynamax Candy", Form = 1 },

        // --- Ogerpon's masks. Each is independent, so the last one bought is the one worn; buying
        //     a second does not undo the first, it replaces it. ---
        new() { Species = Ogerpon, Item = "Wellspring Mask",  Form = 1 },
        new() { Species = Ogerpon, Item = "Hearthflame Mask", Form = 2 },
        new() { Species = Ogerpon, Item = "Cornerstone Mask", Form = 3 },

        // --- friendship-gated, no item ---
        new() { Species = Zarude,    MinFriendship = FriendshipFormThreshold, Form = 1 },
        new() { Species = Terapagos, Item = "Stellar Tera Shard", Form = 1 },
        new() { Species = Terapagos, Item = "Stellar Tera Shard", MinFriendship = FriendshipFormThreshold, Form = 2 },
    ];

    /// <summary>Item that asks the player which form they want rather than picking one.</summary>
    public const string ChoiceItem = "Reins of Unity";

    /// <summary>The two outcomes <see cref="ChoiceItem"/> offers, in prompt order.</summary>
    public static readonly (string Label, int Form)[] ReinsChoices =
        [("Ice Rider", 1), ("Shadow Rider", 2)];

    /// <summary>Species the choice item applies to.</summary>
    public const int ChoiceSpecies = Calyrex;

    /// <summary>
    /// Resolves the form a mascot should be in. <paramref name="chosenForm"/> carries a choice the
    /// player already made for an item that prompts, and wins over the table when set.
    /// </summary>
    public static int Resolve(int species, int baseForm, IReadOnlyCollection<string> owned, int friendship, int? chosenForm = null)
    {
        if (species == ChoiceSpecies && baseForm == 0 && chosenForm.HasValue && owned.Contains(ChoiceItem))
            return chosenForm.Value;

        int form = baseForm;
        foreach (var rule in Rules)
        {
            if (rule.Species != species) continue;
            if (rule.FromForm >= 0 && form != rule.FromForm) continue;
            if (rule.Item != null && !owned.Contains(rule.Item)) continue;
            if (rule.Requires != null && !owned.Contains(rule.Requires)) continue;
            if (friendship < rule.MinFriendship) continue;
            form = rule.Form;
        }
        return form;
    }

    /// <summary>
    /// Theme name to the species and form it depicts, for every entry the theme menu offers.
    /// <para>
    /// This replaces a switch that only listed Gen 6 and Gen 7. Any theme it did not name fell to
    /// the default of species 800, so every mascot added afterwards silently rendered as Necrozma -
    /// including ones whose own sprite was present and loadable. A missing entry here is the single
    /// point of failure for a mascot not appearing, so the table is kept in step with the menu by
    /// the name check in <see cref="MissingFrom"/>.
    /// </para>
    /// </summary>
    public static readonly Dictionary<string, (int Species, int Form)> ThemeSpecies = new(StringComparer.OrdinalIgnoreCase)
    {
        // Gen 6
        ["Xerneas"] = (716, 0), ["Yveltal"] = (717, 0), ["Zygarde"] = (718, 1),
        ["Groudon"] = (383, 0), ["Kyogre"] = (382, 0), ["Rayquaza"] = (384, 0),
        ["Deoxys"] = (386, 0), ["Jirachi"] = (385, 0), ["Latias"] = (380, 0),
        ["Latios"] = (381, 0), ["Hoopa"] = (720, 0), ["Diancie"] = (719, 0),
        ["Sceptile"] = (254, 0), ["Blaziken"] = (257, 0), ["Swampert"] = (260, 0),
        ["Metagross"] = (376, 0), ["Salamence"] = (373, 0),
        ["Regice"] = (378, 0), ["Regirock"] = (377, 0), ["Registeel"] = (379, 0),

        // Gen 7
        ["Solgaleo"] = (791, 0), ["Lunala"] = (792, 0), ["Necrozma"] = (800, 0),
        ["Dusk Mane Necrozma"] = (800, 1), ["Dawn Wings Necrozma"] = (800, 2),
        ["Ultra Necrozma"] = (800, 3), ["Magearna"] = (801, 0), ["Zeraora"] = (807, 0),
        ["Marshadow"] = (802, 0), ["Incineroar"] = (727, 0), ["Primarina"] = (730, 0),
        ["Decidueye"] = (724, 0),

        // Gen 8
        ["Cinderace"] = (815, 0), ["Rillaboom"] = (812, 0), ["Inteleon"] = (818, 0),
        ["Zacian"] = (888, 0), ["Zamazenta"] = (889, 0), ["Dragapult"] = (887, 0),
        ["Eternatus"] = (890, 0), ["Calyrex"] = (898, 0),
        ["Calyrex Ice Rider"] = (898, 1), ["Calyrex Shadow Rider"] = (898, 2),
        ["Glastrier"] = (896, 0), ["Spectrier"] = (897, 0),
        ["Regieleki"] = (894, 0), ["Regidrago"] = (895, 0), ["Zarude"] = (893, 0),
        ["Urshifu"] = (892, 0), ["Urshifu Rapid Strike"] = (892, 1),

        // Gen 9
        ["Skeledirge"] = (911, 0), ["Meowscarada"] = (908, 0), ["Quaquaval"] = (914, 0),
        ["Baxcalibur"] = (998, 0), ["Tinkaton"] = (959, 0), ["Bellibolt"] = (939, 0),
        ["Miraidon"] = (1008, 0), ["Koraidon"] = (1007, 0), ["Roaring Moon"] = (1005, 0),
        ["Iron Valiant"] = (1006, 0), ["Chi-Yu"] = (1004, 0), ["Ting-Lu"] = (1003, 0),
        ["Chien-Pao"] = (1002, 0), ["Wo-Chien"] = (1001, 0), ["Hydrapple"] = (1019, 0),
        ["Archaludon"] = (1018, 0), ["Pecharunt"] = (1025, 0),
        ["Okidogi"] = (1014, 0), ["Munkidori"] = (1015, 0), ["Fezandipiti"] = (1016, 0),
        ["Terapagos"] = (1024, 0),

        // Ogerpon wears one mask per form.
        ["Ogerpon"] = (1017, 0),
        ["Ogerpon Wellspring"] = (1017, 1),
        ["Ogerpon Hearthflame"] = (1017, 2),
        ["Ogerpon Cornerstone"] = (1017, 3),
    };

    /// <summary>Theme names with no species mapping - each would render as the wrong Pokemon.</summary>
    public static IEnumerable<string> MissingFrom(IEnumerable<string> menuNames) =>
        menuNames.Where(n => !ThemeSpecies.ContainsKey(n));

    /// <summary>Every item the table can consume, for stocking the shop.</summary>
    public static IEnumerable<string> AllItems() =>
        Rules.Select(r => r.Item).Where(i => i != null).Concat([ChoiceItem]).Distinct()!;

    /// <summary>What a species can still become, for showing the player why an item matters.</summary>
    public static IEnumerable<string> DescribeFor(int species) =>
        Rules.Where(r => r.Species == species).Select(r => r.Describe);
}
