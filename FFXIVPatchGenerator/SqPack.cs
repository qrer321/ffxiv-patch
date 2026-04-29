using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class SqPackArchive : IDisposable
    {
        private readonly string _sqpackDir;
        private readonly string _datPrefix;
        private readonly SqPackIndexFile _index;
        private readonly Dictionary<byte, FileStream> _datStreams = new Dictionary<byte, FileStream>();

        public SqPackArchive(string indexPath, string sqpackDir, string datPrefix)
        {
            _sqpackDir = sqpackDir;
            _datPrefix = datPrefix;
            _index = new SqPackIndexFile(indexPath);
        }

        public bool TryReadFile(string gamePath, out byte[] data)
        {
            SqPackIndexEntry entry;
            if (!_index.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out entry))
            {
                data = null;
                return false;
            }

            data = ReadFile(entry);
            return true;
        }

        public byte[] ReadFile(string gamePath)
        {
            byte[] data;
            if (!TryReadFile(gamePath, out data))
            {
                throw new FileNotFoundException("SqPack file was not found: " + gamePath);
            }

            return data;
        }

        public bool TryReadPackedFile(string gamePath, out byte[] data)
        {
            SqPackIndexEntry entry;
            if (!_index.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out entry))
            {
                data = null;
                return false;
            }

            FileStream stream = GetDatStream(entry.DataFileId);
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

        private byte[] ReadFile(SqPackIndexEntry entry)
        {
            FileStream stream = GetDatStream(entry.DataFileId);
            lock (stream)
            {
                BinaryReader reader = new BinaryReader(stream);
                stream.Position = entry.Offset;

                uint headerSize = reader.ReadUInt32();
                uint fileType = reader.ReadUInt32();
                uint rawFileSize = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                uint blockCount = reader.ReadUInt32();

                if (fileType != 2)
                {
                    throw new InvalidDataException("Only standard SqPack files are supported. Type=" + fileType);
                }

                List<DatBlockInfo> blocks = new List<DatBlockInfo>();
                for (int i = 0; i < blockCount; i++)
                {
                    DatBlockInfo block = new DatBlockInfo();
                    block.Offset = reader.ReadUInt32();
                    block.CompressedSize = reader.ReadUInt16();
                    block.UncompressedSize = reader.ReadUInt16();
                    blocks.Add(block);
                }

                MemoryStream output = new MemoryStream((int)rawFileSize);
                for (int i = 0; i < blocks.Count; i++)
                {
                    stream.Position = entry.Offset + headerSize + blocks[i].Offset;
                    ReadDataBlock(reader, stream, output);
                }

                byte[] result = output.ToArray();
                if (result.Length != rawFileSize)
                {
                    throw new InvalidDataException("Unexpected decompressed size. Expected " + rawFileSize + ", got " + result.Length);
                }

                return result;
            }
        }

        private static void ReadDataBlock(BinaryReader reader, Stream stream, Stream output)
        {
            uint blockHeaderSize = reader.ReadUInt32();
            reader.ReadUInt32();
            uint blockTypeOrCompressedSize = reader.ReadUInt32();
            uint blockDataSize = reader.ReadUInt32();

            if (blockHeaderSize != 16)
            {
                throw new InvalidDataException("Unexpected SqPack block header size: " + blockHeaderSize);
            }

            if (blockTypeOrCompressedSize == DatBlockTypes.Uncompressed)
            {
                byte[] buffer = reader.ReadBytes((int)blockDataSize);
                output.Write(buffer, 0, buffer.Length);
                return;
            }

            if (blockTypeOrCompressedSize > 0)
            {
                byte[] inflated = new byte[blockDataSize];
                int totalRead = 0;
                using (DeflateStream deflate = new DeflateStream(stream, CompressionMode.Decompress, true))
                {
                    while (totalRead < inflated.Length)
                    {
                        int read = deflate.Read(inflated, totalRead, inflated.Length - totalRead);
                        if (read == 0)
                        {
                            break;
                        }

                        totalRead += read;
                    }
                }

                if (totalRead != inflated.Length)
                {
                    throw new InvalidDataException("Failed to inflate SqPack block.");
                }

                output.Write(inflated, 0, inflated.Length);
                return;
            }

            throw new InvalidDataException("Unknown SqPack block type/size: " + blockTypeOrCompressedSize);
        }

        private FileStream GetDatStream(byte datId)
        {
            FileStream stream;
            if (_datStreams.TryGetValue(datId, out stream))
            {
                return stream;
            }

            string path = Path.Combine(_sqpackDir, _datPrefix + ".dat" + datId.ToString());
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("SqPack dat file is missing.", path);
            }

            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _datStreams.Add(datId, stream);
            return stream;
        }

        public void Dispose()
        {
            _index.Dispose();
            foreach (FileStream stream in _datStreams.Values)
            {
                stream.Dispose();
            }
        }
    }

    internal sealed class SqPackIndexFile : IDisposable
    {
        private readonly string _path;
        private readonly byte[] _bytes;
        private readonly Dictionary<ulong, SqPackIndexEntry> _entries = new Dictionary<ulong, SqPackIndexEntry>();
        private readonly int _sqpackHeaderSize;
        private readonly int _indexHeaderOffset;

        public SqPackIndexFile(string path)
        {
            _path = path;
            _bytes = File.ReadAllBytes(path);
            _sqpackHeaderSize = (int)Endian.ReadUInt32LE(_bytes, 0x0C);
            _indexHeaderOffset = _sqpackHeaderSize;

            uint indexDataOffset = Endian.ReadUInt32LE(_bytes, _indexHeaderOffset + 0x08);
            uint indexDataSize = Endian.ReadUInt32LE(_bytes, _indexHeaderOffset + 0x0C);
            int entryCount = (int)(indexDataSize / 16);

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = (int)indexDataOffset + i * 16;
                ulong hash = Endian.ReadUInt64LE(_bytes, entryOffset);
                uint data = Endian.ReadUInt32LE(_bytes, entryOffset + 8);

                SqPackIndexEntry entry = new SqPackIndexEntry();
                entry.Hash = hash;
                entry.Data = data;
                entry.EntryOffset = entryOffset;
                _entries[hash] = entry;
            }
        }

        public bool TryGetEntry(ulong hash, out SqPackIndexEntry entry)
        {
            return _entries.TryGetValue(hash, out entry);
        }

        public bool ContainsPath(string gamePath)
        {
            return _entries.ContainsKey(SqPackHash.GetIndexHash(gamePath));
        }

        public void SetFileOffset(string gamePath, byte datId, long absoluteOffset)
        {
            if (datId > 7)
            {
                throw new ArgumentOutOfRangeException("datId");
            }

            if (absoluteOffset < 0 || (absoluteOffset % 0x80) != 0)
            {
                throw new ArgumentException("SqPack file offsets must be 0x80-aligned.");
            }

            ulong hash = SqPackHash.GetIndexHash(gamePath);
            SqPackIndexEntry entry;
            if (!_entries.TryGetValue(hash, out entry))
            {
                throw new FileNotFoundException("Target index entry was not found: " + gamePath);
            }

            uint encodedOffset = checked((uint)(absoluteOffset / 8));
            uint flags = (entry.Data & 0x1u) | ((uint)datId << 1);
            uint data = (encodedOffset & 0xFFFFFFF0u) | flags;

            Endian.WriteUInt32LE(_bytes, entry.EntryOffset + 8, data);
            entry.Data = data;
            _entries[hash] = entry;
        }

        public void EnsureDataFileCount(uint count)
        {
            int offset = _indexHeaderOffset + 0x50;
            uint existing = Endian.ReadUInt32LE(_bytes, offset);
            if (existing < count)
            {
                Endian.WriteUInt32LE(_bytes, offset, count);
            }
        }

        public Dictionary<byte, int> CountEntriesByDataFile()
        {
            Dictionary<byte, int> counts = new Dictionary<byte, int>();
            foreach (SqPackIndexEntry entry in _entries.Values)
            {
                byte datId = entry.DataFileId;
                if (!counts.ContainsKey(datId))
                {
                    counts[datId] = 0;
                }

                counts[datId]++;
            }

            return counts;
        }

        public long GetNextOffset(byte dataFileId, long currentOffset, long datLength)
        {
            long next = datLength;
            foreach (SqPackIndexEntry entry in _entries.Values)
            {
                if (entry.DataFileId == dataFileId && entry.Offset > currentOffset && entry.Offset < next)
                {
                    next = entry.Offset;
                }
            }

            return next;
        }

        public void Save()
        {
            File.WriteAllBytes(_path, _bytes);
        }

        public void Dispose()
        {
        }
    }

    internal sealed class SqPackDatWriter : IDisposable
    {
        private const int Alignment = 0x80;
        private const int MaxBlockSize = 16000;

        private readonly FileStream _stream;

        public SqPackDatWriter(string outputPath, string sourceDat0Path)
        {
            byte[] header = ReadSqPackHeader(sourceDat0Path);
            _stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _stream.Write(header, 0, header.Length);
            PadToAlignment(_stream, Alignment);
        }

        public long WriteStandardFile(byte[] rawData)
        {
            PadToAlignment(_stream, Alignment);
            long fileOffset = _stream.Position;

            int blockCount = Math.Max(1, (rawData.Length + MaxBlockSize - 1) / MaxBlockSize);
            int fileHeaderSize = Align(24 + blockCount * 8, Alignment);

            List<PendingBlock> pendingBlocks = new List<PendingBlock>();
            int sourceOffset = 0;
            int blockDataOffset = 0;
            for (int i = 0; i < blockCount; i++)
            {
                int length = Math.Min(MaxBlockSize, rawData.Length - sourceOffset);
                if (length < 0)
                {
                    length = 0;
                }

                byte[] payload = CreatePayload(rawData, sourceOffset, length);
                bool compressed = payload.Length < length;
                int storedSize = Align(16 + payload.Length, Alignment);

                PendingBlock block = new PendingBlock();
                block.Offset = blockDataOffset;
                block.Payload = payload;
                block.RawOffset = sourceOffset;
                block.RawLength = length;
                block.Compressed = compressed;
                block.StoredSize = storedSize;
                pendingBlocks.Add(block);

                sourceOffset += length;
                blockDataOffset += storedSize;
            }

            BinaryWriter writer = new BinaryWriter(_stream);
            writer.Write((uint)fileHeaderSize);
            writer.Write((uint)2);
            writer.Write((uint)rawData.Length);
            writer.Write((uint)0);
            writer.Write((uint)(blockDataOffset / Alignment));
            writer.Write((uint)blockCount);

            for (int i = 0; i < pendingBlocks.Count; i++)
            {
                PendingBlock block = pendingBlocks[i];
                writer.Write((uint)block.Offset);
                writer.Write((ushort)block.StoredSize);
                writer.Write((ushort)block.RawLength);
            }

            PadToPosition(_stream, fileOffset + fileHeaderSize);

            for (int i = 0; i < pendingBlocks.Count; i++)
            {
                PendingBlock block = pendingBlocks[i];
                long blockStart = _stream.Position;

                writer.Write((uint)16);
                writer.Write((uint)0);
                writer.Write(block.Compressed ? (uint)block.Payload.Length : DatBlockTypes.Uncompressed);
                writer.Write((uint)block.RawLength);

                if (block.Compressed)
                {
                    writer.Write(block.Payload);
                }
                else if (block.RawLength > 0)
                {
                    writer.Write(rawData, block.RawOffset, block.RawLength);
                }

                PadToPosition(_stream, blockStart + block.StoredSize);
            }

            return fileOffset;
        }

        public long WritePackedFile(byte[] packedFile)
        {
            if (packedFile == null || packedFile.Length == 0)
            {
                throw new ArgumentException("Packed file must not be empty.");
            }

            PadToAlignment(_stream, Alignment);
            long fileOffset = _stream.Position;
            _stream.Write(packedFile, 0, packedFile.Length);
            return fileOffset;
        }

        private static byte[] CreatePayload(byte[] rawData, int offset, int length)
        {
            if (length <= 0)
            {
                return new byte[0];
            }

            MemoryStream memory = new MemoryStream();
            using (DeflateStream deflate = new DeflateStream(memory, CompressionLevel.Optimal, true))
            {
                deflate.Write(rawData, offset, length);
            }

            byte[] compressed = memory.ToArray();
            if (compressed.Length < length)
            {
                return compressed;
            }

            byte[] raw = new byte[length];
            Buffer.BlockCopy(rawData, offset, raw, 0, length);
            return raw;
        }

        private static byte[] ReadSqPackHeader(string sourceDat0Path)
        {
            using (FileStream stream = new FileStream(sourceDat0Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                stream.Position = 0;
                byte[] header = reader.ReadBytes(0x800);
                if (header.Length != 0x800)
                {
                    throw new InvalidDataException("SqPack dat header is shorter than expected.");
                }

                Endian.WriteUInt32LE(header, 0x400 + 0x10, 2);
                return header;
            }
        }

        private static int Align(int value, int alignment)
        {
            int remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        private static void PadToAlignment(Stream stream, int alignment)
        {
            long remainder = stream.Position % alignment;
            if (remainder != 0)
            {
                PadToPosition(stream, stream.Position + alignment - remainder);
            }
        }

        private static void PadToPosition(Stream stream, long targetPosition)
        {
            while (stream.Position < targetPosition)
            {
                stream.WriteByte(0);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private sealed class PendingBlock
        {
            public int Offset;
            public int RawOffset;
            public int RawLength;
            public int StoredSize;
            public bool Compressed;
            public byte[] Payload;
        }
    }

    internal struct SqPackIndexEntry
    {
        public ulong Hash;
        public uint Data;
        public int EntryOffset;

        public byte DataFileId
        {
            get { return (byte)((Data & 0xEu) >> 1); }
        }

        public long Offset
        {
            get { return (long)(Data & 0xFFFFFFF0u) * 8L; }
        }
    }

    internal struct DatBlockInfo
    {
        public uint Offset;
        public ushort CompressedSize;
        public ushort UncompressedSize;
    }

    internal static class DatBlockTypes
    {
        public const uint Uncompressed = 32000;
    }
}
