#nullable enable

using System;
using System.IO;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Which column of the research corpus applies to the ROM in front of you.
/// </summary>
public static class ResearchVersion
{
    /// <summary>Version strings the corpus is written against.</summary>
    public static readonly string[] Known = ["UM", "US", "SM"];

    /// <summary>Set from the UI to override the inferred value. Null means infer.</summary>
    public static string? Override { get; set; }

    /// <summary>
    /// Best guess at the corpus version for a loaded game, honouring <see cref="Override"/>.
    /// </summary>
    public static string Resolve(GameConfig? cfg, string? romfsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(Override)) return Override!;

        // Sun/Moon are a different generation of the corpus entirely.
        if (cfg?.SM == true) return "SM";

        string hint = (romfsPath ?? "") + " " + (cfg?.RomFS ?? "");
        if (hint.Length > 0)
        {
            // Check Moon first: "Ultra Sun" and "Ultra Moon" both contain "U", and a path holding
            // both words is more likely a parent folder than a ROM, so the more specific token wins.
            if (Contains(hint, "ultramoon") || Contains(hint, "ultra moon")
                || Contains(hint, "_um") || Contains(hint, "-um") || Contains(hint, "\\um"))
                return "UM";

            if (Contains(hint, "ultrasun") || Contains(hint, "ultra sun")
                || Contains(hint, "_us") || Contains(hint, "-us") || Contains(hint, "\\us"))
                return "US";
        }

        // UM is the version the bulk of the corpus was written against, so it is the safer default.
        return "UM";
    }

    /// <summary>
    /// Whether the version was inferred rather than chosen, so the UI can say so instead of
    /// presenting a guess as a fact.
    /// </summary>
    public static bool IsInferred => string.IsNullOrWhiteSpace(Override);

    /// <summary>Cache file for one version, so two games never share a parsed corpus.</summary>
    public static string CachePathFor(string version) =>
        ResearchDatabase.CacheFile($"research_cache_{version}.json");

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
