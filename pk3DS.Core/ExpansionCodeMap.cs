// The project is not nullable-annotated, but the parsing here turns "row may be prose" into
// "may be null" often enough that the compiler checking it is worth the local opt-in.
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace pk3DS.Core
{
    /// <summary>
    /// One documented difference between a retail US/UM file and the Expansion Pack's version of it.
    /// </summary>
    public class CodeMapEntry
    {
        public string File { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Offset { get; set; } = string.Empty;
        public string RetailBytes { get; set; } = string.Empty;
        public string CurrentBytes { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;

        /// <summary>The file this row targets, without the game suffix - e.g. "Battle.cro".</summary>
        public string TargetFile
        {
            get
            {
                int paren = File.IndexOf('(');
                return (paren < 0 ? File : File[..paren]).Trim();
            }
        }

        public bool ForUltraSun => File.Contains("Ultra Sun", StringComparison.OrdinalIgnoreCase);
        public bool ForUltraMoon => File.Contains("Ultra Moon", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when retail had nothing here and the pack added code. Such a row has new bytes to
        /// verify but no original pattern to restore, so "revert" is not meaningful for it.
        /// </summary>
        public bool IsAddedSpace =>
            RetailBytes.Trim().Equals("ADDED SPACE", StringComparison.OrdinalIgnoreCase);

        /// <summary>False when the Offset column is prose rather than an address.</summary>
        public bool HasAddress => TryParseRange(Offset, out _, out _);

        /// <summary>First byte this row covers. Check <see cref="HasAddress"/> first - 0 is a real address.</summary>
        public uint StartOffset => TryParseRange(Offset, out uint start, out _) ? start : 0;

        /// <summary>Last byte this row covers, inclusive. Equals <see cref="StartOffset"/> for a bare address.</summary>
        public uint EndOffset => TryParseRange(Offset, out uint start, out uint end) ? end : 0;

        /// <summary>Byte span the Offset column describes, or 0 when it is prose.</summary>
        public uint Length => TryParseRange(Offset, out uint start, out uint end) ? end - start + 1 : 0;

        public bool TryGetCurrentBytes([NotNullWhen(true)] out byte[]? bytes) => TryParseHex(CurrentBytes, out bytes);

        public bool TryGetRetailBytes([NotNullWhen(true)] out byte[]? bytes) =>
            IsAddedSpace ? Fail(out bytes) : TryParseHex(RetailBytes, out bytes);

        private static bool Fail(out byte[]? bytes) { bytes = null; return false; }

        /// <summary>The pack's bytes for this row, or empty when the column is not hex.</summary>
        public byte[] GetCurrentByteArray() => TryGetCurrentBytes(out var b) ? b : Array.Empty<byte>();

        /// <summary>The retail bytes for this row, or empty when the region was added space.</summary>
        public byte[] GetRetailByteArray() => TryGetRetailBytes(out var b) ? b : Array.Empty<byte>();

        public bool Contains(uint address) =>
            TryParseRange(Offset, out uint start, out uint end) && address >= start && address <= end;

        /// <summary>
        /// Reads "0x1D4C2C", "0x1D4C2C-0x1D4C2F" or "0x0FD000 insertion". The range is inclusive of
        /// its end, matching how the map writes a 4-byte word as 0x...2C-0x...2F.
        /// </summary>
        private static bool TryParseRange(string text, out uint start, out uint end)
        {
            start = end = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (!TryParseAddress(parts[0], out start)) return false;
            end = parts.Length > 1 && TryParseAddress(parts[1], out uint parsedEnd) ? parsedEnd : start;
            if (end < start) end = start;
            return true;
        }

        /// <summary>Takes the leading hex address and ignores any trailing note such as "insertion".</summary>
        private static bool TryParseAddress(string text, out uint value)
        {
            value = 0;
            string s = text.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];

            int len = 0;
            while (len < s.Length && Uri.IsHexDigit(s[len])) len++;
            if (len == 0) return false;

            // Anything after the digits must be a separate word ("0x0FD000 insertion"), never a
            // digit run cut short - "2,001 offset fields" must not read as address 2.
            if (len < s.Length && s[len] != ' ') return false;

            return uint.TryParse(s[..len], System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        /// <summary>
        /// Strict hex-pair parse. Util.StringToByteArray strips spaces before checking length, which
        /// turns "ADDED SPACE" into a 10-character string that passes its even-length guard and then
        /// throws on the pair "DS". Validating the characters first is what keeps prose rows inert.
        /// </summary>
        private static bool TryParseHex(string text, [NotNullWhen(true)] out byte[]? bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string h = text.Replace(" ", "").Replace("-", "").Replace("\n", "").Replace("\r", "").Trim();
            if (h.Length == 0 || h.Length % 2 != 0) return false;
            foreach (char c in h)
            {
                if (!Uri.IsHexDigit(c)) return false;
            }

            var result = new byte[h.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            bytes = result;
            return true;
        }

        public override string ToString() =>
            $"{TargetFile} {Offset} [{Section}] {Purpose}";
    }

    /// <summary>
    /// The Expansion Pack's code map for Ultra Sun / Ultra Moon: every offset the pack changed,
    /// what was there in retail, what is there now, and why.
    /// </summary>
    public static class ExpansionCodeMap
    {
        private static readonly List<CodeMapEntry> _entries = new();
        private static bool _isLoaded;
        private static readonly object _lock = new();

        public static IReadOnlyList<CodeMapEntry> Entries
        {
            get
            {
                EnsureLoaded();
                return _entries.AsReadOnly();
            }
        }

        /// <summary>True when the map was found and parsed. False means every query returns nothing.</summary>
        public static bool IsAvailable
        {
            get { EnsureLoaded(); return _entries.Count > 0; }
        }

        public static void EnsureLoaded()
        {
            if (_isLoaded) return;
            lock (_lock)
            {
                if (_isLoaded) return;
                LoadEntries();
                _isLoaded = true;
            }
        }

        private static void LoadEntries()
        {
            _entries.Clear();
            string? csvContent = ReadFromAssembly(Assembly.GetExecutingAssembly())
                              ?? ReadFromAssembly(Assembly.GetEntryAssembly());

            if (string.IsNullOrEmpty(csvContent))
            {
                foreach (string candidate in EnumerateDiskCandidates())
                {
                    try
                    {
                        if (System.IO.File.Exists(candidate))
                        {
                            csvContent = System.IO.File.ReadAllText(candidate);
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (!string.IsNullOrEmpty(csvContent))
                ParseCsv(csvContent);
        }

        private static string? ReadFromAssembly(Assembly? assembly)
        {
            if (assembly == null) return null;
            try
            {
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("code_map.csv", StringComparison.OrdinalIgnoreCase));
                if (resourceName == null) return null;

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return null;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch { return null; }
        }

        // The working directory is not the install directory when the editor is launched from a
        // shortcut, so a bare relative name alone would miss the file that ships beside the exe.
        private static IEnumerable<string> EnumerateDiskCandidates()
        {
            yield return "code_map.csv";
            string? baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
                yield return Path.Combine(baseDir, "code_map.csv");
        }

        private static void ParseCsv(string content)
        {
            using var reader = new StringReader(content);
            string? line;
            bool isHeader = true;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = ParseCsvLine(line);
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }
                if (cols.Count >= 7)
                {
                    _entries.Add(new CodeMapEntry
                    {
                        File = cols[0],
                        Section = cols[1],
                        Offset = cols[2],
                        RetailBytes = cols[3],
                        CurrentBytes = cols[4],
                        Instruction = cols[5],
                        Purpose = cols[6]
                    });
                }
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result;
        }

        /// <summary>
        /// Rows for a file. <paramref name="version"/> selects between the separate US and UM
        /// code.bin maps; pass <see cref="GameVersion.USUM"/> or Invalid to accept either.
        /// </summary>
        public static IEnumerable<CodeMapEntry> GetEntriesForFile(string targetFileName, GameVersion version = GameVersion.Invalid)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(targetFileName)) return Enumerable.Empty<CodeMapEntry>();

            // Match on the bare name so callers may pass "Battle.cro" or a full path.
            string name = Path.GetFileName(targetFileName);
            if (string.IsNullOrEmpty(name)) name = targetFileName;

            return _entries.Where(e => e.TargetFile.Equals(name, StringComparison.OrdinalIgnoreCase)
                                    && MatchesVersion(e, version));
        }

        private static bool MatchesVersion(CodeMapEntry entry, GameVersion version) => version switch
        {
            GameVersion.US => entry.ForUltraSun,
            GameVersion.UM => entry.ForUltraMoon,
            _ => true,
        };

        public static IEnumerable<CodeMapEntry> GetEntriesForSection(string sectionName)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(sectionName)) return Enumerable.Empty<CodeMapEntry>();
            return _entries.Where(e => e.Section.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Every section name present for a file, in map order.</summary>
        public static IEnumerable<string> GetSections(string targetFileName, GameVersion version = GameVersion.Invalid) =>
            GetEntriesForFile(targetFileName, version).Select(e => e.Section).Distinct(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The rows covering an address, innermost first. This is the lookup an editor wants: point
        /// at an offset and find out what the pack put there and why.
        /// </summary>
        public static IEnumerable<CodeMapEntry> GetEntriesAt(string targetFileName, uint address, GameVersion version = GameVersion.Invalid) =>
            GetEntriesForFile(targetFileName, version).Where(e => e.Contains(address)).OrderBy(e => e.Length);

        /// <summary>One-line description of what lives at an address, or null when it is unmapped.</summary>
        public static string? Describe(string targetFileName, uint address, GameVersion version = GameVersion.Invalid)
        {
            var entry = GetEntriesAt(targetFileName, address, version).FirstOrDefault();
            if (entry == null) return null;
            return string.IsNullOrWhiteSpace(entry.Purpose)
                ? $"[{entry.Section}] {entry.Offset}"
                : $"[{entry.Section}] {entry.Purpose}";
        }

        /// <summary>
        /// Whether a loaded file already carries the Expansion Pack's changes, by sampling rows that
        /// have both a real address and real bytes.
        /// </summary>
        public static bool IsExpansionPatched(byte[] fileData, string targetFileName, GameVersion version = GameVersion.Invalid)
        {
            EnsureLoaded();
            if (fileData == null || fileData.Length == 0) return false;

            var candidates = GetEntriesForFile(targetFileName, version)
                .Where(e => e.HasAddress && e.TryGetCurrentBytes(out var b) && b.Length > 0
                            && e.StartOffset + (uint)b.Length <= (uint)fileData.Length)
                .ToList();
            if (candidates.Count == 0) return false;

            const int sampleSize = 24;
            int step = Math.Max(1, candidates.Count / sampleSize);
            int tested = 0, matched = 0;
            for (int i = 0; i < candidates.Count && tested < sampleSize; i += step)
            {
                var entry = candidates[i];
                if (!entry.TryGetCurrentBytes(out var expected)) continue;
                tested++;
                if (RegionEquals(fileData, entry.StartOffset, expected)) matched++;
            }

            return tested > 0 && matched * 1.0 / tested >= 0.5;
        }

        private static bool RegionEquals(byte[] data, uint offset, byte[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (data[offset + i] != expected[i]) return false;
            }
            return true;
        }
    }
}
