using System;
using System.Collections.Generic;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Regression cases for SayQuestPhraseLocalizer, built from synthetic quest rows.
    // Run through Scripts\test-say-quest-phrases.ps1.
    internal static class SayQuestPhraseTests
    {
        private const string Q = "TEXT_TESTQST001_00001_";
        private const string JaPrompt = "チャットの「Say」モードで『がんばれ！』と入力し励ます";
        private const string KoPrompt = "대화창에 '말하기' 방식으로 \"힘내!\"를 입력하여 격려하기";
        private const string KoPromptAnnotated = "대화창에 '말하기' 방식으로 \"힘내!(がんばれ)\"를 입력하여 격려하기";

        private static int _failures;
        private static int _passed;

        private static int Main()
        {
            // Prompted quests.
            Expect("phrase row keeps the base text and prompts show it",
                Rows(
                    Row(1, "TODO_00", JaPrompt, JaPrompt, KoPrompt),
                    Row(2, "SEQ_00", JaPrompt, JaPrompt, KoPrompt),
                    Row(3, "SYSTEM_000_100", "がんばれ", "がんばれ", "힘내")),
                Applied(1, 2),
                Change(1, KoPromptAnnotated),
                Change(2, KoPromptAnnotated),
                Change(3, "がんばれ"));
            Expect("English base shows the English phrase",
                Rows(
                    Row(1, "TODO_00", JaPrompt, "With the chat mode in Say, enter “Don't give up!”.", KoPrompt),
                    Row(2, "SYSTEM_000_100", "がんばれ", "Don't give up!", "힘내")),
                Applied(1, 1),
                Change(1, "대화창에 '말하기' 방식으로 \"힘내!(Don't give up!)\"를 입력하여 격려하기"),
                Change(2, "Don't give up!"));
            Expect("SAYTODO and SAY keys are phrase rows without a prompt match",
                Rows(
                    Row(1, "TODO_00", "チャットの「Say」モードで『フレイ』を含む言葉を入力", "", "대화창에 '말하기' 방식으로 '프레이'를 포함한 문장을 입력"),
                    Row(2, "TATARU_SAYTODO_000", "フレイ", "フレイ", "프레이")),
                Applied(1, 1),
                Change(1, "대화창에 '말하기' 방식으로 '프레이(フレイ)'를 포함한 문장을 입력"),
                Change(2, "フレイ"));
            Expect("speaker lines equal to the phrase stay dialogue",
                Rows(
                    Row(1, "TODO_00", "チャットの「Say」モードで『ガイア』と入力", "", "대화창에 '말하기' 방식으로 \"가이아\"라고 입력"),
                    Row(2, "RYNE_000_145", "ガイア！", "ガイア！", "가이아!"),
                    Row(3, "SAYTODO_000_160", "ガイア", "ガイア", "가이아")),
                Applied(1, 1),
                Change(1, "대화창에 '말하기' 방식으로 \"가이아(ガイア)\"라고 입력"),
                Change(3, "ガイア"));
            Expect("one Korean phrase with two base phrases follows each prompt",
                Rows(
                    Row(1, "TODO_01", JaHello, "With the chat mode in Say, enter “I come in peace.” to greet Niniya.", KoHello),
                    Row(2, "TODO_04", JaHello, "With the chat mode in Say, enter “Greetings and salutations!” to greet her.", KoHello),
                    Row(3, "SYSTEM_000_012", "こんにちは", "I come in peace.", "안녕하세요."),
                    Row(4, "SYSTEM_000_032", "こんにちは", "Greetings and salutations!", "안녕하세요.")),
                Applied(2, 2),
                Change(1, "대화창에서 대화 방식을 '말하기'로 하고 \"안녕하세요.(I come in peace.)\"라고 입력"),
                Change(2, "대화창에서 대화 방식을 '말하기'로 하고 \"안녕하세요.(Greetings and salutations!)\"라고 입력"),
                Change(3, "I come in peace."),
                Change(4, "Greetings and salutations!"));
            Expect("an undecidable quote is left as it is when every base phrase is shown elsewhere",
                Rows(
                    Row(1, "TODO_01", JaHello, "enter “I come in peace.”", KoHello),
                    Row(2, "TODO_04", JaHello, "enter “Greetings and salutations!”", KoHello),
                    Row(3, "SEQ_01", JaHello, "say hello", KoHello),
                    Row(4, "SYSTEM_000_012", "こんにちは", "I come in peace.", "안녕하세요."),
                    Row(5, "SYSTEM_000_032", "こんにちは", "Greetings and salutations!", "안녕하세요.")),
                Applied(2, 2),
                Change(1, "대화창에서 대화 방식을 '말하기'로 하고 \"안녕하세요.(I come in peace.)\"라고 입력"),
                Change(2, "대화창에서 대화 방식을 '말하기'로 하고 \"안녕하세요.(Greetings and salutations!)\"라고 입력"),
                Change(4, "I come in peace."),
                Change(5, "Greetings and salutations!"));
            Expect("a base phrase that no prompt can show keeps the quest Korean",
                Rows(
                    Row(1, "TODO_01", JaHello, "enter “I come in peace.”", KoHello),
                    Row(2, "SEQ_01", JaHello, "say hello", KoHello),
                    Row(3, "SYSTEM_000_012", "こんにちは", "I come in peace.", "안녕하세요."),
                    Row(4, "SYSTEM_000_032", "こんにちは", "Greetings and salutations!", "안녕하세요.")),
                KeptKorean());
            Expect("a base phrase shown only in dialogue is annotated there",
                Rows(
                    Row(1, "TODO_01", JaHello, "enter “I come in peace.”", KoHello),
                    Row(2, "NIA_000_040", "", "Say “Greetings and salutations!” to her.", "그녀에게 \"안녕하세요.\"라고 말해."),
                    Row(3, "SYSTEM_000_012", "こんにちは", "I come in peace.", "안녕하세요."),
                    Row(4, "SYSTEM_000_032", "こんにちは", "Greetings and salutations!", "안녕하세요.")),
                Applied(2, 2),
                Change(1, "대화창에서 대화 방식을 '말하기'로 하고 \"안녕하세요.(I come in peace.)\"라고 입력"),
                Change(2, "그녀에게 \"안녕하세요.(Greetings and salutations!)\"라고 말해."),
                Change(3, "I come in peace."),
                Change(4, "Greetings and salutations!"));
            Expect("a phrase equal in both languages needs no annotation",
                Rows(
                    Row(1, "TODO_00", "チャットの「Say」モードで『12345』と入力", "", "대화창에 '말하기' 방식으로 '12345'를 입력"),
                    Row(2, "SYSTEM_000_010", "12345", "12345", "12345")),
                Applied(1, 0));

            // Macros: quotes are matched inside text runs only, and macros are copied verbatim.
            byte[] place = Macro(0x28, new byte[] { 0xFF, 0x05, 0x45, 0x4F, 0x62, 0x6A, 0x02, 0x02 });
            Expect("quotes around a macro are not annotated, other quotes are",
                Rows(
                    Row(1, "SEQ_00", Text(JaPrompt), Text(""), Cat("대화창에 '말하기' 방식으로 \"힘내!\"를 입력하여 '", place, "' 주변에서 격려")),
                    Row(2, "SYSTEM_000_100", Text("がんばれ"), Text("がんばれ"), Text("힘내"))),
                Applied(1, 1),
                Change(1, Cat("대화창에 '말하기' 방식으로 \"힘내!(がんばれ)\"를 입력하여 '", place, "' 주변에서 격려")),
                Change(2, Text("がんばれ")));
            Expect("a prompt with a malformed macro is left unchanged and the quest stays Korean",
                Rows(
                    Row(1, "TODO_00", Text(JaPrompt), Text(JaPrompt), Cat(KoPrompt, new byte[] { 0x02, 0x28, 0x09, 0x01 })),
                    Row(2, "SYSTEM_000_100", Text("がんばれ"), Text("がんばれ"), Text("힘내"))),
                KeptKorean());

            // Safety: anything unsafe keeps the whole quest Korean.
            Expect("a phrase that no prompt shows keeps the quest Korean",
                Rows(
                    Row(1, "TODO_00", JaPrompt, JaPrompt, "대화창에 '말하기' 방식으로 응원하기"),
                    Row(2, "SYSTEM_000_100", "がんばれ", "がんばれ", "힘내")),
                KeptKorean());
            Expect("a prompted quest keeps Korean when any phrase is not shown",
                Rows(
                    Row(1, "TODO_00", JaPrompt, JaPrompt, KoPrompt),
                    Row(2, "TODO_01", "チャットの「Say」モードで『もっていますか？』と入力", "", "대화창에서 '말하기'로 용건 말하기"),
                    Row(3, "SYSTEM_000_100", "がんばれ", "がんばれ", "힘내"),
                    Row(4, "SYSTEM_000_200", "もっていますか？", "もっていますか？", "가지고 있어요?")),
                KeptKorean());
            Expect("a phrase row with a macro keeps the quest Korean",
                Rows(
                    Row(1, "TODO_00", Text(JaPrompt), Text(JaPrompt), Text(KoPrompt)),
                    Row(2, "SYSTEM_000_100", Text("がんばれ"), Text("がんばれ"), Cat("힘내", place))),
                KeptKorean());
            Expect("a phrase row without Korean text keeps the quest Korean",
                Rows(
                    Row(1, "TODO_00", Text(JaPrompt), Text(JaPrompt), Text(KoPrompt)),
                    Row(2, "SYSTEM_000_100", Text("がんばれ"), Text("がんばれ"), null)),
                KeptKorean());
            Expect("a long phrase row keeps the quest Korean",
                Rows(
                    Row(1, "TODO_00", "チャットの「Say」モードで『" + new string('あ', 81) + "』と入力", "", "'" + new string('가', 81) + "'를 입력"),
                    Row(2, "SYSTEM_000_100", new string('あ', 81), new string('あ', 81), new string('가', 81))),
                KeptKorean());
            Expect("rows of a quest without say phrases are untouched",
                Rows(
                    Row(1, "TODO_00", "「ハイデリン」と話す", "", "'하이델린'과 대화"),
                    Row(2, "SYSTEM_000_100", "ハイデリン", "ハイデリン", "하이델린")),
                NotSayQuest());

            // Puzzle quests: no prompt quotes the answer.
            Expect("puzzle answers are annotated in quoted dialogue",
                Rows(
                    Row(1, "SEQ_04", "嫌いなものは弟子の「シュトラ」だそうだ。", "", "싫어하는 것은 제자 '슈톨라'임이 확실하다고 한다."),
                    Row(2, "BROOMC_000_061", "「シュトラ」！", "", "왜 있잖아, 마지막 제자…… '슈톨라'!"),
                    Row(3, "SYSTEM_000_085", "チャットの会話モードを「Say」モードにして呪文を入力しよう。", "", "대화창에서 대화 방식을 '말하기'로 한 다음 주문을 입력하세요."),
                    Row(4, "SAY_000_000", "シュトラ", "シュトラ", "슈톨라"),
                    Row(5, "SAY_000_001", "ラトュシ", "ラトュシ", "라톨슈")),
                Applied(2, 2),
                Change(1, "싫어하는 것은 제자 '슈톨라(シュトラ)'임이 확실하다고 한다."),
                Change(2, "왜 있잖아, 마지막 제자…… '슈톨라(シュトラ)'!"),
                Change(4, "シュトラ"),
                Change(5, "ラトュシ"));
            Expect("an unquoted puzzle answer is annotated where it appears",
                Rows(
                    Row(1, "MYSTERYLETTER04317_000_024", "好物であるゴールドエーコンの言葉を耳にすれば", "", "\"자신이 좋아하는 황금도토리란 말을 들으면 모습을 드러내리라.\""),
                    Row(2, "SAYTODO_000_060", "ゴールドエーコン", "ゴールドエーコン", "황금도토리")),
                Applied(1, 1),
                Change(1, "\"자신이 좋아하는 황금도토리(ゴールドエーコン)란 말을 들으면 모습을 드러내리라.\""),
                Change(2, "ゴールドエーコン"));
            Expect("a puzzle answer that appears nowhere keeps the quest Korean",
                Rows(
                    Row(1, "SEQ_04", "", "", "주문을 알아내자."),
                    Row(2, "SAY_000_000", "シュトラ", "シュトラ", "슈톨라")),
                KeptKorean());
            Expect("an unquoted puzzle answer inside a longer word is not annotated",
                Rows(
                    Row(1, "LETTER_000_010", "", "", "마라히는 아니고, 라히라고 외쳐라."),
                    Row(2, "SAYTODO_000_020", "ラヒ", "ラヒ", "라히")),
                Applied(1, 1),
                Change(1, "마라히는 아니고, 라히(ラヒ)라고 외쳐라."),
                Change(2, "ラヒ"));
            Expect("a one-letter unquoted puzzle answer is not annotated",
                Rows(
                    Row(1, "LETTER_000_010", "", "", "쿠 하고 외쳐라."),
                    Row(2, "SAYTODO_000_020", "ク", "ク", "쿠")),
                KeptKorean());

            // Page level: only the say rows are rebuilt; other rows are copied verbatim.
            ExpectPage();

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", _passed, _failures);
            return _failures == 0 ? 0 : 1;
        }

        private static void ExpectPage()
        {
            string[] keys = { Q + "TODO_00", Q + "SYSTEM_000_100", Q + "NPCA_000_010", Q + "SEQ_00" };
            string[] japanese = { JaPrompt, "がんばれ", "がんばれ！", "セリフ" };
            string[] korean = { KoPrompt, "힘내", "힘내! 힘내!", "대사" };
            // The patched page carries a different key on the phrase row; rows are identified by the clean
            // target's keys, and a missing Japanese reference falls back to the target page.
            string[] patchedKeys = { keys[0], "TEXT_PATCHED_KEY", keys[2], keys[3] };
            ExcelHeader header = ExcelHeader.Parse(BuildHeader(keys.Length));
            ExcelDataFile clean = ExcelDataFile.Parse(BuildPage(keys, japanese));
            byte[] patched = BuildPage(patchedKeys, korean);

            List<string> problems = new List<string>();
            SayQuestPhraseResult result;
            byte[] output = SayQuestPhraseLocalizer.ApplyToPage(header, clean, null, patched, out result);
            ExcelDataFile page = ExcelDataFile.Parse(output);
            ExcelDataFile input = ExcelDataFile.Parse(patched);
            string[] expected = { KoPromptAnnotated, "がんばれ", korean[2], korean[3] };
            if (page.Rows.Count != keys.Length)
            {
                problems.Add("row count " + page.Rows.Count);
            }

            for (int i = 0; i < keys.Length && i < page.Rows.Count; i++)
            {
                ExcelDataRow row = page.Rows[i];
                if (!Same(page.GetStringBytesByColumnOffset(row, header, 0), Text(patchedKeys[i])))
                {
                    problems.Add("row " + i + " key changed");
                }

                if (!Same(page.GetStringBytesByColumnOffset(row, header, 4), Text(expected[i])))
                {
                    problems.Add("row " + i + " text is " + Describe(page.GetStringBytesByColumnOffset(row, header, 4)));
                }

                if (Record(page, row).Length % 4 != 0)
                {
                    problems.Add("row " + i + " record is not padded");
                }

                if (i >= 2 && !Same(Record(page, row), Record(input, input.Rows[i])))
                {
                    problems.Add("row " + i + " record was not copied verbatim");
                }
            }

            string[] plainKeys = { Q + "TODO_00", Q + "NPCA_000_010" };
            string[] plain = { "ハイデリンと話す", "セリフ" };
            byte[] untouched = BuildPage(plainKeys, new string[] { "하이델린과 대화", "대사" });
            SayQuestPhraseResult untouchedResult;
            byte[] untouchedOutput = SayQuestPhraseLocalizer.ApplyToPage(
                ExcelHeader.Parse(BuildHeader(plainKeys.Length)),
                ExcelDataFile.Parse(BuildPage(plainKeys, plain)),
                null,
                untouched,
                out untouchedResult);
            if (!ReferenceEquals(untouchedOutput, untouched) || untouchedResult.IsSayQuest)
            {
                problems.Add("a page without say quests was rebuilt");
            }

            Report("page rewrite changes only say rows and keeps others verbatim", problems);
        }

        private static byte[] Record(ExcelDataFile file, ExcelDataRow row)
        {
            int offset = (int)row.Offset;
            int size = 6 + (int)ReadUInt32BE(file.Data, offset);
            byte[] record = new byte[size];
            Buffer.BlockCopy(file.Data, offset, record, 0, size);
            return record;
        }

        // EXH with two string columns: the key at offset 0 and the text at offset 4.
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

        private static byte[] BuildPage(string[] keys, string[] texts)
        {
            List<byte[]> records = new List<byte[]>();
            for (int i = 0; i < keys.Length; i++)
            {
                byte[] key = Text(keys[i]);
                byte[] text = Text(texts[i]);
                List<byte> body = new List<byte>();
                WriteUInt32(body, 0);
                WriteUInt32(body, (uint)(key.Length + 1));
                body.AddRange(key);
                body.Add(0);
                body.AddRange(text);
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
            WriteUInt32(output, (uint)(keys.Length * 8));
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

            uint offset = (uint)(0x20 + keys.Length * 8);
            for (int i = 0; i < records.Count; i++)
            {
                WriteUInt32(output, (uint)i);
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

        private const string JaHello = "チャットの「Say」モードで『こんにちは』と入力しニニヤに挨拶する";
        private const string KoHello = "대화창에서 대화 방식을 '말하기'로 하고 \"안녕하세요.\"라고 입력";

        private static void Expect(string name, List<SayQuestRowText> rows, Expectation expectation, params KeyValuePair<uint, byte[]>[] changes)
        {
            SayQuestPhraseResult result = SayQuestPhraseLocalizer.Localize(rows);
            List<string> problems = new List<string>();
            if (result.IsSayQuest != expectation.IsSayQuest)
            {
                problems.Add("IsSayQuest " + result.IsSayQuest + " (expected " + expectation.IsSayQuest + ")");
            }

            if (result.KeptKorean != expectation.KeptKorean)
            {
                problems.Add("KeptKorean " + result.KeptKorean + " (expected " + expectation.KeptKorean + ")");
            }

            if (!expectation.KeptKorean && expectation.IsSayQuest &&
                (result.PhraseRows != expectation.PhraseRows || result.AnnotatedRows != expectation.AnnotatedRows))
            {
                problems.Add("phrase/annotated rows " + result.PhraseRows + "/" + result.AnnotatedRows +
                             " (expected " + expectation.PhraseRows + "/" + expectation.AnnotatedRows + ")");
            }

            Dictionary<uint, byte[]> expected = new Dictionary<uint, byte[]>();
            for (int i = 0; i < changes.Length; i++)
            {
                expected[changes[i].Key] = changes[i].Value;
            }

            foreach (KeyValuePair<uint, byte[]> change in expected)
            {
                byte[] actual;
                if (!result.Replacements.TryGetValue(change.Key, out actual))
                {
                    problems.Add("row " + change.Key + " not changed; expected " + Describe(change.Value));
                }
                else if (!Same(actual, change.Value))
                {
                    problems.Add("row " + change.Key + " is " + Describe(actual) + "; expected " + Describe(change.Value));
                }
            }

            foreach (uint rowId in result.Replacements.Keys)
            {
                if (!expected.ContainsKey(rowId))
                {
                    problems.Add("row " + rowId + " changed unexpectedly to " + Describe(result.Replacements[rowId]));
                }
            }

            Report(name, problems);
        }

        private sealed class Expectation
        {
            public bool IsSayQuest = true;
            public bool KeptKorean;
            public int PhraseRows;
            public int AnnotatedRows;
        }

        private static Expectation Applied(int phraseRows, int annotatedRows)
        {
            Expectation expectation = new Expectation();
            expectation.PhraseRows = phraseRows;
            expectation.AnnotatedRows = annotatedRows;
            return expectation;
        }

        private static Expectation KeptKorean()
        {
            Expectation expectation = new Expectation();
            expectation.KeptKorean = true;
            return expectation;
        }

        private static Expectation NotSayQuest()
        {
            Expectation expectation = new Expectation();
            expectation.IsSayQuest = false;
            return expectation;
        }

        private static List<SayQuestRowText> Rows(params SayQuestRowText[] rows)
        {
            return new List<SayQuestRowText>(rows);
        }

        private static SayQuestRowText Row(uint rowId, string kind, string japanese, string baseText, string korean)
        {
            return Row(rowId, kind, Text(japanese), Text(baseText), korean == null ? null : Text(korean));
        }

        private static SayQuestRowText Row(uint rowId, string kind, byte[] japanese, byte[] baseText, byte[] korean)
        {
            SayQuestRowText row = new SayQuestRowText();
            row.RowId = rowId;
            row.Key = Q + kind;
            row.Japanese = japanese;
            row.Base = baseText;
            row.Korean = korean;
            return row;
        }

        private static KeyValuePair<uint, byte[]> Change(uint rowId, string text)
        {
            return Change(rowId, Text(text));
        }

        private static KeyValuePair<uint, byte[]> Change(uint rowId, byte[] bytes)
        {
            return new KeyValuePair<uint, byte[]>(rowId, bytes);
        }

        private static byte[] Macro(byte type, byte[] payload)
        {
            List<byte> output = new List<byte> { 0x02, type, (byte)(payload.Length + 1) };
            output.AddRange(payload);
            output.Add(0x03);
            return output.ToArray();
        }

        private static byte[] Text(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        // Concatenates parts; string parts are UTF-8 text.
        private static byte[] Cat(params object[] parts)
        {
            List<byte> output = new List<byte>();
            foreach (object part in parts)
            {
                string text = part as string;
                output.AddRange(text != null ? Text(text) : (byte[])part);
            }

            return output.ToArray();
        }

        private static string Describe(byte[] bytes)
        {
            if (bytes == null)
            {
                return "<null>";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x02 || bytes[i] == 0x03)
                {
                    builder.Append("<").Append(bytes[i].ToString("X2")).Append(">");
                }
                else
                {
                    int start = i;
                    while (i < bytes.Length && bytes[i] != 0x02 && bytes[i] != 0x03)
                    {
                        i++;
                    }

                    builder.Append(Encoding.UTF8.GetString(bytes, start, i - start));
                    i--;
                }
            }

            return "\"" + builder.ToString() + "\"";
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
    }
}
