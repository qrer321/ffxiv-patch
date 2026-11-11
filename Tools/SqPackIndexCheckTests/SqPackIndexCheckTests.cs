using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Regression cases for SqPackIndexCheck, built from synthetic index and dat files.
    // Run through Scripts\test-index-check.ps1.
    internal static class SqPackIndexCheckTests
    {
        private const int PatchDat = 1;
        private const uint FreedRecord = 0;
        private const uint EmptyFile = 1;
        private const uint StandardFile = 2;

        private static string _dir;
        private static int _failures;
        private static int _passed;

        private static int Main(string[] args)
        {
            _dir = args[0];
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }

            Directory.CreateDirectory(_dir);

            // dat0 records: 0x800 and 0x900 are files, 0xA00 was freed by a game update, 0xB00 is an
            // empty placeholder file.
            WriteDat("game.dat0", 0x1000, Record(0x800, StandardFile), Record(0x900, StandardFile), Record(0xA00, FreedRecord), Record(0xB00, EmptyFile));

            foreach (bool index2 in new[] { false, true })
            {
                string kind = index2 ? "index2" : "index";
                ExpectMissing(kind + ": entries at file records",
                    Index("ok", index2, Entry(1, 0, 0x800), Entry(2, 0, 0x900)), 0);
                ExpectMissing(kind + ": entry at a freed record",
                    Index("freed", index2, Entry(1, 0, 0x800), Entry(2, 0, 0xA00)), 1);
                ExpectMissing(kind + ": entry at an empty placeholder file",
                    Index("placeholder", index2, Entry(1, 0, 0x800), Entry(2, 0, 0xB00)), 0);
                ExpectMissing(kind + ": entry past the end of the dat",
                    Index("eof", index2, Entry(1, 0, 0x800), Entry(2, 0, 0x1000)), 1);
                ExpectMissing(kind + ": entry in a dat file that does not exist",
                    Index("nodat", index2, Entry(1, 0, 0x800), Entry(2, PatchDat, 0x800)), 1);
                ExpectMissing(kind + ": synonym entry is not a dat locator",
                    Index("synonym", index2, Entry(1, 0, 0x800), new KeyValuePair<uint, uint>(2, Entry(2, 0, 0xA00).Value | 1)), 0);

                string clean = Index("clean", index2, Entry(1, 0, 0x800), Entry(2, 0, 0x900));
                ExpectUnpatchedForm(kind + ": identical unpatched indexes",
                    clean, Index("same", index2, Entry(1, 0, 0x800), Entry(2, 0, 0x900)), true);
                ExpectUnpatchedForm(kind + ": installed redirects an entry to the patch dat",
                    clean, Index("redirect", index2, Entry(1, 0, 0x800), Entry(2, PatchDat, 0x800)), true);
                ExpectUnpatchedForm(kind + ": installed has an entry the patch added",
                    clean, Index("added", index2, Entry(1, 0, 0x800), Entry(2, PatchDat, 0x800), Entry(3, PatchDat, 0x900)), true);
                ExpectUnpatchedForm(kind + ": a game update moved a file the patch left alone",
                    clean, Index("moved", index2, Entry(1, 0, 0xA00), Entry(2, PatchDat, 0x800)), false);
                ExpectUnpatchedForm(kind + ": a game update added a file",
                    clean, Index("newfile", index2, Entry(1, 0, 0x800), Entry(2, 0, 0x900), Entry(3, 0, 0xA00)), false);
                ExpectUnpatchedForm(kind + ": candidate has an entry the installed index lacks",
                    clean, Index("fewer", index2, Entry(1, 0, 0x800)), false);
                ExpectUnpatchedForm(kind + ": candidate is itself patched",
                    Index("patched", index2, Entry(1, 0, 0x800), Entry(2, PatchDat, 0x800)),
                    Index("patched-installed", index2, Entry(1, 0, 0x800), Entry(2, PatchDat, 0x800)), false);
            }

            ExpectUnpatchedForm("index and index2 are not compared with each other",
                Index("mixed-a", false, Entry(1, 0, 0x800)), Index("mixed-b", true, Entry(1, 0, 0x800)), false);

            Console.WriteLine("{0} passed, {1} failed", _passed, _failures);
            return _failures == 0 ? 0 : 1;
        }

        private static void ExpectMissing(string name, string indexPath, int expected)
        {
            SqPackIndexCheck.DatRecordReport report = SqPackIndexCheck.FindEntriesWithoutDatRecords(indexPath, _dir, "game");
            Check(name, report.Missing == expected && report.Entries == 2,
                "missing " + report.Missing + " of " + report.Entries + " (" + report.FirstMissing + ")");
        }

        private static void ExpectUnpatchedForm(string name, string candidate, string installed, bool expected)
        {
            bool actual = SqPackIndexCheck.IsUnpatchedFormOf(candidate, installed, PatchDat);
            Check(name, actual == expected, "got " + actual);
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                _passed++;
                return;
            }

            _failures++;
            Console.WriteLine("FAIL: {0}: {1}", name, detail);
        }

        private static KeyValuePair<uint, uint> Entry(uint hash, int datId, long offset)
        {
            // Entry data: bits 1-3 are the dat id, the upper bits absoluteOffset / 8.
            return new KeyValuePair<uint, uint>(hash, (uint)(offset / 8) | ((uint)datId << 1));
        }

        private static KeyValuePair<long, uint> Record(long offset, uint fileType)
        {
            return new KeyValuePair<long, uint>(offset, fileType);
        }

        // SqPack header (0x400) + index header (0x400) + entry table.
        private static string Index(string name, bool index2, params KeyValuePair<uint, uint>[] entries)
        {
            int entrySize = index2 ? 8 : 16;
            byte[] bytes = new byte[0x800 + entries.Length * entrySize];
            WriteUInt32(bytes, 0x0C, 0x400);
            WriteUInt32(bytes, 0x408, 0x800);
            WriteUInt32(bytes, 0x40C, (uint)(entries.Length * entrySize));
            for (int i = 0; i < entries.Length; i++)
            {
                int offset = 0x800 + i * entrySize;
                WriteUInt32(bytes, offset, entries[i].Key);
                WriteUInt32(bytes, offset + (index2 ? 4 : 8), entries[i].Value);
            }

            string path = Path.Combine(_dir, name + (index2 ? ".index2" : ".index"));
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void WriteDat(string name, int length, params KeyValuePair<long, uint>[] records)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < records.Length; i++)
            {
                WriteUInt32(bytes, (int)records[i].Key, 0x80);
                WriteUInt32(bytes, (int)records[i].Key + 4, records[i].Value);
            }

            File.WriteAllBytes(Path.Combine(_dir, name), bytes);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
