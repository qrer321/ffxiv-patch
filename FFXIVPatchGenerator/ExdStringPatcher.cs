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

            PatchSheetPolicy sheetPolicy = patchPolicy == null ? PatchSheetPolicy.Empty : patchPolicy.SheetPolicy;
            Dictionary<uint, RowPatchPlan> rowPlans = BuildStringKeyPlans(target, targetHeader, sourceMaps.StringKeyRows, sourceMaps.RowKeyRows, sheetPolicy);
            bool usingStringKeyPlans = rowPlans.Count > 0;

            bool useRowKeyPlans = allowRowKeyFallback || sheetPolicy.HasSourceRowOverrides || sheetPolicy.HasGlobalEnglishRows || sheetPolicy.HasGlobalTargetRows;
            if (useRowKeyPlans)
            {
                Dictionary<uint, RowPatchPlan> rowKeyPlans = BuildRowKeyPlans(target, sourceMaps.RowKeyRows, sheetPolicy, allowRowKeyFallback);
                if (!usingStringKeyPlans)
                {
                    rowPlans = rowKeyPlans;
                }
                else
                {
                    foreach (KeyValuePair<uint, RowPatchPlan> rowKeyPlan in rowKeyPlans)
                    {
                        if (sheetPolicy.ShouldUseGlobalFallbackRow(rowKeyPlan.Key) ||
                            sheetPolicy.SourceRowOverrides.ContainsKey(rowKeyPlan.Key))
                        {
                            rowPlans[rowKeyPlan.Key] = rowKeyPlan.Value;
                        }
                        else if (!rowPlans.ContainsKey(rowKeyPlan.Key))
                        {
                            rowPlans.Add(rowKeyPlan.Key, rowKeyPlan.Value);
                        }
                    }
                }
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
                    result.RsvRows += rowResult.RsvRows;
                    result.RsvStrings += rowResult.RsvStrings;
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
                    AddRowKeyRows(maps.RowKeyRows, source, sourceHeader);
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

                map.Add(key, new SourceRowRef(source, row, sourceHeader));
            }
        }

        private static void AddRowKeyRows(Dictionary<uint, SourceRowRef> map, ExcelDataFile source, ExcelHeader sourceHeader)
        {
            for (int i = 0; i < source.Rows.Count; i++)
            {
                ExcelDataRow row = source.Rows[i];
                if (!map.ContainsKey(row.RowId))
                {
                    map.Add(row.RowId, new SourceRowRef(source, row, sourceHeader));
                }
            }
        }

        private static Dictionary<uint, RowPatchPlan> BuildStringKeyPlans(
            ExcelDataFile target,
            ExcelHeader targetHeader,
            Dictionary<string, SourceRowRef> sourceStringKeyRows,
            Dictionary<uint, SourceRowRef> sourceRowKeyRows,
            PatchSheetPolicy sheetPolicy)
        {
            Dictionary<uint, RowPatchPlan> plans = new Dictionary<uint, RowPatchPlan>();
            if (sourceStringKeyRows.Count == 0 || !IsStringKeyHeader(targetHeader))
            {
                return plans;
            }

            for (int i = 0; i < target.Rows.Count; i++)
            {
                ExcelDataRow row = target.Rows[i];
                if (sheetPolicy.ShouldKeepRow(row.RowId))
                {
                    continue;
                }

                if (sheetPolicy.ShouldUseGlobalFallbackRow(row.RowId))
                {
                    continue;
                }

                string key;
                if (!TryGetPlainString(target, targetHeader, row, 0, out key))
                {
                    continue;
                }

                SourceRowRef sourceRow;
                uint overrideSourceRowId;
                if (sheetPolicy.SourceRowOverrides.TryGetValue(row.RowId, out overrideSourceRowId) &&
                    sourceRowKeyRows.TryGetValue(overrideSourceRowId, out sourceRow))
                {
                    plans[row.RowId] = new RowPatchPlan(sourceRow, RowPatchMode.RowKey);
                    continue;
                }

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
            Dictionary<uint, SourceRowRef> sourceRowKeyRows,
            PatchSheetPolicy sheetPolicy,
            bool allowRowKeyFallback)
        {
            Dictionary<uint, RowPatchPlan> plans = new Dictionary<uint, RowPatchPlan>();
            for (int i = 0; i < target.Rows.Count; i++)
            {
                uint targetRowId = target.Rows[i].RowId;
                if (sheetPolicy.ShouldKeepRow(targetRowId))
                {
                    continue;
                }

                uint sourceRowId;
                bool hasSourceOverride = sheetPolicy.SourceRowOverrides.TryGetValue(targetRowId, out sourceRowId);
                bool hasGlobalFallback = sheetPolicy.ShouldUseGlobalFallbackRow(targetRowId);
                if (!allowRowKeyFallback && !hasSourceOverride && !hasGlobalFallback)
                {
                    continue;
                }

                if (!hasSourceOverride)
                {
                    sourceRowId = targetRowId;
                }

                SourceRowRef sourceRow;
                if (sourceRowKeyRows.TryGetValue(sourceRowId, out sourceRow))
                {
                    plans[targetRowId] = new RowPatchPlan(sourceRow, RowPatchMode.RowKey);
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
            PatchSheetPolicy sheetPolicy = patchPolicy == null ? PatchSheetPolicy.Empty : patchPolicy.SheetPolicy;
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
            int rsvStrings = 0;
            bool rowHasRsv = false;
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

                ExcelHeader rowSourceHeader = sourceRow.Header ?? sourceHeader;
                byte[] replacement = sourceRow.File.GetStringBytesByColumnOffset(sourceRow.Row, rowSourceHeader, targetColumn.Offset);
                byte[] selected = original;
                if (sheetPolicy.ShouldKeepColumn(targetRow.RowId, targetColumn.Offset))
                {
                    selected = original;
                }
                else
                {
                    ColumnRemap columnRemap = sheetPolicy.GetColumnRemap(targetRow.RowId, targetColumn.Offset);
                    if (columnRemap.Mode == ColumnRemapMode.SourceColumn && columnRemap.SourceColumnOffset.HasValue)
                    {
                        replacement = sourceRow.File.GetStringBytesByColumnOffset(sourceRow.Row, rowSourceHeader, columnRemap.SourceColumnOffset.Value);
                    }
                    else if (columnRemap.Mode == ColumnRemapMode.Literal)
                    {
                        replacement = columnRemap.LiteralBytes;
                    }
                    else if (columnRemap.Mode == ColumnRemapMode.TemplateAroundFirstPayload)
                    {
                        byte[] templatedReplacement;
                        if (TryBuildTemplateAroundFirstPayload(
                            original,
                            columnRemap.TemplatePrefixBytes,
                            columnRemap.TemplateSuffixBytes,
                            out templatedReplacement))
                        {
                            replacement = templatedReplacement;
                        }
                    }

                    bool forceReplacement = columnRemap.Mode == ColumnRemapMode.Literal;
                    if (replacement != null &&
                        (replacement.Length > 0 || forceReplacement || sheetPolicy.ShouldUseGlobalFallbackRow(targetRow.RowId)))
                    {
                        if (!sheetPolicy.ShouldUseGlobalFallbackRow(targetRow.RowId) &&
                            ShouldKeepOriginalForUiStructure(patchPolicy, original, replacement))
                        {
                            byte[] hybrid;
                            if (TryBuildHybridUiReplacement(original, replacement, out hybrid))
                            {
                                selected = hybrid;
                                protectedUiStrings++;
                                if (!BytesEqual(original, hybrid))
                                {
                                    touched = true;
                                }
                            }
                            else
                            {
                                protectedUiStrings++;
                            }
                        }
                        else if (!sheetPolicy.ShouldUseGlobalFallbackRow(targetRow.RowId) &&
                                 ShouldKeepOriginalForUiGlyph(patchPolicy, original, replacement))
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
                }

                if (sheetPolicy.GlobalEnglishRows.Contains(targetRow.RowId))
                {
                    byte[] normalizedEnglish = NormalizeGlobalEnglishFallbackBytes(selected);
                    if (!BytesEqual(selected, normalizedEnglish))
                    {
                        selected = normalizedEnglish;
                        touched = true;
                    }
                }

                if (ContainsRsvToken(selected))
                {
                    rsvStrings++;
                    rowHasRsv = true;
                }

                uint newStringOffset = checked((uint)stringData.Position);
                Endian.WriteUInt32BE(fixedData, targetColumn.Offset, newStringOffset);
                stringData.Write(selected, 0, selected.Length);
                stringData.WriteByte(0);
            }

            if (!touched)
            {
                return new RowPatchResult(CopyOriginalRowRecord(target, targetRow), false, protectedUiStrings, rowHasRsv ? 1 : 0, rsvStrings);
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

            return new RowPatchResult(rowOutput.ToArray(), true, protectedUiStrings, rowHasRsv ? 1 : 0, rsvStrings);
        }

        private static bool ShouldKeepOriginalForUiStructure(StringPatchPolicy patchPolicy, byte[] original, byte[] replacement)
        {
            if (patchPolicy == null || !patchPolicy.ProtectUiSeStringStructure)
            {
                return false;
            }

            if (original == null || replacement == null || replacement.Length == 0)
            {
                return false;
            }

            if (!ContainsSeStringControl(original) && !ContainsSeStringControl(replacement))
            {
                return false;
            }

            string originalSignature = BuildUiStructureSignature(original);
            string replacementSignature = BuildUiStructureSignature(replacement);
            return !string.Equals(originalSignature, replacementSignature, StringComparison.Ordinal);
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

        private static bool TryBuildTemplateAroundFirstPayload(
            byte[] original,
            byte[] prefix,
            byte[] suffix,
            out byte[] replacement)
        {
            replacement = null;
            if (original == null || original.Length == 0)
            {
                return false;
            }

            prefix = prefix ?? new byte[0];
            suffix = suffix ?? new byte[0];

            int cursor = 0;
            while (cursor < original.Length)
            {
                int tokenOffset;
                int tokenLength;
                if (TryFindFirstSeStringToken(original, cursor, out tokenOffset, out tokenLength))
                {
                    MemoryStream output = new MemoryStream(prefix.Length + tokenLength + suffix.Length);
                    output.Write(prefix, 0, prefix.Length);
                    output.Write(original, tokenOffset, tokenLength);
                    output.Write(suffix, 0, suffix.Length);
                    replacement = output.ToArray();
                    return true;
                }

                cursor++;
            }

            return false;
        }

        private static bool TryFindFirstSeStringToken(byte[] bytes, int offset, out int tokenOffset, out int tokenLength)
        {
            tokenOffset = 0;
            tokenLength = 0;
            for (int cursor = Math.Max(0, offset); cursor < bytes.Length; cursor++)
            {
                if (bytes[cursor] != 0x02)
                {
                    continue;
                }

                int tokenEnd = cursor;
                int controlSegments = 0;
                while (tokenEnd < bytes.Length)
                {
                    if (bytes[tokenEnd] == 0x02)
                    {
                        SePayloadBounds payload;
                        if (TryReadSePayload(bytes, tokenEnd, bytes.Length, out payload))
                        {
                            tokenEnd = payload.NextOffset;
                            controlSegments++;
                            continue;
                        }

                        int terminator = IndexOfByte(bytes, 0x03, tokenEnd + 1, Math.Min(bytes.Length, tokenEnd + 32));
                        if (terminator < 0)
                        {
                            break;
                        }

                        tokenEnd = terminator + 1;
                        controlSegments++;
                        continue;
                    }

                    if (controlSegments > 0 && !IsTextBoundary(bytes, tokenEnd))
                    {
                        tokenEnd++;
                        continue;
                    }

                    break;
                }

                if (controlSegments > 0 && tokenEnd > cursor)
                {
                    tokenOffset = cursor;
                    tokenLength = tokenEnd - cursor;
                    return true;
                }
            }

            return false;
        }

        private static int IndexOfByte(byte[] bytes, byte value, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsTextBoundary(byte[] bytes, int offset)
        {
            byte value = bytes[offset];
            if (value >= 0x20 && value <= 0x7E)
            {
                return true;
            }

            int length;
            return TryReadUtf8Scalar(bytes, offset, out length);
        }

        private static bool TryReadUtf8Scalar(byte[] bytes, int offset, out int length)
        {
            length = 0;
            byte first = bytes[offset];
            if (first < 0x80)
            {
                return false;
            }

            if (first >= 0xC2 && first <= 0xDF)
            {
                length = 2;
            }
            else if (first >= 0xE0 && first <= 0xEF)
            {
                length = 3;
            }
            else if (first >= 0xF0 && first <= 0xF4)
            {
                length = 4;
            }
            else
            {
                return false;
            }

            if (offset + length > bytes.Length)
            {
                return false;
            }

            for (int i = 1; i < length; i++)
            {
                if ((bytes[offset + i] & 0xC0) != 0x80)
                {
                    return false;
                }
            }

            return true;
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

        private static bool TryBuildHybridUiReplacement(byte[] original, byte[] replacement, out byte[] hybrid)
        {
            hybrid = null;
            if (original == null || replacement == null || original.Length == 0 || replacement.Length == 0)
            {
                return false;
            }

            bool replacementDropsDataCenterLookup =
                ContainsKnownDataCenterLookup(original) && !ContainsKnownDataCenterLookup(replacement);
            if (!ContainsHangul(replacement))
            {
                if (replacementDropsDataCenterLookup)
                {
                    hybrid = original;
                    return true;
                }

                return false;
            }

            List<ByteRange> replacementTextRanges = GetTopLevelTextRanges(replacement, true);
            if (replacementTextRanges.Count == 0)
            {
                return false;
            }

            List<ByteRange> originalTextRanges = GetTopLevelTextRanges(original, false);
            if (originalTextRanges.Count == 0)
            {
                return false;
            }

            // Some global confirmation rows split the sentence and the Japanese
            // suffix across SeString line-break payloads, while the Korean row is
            // already one complete sentence. Hybrid replacement would otherwise
            // produce strings such as "게임을 종료하시겠습니까? / よろしいですか？".
            if (!replacementDropsDataCenterLookup &&
                replacementTextRanges.Count < originalTextRanges.Count &&
                HasUnpairedJapaneseText(original, originalTextRanges, replacementTextRanges.Count))
            {
                hybrid = replacement;
                return true;
            }

            MemoryStream output = new MemoryStream(original.Length + replacement.Length);
            int replacementIndex = 0;
            int cursor = 0;
            bool changed = false;
            while (cursor < original.Length)
            {
                SePayloadBounds payload;
                if (original[cursor] == 0x02 && TryReadSePayload(original, cursor, original.Length, out payload))
                {
                    output.Write(original, cursor, payload.NextOffset - cursor);
                    cursor = payload.NextOffset;
                    continue;
                }

                int textStart = cursor;
                while (cursor < original.Length)
                {
                    if (original[cursor] == 0x02 && TryReadSePayload(original, cursor, original.Length, out payload))
                    {
                        break;
                    }

                    cursor++;
                }

                ByteRange originalRange = new ByteRange(textStart, cursor - textStart);
                if (replacementIndex < replacementTextRanges.Count && IsTranslatableUiText(original, originalRange))
                {
                    ByteRange replacementRange = replacementTextRanges[replacementIndex++];
                    output.Write(replacement, replacementRange.Offset, replacementRange.Length);
                    changed = true;
                }
                else
                {
                    output.Write(original, originalRange.Offset, originalRange.Length);
                }
            }

            if (!changed)
            {
                if (replacementDropsDataCenterLookup)
                {
                    hybrid = original;
                    return true;
                }

                return false;
            }

            hybrid = output.ToArray();
            return true;
        }

        private static bool HasUnpairedJapaneseText(byte[] bytes, List<ByteRange> textRanges, int pairedRangeCount)
        {
            for (int i = pairedRangeCount; i < textRanges.Count; i++)
            {
                ByteRange range = textRanges[i];
                if (ContainsJapaneseKana(bytes, range))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsJapaneseKana(byte[] bytes, ByteRange range)
        {
            if (bytes == null || range.Length <= 0)
            {
                return false;
            }

            string text = new UTF8Encoding(false, false).GetString(bytes, range.Offset, range.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if ((ch >= '\u3040' && ch <= '\u309F') ||
                    (ch >= '\u30A0' && ch <= '\u30FF'))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKnownDataCenterLookup(byte[] bytes)
        {
            return ContainsAscii(bytes, "WorldDCGroupType") ||
                   ContainsAscii(bytes, "WORLDDCGROUPTYPE_NAME") ||
                   ContainsAscii(bytes, "WorldPhysicalDC") ||
                   ContainsAscii(bytes, "WORLDPHYSICALDC_NAME") ||
                   ContainsAscii(bytes, "WorldRegionGroup") ||
                   ContainsAscii(bytes, "WORLDREGIONGROUP_NAME");
        }

        private static List<ByteRange> GetTopLevelTextRanges(byte[] bytes, bool requireHangul)
        {
            List<ByteRange> ranges = new List<ByteRange>();
            int cursor = 0;
            while (cursor < bytes.Length)
            {
                SePayloadBounds payload;
                if (bytes[cursor] == 0x02 && TryReadSePayload(bytes, cursor, bytes.Length, out payload))
                {
                    cursor = payload.NextOffset;
                    continue;
                }

                int textStart = cursor;
                while (cursor < bytes.Length)
                {
                    if (bytes[cursor] == 0x02 && TryReadSePayload(bytes, cursor, bytes.Length, out payload))
                    {
                        break;
                    }

                    cursor++;
                }

                ByteRange range = new ByteRange(textStart, cursor - textStart);
                if (IsMeaningfulUiText(bytes, range) && (!requireHangul || ContainsHangul(bytes, range)))
                {
                    ranges.Add(range);
                }
            }

            return ranges;
        }

        private static bool IsMeaningfulUiText(byte[] bytes, ByteRange range)
        {
            if (range.Length <= 0)
            {
                return false;
            }

            string text = new UTF8Encoding(false, false).GetString(bytes, range.Offset, range.Length).Trim();
            if (text.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!char.IsWhiteSpace(ch) && !IsAsciiSymbol(ch))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTranslatableUiText(byte[] bytes, ByteRange range)
        {
            if (!IsMeaningfulUiText(bytes, range))
            {
                return false;
            }

            string text = new UTF8Encoding(false, false).GetString(bytes, range.Offset, range.Length).Trim();
            return !IsAsciiDigitOrSymbolOnly(text);
        }

        private static string BuildUiStructureSignature(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            AppendSeStringPayloadSignature(bytes, 0, bytes.Length, builder, 0);
            AppendKnownUiReference(builder, bytes, "WorldDCGroupType");
            AppendKnownUiReference(builder, bytes, "WORLDDCGROUPTYPE_NAME");
            AppendKnownUiReference(builder, bytes, "WorldPhysicalDC");
            AppendKnownUiReference(builder, bytes, "WORLDPHYSICALDC_NAME");
            AppendKnownUiReference(builder, bytes, "WorldRegionGroup");
            AppendKnownUiReference(builder, bytes, "WORLDREGIONGROUP_NAME");
            return builder.ToString();
        }

        private static void AppendKnownUiReference(StringBuilder builder, byte[] bytes, string token)
        {
            if (ContainsAscii(bytes, token))
            {
                builder.Append("|ref:");
                builder.Append(token);
            }
        }

        private static bool ContainsAscii(byte[] bytes, string token)
        {
            if (bytes == null || string.IsNullOrEmpty(token) || bytes.Length < token.Length)
            {
                return false;
            }

            for (int i = 0; i <= bytes.Length - token.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < token.Length; j++)
                {
                    if (bytes[i + j] != (byte)token[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendSeStringPayloadSignature(byte[] bytes, int start, int end, StringBuilder builder, int depth)
        {
            if (depth > 8)
            {
                return;
            }

            int index = start;
            while (index < end)
            {
                SePayloadBounds payload;
                if (bytes[index] == 0x02 && TryReadSePayload(bytes, index, end, out payload))
                {
                    builder.Append("|m:");
                    builder.Append(payload.Type.ToString("x"));
                    builder.Append('[');
                    AppendSeStringPayloadSignature(bytes, payload.PayloadOffset, payload.PayloadOffset + payload.PayloadLength, builder, depth + 1);
                    builder.Append(']');
                    index = payload.NextOffset;
                    continue;
                }

                index++;
            }
        }

        private static bool TryReadSePayload(byte[] bytes, int offset, int end, out SePayloadBounds payload)
        {
            payload = new SePayloadBounds();
            if (offset < 0 || offset >= end || bytes[offset] != 0x02)
            {
                return false;
            }

            int cursor = offset + 1;
            uint payloadType;
            int typeLength;
            if (!TryReadSeExpressionUInt32(bytes, cursor, end, out payloadType, out typeLength))
            {
                return false;
            }

            cursor += typeLength;
            uint payloadLength;
            int payloadLengthLength;
            if (!TryReadSeExpressionUInt32(bytes, cursor, end, out payloadLength, out payloadLengthLength))
            {
                return false;
            }

            cursor += payloadLengthLength;
            if (payloadLength > int.MaxValue || cursor + (int)payloadLength >= end)
            {
                return false;
            }

            int payloadEnd = cursor + (int)payloadLength;
            if (payloadEnd >= end || bytes[payloadEnd] != 0x03)
            {
                return false;
            }

            payload.Type = payloadType;
            payload.PayloadOffset = cursor;
            payload.PayloadLength = (int)payloadLength;
            payload.NextOffset = payloadEnd + 1;
            return true;
        }

        private static bool TryReadSeExpressionUInt32(byte[] bytes, int offset, int end, out uint value, out int length)
        {
            value = 0;
            length = 0;
            if (offset < 0 || offset >= end)
            {
                return false;
            }

            byte marker = bytes[offset];
            if (marker > 0x00 && marker < 0xD0)
            {
                value = (uint)(marker - 1);
                length = 1;
                return true;
            }

            if (marker < 0xF0 || marker > 0xFE)
            {
                return false;
            }

            byte flags = (byte)(marker + 1);
            int requiredLength = 1;
            if ((flags & 0x08) != 0) requiredLength++;
            if ((flags & 0x04) != 0) requiredLength++;
            if ((flags & 0x02) != 0) requiredLength++;
            if ((flags & 0x01) != 0) requiredLength++;
            if (offset + requiredLength > end)
            {
                return false;
            }

            int cursor = offset + 1;
            if ((flags & 0x08) != 0) value |= (uint)(bytes[cursor++] << 24);
            if ((flags & 0x04) != 0) value |= (uint)(bytes[cursor++] << 16);
            if ((flags & 0x02) != 0) value |= (uint)(bytes[cursor++] << 8);
            if ((flags & 0x01) != 0) value |= bytes[cursor++];

            length = requiredLength;
            return true;
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

        private static bool ContainsHangul(byte[] bytes, ByteRange range)
        {
            string text = new UTF8Encoding(false, false).GetString(bytes, range.Offset, range.Length);
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

        private static bool ContainsRsvToken(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 5)
            {
                return false;
            }

            for (int i = 0; i <= bytes.Length - 5; i++)
            {
                if (bytes[i] == (byte)'_' &&
                    (bytes[i + 1] == (byte)'r' || bytes[i + 1] == (byte)'R') &&
                    (bytes[i + 2] == (byte)'s' || bytes[i + 2] == (byte)'S') &&
                    (bytes[i + 3] == (byte)'v' || bytes[i + 3] == (byte)'V') &&
                    bytes[i + 4] == (byte)'_')
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

        private static byte[] NormalizeGlobalEnglishFallbackBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return bytes ?? new byte[0];
            }

            byte[] normalized = ReplaceUtf8Sequence(bytes, new byte[] { 0xE3, 0x80, 0x80 }, new byte[] { 0x20 }); // U+3000 ideographic space
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE3, 0x83, 0xBB }, new byte[] { 0x2D }); // U+30FB katakana middle dot
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xC2, 0xA0 }, new byte[] { 0x20 }); // U+00A0 no-break space
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x90 }, new byte[] { 0x2D }); // U+2010 hyphen
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x91 }, new byte[] { 0x2D }); // U+2011 non-breaking hyphen
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x93 }, new byte[] { 0x2D }); // U+2013 en dash
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x94 }, new byte[] { 0x2D }); // U+2014 em dash
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x98 }, new byte[] { 0x27 }); // U+2018 left quote
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x99 }, new byte[] { 0x27 }); // U+2019 right quote
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x9C }, new byte[] { 0x22 }); // U+201C left double quote
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0x9D }, new byte[] { 0x22 }); // U+201D right double quote
            normalized = ReplaceUtf8Sequence(normalized, new byte[] { 0xE2, 0x80, 0xA6 }, new byte[] { 0x2E, 0x2E, 0x2E }); // U+2026 ellipsis
            return normalized;
        }

        private static byte[] ReplaceUtf8Sequence(byte[] bytes, byte[] search, byte[] replacement)
        {
            int firstMatch = IndexOfSequence(bytes, search, 0);
            if (firstMatch < 0)
            {
                return bytes;
            }

            MemoryStream output = new MemoryStream(bytes.Length);
            int readOffset = 0;
            int matchOffset = firstMatch;
            while (matchOffset >= 0)
            {
                output.Write(bytes, readOffset, matchOffset - readOffset);
                output.Write(replacement, 0, replacement.Length);
                readOffset = matchOffset + search.Length;
                matchOffset = IndexOfSequence(bytes, search, readOffset);
            }

            output.Write(bytes, readOffset, bytes.Length - readOffset);
            return output.ToArray();
        }

        private static int IndexOfSequence(byte[] bytes, byte[] search, int start)
        {
            if (search.Length == 0 || bytes.Length < search.Length)
            {
                return -1;
            }

            for (int i = Math.Max(0, start); i <= bytes.Length - search.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < search.Length; j++)
                {
                    if (bytes[i + j] != search[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return i;
                }
            }

            return -1;
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
            public readonly int RsvRows;
            public readonly int RsvStrings;

            public RowPatchResult(byte[] data, bool touched, int protectedUiStrings, int rsvRows, int rsvStrings)
            {
                Data = data;
                Touched = touched;
                ProtectedUiStrings = protectedUiStrings;
                RsvRows = rsvRows;
                RsvStrings = rsvStrings;
            }
        }

        private enum RowPatchMode
        {
            StringKey,
            RowKey
        }

        private struct SePayloadBounds
        {
            public uint Type;
            public int PayloadOffset;
            public int PayloadLength;
            public int NextOffset;
        }

        private struct ByteRange
        {
            public readonly int Offset;
            public readonly int Length;

            public ByteRange(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }
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
        public int RsvRows;
        public int RsvStrings;
    }

    internal sealed class StringPatchPolicy
    {
        public static readonly StringPatchPolicy Default = new StringPatchPolicy(null, false, false, PatchSheetPolicy.Empty);
        public static readonly StringPatchPolicy ProtectAddonUiGlyphs = new StringPatchPolicy("Addon", true, true, PatchSheetPolicy.Empty);

        public readonly string SheetName;
        public readonly bool ProtectShortNonKoreanUiTokens;
        public readonly bool ProtectUiSeStringStructure;
        public readonly PatchSheetPolicy SheetPolicy;

        public StringPatchPolicy(bool protectShortNonKoreanUiTokens, PatchSheetPolicy sheetPolicy)
            : this(protectShortNonKoreanUiTokens, protectShortNonKoreanUiTokens, sheetPolicy)
        {
        }

        public StringPatchPolicy(bool protectShortNonKoreanUiTokens, bool protectUiSeStringStructure, PatchSheetPolicy sheetPolicy)
            : this(null, protectShortNonKoreanUiTokens, protectUiSeStringStructure, sheetPolicy)
        {
        }

        public StringPatchPolicy(string sheetName, bool protectShortNonKoreanUiTokens, PatchSheetPolicy sheetPolicy)
            : this(sheetName, protectShortNonKoreanUiTokens, protectShortNonKoreanUiTokens, sheetPolicy)
        {
        }

        public StringPatchPolicy(string sheetName, bool protectShortNonKoreanUiTokens, bool protectUiSeStringStructure, PatchSheetPolicy sheetPolicy)
        {
            SheetName = sheetName;
            ProtectShortNonKoreanUiTokens = protectShortNonKoreanUiTokens;
            ProtectUiSeStringStructure = protectUiSeStringStructure;
            SheetPolicy = sheetPolicy ?? PatchSheetPolicy.Empty;
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
        public readonly ExcelHeader Header;

        public SourceRowRef(ExcelDataFile file, ExcelDataRow row)
            : this(file, row, null)
        {
        }

        public SourceRowRef(ExcelDataFile file, ExcelDataRow row, ExcelHeader header)
        {
            File = file;
            Row = row;
            Header = header;
        }
    }
}
