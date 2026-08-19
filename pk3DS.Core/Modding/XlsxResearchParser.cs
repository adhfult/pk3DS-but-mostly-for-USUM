using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace pk3DS.Core.Modding
{
    public static class XlsxResearchParser
    {
        public static List<string> GetSheetNames(string xlsxPath)
        {
            var names = new List<string>();
            if (!File.Exists(xlsxPath)) return names;
            try
            {
                using (var zip = ZipFile.OpenRead(xlsxPath))
                {
                    var workbookEntry = zip.GetEntry("xl/workbook.xml");
                    if (workbookEntry == null) return names;
                    using (var s = workbookEntry.Open())
                    {
                        var doc = new XmlDocument();
                        doc.Load(s);
                        var sheets = doc.GetElementsByTagName("sheet");
                        foreach (XmlNode sheet in sheets)
                        {
                            var name = sheet.Attributes["name"]?.Value;
                            if (name != null) names.Add(name);
                        }
                    }
                }
            }
            catch { }
            return names;
        }

        public static List<Dictionary<string, string>> ReadSheet(string xlsxPath, string sheetName = "Sheet1")
        {
            var results = new List<Dictionary<string, string>>();
            if (!File.Exists(xlsxPath)) return results;

            try
            {
                using (var zip = ZipFile.OpenRead(xlsxPath))
                {
                    // 1. Load Shared Strings
                    var sharedStrings = new List<string>();
                    var stringsEntry = zip.GetEntry("xl/sharedStrings.xml");
                    if (stringsEntry != null)
                    {
                        using (var s = stringsEntry.Open())
                        {
                            var doc = new XmlDocument();
                            doc.Load(s);
                            var tNodes = doc.GetElementsByTagName("t");
                            foreach (XmlNode node in tNodes) sharedStrings.Add(node.InnerText);
                        }
                    }

                    // 2. Locate Sheet by Name
                    string targetRelId = null;
                    var workbookEntry = zip.GetEntry("xl/workbook.xml");
                    if (workbookEntry != null)
                    {
                        using (var s = workbookEntry.Open())
                        {
                            var doc = new XmlDocument();
                            doc.Load(s);
                            var sheets = doc.GetElementsByTagName("sheet");
                            foreach (XmlNode sheet in sheets)
                            {
                                if (sheet.Attributes["name"]?.Value == sheetName)
                                {
                                    targetRelId = sheet.Attributes["r:id"]?.Value;
                                    break;
                                }
                            }
                        }
                    }

                    string sheetPath = "xl/worksheets/sheet1.xml"; // Fallback
                    if (targetRelId != null)
                    {
                        var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
                        if (relsEntry != null)
                        {
                            using (var s = relsEntry.Open())
                            {
                                var doc = new XmlDocument();
                                doc.Load(s);
                                var rels = doc.GetElementsByTagName("Relationship");
                                foreach (XmlNode rel in rels)
                                {
                                    if (rel.Attributes["Id"]?.Value == targetRelId)
                                    {
                                        string targetPath = rel.Attributes["Target"]?.Value ?? "";
                                        targetPath = targetPath.TrimStart('/');
                                        if (targetPath.StartsWith("xl/"))
                                            sheetPath = targetPath;
                                        else
                                            sheetPath = "xl/" + targetPath;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    var sheetEntry = zip.GetEntry(sheetPath);
                    if (sheetEntry == null) return results;

                    using (var s = sheetEntry.Open())
                    {
                        var doc = new XmlDocument();
                        doc.Load(s);
                        var rows = doc.GetElementsByTagName("row");
                        
                        var headers = new List<string>();
                        bool firstRow = true;

                        foreach (XmlNode row in rows)
                        {
                            var cells = row.SelectNodes("*[local-name()='c']");
                            var rowData = new Dictionary<string, string>();
                            
                            foreach (XmlNode cell in cells)
                            {
                                string cellRef = cell.Attributes["r"]?.Value;
                                if (string.IsNullOrEmpty(cellRef)) continue;
                                
                                int colIndex = GetColumnIndex(cellRef);
                                string cellValue = GetCellValue(cell, sharedStrings);
                                
                                if (firstRow)
                                {
                                    while (headers.Count <= colIndex) headers.Add($"Col{headers.Count}");
                                    headers[colIndex] = cellValue;
                                }
                                else
                                {
                                    string header = colIndex < headers.Count ? headers[colIndex] : $"Col{colIndex}";
                                    rowData[header] = cellValue;
                                }
                            }

                            if (!firstRow && rowData.Count > 0) results.Add(rowData);
                            firstRow = false;
                        }
                    }
                }
            }
            catch { /* Parsing error */ }
            return results;
        }

        private static int GetColumnIndex(string cellRef)
        {
            string colLetters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
            int index = 0;
            foreach (char c in colLetters) index = index * 26 + (c - 'A' + 1);
            return index - 1;
        }

        private static string GetCellValue(XmlNode cell, List<string> sharedStrings)
        {
            var tAttr = cell.Attributes["t"];
            if (tAttr != null && tAttr.Value == "inlineStr")
            {
                var tNode = cell.SelectSingleNode("*[local-name()='is']/*[local-name()='t']");
                return tNode?.InnerText ?? "";
            }

            var vNode = cell.SelectSingleNode("*[local-name()='v']");
            if (vNode == null) return "";

            string val = vNode.InnerText;
            if (tAttr != null && tAttr.Value == "s") // Shared String
            {
                if (int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                    return sharedStrings[idx];
            }
            return val;
        }

        /// <summary>
        /// Extracts patch entries (offset, bytes) from sheet rows.
        /// Dynamically assembles ARM instructions using Keystone if raw Hex is missing or empty.
        /// Supports configurable custom Item ID override.
        /// </summary>
        public static List<(uint Offset, byte[] Bytes)> ExtractPatchEntries(List<Dictionary<string, string>> rows, string target = "Battle.cro", string version = "US", uint overrideItemId = 0)
        {
            var entries = new List<(uint Offset, byte[] Bytes)>();
            if (rows == null || rows.Count == 0) return entries;

            bool inTrampolineSection = false;
            bool targetIsCro = target.EndsWith(".cro", StringComparison.OrdinalIgnoreCase);
            bool targetIsCode = target.ToLower().Contains("code");

            foreach (var r in rows)
            {
                string firstVal = r.Values.FirstOrDefault()?.Trim() ?? "";
                string hexKey0 = r.Keys.FirstOrDefault(k => k.IndexOf("Hex", StringComparison.OrdinalIgnoreCase) >= 0);
                string hexVal0 = hexKey0 != null && r.ContainsKey(hexKey0) ? r[hexKey0]?.Trim() : "";
                bool isHeaderRow = !string.IsNullOrEmpty(firstVal) && string.IsNullOrEmpty(hexVal0) && firstVal.Length > 8;

                if (isHeaderRow)
                {
                    string headerLower = firstVal.ToLower();
                    if (headerLower.Contains("trampoline") || headerLower.Contains("code.bin"))
                        inTrampolineSection = true;
                    else
                        inTrampolineSection = false;
                    continue;
                }

                if (inTrampolineSection && targetIsCro) continue;

                // Priority for version-specific offset column if present
                string verOffKey = version.Equals("UM", StringComparison.OrdinalIgnoreCase)
                    ? r.Keys.FirstOrDefault(k => k.IndexOf("Address UM", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("Offset UM", StringComparison.OrdinalIgnoreCase) >= 0)
                    : r.Keys.FirstOrDefault(k => k.IndexOf("Address US", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("Offset US", StringComparison.OrdinalIgnoreCase) >= 0);

                string offKey = verOffKey
                             ?? r.Keys.FirstOrDefault(k => k.IndexOf("in-file", StringComparison.OrdinalIgnoreCase) >= 0)
                             ?? r.Keys.FirstOrDefault(k => k.IndexOf("Address File", StringComparison.OrdinalIgnoreCase) >= 0)
                             ?? r.Keys.FirstOrDefault(k => k.IndexOf("Offset", StringComparison.OrdinalIgnoreCase) >= 0)
                             ?? r.Keys.FirstOrDefault(k => k.IndexOf("Address", StringComparison.OrdinalIgnoreCase) >= 0);

                if (offKey == null || !r.ContainsKey(offKey)) continue;

                string offStr = r[offKey].Replace("0x", "").Replace("0X", "").Trim();
                if (string.IsNullOrEmpty(offStr) || !uint.TryParse(offStr, System.Globalization.NumberStyles.HexNumber, null, out uint off)) continue;

                if (targetIsCro && r.Values.Any(v => v != null && v.IndexOf("trampoline", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                bool isAddressColumn = offKey.IndexOf("Address", StringComparison.OrdinalIgnoreCase) >= 0
                                    && offKey.IndexOf("in-file", StringComparison.OrdinalIgnoreCase) < 0
                                    && offKey.IndexOf("File", StringComparison.OrdinalIgnoreCase) < 0;

                if (isAddressColumn && targetIsCode && off >= 0x100000u)
                    off -= 0x100000u;

                // Priority for version-specific Hex column
                string verHexKey = version.Equals("UM", StringComparison.OrdinalIgnoreCase)
                    ? r.Keys.FirstOrDefault(k => k.Equals("Hex UM", StringComparison.OrdinalIgnoreCase))
                    : r.Keys.FirstOrDefault(k => k.Equals("Hex US", StringComparison.OrdinalIgnoreCase));

                string hexKey = verHexKey
                             ?? r.Keys.FirstOrDefault(k => k.IndexOf("Hex", StringComparison.OrdinalIgnoreCase) >= 0);

                byte[] patchBytes = null;

                if (hexKey != null && r.ContainsKey(hexKey) && !string.IsNullOrWhiteSpace(r[hexKey]))
                {
                    string hexStr = r[hexKey].Trim().Replace("0x", "").Replace("0X", "");
                    patchBytes = Util.StringToByteArray(hexStr);

                    // If custom Item ID override is active, patch 16-bit item placeholders
                    if (overrideItemId > 0 && patchBytes != null && patchBytes.Length == 2)
                    {
                        ushort val = BitConverter.ToUInt16(patchBytes, 0);
                        if (val == 0xFEED || val == 0xFFFE || val > 1000)
                        {
                            patchBytes = BitConverter.GetBytes((ushort)overrideItemId);
                        }
                    }
                }

                // Dynamic Assembly Fallback if Hex is missing/empty
                if (patchBytes == null || patchBytes.Length == 0)
                {
                    string asmKey = r.Keys.FirstOrDefault(k => k.Equals("Replacing", StringComparison.OrdinalIgnoreCase))
                                 ?? r.Keys.FirstOrDefault(k => k.IndexOf("instruction", StringComparison.OrdinalIgnoreCase) >= 0)
                                 ?? r.Keys.FirstOrDefault(k => k.IndexOf("Assembly", StringComparison.OrdinalIgnoreCase) >= 0)
                                 ?? r.Keys.FirstOrDefault(k => k.Equals("ARM", StringComparison.OrdinalIgnoreCase));

                    if (asmKey != null && r.ContainsKey(asmKey) && !string.IsNullOrWhiteSpace(r[asmKey]))
                    {
                        string asmText = r[asmKey].Trim();

                        // Replace item ID placeholder in assembly text if custom Item ID is provided
                        if (overrideItemId > 0)
                        {
                            asmText = asmText.Replace("{ITEM_ID}", $"0x{overrideItemId:X}")
                                             .Replace("ITEM_ID", $"0x{overrideItemId:X}");
                        }

                        // Assemble via Keystone
                        patchBytes = ResearchEngine.AssembleARM(asmText, off);
                    }
                }

                if (patchBytes == null || patchBytes.Length == 0) continue;

                // Skip padding trap marker (0xCCCCCCCC)
                if (patchBytes.Length == 4 && patchBytes[0] == 0xCC && patchBytes[1] == 0xCC && patchBytes[2] == 0xCC && patchBytes[3] == 0xCC)
                    continue;

                entries.Add((off, patchBytes));
            }

            return entries;
        }
    }
}
