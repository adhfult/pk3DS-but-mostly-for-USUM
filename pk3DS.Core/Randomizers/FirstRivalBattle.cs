using System;
using System.Linq;

using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// The tutorial battle against Hau, which has to stay a single level 5 Pokemon.
/// </summary>
public static class FirstRivalBattle
{
    /// <summary>Trainer ids of the tutorial battle, one per player starter choice.</summary>
    public static readonly int[] TrainerIds = [491, 492, 493];

    /// <summary>Level the tutorial opponent is fixed at.</summary>
    public const byte Level = 5;

    /// <summary>Whether this index is one of the tutorial battle's entries.</summary>
    public static bool Is(int index) => Array.IndexOf(TrainerIds, index) >= 0;

    /// <summary>
    /// Whether the trainer at this index still looks like the tutorial battle.
    /// </summary>
    public static bool Matches(int index, TrainerData7 trainer) =>
        Is(index) && trainer?.Pokemon is { Count: > 0 } &&
        trainer.Pokemon.Count <= 2 && trainer.Pokemon[0].Level <= 10;

    /// <summary>
    /// Cuts the team back to one Pokemon at <see cref="Level"/>.
    /// </summary>
    public static void Enforce(TrainerData7 trainer)
    {
        if (trainer?.Pokemon is not { Count: > 0 }) return;

        if (trainer.Pokemon.Count > 1)
            trainer.Pokemon.RemoveRange(1, trainer.Pokemon.Count - 1);

        trainer.Pokemon[0].Level = Level;
        trainer.NumPokemon = trainer.Pokemon.Count;

        // Singles regardless of the Battle Style setting: a doubles tutorial battle would ask the
        // player for a second Pokemon they do not have yet.
        trainer.Mode = BattleMode.Singles;
    }
}
