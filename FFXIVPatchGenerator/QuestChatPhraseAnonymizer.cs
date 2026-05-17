using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class QuestChatPhraseAnonymizationFeature
    {
        public const bool Enabled = false;
        public const string DisabledWarning =
            "Say quest chat phrase anonymization is disabled because sheet coverage is incomplete.";
    }

    internal static class QuestChatPhraseAnonymizer
    {
        private const int QuestDialogueColumnIndex = 1;
        private const string AnonymousChatPhrase = "`";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly QuotePair[] QuotePairs = new QuotePair[]
        {
            new QuotePair('“', '”'),
            new QuotePair('"', '"'),
            new QuotePair('「', '」'),
            new QuotePair('『', '』'),
            new QuotePair('‘', '’')
        };

        public static QuestChatAnonymizeResult Apply(string sheetName, ExcelHeader header, ExcelDataFile file)
        {
            QuestChatAnonymizeResult result = new QuestChatAnonymizeResult();
            if (!IsQuestDialogueSheet(sheetName) ||
                header == null ||
                file == null ||
                header.Columns.Count <= QuestDialogueColumnIndex ||
                header.Columns[QuestDialogueColumnIndex].Type != 0)
            {
                result.Data = file == null ? null : file.Data;
                return result;
            }

            Dictionary<uint, string> rowKeys = ReadStringColumn(header, file, 0);
            Dictionary<uint, string> rowTexts = ReadStringColumn(header, file, QuestDialogueColumnIndex);
            Dictionary<string, string> phraseMap = BuildPhraseMap(rowTexts, rowKeys);
            if (phraseMap.Count == 0)
            {
                result.Data = file.Data;
                return result;
            }

            Dictionary<uint, byte[]> replacementRows = new Dictionary<uint, byte[]>();
            HashSet<string> changedPhrases = new HashSet<string>(StringComparer.Ordinal);
            int promptRows = 0;
            int phraseRows = 0;

            foreach (KeyValuePair<uint, string> rowText in rowTexts)
            {
                string transformed = TransformText(rowText.Value, phraseMap, changedPhrases, ref promptRows, ref phraseRows);
                if (!string.Equals(rowText.Value, transformed, StringComparison.Ordinal))
                {
                    replacementRows[rowText.Key] = Utf8.GetBytes(transformed);
                }
            }

            if (replacementRows.Count == 0)
            {
                result.Data = file.Data;
                return result;
            }

            result.Data = RewriteRows(header, file, replacementRows);
            result.Changed = true;
            result.RowsChanged = replacementRows.Count;
            result.PromptRowsChanged = promptRows;
            result.PhraseRowsChanged = phraseRows;
            result.PhrasesChanged = changedPhrases.Count;
            return result;
        }

        private static bool IsQuestDialogueSheet(string sheetName)
        {
            return !string.IsNullOrEmpty(sheetName) &&
                   sheetName.StartsWith("quest/", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<uint, string> ReadStringColumn(ExcelHeader header, ExcelDataFile file, int columnIndex)
        {
            Dictionary<uint, string> texts = new Dictionary<uint, string>();
            if (columnIndex < 0 || columnIndex >= header.Columns.Count || header.Columns[columnIndex].Type != 0)
            {
                return texts;
            }

            for (int i = 0; i < file.Rows.Count; i++)
            {
                ExcelDataRow row = file.Rows[i];
                byte[] bytes = file.GetStringBytes(row, header, columnIndex);
                string text;
                if (TryDecodeUtf8(bytes, out text) && !string.IsNullOrEmpty(text))
                {
                    texts[row.RowId] = text;
                }
            }

            return texts;
        }

        private static Dictionary<string, string> BuildPhraseMap(Dictionary<uint, string> rowTexts, Dictionary<uint, string> rowKeys)
        {
            Dictionary<string, string> phraseMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string text in rowTexts.Values)
            {
                if (!LooksLikeSayQuestPrompt(text))
                {
                    continue;
                }

                List<string> phrases = ExtractQuotedPhrases(text);
                for (int i = 0; i < phrases.Count; i++)
                {
                    string phrase = phrases[i].Trim();
                    if (!IsValidPhraseCandidate(phrase) || phraseMap.ContainsKey(phrase))
                    {
                        continue;
                    }

                    AddPhrase(phraseMap, phrase);
                }
            }

            foreach (KeyValuePair<uint, string> rowKey in rowKeys)
            {
                string text;
                if (IsSayQuestKey(rowKey.Value) &&
                    rowTexts.TryGetValue(rowKey.Key, out text) &&
                    IsValidPhraseCandidate(text))
                {
                    AddPhrase(phraseMap, text.Trim());
                }
            }

            return phraseMap;
        }

        private static void AddPhrase(Dictionary<string, string> phraseMap, string phrase)
        {
            if (!phraseMap.ContainsKey(phrase))
            {
                phraseMap.Add(phrase, AnonymousChatPhrase);
            }
        }

        private static bool IsSayQuestKey(string key)
        {
            return !string.IsNullOrEmpty(key) &&
                   key.StartsWith("TEXT_", StringComparison.OrdinalIgnoreCase) &&
                   key.IndexOf("_SAY_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeSayQuestPrompt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string lower = text.ToLowerInvariant();
            return lower.IndexOf("chat mode", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("say", StringComparison.Ordinal) >= 0 && lower.IndexOf("enter", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("채팅", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("チャット", StringComparison.Ordinal) >= 0;
        }

        private static List<string> ExtractQuotedPhrases(string text)
        {
            List<string> phrases = new List<string>();
            for (int i = 0; i < QuotePairs.Length; i++)
            {
                QuotePair pair = QuotePairs[i];
                int startIndex = 0;
                while (startIndex < text.Length)
                {
                    int start = text.IndexOf(pair.Open, startIndex);
                    if (start < 0)
                    {
                        break;
                    }

                    int contentStart = start + 1;
                    int end = text.IndexOf(pair.Close, contentStart);
                    if (end < 0)
                    {
                        break;
                    }

                    if (end > contentStart)
                    {
                        phrases.Add(text.Substring(contentStart, end - contentStart));
                    }

                    startIndex = end + 1;
                }
            }

            return phrases;
        }

        private static bool IsValidPhraseCandidate(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > 80)
            {
                return false;
            }

            return phrase.IndexOf('\r') < 0 &&
                   phrase.IndexOf('\n') < 0 &&
                   phrase.IndexOf('\u0002') < 0;
        }

        private static string TransformText(
            string text,
            Dictionary<string, string> phraseMap,
            HashSet<string> changedPhrases,
            ref int promptRows,
            ref int phraseRows)
        {
            string transformed = text;
            bool promptChanged = false;
            foreach (KeyValuePair<string, string> phrase in phraseMap)
            {
                string promptReplacement = ReplaceQuotedPhrase(transformed, phrase.Key, phrase.Value);
                if (!string.Equals(transformed, promptReplacement, StringComparison.Ordinal))
                {
                    transformed = promptReplacement;
                    promptChanged = true;
                    changedPhrases.Add(phrase.Key);
                }
            }

            if (promptChanged)
            {
                promptRows++;
                return transformed;
            }

            foreach (KeyValuePair<string, string> phrase in phraseMap)
            {
                string exactReplacement;
                if (TryReplaceExactPhrase(text, phrase.Key, phrase.Value, out exactReplacement))
                {
                    changedPhrases.Add(phrase.Key);
                    phraseRows++;
                    return exactReplacement;
                }
            }

            return text;
        }

        private static string ReplaceQuotedPhrase(string text, string phrase, string replacement)
        {
            string result = text;
            for (int i = 0; i < QuotePairs.Length; i++)
            {
                QuotePair pair = QuotePairs[i];
                result = result.Replace(
                    pair.Open.ToString() + phrase + pair.Close.ToString(),
                    pair.Open.ToString() + replacement + pair.Close.ToString());
            }

            return result;
        }

        private static bool TryReplaceExactPhrase(string text, string phrase, string replacement, out string result)
        {
            string[] suffixes = new string[] { string.Empty, "!", "！", ".", "。" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                string suffix = suffixes[i];
                if (string.Equals(text, phrase + suffix, StringComparison.Ordinal))
                {
                    result = replacement + suffix;
                    return true;
                }
            }

            result = text;
            return false;
        }

        private static byte[] RewriteRows(ExcelHeader header, ExcelDataFile file, Dictionary<uint, byte[]> replacementRows)
        {
            List<int> stringColumns = header.GetStringColumnIndexes();
            List<byte[]> rowRecords = new List<byte[]>();
            uint dataSectionSize = 0;

            for (int i = 0; i < file.Rows.Count; i++)
            {
                ExcelDataRow row = file.Rows[i];
                byte[] replacement;
                byte[] rowRecord = replacementRows.TryGetValue(row.RowId, out replacement)
                    ? RewriteRow(header, file, row, stringColumns, replacement)
                    : CopyOriginalRowRecord(file, row);

                rowRecords.Add(rowRecord);
                dataSectionSize += checked((uint)rowRecord.Length);
            }

            MemoryStream output = new MemoryStream(file.Data.Length + 1024);
            byte[] fileHeader = new byte[0x20];
            Buffer.BlockCopy(file.Data, 0, fileHeader, 0, fileHeader.Length);

            uint indexSize = checked((uint)(file.Rows.Count * 8));
            Endian.WriteUInt32BE(fileHeader, 0x08, indexSize);
            Endian.WriteUInt32BE(fileHeader, 0x0C, dataSectionSize);
            output.Write(fileHeader, 0, fileHeader.Length);

            uint currentRowOffset = checked((uint)(0x20 + indexSize));
            for (int i = 0; i < file.Rows.Count; i++)
            {
                Endian.WriteUInt32BE(output, file.Rows[i].RowId);
                Endian.WriteUInt32BE(output, currentRowOffset);
                currentRowOffset += checked((uint)rowRecords[i].Length);
            }

            for (int i = 0; i < rowRecords.Count; i++)
            {
                output.Write(rowRecords[i], 0, rowRecords[i].Length);
            }

            return output.ToArray();
        }

        private static byte[] RewriteRow(
            ExcelHeader header,
            ExcelDataFile file,
            ExcelDataRow row,
            List<int> stringColumns,
            byte[] questDialogueReplacement)
        {
            int rowOffset = checked((int)row.Offset);
            uint bodySize = Endian.ReadUInt32BE(file.Data, rowOffset);
            ushort rowCount = Endian.ReadUInt16BE(file.Data, rowOffset + 4);
            int fixedOffset = rowOffset + 6;
            byte[] fixedData = new byte[header.DataOffset];
            Buffer.BlockCopy(file.Data, fixedOffset, fixedData, 0, fixedData.Length);

            MemoryStream stringData = new MemoryStream();
            for (int i = 0; i < stringColumns.Count; i++)
            {
                int columnIndex = stringColumns[i];
                ExcelColumnDefinition column = header.Columns[columnIndex];
                byte[] selected = columnIndex == QuestDialogueColumnIndex
                    ? questDialogueReplacement
                    : file.GetStringBytes(row, header, columnIndex) ?? new byte[0];

                uint newStringOffset = checked((uint)stringData.Position);
                Endian.WriteUInt32BE(fixedData, column.Offset, newStringOffset);
                stringData.Write(selected, 0, selected.Length);
                stringData.WriteByte(0);
            }

            byte[] strings = stringData.ToArray();
            int bodySizeWithoutPadding = checked(fixedData.Length + strings.Length);
            int rowRecordSizeWithoutPadding = checked(6 + bodySizeWithoutPadding);
            int paddingSize = (4 - (rowRecordSizeWithoutPadding % 4)) % 4;
            uint newBodySize = checked((uint)(bodySizeWithoutPadding + paddingSize));

            MemoryStream output = new MemoryStream(6 + (int)newBodySize);
            Endian.WriteUInt32BE(output, newBodySize);
            Endian.WriteUInt16BE(output, rowCount);
            output.Write(fixedData, 0, fixedData.Length);
            output.Write(strings, 0, strings.Length);
            for (int i = 0; i < paddingSize; i++)
            {
                output.WriteByte(0);
            }

            if (bodySize == 0 && newBodySize == 0)
            {
                throw new InvalidDataException("Unexpected empty EXD row.");
            }

            return output.ToArray();
        }

        private static byte[] CopyOriginalRowRecord(ExcelDataFile file, ExcelDataRow row)
        {
            int rowOffset = checked((int)row.Offset);
            int bodySize = checked((int)Endian.ReadUInt32BE(file.Data, rowOffset));
            int recordSize = checked(6 + bodySize);
            byte[] copy = new byte[recordSize];
            Buffer.BlockCopy(file.Data, rowOffset, copy, 0, copy.Length);
            return copy;
        }

        private static bool TryDecodeUtf8(byte[] bytes, out string text)
        {
            text = null;
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            try
            {
                text = StrictUtf8.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private struct QuotePair
        {
            public readonly char Open;
            public readonly char Close;

            public QuotePair(char open, char close)
            {
                Open = open;
                Close = close;
            }
        }
    }

    internal sealed class QuestChatAnonymizeResult
    {
        public byte[] Data;
        public bool Changed;
        public int RowsChanged;
        public int PromptRowsChanged;
        public int PhraseRowsChanged;
        public int PhrasesChanged;
    }
}
