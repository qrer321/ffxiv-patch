using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // A phrase said in the Say channel outside quest sheets: a duty compares the line with a text row of
    // the client (variant dungeon chants). Kept in SayPhrases.tsv, embedded as "SayPhrases.tsv":
    //   sheet  row  ja  en  ko  hint_sheet  hint_row  note
    // `ja`/`en` are the target texts of the phrase row, `ko` the Korean client's phrase, and the hint row
    // is the text that tells the player what to say. Empty hint_sheet means no hint row.
    internal sealed class SayContentPhrase
    {
        public string Sheet;
        public uint RowId;
        public string Japanese;
        public string English;
        public string Korean;
        public string HintSheet;
        public uint HintRowId;

        public bool HasHint
        {
            get { return HintSheet.Length > 0; }
        }

        public string TextFor(string targetLanguage)
        {
            if (string.Equals(targetLanguage, "ja", StringComparison.OrdinalIgnoreCase))
            {
                return Japanese;
            }

            return string.Equals(targetLanguage, "en", StringComparison.OrdinalIgnoreCase) ? English : string.Empty;
        }
    }

    internal sealed class SayContentResult
    {
        public int Phrases;
        public int Hints;
        public readonly List<string> Issues = new List<string>();
    }

    // In base say mode a phrase row keeps the target text in the cell whose clean target text is the
    // entry's text, and the hint row shows that text after the quoted Korean phrase, as quest prompts
    // do: "하늘을 나는 신이여(O dancer of the skies, hear me.)". A hint is annotated only in a column whose
    // clean target text quotes the phrase. Cells outside the Korean scope already show the target text.
    internal static class SayContentPhrases
    {
        public const string ResourceName = "SayPhrases.tsv";
        public const string Header = "sheet\trow\tja\ten\tko\thint_sheet\thint_row\tnote";

        private static List<SayContentPhrase> _loaded;

        public static List<SayContentPhrase> Parse(TextReader reader)
        {
            string header = reader.ReadLine();
            if (!string.Equals(header == null ? null : header.TrimStart('\uFEFF'), Header, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Say phrase header must be: " + Header.Replace("\t", "<TAB>"));
            }

            List<SayContentPhrase> phrases = new List<SayContentPhrase>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                uint rowId;
                uint hintRowId = 0;
                if (fields.Length != 8 ||
                    fields[0].Length == 0 ||
                    !uint.TryParse(fields[1], out rowId) ||
                    (fields[5].Length > 0 && !uint.TryParse(fields[6], out hintRowId)) ||
                    (fields[5].Length == 0 && fields[6].Length > 0))
                {
                    throw new InvalidDataException("Say phrase line " + lineNumber + ": expected sheet, row, ja, en, ko, hint_sheet, hint_row, note.");
                }

                SayContentPhrase phrase = new SayContentPhrase
                {
                    Sheet = fields[0],
                    RowId = rowId,
                    Japanese = fields[2],
                    English = fields[3],
                    Korean = fields[4],
                    HintSheet = fields[5],
                    HintRowId = hintRowId,
                };
                if ((phrase.Japanese.Length == 0 && phrase.English.Length == 0) ||
                    !HangulText.Contains(Encoding.UTF8.GetBytes(phrase.Korean)))
                {
                    throw new InvalidDataException("Say phrase line " + lineNumber + ": ja or en and a Korean ko are required.");
                }

                if (!keys.Add(phrase.Sheet + "#" + rowId))
                {
                    throw new InvalidDataException("Say phrase line " + lineNumber + ": duplicate " + phrase.Sheet + " row " + rowId + ".");
                }

                phrases.Add(phrase);
            }

            return phrases;
        }

        // The embedded table; empty when the build has none.
        public static List<SayContentPhrase> Load()
        {
            if (_loaded != null)
            {
                return _loaded;
            }

            using (Stream stream = typeof(SayContentPhrases).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    _loaded = new List<SayContentPhrase>();
                }
                else
                {
                    using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true)))
                    {
                        _loaded = Parse(reader);
                    }
                }
            }

            return _loaded;
        }

        public static bool Involves(IList<SayContentPhrase> phrases, string sheetName)
        {
            for (int i = 0; i < phrases.Count; i++)
            {
                if (SameSheet(phrases[i].Sheet, sheetName) || (phrases[i].HasHint && SameSheet(phrases[i].HintSheet, sheetName)))
                {
                    return true;
                }
            }

            return false;
        }

        // Keys of the phrase and hint rows of a sheet, to tell which ones no page had.
        public static string PhraseKey(SayContentPhrase phrase)
        {
            return phrase.Sheet + "#" + phrase.RowId;
        }

        public static string HintKey(SayContentPhrase phrase)
        {
            return phrase.HintSheet + "#" + phrase.HintRowId + "<" + PhraseKey(phrase);
        }

        // The text a cell of a Korean-patched page ends up with in base say mode: a phrase cell keeps the
        // clean target text, a hint cell shows the target phrase after its quoted Korean phrase. Returns
        // current when no phrase concerns the cell. The story verifier uses it for expected text.
        public static byte[] ResolveCell(
            IList<SayContentPhrase> phrases,
            string sheetName,
            uint rowId,
            byte[] clean,
            byte[] current,
            string targetLanguage)
        {
            string cleanText = clean == null ? string.Empty : Encoding.UTF8.GetString(clean);
            for (int i = 0; i < phrases.Count; i++)
            {
                SayContentPhrase phrase = phrases[i];
                string expected = phrase.TextFor(targetLanguage);
                if (expected.Length == 0)
                {
                    continue;
                }

                if (SameSheet(phrase.Sheet, sheetName) && phrase.RowId == rowId && IsPhrase(cleanText, expected))
                {
                    return clean;
                }

                if (phrase.HasHint && SameSheet(phrase.HintSheet, sheetName) && phrase.HintRowId == rowId &&
                    QuotesPhrase(cleanText, expected) && current != null)
                {
                    current = Annotate(current, phrase, expected) ?? current;
                }
            }

            return current;
        }

        // Applies the table to one patched page of a sheet. isKorean tells whether the Korean patch
        // applies to a cell; seen collects the phrase and hint keys found on the page. Returns the page,
        // or the same array when nothing changed.
        public static byte[] Apply(
            string sheetName,
            ExcelHeader header,
            ExcelDataFile cleanTarget,
            byte[] patchedPage,
            string targetLanguage,
            IList<SayContentPhrase> phrases,
            Func<uint, ushort, bool> isKorean,
            ICollection<string> seen,
            out SayContentResult result)
        {
            result = new SayContentResult();
            ExcelDataFile page = ExcelDataFile.Parse(patchedPage);
            List<int> stringColumns = header.GetStringColumnIndexes();
            Dictionary<int, Dictionary<uint, byte[]>> replacements = new Dictionary<int, Dictionary<uint, byte[]>>();
            for (int i = 0; i < phrases.Count; i++)
            {
                SayContentPhrase phrase = phrases[i];
                string expected = phrase.TextFor(targetLanguage);
                if (expected.Length == 0)
                {
                    continue;
                }

                ExcelDataRow row;
                if (SameSheet(phrase.Sheet, sheetName) && page.TryGetRow(phrase.RowId, out row))
                {
                    seen.Add(PhraseKey(phrase));
                    int column = FindColumn(cleanTarget, header, stringColumns, row.RowId, expected, false);
                    if (column < 0)
                    {
                        result.Issues.Add("row=" + row.RowId + "; the target text is no longer: " + expected);
                    }
                    else if (isKorean(row.RowId, header.Columns[column].Offset))
                    {
                        byte[] clean = cleanTarget.GetStringBytes(row.RowId, header, column);
                        if (!BytesEqual(clean, page.GetStringBytes(row, header, column)))
                        {
                            Replacements(replacements, column)[row.RowId] = clean;
                            result.Phrases++;
                        }
                    }
                }

                if (phrase.HasHint && SameSheet(phrase.HintSheet, sheetName) && page.TryGetRow(phrase.HintRowId, out row))
                {
                    seen.Add(HintKey(phrase));
                    int column = FindColumn(cleanTarget, header, stringColumns, row.RowId, expected, true);
                    if (column < 0)
                    {
                        result.Issues.Add("row=" + row.RowId + "; the target text no longer quotes: " + expected);
                        continue;
                    }

                    if (!isKorean(row.RowId, header.Columns[column].Offset))
                    {
                        continue;
                    }

                    byte[] current;
                    if (!Replacements(replacements, column).TryGetValue(row.RowId, out current))
                    {
                        current = page.GetStringBytes(row, header, column);
                    }

                    byte[] annotated = Annotate(current, phrase, expected);
                    if (annotated == null)
                    {
                        result.Issues.Add("row=" + row.RowId + "; the Korean text does not quote: " + phrase.Korean);
                        continue;
                    }

                    Replacements(replacements, column)[row.RowId] = annotated;
                    result.Hints++;
                }
            }

            bool changed = false;
            foreach (KeyValuePair<int, Dictionary<uint, byte[]>> entry in replacements)
            {
                if (entry.Value.Count > 0)
                {
                    page = ExcelDataFile.Parse(ExdRowRewriter.RewriteStringColumn(header, page, entry.Key, entry.Value));
                    changed = true;
                }
            }

            return changed ? page.Data : patchedPage;
        }

        // A phrase cell is the target phrase itself; a hint cell quotes it.
        private static bool IsPhrase(string cleanText, string phrase)
        {
            return string.Equals(cleanText, phrase, StringComparison.Ordinal);
        }

        private static bool QuotesPhrase(string cleanText, string phrase)
        {
            return cleanText.IndexOf(phrase, StringComparison.Ordinal) >= 0;
        }

        private static byte[] Annotate(byte[] korean, SayContentPhrase phrase, string targetPhrase)
        {
            return SayQuestPhraseLocalizer.AnnotateQuotedPhrase(korean, phrase.Korean, targetPhrase);
        }

        // The string column whose clean target text is (or, for a hint, quotes) the phrase; -1 when none.
        private static int FindColumn(
            ExcelDataFile cleanTarget,
            ExcelHeader header,
            List<int> stringColumns,
            uint rowId,
            string phrase,
            bool quoted)
        {
            for (int i = 0; i < stringColumns.Count; i++)
            {
                byte[] bytes = cleanTarget.GetStringBytes(rowId, header, stringColumns[i]);
                if (bytes == null)
                {
                    continue;
                }

                string text = Encoding.UTF8.GetString(bytes);
                if (quoted ? QuotesPhrase(text, phrase) : IsPhrase(text, phrase))
                {
                    return stringColumns[i];
                }
            }

            return -1;
        }

        private static Dictionary<uint, byte[]> Replacements(Dictionary<int, Dictionary<uint, byte[]>> replacements, int column)
        {
            Dictionary<uint, byte[]> rows;
            if (!replacements.TryGetValue(column, out rows))
            {
                rows = new Dictionary<uint, byte[]>();
                replacements.Add(column, rows);
            }

            return rows;
        }

        private static bool SameSheet(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

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
}
