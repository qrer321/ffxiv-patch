using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Regression cases for SupplementSheet on synthetic two-column pages (word at offset 0, reading at
    // offset 4), and checks of the shipped Supplements\*.tsv files.
    // Run through Scripts\test-supplements.ps1.
    internal static class SupplementTests
    {
        private const string SheetHeader = SupplementSheet.Header;

        private static int _failures;
        private static int _passed;

        private static int Main(string[] args)
        {
            string supplementDir = args.Length > 0 ? args[0] : Path.Combine("FFXIVPatchGenerator", "Supplements");

            ExpectJapaneseTarget();
            ExpectEnglishTarget();
            ExpectSkippedCells();
            ExpectDriftReported();
            ExpectJapaneseTildes();
            ExpectParseErrors();
            ExpectShippedSheets(supplementDir);

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", _passed, _failures);
            return _failures == 0 ? 0 : 1;
        }

        private static SupplementSheet Sheet(params string[] lines)
        {
            return SupplementSheet.Parse(new StringReader(SheetHeader + "\n" + string.Join("\n", lines)));
        }

        private static void ExpectJapaneseTarget()
        {
            SupplementSheet sheet = Sheet(
                "1\t0\t日本語1\tEnglish1\t보충1\tproject\t",
                "2\t0\t日本語2\tEnglish2\t보충2\tproject\t",
                "3\t0\t日本語3\tEnglish3\t보충3\tproject\t",
                "4\t0\t\tEnglish4\t보충4\tproject\t");
            byte[] header = BuildHeader(5);
            ExcelHeader excelHeader = ExcelHeader.Parse(header);
            // Clean JA page; row 3 was changed by a game update after the entry was written.
            byte[] clean = BuildPage(
                new uint[] { 0, 1, 2, 3, 4 },
                new[] { "", "日本語1", "日本語2", "日本語3改", "" },
                new[] { "", "よみ1", "よみ2", "よみ3", "" });
            // After the Korean patch: row 2 has official Korean, the rest kept the target text.
            byte[] patched = BuildPage(
                new uint[] { 0, 1, 2, 3, 4 },
                new[] { "", "日本語1", "공식2", "日本語3改", "" },
                new[] { "", "よみ1", "よみ2", "よみ3", "" });

            SupplementResult result;
            ExcelDataFile output = ExcelDataFile.Parse(sheet.Apply(excelHeader, ExcelDataFile.Parse(clean), patched, "ja", null, out result));
            List<string> problems = new List<string>();
            ExpectWord(problems, output, excelHeader, 1, "보충1", "よみ1");
            ExpectWord(problems, output, excelHeader, 2, "공식2", "よみ2");
            ExpectWord(problems, output, excelHeader, 3, "日本語3改", "よみ3");
            ExpectWord(problems, output, excelHeader, 4, "", "");
            ExpectCount(problems, "applied", result.Applied, 1);
            ExpectIssues(problems, result, SupplementIssueKind.Official, 2);
            ExpectIssues(problems, result, SupplementIssueKind.Stale, 3);
            ExpectIssues(problems, result, SupplementIssueKind.Missing);
            Report("Japanese target: entries match the ja text; official Korean, changed text and empty cells stay", problems);
        }

        private static void ExpectEnglishTarget()
        {
            SupplementSheet sheet = Sheet(
                "1\t0\t日本語1\tEnglish1\t보충1\tproject\t",
                "4\t0\t\tEnglish4\t보충4\tproject\t");
            ExcelHeader excelHeader = ExcelHeader.Parse(BuildHeader(5));
            byte[] clean = BuildPage(
                new uint[] { 1, 2, 3, 4 },
                new[] { "English1", "English2", "English3", "English4" },
                new[] { "よみ1", "よみ2", "よみ3", "" });
            // The Korean client's untranslated Japanese text was written over rows 1 and 3.
            byte[] patched = BuildPage(
                new uint[] { 1, 2, 3, 4 },
                new[] { "日本語1", "공식2", "日本語3", "English4" },
                new[] { "よみ1", "よみ2", "よみ3", "" });

            SupplementResult result;
            ExcelDataFile output = ExcelDataFile.Parse(sheet.Apply(excelHeader, ExcelDataFile.Parse(clean), patched, "en", null, out result));
            List<string> problems = new List<string>();
            ExpectWord(problems, output, excelHeader, 1, "보충1", "よみ1");
            ExpectWord(problems, output, excelHeader, 2, "공식2", "よみ2");
            ExpectWord(problems, output, excelHeader, 3, "English3", "よみ3");
            ExpectWord(problems, output, excelHeader, 4, "보충4", "");
            ExpectCount(problems, "applied", result.Applied, 2);
            ExpectCount(problems, "restored", result.Restored, 1);
            ExpectIssues(problems, result, SupplementIssueKind.Missing, 3);
            Report("English target: entries match the en text; untranslated Korean-client text never replaces the target", problems);
        }

        // Game updates that move or empty a row must not drop an entry silently.
        private static void ExpectDriftReported()
        {
            SupplementSheet sheet = Sheet(
                "1\t0\t日本語1\tEnglish1\t보충1\tproject\t",
                "2\t8\t日本語2\tEnglish2\t보충2\tproject\tcol 8 is not a string column",
                "9\t0\t日本語9\tEnglish9\t보충9\tproject\trow 9 is not in the sheet");
            ExcelHeader excelHeader = ExcelHeader.Parse(BuildHeader(3));
            // Row 1 lost its Japanese word in an update.
            byte[] clean = BuildPage(new uint[] { 1, 2 }, new[] { "", "日本語2" }, new[] { "", "よみ2" });

            SupplementResult result;
            sheet.Apply(excelHeader, ExcelDataFile.Parse(clean), clean, "ja", null, out result);
            List<string> problems = new List<string>();
            ExpectIssues(problems, result, SupplementIssueKind.Stale, 1);
            List<string> unvisited = new List<string>();
            foreach (SupplementEntry entry in sheet.Unvisited())
            {
                unvisited.Add(entry.RowId + "/" + entry.ColumnOffset);
            }

            if (string.Join(",", unvisited) != "2/8,9/0")
            {
                problems.Add("unvisited: expected [2/8,9/0], got [" + string.Join(",", unvisited) + "]");
            }

            Report("an entry whose target text is gone, row is missing or column is not a string column is reported stale", problems);
        }

        private static void ExpectJapaneseTildes()
        {
            ExcelHeader excelHeader = ExcelHeader.Parse(BuildHeader(4));
            string macroWord = "보충\u0002\u0010\u0001\u0003~";
            byte[] page = BuildPage(
                new uint[] { 1, 2, 3 },
                new[] { "보충~1", "English~2", macroWord },
                new[] { "よみ～1", "", "" });

            int words;
            byte[] converted = TypedWordText.UseJapaneseTildes(excelHeader, page, out words);
            ExcelDataFile output = ExcelDataFile.Parse(converted);
            List<string> problems = new List<string>();
            ExpectWord(problems, output, excelHeader, 1, "보충\uFF5E1", "よみ～1");
            ExpectWord(problems, output, excelHeader, 2, "English~2", "");
            ExpectWord(problems, output, excelHeader, 3, macroWord, "");
            ExpectCount(problems, "tilde words", words, 1);
            if (!ReferenceEquals(TypedWordText.UseJapaneseTildes(excelHeader, converted, out words), converted) || words != 0)
            {
                problems.Add("a page with nothing left to convert must be returned as it is");
            }

            if (!TypedWordText.IsTypedWordSheet("ktgtypewordtextdata") || TypedWordText.IsTypedWordSheet("Addon"))
            {
                problems.Add("typed-word sheet matching is wrong");
            }

            Report("Japanese target: Korean typed words take U+FF5E; other words and SeString values keep '~'", problems);
        }

        private static void ExpectSkippedCells()
        {
            SupplementSheet sheet = Sheet("1\t0\t日本語1\tEnglish1\t보충1\tproject\t");
            ExcelHeader excelHeader = ExcelHeader.Parse(BuildHeader(2));
            byte[] clean = BuildPage(new uint[] { 1 }, new[] { "English1" }, new[] { "よみ1" });
            byte[] patched = BuildPage(new uint[] { 1 }, new[] { "日本語1" }, new[] { "よみ1" });

            SupplementResult result;
            byte[] output = sheet.Apply(excelHeader, ExcelDataFile.Parse(clean), patched, "en", delegate { return true; }, out result);
            List<string> problems = new List<string>();
            if (!ReferenceEquals(output, patched))
            {
                problems.Add("a fully skipped page must be returned as it is");
            }

            ExpectCount(problems, "applied", result.Applied, 0);
            ExpectCount(problems, "issues", result.Issues.Count, 0);
            Report("cells the patch must keep (base scope, patch-policy) are neither replaced nor reported", problems);
        }

        private static void ExpectParseErrors()
        {
            List<string> problems = new List<string>();
            ExpectRejected(problems, "duplicate key", SheetHeader + "\n1\t0\ta\t\t가\tproject\t\n1\t0\tb\t\t나\tproject\t");
            ExpectRejected(problems, "missing ko", SheetHeader + "\n1\t0\ta\t\t\tproject\t");
            ExpectRejected(problems, "no ja or en", SheetHeader + "\n1\t0\t\t\t가\tproject\t");
            ExpectRejected(problems, "wrong field count", SheetHeader + "\n1\t0\ta\t가\tproject\t");
            ExpectRejected(problems, "wrong header", "row\tcol\tko\n1\t0\t가");
            Report("malformed supplement sheets are rejected", problems);
        }

        // Every shipped sheet parses; Korean text is plain Hangul text. Typing-game words also have
        // to be typeable on a Korean keyboard.
        private static void ExpectShippedSheets(string supplementDir)
        {
            List<string> problems = new List<string>();
            string[] files = Directory.Exists(supplementDir) ? Directory.GetFiles(supplementDir, "*.tsv") : new string[0];
            if (files.Length == 0)
            {
                problems.Add("no supplement sheets found in " + supplementDir);
            }

            for (int f = 0; f < files.Length; f++)
            {
                string name = Path.GetFileNameWithoutExtension(files[f]);
                bool typedWords = TypedWordText.IsTypedWordSheet(name);
                SupplementSheet sheet;
                try
                {
                    using (StreamReader reader = new StreamReader(files[f], new UTF8Encoding(false, true)))
                    {
                        sheet = SupplementSheet.Parse(reader);
                    }
                }
                catch (Exception exception)
                {
                    problems.Add(name + ": " + exception.Message);
                    continue;
                }

                foreach (SupplementEntry entry in sheet.Entries)
                {
                    string where = name + " row " + entry.RowId + ": ";
                    if (!SupplementTranslations.ContainsHangul(Encoding.UTF8.GetBytes(entry.Korean)))
                    {
                        problems.Add(where + "ko has no Hangul: " + entry.Korean);
                    }

                    if (entry.Korean.Trim() != entry.Korean || entry.Korean.Contains("  "))
                    {
                        problems.Add(where + "ko has stray spaces: '" + entry.Korean + "'");
                    }

                    if (!IsKnownSource(entry.Source))
                    {
                        problems.Add(where + "unknown source: " + entry.Source);
                    }

                    for (int i = 0; i < entry.Korean.Length; i++)
                    {
                        char ch = entry.Korean[i];
                        bool hangul = ch >= '\uAC00' && ch <= '\uD7A3';
                        bool ascii = ch >= 0x20 && ch < 0x7F;
                        bool typeable = hangul || (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                                        (ch >= '0' && ch <= '9') || " ,.!?-'&~".IndexOf(ch) >= 0;
                        if (typedWords ? !typeable : !(hangul || ascii))
                        {
                            problems.Add(where + "character U+" + ((int)ch).ToString("X4") + " is not allowed in '" + entry.Korean + "'");
                            break;
                        }
                    }

                    // A tilde is only kept where it comes from the Korean client's own wording; on a
                    // Japanese target the generator turns it into the fullwidth tilde the typing field enters.
                    if (typedWords && entry.Korean.IndexOf('~') >= 0 &&
                        !entry.Source.StartsWith("kr-row:", StringComparison.Ordinal) && entry.Source != "kr-term")
                    {
                        problems.Add(where + "'~' is only kept from the Korean client's wording: " + entry.Korean);
                    }
                }
            }

            Report("shipped supplement sheets parse and their Korean text is typeable", problems);
        }

        // kr-row:<row> copies a Korean-only row of the Korean client; the others say where the wording
        // comes from and keep the details in the note.
        private static bool IsKnownSource(string source)
        {
            uint row;
            return (source.StartsWith("kr-row:", StringComparison.Ordinal) && uint.TryParse(source.Substring(7), out row)) ||
                   source == "kr-term" || source == "ffxiv-kr" || source == "ff7r-ko" ||
                   source == "community" || source == "project";
        }

        private static void ExpectRejected(List<string> problems, string name, string text)
        {
            try
            {
                SupplementSheet.Parse(new StringReader(text));
                problems.Add(name + " was accepted");
            }
            catch (InvalidDataException)
            {
            }
        }

        private static void ExpectWord(List<string> problems, ExcelDataFile page, ExcelHeader header, uint rowId, string word, string reading)
        {
            string actualWord = Encoding.UTF8.GetString(page.GetStringBytes(rowId, header, 0) ?? new byte[0]);
            string actualReading = Encoding.UTF8.GetString(page.GetStringBytes(rowId, header, 1) ?? new byte[0]);
            if (actualWord != word || actualReading != reading)
            {
                problems.Add("row " + rowId + ": expected '" + word + "'/'" + reading + "', got '" + actualWord + "'/'" + actualReading + "'");
            }
        }

        private static void ExpectCount(List<string> problems, string name, int actual, int expected)
        {
            if (actual != expected)
            {
                problems.Add(name + ": expected " + expected + ", got " + actual);
            }
        }

        private static void ExpectIssues(List<string> problems, SupplementResult result, SupplementIssueKind kind, params uint[] rows)
        {
            List<uint> actual = new List<uint>();
            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Kind == kind)
                {
                    actual.Add(result.Issues[i].RowId);
                }
            }

            if (string.Join(",", actual) != string.Join(",", rows))
            {
                problems.Add(kind + " rows: expected [" + string.Join(",", rows) + "], got [" + string.Join(",", actual) + "]");
            }
        }

        // EXH with two string columns at offsets 0 and 4, one page covering rowCount rows.
        private static byte[] BuildHeader(int rowCount)
        {
            List<byte> output = new List<byte>(Encoding.ASCII.GetBytes("EXHF"));
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

        private static byte[] BuildPage(uint[] rowIds, string[] words, string[] readings)
        {
            List<byte[]> records = new List<byte[]>();
            for (int i = 0; i < rowIds.Length; i++)
            {
                byte[] word = Encoding.UTF8.GetBytes(words[i]);
                byte[] reading = Encoding.UTF8.GetBytes(readings[i]);
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

            List<byte> output = new List<byte>(Encoding.ASCII.GetBytes("EXDF"));
            WriteUInt16(output, 2);
            WriteUInt16(output, 0);
            WriteUInt32(output, (uint)(rowIds.Length * 8));
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

            uint offset = (uint)(0x20 + rowIds.Length * 8);
            for (int i = 0; i < records.Count; i++)
            {
                WriteUInt32(output, rowIds[i]);
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
