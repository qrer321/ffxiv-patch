using System;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class PartyBonusRoleFontPatch
    {
        public const string UldPath = "ui/uld/PartyMemberList.uld";
        public const uint ComponentId = 1020;
        public const uint TextNodeId = 4;
        public const int ComponentInstanceCount = 5;
        public const int ComponentStride = 122;
        public const short TextX = 24;
        public const short TextY = 6;
        public const ushort TextWidth = 64;
        public const ushort TextHeight = 20;
        public const byte SourceFontId = 3;
        public const byte SourceFontSize = 19;
        public const byte TargetFontId = 0;
        public const byte TargetFontSize = 12;
        public const byte AlignmentLeft = 3;

        public const int TextNodeHeaderSize = 88;
        public const int TextFontOffsetInExtra = 10;
        public const int TextFontSizeOffsetInExtra = 11;

        private const int UldHeaderSize = 16;
        private const int UldComponentAtkOffset = 8;
        private const int AtkHeaderMinSize = 36;
        private const int AtkComponentListRelativeOffset = 16;
        private const int UldListHeaderSize = 16;
        private const int UldListCountOffset = 8;
        private const int ComponentEntryNodeCountOffset = 8;
        private const int ComponentEntrySizeOffset = 12;
        private const int ComponentEntryNodeOffsetOffset = 14;
        private const int ComponentEntryMinNodeOffset = 16;
        private const int UldTextNodeMinSize = 112;
        private const int UldNodeTypeOffset = 20;
        private const int UldNodeSizeOffset = 24;
        private const int UldNodeIdOffset = 0;
        private const int UldNodeXOffset = 44;
        private const int UldNodeYOffset = 46;
        private const int UldNodeWidthOffset = 48;
        private const int UldNodeHeightOffset = 50;
        private const int UldTextAlignmentOffsetInExtra = 8;
        private const int UldTextFlagsOffsetInExtra = 16;
        private const int UldTextSheetTypeOffsetInExtra = 17;
        private const int UldTextCharSpacingOffsetInExtra = 18;
        private const int UldTextLineSpacingOffsetInExtra = 19;
        private const int UldTextFlags2OffsetInExtra = 20;
        private const int UldTextNodeType = 3;

        public static byte[] Apply(byte[] sourceUld)
        {
            PartyBonusRoleTextNode node;
            string error;
            if (!TryFindRoleTextNode(sourceUld, out node, out error))
            {
                throw new InvalidDataException(UldPath + " role-label node validation failed: " + error);
            }

            ValidateSourceContract(sourceUld, node);

            byte[] patched = (byte[])sourceUld.Clone();
            patched[node.FontOffset] = TargetFontId;
            patched[node.FontSizeOffset] = TargetFontSize;
            return patched;
        }

        public static bool TryFindRoleTextNode(
            byte[] uld,
            out PartyBonusRoleTextNode result,
            out string error)
        {
            result = new PartyBonusRoleTextNode();
            error = null;
            if (!HasRange(uld, 0, UldHeaderSize) || !HasMagic(uld, 0, "uldh"))
            {
                error = "invalid uldh header";
                return false;
            }

            uint atkOffsetValue = Endian.ReadUInt32LE(uld, UldComponentAtkOffset);
            if (atkOffsetValue == 0 || atkOffsetValue > int.MaxValue)
            {
                error = "invalid component ATK offset";
                return false;
            }

            int atkOffset = (int)atkOffsetValue;
            if (!HasRange(uld, atkOffset, AtkHeaderMinSize) || !HasMagic(uld, atkOffset, "atkh"))
            {
                error = "invalid component atkh header";
                return false;
            }

            uint listRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + AtkComponentListRelativeOffset);
            if (listRelativeOffset == 0 || listRelativeOffset > int.MaxValue)
            {
                error = "invalid component list offset";
                return false;
            }

            int listOffset = checked(atkOffset + (int)listRelativeOffset);
            if (!HasRange(uld, listOffset, UldListHeaderSize) || !HasMagic(uld, listOffset, "cohd"))
            {
                error = "invalid cohd header";
                return false;
            }

            uint componentCount = Endian.ReadUInt32LE(uld, listOffset + UldListCountOffset);
            int componentMatches = 0;
            int nodeMatches = 0;
            int entryOffset = listOffset + UldListHeaderSize;
            for (uint componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                if (!HasRange(uld, entryOffset, UldListHeaderSize))
                {
                    error = "component entry is out of range";
                    return false;
                }

                uint componentId = Endian.ReadUInt32LE(uld, entryOffset);
                uint nodeCount = Endian.ReadUInt32LE(uld, entryOffset + ComponentEntryNodeCountOffset);
                ushort componentSize = Endian.ReadUInt16LE(uld, entryOffset + ComponentEntrySizeOffset);
                ushort nodeRelativeOffset = Endian.ReadUInt16LE(uld, entryOffset + ComponentEntryNodeOffsetOffset);
                int cursor = entryOffset + nodeRelativeOffset;
                if (nodeRelativeOffset < ComponentEntryMinNodeOffset || !HasRange(uld, cursor, UldNodeSizeOffset + 2))
                {
                    cursor = entryOffset + ComponentEntryMinNodeOffset;
                }

                if (componentId == ComponentId)
                {
                    componentMatches++;
                }

                for (uint nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    if (!HasRange(uld, cursor, UldNodeSizeOffset + 2))
                    {
                        error = "component node is out of range";
                        return false;
                    }

                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + UldNodeSizeOffset);
                    if (nodeSize < UldNodeSizeOffset + 2 || !HasRange(uld, cursor, nodeSize))
                    {
                        error = "component node has an invalid size";
                        return false;
                    }

                    if (componentId == ComponentId && Endian.ReadUInt32LE(uld, cursor + UldNodeIdOffset) == TextNodeId)
                    {
                        nodeMatches++;
                        if (nodeMatches == 1)
                        {
                            result = new PartyBonusRoleTextNode
                            {
                                NodeOffset = cursor,
                                NodeSize = nodeSize,
                                FontOffset = cursor + TextNodeHeaderSize + TextFontOffsetInExtra,
                                FontSizeOffset = cursor + TextNodeHeaderSize + TextFontSizeOffsetInExtra
                            };
                        }
                    }

                    cursor += nodeSize;
                }

                int nextEntryOffset = componentSize == 0 ? cursor : entryOffset + componentSize;
                if (nextEntryOffset <= entryOffset)
                {
                    error = "component list did not advance";
                    return false;
                }

                entryOffset = nextEntryOffset;
            }

            if (componentMatches != 1)
            {
                error = "expected one component " + ComponentId.ToString() + ", found " + componentMatches.ToString();
                return false;
            }

            if (nodeMatches != 1)
            {
                error = "expected one text node " + TextNodeId.ToString() + ", found " + nodeMatches.ToString();
                return false;
            }

            return true;
        }

        private static void ValidateSourceContract(byte[] uld, PartyBonusRoleTextNode node)
        {
            int extraOffset = node.NodeOffset + TextNodeHeaderSize;
            if (node.NodeSize < UldTextNodeMinSize || !HasRange(uld, node.NodeOffset, node.NodeSize))
            {
                throw new InvalidDataException(UldPath + " role-label text node is truncated.");
            }

            int nodeType = unchecked((int)Endian.ReadUInt32LE(uld, node.NodeOffset + UldNodeTypeOffset));
            short x = unchecked((short)Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeXOffset));
            short y = unchecked((short)Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeYOffset));
            ushort width = Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeWidthOffset);
            ushort height = Endian.ReadUInt16LE(uld, node.NodeOffset + UldNodeHeightOffset);
            byte alignment = uld[extraOffset + UldTextAlignmentOffsetInExtra];
            byte fontId = uld[node.FontOffset];
            byte fontSize = uld[node.FontSizeOffset];
            byte textFlags = uld[extraOffset + UldTextFlagsOffsetInExtra];
            byte sheetType = uld[extraOffset + UldTextSheetTypeOffsetInExtra];
            byte charSpacing = uld[extraOffset + UldTextCharSpacingOffsetInExtra];
            byte lineSpacing = uld[extraOffset + UldTextLineSpacingOffsetInExtra];
            byte textFlags2 = uld[extraOffset + UldTextFlags2OffsetInExtra];
            if (nodeType != UldTextNodeType ||
                x != TextX ||
                y != TextY ||
                width != TextWidth ||
                height != TextHeight ||
                alignment != AlignmentLeft ||
                fontId != SourceFontId ||
                fontSize != SourceFontSize ||
                textFlags != 0 ||
                sheetType != 0 ||
                charSpacing != 0 ||
                lineSpacing != 0 ||
                textFlags2 != 6)
            {
                throw new InvalidDataException(
                    UldPath + " role-label source contract changed: type=" + nodeType.ToString() +
                    ", rect=" + x.ToString() + "," + y.ToString() + "," + width.ToString() + "," + height.ToString() +
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

        internal struct PartyBonusRoleTextNode
        {
            public int NodeOffset;
            public int NodeSize;
            public int FontOffset;
            public int FontSizeOffset;
        }
    }
}
