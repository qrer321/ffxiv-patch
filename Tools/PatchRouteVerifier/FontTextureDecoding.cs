using System;
using System.IO;
using System.IO.Compression;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static byte[] UnpackTextureFile(byte[] packed)
        {
            uint headerSize = ReadU32(packed, 0);
            uint fileType = ReadU32(packed, 4);
            uint decompressedSize = ReadU32(packed, 8);
            uint blockCount = ReadU32(packed, 20);
            if (fileType != 4)
            {
                throw new InvalidDataException("Not a texture entry. Type=" + fileType);
            }

            int locatorOffset = 24;
            int subBlockOffset = checked(locatorOffset + (int)blockCount * 20);
            using (MemoryStream output = new MemoryStream((int)decompressedSize))
            {
                for (int i = 0; i < blockCount; i++)
                {
                    int loc = locatorOffset + i * 20;
                    uint firstBlockOffset = ReadU32(packed, loc);
                    uint decompressedBlockSize = ReadU32(packed, loc + 8);
                    uint firstSubBlockIndex = ReadU32(packed, loc + 12);
                    uint subBlockCount = ReadU32(packed, loc + 16);

                    if (i == 0)
                    {
                        int textureHeaderStart = (int)headerSize;
                        int textureHeaderLength = checked((int)firstBlockOffset);
                        output.Write(packed, textureHeaderStart, textureHeaderLength);
                    }

                    int blockOffset = checked((int)headerSize + (int)firstBlockOffset);
                    long before = output.Length;
                    for (int s = 0; s < subBlockCount; s++)
                    {
                        int blockHeaderOffset = blockOffset;
                        uint blockHeaderSize = ReadU32(packed, blockHeaderOffset);
                        uint compressedSize = ReadU32(packed, blockHeaderOffset + 8);
                        uint rawSize = ReadU32(packed, blockHeaderOffset + 12);
                        if (blockHeaderSize != 16)
                        {
                            throw new InvalidDataException("Unexpected texture block header size.");
                        }

                        if (compressedSize == UncompressedBlock)
                        {
                            output.Write(packed, blockHeaderOffset + 16, (int)rawSize);
                        }
                        else
                        {
                            using (MemoryStream source = new MemoryStream(packed, blockHeaderOffset + 16, (int)compressedSize, false))
                            using (DeflateStream deflate = new DeflateStream(source, CompressionMode.Decompress))
                            {
                                deflate.CopyTo(output);
                            }
                        }

                        ushort paddedBlockSize = ReadU16(packed, subBlockOffset + checked(((int)firstSubBlockIndex + s) * 2));
                        blockOffset += paddedBlockSize;
                    }

                    if (output.Length - before != decompressedBlockSize)
                    {
                        throw new InvalidDataException("Unexpected texture block size.");
                    }
                }

                if (output.Length != decompressedSize)
                {
                    throw new InvalidDataException("Unexpected texture size.");
                }

                return output.ToArray();
            }
        }

        private static Texture ReadA4R4G4B4Texture(byte[] tex)
        {
            uint format = ReadU32(tex, 4);
            if (format != 0x1440)
            {
                throw new InvalidDataException("Unsupported texture format: 0x" + format.ToString("X"));
            }

            int width = ReadU16(tex, 8);
            int height = ReadU16(tex, 10);
            int offset = checked((int)ReadU32(tex, 0x1C));
            byte[] data = new byte[checked(width * height * 2)];
            Buffer.BlockCopy(tex, offset, data, 0, data.Length);
            return new Texture { Width = width, Height = height, Data = data };
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return data[offset] |
                ((uint)data[offset + 1] << 8) |
                ((uint)data[offset + 2] << 16) |
                ((uint)data[offset + 3] << 24);
        }

        private struct Texture
        {
            public int Width;
            public int Height;
            public byte[] Data;
        }
    }
}
