using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static string GetFirstString(CompositeArchive archive, string sheet, uint rowId, string language)
        {
            return Encoding.UTF8.GetString(GetFirstStringBytes(archive, sheet, rowId, language));
        }

        private static List<string> GetStringColumns(CompositeArchive archive, string sheet, uint rowId, string language)
        {
            ExcelHeader header;
            ExcelDataFile file;
            ExcelDataRow row = ReadExcelRow(archive, sheet, rowId, language, out header, out file);

            List<string> values = new List<string>();
            List<int> stringColumns = header.GetStringColumnIndexes();
            for (int i = 0; i < stringColumns.Count; i++)
            {
                byte[] bytes = file.GetStringBytes(row, header, stringColumns[i]) ?? new byte[0];
                values.Add(Encoding.UTF8.GetString(bytes));
            }

            return values;
        }

        private static string GetStringColumnByOffset(CompositeArchive archive, string sheet, uint rowId, string language, ushort columnOffset)
        {
            ExcelHeader header;
            ExcelDataFile file;
            ExcelDataRow row = ReadExcelRow(archive, sheet, rowId, language, out header, out file);

            byte[] bytes = file.GetStringBytesByColumnOffset(row, header, columnOffset);
            if (bytes == null)
            {
                throw new InvalidDataException(sheet + "#" + rowId + " does not have string column offset " + columnOffset.ToString());
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static byte[] GetFirstStringBytes(CompositeArchive archive, string sheet, uint rowId, string language)
        {
            ExcelHeader header;
            ExcelDataFile file;
            ExcelDataRow row = ReadExcelRow(archive, sheet, rowId, language, out header, out file);

            List<int> stringColumns = header.GetStringColumnIndexes();
            if (stringColumns.Count == 0)
            {
                return new byte[0];
            }

            return file.GetStringBytes(row, header, stringColumns[0]) ?? new byte[0];
        }

        private static ExcelDataRow ReadExcelRow(
            CompositeArchive archive,
            string sheet,
            uint rowId,
            string language,
            out ExcelHeader header,
            out ExcelDataFile file)
        {
            header = ExcelHeader.Parse(archive.ReadFile("exd/" + sheet + ".exh"));
            file = null;

            byte languageId = LanguageToId(language);
            bool hasLanguageSuffix = header.HasLanguage(languageId);
            for (int i = 0; i < header.Pages.Count; i++)
            {
                ExcelPageDefinition page = header.Pages[i];
                if (rowId < page.StartId || rowId >= page.StartId + page.RowCount)
                {
                    continue;
                }

                string exdPath = BuildExdPath(sheet, page.StartId, language, hasLanguageSuffix);
                file = ExcelDataFile.Parse(archive.ReadFile(exdPath));

                ExcelDataRow row;
                if (!file.TryGetRow(rowId, out row))
                {
                    throw new InvalidDataException(sheet + "#" + rowId + " was not found");
                }

                return row;
            }

            throw new InvalidDataException(sheet + "#" + rowId + " is outside all pages");
        }

        private static string BuildExdPath(string sheet, uint pageStartId, string language, bool hasLanguageSuffix)
        {
            return "exd/" + sheet + "_" + pageStartId + (hasLanguageSuffix ? "_" + language : string.Empty) + ".exd";
        }

        private static byte LanguageToId(string language)
        {
            string normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "ja": return 1;
                case "en": return 2;
                case "de": return 3;
                case "fr": return 4;
                case "chs": return 5;
                case "cht": return 6;
                case "ko": return 7;
                default:
                    throw new ArgumentException("unsupported language: " + language);
            }
        }
    }
}
