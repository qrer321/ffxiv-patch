using System;
using System.Collections.Generic;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Keybound Brawler (마법자판 난타전) typing words. Column 0 is the word shown above an enemy and
    // compared with the player's input; column 4 is the Japanese reading, identical in every language.
    // Column 0 is the word itself rather than a string key, so the sheet is matched by row id only.
    // Words the Korean data still leaves in Japanese take a community translation from
    // KeyboundBrawlerWordTranslations, or else the global English word when it is plain ASCII, so they
    // can be typed without a Japanese IME.
    internal static class KeyboundBrawlerWords
    {
        public const string SheetName = "KTGTypeWordTextData";
        public const ushort WordColumnOffset = 0;

        public static bool IsWordSheet(string sheetName)
        {
            return string.Equals(sheetName, SheetName, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<uint, KeyboundBrawlerWordTranslations.Entry> Translations =
            KeyboundBrawlerWordTranslations.ByRowId();

        // Returns the community translation for a word the patch left without Korean, or null when the row
        // has no translation, the word is already Korean, or the clean Japanese word differs from the one
        // the translation was written for (reported through japaneseChanged).
        public static byte[] FindTranslation(uint rowId, byte[] patchedWord, byte[] cleanJapaneseWord, out bool japaneseChanged)
        {
            japaneseChanged = false;
            KeyboundBrawlerWordTranslations.Entry translation;
            if (!Translations.TryGetValue(rowId, out translation) || ContainsHangul(patchedWord))
            {
                return null;
            }

            string japaneseText;
            if (!TryDecodePlain(cleanJapaneseWord, out japaneseText) ||
                !string.Equals(japaneseText, translation.Japanese, StringComparison.Ordinal))
            {
                japaneseChanged = true;
                return null;
            }

            return Encoding.UTF8.GetBytes(translation.Korean);
        }

        // Rewrites the word of every row that FindTranslation translates. Rows for which skipRow returns
        // true (patch-policy keep, fallback and remap rows) are left alone.
        public static byte[] ApplyTranslations(
            ExcelHeader header,
            ExcelDataFile cleanTarget,
            ExcelDataFile cleanJapanese,
            byte[] patchedPage,
            Func<uint, bool> skipRow,
            out KeyboundBrawlerTranslationResult result)
        {
            result = new KeyboundBrawlerTranslationResult();
            int columnIndex = header.FindStringColumnIndexByOffset(WordColumnOffset);
            if (columnIndex < 0 || cleanTarget == null || cleanJapanese == null || patchedPage == null)
            {
                return patchedPage;
            }

            Dictionary<uint, ExcelDataRow> targetRows = RowsById(cleanTarget);
            Dictionary<uint, ExcelDataRow> japaneseRows = RowsById(cleanJapanese);
            ExcelDataFile patched = ExcelDataFile.Parse(patchedPage);
            Dictionary<uint, byte[]> replacements = new Dictionary<uint, byte[]>();
            for (int i = 0; i < patched.Rows.Count; i++)
            {
                ExcelDataRow row = patched.Rows[i];
                ExcelDataRow japaneseRow;
                if ((skipRow != null && skipRow(row.RowId)) ||
                    !japaneseRows.TryGetValue(row.RowId, out japaneseRow))
                {
                    continue;
                }

                byte[] patchedWord = patched.GetStringBytes(row, header, columnIndex);
                bool japaneseChanged;
                byte[] translation = FindTranslation(
                    row.RowId,
                    patchedWord,
                    cleanJapanese.GetStringBytes(japaneseRow, header, columnIndex),
                    out japaneseChanged);
                if (japaneseChanged)
                {
                    result.SkippedChangedJapanese++;
                }

                if (translation == null)
                {
                    continue;
                }

                replacements[row.RowId] = translation;
                ExcelDataRow targetRow;
                if (targetRows.TryGetValue(row.RowId, out targetRow) &&
                    BytesEqual(patchedWord, cleanTarget.GetStringBytes(targetRow, header, columnIndex)))
                {
                    result.NewlyChanged++;
                }
            }

            result.Applied = replacements.Count;
            return result.Applied == 0
                ? patchedPage
                : ExdRowRewriter.RewriteStringColumn(header, patched, columnIndex, replacements);
        }

        private static Dictionary<uint, ExcelDataRow> RowsById(ExcelDataFile file)
        {
            Dictionary<uint, ExcelDataRow> rows = new Dictionary<uint, ExcelDataRow>();
            for (int i = 0; i < file.Rows.Count; i++)
            {
                rows[file.Rows[i].RowId] = file.Rows[i];
            }

            return rows;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return left == right;
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

        private static bool ContainsHangul(byte[] word)
        {
            string text;
            if (!TryDecodePlain(word, out text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (IsHangul(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldUseEnglishWord(byte[] koreanWord, byte[] englishWord)
        {
            return IsUntranslatedJapanese(koreanWord) && IsTypeableAscii(englishWord);
        }

        private static bool IsUntranslatedJapanese(byte[] word)
        {
            string text;
            if (!TryDecodePlain(word, out text))
            {
                return false;
            }

            bool hasJapanese = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (IsHangul(c))
                {
                    return false;
                }

                if (IsJapanese(c))
                {
                    hasJapanese = true;
                }
            }

            return hasJapanese;
        }

        private static bool IsTypeableAscii(byte[] word)
        {
            if (word == null || word.Length == 0)
            {
                return false;
            }

            bool hasLetterOrDigit = false;
            for (int i = 0; i < word.Length; i++)
            {
                byte b = word[i];
                if (b < 0x20 || b > 0x7E)
                {
                    return false;
                }

                if ((b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z'))
                {
                    hasLetterOrDigit = true;
                }
            }

            return hasLetterOrDigit;
        }

        private static bool TryDecodePlain(byte[] word, out string text)
        {
            text = string.Empty;
            if (word == null || word.Length == 0 || Array.IndexOf(word, (byte)0x02) >= 0)
            {
                return false;
            }

            text = Encoding.UTF8.GetString(word);
            return true;
        }

        private static bool IsHangul(char c)
        {
            return (c >= '\uAC00' && c <= '\uD7A3') ||
                   (c >= '\u1100' && c <= '\u11FF') ||
                   (c >= '\u3130' && c <= '\u318F');
        }

        private static bool IsJapanese(char c)
        {
            return (c >= '\u3040' && c <= '\u30FF') ||
                   (c >= '\u3400' && c <= '\u4DBF') ||
                   (c >= '\u4E00' && c <= '\u9FFF') ||
                   (c >= '\uFF66' && c <= '\uFF9F');
        }
    }

    internal sealed class KeyboundBrawlerTranslationResult
    {
        // Rows whose word was replaced with a community translation.
        public int Applied;
        // Replaced rows whose word still equalled the clean target word before the replacement.
        public int NewlyChanged;
        // Rows with a translation whose clean Japanese word no longer matches it.
        public int SkippedChangedJapanese;
    }
}
