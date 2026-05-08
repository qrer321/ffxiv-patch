using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const byte UldTrumpGothicFontId = 3;
        private const byte UldJupiterFontId = 4;
        private const byte UldMiedingerMedFontId = 1;
        private const byte UldAxisFontId = 0;

        private static List<UldTextNodeFont> GetUldTextNodeFonts(byte[] uld)
        {
            List<UldTextNodeFont> results = new List<UldTextNodeFont>();
            if (!HasRange(uld, 0, 16) || !HasAsciiMagic(uld, 0, "uldh"))
            {
                return results;
            }

            WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, 8), true, results);
            WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, 12), false, results);
            return results;
        }

        private static string ResolveUldFontPath(byte fontId, byte fontSize, bool lobby)
        {
            string suffix = lobby ? "_lobby.fdt" : ".fdt";
            switch (fontId)
            {
                case UldAxisFontId:
                    switch (fontSize)
                    {
                        case 12:
                        case 14:
                        case 18:
                        case 36:
                        case 96:
                            return "common/font/AXIS_" + fontSize.ToString() + suffix;
                        case 34:
                            return "common/font/AXIS_36" + suffix;
                    }

                    return null;

                case UldMiedingerMedFontId:
                    switch (fontSize)
                    {
                        case 10:
                        case 12:
                        case 14:
                        case 18:
                        case 36:
                            return "common/font/MiedingerMid_" + fontSize.ToString() + suffix;
                    }

                    return null;

                case UldTrumpGothicFontId:
                    switch (fontSize)
                    {
                        case 23:
                        case 34:
                        case 68:
                        case 184:
                            return "common/font/TrumpGothic_" + fontSize.ToString() + suffix;
                    }

                    return null;

                case UldJupiterFontId:
                    switch (fontSize)
                    {
                        case 12:
                            return "common/font/Jupiter_16" + suffix;
                        case 18:
                            return "common/font/Jupiter_20" + suffix;
                        case 16:
                        case 20:
                        case 23:
                        case 45:
                        case 46:
                        case 90:
                            return "common/font/Jupiter_" + fontSize.ToString() + suffix;
                    }

                    return null;

                default:
                    return null;
            }
        }

        private static Dictionary<int, UldTextNodeFont> GetUldTextNodeFontsByOffset(byte[] uld)
        {
            List<UldTextNodeFont> fonts = GetUldTextNodeFonts(uld);
            Dictionary<int, UldTextNodeFont> byOffset = new Dictionary<int, UldTextNodeFont>();
            for (int i = 0; i < fonts.Count; i++)
            {
                byOffset[fonts[i].NodeOffset] = fonts[i];
            }

            return byOffset;
        }

        private static void WalkUldAtkTextNodes(byte[] uld, uint atkOffsetValue, bool patchComponents, List<UldTextNodeFont> results)
        {
            if (atkOffsetValue == 0 || atkOffsetValue > int.MaxValue)
            {
                return;
            }

            int atkOffset = (int)atkOffsetValue;
            if (!HasRange(uld, atkOffset, 36) || !HasAsciiMagic(uld, atkOffset, "atkh"))
            {
                return;
            }

            if (patchComponents)
            {
                WalkUldComponentTextNodes(uld, atkOffset, results);
            }
            else
            {
                WalkUldWidgetTextNodes(uld, atkOffset, results);
            }
        }

        private static void WalkUldComponentTextNodes(byte[] uld, int atkOffset, List<UldTextNodeFont> results)
        {
            uint componentListRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + 16);
            if (componentListRelativeOffset == 0 || componentListRelativeOffset > int.MaxValue)
            {
                return;
            }

            int componentListOffset = atkOffset + (int)componentListRelativeOffset;
            if (!HasRange(uld, componentListOffset, 16) || !HasAsciiMagic(uld, componentListOffset, "cohd"))
            {
                return;
            }

            uint componentCount = Endian.ReadUInt32LE(uld, componentListOffset + 8);
            int entryOffset = componentListOffset + 16;
            for (uint i = 0; i < componentCount && HasRange(uld, entryOffset, 16); i++)
            {
                uint nodeCount = Endian.ReadUInt32LE(uld, entryOffset + 8);
                ushort componentSize = Endian.ReadUInt16LE(uld, entryOffset + 12);
                ushort nodeOffset = Endian.ReadUInt16LE(uld, entryOffset + 14);
                int nodeStart = entryOffset + nodeOffset;
                if (nodeOffset < 16 || !HasRange(uld, nodeStart, 28))
                {
                    nodeStart = entryOffset + 16;
                }

                int cursor = nodeStart;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, 28); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + 24);
                    AddUldTextNodeFont(uld, cursor, results);
                    if (nodeSize <= 0)
                    {
                        break;
                    }

                    cursor += nodeSize;
                }

                if (componentSize == 0)
                {
                    entryOffset = cursor;
                }
                else
                {
                    entryOffset += componentSize;
                }
            }
        }

        private static void WalkUldWidgetTextNodes(byte[] uld, int atkOffset, List<UldTextNodeFont> results)
        {
            uint widgetRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + 24);
            if (widgetRelativeOffset == 0 || widgetRelativeOffset > int.MaxValue)
            {
                return;
            }

            int widgetOffset = atkOffset + (int)widgetRelativeOffset;
            if (!HasRange(uld, widgetOffset, 16) || !HasAsciiMagic(uld, widgetOffset, "wdhd"))
            {
                return;
            }

            uint widgetCount = Endian.ReadUInt32LE(uld, widgetOffset + 8);
            int cursor = widgetOffset + 16;
            for (uint i = 0; i < widgetCount && HasRange(uld, cursor, 16); i++)
            {
                uint nodeCount = Endian.ReadUInt16LE(uld, cursor + 12);
                cursor += 16;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, 28); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + 24);
                    AddUldTextNodeFont(uld, cursor, results);
                    if (nodeSize <= 0)
                    {
                        break;
                    }

                    cursor += nodeSize;
                }
            }
        }

        private static void AddUldTextNodeFont(byte[] uld, int nodeOffset, List<UldTextNodeFont> results)
        {
            const int NodeTypeOffset = 20;
            const int NodeSizeOffset = 24;
            const int NodeHeaderSize = 88;
            const int TextExtraMinSize = 24;
            const int TextFontOffsetInExtra = 10;
            const int TextFontSizeOffsetInExtra = 11;

            if (!HasRange(uld, nodeOffset, NodeHeaderSize + TextExtraMinSize))
            {
                return;
            }

            int nodeType = unchecked((int)Endian.ReadUInt32LE(uld, nodeOffset + NodeTypeOffset));
            int nodeSize = Endian.ReadUInt16LE(uld, nodeOffset + NodeSizeOffset);
            if (nodeType != 3 || nodeSize < NodeHeaderSize + TextExtraMinSize)
            {
                return;
            }

            int fontOffset = nodeOffset + NodeHeaderSize + TextFontOffsetInExtra;
            if (!HasRange(uld, fontOffset, 1))
            {
                return;
            }

            int fontSizeOffset = nodeOffset + NodeHeaderSize + TextFontSizeOffsetInExtra;
            results.Add(new UldTextNodeFont
            {
                NodeOffset = nodeOffset,
                NodeSize = nodeSize,
                FontId = uld[fontOffset],
                FontSize = uld[fontSizeOffset]
            });
        }

        private struct UldTextNodeFont
        {
            public int NodeOffset;
            public int NodeSize;
            public byte FontId;
            public byte FontSize;
        }
    }
}
