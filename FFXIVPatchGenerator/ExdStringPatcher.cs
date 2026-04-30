using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class ExdStringPatcher
    {
        private static readonly Regex StringKeyRegex = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        public static ExdPatchResult PatchDefaultVariant(
            ExcelDataFile target,
            ExcelDataFile source,
            ExcelHeader targetHeader,
            ExcelHeader sourceHeader,
            List<int> stringColumns,
            bool allowRowKeyFallback)
        {
            List<ExcelDataFile> sources = new List<ExcelDataFile>();
            sources.Add(source);
            ExdSourceMaps sourceMaps = BuildSourceMaps(sources, sourceHeader, allowRowKeyFallback);
            return PatchDefaultVariant(target, targetHeader, sourceHeader, stringColumns, sourceMaps, allowRowKeyFallback, StringPatchPolicy.Default);
        }

        public static ExdPatchResult PatchDefaultVariant(
            ExcelDataFile target,
            ExcelHeader targetHeader,
            ExcelHeader sourceHeader,
            List<int> stringColumns,
            ExdSourceMaps sourceMaps,
            bool allowRowKeyFallback,
            StringPatchPolicy patchPolicy)
        {
            if (sourceMaps == null)
            {
                sourceMaps = new ExdSourceMaps();
            }

            Dictionary<uint, RowPatchPlan> rowPlans = BuildStringKeyPlans(target, targetHeader, sourceMaps.StringKeyRows);
            bool usingStringKeyPlans = rowPlans.Count > 0;

            if (!usingStringKeyPlans && allowRowKeyFallback)
            {
                rowPlans = BuildRowKeyPlans(target, sourceMaps.RowKeyRows);
            }

            ExdPatchResult result = new ExdPatchResult();
            if (rowPlans.Count == 0)
            {
                result.Data = target.Data;
                return result;
            }

            List<byte[]> rowRecords = new List<byte[]>();
            uint dataSectionSize = 0;

            for (int i = 0; i < target.Rows.Count; i++)
            {
                ExcelDataRow row = target.Rows[i];
                RowPatchPlan plan;
                RowPatchResult rowResult = null;

                if (rowPlans.TryGetValue(row.RowId, out plan))
                {
                    try
                    {
                        rowResult = PatchRow(target, targetHeader, sourceHeader, stringColumns, row, plan.SourceRow, patchPolicy);
                    }
                    catch
                    {
                        rowResult = null;
                    }
                }

                byte[] rowRecord;
                if (rowResult != null)
                {
                    result.ProtectedUiStrings += rowResult.ProtectedUiStrings;
                }

                if (rowResult != null && rowResult.Touched)
                {
                    rowRecord = rowResult.Data;
                    result.Changed = true;
                    result.RowsPatched++;
                    if (plan.Mode == RowPatchMode.StringKey)
                    {
                        result.StringKeyRows++;
                    }
                    else
                    {
                        result.RowKeyRows++;
                    }
                }
                else
                {
                    rowRecord = CopyOriginalRowRecord(target, row);
                }

                rowRecord = PadRowRecordToAlignment(rowRecord);
                rowRecords.Add(rowRecord);
                dataSectionSize += checked((uint)rowRecords[rowRecords.Count - 1].Length);
            }

            if (!result.Changed)
            {
                result.Data = target.Data;
                return result;
            }

            MemoryStream output = new MemoryStream(target.Data.Length + 1024);
            byte[] header = new byte[0x20];
            Buffer.BlockCopy(target.Data, 0, header, 0, header.Length);

            uint indexSize = checked((uint)(target.Rows.Count * 8));
            Endian.WriteUInt32BE(header, 0x08, indexSize);
            Endian.WriteUInt32BE(header, 0x0C, dataSectionSize);
            output.Write(header, 0, header.Length);

            uint currentRowOffset = checked((uint)(0x20 + indexSize));
            for (int i = 0; i < target.Rows.Count; i++)
            {
                ExcelDataRow row = target.Rows[i];
                byte[] rowRecord = rowRecords[i];
                Endian.WriteUInt32BE(output, row.RowId);
                Endian.WriteUInt32BE(output, currentRowOffset);
                currentRowOffset += checked((uint)rowRecord.Length);
            }

            for (int i = 0; i < rowRecords.Count; i++)
            {
                byte[] rowRecord = rowRecords[i];
                output.Write(rowRecord, 0, rowRecord.Length);
            }

            result.Data = output.ToArray();
            return result;
        }

        public static ExdSourceMaps BuildSourceMaps(
            List<ExcelDataFile> sources,
            ExcelHeader sourceHeader,
            bool includeRowKeyMap)
        {
            ExdSourceMaps maps = new ExdSourceMaps();
            if (sources == null)
            {
                return maps;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                ExcelDataFile source = sources[i];
                AddStringKeyRows(maps.StringKeyRows, source, sourceHeader);
                if (includeRowKeyMap)
                {
                    AddRowKeyRows(maps.RowKeyRows, source);
                }
            }

            return maps;
        }

        private static void AddStringKeyRows(
            Dictionary<string, SourceRowRef> map,
            ExcelDataFile source,
            ExcelHeader sourceHeader)
        {
            if (!IsStringKeyHeader(sourceHeader))
            {
                return;
            }

            for (int i = 0; i < source.Rows.Count; i++)
            {
                ExcelDataRow row = source.Rows[i];
                string key;
                if (!TryGetPlainString(source, sourceHeader, row, 0, out key))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(key) || !StringKeyRegex.IsMatch(key) || map.ContainsKey(key))
                {
                    continue;
                }

                map.Add(key, new SourceRowRef(source, row));
            }
        }

        private static void AddRowKeyRows(Dictionary<uint, SourceRowRef> map, ExcelDataFile source)
        {
            for (int i = 0; i < source.Rows.Count; i++)
            {
                ExcelDataRow row = source.Rows[i];
                if (!map.ContainsKey(row.RowId))
                {
                    map.Add(row.RowId, new SourceRowRef(source, row));
                }
            }
        }

        private static Dictionary<uint, RowPatchPlan> BuildStringKeyPlans(
            ExcelDataFile target,
            ExcelHeader targetHeader,
            Dictionary<string, SourceRowRef> sourceStringKeyRows)
        {
            Dictionary<uint, RowPatchPlan> plans = new Dictionary<uint, RowPatchPlan>();
            if (sourceStringKeyRows.Count == 0 || !IsStringKeyHeader(targetHeader))
            {
                return plans;
            }

            for (int i = 0; i < target.Rows.Count; i++)
            {
                ExcelDataRow row = target.Rows[i];
                string key;
                if (!TryGetPlainString(target, targetHeader, row, 0, out key))
                {
                    continue;
                }

                SourceRowRef sourceRow;
                if (!StringKeyRegex.IsMatch(key) || !sourceStringKeyRows.TryGetValue(key, out sourceRow))
                {
                    continue;
                }

                plans[row.RowId] = new RowPatchPlan(sourceRow, RowPatchMode.StringKey);
            }

            return plans;
        }

        private static Dictionary<uint, RowPatchPlan> BuildRowKeyPlans(
            ExcelDataFile target,
            Dictionary<uint, SourceRowRef> sourceRowKeyRows)
        {
            Dictionary<uint, RowPatchPlan> plans = new Dictionary<uint, RowPatchPlan>();
            for (int i = 0; i < target.Rows.Count; i++)
            {
                SourceRowRef sourceRow;
                if (sourceRowKeyRows.TryGetValue(target.Rows[i].RowId, out sourceRow))
                {
                    plans[target.Rows[i].RowId] = new RowPatchPlan(sourceRow, RowPatchMode.RowKey);
                }
            }

            return plans;
        }

        private static bool IsStringKeyHeader(ExcelHeader header)
        {
            List<int> stringColumns = header.GetStringColumnIndexes();
            return stringColumns.Count == 2 &&
                   header.FindStringColumnIndexByOffset(0) >= 0 &&
                   header.FindStringColumnIndexByOffset(4) >= 0;
        }

        private static bool TryGetPlainString(
            ExcelDataFile file,
            ExcelHeader header,
            ExcelDataRow row,
            ushort columnOffset,
            out string value)
        {
            value = string.Empty;
            byte[] bytes = file.GetStringBytesByColumnOffset(row, header, columnOffset);
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x02)
                {
                    return false;
                }
            }

            value = new UTF8Encoding(false).GetString(bytes);
            return true;
        }

        private static byte[] CopyOriginalRowRecord(ExcelDataFile target, ExcelDataRow row)
        {
            int rowOffset = checked((int)row.Offset);
            if (rowOffset < 0 || rowOffset + 6 > target.Data.Length)
            {
                throw new InvalidDataException("Invalid EXD row offset.");
            }

            int bodySize = checked((int)Endian.ReadUInt32BE(target.Data, rowOffset));
            int recordSize = 6 + bodySize;
            if (rowOffset + recordSize > target.Data.Length)
            {
                throw new InvalidDataException("Invalid EXD row size.");
            }

            byte[] copy = new byte[recordSize];
            Buffer.BlockCopy(target.Data, rowOffset, copy, 0, copy.Length);
            return copy;
        }

        private static byte[] PadRowRecordToAlignment(byte[] rowRecord)
        {
            int paddingSize = (4 - (rowRecord.Length % 4)) % 4;
            if (paddingSize == 0)
            {
                return rowRecord;
            }

            byte[] padded = new byte[rowRecord.Length + paddingSize];
            Buffer.BlockCopy(rowRecord, 0, padded, 0, rowRecord.Length);
            uint bodySize = Endian.ReadUInt32BE(padded, 0);
            Endian.WriteUInt32BE(padded, 0, checked(bodySize + (uint)paddingSize));
            return padded;
        }

        private static RowPatchResult PatchRow(
            ExcelDataFile target,
            ExcelHeader targetHeader,
            ExcelHeader sourceHeader,
            List<int> stringColumns,
            ExcelDataRow targetRow,
            SourceRowRef sourceRow,
            StringPatchPolicy patchPolicy)
        {
            int rowOffset = checked((int)targetRow.Offset);
            if (rowOffset < 0 || rowOffset + 6 > target.Data.Length)
            {
                throw new InvalidDataException("Invalid EXD row offset.");
            }

            uint originalBodySize = Endian.ReadUInt32BE(target.Data, rowOffset);
            ushort rowCount = Endian.ReadUInt16BE(target.Data, rowOffset + 4);
            if (rowCount != 1)
            {
                throw new InvalidDataException("Default EXD row unexpectedly contains multiple rows.");
            }

            int fixedOffset = rowOffset + 6;
            int fixedSize = targetHeader.DataOffset;
            if (fixedOffset + fixedSize > target.Data.Length || fixedSize < 0)
            {
                throw new InvalidDataException("Invalid EXD fixed row size.");
            }

            byte[] fixedData = new byte[fixedSize];
            Buffer.BlockCopy(target.Data, fixedOffset, fixedData, 0, fixedData.Length);

            bool touched = false;
            int protectedUiStrings = 0;
            MemoryStream stringData = new MemoryStream();
            for (int i = 0; i < stringColumns.Count; i++)
            {
                int columnIndex = stringColumns[i];
                ExcelColumnDefinition targetColumn = targetHeader.Columns[columnIndex];
                if (targetColumn.Offset + 4 > fixedData.Length)
                {
                    continue;
                }

                byte[] original = target.GetStringBytes(targetRow, targetHeader, columnIndex);
                if (original == null)
                {
                    original = new byte[0];
                }

                byte[] replacement = sourceRow.File.GetStringBytesByColumnOffset(sourceRow.Row, sourceHeader, targetColumn.Offset);
                byte[] selected = original;
                if (replacement != null && replacement.Length > 0)
                {
                    if (ShouldKeepOriginalForUiGlyph(patchPolicy, original, replacement))
                    {
                        protectedUiStrings++;
                    }
                    else
                    {
                        selected = replacement;
                        if (!BytesEqual(original, replacement))
                        {
                            touched = true;
                        }
                    }
                }

                uint newStringOffset = checked((uint)stringData.Position);
                Endian.WriteUInt32BE(fixedData, targetColumn.Offset, newStringOffset);
                stringData.Write(selected, 0, selected.Length);
                stringData.WriteByte(0);
            }

            if (!touched)
            {
                return new RowPatchResult(CopyOriginalRowRecord(target, targetRow), false, protectedUiStrings);
            }

            byte[] strings = stringData.ToArray();
            int bodySizeWithoutPadding = checked(fixedData.Length + strings.Length);
            int rowRecordSizeWithoutPadding = checked(6 + bodySizeWithoutPadding);
            int paddingSize = (4 - (rowRecordSizeWithoutPadding % 4)) % 4;
            uint newBodySize = checked((uint)(bodySizeWithoutPadding + paddingSize));

            MemoryStream rowOutput = new MemoryStream(6 + (int)newBodySize);
            Endian.WriteUInt32BE(rowOutput, newBodySize);
            Endian.WriteUInt16BE(rowOutput, rowCount);
            rowOutput.Write(fixedData, 0, fixedData.Length);
            rowOutput.Write(strings, 0, strings.Length);
            for (int i = 0; i < paddingSize; i++)
            {
                rowOutput.WriteByte(0);
            }

            if (originalBodySize == 0 && newBodySize == 0)
            {
                throw new InvalidDataException("Unexpected empty EXD row.");
            }

            return new RowPatchResult(rowOutput.ToArray(), true, protectedUiStrings);
        }

        private static bool ShouldKeepOriginalForUiGlyph(StringPatchPolicy patchPolicy, byte[] original, byte[] replacement)
        {
            if (patchPolicy == null || !patchPolicy.ProtectShortNonKoreanUiTokens)
            {
                return false;
            }

            if (!IsShortNonKoreanUiToken(replacement))
            {
                return false;
            }

            if (ContainsSeStringControl(replacement))
            {
                return true;
            }

            return IsShortNonKoreanUiToken(original);
        }

        private static bool IsShortNonKoreanUiToken(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || ContainsHangul(bytes))
            {
                return false;
            }

            string visibleAscii = ExtractVisibleAscii(bytes).Trim();
            bool hasSeStringControl = ContainsSeStringControl(bytes);
            if (visibleAscii.Length == 0)
            {
                return hasSeStringControl;
            }

            // Addon rows also carry compact SeString icon/glyph markers. They are not translations.
            if (hasSeStringControl && bytes.Length <= 64 && visibleAscii.Length <= 16)
            {
                return true;
            }

            if (visibleAscii.Length > 8)
            {
                return false;
            }

            if (IsAsciiDigitOrSymbolOnly(visibleAscii))
            {
                return true;
            }

            return visibleAscii.Length <= 2 && IsAsciiTokenOnly(visibleAscii);
        }

        private static bool ContainsHangul(byte[] bytes)
        {
            string text = new UTF8Encoding(false, false).GetString(bytes);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if ((ch >= '\uac00' && ch <= '\ud7a3') ||
                    (ch >= '\u1100' && ch <= '\u11ff') ||
                    (ch >= '\u3130' && ch <= '\u318f'))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractVisibleAscii(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= 0x20 && value <= 0x7E)
                {
                    builder.Append((char)value);
                }
            }

            return builder.ToString();
        }

        private static bool ContainsSeStringControl(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x02 || bytes[i] == 0x03)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAsciiDigitOrSymbolOnly(string value)
        {
            bool hasDigitOrSymbol = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                if (ch >= '0' && ch <= '9')
                {
                    hasDigitOrSymbol = true;
                    continue;
                }

                if (IsAsciiSymbol(ch))
                {
                    hasDigitOrSymbol = true;
                    continue;
                }

                return false;
            }

            return hasDigitOrSymbol;
        }

        private static bool IsAsciiTokenOnly(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if ((ch >= '0' && ch <= '9') ||
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= 'a' && ch <= 'z') ||
                    IsAsciiSymbol(ch) ||
                    char.IsWhiteSpace(ch))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool IsAsciiSymbol(char ch)
        {
            return (ch >= '!' && ch <= '/') ||
                   (ch >= ':' && ch <= '@') ||
                   (ch >= '[' && ch <= '`') ||
                   (ch >= '{' && ch <= '~');
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
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

        private sealed class RowPatchPlan
        {
            public readonly SourceRowRef SourceRow;
            public readonly RowPatchMode Mode;

            public RowPatchPlan(SourceRowRef sourceRow, RowPatchMode mode)
            {
                SourceRow = sourceRow;
                Mode = mode;
            }
        }

        private sealed class RowPatchResult
        {
            public readonly byte[] Data;
            public readonly bool Touched;
            public readonly int ProtectedUiStrings;

            public RowPatchResult(byte[] data, bool touched, int protectedUiStrings)
            {
                Data = data;
                Touched = touched;
                ProtectedUiStrings = protectedUiStrings;
            }
        }

        private enum RowPatchMode
        {
            StringKey,
            RowKey
        }
    }

    internal sealed class ExdPatchResult
    {
        public byte[] Data;
        public bool Changed;
        public int RowsPatched;
        public int StringKeyRows;
        public int RowKeyRows;
        public int ProtectedUiStrings;
    }

    internal sealed class StringPatchPolicy
    {
        public static readonly StringPatchPolicy Default = new StringPatchPolicy(false);
        public static readonly StringPatchPolicy ProtectAddonUiGlyphs = new StringPatchPolicy(true);

        public readonly bool ProtectShortNonKoreanUiTokens;

        private StringPatchPolicy(bool protectShortNonKoreanUiTokens)
        {
            ProtectShortNonKoreanUiTokens = protectShortNonKoreanUiTokens;
        }
    }

    internal sealed class ExdSourceMaps
    {
        public readonly Dictionary<string, SourceRowRef> StringKeyRows = new Dictionary<string, SourceRowRef>(StringComparer.Ordinal);
        public readonly Dictionary<uint, SourceRowRef> RowKeyRows = new Dictionary<uint, SourceRowRef>();
    }

    internal sealed class SourceRowRef
    {
        public readonly ExcelDataFile File;
        public readonly ExcelDataRow Row;

        public SourceRowRef(ExcelDataFile file, ExcelDataRow row)
        {
            File = file;
            Row = row;
        }
    }
}
