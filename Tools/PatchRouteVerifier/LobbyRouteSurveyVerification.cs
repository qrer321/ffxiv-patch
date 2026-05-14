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

            private void VerifyLobbyRouteSurvey()
            {
                Console.WriteLine("[REPORT] lobby route survey");
                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);

                LobbyRouteSurveyStats routeStats = WriteLobbyUldRouteReports(reportDir);
                LobbySheetSurveyStats sheetStats = WriteLobbySheetTextReports(reportDir);

                Pass(
                    "lobby route survey wrote {0} ULD node rows, {1} unique routed fonts, {2} Hangul text rows, {3} unique Hangul codepoints, {4} sheet read errors",
                    routeStats.TextNodeRows,
                    routeStats.UniqueFonts,
                    sheetStats.TextRows,
                    sheetStats.UniqueHangulCodepoints,
                    sheetStats.ReadErrors);
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
        }
    }
}
