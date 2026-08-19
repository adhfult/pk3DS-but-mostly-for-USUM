using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Grows the item name and description tables in every language so the expansion's new item IDs
/// resolve to real text.
/// <para>
/// The item-name bounds check in code.bin is a single global limit, but the text itself lives in a
/// separate GARC per language. On the checked UM Expansion build only English carries 1024 item
/// names; the other nine stop at 960. Raising the bound so the game will look up TM101-128 without
/// also lengthening those tables asks it to read past the end of nine of the ten files.
/// </para>
/// <para>
/// New slots are filled from English rather than left blank, because a French player seeing "TM101"
/// is strictly better than a missing entry, and there is no translation to draw on for an item the
/// game never shipped.
/// </para>
/// </summary>
public static class ExpandedTMText
{
    /// <summary>Language index whose text is used to fill new slots in the others.</summary>
    public const int SourceLanguage = 2; // English

    /// <summary>Language slots a Gen 7 build can carry.</summary>
    private const int LanguageCount = 10;

    public sealed class LanguageResult
    {
        public int Language { get; init; }
        public int NamesBefore { get; init; }
        public int NamesAfter { get; init; }
        public int FlavorBefore { get; init; }
        public int FlavorAfter { get; init; }
        public bool Changed { get; init; }
        public string Note { get; init; } = "";

        public override string ToString() =>
            $"lang {Language,2}: names {NamesBefore}->{NamesAfter}, flavor {FlavorBefore}->{FlavorAfter}" +
            (Changed ? "" : " (unchanged)") + (Note.Length > 0 ? $"  {Note}" : "");
    }

    public sealed class Result
    {
        public List<LanguageResult> Languages { get; } = [];
        public int ChangedCount => Languages.Count(l => l.Changed);
        public string Describe() =>
            $"item text grown in {ChangedCount} language(s):" + Environment.NewLine +
            string.Join(Environment.NewLine, Languages.Select(l => "  " + l));
    }

    /// <summary>
    /// Ensures every language's item name and description tables hold at least
    /// <paramref name="requiredCount"/> entries.
    /// <para>
    /// The caller's language is restored before returning, so this can run from an editor without
    /// leaving the loaded config pointing somewhere else.
    /// </para>
    /// </summary>
    public static Result EnsureCapacity(GameConfig cfg, int requiredCount)
    {
        var result = new Result();
        if (cfg == null || requiredCount <= 0) return result;

        int originalLanguage = cfg.Language;
        string[] sourceNames = [];
        string[] sourceFlavor = [];

        // Read the fill source first; if English itself is short it still supplies what it has.
        try
        {
            SwitchTo(cfg, SourceLanguage);
            sourceNames = cfg.GetText(TextName.ItemNames);
            sourceFlavor = cfg.GetText(TextName.ItemFlavor);
        }
        catch { }

        try
        {
            for (int lang = 0; lang < LanguageCount; lang++)
            {
                try
                {
                    SwitchTo(cfg, lang);

                    string[] names = cfg.GetText(TextName.ItemNames);
                    string[] flavor = cfg.GetText(TextName.ItemFlavor);
                    if (names.Length == 0 && flavor.Length == 0)
                    {
                        result.Languages.Add(new LanguageResult { Language = lang, Note = "no item text" });
                        continue;
                    }

                    int nBefore = names.Length, fBefore = flavor.Length;
                    bool grew = false;

                    if (names.Length < requiredCount)
                    {
                        names = Grow(names, requiredCount, sourceNames, "???");
                        cfg.SetText(TextName.ItemNames, names);
                        cfg.SaveText(TextName.ItemNames);
                        grew = true;
                    }

                    if (flavor.Length < requiredCount)
                    {
                        flavor = Grow(flavor, requiredCount, sourceFlavor, "");
                        cfg.SetText(TextName.ItemFlavor, flavor);
                        cfg.SaveText(TextName.ItemFlavor);
                        grew = true;
                    }

                    cfg.InitializeGameText();
                    int nAfter = cfg.GetText(TextName.ItemNames).Length;
                    int fAfter = cfg.GetText(TextName.ItemFlavor).Length;

                    result.Languages.Add(new LanguageResult
                    {
                        Language = lang,
                        NamesBefore = nBefore, NamesAfter = nAfter,
                        FlavorBefore = fBefore, FlavorAfter = fAfter,
                        Changed = grew,
                        Note = (nAfter < requiredCount || fAfter < requiredCount) ? "WRITE DID NOT PERSIST" : "",
                    });
                }
                catch (Exception ex)
                {
                    result.Languages.Add(new LanguageResult { Language = lang, Note = $"skipped: {ex.GetType().Name}" });
                }
            }
        }
        finally
        {
            // Restore whatever the caller had loaded, even if a language above threw.
            try { SwitchTo(cfg, originalLanguage); } catch { }
        }

        return result;
    }

    private static void SwitchTo(GameConfig cfg, int lang)
    {
        if (cfg.Language == lang && cfg.GameTextStrings != null) return;
        cfg.Language = lang;
        cfg.InitializeGameText();
    }

    private static string[] Grow(string[] table, int required, string[] fill, string fallback)
    {
        var grown = new string[required];
        Array.Copy(table, grown, table.Length);
        for (int i = table.Length; i < required; i++)
            grown[i] = i < fill.Length && !string.IsNullOrEmpty(fill[i]) ? fill[i] : fallback;
        return grown;
    }
}
