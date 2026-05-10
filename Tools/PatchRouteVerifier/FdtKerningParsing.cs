using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int FdtKerningHeaderSize = 0x10;
        private const int FdtKerningEntrySize = 0x10;

        private static Dictionary<string, byte[]> ReadAsciiKerningEntries(byte[] fdt)
        {
            Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            int kerningStart;
            uint kerningCount;
            if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
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

        private static Dictionary<string, int> ReadKerningAdjustments(byte[] fdt)
        {
            Dictionary<string, int> entries = new Dictionary<string, int>(StringComparer.Ordinal);
            int kerningStart;
            uint kerningCount;
            if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
            {
                return entries;
            }

            for (int i = 0; i < kerningCount; i++)
            {
                int offset = kerningStart + i * FdtKerningEntrySize;
                uint left = Endian.ReadUInt32LE(fdt, offset);
                uint right = Endian.ReadUInt32LE(fdt, offset + 4);
                int adjustment = unchecked((int)Endian.ReadUInt32LE(fdt, offset + 12));
                entries[BuildKerningAdjustmentKey(left, right)] = adjustment;
            }

            return entries;
        }

        private static bool TryGetKerningTable(byte[] fdt, out int kerningStart, out uint kerningCount)
        {
            kerningStart = 0;
            kerningCount = 0;
            if (fdt == null || fdt.Length < FdtHeaderSize)
            {
                return false;
            }

            uint headerOffset = Endian.ReadUInt32LE(fdt, 0x0C);
            if (headerOffset == 0 ||
                headerOffset > int.MaxValue ||
                headerOffset < FdtHeaderSize ||
                headerOffset > fdt.Length - FdtKerningHeaderSize)
            {
                return false;
            }

            int kerningHeaderOffset = checked((int)headerOffset);
            if (!HasAsciiMagic(fdt, kerningHeaderOffset, "knhd"))
            {
                return false;
            }

            kerningCount = Endian.ReadUInt32LE(fdt, kerningHeaderOffset + 0x04);
            kerningStart = kerningHeaderOffset + FdtKerningHeaderSize;
            long kerningBytes = (long)kerningCount * FdtKerningEntrySize;
            return kerningBytes >= 0 &&
                   kerningStart <= fdt.Length &&
                   kerningStart + kerningBytes <= fdt.Length;
        }

        private static int GetKerningAdjustment(Dictionary<string, int> adjustments, uint leftCodepoint, uint rightCodepoint)
        {
            if (adjustments == null || adjustments.Count == 0)
            {
                return 0;
            }

            uint left = PackUtf8(leftCodepoint);
            uint right = PackUtf8(rightCodepoint);
            int adjustment;
            if (!adjustments.TryGetValue(BuildKerningAdjustmentKey(left, right), out adjustment))
            {
                return 0;
            }

            return adjustment;
        }

        private static bool KerningAdjustmentMatchesOrLobbySafe(string targetFontPath, int sourceAdjustment, int targetAdjustment)
        {
            if (IsLobbyFontPath(targetFontPath) &&
                sourceAdjustment < 0 &&
                targetAdjustment == 0)
            {
                return true;
            }

            return sourceAdjustment == targetAdjustment;
        }

        private static bool KerningEntryMatchesOrLobbySafe(string targetFontPath, byte[] sourceEntry, byte[] targetEntry)
        {
            if (BytesEqual(sourceEntry, targetEntry))
            {
                return true;
            }

            if (IsLobbyFontPath(targetFontPath) &&
                sourceEntry != null &&
                targetEntry != null &&
                sourceEntry.Length == FdtKerningEntrySize &&
                targetEntry.Length == FdtKerningEntrySize &&
                BytesEqualExceptKerningAdjustment(sourceEntry, targetEntry))
            {
                int sourceAdjustment = unchecked((int)Endian.ReadUInt32LE(sourceEntry, 12));
                int targetAdjustment = unchecked((int)Endian.ReadUInt32LE(targetEntry, 12));
                return sourceAdjustment < 0 && targetAdjustment == 0;
            }

            return false;
        }

        private static bool BytesEqualExceptKerningAdjustment(byte[] left, byte[] right)
        {
            for (int i = 0; i < FdtKerningEntrySize; i++)
            {
                if (i >= 12 && i < 16)
                {
                    continue;
                }

                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildKerningAdjustmentKey(uint left, uint right)
        {
            return left.ToString("X8") + ":" + right.ToString("X8");
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
    }
}
