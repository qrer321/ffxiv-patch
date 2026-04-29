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
            Dictionary<string, ExcelDataRow> sourceStringKeyRows = BuildStringKeyMap(source, sourceHeader);
            Dictionary<uint, RowPatchPlan> rowPlans = BuildStringKeyPlans(target, targetHeader, sourceStringKeyRows);
            bool usingStringKeyPlans = rowPlans.Count > 0;

            if (!usingStringKeyPlans && allowRowKeyFallback)
            {
                rowPlans = BuildRowKeyPlans(target, source);
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
                        rowResult = PatchRow(target, source, targetHeader, sourceHeader, stringColumns, row, plan.SourceRow);
                    }
                    catch
                    {
                        rowResult = null;
                    }
                }

                byte[] rowRecord;
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

        private static Dictionary<string, ExcelDataRow> BuildStringKeyMap(ExcelDataFile source, ExcelHeader sourceHeader)
        {
            Dictionary<string, ExcelDataRow> map = new Dictionary<string, ExcelDataRow>(StringComparer.Ordinal);
            if (!IsStringKeyHeader(sourceHeader))
            {
                return map;
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

                map.Add(key, row);
            }

            return map;
        }

        private static Dictionary<uint, RowPatchPlan> BuildStringKeyPlans(
            ExcelDataFile target,
            ExcelHeader targetHeader,
            Dictionary<string, ExcelDataRow> sourceStringKeyRows)
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

                ExcelDataRow sourceRow;
                if (!StringKeyRegex.IsMatch(key) || !sourceStringKeyRows.TryGetValue(key, out sourceRow))
                {
                    continue;
                }

                plans[row.RowId] = new RowPatchPlan(sourceRow, RowPatchMode.StringKey);
            }

            return plans;
        }

        private static Dictionary<uint, RowPatchPlan> BuildRowKeyPlans(ExcelDataFile target, ExcelDataFile source)
        {
            Dictionary<uint, RowPatchPlan> plans = new Dictionary<uint, RowPatchPlan>();
            for (int i = 0; i < target.Rows.Count; i++)
            {
                ExcelDataRow sourceRow;
                if (source.TryGetRow(target.Rows[i].RowId, out sourceRow))
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
            ExcelDataFile source,
            ExcelHeader targetHeader,
            ExcelHeader sourceHeader,
            List<int> stringColumns,
            ExcelDataRow targetRow,
            ExcelDataRow sourceRow)
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

                byte[] replacement = source.GetStringBytesByColumnOffset(sourceRow, sourceHeader, targetColumn.Offset);
                byte[] selected = original;
                if (replacement != null && replacement.Length > 0)
                {
                    selected = replacement;
                    if (!BytesEqual(original, replacement))
                    {
                        touched = true;
                    }
                }

                uint newStringOffset = checked((uint)stringData.Position);
                Endian.WriteUInt32BE(fixedData, targetColumn.Offset, newStringOffset);
                stringData.Write(selected, 0, selected.Length);
                stringData.WriteByte(0);
            }

            if (!touched)
            {
                return new RowPatchResult(CopyOriginalRowRecord(target, targetRow), false);
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

            return new RowPatchResult(rowOutput.ToArray(), true);
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
            public readonly ExcelDataRow SourceRow;
            public readonly RowPatchMode Mode;

            public RowPatchPlan(ExcelDataRow sourceRow, RowPatchMode mode)
            {
                SourceRow = sourceRow;
                Mode = mode;
            }
        }

        private sealed class RowPatchResult
        {
            public readonly byte[] Data;
            public readonly bool Touched;

            public RowPatchResult(byte[] data, bool touched)
            {
                Data = data;
                Touched = touched;
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
    }
}
