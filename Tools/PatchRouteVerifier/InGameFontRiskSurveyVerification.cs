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
            private static readonly string[] InGameFontRiskSheets = new string[]
            {
                "Addon",
                "Action",
                "ActionTransient",
                "ActionProcStatus",
                "Status",
                "Trait",
                "TraitTransient",
                "Item",
                "EventItem",
                "LogMessage",
                "ContentFinderCondition",
                "ContentRoulette",
                "ContentType",
                "InstanceContent",
                "TerritoryType",
                "ClassJob",
                "ClassJobResident",
                "MkdSupportJob",
                "PvPRankTransient",
                "PvPSelectTrait",
                "PvPSelectTraitTransient",
                "XPVPGroupActivity",
                "BNpcName",
                "ENpcResident",
                "ContentTalk",
                "ContentTalkParam"
            };

            private static readonly InGameUldCandidate[] InGameFontRiskUldCandidates = new InGameUldCandidate[]
            {
                new InGameUldCandidate("action-detail", "ui/uld/ActionDetail.uld"),
                new InGameUldCandidate("action-menu", "ui/uld/ActionMenu.uld"),
                new InGameUldCandidate("action-bar", "ui/uld/ActionBar.uld"),
                new InGameUldCandidate("party-list", "ui/uld/PartyList.uld"),
                new InGameUldCandidate("alliance-list", "ui/uld/AllianceList.uld"),
                new InGameUldCandidate("enemy-list", "ui/uld/EnemyList.uld"),
                new InGameUldCandidate("name-plate", "ui/uld/NamePlate.uld"),
                new InGameUldCandidate("chat-log", "ui/uld/ChatLog.uld"),
                new InGameUldCandidate("item-detail", "ui/uld/ItemDetail.uld"),
                new InGameUldCandidate("character", "ui/uld/Character.uld"),
                new InGameUldCandidate("status", "ui/uld/Status.uld"),
                new InGameUldCandidate("content-finder", "ui/uld/ContentsFinder.uld"),
                new InGameUldCandidate("content-info", "ui/uld/ContentsInfo.uld"),
                new InGameUldCandidate("hud-layout", "ui/uld/HudLayout.uld"),
                new InGameUldCandidate("teleport", "ui/uld/Teleport.uld"),
                new InGameUldCandidate("pvp-profile", "ui/uld/PvPProfile.uld", true),
                new InGameUldCandidate("pvp-character", "ui/uld/PvPCharacter.uld", true),
                new InGameUldCandidate("pvp-actions", "ui/uld/PvPAction.uld", true),
                new InGameUldCandidate("pvp-actions", "ui/uld/PvPActions.uld", true),
                new InGameUldCandidate("pvp-team", "ui/uld/PvPTeam.uld", true),
                new InGameUldCandidate("pvp-team", "ui/uld/PvPTeamBoard.uld", true),
                new InGameUldCandidate("pvp-schedule", "ui/uld/PvPSchedule.uld", true)
            };

            private void VerifyInGameFontRiskSurvey()
            {
                Console.WriteLine("[REPORT] in-game font risk survey");
                string reportDir = ResolveInGameFontRiskReportDir();
                Directory.CreateDirectory(reportDir);

                Dictionary<string, InGameFontRouteRiskSummary> fontSummaries =
                    CreateKnownInGameFontRiskSummaries();
                InGameUldSurveyStats uldStats = WriteInGameUldRouteRiskReport(reportDir, fontSummaries);
                InGameSheetRiskStats sheetStats = WriteInGameSheetRiskReports(reportDir);
                InGamePuaRiskStats puaStats = WriteInGamePuaGlyphRiskReport(reportDir);
                WriteInGameFontRiskSummary(reportDir, fontSummaries);

                Pass(
                    "in-game font risk survey wrote {0} ULD rows, {1} routed fonts, {2} sheet candidate rows, {3} unique Hangul codepoints, {4} PUA glyph rows, {5} clean-visible PUA regressions, {6} read errors",
                    uldStats.TextNodeRows,
                    fontSummaries.Count,
                    sheetStats.CandidateRows,
                    sheetStats.UniqueHangulCodepoints,
                    puaStats.Rows,
                    puaStats.Missing,
                    uldStats.ReadErrors + sheetStats.ReadErrors);
            }

            private string ResolveInGameFontRiskReportDir()
            {
                if (!string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return _glyphDumpDir;
                }

                return Path.Combine(_output, DefaultGlyphDumpFolderName);
            }

            private Dictionary<string, InGameFontRouteRiskSummary> CreateKnownInGameFontRiskSummaries()
            {
                Dictionary<string, InGameFontRouteRiskSummary> summaries =
                    new Dictionary<string, InGameFontRouteRiskSummary>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < DialoguePhraseFontPaths.Length; i++)
                {
                    GetOrAddInGameFontRouteRiskSummary(summaries, DialoguePhraseFontPaths[i]);
                }

                return summaries;
            }

            private InGameUldSurveyStats WriteInGameUldRouteRiskReport(
                string reportDir,
                Dictionary<string, InGameFontRouteRiskSummary> fontSummaries)
            {
                string routeReportPath = Path.Combine(reportDir, "ingame-uld-font-routes.tsv");
                InGameUldSurveyStats stats = new InGameUldSurveyStats();

                using (StreamWriter writer = CreateUtf8Writer(routeReportPath))
                {
                    writer.WriteLine("area\tuld\tpresent\tclean_text_nodes\tpatched_text_nodes\tnode_offset\tclean_font_id\tclean_font_size\tclean_font_path\tpatched_font_id\tpatched_font_size\tpatched_font_path\trisk\trender_state_preserved\terror");
                    for (int i = 0; i < InGameFontRiskUldCandidates.Length; i++)
                    {
                        WriteInGameUldRouteRows(writer, fontSummaries, InGameFontRiskUldCandidates[i], stats);
                    }
                }

                return stats;
            }

            private void WriteInGameUldRouteRows(
                StreamWriter writer,
                Dictionary<string, InGameFontRouteRiskSummary> fontSummaries,
                InGameUldCandidate candidate,
                InGameUldSurveyStats stats)
            {
                stats.UldCandidates++;
                byte[] cleanPacked;
                byte[] patchedPacked;
                string cleanError;
                string patchedError;
                bool cleanExists = TryReadOptionalPackedUi(_cleanUi, candidate.Path, out cleanPacked, out cleanError);
                bool patchedExists = TryReadOptionalPackedUi(_patchedUi, candidate.Path, out patchedPacked, out patchedError);
                if (!cleanExists || !patchedExists)
                {
                    string error = cleanExists ? patchedError : cleanError;
                    WriteTsvRow(
                        writer,
                        candidate.Area,
                        candidate.Path,
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
                        string.Empty,
                        string.Empty,
                        error);
                    if (!candidate.Optional && !string.IsNullOrEmpty(error))
                    {
                        stats.ReadErrors++;
                    }

                    return;
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
                    string cleanFontPath = ResolveUldFontPath(cleanNode.FontId, cleanNode.FontSize, false) ?? "unmapped";
                    string patchedFontPath = hasPatchedNode
                        ? ResolveUldFontPath(patchedNode.FontId, patchedNode.FontSize, false) ?? "unmapped"
                        : "missing-node";
                    bool renderStatePreserved = hasPatchedNode &&
                        cleanNode.NodeSize == patchedNode.NodeSize &&
                        BytesEqual(cleanNode.HeaderBytes, patchedNode.HeaderBytes) &&
                        BytesEqual(cleanNode.TextExtraBytes, patchedNode.TextExtraBytes);
                    string risk = DescribeInGameFontRouteRisk(patchedFontPath);

                    stats.TextNodeRows++;
                    WriteTsvRow(
                        writer,
                        candidate.Area,
                        candidate.Path,
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
                        risk,
                        renderStatePreserved ? "yes" : "no",
                        string.Empty);

                    if (hasPatchedNode && !string.Equals(patchedFontPath, "unmapped", StringComparison.OrdinalIgnoreCase))
                    {
                        InGameFontRouteRiskSummary summary = GetOrAddInGameFontRouteRiskSummary(fontSummaries, patchedFontPath);
                        summary.TextNodes++;
                        summary.Areas.Add(candidate.Area);
                        summary.Ulds.Add(candidate.Path);
                    }
                }
            }

            private bool TryReadOptionalPackedUi(CompositeArchive archive, string path, out byte[] packed, out string error)
            {
                packed = null;
                error = string.Empty;
                try
                {
                    if (!archive.TryReadPackedFile(path, out packed))
                    {
                        error = "not-found";
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
            }

            private InGameSheetRiskStats WriteInGameSheetRiskReports(string reportDir)
            {
                string textReportPath = Path.Combine(reportDir, "ingame-sheet-font-risk-candidates.tsv");
                string summaryReportPath = Path.Combine(reportDir, "ingame-sheet-font-risk-summary.tsv");
                string errorReportPath = Path.Combine(reportDir, "ingame-sheet-font-risk-errors.tsv");
                InGameSheetRiskStats stats = new InGameSheetRiskStats();
                Dictionary<string, InGameSheetRiskSummary> summaries =
                    new Dictionary<string, InGameSheetRiskSummary>(StringComparer.OrdinalIgnoreCase);
                HashSet<uint> allHangulCodepoints = new HashSet<uint>();

                using (StreamWriter writer = CreateUtf8Writer(textReportPath))
                using (StreamWriter errorWriter = CreateUtf8Writer(errorReportPath))
                {
                    writer.WriteLine("sheet\trow_id\tcolumn_index\tcolumn_offset\trisk\treasons\thangul_chars\tunique_hangul\ttext");
                    errorWriter.WriteLine("sheet\tpath\terror_type\tmessage");
                    for (int i = 0; i < InGameFontRiskSheets.Length; i++)
                    {
                        SurveyInGameFontRiskSheet(
                            writer,
                            errorWriter,
                            summaries,
                            allHangulCodepoints,
                            InGameFontRiskSheets[i],
                            stats);
                    }
                }

                WriteInGameSheetRiskSummary(summaryReportPath, summaries);
                stats.UniqueHangulCodepoints = allHangulCodepoints.Count;
                return stats;
            }

            private void SurveyInGameFontRiskSheet(
                StreamWriter writer,
                StreamWriter errorWriter,
                Dictionary<string, InGameSheetRiskSummary> summaries,
                HashSet<uint> allHangulCodepoints,
                string sheet,
                InGameSheetRiskStats stats)
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
                InGameSheetRiskSummary summary = new InGameSheetRiskSummary(sheet);
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
                            ExcelColumnDefinition column = header.Columns[columnIndex];
                            byte[] bytes = file.GetStringBytes(row, header, columnIndex);
                            string value = bytes == null ? string.Empty : Encoding.UTF8.GetString(bytes);
                            if (!ContainsHangul(value))
                            {
                                continue;
                            }

                            string risk = DescribeInGameSheetRisk(sheet);
                            if (string.IsNullOrEmpty(risk))
                            {
                                continue;
                            }

                            string reasons = DescribeInGameSheetRiskReasons(sheet, value);
                            HashSet<uint> rowCodepoints = CollectHangulCodepoints(value);
                            AddRange(allHangulCodepoints, rowCodepoints);
                            AddRange(summary.HangulCodepoints, rowCodepoints);

                            stats.CandidateColumns++;
                            summary.CandidateColumns++;
                            summary.HangulChars += CountHangulChars(value);
                            if (!rowCounted)
                            {
                                rowCounted = true;
                                stats.CandidateRows++;
                                summary.CandidateRows++;
                            }

                            WriteTsvRow(
                                writer,
                                sheet,
                                row.RowId.ToString(),
                                columnIndex.ToString(),
                                column.Offset.ToString(),
                                risk,
                                reasons,
                                CountHangulChars(value).ToString(),
                                FormatCodepoints(rowCodepoints),
                                value);
                        }
                    }
                }
            }

            private InGamePuaRiskStats WriteInGamePuaGlyphRiskReport(string reportDir)
            {
                string reportPath = Path.Combine(reportDir, "ingame-pua-glyph-risk.tsv");
                InGamePuaRiskStats stats = new InGamePuaRiskStats();
                HashSet<string> fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddValues(fonts, PartyListSelfMarkerSameFontChecks);
                for (int i = 0; i < PartyListSelfMarkerKoreanFontChecks.GetLength(0); i++)
                {
                    fonts.Add(PartyListSelfMarkerKoreanFontChecks[i, 1]);
                }

                using (StreamWriter writer = CreateUtf8Writer(reportPath))
                {
                    writer.WriteLine("font\tcodepoint\tpatched_visible\tclean_visible\tstatus");
                    foreach (string fontPath in fonts)
                    {
                        for (int i = 0; i < PartyListProtectedPuaGlyphs.Length; i++)
                        {
                            uint codepoint = PartyListProtectedPuaGlyphs[i];
                            int cleanVisible;
                            int patchedVisible;
                            string cleanError;
                            string patchedError;
                            bool cleanOk = TryRenderGlyphVisiblePixels(_cleanFont, fontPath, codepoint, out cleanVisible, out cleanError);
                            bool patchedOk = TryRenderGlyphVisiblePixels(_patchedFont, fontPath, codepoint, out patchedVisible, out patchedError);
                            bool cleanHasGlyph = cleanOk && cleanVisible > 0;
                            bool patchedHasGlyph = patchedOk && patchedVisible > 0;
                            string status;
                            if (patchedHasGlyph)
                            {
                                status = cleanHasGlyph ? "patched-visible-clean-visible" : "patched-visible-clean-missing";
                            }
                            else if (cleanHasGlyph)
                            {
                                status = "patched-missing-clean-visible:" + patchedError;
                            }
                            else
                            {
                                status = "both-missing";
                            }

                            if (!patchedHasGlyph && cleanHasGlyph)
                            {
                                stats.Missing++;
                            }

                            stats.Rows++;
                            WriteTsvRow(
                                writer,
                                fontPath,
                                "U+" + codepoint.ToString("X4"),
                                patchedOk ? patchedVisible.ToString() : patchedError,
                                cleanOk ? cleanVisible.ToString() : cleanError,
                                status);
                        }
                    }
                }

                return stats;
            }

            private bool TryRenderGlyphVisiblePixels(
                CompositeArchive archive,
                string fontPath,
                uint codepoint,
                out int visiblePixels,
                out string error)
            {
                visiblePixels = 0;
                error = string.Empty;
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fontPath, codepoint);
                    visiblePixels = canvas.VisiblePixels;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
            }

            private void WriteInGameFontRiskSummary(
                string reportDir,
                Dictionary<string, InGameFontRouteRiskSummary> summaries)
            {
                string summaryPath = Path.Combine(reportDir, "ingame-font-risk-summary.tsv");
                List<string> keys = new List<string>(summaries.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(summaryPath))
                {
                    writer.WriteLine("font\trisk\ttext_nodes\tareas\tulds");
                    for (int i = 0; i < keys.Count; i++)
                    {
                        InGameFontRouteRiskSummary summary = summaries[keys[i]];
                        WriteTsvRow(
                            writer,
                            summary.FontPath,
                            summary.Risk,
                            summary.TextNodes.ToString(),
                            JoinSorted(summary.Areas),
                            JoinSorted(summary.Ulds));
                    }
                }
            }

            private static void WriteInGameSheetRiskSummary(
                string summaryReportPath,
                Dictionary<string, InGameSheetRiskSummary> summaries)
            {
                List<string> keys = new List<string>(summaries.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(summaryReportPath))
                {
                    writer.WriteLine("sheet\trisk\tcandidate_rows\tcandidate_columns\thangul_chars\tunique_hangul\tcodepoints");
                    for (int i = 0; i < keys.Count; i++)
                    {
                        InGameSheetRiskSummary summary = summaries[keys[i]];
                        WriteTsvRow(
                            writer,
                            summary.Sheet,
                            DescribeInGameSheetRisk(summary.Sheet),
                            summary.CandidateRows.ToString(),
                            summary.CandidateColumns.ToString(),
                            summary.HangulChars.ToString(),
                            summary.HangulCodepoints.Count.ToString(),
                            FormatCodepoints(summary.HangulCodepoints));
                    }
                }
            }

            private static InGameFontRouteRiskSummary GetOrAddInGameFontRouteRiskSummary(
                Dictionary<string, InGameFontRouteRiskSummary> summaries,
                string fontPath)
            {
                InGameFontRouteRiskSummary summary;
                if (!summaries.TryGetValue(fontPath, out summary))
                {
                    summary = new InGameFontRouteRiskSummary(fontPath, DescribeInGameFontRouteRisk(fontPath));
                    summaries.Add(fontPath, summary);
                }

                return summary;
            }

            private static string DescribeInGameFontRouteRisk(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                if (string.Equals(normalized, "common/font/TrumpGothic_68.fdt", StringComparison.OrdinalIgnoreCase))
                {
                    return "combat-flytext,action-detail-high-scale-target";
                }

                if (string.Equals(normalized, ActionDetailHighScaleHangulGlyphs.SourceFontPath, StringComparison.OrdinalIgnoreCase))
                {
                    return "combat-flytext,action-detail-high-scale-source";
                }

                if (IsCombatFlyTextSourceFontPath(normalized))
                {
                    return "combat-flytext";
                }

                if (normalized.IndexOf("/KrnAXIS_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "global-krnaxis,party-pua";
                }

                if (normalized.IndexOf("/AXIS_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "global-axis,party-pua";
                }

                if (normalized.IndexOf("_lobby.", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "lobby-only";
                }

                if (string.Equals(normalized, "missing-node", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "unmapped", StringComparison.OrdinalIgnoreCase))
                {
                    return "unresolved";
                }

                return "global-font";
            }

            private static string DescribeInGameSheetRisk(string sheet)
            {
                if (string.Equals(sheet, "Addon", StringComparison.OrdinalIgnoreCase))
                {
                    return "hud-addon";
                }

                if (StartsWithIgnoreCase(sheet, "Action") ||
                    StartsWithIgnoreCase(sheet, "Trait") ||
                    string.Equals(sheet, "Status", StringComparison.OrdinalIgnoreCase))
                {
                    return "action-status-tooltip";
                }

                if (string.Equals(sheet, "Item", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sheet, "EventItem", StringComparison.OrdinalIgnoreCase))
                {
                    return "item-tooltip";
                }

                if (StartsWithIgnoreCase(sheet, "Content") ||
                    string.Equals(sheet, "InstanceContent", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sheet, "TerritoryType", StringComparison.OrdinalIgnoreCase))
                {
                    return "duty-content-ui";
                }

                if (StartsWithIgnoreCase(sheet, "ClassJob") ||
                    string.Equals(sheet, "MkdSupportJob", StringComparison.OrdinalIgnoreCase))
                {
                    return "job-character-ui";
                }

                if (StartsWithIgnoreCase(sheet, "PvP") ||
                    StartsWithIgnoreCase(sheet, "XPvP"))
                {
                    return "pvp-profile-ui";
                }

                if (string.Equals(sheet, "LogMessage", StringComparison.OrdinalIgnoreCase))
                {
                    return "log-chat-ui";
                }

                if (string.Equals(sheet, "BNpcName", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sheet, "ENpcResident", StringComparison.OrdinalIgnoreCase))
                {
                    return "nameplate-talk-ui";
                }

                return "ingame-text";
            }

            private static string DescribeInGameSheetRiskReasons(string sheet, string text)
            {
                List<string> reasons = new List<string>();
                reasons.Add("sheet:" + DescribeInGameSheetRisk(sheet));
                if (LooksLikeMissingGlyphFallback(text))
                {
                    reasons.Add("fallback-looking-text");
                }

                if (text.IndexOf("!", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("%", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf(".", StringComparison.Ordinal) >= 0)
                {
                    reasons.Add("mixed-symbols");
                }

                if (ContainsAsciiDigit(text))
                {
                    reasons.Add("mixed-digits");
                }

                if (CountHangulChars(text) <= 4)
                {
                    reasons.Add("short-hud-label");
                }

                return string.Join(",", reasons.ToArray());
            }

            private static bool ContainsAsciiDigit(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] >= '0' && text[i] <= '9')
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool StartsWithIgnoreCase(string value, string prefix)
            {
                return (value ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            private struct InGameUldCandidate
            {
                public readonly string Area;
                public readonly string Path;
                public readonly bool Optional;

                public InGameUldCandidate(string area, string path)
                    : this(area, path, false)
                {
                }

                public InGameUldCandidate(string area, string path, bool optional)
                {
                    Area = area;
                    Path = path;
                    Optional = optional;
                }
            }

            private sealed class InGameUldSurveyStats
            {
                public int UldCandidates;
                public int PresentUlds;
                public int TextNodeRows;
                public int ReadErrors;
            }

            private sealed class InGameSheetRiskStats
            {
                public int Sheets;
                public int CandidateRows;
                public int CandidateColumns;
                public int UniqueHangulCodepoints;
                public int ReadErrors;
            }

            private sealed class InGamePuaRiskStats
            {
                public int Rows;
                public int Missing;
            }

            private sealed class InGameFontRouteRiskSummary
            {
                public readonly string FontPath;
                public readonly string Risk;
                public int TextNodes;
                public readonly HashSet<string> Areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                public readonly HashSet<string> Ulds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                public InGameFontRouteRiskSummary(string fontPath, string risk)
                {
                    FontPath = fontPath;
                    Risk = risk;
                }
            }

            private sealed class InGameSheetRiskSummary
            {
                public readonly string Sheet;
                public int CandidateRows;
                public int CandidateColumns;
                public int HangulChars;
                public readonly HashSet<uint> HangulCodepoints = new HashSet<uint>();

                public InGameSheetRiskSummary(string sheet)
                {
                    Sheet = sheet;
                }
            }
        }
    }
}
