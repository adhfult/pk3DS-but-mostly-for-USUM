using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Randomizers;

public class MartRandomizer
{
    private readonly string RomFSPath;
    private readonly int Mode; // 1: Shuffle, 2: Random
    private readonly bool BanBadItems;
    private readonly bool RandomizeAllShops;

    private const int FlatRandomExpandedSlots = 20;

    private static readonly int[] BannedItems = [0x1B, 0x4B, 0x4C, 0x4D, 0x12, 0x121, 0x122, 0x123, 0x124];
    private static readonly byte[] Signature =
    [
        0x2D, 0x00, 0x00, 0x00, 0x3B, 0x00, 0x00, 0x00, 0x2F, 0x00, 0x00, 0x00, 0x3D, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x10, 0x00, 0x00, 0x00, 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
    ];
    private static readonly byte[] BPSignature =
    [
        0x09, 0x0B, 0x0D, 0x0F, 0x11, 0x13, 0x14, 0x15, 0x09, 0x04, 0x08, 0x0C, 0x05, 0x04, 0x0B, 0x03,
        0x0A, 0x06, 0x0A, 0x06, 0x04, 0x05, 0x07, 0x01,
    ];

    private static readonly int[] entries = [9, 11, 13, 15, 17, 19, 20, 21, 9, 4, 8, 12, 5, 4, 11, 3, 10, 6, 10, 6, 4, 5, 7, 1];
    private static readonly int[] entriesBP = [8, 7, 18, 12, 21, 16];

    /// <summary>Items every regular Poke Mart list ends with (Repel, Super Repel).</summary>
    private static readonly ushort[] MartTerminator = [78, 79];

    /// <summary>Number of trial-stage lists, which are the same mart at successive stages.</summary>
    private const int TrialStages = 8;

    /// <summary>
    /// The real per-location slot counts, read out of Shop.cro rather than assumed.
    /// </summary>
    public static int[] ResolveEntryCounts(byte[] shopCro, int listStart, int listEnd, string croPath = null)
    {
        if (croPath != null && LoadCounts(croPath) is { } saved) return saved.Regular;

        var counts = (int[])entries.Clone();
        if (shopCro == null || listStart <= 0 || listEnd <= listStart || listEnd > shopCro.Length)
            return counts;

        var derived = new List<int>();
        int ofs = listStart, run = 0;
        ushort prev = 0;

        while (ofs + 1 < listEnd && derived.Count < TrialStages)
        {
            ushort v = BitConverter.ToUInt16(shopCro, ofs);
            ofs += 2; run++;
            if (run >= 2 && prev == MartTerminator[0] && v == MartTerminator[1])
            {
                derived.Add(run);
                run = 0;
            }
            prev = v;
        }

        // Only trust a complete reading. A partial one means this is not the layout expected, and
        // half-derived counts would be worse than the table.
        if (derived.Count != TrialStages) return counts;
        for (int i = 0; i < TrialStages; i++) counts[i] = derived[i];

        // Whatever the specialty shops' individual counts are, together they cannot exceed what is
        // left of the block.
        int remaining = ((listEnd - listStart) / 2) - derived.Sum();
        for (int i = TrialStages; i < counts.Length; i++)
        {
            if (remaining <= 0) { counts[i] = 0; continue; }
            counts[i] = Math.Min(counts[i], remaining);
            remaining -= counts[i];
        }
        return counts;
    }

    /// <summary>
    /// The shops, in the order <see cref="MartLayout.MartPatchAddrs"/> addresses them.
    /// </summary>
    private static readonly string[] locations =
    [
        "No Trials", "1 Trial", "2 Trials", "3 Trials", "4 Trials", "5 Trials", "6 Trials", "7 Trials",
        "Konikoni City [Incenses]",
        "Konikoni City [Herbs]",
        "Hau'oli City [X Items]",
        "Route 2 [Misc]",
        "Heahea City [TMs]",
        "Royal Avenue [TMs]",
        "Route 8 [Misc]",
        "Paniola Town [Poké Balls]",
        "Malie City [TMs]",
        "Mount Hokulani [Vitamins]",
        "Seafolk Village [TMs]",
        "Konikoni City [TMs]",
        "Konikoni City [Stones]",
        "Thrifty Megamart, Left [Poké Balls]",
        "Thrifty Megamart, Middle [Misc]",
        "Thrifty Megamart, Right [Strange Souvenir]",
        "Route 5 [X Items]",
        "Konikoni City [X Items]",
        "Tapu Village [X Items]",
        "Mount Lanakila [X Items]",
    ];

