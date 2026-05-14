using System;
using System.Collections.Generic;
using System.IO;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int MaxLobbyHangulSourceCellFailuresPerFont = 20;

            private void VerifyLobbyHangulSourceCells()
            {
                Console.WriteLine("[FDT] Lobby Hangul source cells");
                if (_ttmpFont == null)
                {
                    Fail("TTMP font package is required to verify lobby Hangul source cells");
                    return;
                }

                uint[] codepoints = CollectLobbyHangulCoverageCodepoints();
                uint[] mainMenuCodepoints = CreateHangulCodepoints(LobbyScaledHangulPhrases.StartScreenMainMenu);
                List<LobbyAtlasCellUse> cleanLobbyCells = CollectActiveLobbyAtlasCellUses(_cleanFont);
                for (int fontIndex = 0; fontIndex < LobbyHangulCoverage.TargetFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyHangulCoverage.TargetFontPaths[fontIndex];
                    VerifyLobbyHangulSourceCellFont(
                        fontPath,
                        LobbyHangulCoverage.IsMainMenuOnlyTargetFontPath(fontPath) ? mainMenuCodepoints : codepoints,
                        cleanLobbyCells);
                }
            }

            private uint[] CollectLobbyHangulCoverageCodepoints()
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                AddDynamicHangulCodepoints(codepoints, LobbyScaledHangulPhrases.All);
                int staticCount = codepoints.Count;
                int sheetDerived = AddKoreanLobbyCoverageSheetCodepoints(codepoints);

                uint[] values = new uint[codepoints.Count];
                codepoints.CopyTo(values);
                Array.Sort(values);
                Pass(
                    "lobby Hangul source-cell codepoints collected: static={0}, sheet-derived={1}, total={2}",
                    staticCount,
                    sheetDerived,
                    values.Length);
                return values;
            }

            private int AddKoreanLobbyCoverageSheetCodepoints(HashSet<uint> codepoints)
            {
                if (string.IsNullOrWhiteSpace(_koreaSqpack))
                {
                    Warn("Korean sqpack path is missing; lobby source-cell verification uses static phrases only");
                    return 0;
                }

                string indexPath = Path.Combine(_koreaSqpack, TextPrefix + ".index");
                if (!File.Exists(indexPath))
                {
                    Warn("Korean text index is missing; lobby source-cell verification uses static phrases only: {0}", indexPath);
                    return 0;
                }

                int before = codepoints.Count;
                try
                {
                    using (CompositeArchive koreanText = new CompositeArchive(indexPath, _koreaSqpack, _koreaSqpack, TextPrefix))
                    {
                        Dictionary<string, List<LobbyHangulCoverageRowSpec>> rangesBySheet =
                            new Dictionary<string, List<LobbyHangulCoverageRowSpec>>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < LobbyHangulCoverage.Rows.Length; i++)
                        {
                            LobbyHangulCoverageRowSpec range = LobbyHangulCoverage.Rows[i];
                            if (string.IsNullOrEmpty(range.Sheet))
                            {
                                continue;
                            }

                            List<LobbyHangulCoverageRowSpec> ranges;
                            if (!rangesBySheet.TryGetValue(range.Sheet, out ranges))
                            {
                                ranges = new List<LobbyHangulCoverageRowSpec>();
                                rangesBySheet.Add(range.Sheet, ranges);
                            }

                            ranges.Add(range);
                        }

                        foreach (KeyValuePair<string, List<LobbyHangulCoverageRowSpec>> pair in rangesBySheet)
                        {
                            AddKoreanLobbyCoverageSheetCodepoints(koreanText, codepoints, pair.Key, pair.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Warn("Could not collect Korean lobby glyph coverage: {0}", ex.Message);
                    return 0;
                }

                return codepoints.Count - before;
            }

            private void AddKoreanLobbyCoverageSheetCodepoints(
                CompositeArchive koreanText,
                HashSet<uint> codepoints,
                string sheet,
                List<LobbyHangulCoverageRowSpec> ranges)
            {
                ExcelHeader header;
                try
                {
                    header = ExcelHeader.Parse(koreanText.ReadFile("exd/" + sheet + ".exh"));
                }
                catch (Exception ex)
                {
                    Warn("Korean lobby coverage sheet header missing for {0}: {1}", sheet, ex.Message);
                    return;
                }

                if (header.Variant != ExcelVariant.Default)
                {
                    Warn("Korean lobby coverage sheet variant is not supported for {0}: {1}", sheet, header.Variant);
                    return;
                }

                byte languageId = LanguageToId("ko");
                bool hasLanguageSuffix = header.HasLanguage(languageId);
                List<int> allStringColumns = header.GetStringColumnIndexes();
                if (allStringColumns.Count == 0)
                {
                    return;
                }

                for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = header.Pages[pageIndex];
                    if (!LobbyCoveragePageOverlaps(page, ranges))
                    {
                        continue;
                    }

                    string exdPath = BuildExdPath(sheet, page.StartId, "ko", hasLanguageSuffix);
                    ExcelDataFile file;
                    try
                    {
                        file = ExcelDataFile.Parse(koreanText.ReadFile(exdPath));
                    }
                    catch (Exception ex)
                    {
                        Warn("Korean lobby coverage page missing for {0}: {1}", exdPath, ex.Message);
                        continue;
                    }

                    for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                    {
                        ExcelDataRow row = file.Rows[rowIndex];
                        for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                        {
                            LobbyHangulCoverageRowSpec range = ranges[rangeIndex];
                            if (!range.Contains(row.RowId))
                            {
                                continue;
                            }

                            if (range.ColumnOffset.HasValue)
                            {
                                AddDynamicHangulCodepoints(codepoints, file.GetStringBytesByColumnOffset(row, header, range.ColumnOffset.Value));
                            }
                            else
                            {
                                for (int columnIndex = 0; columnIndex < allStringColumns.Count; columnIndex++)
                                {
                                    AddDynamicHangulCodepoints(codepoints, file.GetStringBytes(row, header, allStringColumns[columnIndex]));
                                }
                            }
                        }
                    }
                }
            }

            private void VerifyLobbyHangulSourceCellFont(string fontPath, uint[] codepoints, List<LobbyAtlasCellUse> cleanLobbyCells)
            {
                if (!_ttmpFont.ContainsPath(fontPath))
                {
                    Fail("{0} TTMP lobby Hangul source font is missing", fontPath);
                    return;
                }

                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(fontPath);
                    targetFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} lobby Hangul source-cell read error: {1}", fontPath, ex.Message);
                    return;
                }

                int checkedGlyphs = 0;
                int failures = 0;
                for (int codepointIndex = 0; codepointIndex < codepoints.Length; codepointIndex++)
                {
                    uint codepoint = codepoints[codepointIndex];
                    if (!VerifyLobbyHangulSourceCellGlyph(fontPath, sourceFdt, targetFdt, codepoint, cleanLobbyCells))
                    {
                        failures++;
                        if (failures >= MaxLobbyHangulSourceCellFailuresPerFont)
                        {
                            Warn("{0} lobby Hangul source-cell check stopped after {1} failures", fontPath, failures);
                            break;
                        }

                        continue;
                    }

                    checkedGlyphs++;
                }

                if (failures == 0)
                {
                    Pass("{0} lobby Hangul source cells match TTMP source: glyphs={1}", fontPath, checkedGlyphs);
                }
            }

            private bool VerifyLobbyHangulSourceCellGlyph(
                string fontPath,
                byte[] sourceFdt,
                byte[] targetFdt,
                uint codepoint,
                List<LobbyAtlasCellUse> cleanLobbyCells)
            {
                FdtGlyphEntry sourceGlyph;
                if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                {
                    Fail("{0} TTMP lobby source is missing U+{1:X4}", fontPath, codepoint);
                    return false;
                }

                FdtGlyphEntry targetGlyph;
                if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                {
                    Fail("{0} patched lobby target is missing U+{1:X4}", fontPath, codepoint);
                    return false;
                }

                if (!LobbyHangulGlyphMetricsMatchSource(sourceGlyph, targetGlyph))
                {
                    Fail(
                        "{0} U+{1:X4} patched lobby glyph metrics differ from TTMP source: target={2}, source={3}",
                        fontPath,
                        codepoint,
                        FormatLobbyHangulGlyphRoute(targetGlyph),
                        FormatLobbyHangulGlyphRoute(sourceGlyph));
                    return false;
                }

                LobbyAtlasCellUse conflictingCell;
                if (TryFindCleanLobbyCellOverlap(fontPath, codepoint, targetGlyph, cleanLobbyCells, out conflictingCell))
                {
                    Fail(
                        "{0} U+{1:X4} patched lobby cell overlaps clean lobby glyph {2} U+{3:X4}: target={4}, clean-cell={5}/ch{6}/{7},{8} {9}x{10}",
                        fontPath,
                        codepoint,
                        conflictingCell.FontPath,
                        conflictingCell.Codepoint,
                        FormatLobbyHangulGlyphRoute(targetGlyph),
                        conflictingCell.TexturePath,
                        conflictingCell.Channel,
                        conflictingCell.X,
                        conflictingCell.Y,
                        conflictingCell.Width,
                        conflictingCell.Height);
                    return false;
                }

                GlyphCanvas sourceCanvas;
                try
                {
                    sourceCanvas = RenderGlyph(_ttmpFont, fontPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} lobby Hangul TTMP source render error: {2}", fontPath, codepoint, ex.Message);
                    return false;
                }

                GlyphCanvas targetCanvas;
                try
                {
                    targetCanvas = RenderGlyph(_patchedFont, fontPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} lobby Hangul patched render error: {2}", fontPath, codepoint, ex.Message);
                    return false;
                }

                if (sourceCanvas.VisiblePixels < 10 || targetCanvas.VisiblePixels < 10)
                {
                    Fail(
                        "{0} U+{1:X4} lobby Hangul visibility is too low: target={2}, source={3}",
                        fontPath,
                        codepoint,
                        targetCanvas.VisiblePixels,
                        sourceCanvas.VisiblePixels);
                    return false;
                }

                long diff = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                if (diff != 0)
                {
                    Fail(
                        "{0} U+{1:X4} lobby Hangul pixels differ from TTMP source: score={2}, visible={3}/{4}",
                        fontPath,
                        codepoint,
                        diff,
                        targetCanvas.VisiblePixels,
                        sourceCanvas.VisiblePixels);
                    return false;
                }

                return true;
            }

            private static bool TryFindCleanLobbyCellOverlap(
                string fontPath,
                uint codepoint,
                FdtGlyphEntry targetGlyph,
                List<LobbyAtlasCellUse> cleanLobbyCells,
                out LobbyAtlasCellUse conflictingCell)
            {
                conflictingCell = new LobbyAtlasCellUse();
                if (cleanLobbyCells == null || cleanLobbyCells.Count == 0)
                {
                    return false;
                }

                string targetTexturePath = ResolveFontTexturePath(fontPath, targetGlyph.ImageIndex);
                if (!IsLobbyTexturePath(targetTexturePath))
                {
                    return false;
                }

                int targetChannel = targetGlyph.ImageIndex % 4;
                for (int i = 0; i < cleanLobbyCells.Count; i++)
                {
                    LobbyAtlasCellUse clean = cleanLobbyCells[i];
                    if (string.Equals(clean.FontPath, fontPath, StringComparison.OrdinalIgnoreCase) &&
                        clean.Codepoint == codepoint)
                    {
                        continue;
                    }

                    if (!string.Equals(clean.TexturePath, targetTexturePath, StringComparison.OrdinalIgnoreCase) ||
                        clean.Channel != targetChannel)
                    {
                        continue;
                    }

                    if (!RectanglesOverlap(targetGlyph.X, targetGlyph.Y, targetGlyph.Width, targetGlyph.Height, clean.X, clean.Y, clean.Width, clean.Height))
                    {
                        continue;
                    }

                    conflictingCell = clean;
                    return true;
                }

                return false;
            }

            private static bool LobbyCoveragePageOverlaps(ExcelPageDefinition page, List<LobbyHangulCoverageRowSpec> ranges)
            {
                uint pageEnd = page.RowCount == 0 ? page.StartId : page.StartId + page.RowCount - 1;
                for (int i = 0; i < ranges.Count; i++)
                {
                    if (ranges[i].StartId <= pageEnd && ranges[i].EndId >= page.StartId)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool LobbyHangulGlyphMetricsMatchSource(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
            {
                return sourceGlyph.ShiftJisValue == targetGlyph.ShiftJisValue &&
                       sourceGlyph.Width == targetGlyph.Width &&
                       sourceGlyph.Height == targetGlyph.Height &&
                       sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                       sourceGlyph.OffsetY == targetGlyph.OffsetY;
            }

            private static string FormatLobbyHangulGlyphRoute(FdtGlyphEntry glyph)
            {
                return "sjis=0x" + glyph.ShiftJisValue.ToString("X4") +
                    ", image=" + glyph.ImageIndex.ToString() +
                    ", cell=" + glyph.X.ToString() + "/" + glyph.Y.ToString() +
                    ", size=" + glyph.Width.ToString() + "x" + glyph.Height.ToString() +
                    ", offset=" + glyph.OffsetX.ToString() + "/" + glyph.OffsetY.ToString();
            }
        }
    }
}
