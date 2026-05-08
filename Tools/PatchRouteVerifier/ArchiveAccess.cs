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
            private readonly string _datDir;
            private readonly string _fallbackDatDir;
            private readonly string _datPrefix;
            private readonly Dictionary<byte, FileStream> _streams = new Dictionary<byte, FileStream>();

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

            public bool TryReadPackedFile(string gamePath, out byte[] data)
            {
                SqPackIndexEntry entry;
                if (!_index.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out entry))
                {
                    data = null;
                    return false;
                }

                FileStream stream = GetStream(entry.DataFileId);
                lock (stream)
                {
                    long nextOffset = _index.GetNextOffset(entry.DataFileId, entry.Offset, stream.Length);
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

            private FileStream GetStream(byte datId)
            {
                FileStream stream;
                if (_streams.TryGetValue(datId, out stream))
                {
                    return stream;
                }

                string path = Path.Combine(_datDir, _datPrefix + ".dat" + datId.ToString());
                if (!File.Exists(path))
                {
                    path = Path.Combine(_fallbackDatDir, _datPrefix + ".dat" + datId.ToString());
                }

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("SqPack dat file is missing", path);
                }

                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                _streams.Add(datId, stream);
                return stream;
            }

            public void Dispose()
            {
                _index.Dispose();
                foreach (FileStream stream in _streams.Values)
                {
                    stream.Dispose();
                }
            }
        }
    }
}
