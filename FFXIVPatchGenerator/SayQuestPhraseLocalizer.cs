using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal enum SayQuestPhraseMode
    {
        // Say quests expect the Korean phrase (the plain Korean patch behavior).
        Korean,
        // Say quests expect the base-language phrase, and Korean prompts show it as "한국어(base)".
        Base
    }

    internal sealed class SayQuestRowText
    {
        public uint RowId;
        public string Key;
        public byte[] Japanese;
        public byte[] Base;
        public byte[] Korean;
    }

    internal sealed class SayQuestPhraseResult
    {
        public readonly Dictionary<uint, byte[]> Replacements = new Dictionary<uint, byte[]>();
        public bool IsSayQuest;
        public bool KeptKorean;
        public int PhraseRows;
        public int AnnotatedRows;
    }

    // Say quests compare what the player types with a phrase row of the client's own text. With the
    // Korean patch that row is Korean, so the player has to type Hangul, which other players on the
    // global servers see as "===". In Base mode the phrase rows keep the base-language text, and the
    // Korean journal/objective/system prompts show it next to the Korean phrase: "안녕하세요(こんにちは)".
    // Anything that cannot be mapped safely keeps the whole quest Korean.
    internal static class SayQuestPhraseLocalizer
    {
        private const int TextColumnOffset = 4;
        private const int MaxPhraseLength = 80;
        private const string SayModeMarker = "「Say」モード";
        private const int MinWordAnnotationLength = 2;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly char[] TrailingPunctuation = { '！', '!', '。', '.', '？', '?', '…' };
        private static readonly QuotePair[] KoreanQuotePairs =
        {
            new QuotePair('\'', '\''),
            new QuotePair('"', '"'),
            new QuotePair('“', '”'),
            new QuotePair('‘', '’'),
            new QuotePair('「', '」'),
            new QuotePair('『', '』')
        };

        public static bool TryParseMode(string value, out SayQuestPhraseMode mode)
        {
            if (string.Equals(value, "base", StringComparison.OrdinalIgnoreCase))
            {
                mode = SayQuestPhraseMode.Base;
                return true;
            }

            if (string.Equals(value, "ko", StringComparison.OrdinalIgnoreCase))
            {
                mode = SayQuestPhraseMode.Korean;
                return true;
            }

            mode = SayQuestPhraseMode.Korean;
            return false;
        }

        public static string FormatMode(SayQuestPhraseMode mode)
        {
            return mode == SayQuestPhraseMode.Base ? "base" : "ko";
        }

        public static bool IsCandidateSheet(string sheetName)
        {
            return !string.IsNullOrEmpty(sheetName) &&
                   sheetName.StartsWith("quest/", StringComparison.OrdinalIgnoreCase);
        }

        // Rows of one quest sheet: Japanese is the clean global Japanese text (used to find the phrases),
        // Base the clean target-language text, Korean the patched Korean text.
        public static SayQuestPhraseResult Localize(IList<SayQuestRowText> rows)
        {
            SayQuestPhraseResult result = new SayQuestPhraseResult();
            HashSet<string> promptPhrases = CollectPromptPhrases(rows);

            List<SayQuestRowText> phraseRows = new List<SayQuestRowText>();
            for (int i = 0; i < rows.Count; i++)
            {
                SayQuestRowText row = rows[i];
                KeyKind kind = ClassifyKey(row.Key);
                if (kind == KeyKind.Say ||
                    (kind == KeyKind.System && promptPhrases.Contains(Normalize(PlainText(row.Japanese)))))
                {
                    phraseRows.Add(row);
                }
            }

            if (phraseRows.Count == 0)
            {
                return result;
            }

            result.IsSayQuest = true;
            HashSet<uint> phraseRowIds = new HashSet<uint>();
            Dictionary<string, List<string>> basesByKorean = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < phraseRows.Count; i++)
            {
                SayQuestRowText row = phraseRows[i];
                string japanese;
                string baseText;
                string korean;
                if (!TryDecodePhrase(row.Japanese, out japanese) ||
                    !TryDecodePhrase(row.Base, out baseText) ||
                    !TryDecodePhrase(row.Korean, out korean))
                {
                    return KeepKorean(result);
                }

                phraseRowIds.Add(row.RowId);
                result.PhraseRows++;
                if (!BytesEqual(row.Korean, row.Base))
                {
                    result.Replacements[row.RowId] = row.Base;
                }

                string koreanKey = Normalize(korean);
                if (koreanKey.Length == 0 || string.Equals(koreanKey, Normalize(baseText), StringComparison.Ordinal))
                {
                    continue;
                }

                List<string> bases;
                if (!basesByKorean.TryGetValue(koreanKey, out bases))
                {
                    bases = new List<string>();
                    basesByKorean.Add(koreanKey, bases);
                }

                if (!bases.Contains(baseText))
                {
                    bases.Add(baseText);
                }
            }

            // Journal, objective and system prompts are where the game tells the player what to say. Puzzle
            // quests never quote the answer in a prompt; their hints are in the dialogue, which is what the
            // chat log keeps, so every quoted mention is annotated there.
            bool promptedQuest = promptPhrases.Count > 0;
            HashSet<string> annotated = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                SayQuestRowText row = rows[i];
                if (!phraseRowIds.Contains(row.RowId) && (!promptedQuest || IsPromptKey(row.Key)))
                {
                    AnnotateRow(row, basesByKorean, true, annotated, result);
                }
            }

            // A prompted quest may still quote a phrase only in dialogue. A puzzle quest may name its answer
            // without quotes (a riddle letter); there the word itself is annotated wherever it appears.
            // Phrases with a base phrase not shown yet keep all their base phrases, so a quote is still
            // matched to the base phrase its own row names.
            Dictionary<string, List<string>> unannotated = Unannotated(basesByKorean, annotated);
            if (unannotated.Count > 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    SayQuestRowText row = rows[i];
                    if (!phraseRowIds.Contains(row.RowId) && (!promptedQuest || !IsPromptKey(row.Key)))
                    {
                        AnnotateRow(row, unannotated, promptedQuest, annotated, result);
                    }
                }
            }

            // The player must be able to see what to type. A quest whose prompts quote the phrases needs
            // every base phrase shown; a puzzle quest at least one hint (its other phrases are derived answers).
            foreach (KeyValuePair<string, List<string>> entry in basesByKorean)
            {
                for (int i = 0; promptedQuest && i < entry.Value.Count; i++)
                {
                    if (!annotated.Contains(AnnotationKey(entry.Key, entry.Value[i])))
                    {
                        return KeepKorean(result);
                    }
                }
            }

            if (basesByKorean.Count > 0 && annotated.Count == 0)
            {
                return KeepKorean(result);
            }

            return result;
        }

        // Applies Localize to a patched quest page whose string columns are the key (offset 0) and the
        // text (offset 4). Returns the original bytes when nothing changes.
        public static byte[] ApplyToPage(
            ExcelHeader header,
            ExcelDataFile cleanTarget,
            ExcelDataFile japaneseReference,
            byte[] patchedPage,
            out SayQuestPhraseResult result)
        {
            result = new SayQuestPhraseResult();
            if (!IsKeyTextLayout(header))
            {
                return patchedPage;
            }

            ExcelDataFile patched = ExcelDataFile.Parse(patchedPage);
            result = Localize(BuildRows(header, cleanTarget, japaneseReference, delegate(ExcelDataRow row)
            {
                return GetText(patched, header, row.RowId);
            }));
            if (result.Replacements.Count == 0)
            {
                return patchedPage;
            }

            return ExdRowRewriter.RewriteStringColumn(
                header,
                patched,
                header.FindStringColumnIndexByOffset(TextColumnOffset),
                result.Replacements);
        }

        public delegate byte[] KoreanTextSelector(ExcelDataRow cleanRow);

        // Rows of a quest page as Localize sees them. Keys and base text come from the clean target page,
        // the Japanese text from the reference page (the target page when null), the Korean text from the
        // selector. The generator passes its patched page; the verifier its expected Korean text.
        public static List<SayQuestRowText> BuildRows(
            ExcelHeader header,
            ExcelDataFile cleanTarget,
            ExcelDataFile japaneseReference,
            KoreanTextSelector koreanText)
        {
            ExcelDataFile reference = japaneseReference ?? cleanTarget;
            List<SayQuestRowText> rows = new List<SayQuestRowText>();
            for (int i = 0; i < cleanTarget.Rows.Count; i++)
            {
                ExcelDataRow row = cleanTarget.Rows[i];
                string key;
                if (!TryDecodePlain(cleanTarget.GetStringBytesByColumnOffset(row, header, 0), out key))
                {
                    continue;
                }

                SayQuestRowText text = new SayQuestRowText();
                text.RowId = row.RowId;
                text.Key = key;
                text.Korean = koreanText(row);
                text.Base = cleanTarget.GetStringBytesByColumnOffset(row, header, TextColumnOffset);
                text.Japanese = GetText(reference, header, row.RowId);
                rows.Add(text);
            }

            return rows;
        }

        public static bool IsKeyTextLayout(ExcelHeader header)
        {
            List<int> stringColumns = header.GetStringColumnIndexes();
            return stringColumns.Count == 2 &&
                   header.FindStringColumnIndexByOffset(0) >= 0 &&
                   header.FindStringColumnIndexByOffset(TextColumnOffset) >= 0;
        }

        private static Dictionary<string, List<string>> Unannotated(
            Dictionary<string, List<string>> basesByKorean,
            HashSet<string> annotated)
        {
            Dictionary<string, List<string>> unannotated = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> entry in basesByKorean)
            {
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    if (!annotated.Contains(AnnotationKey(entry.Key, entry.Value[i])))
                    {
                        unannotated.Add(entry.Key, entry.Value);
                        break;
                    }
                }
            }

            return unannotated;
        }

        // Annotations are tracked per Korean phrase and base phrase: one Korean phrase may stand for
        // several base phrases, and each must be shown somewhere.
        private static string AnnotationKey(string koreanKey, string baseText)
        {
            return koreanKey + "\u0001" + baseText;
        }

        private static SayQuestPhraseResult KeepKorean(SayQuestPhraseResult result)
        {
            result.Replacements.Clear();
            result.KeptKorean = true;
            result.AnnotatedRows = 0;
            return result;
        }

        private static HashSet<string> CollectPromptPhrases(IList<SayQuestRowText> rows)
        {
            HashSet<string> phrases = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                string text = PlainText(rows[i].Japanese);
                if (text.IndexOf(SayModeMarker, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                int searchFrom = 0;
                while (searchFrom < text.Length)
                {
                    int open = text.IndexOf('『', searchFrom);
                    if (open < 0)
                    {
                        break;
                    }

                    int close = text.IndexOf('』', open + 1);
                    if (close < 0)
                    {
                        break;
                    }

                    string phrase = Normalize(text.Substring(open + 1, close - open - 1));
                    if (phrase.Length > 0)
                    {
                        phrases.Add(phrase);
                    }

                    searchFrom = close + 1;
                }
            }

            return phrases;
        }

        private static void AnnotateRow(
            SayQuestRowText row,
            Dictionary<string, List<string>> basesByKorean,
            bool quotedOnly,
            HashSet<string> annotated,
            SayQuestPhraseResult result)
        {
            byte[] current;
            bool alreadyAnnotated = result.Replacements.TryGetValue(row.RowId, out current);
            if (!alreadyAnnotated)
            {
                current = row.Korean;
            }

            List<Segment> segments;
            if (current == null || current.Length == 0 || !TrySplit(current, out segments))
            {
                return;
            }

            string baseContext = PlainText(row.Base);
            bool changed = false;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                if (segment.Text == null)
                {
                    continue;
                }

                string updated = quotedOnly
                    ? AnnotateQuoted(segment.Text, basesByKorean, baseContext, annotated)
                    : AnnotateWords(segment.Text, basesByKorean, baseContext, annotated);
                if (!string.Equals(updated, segment.Text, StringComparison.Ordinal))
                {
                    segment.Text = updated;
                    changed = true;
                }
            }

            if (changed)
            {
                result.Replacements[row.RowId] = Join(segments);
                if (!alreadyAnnotated)
                {
                    result.AnnotatedRows++;
                }
            }
        }

        // Inserts "(base)" after every occurrence of the Korean phrase that starts a word, so a short
        // answer is not annotated inside a longer word. Particles may follow, so only the start is checked.
        private static string AnnotateWords(
            string text,
            Dictionary<string, List<string>> basesByKorean,
            string baseContext,
            HashSet<string> annotated)
        {
            SortedDictionary<int, string> insertions = new SortedDictionary<int, string>();
            foreach (KeyValuePair<string, List<string>> entry in basesByKorean)
            {
                string chosen = ChooseBase(entry.Value, baseContext);
                if (chosen == null || entry.Key.Length < MinWordAnnotationLength)
                {
                    continue;
                }

                string annotation = "(" + chosen + ")";
                int searchFrom = 0;
                while (searchFrom < text.Length)
                {
                    int index = text.IndexOf(entry.Key, searchFrom, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        break;
                    }

                    int end = index + entry.Key.Length;
                    searchFrom = end;
                    if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
                    {
                        continue;
                    }

                    if (!insertions.ContainsKey(end))
                    {
                        insertions.Add(end, annotation);
                    }

                    annotated.Add(AnnotationKey(entry.Key, chosen));
                }
            }

            return Insert(text, insertions);
        }

        // Inserts "(base)" before the closing quote of each quoted Korean phrase.
        private static string AnnotateQuoted(
            string text,
            Dictionary<string, List<string>> basesByKorean,
            string baseContext,
            HashSet<string> annotated)
        {
            SortedDictionary<int, string> insertions = new SortedDictionary<int, string>();
            for (int p = 0; p < KoreanQuotePairs.Length; p++)
            {
                QuotePair pair = KoreanQuotePairs[p];
                int searchFrom = 0;
                while (searchFrom < text.Length)
                {
                    int open = text.IndexOf(pair.Open, searchFrom);
                    if (open < 0)
                    {
                        break;
                    }

                    int close = text.IndexOf(pair.Close, open + 1);
                    if (close < 0)
                    {
                        break;
                    }

                    string content = text.Substring(open + 1, close - open - 1);
                    List<string> bases;
                    string koreanKey = Normalize(content);
                    if (koreanKey.Length > 0 && basesByKorean.TryGetValue(koreanKey, out bases))
                    {
                        string chosen = ChooseBase(bases, baseContext);
                        if (chosen != null)
                        {
                            string annotation = "(" + chosen + ")";
                            if (!insertions.ContainsKey(close))
                            {
                                insertions.Add(close, annotation);
                            }

                            annotated.Add(AnnotationKey(koreanKey, chosen));
                        }
                    }

                    searchFrom = close + 1;
                }
            }

            return Insert(text, insertions);
        }

        private static string Insert(string text, SortedDictionary<int, string> insertions)
        {
            if (insertions.Count == 0)
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text);
            List<int> positions = new List<int>(insertions.Keys);
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                builder.Insert(positions[i], insertions[positions[i]]);
            }

            return builder.ToString();
        }

        // One Korean phrase can stand for different base phrases in different steps (English rewrites
        // "안녕하세요" two ways); the prompt's own base-language text tells which one this step expects.
        private static string ChooseBase(List<string> bases, string baseContext)
        {
            if (bases.Count == 1)
            {
                return bases[0];
            }

            string chosen = null;
            for (int i = 0; i < bases.Count; i++)
            {
                if (baseContext.IndexOf(bases[i], StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (chosen != null)
                {
                    return null;
                }

                chosen = bases[i];
            }

            return chosen;
        }

        private enum KeyKind
        {
            Other,
            Say,
            System
        }

        // Quest text keys look like TEXT_<QUEST>_<ID>_<KIND>_..., e.g. TEXT_SUBFST122_01428_SYSTEM_000_012,
        // TEXT_STMBDA401_02500_SAYTODO_000_000 or TEXT_GAIUSE314_01455_TATARU_SAYTODO_000. Speaker keys
        // (TEXT_..._RYNE_000_145) are dialogue even when the line equals a phrase.
        private static KeyKind ClassifyKey(string key)
        {
            string[] tokens = SplitKey(key);
            bool system = false;
            for (int i = 3; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], "SAY", StringComparison.Ordinal) ||
                    tokens[i].StartsWith("SAYTODO", StringComparison.Ordinal))
                {
                    return KeyKind.Say;
                }

                if (string.Equals(tokens[i], "SYSTEM", StringComparison.Ordinal))
                {
                    system = true;
                }
            }

            return system ? KeyKind.System : KeyKind.Other;
        }

        private static bool IsPromptKey(string key)
        {
            string[] tokens = SplitKey(key);
            for (int i = 3; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], "SEQ", StringComparison.Ordinal) ||
                    string.Equals(tokens[i], "TODO", StringComparison.Ordinal) ||
                    string.Equals(tokens[i], "SYSTEM", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] SplitKey(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("TEXT_", StringComparison.Ordinal))
            {
                return new string[0];
            }

            return key.ToUpperInvariant().Split('_');
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            int end = trimmed.Length;
            while (end > 0 && (Array.IndexOf(TrailingPunctuation, trimmed[end - 1]) >= 0 || char.IsWhiteSpace(trimmed[end - 1])))
            {
                end--;
            }

            return trimmed.Substring(0, end);
        }

        // A phrase row must be a short single-line plain string in every language.
        private static bool TryDecodePhrase(byte[] bytes, out string text)
        {
            if (!TryDecodePlain(bytes, out text))
            {
                return false;
            }

            string trimmed = text.Trim();
            return trimmed.Length > 0 &&
                   trimmed.Length <= MaxPhraseLength &&
                   trimmed.IndexOf('\n') < 0 &&
                   trimmed.IndexOf('\r') < 0;
        }

        private static bool TryDecodePlain(byte[] bytes, out string text)
        {
            text = null;
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x02 || bytes[i] == 0x03)
                {
                    return false;
                }
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

        // Top-level text without macros; empty when the SeString cannot be parsed.
        private static string PlainText(byte[] bytes)
        {
            List<Segment> segments;
            if (bytes == null || !TrySplit(bytes, out segments))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Text != null)
                {
                    builder.Append(segments[i].Text);
                }
            }

            return builder.ToString();
        }

        private static bool TrySplit(byte[] bytes, out List<Segment> segments)
        {
            segments = new List<Segment>();
            int cursor = 0;
            int textStart = 0;
            while (cursor < bytes.Length)
            {
                if (bytes[cursor] != 0x02)
                {
                    if (bytes[cursor] == 0x03)
                    {
                        return false;
                    }

                    cursor++;
                    continue;
                }

                int macroLength;
                if (!TryMeasureMacro(bytes, cursor, out macroLength) ||
                    !AddText(segments, bytes, textStart, cursor))
                {
                    return false;
                }

                segments.Add(Segment.Macro(Slice(bytes, cursor, macroLength)));
                cursor += macroLength;
                textStart = cursor;
            }

            return AddText(segments, bytes, textStart, bytes.Length);
        }

        private static bool AddText(List<Segment> segments, byte[] bytes, int start, int end)
        {
            if (end <= start)
            {
                return true;
            }

            try
            {
                segments.Add(Segment.TextRun(StrictUtf8.GetString(bytes, start, end - start)));
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static byte[] Join(List<Segment> segments)
        {
            MemoryStream output = new MemoryStream();
            for (int i = 0; i < segments.Count; i++)
            {
                byte[] bytes = segments[i].Text != null ? StrictUtf8.GetBytes(segments[i].Text) : segments[i].MacroBytes;
                output.Write(bytes, 0, bytes.Length);
            }

            return output.ToArray();
        }

        // <02 type length payload 03>; only the outer extent is needed because macros are copied verbatim.
        private static bool TryMeasureMacro(byte[] bytes, int offset, out int length)
        {
            length = 0;
            if (offset + 3 > bytes.Length)
            {
                return false;
            }

            uint payloadLength;
            int lengthSize;
            if (!TryReadUInt(bytes, offset + 2, out payloadLength, out lengthSize))
            {
                return false;
            }

            long end = (long)offset + 2 + lengthSize + payloadLength;
            if (end >= bytes.Length || bytes[end] != 0x03)
            {
                return false;
            }

            length = (int)(end + 1 - offset);
            return true;
        }

        private static bool TryReadUInt(byte[] bytes, int offset, out uint value, out int length)
        {
            value = 0;
            length = 0;
            if (offset >= bytes.Length)
            {
                return false;
            }

            byte marker = bytes[offset];
            if (marker > 0x00 && marker < 0xD0)
            {
                value = (uint)(marker - 1);
                length = 1;
                return true;
            }

            if (marker < 0xF0 || marker > 0xFE)
            {
                return false;
            }

            byte flags = (byte)(marker + 1);
            int required = 1;
            if ((flags & 0x08) != 0) required++;
            if ((flags & 0x04) != 0) required++;
            if ((flags & 0x02) != 0) required++;
            if ((flags & 0x01) != 0) required++;
            if (offset + required > bytes.Length)
            {
                return false;
            }

            int cursor = offset + 1;
            if ((flags & 0x08) != 0) value |= (uint)(bytes[cursor++] << 24);
            if ((flags & 0x04) != 0) value |= (uint)(bytes[cursor++] << 16);
            if ((flags & 0x02) != 0) value |= (uint)(bytes[cursor++] << 8);
            if ((flags & 0x01) != 0) value |= bytes[cursor++];
            length = required;
            return true;
        }

        private static byte[] GetText(ExcelDataFile file, ExcelHeader header, uint rowId)
        {
            ExcelDataRow row;
            if (file == null || !file.TryGetRow(rowId, out row))
            {
                return null;
            }

            return file.GetStringBytesByColumnOffset(row, header, TextColumnOffset);
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

        private static byte[] Slice(byte[] bytes, int offset, int length)
        {
            byte[] slice = new byte[length];
            Buffer.BlockCopy(bytes, offset, slice, 0, length);
            return slice;
        }

        private sealed class Segment
        {
            public string Text;
            public byte[] MacroBytes;

            public static Segment TextRun(string text)
            {
                Segment segment = new Segment();
                segment.Text = text;
                return segment;
            }

            public static Segment Macro(byte[] bytes)
            {
                Segment segment = new Segment();
                segment.MacroBytes = bytes;
                return segment;
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
}
