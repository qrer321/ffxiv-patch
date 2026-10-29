using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Korean text the Korean client does not have, kept as one TSV per sheet in Supplements/ and
    // embedded as "Supplements/<sheet>.tsv" ('/' in a sheet path becomes '_'). Columns:
    //   row  col  ja  en  ko  source  note
    // `col` is the string column offset; `ja`/`en` are the target texts the entry was translated from.
    // In the string columns a sheet has entries for, a cell the Korean client leaves without Hangul
    // keeps the target text (the Korean client's untranslated text is never written), and an entry
    // replaces it only while the clean target text equals the entry's text for the target language.
    // Official Korean text always wins; a changed target text keeps the global text.
    internal sealed class SupplementEntry
    {
        public uint RowId;
        public ushort ColumnOffset;
        public string Japanese;
        public string English;
        public string Korean;
        public string Source;

        public string TextFor(string targetLanguage)
        {
            if (string.Equals(targetLanguage, "ja", StringComparison.OrdinalIgnoreCase))
            {
                return Japanese;
            }

            return string.Equals(targetLanguage, "en", StringComparison.OrdinalIgnoreCase) ? English : string.Empty;
        }
    }

    internal enum SupplementIssueKind
    {
        // The Korean client now has Korean text for an entry; the entry can be removed.
        Official,
        // The target text differs from the entry's text for the target language.
        Stale,
        // The target shows text the Korean client has no Korean for, and no entry covers it.
        Missing,
    }

    internal sealed class SupplementIssue
    {
        public SupplementIssueKind Kind;
        public uint RowId;
        public ushort ColumnOffset;
        public string TargetText;
    }

    internal sealed class SupplementResult
    {
        public int Applied;
        public int Restored;
        public readonly List<SupplementIssue> Issues = new List<SupplementIssue>();

        public int Count(SupplementIssueKind kind)
        {
            int count = 0;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // The text a managed cell ends up with and what the sheet reports for it.
    internal sealed class SupplementCell
    {
        public byte[] Selected;
        public bool Applied;
        public bool HasIssue;
        public SupplementIssueKind Issue;

        public void SetIssue(bool hasIssue, SupplementIssueKind issue)
        {
            HasIssue = hasIssue;
            Issue = issue;
        }
    }

    internal sealed class SupplementSheet
    {
        public const string Header = "row\tcol\tja\ten\tko\tsource\tnote";

        private readonly Dictionary<ulong, SupplementEntry> _entries = new Dictionary<ulong, SupplementEntry>();
        private readonly HashSet<ushort> _columns = new HashSet<ushort>();
        private readonly HashSet<ulong> _visited = new HashSet<ulong>();

        public int Count
        {
            get { return _entries.Count; }
        }

        public IEnumerable<SupplementEntry> Entries
        {
            get { return _entries.Values; }
        }

        // checkEntry adds the rules of one sheet (see SupplementTranslations.Parse) and returns a
        // problem, or null for a valid entry.
        public static SupplementSheet Parse(TextReader reader, Func<SupplementEntry, string> checkEntry = null)
        {
            string header = reader.ReadLine();
            if (!string.Equals(header == null ? null : header.TrimStart('\uFEFF'), Header, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Supplement header must be: " + Header.Replace("\t", "<TAB>"));
            }

            SupplementSheet sheet = new SupplementSheet();
            int lineNumber = 1;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.Length == 0)
                {
                    continue;
                }

                string[] fields = line.Split('\t');
                if (fields.Length != 7)
                {
                    throw new InvalidDataException("Supplement line " + lineNumber + ": expected 7 tab-separated fields, got " + fields.Length + ".");
                }

                uint rowId;
                ushort columnOffset;
                if (!uint.TryParse(fields[0], out rowId) || !ushort.TryParse(fields[1], out columnOffset))
                {
                    throw new InvalidDataException("Supplement line " + lineNumber + ": row and col must be numbers.");
                }

                SupplementEntry entry = new SupplementEntry
                {
                    RowId = rowId,
                    ColumnOffset = columnOffset,
                    Japanese = fields[2],
                    English = fields[3],
                    Korean = fields[4],
                    Source = fields[5],
                };
                string problem = CheckEntry(entry) ?? (checkEntry == null ? null : checkEntry(entry));
                if (problem != null)
                {
                    throw new InvalidDataException("Supplement line " + lineNumber + ": " + problem);
                }

                ulong key = Key(rowId, columnOffset);
                if (sheet._entries.ContainsKey(key))
                {
                    throw new InvalidDataException("Supplement line " + lineNumber + ": duplicate row " + rowId + " col " + columnOffset + ".");
                }

                sheet._entries.Add(key, entry);
                sheet._columns.Add(columnOffset);
            }

            return sheet;
        }

        private static string CheckEntry(SupplementEntry entry)
        {
            if (entry.Korean.Length == 0 || (entry.Japanese.Length == 0 && entry.English.Length == 0))
            {
                return "ko and at least one of ja/en are required.";
            }

            if (!SupplementSources.IsKnown(entry.Source))
            {
                return "unknown source '" + entry.Source + "'.";
            }

            if (!HangulText.Contains(Encoding.UTF8.GetBytes(entry.Korean)))
            {
                return "ko has no Hangul: " + entry.Korean;
            }

            if (entry.Korean.Trim() != entry.Korean || entry.Korean.Contains("  "))
            {
                return "ko has stray spaces: '" + entry.Korean + "'";
            }

            return null;
        }

        public bool Manages(ushort columnOffset)
        {
            return _columns.Contains(columnOffset);
        }

        // Entries no applied page has reached: the row is not in the target sheet, or `col` is not one
        // of its string columns.
        public List<SupplementEntry> Unvisited()
        {
            List<SupplementEntry> entries = new List<SupplementEntry>();
            foreach (KeyValuePair<ulong, SupplementEntry> pair in _entries)
            {
                if (!_visited.Contains(pair.Key))
                {
                    entries.Add(pair.Value);
                }
            }

            entries.Sort(delegate(SupplementEntry left, SupplementEntry right)
            {
                return left.RowId != right.RowId
                    ? left.RowId.CompareTo(right.RowId)
                    : left.ColumnOffset.CompareTo(right.ColumnOffset);
            });
            return entries;
        }

        // Decides one managed cell. patched is the cell after the Korean patch, target the clean target
        // cell. Shared by Apply and the diagnostic CSV so both report the same text.
        public SupplementCell Resolve(uint rowId, ushort columnOffset, byte[] patched, byte[] target, string targetLanguage)
        {
            SupplementEntry entry;
            _entries.TryGetValue(Key(rowId, columnOffset), out entry);
            SupplementCell cell = new SupplementCell { Selected = target };
            if (HangulText.Contains(patched))
            {
                cell.Selected = patched;
                cell.SetIssue(entry != null, SupplementIssueKind.Official);
                return cell;
            }

            string expected = entry == null ? string.Empty : entry.TextFor(targetLanguage);
            if (target.Length == 0)
            {
                // The entry was written for text the target no longer has.
                cell.SetIssue(expected.Length > 0, SupplementIssueKind.Stale);
                return cell;
            }

            if (entry == null)
            {
                cell.SetIssue(true, SupplementIssueKind.Missing);
            }
            else if (string.Equals(expected, Encoding.UTF8.GetString(target), StringComparison.Ordinal))
            {
                cell.Selected = Encoding.UTF8.GetBytes(entry.Korean);
                cell.Applied = true;
            }
            else
            {
                cell.SetIssue(true, SupplementIssueKind.Stale);
            }

            return cell;
        }

        // Applies the sheet to a patched page. Only string columns that have entries are managed;
        // other columns (a reading, for example) keep the patched text. skipCell returns true for cells
        // the patch must leave as they are (base-language scope, patch-policy rows and columns).
        public byte[] Apply(
            ExcelHeader header,
            ExcelDataFile cleanTarget,
            byte[] patchedPage,
            string targetLanguage,
            Func<uint, ushort, bool> skipCell,
            out SupplementResult result)
        {
            result = new SupplementResult();
            ExcelDataFile page = ExcelDataFile.Parse(patchedPage);
            List<int> stringColumns = header.GetStringColumnIndexes();
            bool changed = false;
            for (int stringIndex = 0; stringIndex < stringColumns.Count; stringIndex++)
            {
                int columnIndex = stringColumns[stringIndex];
                ushort columnOffset = header.Columns[columnIndex].Offset;
                if (!_columns.Contains(columnOffset))
                {
                    continue;
                }

                Dictionary<uint, byte[]> replacements = new Dictionary<uint, byte[]>();
                for (int rowIndex = 0; rowIndex < page.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = page.Rows[rowIndex];
                    _visited.Add(Key(row.RowId, columnOffset));
                    if (skipCell != null && skipCell(row.RowId, columnOffset))
                    {
                        continue;
                    }

                    byte[] patched = page.GetStringBytes(row, header, columnIndex) ?? new byte[0];
                    byte[] target = cleanTarget.GetStringBytes(row.RowId, header, columnIndex) ?? new byte[0];
                    SupplementCell cell = Resolve(row.RowId, columnOffset, patched, target, targetLanguage);
                    if (cell.HasIssue)
                    {
                        result.Issues.Add(new SupplementIssue
                        {
                            Kind = cell.Issue,
                            RowId = row.RowId,
                            ColumnOffset = columnOffset,
                            TargetText = Encoding.UTF8.GetString(cell.Issue == SupplementIssueKind.Official ? patched : target),
                        });
                    }

                    if (cell.Applied)
                    {
                        result.Applied++;
                    }

                    if (BytesEqual(cell.Selected, patched))
                    {
                        continue;
                    }

                    if (!cell.Applied)
                    {
                        result.Restored++;
                    }

                    replacements[row.RowId] = cell.Selected;
                }

                if (replacements.Count > 0)
                {
                    page = ExcelDataFile.Parse(ExdRowRewriter.RewriteStringColumn(header, page, columnIndex, replacements));
                    changed = true;
                }
            }

            return changed ? page.Data : patchedPage;
        }

        private static ulong Key(uint rowId, ushort columnOffset)
        {
            return ((ulong)rowId << 16) | columnOffset;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    // Where an entry's Korean comes from; the note keeps the details (rows, sheets, URLs).
    internal static class SupplementSources
    {
        // kr-row:<row> copies a Korean-only row of the Korean client.
        public const string KoreanRowPrefix = "kr-row:";
        // Built from the Korean client's own terms in the same sheet.
        public const string KoreanTerms = "kr-term";

        // FFXIV KR terms of other sheets, FF7 Remake-series Korean releases, Korean community usage,
        // and the project's own translation.
        private static readonly string[] Others = { "ffxiv-kr", "ff7r-ko", "community", "project" };

        public static bool IsKnown(string source)
        {
            uint row;
            return IsKoreanClientWording(source) &&
                   (source == KoreanTerms || uint.TryParse(source.Substring(KoreanRowPrefix.Length), out row)) ||
                   Array.IndexOf(Others, source) >= 0;
        }

        public static bool IsKoreanClientWording(string source)
        {
            return source == KoreanTerms || source.StartsWith(KoreanRowPrefix, StringComparison.Ordinal);
        }
    }

    internal static class SupplementTranslations
    {
        public const string ResourcePrefix = "Supplements/";

        private static Dictionary<string, string> _resourceNames;

        public static string ResourceNameFor(string sheetName)
        {
            return ResourcePrefix + sheetName.Replace('/', '_') + ".tsv";
        }

        // Parses a supplement with the rules of its sheet.
        public static SupplementSheet Parse(string sheetName, TextReader reader)
        {
            return SupplementSheet.Parse(
                reader,
                TypedWordText.IsTypedWordSheet(sheetName) ? new Func<SupplementEntry, string>(TypedWordText.CheckWord) : null);
        }

        // Returns the embedded supplement of a sheet, or null when the sheet has none.
        public static SupplementSheet Load(string sheetName)
        {
            Assembly assembly = typeof(SupplementTranslations).Assembly;
            if (_resourceNames == null)
            {
                Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                    {
                        names[name] = name;
                    }
                }

                _resourceNames = names;
            }

            string resourceName;
            if (!_resourceNames.TryGetValue(ResourceNameFor(sheetName), out resourceName))
            {
                return null;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true)))
            {
                return Parse(sheetName, reader);
            }
        }
    }
}
