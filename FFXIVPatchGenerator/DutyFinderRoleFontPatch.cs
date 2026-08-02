using System;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class DutyFinderRoleFontPatch
    {
        public const string UldPath = "ui/uld/ContentsFinder.uld";
        public const uint WidgetId = 1;
        public const uint RoleHeadingNodeId = 24;
        public const uint RoleValueNodeId = 28;
        public const short TextY = 5;
        public const ushort TextHeight = 26;
        public const short RoleHeadingX = 10;
        public const ushort RoleHeadingWidth = 30;
        public const uint RoleHeadingTextId = 2503;
        public const short RoleValueX = 74;
        public const ushort RoleValueWidth = 70;
        public const uint RoleValueTextId = 0;
        public const byte SourceFontId = 3;
        public const byte SourceFontSize = 23;
        public const byte TargetFontId = 0;
        public const byte TargetFontSize = 12;
        public const byte AlignmentLeft = 3;

        public const int TextNodeHeaderSize = 88;
        public const int TextFontOffsetInExtra = 10;
        public const int TextFontSizeOffsetInExtra = 11;

        private const int UldHeaderSize = 16;
        private const int UldWidgetAtkOffset = 12;
        private const int AtkHeaderMinSize = 36;
        private const int AtkWidgetListRelativeOffset = 24;
        private const int UldListHeaderSize = 16;
        private const int UldListCountOffset = 8;
        private const int WidgetEntryNodeCountOffset = 12;
        private const int UldTextNodeMinSize = 112;
        private const int UldNodeIdOffset = 0;
        private const int UldNodeTypeOffset = 20;
        private const int UldNodeSizeOffset = 24;
        private const int UldNodeXOffset = 44;
        private const int UldNodeYOffset = 46;
        private const int UldNodeWidthOffset = 48;
        private const int UldNodeHeightOffset = 50;
        private const int UldTextIdOffsetInExtra = 0;
        private const int UldTextAlignmentOffsetInExtra = 8;
        private const int UldTextFlagsOffsetInExtra = 16;
        private const int UldTextSheetTypeOffsetInExtra = 17;
        private const int UldTextCharSpacingOffsetInExtra = 18;
        private const int UldTextLineSpacingOffsetInExtra = 19;
        private const int UldTextFlags2OffsetInExtra = 20;
        private const int UldTextNodeType = 3;

        public static byte[] Apply(byte[] sourceUld)
        {
            DutyFinderRoleTextNodes nodes;
            string error;
            if (!TryFindRoleTextNodes(sourceUld, out nodes, out error))
            {
                throw new InvalidDataException(UldPath + " role-tab node validation failed: " + error);
            }

            ValidateSourceContract(sourceUld, nodes.RoleHeading, true);
            ValidateSourceContract(sourceUld, nodes.RoleValue, false);

            byte[] patched = (byte[])sourceUld.Clone();
            patched[nodes.RoleHeading.FontOffset] = TargetFontId;
            patched[nodes.RoleHeading.FontSizeOffset] = TargetFontSize;
            patched[nodes.RoleValue.FontOffset] = TargetFontId;
            patched[nodes.RoleValue.FontSizeOffset] = TargetFontSize;
            return patched;
        }

        public static bool TryFindRoleTextNodes(
            byte[] uld,
            out DutyFinderRoleTextNodes result,
            out string error)
        {
            result = new DutyFinderRoleTextNodes();
            error = null;
            if (!HasRange(uld, 0, UldHeaderSize) || !HasMagic(uld, 0, "uldh"))
            {
                error = "invalid uldh header";
                return false;
            }

            uint atkOffsetValue = Endian.ReadUInt32LE(uld, UldWidgetAtkOffset);
            if (atkOffsetValue == 0 || atkOffsetValue > int.MaxValue)
            {
                error = "invalid widget ATK offset";
                return false;
            }

            int atkOffset = (int)atkOffsetValue;
            if (!HasRange(uld, atkOffset, AtkHeaderMinSize) || !HasMagic(uld, atkOffset, "atkh"))
            {
                error = "invalid widget atkh header";
                return false;
            }

            uint listRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + AtkWidgetListRelativeOffset);
            long listOffsetValue = (long)atkOffset + listRelativeOffset;
            if (listRelativeOffset == 0 || listOffsetValue > int.MaxValue)
            {
                error = "invalid widget list offset";
                return false;
            }

            int listOffset = (int)listOffsetValue;
            if (!HasRange(uld, listOffset, UldListHeaderSize) || !HasMagic(uld, listOffset, "wdhd"))
            {
                error = "invalid wdhd header";
                return false;
            }

            uint widgetCount = Endian.ReadUInt32LE(uld, listOffset + UldListCountOffset);
            int widgetMatches = 0;
            int headingMatches = 0;
            int roleValueMatches = 0;
            int cursor = listOffset + UldListHeaderSize;
            for (uint widgetIndex = 0; widgetIndex < widgetCount; widgetIndex++)
            {
                if (!HasRange(uld, cursor, UldListHeaderSize))
                {
                    error = "widget entry is out of range";
                    return false;
                }

                uint widgetId = Endian.ReadUInt32LE(uld, cursor);
                uint nodeCount = Endian.ReadUInt16LE(uld, cursor + WidgetEntryNodeCountOffset);
                if (widgetId == WidgetId)
                {
                    widgetMatches++;
                }

                cursor += UldListHeaderSize;
                for (uint nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    if (!HasRange(uld, cursor, UldNodeSizeOffset + 2))
                    {
                        error = "widget node is out of range";
                        return false;
                    }

                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + UldNodeSizeOffset);
                    if (nodeSize < UldNodeSizeOffset + 2 || !HasRange(uld, cursor, nodeSize))
                    {
                        error = "widget node has an invalid size";
                        return false;
                    }

                    if (widgetId == WidgetId)
                    {
                        uint nodeId = Endian.ReadUInt32LE(uld, cursor + UldNodeIdOffset);
                        if (nodeId == RoleHeadingNodeId)
                        {
                            headingMatches++;
                            result.RoleHeading = CreateNode(cursor, nodeSize);
                        }
                        else if (nodeId == RoleValueNodeId)
                        {
                            roleValueMatches++;
                            result.RoleValue = CreateNode(cursor, nodeSize);
                        }
                    }

                    cursor += nodeSize;
                }
            }

            if (widgetMatches != 1)
            {
                error = "expected one widget " + WidgetId.ToString() + ", found " + widgetMatches.ToString();
                return false;
            }

            if (headingMatches != 1 || roleValueMatches != 1)
            {
                error = "expected one heading/value node, found " +
                        headingMatches.ToString() + "/" + roleValueMatches.ToString();
                return false;
            }

            return true;
        }

        private static DutyFinderRoleTextNode CreateNode(int nodeOffset, int nodeSize)
        {
            return new DutyFinderRoleTextNode
            {
                NodeOffset = nodeOffset,
                NodeSize = nodeSize,
                FontOffset = nodeOffset + TextNodeHeaderSize + TextFontOffsetInExtra,
                FontSizeOffset = nodeOffset + TextNodeHeaderSize + TextFontSizeOffsetInExtra
            };
        }

        private static void ValidateSourceContract(
            byte[] uld,
            DutyFinderRoleTextNode node,
            bool heading)
        {
            int extraOffset = node.NodeOffset + TextNodeHeaderSize;
            if (node.NodeSize < UldTextNodeMinSize || !HasRange(uld, node.NodeOffset, node.NodeSize))
            {
                throw new InvalidDataException(UldPath + " role-tab text node is truncated.");
            }

            int nodeType = unchecked((int)Endian.ReadUInt32LE(uld, node.NodeOffset + UldNodeTypeOffset));
            uint nodeId = Endian.ReadUInt32LE(uld, node.NodeOffset + UldNodeIdOffset);
            short x = unchecked((short)Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeXOffset));
            short y = unchecked((short)Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeYOffset));
            ushort width = Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeWidthOffset);
            ushort height = Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeHeightOffset);
            uint textId = Endian.ReadUInt32LE(uld, extraOffset + UldTextIdOffsetInExtra);
            byte alignment = uld[extraOffset + UldTextAlignmentOffsetInExtra];
            byte fontId = uld[node.FontOffset];
            byte fontSize = uld[node.FontSizeOffset];
            byte textFlags = uld[extraOffset + UldTextFlagsOffsetInExtra];
            byte sheetType = uld[extraOffset + UldTextSheetTypeOffsetInExtra];
            byte charSpacing = uld[extraOffset + UldTextCharSpacingOffsetInExtra];
            byte lineSpacing = uld[extraOffset + UldTextLineSpacingOffsetInExtra];
            byte textFlags2 = uld[extraOffset + UldTextFlags2OffsetInExtra];
            uint expectedNodeId = heading ? RoleHeadingNodeId : RoleValueNodeId;
            short expectedX = heading ? RoleHeadingX : RoleValueX;
            ushort expectedWidth = heading ? RoleHeadingWidth : RoleValueWidth;
            uint expectedTextId = heading ? RoleHeadingTextId : RoleValueTextId;
            if (nodeType != UldTextNodeType ||
                nodeId != expectedNodeId ||
                x != expectedX ||
                y != TextY ||
                width != expectedWidth ||
                height != TextHeight ||
                textId != expectedTextId ||
                alignment != AlignmentLeft ||
                fontId != SourceFontId ||
                fontSize != SourceFontSize ||
                textFlags != 128 ||
                sheetType != 0 ||
                charSpacing != 0 ||
                lineSpacing != 0 ||
                textFlags2 != 6)
            {
                throw new InvalidDataException(
                    UldPath + " role-tab source contract changed: node=" + nodeId.ToString() +
                    ", type=" + nodeType.ToString() +
                    ", rect=" + x.ToString() + "," + y.ToString() + "," + width.ToString() + "," + height.ToString() +
                    ", text=" + textId.ToString() +
                    ", align=" + alignment.ToString() +
                    ", font=" + fontId.ToString() + "/" + fontSize.ToString() +
                    ", flags=" + textFlags.ToString() + "/" + textFlags2.ToString() +
                    ", spacing=" + charSpacing.ToString() + "/" + lineSpacing.ToString() +
                    ", sheet=" + sheetType.ToString());
            }
        }

        private static bool HasRange(byte[] data, int offset, int length)
        {
            return data != null && offset >= 0 && length >= 0 && offset <= data.Length - length;
        }

        private static bool HasMagic(byte[] data, int offset, string magic)
        {
            if (!HasRange(data, offset, magic.Length))
            {
                return false;
            }

            for (int i = 0; i < magic.Length; i++)
            {
                if (data[offset + i] != (byte)magic[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal struct DutyFinderRoleTextNodes
        {
            public DutyFinderRoleTextNode RoleHeading;
            public DutyFinderRoleTextNode RoleValue;
        }

        internal struct DutyFinderRoleTextNode
        {
            public int NodeOffset;
            public int NodeSize;
            public int FontOffset;
            public int FontSizeOffset;
        }
    }
}
