using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const string UldHeaderMagic = "uldh";
        private const string UldAtkHeaderMagic = "atkh";
        private const string UldComponentHeaderMagic = "cohd";
        private const string UldWidgetHeaderMagic = "wdhd";
        private const int UldHeaderSize = 16;
        private const int UldComponentAtkOffset = 8;
        private const int UldWidgetAtkOffset = 12;
        private const int AtkHeaderMinSize = 36;
        private const int AtkComponentListRelativeOffset = 16;
        private const int AtkWidgetListRelativeOffset = 24;
        private const int UldListHeaderSize = 16;
        private const int UldListCountOffset = 8;
        private const int ComponentEntryNodeCountOffset = 8;
        private const int ComponentEntrySizeOffset = 12;
        private const int ComponentEntryNodeOffsetOffset = 14;
        private const int ComponentEntryMinNodeOffset = 16;
        private const int WidgetEntryNodeCountOffset = 12;
        private const int UldTextNodeMinSize = 28;
        private const int UldNodeTypeOffset = 20;
        private const int UldNodeSizeOffset = 24;
        private const int UldTextNodeHeaderSize = 88;
        private const int UldTextExtraMinSize = 24;
        private const int UldTextFontOffsetInExtra = 10;
        private const int UldTextFontSizeOffsetInExtra = 11;
        private const int UldTextNodeType = 3;

        private static List<UldTextNodeFont> GetUldTextNodeFonts(byte[] uld)
        {
            List<UldTextNodeFont> results = new List<UldTextNodeFont>();
            if (!HasRange(uld, 0, UldHeaderSize) || !HasAsciiMagic(uld, 0, UldHeaderMagic))
            {
                return results;
            }

            WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, UldComponentAtkOffset), true, results);
            WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, UldWidgetAtkOffset), false, results);
            return results;
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
            if (!HasRange(uld, atkOffset, AtkHeaderMinSize) || !HasAsciiMagic(uld, atkOffset, UldAtkHeaderMagic))
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
            uint componentListRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + AtkComponentListRelativeOffset);
            if (componentListRelativeOffset == 0 || componentListRelativeOffset > int.MaxValue)
            {
                return;
            }

            int componentListOffset = atkOffset + (int)componentListRelativeOffset;
            if (!HasRange(uld, componentListOffset, UldListHeaderSize) || !HasAsciiMagic(uld, componentListOffset, UldComponentHeaderMagic))
            {
                return;
            }

            uint componentCount = Endian.ReadUInt32LE(uld, componentListOffset + UldListCountOffset);
            int entryOffset = componentListOffset + UldListHeaderSize;
            for (uint i = 0; i < componentCount && HasRange(uld, entryOffset, UldListHeaderSize); i++)
            {
                uint nodeCount = Endian.ReadUInt32LE(uld, entryOffset + ComponentEntryNodeCountOffset);
                ushort componentSize = Endian.ReadUInt16LE(uld, entryOffset + ComponentEntrySizeOffset);
                ushort nodeOffset = Endian.ReadUInt16LE(uld, entryOffset + ComponentEntryNodeOffsetOffset);
                int nodeStart = entryOffset + nodeOffset;
                if (nodeOffset < ComponentEntryMinNodeOffset || !HasRange(uld, nodeStart, UldTextNodeMinSize))
                {
                    nodeStart = entryOffset + ComponentEntryMinNodeOffset;
                }

                int cursor = nodeStart;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, UldTextNodeMinSize); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + UldNodeSizeOffset);
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
            uint widgetRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + AtkWidgetListRelativeOffset);
            if (widgetRelativeOffset == 0 || widgetRelativeOffset > int.MaxValue)
            {
                return;
            }

            int widgetOffset = atkOffset + (int)widgetRelativeOffset;
            if (!HasRange(uld, widgetOffset, UldListHeaderSize) || !HasAsciiMagic(uld, widgetOffset, UldWidgetHeaderMagic))
            {
                return;
            }

            uint widgetCount = Endian.ReadUInt32LE(uld, widgetOffset + UldListCountOffset);
            int cursor = widgetOffset + UldListHeaderSize;
            for (uint i = 0; i < widgetCount && HasRange(uld, cursor, UldListHeaderSize); i++)
            {
                uint nodeCount = Endian.ReadUInt16LE(uld, cursor + WidgetEntryNodeCountOffset);
                cursor += UldListHeaderSize;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, UldTextNodeMinSize); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + UldNodeSizeOffset);
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
            if (!HasRange(uld, nodeOffset, UldTextNodeHeaderSize + UldTextExtraMinSize))
            {
                return;
            }

            int nodeType = unchecked((int)Endian.ReadUInt32LE(uld, nodeOffset + UldNodeTypeOffset));
            int nodeSize = Endian.ReadUInt16LE(uld, nodeOffset + UldNodeSizeOffset);
            if (nodeType != UldTextNodeType || nodeSize < UldTextNodeHeaderSize + UldTextExtraMinSize)
            {
                return;
            }

            int fontOffset = nodeOffset + UldTextNodeHeaderSize + UldTextFontOffsetInExtra;
            if (!HasRange(uld, fontOffset, 1))
            {
                return;
            }

            int fontSizeOffset = nodeOffset + UldTextNodeHeaderSize + UldTextFontSizeOffsetInExtra;
            results.Add(new UldTextNodeFont
            {
                NodeOffset = nodeOffset,
                NodeSize = nodeSize,
                FontId = uld[fontOffset],
                FontSize = uld[fontSizeOffset],
                HeaderBytes = CopyBytes(uld, nodeOffset, UldTextNodeHeaderSize),
                TextExtraBytes = CopyBytes(uld, nodeOffset + UldTextNodeHeaderSize, UldTextExtraMinSize)
            });
        }

        private static byte[] CopyBytes(byte[] data, int offset, int length)
        {
            byte[] bytes = new byte[length];
            Buffer.BlockCopy(data, offset, bytes, 0, length);
            return bytes;
        }

        private struct UldTextNodeFont
        {
            public int NodeOffset;
            public int NodeSize;
            public byte FontId;
            public byte FontSize;
            public byte[] HeaderBytes;
            public byte[] TextExtraBytes;
        }
    }
}
