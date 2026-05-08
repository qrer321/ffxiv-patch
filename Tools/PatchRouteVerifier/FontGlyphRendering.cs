using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const string Font1TexturePath = "common/font/font1.tex";
        private const string Font2TexturePath = "common/font/font2.tex";
        private const string Font3TexturePath = "common/font/font3.tex";
        private const string Font4TexturePath = "common/font/font4.tex";
        private const string Font5TexturePath = "common/font/font5.tex";
        private const string Font6TexturePath = "common/font/font6.tex";
        private const string Font7TexturePath = "common/font/font7.tex";
        private const string FontLobby1TexturePath = "common/font/font_lobby1.tex";
        private const string FontLobby2TexturePath = "common/font/font_lobby2.tex";
        private const string FontLobby3TexturePath = "common/font/font_lobby3.tex";
        private const string FontLobby4TexturePath = "common/font/font_lobby4.tex";
        private const string FontLobby5TexturePath = "common/font/font_lobby5.tex";
        private const string FontLobby6TexturePath = "common/font/font_lobby6.tex";
        private const string FontLobby7TexturePath = "common/font/font_lobby7.tex";
        private const string FontKrnTexturePath = "common/font/font_krn_1.tex";
        private const int FdtHeaderSize = 0x20;
        private const int FdtFontTableHeaderSize = 0x20;
        private const int FdtGlyphEntrySize = 0x10;
        private const int FdtKerningHeaderSize = 0x10;
        private const int FdtKerningEntrySize = 0x10;
        private const int GlyphCanvasSize = 96;

        private sealed partial class Verifier
        {
            private GlyphCanvas RenderGlyph(CompositeArchive archive, string fdtPath, uint codepoint)
            {
                byte[] fdt = archive.ReadFile(fdtPath);
                FdtGlyphEntry glyph;
                if (!TryFindGlyph(fdt, codepoint, out glyph))
                {
                    throw new InvalidDataException(fdtPath + " is missing U+" + codepoint.ToString("X4"));
                }

                string texturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    throw new InvalidDataException("Could not resolve texture for " + fdtPath);
                }

                string cacheKey = archive.CacheKey + "|" + texturePath;
                byte[] rawTexture;
                if (!_textureCache.TryGetValue(cacheKey, out rawTexture))
                {
                    byte[] packed;
                    if (!archive.TryReadPackedFile(texturePath, out packed))
                    {
                        throw new FileNotFoundException("texture was not found", texturePath);
                    }

                    rawTexture = UnpackTextureFile(packed);
                    _textureCache.Add(cacheKey, rawTexture);
                }

                Texture texture = ReadA4R4G4B4Texture(rawTexture);
                byte[] alpha = RenderGlyphAlpha(texture, glyph);
                int visible = 0;
                for (int i = 0; i < alpha.Length; i++)
                {
                    if (alpha[i] != 0)
                    {
                        visible++;
                    }
                }

                return new GlyphCanvas
                {
                    Alpha = alpha,
                    VisiblePixels = visible,
                    Glyph = glyph,
                    TexturePath = texturePath
                };
            }
        }

        private static byte[] RenderGlyphAlpha(Texture texture, FdtGlyphEntry glyph)
        {
            byte[] canvas = new byte[GlyphCanvasSize * GlyphCanvasSize];
            int channel = glyph.ImageIndex % 4;
            int startX = 32 + glyph.OffsetX;
            int startY = 32 + glyph.OffsetY;
            for (int y = 0; y < glyph.Height; y++)
            {
                int dy = startY + y;
                for (int x = 0; x < glyph.Width; x++)
                {
                    int dx = startX + x;
                    if (dx < 0 || dy < 0 || dx >= GlyphCanvasSize || dy >= GlyphCanvasSize)
                    {
                        continue;
                    }

                    int sourceX = glyph.X + x;
                    int sourceY = glyph.Y + y;
                    if (sourceX < 0 || sourceY < 0 || sourceX >= texture.Width || sourceY >= texture.Height)
                    {
                        throw new InvalidDataException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "glyph source outside texture: image={0}, source=({1},{2}), texture={3}x{4}",
                                glyph.ImageIndex,
                                sourceX,
                                sourceY,
                                texture.Width,
                                texture.Height));
                    }

                    int pixelOffset = (sourceY * texture.Width + sourceX) * 2;
                    if (pixelOffset < 0 || pixelOffset + 1 >= texture.Data.Length)
                    {
                        throw new InvalidDataException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "glyph source outside texture data: image={0}, offset={1}, bytes={2}",
                                glyph.ImageIndex,
                                pixelOffset,
                                texture.Data.Length));
                    }

                    byte lo = texture.Data[pixelOffset];
                    byte hi = texture.Data[pixelOffset + 1];
                    byte nibble;
                    switch (channel)
                    {
                        case 0: nibble = (byte)(hi & 0x0F); break;
                        case 1: nibble = (byte)((lo >> 4) & 0x0F); break;
                        case 2: nibble = (byte)(lo & 0x0F); break;
                        default: nibble = (byte)((hi >> 4) & 0x0F); break;
                    }

                    canvas[dy * GlyphCanvasSize + dx] = (byte)(nibble * 17);
                }
            }

            return canvas;
        }

        private static bool GlyphSpacingMetricsMatch(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
        {
            return sourceGlyph.ShiftJisValue == targetGlyph.ShiftJisValue &&
                   sourceGlyph.Width == targetGlyph.Width &&
                   sourceGlyph.Height == targetGlyph.Height &&
                   sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                   sourceGlyph.OffsetY == targetGlyph.OffsetY;
        }

        private static string FormatGlyphSpacing(FdtGlyphEntry glyph)
        {
            return "sjis=0x" + glyph.ShiftJisValue.ToString("X4") +
                ", size=" + glyph.Width.ToString() + "x" + glyph.Height.ToString() +
                ", offset=" + glyph.OffsetX.ToString() + "/" + glyph.OffsetY.ToString();
        }

        private static Dictionary<string, byte[]> ReadAsciiKerningEntries(byte[] fdt)
        {
            Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (fdt == null || fdt.Length < FdtHeaderSize)
            {
                return entries;
            }

            uint headerOffset = Endian.ReadUInt32LE(fdt, 0x0C);
            if (headerOffset == 0 ||
                headerOffset > int.MaxValue ||
                headerOffset < FdtHeaderSize ||
                headerOffset > fdt.Length - FdtKerningHeaderSize)
            {
                return entries;
            }

            int kerningHeaderOffset = checked((int)headerOffset);
            if (!HasAsciiMagic(fdt, kerningHeaderOffset, "knhd"))
            {
                return entries;
            }

            uint kerningCount = Endian.ReadUInt32LE(fdt, kerningHeaderOffset + 0x04);
            int kerningStart = kerningHeaderOffset + FdtKerningHeaderSize;
            long kerningBytes = (long)kerningCount * FdtKerningEntrySize;
            if (kerningBytes < 0 || kerningStart > fdt.Length || kerningStart + kerningBytes > fdt.Length)
            {
                return entries;
            }

            for (int i = 0; i < kerningCount; i++)
            {
                int offset = kerningStart + i * FdtKerningEntrySize;
                uint left = Endian.ReadUInt32LE(fdt, offset);
                uint right = Endian.ReadUInt32LE(fdt, offset + 4);
                if (!IsAsciiKerningKey(left) || !IsAsciiKerningKey(right))
                {
                    continue;
                }

                byte[] entry = new byte[FdtKerningEntrySize];
                Buffer.BlockCopy(fdt, offset, entry, 0, FdtKerningEntrySize);
                entries[left.ToString("X2") + ":" + right.ToString("X2")] = entry;
            }

            return entries;
        }

        private static bool IsAsciiKerningKey(uint value)
        {
            return value >= 0x20 && value <= 0x7E;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
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

        private static bool TryFindGlyph(byte[] fdt, uint codepoint, out FdtGlyphEntry glyph)
        {
            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                glyph = new FdtGlyphEntry();
                return false;
            }

            uint utf8Value = PackUtf8(codepoint);
            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                if (Endian.ReadUInt32LE(fdt, offset) == utf8Value)
                {
                    glyph = new FdtGlyphEntry
                    {
                        ShiftJisValue = Endian.ReadUInt16LE(fdt, offset + 4),
                        ImageIndex = Endian.ReadUInt16LE(fdt, offset + 6),
                        X = Endian.ReadUInt16LE(fdt, offset + 8),
                        Y = Endian.ReadUInt16LE(fdt, offset + 10),
                        Width = fdt[offset + 12],
                        Height = fdt[offset + 13],
                        OffsetX = unchecked((sbyte)fdt[offset + 14]),
                        OffsetY = unchecked((sbyte)fdt[offset + 15])
                    };
                    return true;
                }
            }

            glyph = new FdtGlyphEntry();
            return false;
        }

        private static bool TryGetFdtGlyphTable(byte[] fdt, out int fontTableOffset, out uint glyphCount, out int glyphStart)
        {
            fontTableOffset = 0;
            glyphCount = 0;
            glyphStart = 0;
            if (fdt == null || fdt.Length < FdtHeaderSize + FdtFontTableHeaderSize)
            {
                return false;
            }

            uint fontTableHeaderOffset = Endian.ReadUInt32LE(fdt, 0x08);
            if (fontTableHeaderOffset >= fdt.Length || fontTableHeaderOffset > int.MaxValue)
            {
                return false;
            }

            fontTableOffset = checked((int)fontTableHeaderOffset);
            if (fontTableOffset + FdtFontTableHeaderSize > fdt.Length)
            {
                return false;
            }

            glyphCount = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x04);
            glyphStart = fontTableOffset + FdtFontTableHeaderSize;
            long glyphBytes = (long)glyphCount * FdtGlyphEntrySize;
            return glyphBytes >= 0 && glyphStart <= fdt.Length && glyphStart + glyphBytes <= fdt.Length;
        }

        private static uint PackUtf8(uint codepoint)
        {
            if (codepoint <= 0x7F)
            {
                return codepoint;
            }

            if (codepoint <= 0x7FF)
            {
                return (uint)((0xC0 | (codepoint >> 6)) << 8 | (0x80 | (codepoint & 0x3F)));
            }

            if (codepoint <= 0xFFFF)
            {
                return (uint)((0xE0 | (codepoint >> 12)) << 16 |
                              (0x80 | ((codepoint >> 6) & 0x3F)) << 8 |
                              (0x80 | (codepoint & 0x3F)));
            }

            return (uint)((0xF0 | (codepoint >> 18)) << 24 |
                          (0x80 | ((codepoint >> 12) & 0x3F)) << 16 |
                          (0x80 | ((codepoint >> 6) & 0x3F)) << 8 |
                          (0x80 | (codepoint & 0x3F)));
        }

        private static string ResolveFontTexturePath(string fdtPath, int imageIndex)
        {
            string normalized = fdtPath.Replace('\\', '/').ToLowerInvariant();
            if (normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FontKrnTexturePath;
            }

            int textureIndex = imageIndex / 4;
            bool lobby = normalized.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0;
            if (lobby)
            {
                switch (textureIndex)
                {
                    case 0: return FontLobby1TexturePath;
                    case 1: return FontLobby2TexturePath;
                    case 2: return FontLobby3TexturePath;
                    case 3: return FontLobby4TexturePath;
                    case 4: return FontLobby5TexturePath;
                    case 5: return FontLobby6TexturePath;
                    case 6: return FontLobby7TexturePath;
                    default: return null;
                }
            }

            switch (textureIndex)
            {
                case 0: return Font1TexturePath;
                case 1: return Font2TexturePath;
                case 2: return Font3TexturePath;
                case 3: return Font4TexturePath;
                case 4: return Font5TexturePath;
                case 5: return Font6TexturePath;
                case 6: return Font7TexturePath;
                default: return null;
            }
        }

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

        private struct FdtGlyphEntry
        {
            public ushort ShiftJisValue;
            public ushort ImageIndex;
            public ushort X;
            public ushort Y;
            public byte Width;
            public byte Height;
            public sbyte OffsetX;
            public sbyte OffsetY;
        }

        private struct Texture
        {
            public int Width;
            public int Height;
            public byte[] Data;
        }

        private struct GlyphCanvas
        {
            public byte[] Alpha;
            public int VisiblePixels;
            public FdtGlyphEntry Glyph;
            public string TexturePath;
        }
    }
}