    private static readonly string[] locationsBP =
    [
        "Battle Royale (Left) [Medicine]",
        "Battle Royale (Middle) [EV Training]",
        "Battle Royale (Right) [Held Items]",
        "Battle Tree (Left) [Trade Evolution Items]",
        "Battle Tree (Middle) [Held Items]",
        "Battle Tree (Right) [Mega Stones]",
        "Big Wave Beach [Misc]",
    ];

    // X Attack, X Defense, X Sp. Atk, X Sp. Def, X Speed, X Accuracy, Dire Hit, Guard Spec.
    private static readonly int[] XItemIDs = [0x37, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x163];

    private const int MaxExpandedSlots = 128;

    /// <summary>
    /// Why slot expansion was skipped on the last run, or null if it was not skipped.
    /// </summary>
    public static string ExpansionSkipped { get; private set; }

    /// <summary>Whether to grow shops to fit their whole category list.</summary>
    public static bool ExpandSlots { get; set; } = true;

    /// <summary>
    /// Where the post-expansion slot counts are recorded, beside Shop.cro.
    /// </summary>
    private static string CountsPath(string croPath) =>
        Path.Combine(Path.GetDirectoryName(croPath) ?? "", "Shop.slotcounts.txt");

    /// <summary>Records the live counts beside Shop.cro so the editor reads the same file the same way.</summary>
    private static void SaveCounts(string croPath, int[] regular, int[] bp)
    {
        try
        {
            long size = new FileInfo(croPath).Length;
            File.WriteAllText(CountsPath(croPath),
                size + Environment.NewLine +
                string.Join(",", regular) + Environment.NewLine +
                string.Join(",", bp));
        }
        catch { /* the ROM is already correct; losing the note only affects display */ }
    }

    /// <summary>The recorded counts, or null when there are none.</summary>
    public static (int[] Regular, int[] BP)? LoadCounts(string croPath)
    {
        try
        {
            string p = CountsPath(croPath);
            if (!File.Exists(p) || !File.Exists(croPath)) return null;
            var lines = File.ReadAllLines(p);
            if (lines.Length < 3) return null;

            // Only trust the note if it describes the file that is actually there.
            if (!long.TryParse(lines[0].Trim(), out long size) || size != new FileInfo(croPath).Length)
                return null;

            int[] Parse(string s) => [.. s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(x => int.TryParse(x.Trim(), out int v) ? v : 0)];
            var reg = Parse(lines[1]);
            var bp = Parse(lines[2]);
            if (reg.Length != entries.Length || bp.Length != entriesBP.Length) return null;
            if (reg.Any(v => v <= 0) || bp.Any(v => v <= 0)) return null;
            return (reg, bp);
        }
        catch { return null; }
    }

    public MartRandomizer(string romfsPath, int mode, bool banBadItems, bool randomizeAllShops = false)
    {
        RomFSPath = romfsPath;
        Mode = mode;
        BanBadItems = banBadItems;
        RandomizeAllShops = randomizeAllShops;
    }

    /// <summary>Z-Crystals for the ROM being randomized; shops never stock them.</summary>
    private ZCrystalFilter ZFilter = new(null);

    private int RandomItemId(int maxItemID)
    {
        int item;
        int guard = 0;
        do
        {
            item = (int)(Util.Random32() % (uint)(maxItemID > 0 ? maxItemID : 800)) + 1;
        } while (guard++ < 256 &&
                 ((BanBadItems && Array.IndexOf(BannedItems, item) >= 0) || ZFilter.IsZCrystal(item)));
        return item;
    }

