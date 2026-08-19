using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Classifies a worksheet by its actual column schema, then extracts typed records for that
/// schema.
/// <para>
/// The previous pipeline ran one heuristic ("find any column named Offset/Address, read a Hex
/// column next to it") over every sheet in the tree. That produced ~29k "patches" including
/// obvious nonsense — offsets past 0x33000000, which is far beyond any CRO or code.bin — because
/// index sheets, type charts and text dumps were all being read as if they were patch lists. It
/// also meant the relocation tracker, the 3k-entry stock-function index and the free-space
/// registry were mined for patches and otherwise thrown away.
/// </para>
/// </summary>
public static class ResearchSheetReader
{
    /// <summary>Largest plausible in-file offset. USUM's .code.bin is ~6 MB; CROs are far smaller.</summary>
    public const uint MaxPlausibleOffset = 0x0100_0000; // 16 MB

    public static ResearchSheet Read(string xlsxPath, string sheetName, string category, string version)
    {
        var sheet = new ResearchSheet
        {
            SourceFile = xlsxPath,
            SheetName = sheetName,
            Category = category,
            DisplayName = BuildDisplayName(xlsxPath, sheetName),
        };

        var grid = ResearchXlsxReader.ReadSheet(xlsxPath, sheetName, sheet.Diagnostics);
        if (grid == null || grid.RowCount == 0)
        {
            sheet.Kind = ResearchSheetKind.Unknown;
            return sheet;
        }

        sheet.Target = DetectTarget(grid, xlsxPath, sheetName);
        var (kind, headerRow, confidence) = Classify(grid, sheetName);
        sheet.Kind = kind;
        sheet.Confidence = confidence;

        switch (kind)
        {
            case ResearchSheetKind.RelocationTracker: ExtractRelocations(grid, headerRow, sheet); break;
            case ResearchSheetKind.FunctionIndex: ExtractFunctions(grid, headerRow, sheet); break;
            case ResearchSheetKind.TableRegistry: ExtractTableLocations(grid, headerRow, sheet); break;
            case ResearchSheetKind.FreeSpaceRegistry: ExtractFreeSpace(grid, headerRow, sheet); break;
            case ResearchSheetKind.TimingFlags: ExtractTimings(grid, headerRow, sheet); break;
            case ResearchSheetKind.MechanicIndex: ExtractMechanicIndex(grid, headerRow, sheet); break;
            case ResearchSheetKind.PatchList: ExtractPatches(grid, headerRow, sheet, version); break;
            default: break; // Reference / Unknown: retained for lookup, nothing extracted
        }

        return sheet;
    }

    private static string BuildDisplayName(string xlsxPath, string sheetName)
    {
        string baseName = Path.GetFileNameWithoutExtension(xlsxPath);
        bool generic = sheetName.StartsWith("Sheet", StringComparison.OrdinalIgnoreCase);
        return generic ? baseName : $"{baseName} - {sheetName}";
    }

    #region Classification

    private static readonly string[] RelocationHeaders =
        ["Patch Address", "Write-to", "Pointer", "Write Segment", "Pointer Segment"];
    private static readonly string[] FunctionIndexHeaders =
        ["Function", "Details", "Loaded", "Offset", "CRO"];
    private static readonly string[] TableRegistryHeaders =
        ["Code Location", "Table Data"];
    private static readonly string[] FreeSpaceHeaders =
        ["Room", "Length", "Offset", "Function"];
    private static readonly string[] PatchHeaders =
        ["Hex", "Offset", "Address", "Assembly", "Replacing", "instruction"];

