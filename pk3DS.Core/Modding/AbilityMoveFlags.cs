#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace pk3DS.Core.Modding;

/// <summary>
/// Ties one of the unused move flag bits to an ability, turning it into a move category.
/// </summary>
/// <summary>What reads a move category: an ability on the attacker, or its held item.</summary>
public enum FlagTrigger
{
    /// <summary>Only a label; nothing acts on it.</summary>
    None = 0,

    /// <summary>An ability, as Iron Fist reads the Punch bit.</summary>
    Ability = 1,

    /// <summary>A held item, as Punching Glove reads the same bit in later games.</summary>
    Item = 2,
}

public sealed class AbilityFlagBinding
{
    /// <summary>Bit index within the move's flag word (17-31 are the unused ones).</summary>
    public int Bit { get; set; }

    /// <summary>Whether an ability, an item, or nothing acts on this category.</summary>
    public FlagTrigger Trigger { get; set; } = FlagTrigger.None;

    /// <summary>
    /// Index of the ability or item that reads this flag, or -1 when the flag is only named.
    /// Which table it indexes is decided by <see cref="Trigger"/>.
    /// </summary>
    public int TriggerId { get; set; } = -1;

    /// <summary>Name captured at bind time, so the stored file stays readable.</summary>
    public string TriggerName { get; set; } = "";

    /// <summary>Power multiplier applied to moves carrying this flag.</summary>
    public double Multiplier { get; set; } = 1.3;

    /// <summary>What the flag is called in the editor's list.</summary>
    public string Label { get; set; } = "";

    /// <summary>True when something actually acts on this flag rather than it just carrying a name.</summary>
    public bool IsBound => Trigger != FlagTrigger.None && TriggerId >= 0;

    /// <summary>Legacy field; assigning it marks the binding as an ability trigger.</summary>
    public int AbilityId
    {
        get => Trigger == FlagTrigger.Ability ? TriggerId : -1;
        set
        {
            if (value < 0) return;
            TriggerId = value;
            if (Trigger == FlagTrigger.None) Trigger = FlagTrigger.Ability;
        }
    }

    /// <summary>Legacy field, kept so old stores round-trip.</summary>
    public string AbilityName
    {
        get => Trigger == FlagTrigger.Ability ? TriggerName : "";
        set { if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(TriggerName)) TriggerName = value; }
    }
}

/// <summary>Loads, saves and queries the flag/ability bindings for the open ROM.</summary>
public static class AbilityMoveFlags
{
    /// <summary>First flag bit with no assigned meaning in the retail games.</summary>
    public const int FirstFreeBit = 17;

    /// <summary>Last bit in the 32-bit flag word.</summary>
    public const int LastBit = 31;

    private static readonly Dictionary<int, AbilityFlagBinding> Bindings = [];
    private static string _path = "";
    private static bool _loaded;

    /// <summary>Where the bindings are stored. Set per ROM so two projects do not share one file.</summary>
    public static void SetStorePath(string path)
    {
        if (_path == path) return;
        _path = path;
        _loaded = false;
        Bindings.Clear();
    }

    public static IReadOnlyCollection<AbilityFlagBinding> All
    {
        get { EnsureLoaded(); return Bindings.Values; }
    }

    /// <summary>The binding for a bit, or null when that bit means nothing yet.</summary>
    public static AbilityFlagBinding? Get(int bit)
    {
        EnsureLoaded();
        return Bindings.TryGetValue(bit, out var b) ? b : null;
    }

    /// <summary>True when the bit is one of the free ones this can be applied to.</summary>
    public static bool IsFreeBit(int bit) => bit is >= FirstFreeBit and <= LastBit;

    /// <summary>
    /// Binds a flag to an ability or a held item. Pass <see cref="FlagTrigger.None"/> to leave it
    /// as a name with nothing acting on it.
    /// </summary>
    public static void Set(int bit, FlagTrigger trigger, int triggerId, string triggerName, double multiplier, string label)
    {
        if (!IsFreeBit(bit))
            throw new ArgumentOutOfRangeException(nameof(bit),
                $"Only bits {FirstFreeBit}-{LastBit} are unused and safe to reassign.");

        EnsureLoaded();
        Bindings[bit] = new AbilityFlagBinding
        {
            Bit = bit,
            Trigger = triggerId >= 0 ? trigger : FlagTrigger.None,
            TriggerId = triggerId,
            TriggerName = triggerName ?? "",
            Multiplier = multiplier,
            Label = string.IsNullOrWhiteSpace(label)
                ? (triggerId >= 0 ? triggerName ?? "" : $"F{bit + 1}")
                : label,
        };
        Save();
    }

    /// <summary>Removes any meaning from a bit.</summary>
    public static void Clear(int bit)
    {
        EnsureLoaded();
        if (Bindings.Remove(bit)) Save();
    }

    /// <summary>
    /// Display text for a flag: the ability and its multiplier when bound, else the plain name.
    /// </summary>
    public static string Describe(int bit)
    {
        var b = Get(bit);
        if (b == null) return $"F{bit + 1}";
        if (!b.IsBound) return string.IsNullOrWhiteSpace(b.Label) ? $"F{bit + 1}" : b.Label;
        string kind = b.Trigger == FlagTrigger.Item ? "item" : "ability";
        return $"{b.Label} ({kind}: {b.TriggerName} x{b.Multiplier:0.##})";
    }

    /// <summary>Bit a given ability or item is bound to, or -1 if it has none.</summary>
    public static int BitFor(FlagTrigger trigger, int triggerId)
    {
        EnsureLoaded();
        foreach (var b in Bindings.Values)
        {
            if (b.Trigger == trigger && b.TriggerId == triggerId) return b.Bit;
        }
        return -1;
    }

    /// <summary>
    /// Power multiplier for a move given the attacker's ability and held item.
    /// <para>
    /// Both are considered because a move can sit in two categories at once. They multiply together
    /// rather than the first match winning, which is how the games stack an ability boost with an
    /// item boost.
    /// </para>
    /// </summary>
    public static double GetMultiplier(uint moveFlags, int abilityId, int itemId = -1)
    {
        EnsureLoaded();
        double result = 1.0;
        foreach (var b in Bindings.Values)
        {
            if (!b.IsBound) continue;
            if ((moveFlags & (1u << b.Bit)) == 0) continue;

            bool applies = b.Trigger switch
            {
                FlagTrigger.Ability => b.TriggerId == abilityId,
                FlagTrigger.Item => itemId >= 0 && b.TriggerId == itemId,
                _ => false,
            };
            if (applies) result *= b.Multiplier;
        }
        return result;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Bindings.Clear();

        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<AbilityFlagBinding>>(File.ReadAllText(_path));
            if (list == null) return;
            foreach (var b in list.Where(b => IsFreeBit(b.Bit)))
                Bindings[b.Bit] = b;
        }
        catch
        {
            // A damaged store must not stop the editor opening; it just starts empty.
        }
    }

    private static void Save()
    {
        if (string.IsNullOrEmpty(_path)) return;
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(
                Bindings.Values.OrderBy(b => b.Bit).ToList(),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* the binding is still live in memory for this session */ }
    }
}
