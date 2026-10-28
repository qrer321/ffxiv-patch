using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Typing-game word sheets: the player types the word shown on screen, so its Korean text has to be
    // typeable on a Korean keyboard and match what the client's typing field enters.
    internal static class TypedWordText
    {
        private static readonly Regex[] Sheets = new Regex[]
        {
            new Regex("^KTGTypeWordTextData$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly byte[] FullwidthTilde = Encoding.UTF8.GetBytes("\uFF5E");

        public static bool IsTypedWordSheet(string sheetName)
        {
            for (int i = 0; i < Sheets.Length; i++)
            {
                if (Sheets[i].IsMatch(sheetName))
                {
                    return true;
                }
            }

            return false;
        }

        // The Japanese client's typing field enters a typed tilde as U+FF5E and does not match it with
        // the U+007E in Korean words, while its own words use only U+FF5E. Rewrites every string cell
        // with Hangul and '~' (SeString values excluded) and returns the page, or the same array when
        // nothing changed.
        public static byte[] UseJapaneseTildes(ExcelHeader header, byte[] pageData, out int words)
        {
            words = 0;
            ExcelDataFile page = ExcelDataFile.Parse(pageData);
            List<int> stringColumns = header.GetStringColumnIndexes();
            bool changed = false;
            for (int stringIndex = 0; stringIndex < stringColumns.Count; stringIndex++)
            {
                Dictionary<uint, byte[]> replacements = new Dictionary<uint, byte[]>();
                for (int rowIndex = 0; rowIndex < page.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = page.Rows[rowIndex];
                    byte[] word = page.GetStringBytes(row, header, stringColumns[stringIndex]);
                    if (word == null ||
                        Array.IndexOf(word, (byte)'~') < 0 ||
                        Array.IndexOf(word, (byte)0x02) >= 0 ||
                        !SupplementTranslations.ContainsHangul(word))
                    {
                        continue;
                    }

                    List<byte> replaced = new List<byte>(word.Length + 4);
                    for (int i = 0; i < word.Length; i++)
                    {
                        if (word[i] == (byte)'~')
                        {
                            replaced.AddRange(FullwidthTilde);
                        }
                        else
                        {
                            replaced.Add(word[i]);
                        }
                    }

                    replacements[row.RowId] = replaced.ToArray();
                }

                if (replacements.Count > 0)
                {
                    page = ExcelDataFile.Parse(ExdRowRewriter.RewriteStringColumn(header, page, stringColumns[stringIndex], replacements));
                    words += replacements.Count;
                    changed = true;
                }
            }

            return changed ? page.Data : pageData;
        }
    }
}
