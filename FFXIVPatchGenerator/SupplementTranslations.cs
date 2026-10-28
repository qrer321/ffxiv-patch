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

    internal sealed class SupplementSheet
    {
        public const string Header = "row\tcol\tja\ten\tko\tsource\tnote";

        private readonly Dictionary<ulong, SupplementEntry> _entries = new Dictionary<ulong, SupplementEntry>();
        private readonly HashSet<ushort> _columns = new HashSet<ushort>();

        public int Count
        {
            get { return _entries.Count; }
        }

        public IEnumerable<SupplementEntry> Entries
        {
            get { return _entries.Values; }
        }

        public static SupplementSheet Parse(TextReader reader)
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
                if (entry.Korean.Length == 0 || (entry.Japanese.Length == 0 && entry.English.Length == 0))
                {
                    throw new InvalidDataException("Supplement line " + lineNumber + ": ko and at least one of ja/en are required.");
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
                    if (skipCell != null && skipCell(row.RowId, columnOffset))
                    {
                        continue;
                    }

                    SupplementEntry entry;
                    _entries.TryGetValue(Key(row.RowId, columnOffset), out entry);
                    byte[] patched = page.GetStringBytes(row, header, columnIndex) ?? new byte[0];
                    if (ContainsHangul(patched))
                    {
                        if (entry != null)
                        {
                            result.Issues.Add(Issue(SupplementIssueKind.Official, row.RowId, columnOffset, patched));
                        }

                        continue;
                    }

                    byte[] target = cleanTarget.GetStringBytes(row.RowId, header, columnIndex) ?? new byte[0];
                    byte[] selected = target;
                    if (target.Length > 0)
                    {
                        string targetText = Encoding.UTF8.GetString(target);
                        if (entry == null)
                        {
                            result.Issues.Add(Issue(SupplementIssueKind.Missing, row.RowId, columnOffset, target));
                        }
                        else if (string.Equals(entry.TextFor(targetLanguage), targetText, StringComparison.Ordinal))
                        {
                            selected = Encoding.UTF8.GetBytes(entry.Korean);
                            result.Applied++;
                        }
                        else
                        {
                            result.Issues.Add(Issue(SupplementIssueKind.Stale, row.RowId, columnOffset, target));
                        }
                    }

                    if (BytesEqual(selected, patched))
                    {
                        continue;
                    }

                    if (ReferenceEquals(selected, target))
                    {
                        result.Restored++;
                    }

                    replacements[row.RowId] = selected;
                }

                if (replacements.Count > 0)
                {
                    page = ExcelDataFile.Parse(ExdRowRewriter.RewriteStringColumn(header, page, columnIndex, replacements));
                    changed = true;
                }
            }

            return changed ? page.Data : patchedPage;
        }

        private static SupplementIssue Issue(SupplementIssueKind kind, uint rowId, ushort columnOffset, byte[] text)
        {
            return new SupplementIssue
            {
                Kind = kind,
                RowId = rowId,
                ColumnOffset = columnOffset,
                TargetText = Encoding.UTF8.GetString(text),
            };
        }

        private static ulong Key(uint rowId, ushort columnOffset)
        {
            return ((ulong)rowId << 16) | columnOffset;
        }

        internal static bool ContainsHangul(byte[] bytes)
        {
            string text = new UTF8Encoding(false, false).GetString(bytes);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if ((ch >= '\uAC00' && ch <= '\uD7A3') ||
                    (ch >= '\u1100' && ch <= '\u11FF') ||
                    (ch >= '\u3130' && ch <= '\u318F'))
                {
                    return true;
                }
            }

            return false;
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

    internal static class SupplementTranslations
    {
        public const string ResourcePrefix = "Supplements/";

        private static Dictionary<string, string> _resourceNames;

        public static string ResourceNameFor(string sheetName)
        {
            return ResourcePrefix + sheetName.Replace('/', '_') + ".tsv";
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
                return SupplementSheet.Parse(reader);
            }
        }
    }
}
