using System.Collections.Generic;
using System.Linq;

using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Works out what type, if any, a trainer's whole team should share.
/// </summary>
public static class TrainerThemes
{
    /// <summary>Modes, matching <see cref="UniversalSettings.TrainerPokemonMode"/>.</summary>
    public const int Unchanged = 0;
    public const int RandomMode = 1;
    public const int RandomEvenDistribution = 2;
    public const int TypeThemed = 3;
    public const int TypeThemedBossesOnly = 4;
    public const int KeepThemed = 5;
    public const int KeepThemeOrPrimary = 6;

    /// <summary>Labels for the Trainer Pokemon dropdown, in value order.</summary>
    public static readonly string[] Labels =
    [
        "Unchanged",
        "Random",
        "Random (even distribution)",
        "Type Themed",
        "Type Themed (Bosses Only)",
        "Keep Type Themed Trainers' Themes",
        "Keep Themes Or Primary",
    ];

    /// <summary>Whether the mode re-picks species at all.</summary>
    public static bool Randomizes(int mode) => mode > Unchanged;

    /// <summary>Whether the mode involves type themes in any form.</summary>
    public static bool UsesThemes(int mode) => mode >= TypeThemed;

    /// <summary>
    /// A trainer's own theme in the vanilla data, or -1 when their team has no shared type.
    /// </summary>
    public static int VanillaTheme(GameConfig config, IReadOnlyList<int> speciesBefore)
    {
        if (config == null || speciesBefore == null || speciesBefore.Count < 2) return -1;

        HashSet<int> shared = null;
        foreach (int s in speciesBefore)
        {
            var types = TypeRestrictions.TypesOf(config, s).ToHashSet();
            if (types.Count == 0) return -1;
            if (shared == null) shared = types;
            else shared.IntersectWith(types);
            if (shared.Count == 0) return -1;
        }

        // A team sharing two types (an all-Water/Flying team) is themed on either; take the lower
        // id so the choice is stable rather than dependent on set ordering.
        return shared is { Count: > 0 } ? shared.Min() : -1;
    }

    /// <summary>The primary type of the team's first Pokemon, or -1 if it cannot be read.</summary>
    public static int PrimaryTypeOf(GameConfig config, int species)
    {
        var types = TypeRestrictions.TypesOf(config, species).ToList();
        return types.Count > 0 ? types[0] : -1;
    }

    /// <summary>
    /// The type this trainer's new team should share, or -1 for no theme.
    /// </summary>
    /// <param name="speciesBefore">The team's species as the ROM shipped them, before any mapping.</param>
    public static int ThemeFor(GameConfig config, int mode, TrainerData7 trainer, int index,
                               IReadOnlyList<int> speciesBefore)
    {
        switch (mode)
        {
            case TypeThemed:
                return Util.Rand.Next(TypeEffectivenessTable.TypeCount);

            case TypeThemedBossesOnly:
                return TrainerTiers.IsBoss(trainer)
                    ? Util.Rand.Next(TypeEffectivenessTable.TypeCount)
                    : -1;

            case KeepThemed:
                return VanillaTheme(config, speciesBefore);

            case KeepThemeOrPrimary:
            {
                int theme = VanillaTheme(config, speciesBefore);
                if (theme >= 0) return theme;
                // No shared type: fall back to the lead's primary, so every trainer ends up themed
                // on something rather than only the handful that were already coherent.
                return speciesBefore.Count > 0 ? PrimaryTypeOf(config, speciesBefore[0]) : -1;
            }

            default:
                return -1;
        }
    }
}
