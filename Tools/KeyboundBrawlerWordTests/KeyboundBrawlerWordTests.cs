using System;
using System.Collections.Generic;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Regression cases for KeyboundBrawlerWords, built from synthetic KTGTypeWordTextData pages.
    // Run through Scripts\test-keybound-brawler-words.ps1.
    internal static class KeyboundBrawlerWordTests
    {
        private static int _failures;
        private static int _passed;

        private static int Main()
        {
            ExpectEnglish("untranslated Japanese word with an ASCII English word", "ブーメランちょうだい！", "Gimme a boomerang!", true);
            ExpectEnglish("Korean word stays Korean", "부메랑 줘!", "Gimme a boomerang!", false);
            ExpectEnglish("mixed Korean and Japanese counts as Korean", "부메랑ちょうだい", "Gimme a boomerang!", false);
            ExpectEnglish("empty Korean word", "", "Gimme a boomerang!", false);
            ExpectEnglish("ASCII Korean word is not untranslated Japanese", "XIII", "XIII", false);
            ExpectEnglish("English word still in Japanese", "ゾンダー", "ゾンダー", false);
            ExpectEnglish("English word with a non-ASCII letter", "カフェ", "Café", false);
            ExpectEnglish("English word with only symbols", "ゾンダー", "!?", false);
            ExpectEnglish("empty English word", "ゾンダー", "", false);
            ExpectEnglishBytes("Korean word with a SeString macro", Cat("ゾンダー", new byte[] { 0x02, 0x10, 0x01, 0x03 }), Text("Zonder"), false);
            ExpectEnglishBytes("English word with a SeString macro", Text("ゾンダー"), Cat("Zonder", new byte[] { 0x02, 0x10, 0x01, 0x03 }), false);

            ExpectTilde("halfwidth tildes in a Korean word become fullwidth", "호히~ 호히~!", "호히～ 호히～!");
            ExpectTilde("Korean word without a tilde is kept", "부메랑 줘!", null);
            ExpectTilde("word without Hangul keeps its tilde", "Hi~", null);
            ExpectTildeBytes("Korean word with a SeString macro keeps its tilde", Cat("호히~", new byte[] { 0x02, 0x10, 0x01, 0x03 }));

            ExpectTranslatedPage();
            ExpectEnglishTarget();
            ExpectTildePage();
            ExpectUntouchedPage();
            ExpectTable();

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", _passed, _failures);
            return _failures == 0 ? 0 : 1;
        }

        private static void ExpectTranslatedPage()
        {
            // Row 39: English fallback is replaced. Row 41: an official Korean word wins. Row 243: a word
            // left in Japanese is replaced. Row 54: the clean Japanese word changed, so it is skipped.
            // Row 51: patch-policy pins the row. Row 1: not in the table.
            uint[] ids = { 1, 39, 41, 51, 54, 243 };
            string[] japanese = { "神羅", "ブーメランちょうだい！", "釘バットちょうだい！", "リボンちょうだい！", "金の針をちょうだい！", "ゾンダー" };
            string[] readings = { "しんら", "ぶーめらんちょうだい", "くぎばっとちょうだい", "りぼんちょうだい", "きんのはりをちょうだい", "ぞんだー" };
            string[] patchedWords = { "신라", "Gimme a boomerang!", "못 박힌 배트!", "リボンちょうだい！", "金の針をちょうだい！", "ゾンダー" };
            string[] expectedWords = { "신라", Translation(39), "못 박힌 배트!", "リボンちょうだい！", "金の針をちょうだい！", Translation(243) };

            ExcelHeader header = ExcelHeader.Parse(BuildHeader(ids.Length));
            ExcelDataFile clean = ExcelDataFile.Parse(BuildPage(ids, japanese, readings));
            byte[] patched = BuildPage(ids, patchedWords, readings);
            KeyboundBrawlerWordResult result;
            byte[] output = KeyboundBrawlerWords.ApplyWordFixes(
                header, clean, clean, patched, delegate(uint rowId) { return rowId == 51; }, out result);

            List<string> problems = new List<string>();
            // Only row 243 still had the clean target word; row 39 was already counted as patched.
            if (result.Translated != 2 || result.NewlyChanged != 1 || result.SkippedChangedJapanese != 1)
            {
                problems.Add("applied=" + result.Translated + ", newly changed=" + result.NewlyChanged + ", skipped=" + result.SkippedChangedJapanese);
            }

            ExcelDataFile page = ExcelDataFile.Parse(output);
            ExcelDataFile input = ExcelDataFile.Parse(patched);
            if (page.Rows.Count != ids.Length)
            {
                problems.Add("row count " + page.Rows.Count);
            }

            for (int i = 0; i < ids.Length && i < page.Rows.Count; i++)
            {
                ExcelDataRow row = page.Rows[i];
                if (row.RowId != ids[i])
                {
                    problems.Add("row " + i + " id " + row.RowId);
                }

                if (!Same(page.GetStringBytesByColumnOffset(row, header, 0), Text(expectedWords[i])))
                {
                    problems.Add("row " + ids[i] + " word is " + Encoding.UTF8.GetString(page.GetStringBytesByColumnOffset(row, header, 0)));
                }

                if (!Same(page.GetStringBytesByColumnOffset(row, header, 4), Text(readings[i])))
                {
                    problems.Add("row " + ids[i] + " reading changed");
                }

                if (Record(page, row).Length % 4 != 0)
                {
                    problems.Add("row " + ids[i] + " record is not padded");
                }

                bool replaced = ids[i] == 39 || ids[i] == 243;
                if (!replaced && !Same(Record(page, row), Record(input, input.Rows[i])))
                {
                    problems.Add("row " + ids[i] + " record was not copied verbatim");
                }
            }

            Report("words still not Korean take the translation; official Korean, policy rows, unknown rows and changed Japanese stay", problems);
        }

        private static void ExpectTildePage()
        {
            // Row 1540: an official Korean word with a tilde. Row 151: a translation with a tilde.
            // Row 1541: patch-policy pins the row. Row 1: no tilde.
            uint[] ids = { 1, 151, 1540, 1541 };
            string[] japanese = { "神羅", "ほひ～！", "なにするんだ、ゴラ～！", "ありがっとね～！" };
            string[] readings = { "しんら", "ほひ～", "なにするんだごら～", "ありがっとね～" };
            string[] patchedWords = { "신라", "ほひ～！", "뭐 하는 짓이야~!", "고마워~!" };
            string[] expectedWords = { "신라", Translation(151).Replace('~', '～'), "뭐 하는 짓이야～!", "고마워~!" };

            ExcelHeader header = ExcelHeader.Parse(BuildHeader(ids.Length));
            ExcelDataFile clean = ExcelDataFile.Parse(BuildPage(ids, japanese, readings));
            byte[] patched = BuildPage(ids, patchedWords, readings);
            KeyboundBrawlerWordResult result;
            byte[] output = KeyboundBrawlerWords.ApplyWordFixes(
                header, clean, clean, patched, delegate(uint rowId) { return rowId == 1541; }, out result);

            List<string> problems = new List<string>();
            if (result.Translated != 1 || result.FullwidthTildeWords != 2 || result.NewlyChanged != 1)
            {
                problems.Add("translated=" + result.Translated + ", tildes=" + result.FullwidthTildeWords + ", newly changed=" + result.NewlyChanged);
            }

            ExcelDataFile page = ExcelDataFile.Parse(output);
            for (int i = 0; i < ids.Length; i++)
            {
                if (!Same(page.GetStringBytesByColumnOffset(page.Rows[i], header, 0), Text(expectedWords[i])))
                {
                    problems.Add("row " + ids[i] + " word is " + Encoding.UTF8.GetString(page.GetStringBytesByColumnOffset(page.Rows[i], header, 0)));
                }

                if (!Same(page.GetStringBytesByColumnOffset(page.Rows[i], header, 4), Text(readings[i])))
                {
                    problems.Add("row " + ids[i] + " reading changed");
                }
            }

            byte[] withoutJapanese = KeyboundBrawlerWords.ApplyWordFixes(header, clean, null, patched, null, out result);
            ExcelDataFile tildeOnly = ExcelDataFile.Parse(withoutJapanese);
            if (result.Translated != 0 || result.FullwidthTildeWords != 2 ||
                !Same(tildeOnly.GetStringBytesByColumnOffset(tildeOnly.Rows[1], header, 0), Text(patchedWords[1])))
            {
                problems.Add("without a Japanese page: translated=" + result.Translated + ", tildes=" + result.FullwidthTildeWords);
            }

            Report("Korean words get fullwidth tildes, translations included, policy rows kept", problems);
        }

        private static void ExpectTilde(string name, string word, string expected)
        {
            byte[] input = Text(word);
            byte[] actual = KeyboundBrawlerWords.UseFullwidthTildes(input);
            List<string> problems = new List<string>();
            if (expected == null ? !ReferenceEquals(actual, input) : !Same(actual, Text(expected)))
            {
                problems.Add("got " + Encoding.UTF8.GetString(actual));
            }

            Report(name, problems);
        }

        private static void ExpectTildeBytes(string name, byte[] word)
        {
            List<string> problems = new List<string>();
            if (!ReferenceEquals(KeyboundBrawlerWords.UseFullwidthTildes(word), word))
            {
                problems.Add("word was changed");
            }

            Report(name, problems);
        }

        private static void ExpectEnglishTarget()
        {
            // An English target page: the English fallback word equals the clean target word, so the
            // translated row counts as newly changed; the Japanese page decides the translation.
            uint[] ids = { 39 };
            string[] readings = { "ぶーめらんちょうだい" };
            ExcelHeader header = ExcelHeader.Parse(BuildHeader(ids.Length));
            ExcelDataFile cleanEnglish = ExcelDataFile.Parse(BuildPage(ids, new string[] { "Gimme a boomerang!" }, readings));
            ExcelDataFile cleanJapanese = ExcelDataFile.Parse(BuildPage(ids, new string[] { "ブーメランちょうだい！" }, readings));
            byte[] patched = BuildPage(ids, new string[] { "Gimme a boomerang!" }, readings);
            KeyboundBrawlerWordResult result;
            byte[] output = KeyboundBrawlerWords.ApplyWordFixes(header, cleanEnglish, cleanJapanese, patched, null, out result);

            List<string> problems = new List<string>();
            ExcelDataFile page = ExcelDataFile.Parse(output);
            if (result.Translated != 1 || result.NewlyChanged != 1 ||
                !Same(page.GetStringBytesByColumnOffset(page.Rows[0], header, 0), Text(Translation(39))))
            {
                problems.Add("applied=" + result.Translated + ", newly changed=" + result.NewlyChanged);
            }

            KeyboundBrawlerWords.ApplyWordFixes(header, cleanEnglish, cleanEnglish, patched, null, out result);
            if (result.Translated != 0 || result.SkippedChangedJapanese != 1)
            {
                problems.Add("an English page used as the Japanese reference was not rejected");
            }

            Report("English target translates through the Japanese page and counts the row", problems);
        }

        private static void ExpectUntouchedPage()
        {
            uint[] ids = { 1, 41 };
            string[] readings = { "しんら", "くぎばっとちょうだい" };
            ExcelHeader header = ExcelHeader.Parse(BuildHeader(ids.Length));
            ExcelDataFile clean = ExcelDataFile.Parse(BuildPage(ids, new string[] { "神羅", "釘バットちょうだい！" }, readings));
            byte[] patched = BuildPage(ids, new string[] { "신라", "못 박힌 배트 줘!" }, readings);
            KeyboundBrawlerWordResult result;
            byte[] output = KeyboundBrawlerWords.ApplyWordFixes(header, clean, clean, patched, null, out result);

            List<string> problems = new List<string>();
            if (!ReferenceEquals(output, patched) || result.Translated != 0 || result.SkippedChangedJapanese != 0)
            {
                problems.Add("a page with only Korean words was rebuilt");
            }

            byte[] untranslated = BuildPage(ids, new string[] { "신라", "釘バットちょうだい！" }, readings);
            byte[] missingReference = KeyboundBrawlerWords.ApplyWordFixes(header, clean, null, untranslated, null, out result);
            if (!ReferenceEquals(missingReference, untranslated) || result.Translated != 0)
            {
                problems.Add("a missing Japanese reference changed the page");
            }

            Report("pages without words to translate are returned unchanged", problems);
        }

        private static void ExpectTable()
        {
            List<string> problems = new List<string>();
            HashSet<uint> rows = new HashSet<uint>();
            for (int i = 0; i < KeyboundBrawlerWordTranslations.Entries.Length; i++)
            {
                KeyboundBrawlerWordTranslations.Entry entry = KeyboundBrawlerWordTranslations.Entries[i];
                if (!rows.Add(entry.RowId))
                {
                    problems.Add("row " + entry.RowId + " is listed twice");
                }

                if (!HasJapanese(entry.Japanese))
                {
                    problems.Add("row " + entry.RowId + " has no Japanese source word");
                }

                if (!HasHangul(entry.Korean) || HasJapanese(entry.Korean))
                {
                    problems.Add("row " + entry.RowId + " Korean word is not plain Korean: " + entry.Korean);
                }

                if (entry.Korean != entry.Korean.Trim() || entry.Korean.Contains("  "))
                {
                    problems.Add("row " + entry.RowId + " Korean word has stray spaces");
                }
            }

            Report("translation table has one plain Korean word per Japanese row", problems);
        }

        private static string Translation(uint rowId)
        {
            return KeyboundBrawlerWordTranslations.ByRowId()[rowId].Korean;
        }

        private static void ExpectEnglish(string name, string korean, string english, bool expected)
        {
            ExpectEnglishBytes(name, Text(korean), Text(english), expected);
        }

        private static void ExpectEnglishBytes(string name, byte[] korean, byte[] english, bool expected)
        {
            List<string> problems = new List<string>();
            bool actual = KeyboundBrawlerWords.ShouldUseEnglishWord(korean, english);
            if (actual != expected)
            {
                problems.Add("expected " + expected + ", got " + actual);
            }

            Report(name, problems);
        }

        private static bool HasHangul(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '\uAC00' && text[i] <= '\uD7A3')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasJapanese(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\u3040' && c <= '\u30FF') || (c >= '\u4E00' && c <= '\u9FFF'))
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] Record(ExcelDataFile file, ExcelDataRow row)
        {
            int offset = (int)row.Offset;
            int size = 6 + (int)ReadUInt32BE(file.Data, offset);
            byte[] record = new byte[size];
            Buffer.BlockCopy(file.Data, offset, record, 0, size);
            return record;
        }

        // EXH with two string columns: the word at offset 0 and the reading at offset 4.
        private static byte[] BuildHeader(int rowCount)
        {
            List<byte> output = new List<byte>(Text("EXHF"));
            WriteUInt16(output, 3);
            WriteUInt16(output, 8);
            WriteUInt16(output, 2);
            WriteUInt16(output, 1);
            WriteUInt16(output, 1);
            WriteUInt16(output, 0);
            output.Add(0);
            output.Add(1);
            WriteUInt16(output, 0);
            WriteUInt32(output, (uint)rowCount);
            while (output.Count < 0x20)
            {
                output.Add(0);
            }

            WriteUInt16(output, 0);
            WriteUInt16(output, 0);
            WriteUInt16(output, 0);
            WriteUInt16(output, 4);
            WriteUInt32(output, 0);
            WriteUInt32(output, (uint)rowCount);
            output.Add(1);
            output.Add(0);
            return output.ToArray();
        }

        private static byte[] BuildPage(uint[] ids, string[] words, string[] readings)
        {
            List<byte[]> records = new List<byte[]>();
            for (int i = 0; i < ids.Length; i++)
            {
                byte[] word = Text(words[i]);
                byte[] reading = Text(readings[i]);
                List<byte> body = new List<byte>();
                WriteUInt32(body, 0);
                WriteUInt32(body, (uint)(word.Length + 1));
                body.AddRange(word);
                body.Add(0);
                body.AddRange(reading);
                body.Add(0);
                while ((6 + body.Count) % 4 != 0)
                {
                    body.Add(0);
                }

                List<byte> record = new List<byte>();
                WriteUInt32(record, (uint)body.Count);
                WriteUInt16(record, 1);
                record.AddRange(body);
                records.Add(record.ToArray());
            }

            List<byte> output = new List<byte>(Text("EXDF"));
            WriteUInt16(output, 2);
            WriteUInt16(output, 0);
            WriteUInt32(output, (uint)(ids.Length * 8));
            uint dataSize = 0;
            for (int i = 0; i < records.Count; i++)
            {
                dataSize += (uint)records[i].Length;
            }

            WriteUInt32(output, dataSize);
            while (output.Count < 0x20)
            {
                output.Add(0);
            }

            uint offset = (uint)(0x20 + ids.Length * 8);
            for (int i = 0; i < records.Count; i++)
            {
                WriteUInt32(output, ids[i]);
                WriteUInt32(output, offset);
                offset += (uint)records[i].Length;
            }

            for (int i = 0; i < records.Count; i++)
            {
                output.AddRange(records[i]);
            }

            return output.ToArray();
        }

        private static void WriteUInt16(List<byte> output, ushort value)
        {
            output.Add((byte)(value >> 8));
            output.Add((byte)value);
        }

        private static void WriteUInt32(List<byte> output, uint value)
        {
            output.Add((byte)(value >> 24));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 8));
            output.Add((byte)value);
        }

        private static uint ReadUInt32BE(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);
        }

        private static byte[] Text(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        private static byte[] Cat(string text, byte[] tail)
        {
            List<byte> output = new List<byte>(Text(text));
            output.AddRange(tail);
            return output.ToArray();
        }

        private static bool Same(byte[] left, byte[] right)
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

        private static void Report(string name, List<string> problems)
        {
            if (problems.Count == 0)
            {
                _passed++;
                Console.WriteLine("PASS  {0}", name);
                return;
            }

            _failures++;
            Console.WriteLine("FAIL  {0}", name);
            for (int i = 0; i < problems.Count; i++)
            {
                Console.WriteLine("      {0}", problems[i]);
            }
        }
    }
}
