using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int FdtHeaderSize = 0x20;
        private const int FdtFontTableHeaderSize = 0x20;
        private const int FdtGlyphEntrySize = 0x10;

        private static bool GlyphSpacingMetricsMatch(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
        {
            return sourceGlyph.ShiftJisValue == targetGlyph.ShiftJisValue &&
                   sourceGlyph.Width == targetGlyph.Width &&
                   sourceGlyph.Height == targetGlyph.Height &&
                   sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                   sourceGlyph.OffsetY == targetGlyph.OffsetY;
        }

        private static bool GlyphSpacingMetricsMatchOrLobbySafe(
            string targetFontPath,
            uint codepoint,
            FdtGlyphEntry sourceGlyph,
            FdtGlyphEntry targetGlyph)
        {
            if (GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
            {
                return true;
            }

            if (!IsLobbyFontPath(targetFontPath) ||
                codepoint <= 0x20u ||
                codepoint > 0x7Eu ||
                sourceGlyph.ShiftJisValue != targetGlyph.ShiftJisValue ||
                sourceGlyph.Width != targetGlyph.Width ||
                sourceGlyph.Height != targetGlyph.Height ||
                sourceGlyph.OffsetY != targetGlyph.OffsetY)
            {
                return false;
            }

            sbyte expectedOffsetX = sourceGlyph.OffsetX < 0 ? (sbyte)0 : sourceGlyph.OffsetX;
            return targetGlyph.OffsetX == expectedOffsetX;
        }

        private static string FormatGlyphSpacing(FdtGlyphEntry glyph)
        {
            return "sjis=0x" + glyph.ShiftJisValue.ToString("X4") +
                ", size=" + glyph.Width.ToString() + "x" + glyph.Height.ToString() +
                ", offset=" + glyph.OffsetX.ToString() + "/" + glyph.OffsetY.ToString();
        }

        private static string FormatGlyphRoute(FdtGlyphEntry glyph)
        {
            return FormatGlyphSpacing(glyph) +
                ", image=" + glyph.ImageIndex.ToString() +
                ", cell=" + glyph.X.ToString() + "/" + glyph.Y.ToString();
        }

        private static int GetGlyphAdvance(FdtGlyphEntry glyph)
        {
            return Math.Max(1, glyph.Width + glyph.OffsetX);
        }

        private static bool IsLobbyFontPath(string fontPath)
        {
            return !string.IsNullOrEmpty(fontPath) &&
                   fontPath.Replace('\\', '/').IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryComputeMedianCjkAdvance(byte[] fdt, out int medianAdvance, out int sampleCount)
        {
            medianAdvance = 0;
            sampleCount = 0;

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return false;
            }

            List<int> advances = new List<int>();
            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                uint codepoint;
                if (!TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(fdt, offset), out codepoint) ||
                    !IsCjkSpacingBaselineCodepoint(codepoint))
                {
                    continue;
                }

                FdtGlyphEntry glyph = ReadGlyphEntry(fdt, offset);
                if (glyph.Width == 0 || glyph.Height == 0)
                {
                    continue;
                }

                advances.Add(GetGlyphAdvance(glyph));
            }

            if (advances.Count == 0)
            {
                return false;
            }

            advances.Sort();
            sampleCount = advances.Count;
            medianAdvance = advances[advances.Count / 2];
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

        private static FdtGlyphEntry ReadGlyphEntry(byte[] fdt, int offset)
        {
            return new FdtGlyphEntry
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
        }

        private static bool IsCjkSpacingBaselineCodepoint(uint codepoint)
        {
            return (codepoint >= 0x3040u && codepoint <= 0x30FFu) ||
                   (codepoint >= 0x3400u && codepoint <= 0x4DBFu) ||
                   (codepoint >= 0x4E00u && codepoint <= 0x9FFFu) ||
                   (codepoint >= 0xF900u && codepoint <= 0xFAFFu);
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

        private static bool TryDecodeFdtUtf8Value(uint value, out uint codepoint)
        {
            if ((value & 0xFFFFFF80u) == 0)
            {
                codepoint = value & 0x7Fu;
                return true;
            }

            if ((value & 0xFFFFE0C0u) == 0x0000C080u)
            {
                codepoint = (((value >> 8) & 0x1Fu) << 6) |
                            (((value >> 0) & 0x3Fu) << 0);
                return true;
            }

            if ((value & 0x00F0C0C0u) == 0x00E08080u)
            {
                codepoint = (((value >> 16) & 0x0Fu) << 12) |
                            (((value >> 8) & 0x3Fu) << 6) |
                            (((value >> 0) & 0x3Fu) << 0);
                return true;
            }

            if ((value & 0xF8C0C0C0u) == 0xF0808080u)
            {
                codepoint = (((value >> 24) & 0x07u) << 18) |
                            (((value >> 16) & 0x3Fu) << 12) |
                            (((value >> 8) & 0x3Fu) << 6) |
                            (((value >> 0) & 0x3Fu) << 0);
                return codepoint <= 0x10FFFFu;
            }

            codepoint = 0;
            return false;
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
    }
}
