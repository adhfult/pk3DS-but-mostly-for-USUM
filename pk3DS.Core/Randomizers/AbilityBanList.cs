#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Which abilities the randomizers may not hand out, resolved once from the settings.
/// </summary>
public sealed class AbilityBanList
{
    /// <summary>Abilities that stop the opponent switching out.</summary>
    public static readonly string[] TrappingAbilities =
        ["Shadow Tag", "Arena Trap", "Magnet Pull"];

    /// <summary>
    /// Abilities that are an active liability, rather than merely weak.
    /// </summary>
    public static readonly string[] NegativeAbilities =
    [
        "Truant", "Slow Start", "Defeatist", "Stall", "Normalize",
        "Klutz", "Slow Start", "Comatose",
    ];

    /// <summary>Abilities that mostly do nothing, without actively hurting.</summary>
    public static readonly string[] BadAbilities =
    [
        "Honey Gather", "Illuminate", "Run Away", "Stench", "Pickup",
        "Ball Fetch", "Friend Guard", "Healer", "Telepathy", "Rivalry",
        "Anticipation", "Forewarn", "Frisk", "Keen Eye", "Minus", "Plus",
    ];

    private readonly HashSet<int> _banned = [];

    /// <summary>Ability ids that may not be handed out. Empty when nothing is banned.</summary>
    public IReadOnlySet<int> BannedIds => _banned;

    public int Count => _banned.Count;

    /// <summary>Whether this ability is off limits.</summary>
    public bool IsBanned(int abilityId) => _banned.Contains(abilityId);

    /// <summary>Whether this ability is off limits, by name.</summary>
    public bool IsBanned(string? name) =>
        !string.IsNullOrEmpty(name) && _names.Contains(name);

    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the ban list for a loaded ROM.
    /// </summary>
    /// <param name="abilityNames">The ROM's ability text, indexed by id.</param>
    /// <param name="settings">The toggles the user set.</param>
    public AbilityBanList(string[]? abilityNames, UniversalSettings? settings)
    {
        if (abilityNames == null || abilityNames.Length == 0 || settings == null) return;

        void Ban(IEnumerable<string> names)
        {
            foreach (string n in names)
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                _names.Add(n);
                int id = Array.FindIndex(abilityNames, a => string.Equals(a, n, StringComparison.OrdinalIgnoreCase));
                if (id > 0) _banned.Add(id);
            }
        }

        if (!settings.AllowWonderGuard) Ban(["Wonder Guard"]);
        if (settings.BanTrappingAbilities) Ban(TrappingAbilities);
        if (settings.BanNegativeAbilities) Ban(NegativeAbilities);
        if (settings.BanBadAbilities) Ban(BadAbilities);
        if (settings.BannedAbilityNames is { Count: > 0 }) Ban(settings.BannedAbilityNames);
    }

    /// <summary>
    /// The chooser's candidates with banned ones removed - unless that would leave nothing.
    /// </summary>
    public List<T> Filter<T>(List<T> candidates, Func<T, int> abilityIdOf)
    {
        if (_banned.Count == 0 || candidates.Count == 0) return candidates;
        var kept = candidates.Where(c => !_banned.Contains(abilityIdOf(c))).ToList();
        return kept.Count > 0 ? kept : candidates;
    }
}
