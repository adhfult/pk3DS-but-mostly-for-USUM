#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Finds free item / move / ability slots for a recipe that needs them.
/// </summary>
public static class RecipeIdAllocator
{
    /// <summary>Placeholder names the games use for a slot that is not really occupied.</summary>
    private static readonly string[] Placeholders = ["???", "-----", "—", "———"];

    /// <summary>Whether an id is unnamed or holds a placeholder.</summary>
    public static bool IsFree(string[]? names, int id)
    {
        if (names == null || id < 1 || id >= names.Length) return false;
        string n = names[id]?.Trim() ?? "";
        return n.Length == 0
            || Array.IndexOf(Placeholders, n) >= 0
            || n.All(c => c is '?' or '？' or '(' or ')');
    }

    /// <summary>Lowest free id not in <paramref name="taken"/>, or -1.</summary>
    public static int NextFree(string[]? names, ISet<int>? taken = null)
    {
        if (names == null) return -1;
        for (int i = 1; i < names.Length; i++)
            if (IsFree(names, i) && (taken == null || !taken.Contains(i))) return i;
        return -1;
    }

    /// <summary>
    /// Start of the lowest run of <paramref name="count"/> consecutive free ids, or -1.
    /// </summary>
    public static int NextFreeRun(string[]? names, int count)
    {
        if (names == null || count <= 0) return -1;
        if (count == 1) return NextFree(names);

        for (int i = 1; i + count <= names.Length; i++)
        {
            bool all = true;
            for (int k = 0; k < count && all; k++)
                if (!IsFree(names, i + k)) all = false;
            if (all) return i;
        }
        return -1;
    }

    /// <summary>The name table a mechanic kind's ids live in.</summary>
    public static string[]? TableFor(GameConfig config, CustomMechanicKind kind) => kind switch
    {
        CustomMechanicKind.Move => config?.GetText(TextName.MoveNames),
        CustomMechanicKind.Ability => config?.GetText(TextName.AbilityNames),
        _ => config?.GetText(TextName.ItemNames),
    };

    /// <summary>The name table a package parameter's ids live in.</summary>
    public static string[]? TableForParameter(GameConfig config, string? type) => (type ?? "").ToLowerInvariant() switch
    {
        "move" => config?.GetText(TextName.MoveNames),
        "ability" => config?.GetText(TextName.AbilityNames),
        _ => config?.GetText(TextName.ItemNames),
    };

    /// <summary>
    /// Assigns every id a recipe needs, and reports what could not be placed.
    /// </summary>
    public static Dictionary<string, string>? AssignIds(Recipe recipe, GameConfig config, List<string> problems)
    {
        if (recipe == null || config == null) return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ps = recipe.Package?.Parameters;

        if (ps is { Count: > 0 })
        {
            var taken = new HashSet<int>();

            // List parameters first: their ids cannot move.
            foreach (var (key, p) in ps)
            {
                if (!string.Equals(p?.Type, "list", StringComparison.OrdinalIgnoreCase)) continue;
                values[key] = p?.Default ?? "";
                foreach (string part in (p?.Default ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (int.TryParse(part, out int id)) taken.Add(id);
            }

            foreach (var (key, p) in ps)
            {
                if (string.Equals(p?.Type, "list", StringComparison.OrdinalIgnoreCase)) continue;
                var table = TableForParameter(config, p?.Type);

                // Keep the authored default when it is genuinely free on THIS ROM and unclaimed.
                if (int.TryParse(p?.Default, out int def) && IsFree(table, def) && !taken.Contains(def))
                {
                    values[key] = def.ToString();
                    taken.Add(def);
                    continue;
                }

                // A parameter pointing at something that must already exist (AllowUnnamed = false)
                // keeps its default: substituting a blank slot would break a working reference.
                if (p?.AllowUnnamed == false)
                {
                    values[key] = p?.Default ?? "";
                    continue;
                }

                int free = NextFree(table, taken);
                if (free <= 0) { problems.Add($"no free slot for '{key}'"); return null; }
                values[key] = free.ToString();
                taken.Add(free);
            }
            return values;
        }

        // Not package-driven: the recipe's own entries need a consecutive run.
        if (recipe.SlotCount > 0)
        {
            int start = NextFreeRun(TableFor(config, recipe.Kind), recipe.SlotCount);
            if (start <= 0)
            {
                problems.Add($"no run of {recipe.SlotCount} consecutive free id(s)");
                return null;
            }
            for (int i = 0; i < recipe.Entries.Count; i++) recipe.Entries[i].Id = start + i;
        }

        return values;
    }
}