    public void Execute(int maxItemID)
    {
        if (Mode == 0) return;
        string croPath = Path.Combine(RomFSPath, "Shop.cro");
        if (!File.Exists(croPath)) return;

        byte[] data = File.ReadAllBytes(croPath);
        uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
        int rodataOffset = (int)BitConverter.ToUInt32(data, (int)segmentTableOffset + 0x0C);
        int offset = Util.IndexOfBytes(data, Signature, rodataOffset, 0) + Signature.Length;
        if (offset < Signature.Length) return;

        int currentOfs = offset;
        for (int entry = 0; entry < entries.Length; entry++)
        {
            int count = entries[entry];
            for (int i = 0; i < count; i++)
            {
                int item = RandomItemId(maxItemID);
                Array.Copy(BitConverter.GetBytes((ushort)item), 0, data, currentOfs + (2 * i), 2);
            }
            currentOfs += 2 * count;
        }

        CROUtil.UpdateHashes(data);
        File.WriteAllBytes(croPath, data);
    }

    /// <summary>
    /// Restocks every shop from its category list, growing shops when asked.
    /// </summary>
    public (int[] Regular, int[] BP) ExecuteCompetitive(int maxItemID, GameConfig config)
    {
        string croPath = Path.Combine(RomFSPath, "Shop.cro");
        if (!File.Exists(croPath)) return (null, null);

        byte[] data = File.ReadAllBytes(croPath);
        var layout = MartLayout.Read(data);
        if (!layout.Valid)
        {
            ExpansionSkipped = "Shop.cro's shop table could not be read, so no shop was changed.";
            return (null, null);
        }

        var problems = layout.Validate(data);
        if (problems.Count > 0)
        {
            ExpansionSkipped = "Shop.cro did not validate (" + problems[0] + "), so no shop was changed.";
            return (null, null);
        }

        string[] itemNames = config != null ? config.GetText(TextName.ItemNames) : [];
        ZFilter = new ZCrystalFilter(itemNames);

        string[] Present(string[] cat)
        {
            if (cat == null || cat.Length == 0) return cat ?? [];
            var kept = new List<string>(cat.Length);
            foreach (string n in cat)
            {
                if (ZCrystalFilter.IsZCrystalName(n)) continue;
                if (Array.FindIndex(itemNames, z => Competitive.GameNameComparer.Instance.Equals(z, n)) > 0)
                    kept.Add(n);
            }
            return [.. kept];
        }

        int GetItemByName(string name)
        {
            // A curated list should never name a crystal, but this is the single door every shop
            // item comes through, so it is checked here rather than trusted upstream.
            if (ZCrystalFilter.IsZCrystalName(name)) return 1;
            if (itemNames.Length == 0 || string.IsNullOrEmpty(name)) return 1;
            int idx = Array.FindIndex(itemNames, z => Competitive.GameNameComparer.Instance.Equals(z, name));
            return idx > 0 ? idx : 1;
        }

        string[] mainShopItems = Competitive.CompetitiveDatabase.MainShopItems;
        string[] tmPool = BuildTMPool(itemNames);
        string[] mintPool = BuildMintPool(itemNames);
        int tmCursor = 0;

        var regular = new int[layout.ShopOffsets.Length][];
        for (int i = 0; i < regular.Length; i++)
        {
            string where = i < locations.Length ? locations[i] : "";
            var cat = Present(ResolveRegularCategory(i, mainShopItems, tmPool, mintPool, RandomizeAllShops, out bool flat));
            int have = layout.ShopCounts[i];

            // Each TM shop draws its own slice of the shuffled pool, so two of them never stock
            // the same TMs.
            if (cat != null && where.Contains("[TMs]") && tmPool.Length > 0)
            {
                int want = ExpandSlots ? Math.Min(Math.Max(have, tmPool.Length / 4), MaxExpandedSlots) : have;
                var slice = new List<string>();
                for (int k = 0; k < want; k++) slice.Add(tmPool[tmCursor++ % tmPool.Length]);
                cat = [.. slice];
            }

            if (cat == null) { regular[i] = null; continue; }   // left exactly as it was

            int size = have;
            if (ExpandSlots)
            {
                int wanted = flat ? FlatRandomExpandedSlots : cat.Length;
                if (wanted > 0) size = Math.Min(Math.Max(have, wanted), MaxExpandedSlots);
            }

            var list = new int[size];
            for (int k = 0; k < size; k++)
            {
                list[k] = where.Contains("[X Items]")
                    ? XItemIDs[k % XItemIDs.Length]
                    : flat || cat.Length == 0
                        ? RandomItemId(maxItemID)
                        : GetItemByName(cat[k % cat.Length]);
            }
            regular[i] = list;
        }

        var bp = new int[layout.BPOffsets.Length][];
        for (int i = 0; i < bp.Length; i++)
        {
            var cat = Present(ResolveBPCategory(i, RandomizeAllShops, out bool flat));
            if (cat == null) { bp[i] = null; continue; }

            int have = layout.BPCounts[i];
            int size = have;
            if (ExpandSlots)
            {
                int wanted = flat ? FlatRandomExpandedSlots : cat.Length;
                if (wanted > 0) size = Math.Min(Math.Max(have, wanted), MaxExpandedSlots);
            }

            var list = new int[size];
            for (int k = 0; k < size; k++)
                list[k] = flat || cat.Length == 0 ? RandomItemId(maxItemID) : GetItemByName(cat[k % cat.Length]);
            bp[i] = list;
        }

        data = MartLayout.Rebuild(data, regular, bp, out var log);

        // Never save a Shop.cro whose own shop table no longer reads back. This is the check that
        // would have caught the previous model on its first run instead of in game.
        var after = MartLayout.Read(data);
        var afterProblems = after.Validate(data);
        if (!after.Valid || afterProblems.Count > 0)
        {
            ExpansionSkipped = "The rewritten Shop.cro did not validate (" +
                               (afterProblems.Count > 0 ? afterProblems[0] : "layout unreadable") +
                               "), so it was NOT saved and the shops are unchanged.";
            return (null, null);
        }

        CROUtil.UpdateHashes(data);
        File.WriteAllBytes(croPath, data);

        ExpansionSkipped = ExpandSlots ? null
            : "Shops were restocked within their existing slot counts; expansion was not requested.";

        return (after.ShopCounts, after.BPCounts);
    }

