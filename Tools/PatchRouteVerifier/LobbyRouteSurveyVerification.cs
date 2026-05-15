using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] LobbySurveySheets = new string[]
            {
                "Lobby",
                "Error",
                "Addon",
                "ClassJob",
                "Race",
                "Tribe",
                "GuardianDeity"
            };

            private static readonly string[] LobbyCoverageActionKeywords = new string[]
            {
                "HUD",
                "게임 시작",
                "데이터 센터",
                "동영상",
                "라이선스",
                "종료",
                "뒤로",
                "확인",
                "취소",
                "시스템",
                "설정",
                "설치",
                "정보",
                "고해상도",
                "해상도",
                "화면",
                "변경",
                "캐릭터",
                "불러오기",
                "이름",
                "집사",
                "백업",
                "쾌적",
                "서버",
                "월드",
                "방문",
                "이동",
                "종족",
                "부족",
                "직업",
                "클래스",
                "수호성",
                "생일",
                "그림자",
                "끌기",
                "회전",
                "현재",
                "누적",
                "결제",
                "보상",
                "삭제",
                "매크로",
                "비례",
                "입력",
                "장치",
                "대상",
                "위치",
                "단축바",
                "십자",
                "복사",
                "성공적으로",
                "로그인",
                "처리"
            };

            private static readonly string[] ReportedLobbyCoveragePhrases = new string[]
            {
                "시스템 설정",
                "설치 정보",
                "뒤로",
                "종족",
                "그림자",
                "직업",
                "캐릭터 정보",
                "불러오기",
                "이름 변경",
                "집사 이름 변경",
                "캐릭터 설정 데이터",
                "백업",
                "쾌적한 서버로 이동",
                "다른 데이터 센터 방문",
                "끌기",
                "회전",
                "취소",
                "별",
                "현재",
                "누적",
                "결제 보상",
                "누적된",
                "캐릭터 삭제",
                "매크로",
                "화면 해상도",
                "비례",
                "바꿀 수 있는",
                "입력 장치",
                "백업 대상",
                "HUD 위치",
                "단축바",
                "십자",
                "설정 데이터",
                "복사",
                "성공적으로 로그인",
                "로그인 처리"
            };

            private void VerifyLobbyRouteSurvey()
            {
                Console.WriteLine("[REPORT] lobby route survey");
                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);

                LobbyRouteSurveyStats routeStats = WriteLobbyUldRouteReports(reportDir);
                LobbySheetSurveyStats sheetStats = WriteLobbySheetTextReports(reportDir);
                LobbyCoverageGapStats gapStats = WriteLobbyCoverageGapReports(reportDir);

                Pass(
                    "lobby route survey wrote {0} ULD node rows, {1} unique routed fonts, {2} Hangul text rows, {3} unique Hangul codepoints, {4} uncovered candidate rows, {5} actionable rows, {6} sheet read errors",
                    routeStats.TextNodeRows,
                    routeStats.UniqueFonts,
                    sheetStats.TextRows,
                    sheetStats.UniqueHangulCodepoints,
                    gapStats.UncoveredRows,
                    gapStats.ActionableRows,
                    sheetStats.ReadErrors + gapStats.ReadErrors);
            }

            private string ResolveLobbyReportDir()
            {
                if (!string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return _glyphDumpDir;
                }

                return Path.Combine(_output, DefaultGlyphDumpFolderName);
            }

            private LobbyRouteSurveyStats WriteLobbyUldRouteReports(string reportDir)
            {
                string routeReportPath = Path.Combine(reportDir, "lobby-uld-font-routes.tsv");
                string summaryReportPath = Path.Combine(reportDir, "lobby-uld-font-summary.tsv");
                LobbyRouteSurveyStats stats = new LobbyRouteSurveyStats();
                Dictionary<string, LobbyFontRouteSummary> summaries =
                    new Dictionary<string, LobbyFontRouteSummary>(StringComparer.OrdinalIgnoreCase);

                using (StreamWriter writer = CreateUtf8Writer(routeReportPath))
                {
                    writer.WriteLine("screen\tuld\tlobby_fonts\tpresent\tclean_text_nodes\tpatched_text_nodes\tnode_offset\tclean_font_id\tclean_font_size\tclean_font_path\tpatched_font_id\tpatched_font_size\tpatched_font_path\trender_state_preserved");
                    WriteUldCandidateRouteRows(writer, summaries, "data-center-select", new UldRouteCandidate[]
                    {
                        new UldRouteCandidate(DataCenterTitleUldPath, true),
                        new UldRouteCandidate(DataCenterWorldmapUldPath, true)
                    }, stats);
                    WriteUldCandidateRouteRows(writer, summaries, "start-system-settings", StartScreenSystemSettingsUldCandidates, stats);
                    WriteUldCandidateRouteRows(writer, summaries, "start-main-menu", StartScreenMainMenuUldCandidates, stats);
                    WriteUldCandidateRouteRows(writer, summaries, "character-select", CharacterSelectLobbyUldCandidates, stats);
                }

                WriteUldFontSummary(summaryReportPath, summaries);
                stats.UniqueFonts = summaries.Count;
                return stats;
            }

            private void WriteUldCandidateRouteRows(
                StreamWriter writer,
                Dictionary<string, LobbyFontRouteSummary> summaries,
                string screen,
                UldRouteCandidate[] candidates,
                LobbyRouteSurveyStats stats)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    stats.UldCandidates++;
                    UldRouteCandidate candidate = candidates[i];

                    byte[] cleanPacked;
                    byte[] patchedPacked;
                    bool cleanExists = _cleanUi.TryReadPackedFile(candidate.Path, out cleanPacked);
                    bool patchedExists = _patchedUi.TryReadPackedFile(candidate.Path, out patchedPacked);
                    if (!cleanExists || !patchedExists)
                    {
                        WriteTsvRow(
                            writer,
                            screen,
                            candidate.Path,
                            candidate.UsesLobbyFonts ? "yes" : "no",
                            "no",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty);
                        continue;
                    }

                    stats.PresentUlds++;
                    byte[] cleanUld = SqPackArchive.UnpackStandardFile(cleanPacked);
                    byte[] patchedUld = SqPackArchive.UnpackStandardFile(patchedPacked);
                    List<UldTextNodeFont> cleanFonts = GetUldTextNodeFonts(cleanUld);
                    List<UldTextNodeFont> patchedFonts = GetUldTextNodeFonts(patchedUld);
                    Dictionary<int, UldTextNodeFont> patchedByOffset = GetUldTextNodeFontsByOffset(patchedUld);

                    for (int fontIndex = 0; fontIndex < cleanFonts.Count; fontIndex++)
                    {
                        UldTextNodeFont cleanNode = cleanFonts[fontIndex];
                        UldTextNodeFont patchedNode;
                        bool hasPatchedNode = patchedByOffset.TryGetValue(cleanNode.NodeOffset, out patchedNode);
                        string cleanFontPath = ResolveUldFontPath(cleanNode.FontId, cleanNode.FontSize, candidate.UsesLobbyFonts) ?? "unmapped";
                        string patchedFontPath = hasPatchedNode
                            ? ResolveUldFontPath(patchedNode.FontId, patchedNode.FontSize, candidate.UsesLobbyFonts) ?? "unmapped"
                            : "missing-node";
                        bool renderStatePreserved = hasPatchedNode &&
                            cleanNode.NodeSize == patchedNode.NodeSize &&
                            BytesEqual(cleanNode.HeaderBytes, patchedNode.HeaderBytes) &&
                            BytesEqual(cleanNode.TextExtraBytes, patchedNode.TextExtraBytes);

                        stats.TextNodeRows++;
                        WriteTsvRow(
                            writer,
                            screen,
                            candidate.Path,
                            candidate.UsesLobbyFonts ? "yes" : "no",
                            "yes",
                            cleanFonts.Count.ToString(),
                            patchedFonts.Count.ToString(),
                            "0x" + cleanNode.NodeOffset.ToString("X"),
                            cleanNode.FontId.ToString(),
                            cleanNode.FontSize.ToString(),
                            cleanFontPath,
                            hasPatchedNode ? patchedNode.FontId.ToString() : string.Empty,
                            hasPatchedNode ? patchedNode.FontSize.ToString() : string.Empty,
                            patchedFontPath,
                            renderStatePreserved ? "yes" : "no");

                        if (hasPatchedNode && !string.Equals(patchedFontPath, "unmapped", StringComparison.OrdinalIgnoreCase))
                        {
                            AddLobbyFontRouteSummary(summaries, patchedFontPath, screen, candidate.Path);
                        }
                    }
                }
            }

            private static void AddLobbyFontRouteSummary(
                Dictionary<string, LobbyFontRouteSummary> summaries,
                string fontPath,
                string screen,
                string uldPath)
            {
                LobbyFontRouteSummary summary;
                if (!summaries.TryGetValue(fontPath, out summary))
                {
                    summary = new LobbyFontRouteSummary(fontPath);
                    summaries.Add(fontPath, summary);
                }

                summary.TextNodes++;
                summary.Screens.Add(screen);
                summary.Ulds.Add(uldPath);
            }

            private static void WriteUldFontSummary(string summaryReportPath, Dictionary<string, LobbyFontRouteSummary> summaries)
            {
                List<string> keys = new List<string>(summaries.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(summaryReportPath))
                {
                    writer.WriteLine("font_path\ttext_nodes\tscreens\tulds");
                    for (int i = 0; i < keys.Count; i++)
                    {
                        LobbyFontRouteSummary summary = summaries[keys[i]];
                        WriteTsvRow(
                            writer,
                            summary.FontPath,
                            summary.TextNodes.ToString(),
                            JoinSorted(summary.Screens),
                            JoinSorted(summary.Ulds));
                    }
                }
            }

            private LobbySheetSurveyStats WriteLobbySheetTextReports(string reportDir)
            {
                string textReportPath = Path.Combine(reportDir, "lobby-sheet-hangul-text.tsv");
                string summaryReportPath = Path.Combine(reportDir, "lobby-sheet-hangul-summary.tsv");
                string errorReportPath = Path.Combine(reportDir, "lobby-sheet-read-errors.tsv");
                LobbySheetSurveyStats stats = new LobbySheetSurveyStats();
                Dictionary<string, LobbySheetSummary> summaries =
                    new Dictionary<string, LobbySheetSummary>(StringComparer.OrdinalIgnoreCase);
                HashSet<uint> allHangulCodepoints = new HashSet<uint>();

                using (StreamWriter writer = CreateUtf8Writer(textReportPath))
                using (StreamWriter errorWriter = CreateUtf8Writer(errorReportPath))
                {
                    writer.WriteLine("sheet\trow_id\tcolumn_index\tcolumn_offset\thangul_chars\tunique_hangul\ttext");
                    errorWriter.WriteLine("sheet\tpath\terror_type\tmessage");
                    for (int i = 0; i < LobbySurveySheets.Length; i++)
                    {
                        SurveyLobbySheet(writer, errorWriter, summaries, allHangulCodepoints, LobbySurveySheets[i], stats);
                    }
                }

                WriteLobbySheetSummary(summaryReportPath, summaries);
                stats.UniqueHangulCodepoints = allHangulCodepoints.Count;
                return stats;
            }

            private void SurveyLobbySheet(
                StreamWriter writer,
                StreamWriter errorWriter,
                Dictionary<string, LobbySheetSummary> summaries,
                HashSet<uint> allHangulCodepoints,
                string sheet,
                LobbySheetSurveyStats stats)
            {
                ExcelHeader header;
                try
                {
                    header = ExcelHeader.Parse(_patchedText.ReadFile("exd/" + sheet + ".exh"));
                }
                catch (FileNotFoundException)
                {
                    Warn("lobby survey sheet not found: {0}", sheet);
                    WriteSheetReadError(errorWriter, sheet, "exd/" + sheet + ".exh", "FileNotFoundException", "sheet not found");
                    stats.ReadErrors++;
                    return;
                }
                catch (Exception ex)
                {
                    Warn("lobby survey could not read {0}.exh: {1}", sheet, ex.Message);
                    WriteSheetReadError(errorWriter, sheet, "exd/" + sheet + ".exh", ex.GetType().Name, ex.Message);
                    stats.ReadErrors++;
                    return;
                }

                List<int> stringColumns = header.GetStringColumnIndexes();
                bool hasLanguageSuffix = header.HasLanguage(LanguageToId(_language));
                LobbySheetSummary summary = new LobbySheetSummary(sheet);
                summaries[sheet] = summary;
                stats.Sheets++;

                for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = header.Pages[pageIndex];
                    string exdPath = BuildExdPath(sheet, page.StartId, _language, hasLanguageSuffix);
                    ExcelDataFile file;
                    try
                    {
                        file = ExcelDataFile.Parse(_patchedText.ReadFile(exdPath));
                    }
                    catch (Exception ex)
                    {
                        Warn("lobby survey could not read {0}: {1}", exdPath, ex.Message);
                        WriteSheetReadError(errorWriter, sheet, exdPath, ex.GetType().Name, ex.Message);
                        stats.ReadErrors++;
                        continue;
                    }

                    for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                    {
                        ExcelDataRow row = file.Rows[rowIndex];
                        bool rowCounted = false;
                        for (int columnListIndex = 0; columnListIndex < stringColumns.Count; columnListIndex++)
                        {
                            int columnIndex = stringColumns[columnListIndex];
                            byte[] bytes = file.GetStringBytes(row, header, columnIndex);
                            string value = bytes == null ? string.Empty : Encoding.UTF8.GetString(bytes);
                            if (!ContainsHangul(value))
                            {
                                continue;
                            }

                            HashSet<uint> rowCodepoints = CollectHangulCodepoints(value);
                            AddRange(allHangulCodepoints, rowCodepoints);
                            AddRange(summary.HangulCodepoints, rowCodepoints);

                            stats.TextColumns++;
                            summary.TextColumns++;
                            summary.HangulChars += CountHangulChars(value);
                            if (!rowCounted)
                            {
                                rowCounted = true;
                                stats.TextRows++;
                                summary.TextRows++;
                            }

                            ExcelColumnDefinition column = header.Columns[columnIndex];
                            WriteTsvRow(
                                writer,
                                sheet,
                                row.RowId.ToString(),
                                columnIndex.ToString(),
                                column.Offset.ToString(),
                                CountHangulChars(value).ToString(),
                                FormatCodepoints(rowCodepoints),
                                value);
                        }
                    }
                }
            }

            private static void WriteSheetReadError(
                StreamWriter writer,
                string sheet,
                string path,
                string errorType,
                string message)
            {
                WriteTsvRow(writer, sheet, path, errorType, message);
            }

            private static void WriteLobbySheetSummary(string summaryReportPath, Dictionary<string, LobbySheetSummary> summaries)
            {
                List<string> keys = new List<string>(summaries.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(summaryReportPath))
                {
                    writer.WriteLine("sheet\ttext_rows\ttext_columns\thangul_chars\tunique_hangul\tcodepoints");
                    for (int i = 0; i < keys.Count; i++)
                    {
                        LobbySheetSummary summary = summaries[keys[i]];
                        WriteTsvRow(
                            writer,
                            summary.Sheet,
                            summary.TextRows.ToString(),
                            summary.TextColumns.ToString(),
                            summary.HangulChars.ToString(),
                            summary.HangulCodepoints.Count.ToString(),
                            FormatCodepoints(summary.HangulCodepoints));
                    }
                }
            }

            private LobbyCoverageGapStats WriteLobbyCoverageGapReports(string reportDir)
            {
                string gapReportPath = Path.Combine(reportDir, "lobby-uncovered-hangul-candidates.tsv");
                string actionableReportPath = Path.Combine(reportDir, "lobby-uncovered-actionable-candidates.tsv");
                string summaryReportPath = Path.Combine(reportDir, "lobby-uncovered-hangul-summary.tsv");
                string errorReportPath = Path.Combine(reportDir, "lobby-uncovered-hangul-read-errors.tsv");
                LobbyCoverageGapStats stats = new LobbyCoverageGapStats();
                Dictionary<string, LobbyCoverageGapSummary> summaries =
                    new Dictionary<string, LobbyCoverageGapSummary>(StringComparer.OrdinalIgnoreCase);
                HashSet<uint> uncoveredCodepoints = new HashSet<uint>();
                HashSet<uint> actionableCodepoints = new HashSet<uint>();

                using (StreamWriter writer = CreateUtf8Writer(gapReportPath))
                using (StreamWriter actionableWriter = CreateUtf8Writer(actionableReportPath))
                using (StreamWriter errorWriter = CreateUtf8Writer(errorReportPath))
                {
                    writer.WriteLine("sheet\trow_id\tcolumn_index\tcolumn_offset\tnearest_coverage\tpriority\thangul_chars\tunique_hangul\ttext");
                    actionableWriter.WriteLine("sheet\trow_id\tcolumn_index\tcolumn_offset\tnearest_coverage\tpriority\treasons\thangul_chars\tunique_hangul\ttext");
                    errorWriter.WriteLine("sheet\tpath\terror_type\tmessage");
                    for (int i = 0; i < LobbySurveySheets.Length; i++)
                    {
                        SurveyLobbyCoverageGaps(
                            writer,
                            actionableWriter,
                            errorWriter,
                            summaries,
                            uncoveredCodepoints,
                            actionableCodepoints,
                            LobbySurveySheets[i],
                            stats);
                    }
                }

                WriteLobbyCoverageGapSummary(summaryReportPath, summaries);
                stats.UniqueUncoveredCodepoints = uncoveredCodepoints.Count;
                stats.UniqueActionableCodepoints = actionableCodepoints.Count;
                return stats;
            }

            private void SurveyLobbyCoverageGaps(
                StreamWriter writer,
                StreamWriter actionableWriter,
                StreamWriter errorWriter,
                Dictionary<string, LobbyCoverageGapSummary> summaries,
                HashSet<uint> uncoveredCodepoints,
                HashSet<uint> actionableCodepoints,
                string sheet,
                LobbyCoverageGapStats stats)
            {
                ExcelHeader header;
                try
                {
                    header = ExcelHeader.Parse(_patchedText.ReadFile("exd/" + sheet + ".exh"));
                }
                catch (FileNotFoundException)
                {
                    WriteSheetReadError(errorWriter, sheet, "exd/" + sheet + ".exh", "FileNotFoundException", "sheet not found");
                    stats.ReadErrors++;
                    return;
                }
                catch (Exception ex)
                {
                    WriteSheetReadError(errorWriter, sheet, "exd/" + sheet + ".exh", ex.GetType().Name, ex.Message);
                    stats.ReadErrors++;
                    return;
                }

                List<int> stringColumns = header.GetStringColumnIndexes();
                bool hasLanguageSuffix = header.HasLanguage(LanguageToId(_language));
                LobbyCoverageGapSummary summary = GetOrAddCoverageGapSummary(summaries, sheet);
                stats.Sheets++;

                for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = header.Pages[pageIndex];
                    string exdPath = BuildExdPath(sheet, page.StartId, _language, hasLanguageSuffix);
                    ExcelDataFile file;
                    try
                    {
                        file = ExcelDataFile.Parse(_patchedText.ReadFile(exdPath));
                    }
                    catch (Exception ex)
                    {
                        WriteSheetReadError(errorWriter, sheet, exdPath, ex.GetType().Name, ex.Message);
                        stats.ReadErrors++;
                        continue;
                    }

                    for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                    {
                        ExcelDataRow row = file.Rows[rowIndex];
                        bool rowCounted = false;
                        bool actionableRowCounted = false;
                        for (int columnListIndex = 0; columnListIndex < stringColumns.Count; columnListIndex++)
                        {
                            int columnIndex = stringColumns[columnListIndex];
                            ExcelColumnDefinition column = header.Columns[columnIndex];
                            byte[] bytes = file.GetStringBytes(row, header, columnIndex);
                            string value = bytes == null ? string.Empty : Encoding.UTF8.GetString(bytes);
                            if (!ContainsHangul(value))
                            {
                                continue;
                            }

                            if (IsLobbyCoverageRowCovered(sheet, row.RowId, column.Offset))
                            {
                                stats.CoveredColumns++;
                                summary.CoveredColumns++;
                                continue;
                            }

                            HashSet<uint> rowCodepoints = CollectHangulCodepoints(value);
                            AddRange(uncoveredCodepoints, rowCodepoints);
                            AddRange(summary.Codepoints, rowCodepoints);

                            if (!rowCounted)
                            {
                                rowCounted = true;
                                stats.UncoveredRows++;
                                summary.UncoveredRows++;
                            }

                            stats.UncoveredColumns++;
                            summary.UncoveredColumns++;
                            summary.HangulChars += CountHangulChars(value);
                            string nearestCoverage = DescribeNearestLobbyCoverage(sheet, row.RowId);
                            string priority = GetLobbyCoverageGapPriority(sheet, row.RowId);
                            string actionReasons = GetLobbyCoverageGapActionReasons(sheet, row.RowId, value, priority);

                            WriteTsvRow(
                                writer,
                                sheet,
                                row.RowId.ToString(),
                                columnIndex.ToString(),
                                column.Offset.ToString(),
                                nearestCoverage,
                                priority,
                                CountHangulChars(value).ToString(),
                                FormatCodepoints(rowCodepoints),
                                value);

                            if (actionReasons.Length > 0)
                            {
                                AddRange(actionableCodepoints, rowCodepoints);
                                AddRange(summary.ActionableCodepoints, rowCodepoints);
                                if (!actionableRowCounted)
                                {
                                    actionableRowCounted = true;
                                    stats.ActionableRows++;
                                    summary.ActionableRows++;
                                }

                                stats.ActionableColumns++;
                                summary.ActionableColumns++;
                                WriteTsvRow(
                                    actionableWriter,
                                    sheet,
                                    row.RowId.ToString(),
                                    columnIndex.ToString(),
                                    column.Offset.ToString(),
                                    nearestCoverage,
                                    priority,
                                    actionReasons,
                                    CountHangulChars(value).ToString(),
                                    FormatCodepoints(rowCodepoints),
                                    value);
                            }
                        }
                    }
                }
            }

            private static void WriteLobbyCoverageGapSummary(
                string summaryReportPath,
                Dictionary<string, LobbyCoverageGapSummary> summaries)
            {
                List<string> keys = new List<string>(summaries.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(summaryReportPath))
                {
                    writer.WriteLine("sheet\tuncovered_rows\tuncovered_columns\tactionable_rows\tactionable_columns\tcovered_columns\thangul_chars\tunique_hangul\tactionable_unique_hangul\tcodepoints\tactionable_codepoints");
                    for (int i = 0; i < keys.Count; i++)
                    {
                        LobbyCoverageGapSummary summary = summaries[keys[i]];
                        WriteTsvRow(
                            writer,
                            summary.Sheet,
                            summary.UncoveredRows.ToString(),
                            summary.UncoveredColumns.ToString(),
                            summary.ActionableRows.ToString(),
                            summary.ActionableColumns.ToString(),
                            summary.CoveredColumns.ToString(),
                            summary.HangulChars.ToString(),
                            summary.Codepoints.Count.ToString(),
                            summary.ActionableCodepoints.Count.ToString(),
                            FormatCodepoints(summary.Codepoints),
                            FormatCodepoints(summary.ActionableCodepoints));
                    }
                }
            }

            private static bool IsLobbyCoverageRowCovered(string sheet, uint rowId, int columnOffset)
            {
                for (int i = 0; i < LobbyHangulCoverage.Rows.Length; i++)
                {
                    LobbyHangulCoverageRowSpec range = LobbyHangulCoverage.Rows[i];
                    if (!string.Equals(range.Sheet, sheet, StringComparison.OrdinalIgnoreCase) ||
                        !range.Contains(rowId))
                    {
                        continue;
                    }

                    if (range.ColumnOffset.HasValue && range.ColumnOffset.Value != columnOffset)
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }

            private static string DescribeNearestLobbyCoverage(string sheet, uint rowId)
            {
                uint bestDistance = uint.MaxValue;
                LobbyHangulCoverageRowSpec best = new LobbyHangulCoverageRowSpec();
                bool found = false;
                for (int i = 0; i < LobbyHangulCoverage.Rows.Length; i++)
                {
                    LobbyHangulCoverageRowSpec range = LobbyHangulCoverage.Rows[i];
                    if (!string.Equals(range.Sheet, sheet, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    uint distance = rowId < range.StartId
                        ? range.StartId - rowId
                        : rowId > range.EndId
                            ? rowId - range.EndId
                            : 0;
                    if (!found || distance < bestDistance)
                    {
                        found = true;
                        bestDistance = distance;
                        best = range;
                    }
                }

                if (!found)
                {
                    return "none";
                }

                string column = best.ColumnOffset.HasValue
                    ? ",col=" + best.ColumnOffset.Value.ToString()
                    : string.Empty;
                return best.Sheet + "#" + best.StartId.ToString() + "-" + best.EndId.ToString() + column + ",distance=" + bestDistance.ToString();
            }

            private static string GetLobbyCoverageGapPriority(string sheet, uint rowId)
            {
                if (string.Equals(sheet, "Lobby", StringComparison.OrdinalIgnoreCase))
                {
                    return "high";
                }

                if (string.Equals(sheet, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    return "high";
                }

                if (string.Equals(sheet, "Addon", StringComparison.OrdinalIgnoreCase))
                {
                    uint nearest = GetNearestLobbyCoverageDistance(sheet, rowId);
                    return nearest <= 50 ? "medium" : "low";
                }

                return "low";
            }

            private static string GetLobbyCoverageGapActionReasons(
                string sheet,
                uint rowId,
                string text,
                string priority)
            {
                List<string> reasons = new List<string>();
                if (string.Equals(sheet, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("error-sheet");
                }

                if (string.Equals(sheet, "Lobby", StringComparison.OrdinalIgnoreCase))
                {
                    uint distance = GetNearestLobbyCoverageDistance(sheet, rowId);
                    if (distance <= 2)
                    {
                        reasons.Add("near-lobby-coverage");
                    }
                }

                if (string.Equals(sheet, "Addon", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(priority, "medium", StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("near-addon-coverage");
                }

                bool isAddon = string.Equals(sheet, "Addon", StringComparison.OrdinalIgnoreCase);
                bool isLongText = IsLongLobbyCoverageGapText(text);
                for (int i = 0; i < ReportedLobbyCoveragePhrases.Length; i++)
                {
                    string phrase = ReportedLobbyCoveragePhrases[i];
                    if ((isAddon || isLongText) && phrase.Length < 4)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(phrase) &&
                        text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        reasons.Add("reported:" + phrase);
                    }
                }

                bool allowBroadKeywords = !isAddon && !isLongText;
                if (!allowBroadKeywords)
                {
                    return string.Join(",", reasons.ToArray());
                }

                for (int i = 0; i < LobbyCoverageActionKeywords.Length; i++)
                {
                    string keyword = LobbyCoverageActionKeywords[i];
                    if (!string.IsNullOrEmpty(keyword) &&
                        text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        reasons.Add("keyword:" + keyword);
                    }
                }

                return string.Join(",", reasons.ToArray());
            }

            private static bool IsLongLobbyCoverageGapText(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                if (text.IndexOf('\n') >= 0)
                {
                    return true;
                }

                return CountHangulChars(text) > 80;
            }

            private static uint GetNearestLobbyCoverageDistance(string sheet, uint rowId)
            {
                uint bestDistance = uint.MaxValue;
                for (int i = 0; i < LobbyHangulCoverage.Rows.Length; i++)
                {
                    LobbyHangulCoverageRowSpec range = LobbyHangulCoverage.Rows[i];
                    if (!string.Equals(range.Sheet, sheet, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    uint distance = rowId < range.StartId
                        ? range.StartId - rowId
                        : rowId > range.EndId
                            ? rowId - range.EndId
                            : 0;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                    }
                }

                return bestDistance;
            }

            private static LobbyCoverageGapSummary GetOrAddCoverageGapSummary(
                Dictionary<string, LobbyCoverageGapSummary> summaries,
                string sheet)
            {
                LobbyCoverageGapSummary summary;
                if (!summaries.TryGetValue(sheet, out summary))
                {
                    summary = new LobbyCoverageGapSummary(sheet);
                    summaries.Add(sheet, summary);
                }

                return summary;
            }

            private static HashSet<uint> CollectHangulCodepoints(string value)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                if (string.IsNullOrEmpty(value))
                {
                    return codepoints;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    uint codepoint = ReadCodepoint(value, ref i);
                    if (IsHangulCodepoint(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }

                return codepoints;
            }

            private static int CountHangulChars(string value)
            {
                int count = 0;
                if (string.IsNullOrEmpty(value))
                {
                    return count;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    uint codepoint = ReadCodepoint(value, ref i);
                    if (IsHangulCodepoint(codepoint))
                    {
                        count++;
                    }
                }

                return count;
            }

            private static void AddRange(HashSet<uint> target, HashSet<uint> values)
            {
                foreach (uint value in values)
                {
                    target.Add(value);
                }
            }

            private static string FormatCodepoints(HashSet<uint> codepoints)
            {
                List<uint> values = new List<uint>(codepoints);
                values.Sort();
                string[] formatted = new string[values.Count];
                for (int i = 0; i < values.Count; i++)
                {
                    formatted[i] = "U+" + values[i].ToString("X4");
                }

                return string.Join(" ", formatted);
            }

            private static StreamWriter CreateUtf8Writer(string path)
            {
                return new StreamWriter(path, false, new UTF8Encoding(false));
            }

            private static void WriteTsvRow(StreamWriter writer, params string[] values)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                    {
                        writer.Write('\t');
                    }

                    writer.Write(EscapeTsv(values[i]));
                }

                writer.WriteLine();
            }

            private static string JoinSorted(HashSet<string> values)
            {
                List<string> sorted = new List<string>(values);
                sorted.Sort(StringComparer.OrdinalIgnoreCase);
                return string.Join(",", sorted.ToArray());
            }

            private sealed class LobbyRouteSurveyStats
            {
                public int UldCandidates;
                public int PresentUlds;
                public int TextNodeRows;
                public int UniqueFonts;
            }

            private sealed class LobbyFontRouteSummary
            {
                public readonly string FontPath;
                public int TextNodes;
                public readonly HashSet<string> Screens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                public readonly HashSet<string> Ulds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                public LobbyFontRouteSummary(string fontPath)
                {
                    FontPath = fontPath;
                }
            }

            private sealed class LobbySheetSurveyStats
            {
                public int Sheets;
                public int TextRows;
                public int TextColumns;
                public int UniqueHangulCodepoints;
                public int ReadErrors;
            }

            private sealed class LobbySheetSummary
            {
                public readonly string Sheet;
                public int TextRows;
                public int TextColumns;
                public int HangulChars;
                public readonly HashSet<uint> HangulCodepoints = new HashSet<uint>();

                public LobbySheetSummary(string sheet)
                {
                    Sheet = sheet;
                }
            }

            private sealed class LobbyCoverageGapStats
            {
                public int Sheets;
                public int UncoveredRows;
                public int UncoveredColumns;
                public int ActionableRows;
                public int ActionableColumns;
                public int CoveredColumns;
                public int UniqueUncoveredCodepoints;
                public int UniqueActionableCodepoints;
                public int ReadErrors;
            }

            private sealed class LobbyCoverageGapSummary
            {
                public readonly string Sheet;
                public int UncoveredRows;
                public int UncoveredColumns;
                public int ActionableRows;
                public int ActionableColumns;
                public int CoveredColumns;
                public int HangulChars;
                public readonly HashSet<uint> Codepoints = new HashSet<uint>();
                public readonly HashSet<uint> ActionableCodepoints = new HashSet<uint>();

                public LobbyCoverageGapSummary(string sheet)
                {
                    Sheet = sheet;
                }
            }
        }
    }
}
