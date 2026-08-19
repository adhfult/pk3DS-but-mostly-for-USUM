// A miss is a normal outcome here - most lookups are for items with no art installed - so the
// null-returning paths are load-bearing and worth having the compiler check.
#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace pk3DS.WinForms;

/// <summary>
/// Item sprites looked up by name rather than by index.
/// <para>
/// Keying on the numeric ID was fine while IDs were fixed, but this project moves them: the TM
/// expansion allocates 1024-1051 for TM101-128, and a randomizer run can reshuffle what any given
/// index means. A cache keyed on the index hands back the previous item's picture after either of
/// those, and the failure is silent - the wrong sprite is still a valid sprite.
/// </para>
/// <para>
/// Names survive both. The name is normalised before lookup so that the game's typographic
/// apostrophe (U+2019, as in "King's Rock") matches an ASCII filename, and so that spacing,
/// case and punctuation differences between a ROM's text and whatever a file was called do not
/// cause a miss.
/// </para>
/// </summary>
public static class ItemSpriteCache
{
    private static readonly Dictionary<string, Image?> Cache = new(StringComparer.Ordinal);
    private static readonly object Sync = new();
    private static string[]? _searchRoots;

    /// <summary>Folders scanned for sprites, nearest first.</summary>
    public static string[] SearchRoots => _searchRoots ??=
    [
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ItemSprites"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomSprites", "Items"),
    ];

    /// <summary>
    /// Reduces a name to its comparable form: lowercase, no punctuation, no spaces. "King's Rock",
    /// "kings-rock" and "KingsRock.png" all collapse to the same key, which is what lets a sprite
    /// dropped in by hand match the ROM's own text without the two having to agree on styling.
    /// </summary>
    public static string Normalise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>
    /// The sprite for an item name, or null when none is installed. Misses are cached too, so a
    /// missing sprite costs one directory probe rather than one per repaint.
    /// </summary>
    public static Image? Get(string? itemName)
    {
        string key = Normalise(itemName);
        if (key.Length == 0) return null;

        lock (Sync)
        {
            if (!Cache.TryGetValue(key, out var cached))
            {
                cached = Load(key);
                Cache[key] = cached;
            }

            return cached == null ? null : new Bitmap(cached);
        }
    }

    private static Image? Load(string key)
    {
        foreach (string root in SearchRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (string file in Directory.EnumerateFiles(root))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".png" or ".bmp" or ".gif" or ".jpg" or ".jpeg")) continue;
                if (Normalise(Path.GetFileNameWithoutExtension(file)) != key) continue;

                try
                {
                    // Read through memory so the file is not left locked - sprites live in a folder
                    // the user edits while the editor is open.
                    byte[] bytes = File.ReadAllBytes(file);
                    using var ms = new MemoryStream(bytes);
                    return new Bitmap(ms);
                }
                catch { return null; }
            }
        }
        return null;
    }

    /// <summary>Drops every entry, so newly added files are picked up without a restart.</summary>
    public static void Invalidate()
    {
        lock (Sync)
        {
            foreach (var img in Cache.Values) img?.Dispose();
            Cache.Clear();
        }
    }

    /// <summary>How many names resolved and how many did not, for a quick sanity check.</summary>
    public static (int Hits, int Misses) Coverage(IEnumerable<string> itemNames)
    {
        int hit = 0, miss = 0;
        foreach (string n in itemNames.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (Get(n) != null) hit++; else miss++;
        }
        return (hit, miss);
    }
}