    private static string[] ResolveRegularCategory(int entry, string[] mainShopItems, string[] tmPool, string[] mintPool, bool randomizeAllShops, out bool flatRandom)
    {
        flatRandom = false;
        if (entry <= 7) return mainShopItems; // same mart, expanding across trial stages
        string locationName = entry < locations.Length ? locations[entry] : "";

        // Fixed-purpose shops keep their purpose.
        if (locationName.Contains("[X Items]")) return []; // fixed id list, non-null to mark "owned"
        if (locationName.Contains("[TMs]")) return tmPool.Length > 0 ? tmPool : [];
        if (locationName.Contains("[Mints]")) return mintPool.Length > 0 ? mintPool : null;

        var rotating = Competitive.CompetitiveDatabase.RotatingShopLists;
        if (rotating is { Length: > 0 })
        {
            // Count only the shops that actually take a rotating list, so the sequence does not
            // skip entries when a TM or X Item shop is passed over.
            int slot = 0;
            for (int i = 8; i < entry && i < locations.Length; i++)
            {
                string n = locations[i];
                if (!n.Contains("[X Items]") && !n.Contains("[TMs]") && !n.Contains("[Mints]")) slot++;
            }
            return rotating[slot % rotating.Length];
        }

        if (randomizeAllShops) { flatRandom = true; return []; }
        return null;
    }

    private static string[] ResolveBPCategory(int entry, bool randomizeAllShops, out bool flatRandom)
    {
        flatRandom = false;

        var rotating = Competitive.CompetitiveDatabase.RotatingShopLists;
        if (rotating is { Length: > 0 })
            return rotating[(RotatingRegularShopCount + entry) % rotating.Length];

        if (randomizeAllShops) { flatRandom = true; return []; }
        return null;
    }

    /// <summary>How many regular shops draw from the rotation, so the BP shops can continue it.</summary>
    private static int RotatingRegularShopCount
    {
        get
        {
            int n = 0;
            for (int i = 8; i < locations.Length; i++)
            {
                string s = locations[i];
                if (!s.Contains("[X Items]") && !s.Contains("[TMs]") && !s.Contains("[Mints]")) n++;
            }
            return n;
        }
    }

    /// <summary>
    /// What a BP shop charges. Always nothing.
    /// </summary>
    private static ushort GetBPPrice(int entry, bool flatRandom) => 0;