    private static (ResearchSheetKind Kind, int HeaderRow, double Confidence) Classify(ResearchGrid grid, string sheetName)
    {
        int mi = grid.FindHeaderRow(["Offset (loaded)", "Offset (file)", "Reference", "Decimal"], minimumMatches: 3);
        if (mi >= 0)
        {
            var mh = grid.HeaderMap(mi);
            if (ResearchGrid.ColumnOf(mh, "Reference") >= 0 && ResearchGrid.ColumnOf(mh, "Offset (file)") >= 0)
                return (ResearchSheetKind.MechanicIndex, mi, 0.95);
        }

        // Relocation tracker: the only schema carrying both a patch address and a pointer column.
        int r = grid.FindHeaderRow(RelocationHeaders, minimumMatches: 3);
        if (r >= 0)
        {
            var h = grid.HeaderMap(r);
            if (ResearchGrid.ColumnOf(h, "Write-to", "Write to", "WriteTo") >= 0 &&
                ResearchGrid.ColumnOf(h, "Pointer") >= 0)
                return (ResearchSheetKind.RelocationTracker, r, 0.95);
        }

        // Table registry: "Code Location" + "Table Data" paired original/edited columns.
        r = grid.FindHeaderRow(TableRegistryHeaders, minimumMatches: 2);
        if (r >= 0)
        {
            var h = grid.HeaderMap(r);
            if (ResearchGrid.ColumnOf(h, "Code Location") >= 0 && ResearchGrid.ColumnOf(h, "Table Data") >= 0)
                return (ResearchSheetKind.TableRegistry, r, 0.9);
        }

        // Timing flags: the byte-meaning table keyed off the 0x876F8 return value.
        r = grid.FindHeaderRow(["Byte stored at return", "Examples"], minimumMatches: 1);
        if (r >= 0 && grid.HeaderMap(r).Keys.Any(k => k.Contains("Byte stored at return", StringComparison.OrdinalIgnoreCase)))
            return (ResearchSheetKind.TimingFlags, r, 0.9);

        // Free-space registry: "Room" is unique to the custom-function tracker.
        r = grid.FindHeaderRow(FreeSpaceHeaders, minimumMatches: 3);
        if (r >= 0)
        {
            var h = grid.HeaderMap(r);
            if (ResearchGrid.ColumnOf(h, "Room") >= 0 && ResearchGrid.ColumnOf(h, "Offset") >= 0)
                return (ResearchSheetKind.FreeSpaceRegistry, r, 0.85);
        }

        // Function index: offset + a Function/Details description, and NO hex payload column.
        r = grid.FindHeaderRow(FunctionIndexHeaders, minimumMatches: 2);
        if (r >= 0)
        {
            var h = grid.HeaderMap(r);
            bool hasOffset = ResearchGrid.ColumnOf(h, "Offset", "CRO", "Address") >= 0;
            bool hasFunction = ResearchGrid.ColumnOf(h, "Function") >= 0;
            bool hasHex = ResearchGrid.ColumnOf(h, "Hex") >= 0;
            if (hasOffset && hasFunction && !hasHex)
                return (ResearchSheetKind.FunctionIndex, r, 0.85);
        }

        // Patch list: an offset paired with a hex payload or assembly text.
        r = grid.FindHeaderRow(PatchHeaders, minimumMatches: 2);
        if (r >= 0)
        {
            var h = grid.HeaderMap(r);
            bool hasOffset = ResearchGrid.ColumnOf(h, "in-file", "Offset", "Address") >= 0;
            bool hasPayload = ResearchGrid.ColumnOf(h, "Hex", "Assembly", "Replacing", "instruction", "ARM") >= 0;
            if (hasOffset && hasPayload)
                return (ResearchSheetKind.PatchList, r, 0.8);
        }

        // Known reference sheets we deliberately never treat as patches.
        if (sheetName.Equals("txt", StringComparison.OrdinalIgnoreCase) ||
            sheetName.Equals("Tables", StringComparison.OrdinalIgnoreCase) ||
            sheetName.Contains("chart", StringComparison.OrdinalIgnoreCase) ||
            sheetName.Contains("Table of IDs", StringComparison.OrdinalIgnoreCase))
            return (ResearchSheetKind.Reference, -1, 0.6);

        if (LooksPositionalPatchList(grid))
            return (ResearchSheetKind.PatchList, -1, 0.7);

        return (ResearchSheetKind.Unknown, -1, 0.0);
    }

