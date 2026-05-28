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
    }
}
