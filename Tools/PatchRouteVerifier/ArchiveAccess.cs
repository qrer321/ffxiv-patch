using System;
using System.Collections.Generic;
using System.IO;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed class CompositeArchive : IDisposable
        {
            private readonly SqPackIndexFile _index;
            private readonly SqPackIndexFile _baselineIndex;
            private readonly string _datDir;
            private readonly string _fallbackDatDir;
            private readonly string _datPrefix;
            private readonly Dictionary<string, FileStream> _streams = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<byte, List<SqPackIndexEntry>> _primaryEntriesByDat = new Dictionary<byte, List<SqPackIndexEntry>>();

            public string CacheKey { get; private set; }

            public CompositeArchive(string indexPath, string datDir, string fallbackDatDir, string datPrefix)
            {
                if (!File.Exists(indexPath))
                {
                    throw new FileNotFoundException("index file was not found", indexPath);
                }

                _index = new SqPackIndexFile(indexPath);
                _datDir = datDir;
                _fallbackDatDir = fallbackDatDir;
                _datPrefix = datPrefix;
                CacheKey = Path.GetFullPath(indexPath);

                string baselineIndexPath = ResolveBaselineIndexPath(indexPath);
                if (baselineIndexPath != null)
                {
                    _baselineIndex = new SqPackIndexFile(baselineIndexPath);
                }

                BuildPrimaryEntryMap();
            }

            public byte[] ReadFile(string gamePath)
            {
                byte[] packed;
                if (!TryReadPackedFile(gamePath, out packed))
                {
                    throw new FileNotFoundException("SqPack file was not found", gamePath);
                }

                return SqPackArchive.UnpackStandardFile(packed);
            }

            public bool ContainsPath(string gamePath)
            {
                return _index.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out _);
            }

            public bool TryReadPackedFile(string gamePath, out byte[] data)
            {
                SqPackIndexEntry entry;
                if (!_index.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out entry))
                {
                    data = null;
                    return false;
                }

                bool usePrimary = ShouldReadFromPrimary(entry);
                FileStream stream = GetStream(entry.DataFileId, usePrimary);
                lock (stream)
                {
                    long nextOffset = usePrimary
                        ? GetNextPrimaryOffset(entry.DataFileId, entry.Offset, stream.Length)
                        : GetNextFallbackOffset(entry, stream.Length);
                    long length = nextOffset - entry.Offset;
                    if (length <= 0 || length > int.MaxValue)
                    {
                        throw new InvalidDataException("Invalid packed SqPack file length for " + gamePath);
                    }

                    byte[] bytes = new byte[(int)length];
                    stream.Position = entry.Offset;
                    int totalRead = 0;
                    while (totalRead < bytes.Length)
                    {
                        int read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                        if (read == 0)
                        {
                            break;
                        }

                        totalRead += read;
                    }

                    if (totalRead != bytes.Length)
                    {
                        throw new EndOfStreamException("Could not read packed SqPack file: " + gamePath);
                    }

                    data = bytes;
                    return true;
                }
            }

            private static string ResolveBaselineIndexPath(string indexPath)
            {
                if (string.IsNullOrWhiteSpace(indexPath))
                {
                    return null;
                }

                string directory = Path.GetDirectoryName(Path.GetFullPath(indexPath));
                string fileName = Path.GetFileName(indexPath);
                if (string.IsNullOrEmpty(directory) ||
                    string.IsNullOrEmpty(fileName) ||
                    fileName.StartsWith("orig.", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string baseline = Path.Combine(directory, "orig." + fileName);
                return File.Exists(baseline) ? baseline : null;
            }

            private void BuildPrimaryEntryMap()
            {
                List<SqPackIndexEntry> entries = _index.GetEntries();
                for (int i = 0; i < entries.Count; i++)
                {
                    SqPackIndexEntry entry = entries[i];
                    if (!ShouldReadFromPrimary(entry))
                    {
                        continue;
                    }

                    List<SqPackIndexEntry> byDat;
                    if (!_primaryEntriesByDat.TryGetValue(entry.DataFileId, out byDat))
                    {
                        byDat = new List<SqPackIndexEntry>();
                        _primaryEntriesByDat.Add(entry.DataFileId, byDat);
                    }

                    byDat.Add(entry);
                }

                foreach (List<SqPackIndexEntry> byDat in _primaryEntriesByDat.Values)
                {
                    byDat.Sort(CompareIndexEntryOffset);
                }
            }

            private static int CompareIndexEntryOffset(SqPackIndexEntry left, SqPackIndexEntry right)
            {
                return left.Offset.CompareTo(right.Offset);
            }

            private bool ShouldReadFromPrimary(SqPackIndexEntry entry)
            {
                if (_baselineIndex != null)
                {
                    SqPackIndexEntry baselineEntry;
                    return !_baselineIndex.TryGetEntry(entry.Hash, out baselineEntry) ||
                           baselineEntry.Data != entry.Data;
                }

                return !string.Equals(_datPrefix, FontPrefix, StringComparison.OrdinalIgnoreCase) ||
                       PrimaryDatContainsOffset(entry.DataFileId, entry.Offset);
            }

            private bool PrimaryDatContainsOffset(byte datId, long offset)
            {
                if (offset < 0)
                {
                    return false;
                }

                string path = Path.Combine(_datDir, _datPrefix + ".dat" + datId.ToString());
                if (!File.Exists(path))
                {
                    return false;
                }

                long length = new FileInfo(path).Length;
                return offset < length;
            }

            private long GetNextPrimaryOffset(byte dataFileId, long currentOffset, long datLength)
            {
                long next = datLength;
                List<SqPackIndexEntry> entries;
                if (!_primaryEntriesByDat.TryGetValue(dataFileId, out entries))
                {
                    return next;
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    long offset = entries[i].Offset;
                    if (offset > currentOffset && offset < next)
                    {
                        next = offset;
                    }
                }

                return next;
            }

            private long GetNextFallbackOffset(SqPackIndexEntry entry, long datLength)
            {
                if (_baselineIndex != null)
                {
                    SqPackIndexEntry baselineEntry;
                    if (_baselineIndex.TryGetEntry(entry.Hash, out baselineEntry))
                    {
                        return _baselineIndex.GetNextOffset(baselineEntry.DataFileId, baselineEntry.Offset, datLength);
                    }
                }

                return _index.GetNextOffset(entry.DataFileId, entry.Offset, datLength);
            }

            private FileStream GetStream(byte datId, bool usePrimary)
            {
                string path = Path.Combine(usePrimary ? _datDir : _fallbackDatDir, _datPrefix + ".dat" + datId.ToString());
                if (usePrimary && !File.Exists(path))
                {
                    path = Path.Combine(_fallbackDatDir, _datPrefix + ".dat" + datId.ToString());
                }

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("SqPack dat file is missing", path);
                }

                FileStream stream;
                if (_streams.TryGetValue(path, out stream))
                {
                    return stream;
                }

                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                _streams.Add(path, stream);
                return stream;
            }

            public void Dispose()
            {
                _index.Dispose();
                if (_baselineIndex != null)
                {
                    _baselineIndex.Dispose();
                }

                foreach (FileStream stream in _streams.Values)
                {
                    stream.Dispose();
                }
            }
        }
    }
}
