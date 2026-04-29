using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class ExcelRootList
    {
        public static List<string> Parse(byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            List<string> sheets = new List<string>();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int comma = line.IndexOf(',');
                string name = comma >= 0 ? line.Substring(0, comma) : line;
                name = name.Trim().Replace('\\', '/').ToLowerInvariant();
                if (name.Length > 0)
                {
                    sheets.Add(name);
                }
            }

            return sheets;
        }
    }

    internal sealed class ExcelHeader
    {
        public ushort DataOffset;
        public ushort ColumnCount;
        public ushort PageCount;
        public ushort LanguageCount;
        public ExcelVariant Variant;
        public uint RowCount;
        public readonly List<ExcelColumnDefinition> Columns = new List<ExcelColumnDefinition>();
        public readonly List<ExcelPageDefinition> Pages = new List<ExcelPageDefinition>();
        public readonly List<byte> Languages = new List<byte>();

        public static ExcelHeader Parse(byte[] data)
        {
            if (data.Length < 0x20 ||
                data[0] != (byte)'E' ||
                data[1] != (byte)'X' ||
                data[2] != (byte)'H' ||
                data[3] != (byte)'F')
            {
                throw new InvalidDataException("Invalid EXH file.");
            }

            ExcelHeader header = new ExcelHeader();
            header.DataOffset = Endian.ReadUInt16BE(data, 0x06);
            header.ColumnCount = Endian.ReadUInt16BE(data, 0x08);
            header.PageCount = Endian.ReadUInt16BE(data, 0x0A);
            header.LanguageCount = Endian.ReadUInt16BE(data, 0x0C);
            header.Variant = (ExcelVariant)data[0x11];
            header.RowCount = Endian.ReadUInt32BE(data, 0x14);

            int offset = 0x20;
            for (int i = 0; i < header.ColumnCount; i++)
            {
                ExcelColumnDefinition column = new ExcelColumnDefinition();
                column.Type = Endian.ReadUInt16BE(data, offset);
                column.Offset = Endian.ReadUInt16BE(data, offset + 2);
                header.Columns.Add(column);
                offset += 4;
            }

            for (int i = 0; i < header.PageCount; i++)
            {
                ExcelPageDefinition page = new ExcelPageDefinition();
                page.StartId = Endian.ReadUInt32BE(data, offset);
                page.RowCount = Endian.ReadUInt32BE(data, offset + 4);
                header.Pages.Add(page);
                offset += 8;
            }

            for (int i = 0; i < header.LanguageCount && offset < data.Length; i++)
            {
                header.Languages.Add(data[offset++]);
                while (offset < data.Length && data[offset++] != 0)
                {
                }
            }

            return header;
        }

        public bool HasLanguage(byte languageId)
        {
            for (int i = 0; i < Languages.Count; i++)
            {
                if (Languages[i] == languageId)
                {
                    return true;
                }
            }

            return false;
        }

        public List<int> GetStringColumnIndexes()
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Type == 0)
                {
                    indexes.Add(i);
                }
            }

            return indexes;
        }

        public int FindStringColumnIndexByOffset(ushort columnOffset)
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Type == 0 && Columns[i].Offset == columnOffset)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    internal sealed class ExcelDataFile
    {
        public byte[] Data;
        public ushort Version;
        public ushort Unknown1;
        public uint IndexSize;
        public uint DataSectionSize;
        public readonly List<ExcelDataRow> Rows = new List<ExcelDataRow>();
        private readonly Dictionary<uint, ExcelDataRow> _rowMap = new Dictionary<uint, ExcelDataRow>();

        public static ExcelDataFile Parse(byte[] data)
        {
            if (data.Length < 0x20 ||
                data[0] != (byte)'E' ||
                data[1] != (byte)'X' ||
                data[2] != (byte)'D' ||
                data[3] != (byte)'F')
            {
                throw new InvalidDataException("Invalid EXD file.");
            }

            ExcelDataFile file = new ExcelDataFile();
            file.Data = data;
            file.Version = Endian.ReadUInt16BE(data, 0x04);
            file.Unknown1 = Endian.ReadUInt16BE(data, 0x06);
            file.IndexSize = Endian.ReadUInt32BE(data, 0x08);
            file.DataSectionSize = Endian.ReadUInt32BE(data, 0x0C);

            int rowCount = (int)(file.IndexSize / 8);
            for (int i = 0; i < rowCount; i++)
            {
                int offset = 0x20 + i * 8;
                ExcelDataRow row = new ExcelDataRow();
                row.RowId = Endian.ReadUInt32BE(data, offset);
                row.Offset = Endian.ReadUInt32BE(data, offset + 4);
                file.Rows.Add(row);
                file._rowMap[row.RowId] = row;
            }

            return file;
        }

        public bool TryGetRow(uint rowId, out ExcelDataRow row)
        {
            return _rowMap.TryGetValue(rowId, out row);
        }

        public byte[] GetStringBytes(uint rowId, ExcelHeader header, int columnIndex)
        {
            ExcelDataRow row;
            if (!TryGetRow(rowId, out row))
            {
                return null;
            }

            return GetStringBytes(row, header, columnIndex);
        }

        public byte[] GetStringBytes(ExcelDataRow row, ExcelHeader header, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= header.Columns.Count)
            {
                return null;
            }

            ExcelColumnDefinition column = header.Columns[columnIndex];
            if (column.Type != 0)
            {
                return null;
            }

            int rowOffset = checked((int)row.Offset);
            if (rowOffset < 0 || rowOffset + 6 > Data.Length)
            {
                return null;
            }

            int rowBodySize = checked((int)Endian.ReadUInt32BE(Data, rowOffset));
            int rowDataOffset = rowOffset + 6;
            int rowEnd = rowDataOffset + rowBodySize;
            int fieldOffset = rowDataOffset + column.Offset;
            if (rowEnd > Data.Length || fieldOffset + 4 > rowEnd)
            {
                return null;
            }

            uint stringOffset = Endian.ReadUInt32BE(Data, fieldOffset);
            int stringStart = rowDataOffset + header.DataOffset + checked((int)stringOffset);
            if (stringStart < rowDataOffset || stringStart >= rowEnd)
            {
                return new byte[0];
            }

            int stringEnd = stringStart;
            while (stringEnd < rowEnd && Data[stringEnd] != 0)
            {
                stringEnd++;
            }

            byte[] result = new byte[stringEnd - stringStart];
            Buffer.BlockCopy(Data, stringStart, result, 0, result.Length);
            return result;
        }

        public byte[] GetStringBytesByColumnOffset(ExcelDataRow row, ExcelHeader header, ushort columnOffset)
        {
            int columnIndex = header.FindStringColumnIndexByOffset(columnOffset);
            if (columnIndex < 0)
            {
                return null;
            }

            return GetStringBytes(row, header, columnIndex);
        }
    }

    internal enum ExcelVariant
    {
        Unknown = 0,
        Default = 1,
        Subrows = 2
    }

    internal struct ExcelColumnDefinition
    {
        public ushort Type;
        public ushort Offset;
    }

    internal struct ExcelPageDefinition
    {
        public uint StartId;
        public uint RowCount;
    }

    internal struct ExcelDataRow
    {
        public uint RowId;
        public uint Offset;
    }

    internal static class LanguageCodes
    {
        public static byte ToId(string code)
        {
            switch ((code ?? string.Empty).ToLowerInvariant())
            {
                case "ja":
                    return 1;
                case "en":
                    return 2;
                case "de":
                    return 3;
                case "fr":
                    return 4;
                case "chs":
                    return 5;
                case "cht":
                    return 6;
                case "ko":
                    return 7;
                default:
                    return 0;
            }
        }
    }
}
