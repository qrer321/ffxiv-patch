using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Checks that an index file belongs to the dat files it is used with. A game update frees or reuses
    // the dat records of the files it changes, so an index kept from before the update sends those
    // paths to empty records or to other files. The client then loads the wrong data (for example an
    // EXD page of another sheet) and can crash.
    //
    // Self-contained: FFXIVPatchUI compiles this file too.
    internal static class SqPackIndexCheck
    {
        internal sealed class DatRecordReport
        {
            public int Entries;
            public int Missing;
            public string FirstMissing;
        }

        // Counts index entries that do not point at a file record in <sqpackDir>\<datPrefix>.dat<N>.
        // Synonym entries (bit 0) are skipped: they locate a row of the index's synonym table, not a
        // dat record.
        public static DatRecordReport FindEntriesWithoutDatRecords(string indexPath, string sqpackDir, string datPrefix)
        {
            List<KeyValuePair<ulong, uint>> entries = ReadEntries(indexPath);
            Dictionary<byte, List<long>> offsetsByDat = new Dictionary<byte, List<long>>();
            for (int i = 0; i < entries.Count; i++)
            {
                if ((entries[i].Value & 0x1u) != 0)
                {
                    continue;
                }

                byte datId = GetDataFileId(entries[i].Value);
                List<long> offsets;
                if (!offsetsByDat.TryGetValue(datId, out offsets))
                {
                    offsets = new List<long>();
                    offsetsByDat.Add(datId, offsets);
                }

                offsets.Add(GetOffset(entries[i].Value));
            }

            DatRecordReport report = new DatRecordReport();
            report.Entries = entries.Count;
            byte[] header = new byte[8];
            foreach (KeyValuePair<byte, List<long>> pair in offsetsByDat)
            {
                string datName = datPrefix + ".dat" + pair.Key.ToString();
                string datPath = Path.Combine(sqpackDir, datName);
                if (!File.Exists(datPath))
                {
                    AddMissing(report, pair.Value.Count, datName + " is missing");
                    continue;
                }

                // Ascending offsets keep the pass sequential on slow disks.
                pair.Value.Sort();
                using (FileStream stream = new FileStream(datPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        long offset = pair.Value[i];
                        if (!IsFileRecord(stream, offset, header))
                        {
                            AddMissing(report, 1, datName + " offset 0x" + offset.ToString("X"));
                        }
                    }
                }
            }

            return report;
        }

        // True when candidate is the installed index without its patch redirects: every entry the
        // installed index keeps in a game dat is identical, and candidate has no other entries.
        // Entries the installed index points at patchDatId are ignored (a patch may also add them).
        // With an unpatched installed index this means both hold the same entries.
        public static bool IsUnpatchedFormOf(string candidateIndexPath, string installedIndexPath, int patchDatId)
        {
            if (IsIndex2(candidateIndexPath) != IsIndex2(installedIndexPath))
            {
                return false;
            }

            List<KeyValuePair<ulong, uint>> candidateEntries = ReadEntries(candidateIndexPath);
            Dictionary<ulong, uint> candidate = new Dictionary<ulong, uint>(candidateEntries.Count);
            for (int i = 0; i < candidateEntries.Count; i++)
            {
                if (GetDataFileId(candidateEntries[i].Value) == patchDatId)
                {
                    return false;
                }

                candidate[candidateEntries[i].Key] = candidateEntries[i].Value;
            }

            List<KeyValuePair<ulong, uint>> installed = ReadEntries(installedIndexPath);
            HashSet<ulong> matched = new HashSet<ulong>();
            for (int i = 0; i < installed.Count; i++)
            {
                bool redirected = GetDataFileId(installed[i].Value) == patchDatId;
                uint candidateData;
                if (!candidate.TryGetValue(installed[i].Key, out candidateData))
                {
                    if (!redirected)
                    {
                        return false;
                    }

                    continue;
                }

                if (!redirected && candidateData != installed[i].Value)
                {
                    return false;
                }

                matched.Add(installed[i].Key);
            }

            return matched.Count == candidate.Count;
        }

        private static bool IsFileRecord(FileStream stream, long offset, byte[] header)
        {
            if (offset < 0 || offset > stream.Length - 24)
            {
                return false;
            }

            stream.Position = offset;
            int total = 0;
            while (total < header.Length)
            {
                int read = stream.Read(header, total, header.Length - total);
                if (read == 0)
                {
                    return false;
                }

                total += read;
            }

            uint headerSize = ReadUInt32(header, 0);
            uint fileType = ReadUInt32(header, 4);
            // A record the game freed keeps a 0x80 header with type 0. File records are 1 (empty
            // placeholder, e.g. other regions' fonts in the global common/font), 2 (standard),
            // 3 (model) and 4 (texture).
            return headerSize != 0 && (headerSize % 0x80) == 0 && fileType >= 1 && fileType <= 4;
        }

        private static void AddMissing(DatRecordReport report, int count, string example)
        {
            report.Missing += count;
            if (report.FirstMissing == null)
            {
                report.FirstMissing = example;
            }
        }

        // Returns (hash, data) for every file entry. index: 16-byte entries with a 64-bit hash;
        // index2: 8-byte entries with a 32-bit hash.
        private static List<KeyValuePair<ulong, uint>> ReadEntries(string indexPath)
        {
            byte[] bytes = File.ReadAllBytes(indexPath);
            if (bytes.Length < 0x10)
            {
                throw new InvalidDataException("Index file is too short: " + indexPath);
            }

            long indexHeaderOffset = ReadUInt32(bytes, 0x0C);
            if (indexHeaderOffset + 0x10 > bytes.Length)
            {
                throw new InvalidDataException("Index header is out of range: " + indexPath);
            }

            long dataOffset = ReadUInt32(bytes, (int)indexHeaderOffset + 0x08);
            long dataSize = ReadUInt32(bytes, (int)indexHeaderOffset + 0x0C);
            if (dataOffset + dataSize > bytes.Length)
            {
                throw new InvalidDataException("Index entry table is out of range: " + indexPath);
            }

            bool index2 = IsIndex2(indexPath);
            int entrySize = index2 ? 8 : 16;
            int count = (int)(dataSize / entrySize);
            List<KeyValuePair<ulong, uint>> entries = new List<KeyValuePair<ulong, uint>>(count);
            for (int i = 0; i < count; i++)
            {
                int entryOffset = (int)dataOffset + i * entrySize;
                ulong hash = index2
                    ? ReadUInt32(bytes, entryOffset)
                    : ReadUInt32(bytes, entryOffset) | ((ulong)ReadUInt32(bytes, entryOffset + 4) << 32);
                entries.Add(new KeyValuePair<ulong, uint>(hash, ReadUInt32(bytes, entryOffset + (index2 ? 4 : 8))));
            }

            return entries;
        }

        private static bool IsIndex2(string indexPath)
        {
            return indexPath.EndsWith(".index2", StringComparison.OrdinalIgnoreCase);
        }

        // Entry data: bit 0 is a flag, bits 1-3 the dat id, the rest absoluteOffset / 8.
        private static byte GetDataFileId(uint data)
        {
            return (byte)((data & 0xEu) >> 1);
        }

        private static long GetOffset(uint data)
        {
            return (long)(data & 0xFFFFFFF0u) * 8L;
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }
    }
}
