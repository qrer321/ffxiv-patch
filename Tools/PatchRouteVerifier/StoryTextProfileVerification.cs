using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly Regex StoryStringKeyRegex = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
            private StoryProfileScanResult _storyProfileScan;

            private void VerifyStoryTextProfileScopes()
            {
                Console.WriteLine("[TEXT] Story profile exhaustive scope selection");
                StoryProfileScanResult result = GetStoryProfileScan();
                ReportFailures("story scope", result.ContentFailures);
                if (result.ContentFailures.Count == 0)
                {
                    Pass(
                        "story profile checked {0} sheets, {1} base pages, {2} base cells, {3} Korean source cells ({4} distinct from base), {5} missing-source fallback cells",
                        result.SheetsChecked,
                        result.BasePagesChecked,
                        result.BaseCellsChecked,
                        result.KoreanCellsChecked,
                        result.DistinctKoreanCellsChecked,
                        result.SourceFallbackCellsChecked);
                }
            }

            private void VerifyStoryInstanceContentBoundary()
            {
                Console.WriteLine("[TEXT] InstanceContentTextData story boundary");
                StoryProfileScanResult result = GetStoryProfileScan();
                ReportFailures("story scan", result.ContentFailures);
                ReportFailures("InstanceContentTextData boundary", result.BoundaryFailures);
                if (result.BoundaryBaseCells == 0 || result.BoundaryStoryCells == 0)
                {
                    Fail("boundary verification requires base cells below row 1000 and Korean source cells distinct from base at or above row 1000");
                }
                else if (result.ContentFailures.Count == 0 && result.BoundaryFailures.Count == 0)
                {
                    Pass(
                        "InstanceContentTextData row < 1000 kept {0} base cells; row >= 1000 selected {1} non-base Korean cells",
                        result.BoundaryBaseCells,
                        result.BoundaryStoryCells);
                }
            }

            private void VerifyStorySeStringStructure()
            {
                Console.WriteLine("[TEXT] Story SeString structure");
                StoryProfileScanResult result = GetStoryProfileScan();
                ReportFailures("story scan", result.ContentFailures);
                ReportFailures("story SeString", result.StructureFailures);
                if (result.StructuredCellsChecked == 0)
                {
                    Fail("SeString verification requires structured Korean story source cells");
                }
                else if (result.ContentFailures.Count == 0 && result.StructureFailures.Count == 0)
                {
                    Pass("validated {0} structured story cells", result.StructuredCellsChecked);
                }
            }

            private void VerifyStoryUiAssets()
            {
                Console.WriteLine("[TEXT] Story profile localized UI asset exclusion");
                LimitedFailures failures = new LimitedFailures();
                ScanStoryUiAssets(failures);
                ReportFailures("story UI asset", failures);
                if (failures.Count == 0)
                {
                    Pass("story output contains no generated 060000 UI archive files");
                }
            }

            private StoryProfileScanResult GetStoryProfileScan()
            {
                if (_storyProfileScan == null)
                {
                    _storyProfileScan = ScanStoryProfile();
                }

                return _storyProfileScan;
            }

            private StoryProfileScanResult ScanStoryProfile()
            {
                StoryProfileScanResult result = new StoryProfileScanResult();

                string generatedTextIndex = Path.Combine(_patchedSqpack, TextPrefix + ".index");
                if (!File.Exists(generatedTextIndex))
                {
                    result.ContentFailures.Add("generated text index is missing: " + generatedTextIndex);
                    return result;
                }

                if (_koreanText == null)
                {
                    result.ContentFailures.Add("--korea staged backup is required for story profile verification");
                    return result;
                }

                byte[] rootBytes;
                try
                {
                    rootBytes = _cleanText.ReadFile("exd/root.exl");
                }
                catch (Exception ex)
                {
                    result.ContentFailures.Add("clean root.exl could not be read: " + ex.Message);
                    return result;
                }

                List<string> sheets = ExcelRootList.Parse(rootBytes);
                TextScopePolicy storyPolicy = TextScopePolicy.CreateStory();
                for (int i = 0; i < sheets.Count; i++)
                {
                    string sheet = sheets[i];
                    if (!string.IsNullOrEmpty(_sheetLimit) &&
                        !string.Equals(sheet, _sheetLimit, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    try
                    {
                        ScanStorySheet(result, storyPolicy, sheet);
                    }
                    catch (Exception ex)
                    {
                        result.ContentFailures.Add(sheet + ": " + ex.Message);
                    }
                }

                if (result.DistinctKoreanCellsChecked == 0)
                {
                    result.ContentFailures.Add("no Korean story source distinct from clean target was compared");
                }

                return result;
            }

            private void ScanStoryUiAssets(LimitedFailures failures)
            {
                if (!Directory.Exists(_output))
                {
                    failures.Add("output directory is missing: " + _output);
                    return;
                }

                string[] files = Directory.GetFiles(_output, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileName(files[i]);
                    if (name.StartsWith(UiPrefix + ".", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("orig." + UiPrefix + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add("localized UI archive file must be absent: " + name);
                    }
                }
            }

            private void ScanStorySheet(
                StoryProfileScanResult result,
                TextScopePolicy storyPolicy,
                string sheet)
            {
                string headerPath = "exd/" + sheet + ".exh";
                if (!_cleanText.ContainsPath(headerPath))
                {
                    return;
                }

                byte[] cleanHeaderBytes = _cleanText.ReadFile(headerPath);
                byte[] patchedHeaderBytes = _patchedText.ReadFile(headerPath);
                if (!BytesEqual(cleanHeaderBytes, patchedHeaderBytes))
                {
                    result.ContentFailures.Add(sheet + ": EXH differs from clean target");
                }

                ExcelHeader cleanHeader = ExcelHeader.Parse(cleanHeaderBytes);
                List<int> stringColumns = cleanHeader.GetStringColumnIndexes();
                if (stringColumns.Count == 0)
                {
                    return;
                }

                result.SheetsChecked++;
                TextSheetScopePolicy sheetScope = storyPolicy.ForSheet(sheet);
                if (!sheetScope.MayUseKorean || cleanHeader.Variant != ExcelVariant.Default)
                {
                    ScanBaseOnlySheetPages(result, sheet, cleanHeader);
                    return;
                }

                ExcelHeader sourceHeader = ReadKoreanHeaderOrFallback(headerPath, cleanHeader);
                StorySourceRows sourceRows = LoadStorySourceRows(sheet, sourceHeader);
                bool cleanUsesLanguageSuffix = cleanHeader.HasLanguage(LanguageToId(_language));
                for (int pageIndex = 0; pageIndex < cleanHeader.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = cleanHeader.Pages[pageIndex];
                    string pagePath = BuildExdPath(sheet, page.StartId, _language, cleanUsesLanguageSuffix);
                    if (!_cleanText.ContainsPath(pagePath))
                    {
                        continue;
                    }

                    ExcelDataFile cleanFile = ExcelDataFile.Parse(_cleanText.ReadFile(pagePath));
                    ExcelDataFile patchedFile = ExcelDataFile.Parse(_patchedText.ReadFile(pagePath));
                    ScanStoryPage(
                        result,
                        sheet,
                        cleanHeader,
                        stringColumns,
                        sheetScope,
                        cleanFile,
                        patchedFile,
                        LoadNameFormReferencePage(sheet, page, cleanHeader, cleanUsesLanguageSuffix) ?? cleanFile,
                        sourceRows);
                }
            }

            private void ScanBaseOnlySheetPages(
                StoryProfileScanResult result,
                string sheet,
                ExcelHeader cleanHeader)
            {
                bool usesLanguageSuffix = cleanHeader.HasLanguage(LanguageToId(_language));
                for (int pageIndex = 0; pageIndex < cleanHeader.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = cleanHeader.Pages[pageIndex];
                    string pagePath = BuildExdPath(sheet, page.StartId, _language, usesLanguageSuffix);
                    if (!_cleanText.ContainsPath(pagePath))
                    {
                        continue;
                    }

                    result.BasePagesChecked++;
                    byte[] clean = _cleanText.ReadFile(pagePath);
                    byte[] patched = _patchedText.ReadFile(pagePath);
                    if (!BytesEqual(clean, patched))
                    {
                        result.ContentFailures.Add(pagePath + ": base-only page differs from clean target");
                    }
                }
            }

            private ExcelHeader ReadKoreanHeaderOrFallback(string headerPath, ExcelHeader cleanHeader)
            {
                if (!_koreanText.ContainsPath(headerPath))
                {
                    return cleanHeader;
                }

                try
                {
                    ExcelHeader sourceHeader = ExcelHeader.Parse(_koreanText.ReadFile(headerPath));
                    return sourceHeader.Variant == ExcelVariant.Default ? sourceHeader : cleanHeader;
                }
                catch
                {
                    return cleanHeader;
                }
            }

            private StorySourceRows LoadStorySourceRows(string sheet, ExcelHeader sourceHeader)
            {
                StorySourceRows rows = new StorySourceRows();
                bool sourceUsesLanguageSuffix = sourceHeader.HasLanguage(LanguageToId(_sourceLanguage));
                bool sourceHasStringKeys = IsStringKeyHeader(sourceHeader);
                for (int pageIndex = 0; pageIndex < sourceHeader.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = sourceHeader.Pages[pageIndex];
                    string pagePath = BuildExdPath(sheet, page.StartId, _sourceLanguage, sourceUsesLanguageSuffix);
                    if (!_koreanText.ContainsPath(pagePath))
                    {
                        continue;
                    }

                    ExcelDataFile file = ExcelDataFile.Parse(_koreanText.ReadFile(pagePath));
                    for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                    {
                        ExcelDataRow row = file.Rows[rowIndex];
                        StorySourceRow sourceRow = new StorySourceRow(file, row, sourceHeader);
                        if (!rows.ByRowId.ContainsKey(row.RowId))
                        {
                            rows.ByRowId.Add(row.RowId, sourceRow);
                        }

                        if (sourceHasStringKeys)
                        {
                            string key;
                            if (TryGetPlainString(file, sourceHeader, row, 0, out key) &&
                                StoryStringKeyRegex.IsMatch(key) &&
                                !rows.ByStringKey.ContainsKey(key))
                            {
                                rows.ByStringKey.Add(key, sourceRow);
                            }
                        }
                    }
                }

                return rows;
            }

            private void ScanStoryPage(
                StoryProfileScanResult result,
                string sheet,
                ExcelHeader cleanHeader,
                List<int> stringColumns,
                TextSheetScopePolicy sheetScope,
                ExcelDataFile cleanFile,
                ExcelDataFile patchedFile,
                ExcelDataFile nameFormReferenceFile,
                StorySourceRows sourceRows)
            {
                bool targetHasStringKeys = IsStringKeyHeader(cleanHeader);
                Dictionary<uint, byte[]> sayQuestTexts = ResolveExpectedSayQuestTexts(
                    sheet,
                    cleanHeader,
                    sheetScope,
                    cleanFile,
                    nameFormReferenceFile,
                    sourceRows);
                for (int rowIndex = 0; rowIndex < cleanFile.Rows.Count; rowIndex++)
                {
                    ExcelDataRow cleanRow = cleanFile.Rows[rowIndex];
                    ExcelDataRow patchedRow;
                    if (!patchedFile.TryGetRow(cleanRow.RowId, out patchedRow))
                    {
                        result.ContentFailures.Add(sheet + "#" + cleanRow.RowId + ": patched row is missing");
                        continue;
                    }

                    StorySourceRow sourceRow = ResolveStorySourceRow(
                        cleanFile,
                        cleanHeader,
                        cleanRow,
                        targetHasStringKeys,
                        sourceRows);
                    for (int columnListIndex = 0; columnListIndex < stringColumns.Count; columnListIndex++)
                    {
                        int columnIndex = stringColumns[columnListIndex];
                        ExcelColumnDefinition column = cleanHeader.Columns[columnIndex];
                        TextScope scope = sheetScope.Classify(cleanRow.RowId, column.Offset);
                        byte[] cleanBytes = cleanFile.GetStringBytes(cleanRow, cleanHeader, columnIndex) ?? new byte[0];
                        byte[] patchedBytes = patchedFile.GetStringBytes(patchedRow, cleanHeader, columnIndex) ?? new byte[0];
                        byte[] expectedBytes = cleanBytes;
                        bool koreanSourceDiffers = false;

                        if (scope == TextScope.Story)
                        {
                            byte[] sourceBytes = sourceRow == null
                                ? null
                                : sourceRow.File.GetStringBytesByColumnOffset(sourceRow.Row, sourceRow.Header, column.Offset);
                            if (sourceBytes != null && sourceBytes.Length > 0)
                            {
                                byte[] nameFormReference = nameFormReferenceFile.GetStringBytes(cleanRow.RowId, cleanHeader, columnIndex) ?? cleanBytes;
                                expectedBytes = ResolveExpectedSayQuestText(
                                    sayQuestTexts,
                                    cleanRow.RowId,
                                    column.Offset,
                                    ResolveExpectedStoryBytes(sourceBytes, nameFormReference));
                                result.KoreanCellsChecked++;
                                koreanSourceDiffers = !BytesEqual(cleanBytes, expectedBytes);
                                if (koreanSourceDiffers)
                                {
                                    result.DistinctKoreanCellsChecked++;
                                }
                                CheckStoryStructure(
                                    result,
                                    sheet,
                                    cleanRow.RowId,
                                    column.Offset,
                                    expectedBytes,
                                    patchedBytes);
                            }
                            else
                            {
                                // The generator's say quest pass also annotates cells that fell back to base text.
                                expectedBytes = ResolveExpectedSayQuestText(sayQuestTexts, cleanRow.RowId, column.Offset, cleanBytes);
                                result.SourceFallbackCellsChecked++;
                            }
                        }
                        else
                        {
                            result.BaseCellsChecked++;
                        }

                        string label = sheet + "#" + cleanRow.RowId + "/" + column.Offset;
                        if (!BytesEqual(expectedBytes, patchedBytes))
                        {
                            result.ContentFailures.Add(label + ": selected bytes differ from " + (scope == TextScope.Story ? "story policy source" : "clean base"));
                            if (IsInstanceContentTextData(sheet))
                            {
                                result.BoundaryFailures.Add(label + ": boundary source mismatch");
                            }
                        }

                        if (IsInstanceContentTextData(sheet))
                        {
                            if (cleanRow.RowId < 1000)
                            {
                                result.BoundaryBaseCells++;
                                if (scope != TextScope.Remainder)
                                {
                                    result.BoundaryFailures.Add(label + ": row < 1000 was not classified as remainder");
                                }
                            }
                            else
                            {
                                if (koreanSourceDiffers)
                                {
                                    result.BoundaryStoryCells++;
                                }
                                if (scope != TextScope.Story)
                                {
                                    result.BoundaryFailures.Add(label + ": row >= 1000 was not classified as story");
                                }
                            }
                        }
                    }
                }
            }

            // Mirrors the generator: the player name forms of the global Japanese text are applied to the
            // Korean source before RSV resolution.
            private byte[] ResolveExpectedStoryBytes(byte[] sourceBytes, byte[] nameFormReference)
            {
                byte[] named;
                if (PlayerNameFormTransfer.Apply(nameFormReference, sourceBytes, out named) == PlayerNameFormTransferStatus.Applied)
                {
                    sourceBytes = named;
                }

                if (_rsvResolver == null || !_rsvResolver.IsEnabled)
                {
                    return sourceBytes;
                }

                return _rsvResolver.Resolve(sourceBytes).Bytes;
            }

            // Mirrors the generator's say quest phrase pass on the expected Korean text of a quest page.
            // Rows absent from the result keep their expected Korean text.
            private Dictionary<uint, byte[]> ResolveExpectedSayQuestTexts(
                string sheet,
                ExcelHeader cleanHeader,
                TextSheetScopePolicy sheetScope,
                ExcelDataFile cleanFile,
                ExcelDataFile nameFormReferenceFile,
                StorySourceRows sourceRows)
            {
                Dictionary<uint, byte[]> none = new Dictionary<uint, byte[]>();
                if (_sayQuestPhrases != SayQuestPhraseMode.Base ||
                    !SayQuestPhraseLocalizer.IsCandidateSheet(sheet) ||
                    !SayQuestPhraseLocalizer.IsKeyTextLayout(cleanHeader))
                {
                    return none;
                }

                int textColumnIndex = cleanHeader.FindStringColumnIndexByOffset(4);
                bool targetHasStringKeys = IsStringKeyHeader(cleanHeader);
                List<SayQuestRowText> rows = SayQuestPhraseLocalizer.BuildRows(
                    cleanHeader,
                    cleanFile,
                    nameFormReferenceFile,
                    delegate(ExcelDataRow cleanRow)
                    {
                        byte[] cleanBytes = cleanFile.GetStringBytes(cleanRow, cleanHeader, textColumnIndex) ?? new byte[0];
                        StorySourceRow sourceRow = ResolveStorySourceRow(cleanFile, cleanHeader, cleanRow, targetHasStringKeys, sourceRows);
                        byte[] sourceBytes = sourceRow == null
                            ? null
                            : sourceRow.File.GetStringBytesByColumnOffset(sourceRow.Row, sourceRow.Header, 4);
                        if (sheetScope.Classify(cleanRow.RowId, 4) != TextScope.Story || sourceBytes == null || sourceBytes.Length == 0)
                        {
                            return cleanBytes;
                        }

                        byte[] nameFormReference = nameFormReferenceFile.GetStringBytes(cleanRow.RowId, cleanHeader, textColumnIndex) ?? cleanBytes;
                        return ResolveExpectedStoryBytes(sourceBytes, nameFormReference);
                    });

                return SayQuestPhraseLocalizer.Localize(rows).Replacements;
            }

            private static byte[] ResolveExpectedSayQuestText(
                Dictionary<uint, byte[]> sayQuestTexts,
                uint rowId,
                ushort columnOffset,
                byte[] expected)
            {
                byte[] sayQuestText;
                return columnOffset == 4 && sayQuestTexts.TryGetValue(rowId, out sayQuestText) ? sayQuestText : expected;
            }

            // The generator copies player name forms from the Japanese page; an English target uses the
            // clean Japanese page as reference, a Japanese target its own clean page (null).
            private ExcelDataFile LoadNameFormReferencePage(
                string sheet,
                ExcelPageDefinition page,
                ExcelHeader cleanHeader,
                bool cleanUsesLanguageSuffix)
            {
                const string referenceLanguage = "ja";
                if (!cleanUsesLanguageSuffix ||
                    string.Equals(_language, referenceLanguage, StringComparison.OrdinalIgnoreCase) ||
                    !cleanHeader.HasLanguage(LanguageToId(referenceLanguage)))
                {
                    return null;
                }

                string referencePath = BuildExdPath(sheet, page.StartId, referenceLanguage, true);
                if (!_cleanText.ContainsPath(referencePath))
                {
                    return null;
                }

                // Like the generator, an unreadable optional reference falls back to the target page.
                try
                {
                    return ExcelDataFile.Parse(_cleanText.ReadFile(referencePath));
                }
                catch (InvalidDataException)
                {
                    return null;
                }
            }

            private void CheckStoryStructure(
                StoryProfileScanResult result,
                string sheet,
                uint rowId,
                ushort columnOffset,
                byte[] expected,
                byte[] actual)
            {
                string expectedSignature;
                string expectedError;
                string actualSignature;
                string actualError;
                bool expectedValid = SeStringStructureInspector.TryBuildSignature(expected, out expectedSignature, out expectedError);
                bool actualValid = SeStringStructureInspector.TryBuildSignature(actual, out actualSignature, out actualError);
                if (string.IsNullOrEmpty(expectedSignature) && string.IsNullOrEmpty(actualSignature) && expectedValid && actualValid)
                {
                    return;
                }

                result.StructuredCellsChecked++;
                string label = sheet + "#" + rowId + "/" + columnOffset;
                if (!expectedValid)
                {
                    result.StructureFailures.Add(label + ": Korean source SeString is malformed: " + expectedError);
                }
                if (!actualValid)
                {
                    result.StructureFailures.Add(label + ": patched SeString is malformed: " + actualError);
                }
                if (expectedValid && actualValid && !string.Equals(expectedSignature, actualSignature, StringComparison.Ordinal))
                {
                    result.StructureFailures.Add(label + ": SeString signature differs; expected " + expectedSignature + ", actual " + actualSignature);
                }
            }

            private static StorySourceRow ResolveStorySourceRow(
                ExcelDataFile cleanFile,
                ExcelHeader cleanHeader,
                ExcelDataRow cleanRow,
                bool targetHasStringKeys,
                StorySourceRows sourceRows)
            {
                StorySourceRow sourceRow;
                if (targetHasStringKeys)
                {
                    string key;
                    if (TryGetPlainString(cleanFile, cleanHeader, cleanRow, 0, out key) &&
                        StoryStringKeyRegex.IsMatch(key) &&
                        sourceRows.ByStringKey.TryGetValue(key, out sourceRow))
                    {
                        return sourceRow;
                    }
                }

                sourceRows.ByRowId.TryGetValue(cleanRow.RowId, out sourceRow);
                return sourceRow;
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

                value = Encoding.UTF8.GetString(bytes);
                return true;
            }

            private static bool IsStringKeyHeader(ExcelHeader header)
            {
                List<int> stringColumns = header.GetStringColumnIndexes();
                return stringColumns.Count == 2 &&
                       header.FindStringColumnIndexByOffset(0) >= 0 &&
                       header.FindStringColumnIndexByOffset(4) >= 0;
            }

            private static bool IsInstanceContentTextData(string sheet)
            {
                return string.Equals(sheet, "InstanceContentTextData", StringComparison.OrdinalIgnoreCase);
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

            private void ReportFailures(string label, LimitedFailures failures)
            {
                for (int i = 0; i < failures.Samples.Count; i++)
                {
                    Fail("{0}: {1}", label, failures.Samples[i]);
                }
                if (failures.Count > failures.Samples.Count)
                {
                    Fail("{0}: {1} additional failures omitted", label, failures.Count - failures.Samples.Count);
                }
            }

            private sealed class StorySourceRows
            {
                public readonly Dictionary<string, StorySourceRow> ByStringKey = new Dictionary<string, StorySourceRow>(StringComparer.Ordinal);
                public readonly Dictionary<uint, StorySourceRow> ByRowId = new Dictionary<uint, StorySourceRow>();
            }

            private sealed class StorySourceRow
            {
                public readonly ExcelDataFile File;
                public readonly ExcelDataRow Row;
                public readonly ExcelHeader Header;

                public StorySourceRow(ExcelDataFile file, ExcelDataRow row, ExcelHeader header)
                {
                    File = file;
                    Row = row;
                    Header = header;
                }
            }

            private sealed class StoryProfileScanResult
            {
                public readonly LimitedFailures ContentFailures = new LimitedFailures();
                public readonly LimitedFailures BoundaryFailures = new LimitedFailures();
                public readonly LimitedFailures StructureFailures = new LimitedFailures();
                public long SheetsChecked;
                public long BasePagesChecked;
                public long BaseCellsChecked;
                public long KoreanCellsChecked;
                public long DistinctKoreanCellsChecked;
                public long SourceFallbackCellsChecked;
                public long BoundaryBaseCells;
                public long BoundaryStoryCells;
                public long StructuredCellsChecked;
            }

            private sealed class LimitedFailures
            {
                private const int SampleLimit = 100;
                public readonly List<string> Samples = new List<string>();
                public long Count { get; private set; }

                public void Add(string value)
                {
                    Count++;
                    if (Samples.Count < SampleLimit)
                    {
                        Samples.Add(value);
                    }
                }
            }
        }
    }
}