    /// <summary>
    /// True when the sheet has no header but reads as address/hex pairs in the first two columns.
    /// Requires several consecutive hits so a stray hex-looking cell can't promote a notes sheet.
    /// </summary>
    private static bool LooksPositionalPatchList(ResearchGrid grid)
    {
        int hits = 0, examined = 0;
        for (int r = 0; r < grid.RowCount && examined < 60; r++)
        {
            if (grid.IsRowEmpty(r)) continue;
            examined++;
            string a = grid[r, 0].Trim();
            string b = grid[r, 1].Trim();
            if (!TryHex(a, out _)) continue;
            string hex = b.Replace(" ", "").Replace("-", "");
            if (hex.Length is 8 && hex.All(Uri.IsHexDigit)) hits++;
        }
        return hits >= 5;
    }

    private static ResearchTarget DetectTarget(ResearchGrid grid, string xlsxPath, string sheetName)
    {
        // A sheet named after the binary is the most reliable signal (the stock-function and
        // custom-function workbooks are organised exactly this way).
        var byName = FromText(sheetName);
        if (byName != ResearchTarget.Unknown) return byName;

        // Otherwise scan a bounded window of cells for an explicit mention.
        int rows = Math.Min(grid.RowCount, 40);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < Math.Min(grid.ColumnCount, 20); c++)
            {
                var t = FromText(grid[r, c]);
                if (t != ResearchTarget.Unknown) return t;
            }
        }

        var byFile = FromText(Path.GetFileNameWithoutExtension(xlsxPath));
        if (byFile != ResearchTarget.Unknown) return byFile;

        return ResearchTarget.BattleCro; // overwhelmingly the common case for effect research
    }

    private static ResearchTarget FromText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return ResearchTarget.Unknown;
        if (s.Contains("Battle.cro", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.BattleCro;
        if (s.Contains("Bag.cro", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.BagCro;
        if (s.Contains("Shop.cro", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.ShopCro;
        if (s.Contains("Box.cro", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.BoxCro;
        if (s.Contains("Status.cro", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.StatusCro;
        if (s.Contains("FieldRo.cro", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.FieldRoCro;
        if (s.Contains("Evolution.cro", StringComparison.OrdinalIgnoreCase) || s.Contains("Evolution", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.EvolutionCro;
        if (s.Contains("code.bin", StringComparison.OrdinalIgnoreCase)) return ResearchTarget.CodeBin;
        return ResearchTarget.Unknown;
    }

    #endregion

    #region Extractors

    private static void ExtractRelocations(ResearchGrid grid, int headerRow, ResearchSheet sheet)
    {
        var h = grid.HeaderMap(headerRow);
        int cCategory = ResearchGrid.ColumnOf(h, "Category");
        int cSpecific = ResearchGrid.ColumnOf(h, "Specific");
        int cTarget = ResearchGrid.ColumnOf(h, "Target");
        int cPatchAddr = ResearchGrid.ColumnOf(h, "Patch Address");
        int cWriteTo = ResearchGrid.ColumnOf(h, "Write-to", "Write to", "WriteTo");
        int cPointer = ResearchGrid.ColumnOf(h, "Pointer");
        int cWriteSeg = ResearchGrid.ColumnOf(h, "Write Segment");
        int cPtrSeg = ResearchGrid.ColumnOf(h, "Pointer Segment");
        int cBss = ResearchGrid.ColumnOf(h, ".bss", "bss");

        for (int r = headerRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;

            bool haveWrite = TryHex(grid[r, cWriteTo], out uint writeTo);
            bool havePtr = TryHex(grid[r, cPointer], out uint pointer);
            if (!haveWrite && !havePtr) continue; // narrative/blank row

            var rec = new ResearchRelocation
            {
                Category = Get(grid, r, cCategory),
                Specific = Get(grid, r, cSpecific),
                TargetNote = Get(grid, r, cTarget),
                WriteTo = writeTo,
                Pointer = pointer,
                Target = sheet.Target,
                Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, r),
            };
            if (TryHex(grid[r, cPatchAddr], out uint pa)) rec.PatchAddress = pa;
            if (TryInt(grid[r, cWriteSeg], out int ws)) rec.WriteSegment = ws;
            if (TryInt(grid[r, cPtrSeg], out int ps)) rec.PointerSegment = ps;
            string bss = Get(grid, r, cBss);
            rec.IsBss = bss.Equals("true", StringComparison.OrdinalIgnoreCase) || bss == "1";

            sheet.Relocations.Add(rec);
        }
    }

    private static void ExtractFunctions(ResearchGrid grid, int headerRow, ResearchSheet sheet)
    {
        var h = grid.HeaderMap(headerRow);
        int cOffset = ResearchGrid.ColumnOf(h, "Offset", "CRO");
        int cLoaded = ResearchGrid.ColumnOf(h, "Loaded");
        int cFunction = ResearchGrid.ColumnOf(h, "Function");
        int cDetails = ResearchGrid.ColumnOf(h, "Details");

        for (int r = headerRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;
            if (!TryHex(grid[r, cOffset], out uint offset)) continue;
            if (offset == 0 || offset > MaxPlausibleOffset) continue;

            string name = Get(grid, r, cFunction);
            string details = Get(grid, r, cDetails);

            string loadedText = Get(grid, r, cLoaded);
            if (TryHex(name, out _) && !string.IsNullOrWhiteSpace(loadedText) && !TryHex(loadedText, out _))
                (name, loadedText) = (loadedText, name);

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(details)) continue;

            var rec = new ResearchFunction
            {
                Offset = offset,
                Name = name,
                Details = details,
                Target = sheet.Target,
                Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, r),
            };
            if (TryHex(loadedText, out uint loaded)) rec.LoadedAddress = loaded;
            sheet.Functions.Add(rec);
        }
    }

    /// <summary>
    /// Reads the "Table locations and sizes" registry: which master tables exist, where they live
    /// originally, and where they've been relocated to.
    /// <para>
    /// The sheet uses a two-row header — a banner row spanning each block ("Code Location" at one
    /// column, "Table Data" further right) with per-column sub-headers ("Original", "Post-Load",
    /// "# entries", "Edited") on the row below. Both blocks are resolved by finding their
    /// sub-header columns within the block's span, so re-ordering or inserting a column in the
    /// spreadsheet doesn't break the mapping.
    /// </para>
    /// </summary>
    private static void ExtractTableLocations(ResearchGrid grid, int bannerRow, ResearchSheet sheet)
    {
        int cCodeBanner = ResearchGrid.ColumnOf(grid.HeaderMap(bannerRow), "Code Location");
        int cDataBanner = ResearchGrid.ColumnOf(grid.HeaderMap(bannerRow), "Table Data");
        if (cCodeBanner < 0 && cDataBanner < 0) return;

        int subRow = bannerRow + 1;
        if (subRow >= grid.RowCount) return;

        // Locate a sub-header within [from, to) on the sub-header row.
        int SubCol(int from, int to, params string[] fragments)
        {
            if (from < 0) return -1;
            to = to < 0 ? grid.ColumnCount : Math.Min(to, grid.ColumnCount);
            for (int c = from; c < to; c++)
            {
                string v = grid[subRow, c];
                if (string.IsNullOrWhiteSpace(v)) continue;
                foreach (var f in fragments)
                    if (v.Contains(f, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return -1;
        }

        int codeEnd = cDataBanner > cCodeBanner ? cDataBanner : grid.ColumnCount;
        int cCodeOrig = SubCol(cCodeBanner, codeEnd, "Original");
        int cEntryParam = SubCol(cCodeBanner, codeEnd, "Entry Param Address");
        int cCount = SubCol(cCodeBanner, codeEnd, "# entries", "entries");
        int cLogic = SubCol(cCodeBanner, codeEnd, "Logic");
        int cCountEdit = SubCol(cCodeBanner, codeEnd, "Edited");
        int cDataOrig = SubCol(cDataBanner, grid.ColumnCount, "Original");
        int cDataEdit = SubCol(cDataBanner, grid.ColumnCount, "Edited");

        for (int r = subRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;

            // Row label is the leftmost non-numeric cell.
            string label = null;
            for (int c = 0; c < grid.ColumnCount; c++)
            {
                string v = grid[r, c];
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (TryHex(v, out _) || TryInt(v, out _)) continue;
                label = v.Trim();
                break;
            }
            if (string.IsNullOrWhiteSpace(label)) continue;
            // Spill-over from adjacent scratch areas: a label that's really a number, or one of
            // the generic before/after captions used elsewhere in these workbooks.
            if (double.TryParse(label, out _)) continue;
            if (label.Equals("Original Number", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("New", StringComparison.OrdinalIgnoreCase)) continue;

            var rec = new ResearchTableLocation
            {
                Name = label,
                Target = sheet.Target,
                BoundsCondition = cLogic >= 0 ? grid[r, cLogic].Trim() : null,
                Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, r),
            };
            if (cCodeOrig >= 0 && TryHex(grid[r, cCodeOrig], out uint co)) rec.CodeLocation = co;
            if (cEntryParam >= 0 && TryHex(grid[r, cEntryParam], out uint ep)) rec.EntryParamAddress = ep;
            if (cDataOrig >= 0 && TryHex(grid[r, cDataOrig], out uint dot)) rec.OriginalTableData = dot;
            if (cDataEdit >= 0 && TryHex(grid[r, cDataEdit], out uint de)) rec.EditedTableData = de;
            // Entry counts are written in hex here (E2, BF, 157, ...).
            if (cCount >= 0 && TryHex(grid[r, cCount], out uint n) && n <= 0xFFFF) rec.OriginalEntryCount = (int)n;
            if (cCountEdit >= 0 && TryHex(grid[r, cCountEdit], out uint ne) && ne <= 0xFFFF) rec.EditedEntryCount = (int)ne;

            if (rec.CodeLocation == 0 && rec.OriginalTableData == 0 && rec.OriginalEntryCount < 0) continue;
            sheet.Tables.Add(rec);
        }
    }

    private static void ExtractFreeSpace(ResearchGrid grid, int headerRow, ResearchSheet sheet)
    {
        var h = grid.HeaderMap(headerRow);
        int cOffset = ResearchGrid.ColumnOf(h, "Offset");
        int cLength = ResearchGrid.ColumnOf(h, "Length");
        int cRoom = ResearchGrid.ColumnOf(h, "Room");
        int cFunction = ResearchGrid.ColumnOf(h, "Function");
        int cDetails = ResearchGrid.ColumnOf(h, "Details");

        for (int r = headerRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;
            if (!TryHex(grid[r, cOffset], out uint offset)) continue;
            if (offset == 0 || offset > MaxPlausibleOffset) continue;

            var rec = new ResearchFreeSpace
            {
                Offset = offset,
                Function = Get(grid, r, cFunction),
                Details = Get(grid, r, cDetails),
                Target = sheet.Target,
                Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, r),
            };
            if (TryHex(grid[r, cLength], out uint len)) rec.Length = (int)len;
            if (TryHex(grid[r, cRoom], out uint room) && room <= MaxPlausibleOffset) rec.Room = (int)room;
            sheet.FreeSpace.Add(rec);
        }
    }

    /// <summary>
    /// Reads a master index sheet ("Move Index", "Ability Index", ...) entry by entry.
    /// <para>
    /// The id sequence captured here is what lets the table be found in an arbitrary ROM later:
    /// handler pointers are relocated and so differ per build, but the ids are part of the game's
    /// data and stay put.
    /// </para>
    /// </summary>
    private static void ExtractMechanicIndex(ResearchGrid grid, int headerRow, ResearchSheet sheet)
    {
        var h = grid.HeaderMap(headerRow);
        int cFile = ResearchGrid.ColumnOf(h, "Offset (file)");
        int cRef = ResearchGrid.ColumnOf(h, "Reference");
        int cCodeFile = ResearchGrid.ColumnOf(h, "Code (File)");
        // The name column is the header cell that isn't one of the known structural ones -
        // it's literally titled "Move", "Ability", "Item", etc.
        int cName = -1;
        foreach (var kv in h)
        {
            if (kv.Key.Contains("Offset", StringComparison.OrdinalIgnoreCase)) continue;
            if (kv.Key.Contains("Data", StringComparison.OrdinalIgnoreCase)) continue;
            if (kv.Key.Contains("Reference", StringComparison.OrdinalIgnoreCase)) continue;
            if (kv.Key.Contains("Decimal", StringComparison.OrdinalIgnoreCase)) continue;
            if (kv.Key.Contains("Code", StringComparison.OrdinalIgnoreCase)) continue;
            if (kv.Key.Contains("File", StringComparison.OrdinalIgnoreCase)) continue;
            cName = kv.Value; break;
        }

        if (cRef < 0) { sheet.Diagnostics.Add("mechanic index without a Reference column"); return; }

        var index = new ResearchMechanicIndex
        {
            Name = sheet.SheetName,
            Kind = KindFromSheetName(sheet.SheetName),
            Target = sheet.Target,
            Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, headerRow),
        };

        for (int r = headerRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;
            if (!TryHex(grid[r, cRef], out uint id)) continue;

            var e = new ResearchMechanicEntry { Id = id, Name = Get(grid, r, cName) };
            if (cFile >= 0 && TryHex(grid[r, cFile], out uint ef)) e.EntryFileOffset = ef;
            if (cCodeFile >= 0 && TryHex(grid[r, cCodeFile], out uint hf)) e.HandlerFileOffset = hf;

            if (index.DocumentedTableOffset == 0 && e.EntryFileOffset != 0)
                index.DocumentedTableOffset = e.EntryFileOffset;

            index.Entries.Add(e);
        }

        if (index.Entries.Count > 0) sheet.MechanicIndexes.Add(index);
    }

    private static CustomMechanicKind? KindFromSheetName(string name)
    {
        if (name.Contains("Move", StringComparison.OrdinalIgnoreCase)) return CustomMechanicKind.Move;
        if (name.Contains("Abilit", StringComparison.OrdinalIgnoreCase)) return CustomMechanicKind.Ability;
        if (name.Contains("Item", StringComparison.OrdinalIgnoreCase)) return CustomMechanicKind.Item;
        return null;
    }

    private static void ExtractTimings(ResearchGrid grid, int headerRow, ResearchSheet sheet)
    {
        var h = grid.HeaderMap(headerRow);
        int cMeaning = ResearchGrid.ColumnOf(h, "Byte stored at return");
        int cExamples = ResearchGrid.ColumnOf(h, "Examples");
        int cValue = 0;
        // The value column is whichever one holds short hex byte values; default to the first.
        for (int c = 0; c < grid.ColumnCount; c++)
        {
            string v = grid[headerRow + 1, c];
            if (v.Length is 1 or 2 && TryHex(v, out _)) { cValue = c; break; }
        }

        for (int r = headerRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;
            if (!TryHex(grid[r, cValue], out uint val) || val > 0xFF) continue;
            string meaning = Get(grid, r, cMeaning);
            if (string.IsNullOrWhiteSpace(meaning)) continue;

            sheet.Timings.Add(new ResearchTiming
            {
                Value = (byte)val,
                Meaning = meaning,
                Examples = Get(grid, r, cExamples),
                Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, r),
            });
        }
    }

    /// <summary>
    /// Extracts a patch sheet as a sequence of <em>sections</em>.
    /// <para>
    /// These sheets aren't flat lists: a text-only row acts as a banner ("Hidden Power 0x2D",
    /// "Call Function") and the address/hex rows beneath it belong to that banner. Crucially, a
    /// section whose addresses restart at 0 and step by 4 is not a patch at all — it's a complete
    /// relocatable routine the researcher wrote for placement at an address chosen later. Reading
    /// row-at-a-time (as before) both lost the grouping and threw those routines away as
    /// "offset 0 is out of range", discarding the most directly reusable material in the corpus.
    /// </para>
    /// </summary>
    private static void ExtractPatches(ResearchGrid grid, int headerRow, ResearchSheet sheet, string version)
    {
        int cOffset, cHex, cAsm, cNote;

        if (headerRow >= 0)
        {
            var h = grid.HeaderMap(headerRow);

            bool um = version.Equals("UM", StringComparison.OrdinalIgnoreCase);
            bool us = version.Equals("US", StringComparison.OrdinalIgnoreCase);
            string verOffset = um ? "Offset UM" : us ? "Offset US" : null;
            string verAddress = um ? "Address UM" : us ? "Address US" : null;
            string verHex = um ? "Hex UM" : us ? "Hex US" : null;

            cOffset = verOffset != null ? ResearchGrid.ColumnOf(h, verOffset) : -1;
            if (cOffset < 0 && verAddress != null) cOffset = ResearchGrid.ColumnOf(h, verAddress);
            if (cOffset < 0) cOffset = ResearchGrid.ColumnOf(h, "in-file", "Address File", "Offset", "Address");

            cHex = verHex != null ? ResearchGrid.ColumnOf(h, verHex) : -1;
            if (cHex < 0) cHex = ResearchGrid.ColumnOf(h, "Hex");

            cAsm = ResearchGrid.ColumnOf(h, "Replacing", "instruction", "Assembly", "ARM");
            cNote = ResearchGrid.ColumnOf(h, "Note", "Details", "Comment");
        }
        else
        {
            // Headerless positional layout: address | hex | instruction | notes
            cOffset = 0; cHex = 1; cAsm = 2; cNote = 3;
        }

        if (cOffset < 0) { sheet.Diagnostics.Add("patch list without a usable offset column"); return; }
        if (cHex < 0 && cAsm < 0) { sheet.Diagnostics.Add("patch list without hex or assembly column"); return; }

        bool targetIsCode = sheet.Target == ResearchTarget.CodeBin;
        int rejectedRange = 0, rejectedEmpty = 0;

        string sectionName = null;
        var section = new List<(uint Offset, byte[] Bytes, string Asm, int Row)>();

        void FlushSection()
        {
            if (section.Count > 0) EmitSection(sheet, sectionName, section, rejectAbsolute: false);
            section.Clear();
        }

        for (int r = headerRow + 1; r < grid.RowCount; r++)
        {
            if (grid.IsRowEmpty(r)) continue;

            string offsetCell = Get(grid, r, cOffset);
            string hexCell = Get(grid, r, cHex).Replace("0x", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace("-", "");
            string asmCell = Get(grid, r, cAsm);

            bool haveOffset = TryHex(offsetCell, out uint offset);

            // A row with text but no parseable address starts a new section.
            if (!haveOffset)
            {
                string banner = offsetCell;
                if (string.IsNullOrWhiteSpace(banner))
                    banner = Enumerable.Range(0, grid.ColumnCount)
                        .Select(c => grid[r, c]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
                if (!string.IsNullOrWhiteSpace(banner))
                {
                    FlushSection();
                    sectionName = banner.Trim();
                }
                continue;
            }

            if (hexCell.Length == 8 && hexCell.All(Uri.IsHexDigit) &&
                uint.TryParse(hexCell, System.Globalization.NumberStyles.HexNumber, null, out uint asAddr) &&
                (asAddr == offset + 0x0010_0000u || asAddr == offset))
            {
                string swap = (asmCell ?? "").Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
                if (swap.Length == 8 && swap.All(Uri.IsHexDigit))
                {
                    hexCell = swap;
                    asmCell = "";
                }
            }

            byte[] bytes = null;
            if (hexCell.Length > 0 && hexCell.Length % 2 == 0 && hexCell.All(Uri.IsHexDigit))
            {
                try { bytes = Convert.FromHexString(hexCell); } catch { bytes = null; }
            }
            if ((bytes == null || bytes.Length == 0) && string.IsNullOrWhiteSpace(asmCell)) { rejectedEmpty++; continue; }
            if (bytes != null && bytes.Length == 4 && bytes.All(b => b == 0xCC)) continue; // padding trap

            section.Add((offset, bytes ?? [], asmCell, r));
        }
        FlushSection();

        // Re-validate absolute patches now that sections are resolved.
        var kept = new List<ResearchPatch>();
        foreach (var p in sheet.Patches)
        {
            uint off = p.Offset;
            if (targetIsCode && off >= 0x0010_0000u) off -= 0x0010_0000u;
            if (off == 0 || off > MaxPlausibleOffset) { rejectedRange++; continue; }
            p.Offset = off;
            kept.Add(p);
        }
        sheet.Patches.Clear();
        sheet.Patches.AddRange(kept);

        if (rejectedRange > 0)
            sheet.Diagnostics.Add($"{rejectedRange} patch row(s) rejected: offset outside 0x1..0x{MaxPlausibleOffset:X}");
        if (rejectedEmpty > 0)
            sheet.Diagnostics.Add($"{rejectedEmpty} row(s) rejected: no hex payload and no assembly");
    }

    /// <summary>
    /// Decides whether a collected section is a relocatable routine or a set of absolute patches,
    /// and files it accordingly.
    /// </summary>
    private static void EmitSection(
        ResearchSheet sheet, string name,
        List<(uint Offset, byte[] Bytes, string Asm, int Row)> rows,
        bool rejectAbsolute)
    {
        var origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, rows[0].Row);

        // Relocatable body: starts at 0 and every entry is contiguous with the previous one.
        bool relocatable = rows[0].Offset == 0 && rows.Count > 1;
        if (relocatable)
        {
            uint expected = 0;
            foreach (var (off, bytes, _, _) in rows)
            {
                if (off != expected) { relocatable = false; break; }
                expected += (uint)Math.Max(bytes.Length, 4);
            }
        }

        if (relocatable && rows.All(x => x.Bytes.Length > 0))
        {
            var body = new ResearchFunctionBody
            {
                Name = string.IsNullOrWhiteSpace(name) ? sheet.DisplayName : name,
                Code = rows.SelectMany(x => x.Bytes).ToArray(),
                Target = sheet.Target,
                Origin = origin,
            };
            foreach (var (_, _, asm, _) in rows)
                body.Assembly.Add(asm ?? "");
            sheet.Bodies.Add(body);
            return;
        }

        foreach (var (off, bytes, asm, row) in rows)
        {
            sheet.Patches.Add(new ResearchPatch
            {
                Offset = off,
                Bytes = bytes,
                Assembly = asm,
                Note = name,
                Origin = new ResearchOrigin(sheet.SourceFile, sheet.SheetName, row),
            });
        }
    }

    #endregion

    #region Cell helpers

    private static string Get(ResearchGrid g, int row, int col) => col < 0 ? "" : g[row, col].Trim();

    /// <summary>
    /// Parses a hex address cell. Deliberately strict: the cell must be pure hex digits (after
    /// stripping an optional 0x and separators). The old parser used <c>uint.TryParse(..., HexNumber)</c>
    /// on loosely-trimmed text, which is what let stray values through as multi-hundred-megabyte
    /// offsets, and threw on cells like "n/a" — taking the whole workbook down with it.
    /// </summary>
    public static bool TryHex(string s, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        string t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        t = t.Replace(" ", "").Replace("-", "").Replace("_", "");
        if (t.Length == 0 || t.Length > 8) return false;
        if (!t.All(Uri.IsHexDigit)) return false;
        return uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryInt(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    #endregion
}