    /// <summary>
    /// Grows every mapped location's slot list inside Shop.cro so it can hold its whole category,
    /// capped at <see cref="MaxExpandedSlots"/>. Mutates liveEntries/liveEntriesBP with the new
    /// counts.
    /// </summary>
    private static byte[] ExpandMartSlots(
        byte[] data,
        string[][] regularCategoryFor, string[][] bpCategoryFor,
        bool[] flatRandom, bool[] flatRandomBP,
        int[] liveEntries, int[] liveEntriesBP)
    {
        ExpansionSkipped = null;

        // --- Regular entries ---
        {
            uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
            int rodataOffset = (int)BitConverter.ToUInt32(data, (int)segmentTableOffset + 0x0C);
            int offset = Util.IndexOfBytes(data, Signature, rodataOffset, 0) + Signature.Length;
            if (offset >= Signature.Length)
            {
                int[] targetCounts = new int[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    if (i >= TrialStages) { targetCounts[i] = liveEntries[i]; continue; }
                    var cat = regularCategoryFor[i];
                    int wantedLen = flatRandom[i] ? FlatRandomExpandedSlots : cat?.Length ?? -1;
                    targetCounts[i] = wantedLen < 0 ? entries[i] : Math.Max(entries[i], Math.Min(wantedLen, MaxExpandedSlots));
                }

                for (int i = entries.Length - 1; i >= 0; i--)
                {
                    int grow = targetCounts[i] - liveEntries[i];
                    for (int g = 0; g < grow; g++)
                    {
                        int currentOfs = offset;
                        for (int j = 0; j < i; j++) currentOfs += 2 * liveEntries[j];
                        int insertionPoint = currentOfs + (liveEntries[i] * 2);

                        data = CROUtil.ExpandSegment(data, 'r', 2, insertionPoint, 0x01);
                        liveEntries[i]++;
                    }
                }
            }
            else
            {
                ExpansionSkipped = "The regular shop table could not be located in Shop.cro, so those " +
                                   "shops were left at their existing slot counts.";
            }
        }

        // --- BP entries (re-locate the BP signature fresh, since the regular-entry expansion
        // above may have shifted its position within the now-updated `data`) ---
        {
            uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
            int rodataOffset = (int)BitConverter.ToUInt32(data, (int)segmentTableOffset + 0x0C);
            int offsetBP = Util.IndexOfBytes(data, BPSignature, rodataOffset, 0) + BPSignature.Length;
            if (offsetBP >= BPSignature.Length)
            {
                int[] targetCountsBP = new int[entriesBP.Length];
                for (int i = 0; i < entriesBP.Length; i++)
                {
                    targetCountsBP[i] = liveEntriesBP[i];
                }

                for (int i = entriesBP.Length - 1; i >= 0; i--)
                {
                    int grow = targetCountsBP[i] - liveEntriesBP[i];
                    for (int g = 0; g < grow; g++)
                    {
                        int currentOfs = offsetBP;
                        for (int j = 0; j < i; j++) currentOfs += 4 * liveEntriesBP[j];
                        int insertionPoint = currentOfs + (liveEntriesBP[i] * 4);

                        data = CROUtil.ExpandSegment(data, 'r', 4, insertionPoint, 0x00);
                        liveEntriesBP[i]++;
                    }
                }
            }
        }

        return data;
    }

    /// <summary>
    /// The Nature Mints present in this ROM, or an empty array when it has none.
    /// </summary>
    private static string[] BuildMintPool(string[] itemNames)
    {
        var mints = new List<string>();
        if (itemNames == null) return [];

        foreach (string nm in itemNames)
        {
            if (string.IsNullOrWhiteSpace(nm)) continue;
            // "Lonely Mint", "Adamant Mint", ... - the noun is always the last word.
            if (nm.EndsWith(" Mint", StringComparison.OrdinalIgnoreCase) && !mints.Contains(nm))
                mints.Add(nm);
        }
        return mints.ToArray();
    }

    private static string[] BuildTMPool(string[] itemNames)
    {
        var tmNames = new List<string>();
        if (itemNames != null)
        {
            foreach (string nm in itemNames)
            {
                if (string.IsNullOrEmpty(nm) || nm.Length < 3) continue;
                if (nm.StartsWith("TM", StringComparison.OrdinalIgnoreCase) && char.IsDigit(nm[2]))
                    tmNames.Add(nm);
            }
        }
        var tmArr = tmNames.ToArray();
        Util.Shuffle(tmArr);
        return tmArr;
    }
}
