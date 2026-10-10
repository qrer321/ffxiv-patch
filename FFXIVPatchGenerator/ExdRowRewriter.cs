using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Rebuilds a Default-variant EXD page with one string column replaced for selected rows. Other rows
    // are copied byte-for-byte; replaced rows keep their fixed data and other string columns.
    internal static class ExdRowRewriter
    {
        public static byte[] RewriteStringColumn(
            ExcelHeader header,
            ExcelDataFile file,
            int columnIndex,
            Dictionary<uint, byte[]> replacementRows)
        {
            List<int> stringColumns = header.GetStringColumnIndexes();
            List<byte[]> rowRecords = new List<byte[]>();
            uint dataSectionSize = 0;

            for (int i = 0; i < file.Rows.Count; i++)
            {
                ExcelDataRow row = file.Rows[i];
                byte[] replacement;
                byte[] rowRecord = replacementRows.TryGetValue(row.RowId, out replacement)
                    ? RewriteRow(header, file, row, stringColumns, columnIndex, replacement)
                    : CopyOriginalRowRecord(file, row);

                rowRecords.Add(rowRecord);
                dataSectionSize += checked((uint)rowRecord.Length);
            }

            MemoryStream output = new MemoryStream(file.Data.Length + 1024);
            byte[] fileHeader = new byte[0x20];
            Buffer.BlockCopy(file.Data, 0, fileHeader, 0, fileHeader.Length);

            uint indexSize = checked((uint)(file.Rows.Count * 8));
            Endian.WriteUInt32BE(fileHeader, 0x08, indexSize);
            Endian.WriteUInt32BE(fileHeader, 0x0C, dataSectionSize);
            output.Write(fileHeader, 0, fileHeader.Length);

            uint currentRowOffset = checked((uint)(0x20 + indexSize));
            for (int i = 0; i < file.Rows.Count; i++)
            {
                Endian.WriteUInt32BE(output, file.Rows[i].RowId);
                Endian.WriteUInt32BE(output, currentRowOffset);
                currentRowOffset += checked((uint)rowRecords[i].Length);
            }

            for (int i = 0; i < rowRecords.Count; i++)
            {
                output.Write(rowRecords[i], 0, rowRecords[i].Length);
            }

            return output.ToArray();
        }

        private static byte[] RewriteRow(
            ExcelHeader header,
            ExcelDataFile file,
            ExcelDataRow row,
            List<int> stringColumns,
            int replacedColumnIndex,
            byte[] replacement)
        {
            int rowOffset = checked((int)row.Offset);
            uint bodySize = Endian.ReadUInt32BE(file.Data, rowOffset);
            ushort rowCount = Endian.ReadUInt16BE(file.Data, rowOffset + 4);
            int fixedOffset = rowOffset + 6;
            byte[] fixedData = new byte[header.DataOffset];
            Buffer.BlockCopy(file.Data, fixedOffset, fixedData, 0, fixedData.Length);

            MemoryStream stringData = new MemoryStream();
            for (int i = 0; i < stringColumns.Count; i++)
            {
                int columnIndex = stringColumns[i];
                ExcelColumnDefinition column = header.Columns[columnIndex];
                byte[] selected = columnIndex == replacedColumnIndex
                    ? replacement
                    : file.GetStringBytes(row, header, columnIndex) ?? new byte[0];

                uint newStringOffset = checked((uint)stringData.Position);
                Endian.WriteUInt32BE(fixedData, column.Offset, newStringOffset);
                stringData.Write(selected, 0, selected.Length);
                stringData.WriteByte(0);
            }

            byte[] strings = stringData.ToArray();
            int bodySizeWithoutPadding = checked(fixedData.Length + strings.Length);
            int rowRecordSizeWithoutPadding = checked(6 + bodySizeWithoutPadding);
            int paddingSize = (4 - (rowRecordSizeWithoutPadding % 4)) % 4;
            uint newBodySize = checked((uint)(bodySizeWithoutPadding + paddingSize));

            MemoryStream output = new MemoryStream(6 + (int)newBodySize);
            Endian.WriteUInt32BE(output, newBodySize);
            Endian.WriteUInt16BE(output, rowCount);
            output.Write(fixedData, 0, fixedData.Length);
            output.Write(strings, 0, strings.Length);
            for (int i = 0; i < paddingSize; i++)
            {
                output.WriteByte(0);
            }

            if (bodySize == 0 && newBodySize == 0)
            {
                throw new InvalidDataException("Unexpected empty EXD row.");
            }

            return output.ToArray();
        }

        private static byte[] CopyOriginalRowRecord(ExcelDataFile file, ExcelDataRow row)
        {
            int rowOffset = checked((int)row.Offset);
            int bodySize = checked((int)Endian.ReadUInt32BE(file.Data, rowOffset));
            int recordSize = checked(6 + bodySize);
            byte[] copy = new byte[recordSize];
            Buffer.BlockCopy(file.Data, rowOffset, copy, 0, copy.Length);
            return copy;
        }
    }
}
