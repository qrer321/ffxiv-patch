using System;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class PvpProfileUldFontRoutes
    {
        public const byte AxisFontId = 0;
        public const byte JupiterFontId = 4;

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

        public static readonly string[] TargetUldPaths = new string[]
        {
            "ui/uld/PvPProfile.uld",
            "ui/uld/PvPCharacter.uld",
            "ui/uld/PvPAction.uld",
            "ui/uld/PvPActions.uld",
            "ui/uld/PvPTeam.uld",
            "ui/uld/PvPTeamBoard.uld",
            "ui/uld/PvPSchedule.uld"
        };

        public static int PatchAxisTextRoutes(byte[] uld)
        {
            if (!HasRange(uld, 0, UldHeaderSize) || !HasAsciiMagic(uld, 0, UldHeaderMagic))
            {
                return 0;
            }

            int changed = 0;
            changed += WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, UldComponentAtkOffset), true);
            changed += WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, UldWidgetAtkOffset), false);
            return changed;
        }

        public static bool IsExpectedRouteChange(byte cleanFontId, byte cleanFontSize, byte patchedFontId, byte patchedFontSize)
        {
            if (cleanFontId != AxisFontId || patchedFontId != JupiterFontId)
            {
                return false;
            }

            return (cleanFontSize == 12 && patchedFontSize == 12) ||
                   (cleanFontSize == 14 && patchedFontSize == 16) ||
                   (cleanFontSize == 18 && patchedFontSize == 18);
        }

        private static int WalkUldAtkTextNodes(byte[] uld, uint atkOffsetValue, bool patchComponents)
        {
            if (atkOffsetValue == 0 || atkOffsetValue > int.MaxValue)
            {
                return 0;
            }

            int atkOffset = (int)atkOffsetValue;
            if (!HasRange(uld, atkOffset, AtkHeaderMinSize) || !HasAsciiMagic(uld, atkOffset, UldAtkHeaderMagic))
            {
                return 0;
            }

            return patchComponents
                ? WalkUldComponentTextNodes(uld, atkOffset)
                : WalkUldWidgetTextNodes(uld, atkOffset);
        }

        private static int WalkUldComponentTextNodes(byte[] uld, int atkOffset)
        {
            uint componentListRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + AtkComponentListRelativeOffset);
            if (componentListRelativeOffset == 0 || componentListRelativeOffset > int.MaxValue)
            {
                return 0;
            }

            int componentListOffset = atkOffset + (int)componentListRelativeOffset;
            if (!HasRange(uld, componentListOffset, UldListHeaderSize) || !HasAsciiMagic(uld, componentListOffset, UldComponentHeaderMagic))
            {
                return 0;
            }

            int changed = 0;
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
                    changed += PatchTextNodeRoute(uld, cursor);
                    if (nodeSize <= 0)
                    {
                        break;
                    }

                    cursor += nodeSize;
                }

                entryOffset = componentSize == 0 ? cursor : entryOffset + componentSize;
            }

            return changed;
        }

        private static int WalkUldWidgetTextNodes(byte[] uld, int atkOffset)
        {
            uint widgetRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + AtkWidgetListRelativeOffset);
            if (widgetRelativeOffset == 0 || widgetRelativeOffset > int.MaxValue)
            {
                return 0;
            }

            int widgetOffset = atkOffset + (int)widgetRelativeOffset;
            if (!HasRange(uld, widgetOffset, UldListHeaderSize) || !HasAsciiMagic(uld, widgetOffset, UldWidgetHeaderMagic))
            {
                return 0;
            }

            int changed = 0;
            uint widgetCount = Endian.ReadUInt32LE(uld, widgetOffset + UldListCountOffset);
            int cursor = widgetOffset + UldListHeaderSize;
            for (uint i = 0; i < widgetCount && HasRange(uld, cursor, UldListHeaderSize); i++)
            {
                uint nodeCount = Endian.ReadUInt16LE(uld, cursor + WidgetEntryNodeCountOffset);
                cursor += UldListHeaderSize;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, UldTextNodeMinSize); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + UldNodeSizeOffset);
                    changed += PatchTextNodeRoute(uld, cursor);
                    if (nodeSize <= 0)
                    {
                        break;
                    }

                    cursor += nodeSize;
                }
            }

            return changed;
        }

        private static int PatchTextNodeRoute(byte[] uld, int nodeOffset)
        {
            if (!HasRange(uld, nodeOffset, UldTextNodeHeaderSize + UldTextExtraMinSize))
            {
                return 0;
            }

            int nodeType = unchecked((int)Endian.ReadUInt32LE(uld, nodeOffset + UldNodeTypeOffset));
            int nodeSize = Endian.ReadUInt16LE(uld, nodeOffset + UldNodeSizeOffset);
            if (nodeType != UldTextNodeType || nodeSize < UldTextNodeHeaderSize + UldTextExtraMinSize)
            {
                return 0;
            }

            int fontOffset = nodeOffset + UldTextNodeHeaderSize + UldTextFontOffsetInExtra;
            int fontSizeOffset = nodeOffset + UldTextNodeHeaderSize + UldTextFontSizeOffsetInExtra;
            byte fontId = uld[fontOffset];
            byte fontSize = uld[fontSizeOffset];
            if (fontId != AxisFontId)
            {
                return 0;
            }

            if (fontSize == 12)
            {
                uld[fontOffset] = JupiterFontId;
                return 1;
            }

            if (fontSize == 14)
            {
                uld[fontOffset] = JupiterFontId;
                uld[fontSizeOffset] = 16;
                return 1;
            }

            if (fontSize == 18)
            {
                uld[fontOffset] = JupiterFontId;
                return 1;
            }

            return 0;
        }

        private static bool HasRange(byte[] data, int offset, int length)
        {
            return data != null && offset >= 0 && length >= 0 && offset <= data.Length - length;
        }

        private static bool HasAsciiMagic(byte[] data, int offset, string magic)
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
    }
}
