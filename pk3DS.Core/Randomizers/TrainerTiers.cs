using System;

using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Splits USUM's trainers into the boss / important / regular tiers the randomizer options talk about.
/// </summary>
public static class TrainerTiers
{
    /// <summary>
    /// Trainer classes that make their battles bosses, from the USUM class table.
    /// </summary>
    public static readonly int[] BossClasses_USUM =
    [
        031, 049, 050, 051, 141, 164,   // Island Kahuna
        080, 107, 109, 110, 191,        // Elite Four
        076,                            // Team Skull Boss
        071, 082, 220,                  // Aether President
    ];

    /// <summary>The tier a trainer belongs to. Every trainer is in exactly one.</summary>
    public enum Tier
    {
        Regular,
        Important,
        Boss,
    }

    /// <summary>Whether this trainer's class is one of the boss classes.</summary>
    public static bool IsBoss(TrainerData7 trainer) =>
        trainer != null && Array.IndexOf(BossClasses_USUM, trainer.TrainerClass) >= 0;

    /// <summary>Whether this trainer index is on pk3DS's important-trainer list.</summary>
    public static bool IsImportant(int index) =>
        Array.IndexOf(pk3DS.Core.Legal.ImportantTrainers_USUM, index) >= 0;

    /// <summary>
    /// Which tier a trainer falls into.
    /// </summary>
    public static Tier Of(TrainerData7 trainer, int index)
    {
        if (IsBoss(trainer)) return Tier.Boss;
        if (IsImportant(index)) return Tier.Important;
        return Tier.Regular;
    }

    /// <summary>Picks the boss/important/regular value that applies to this trainer.</summary>
    public static T Pick<T>(TrainerData7 trainer, int index, T boss, T important, T regular) =>
        Of(trainer, index) switch
        {
            Tier.Boss => boss,
            Tier.Important => important,
            _ => regular,
        };
}
