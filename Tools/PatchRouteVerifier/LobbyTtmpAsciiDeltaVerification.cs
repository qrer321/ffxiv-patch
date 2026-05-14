using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyLobbyTtmpAsciiDelta()
            {
                Console.WriteLine("[REPORT] lobby TTMP ASCII delta");
                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);

                LobbyTtmpAsciiDeltaStats stats = WriteLobbyTtmpAsciiDeltaReports(reportDir);
                Pass(
                    "lobby TTMP ASCII delta wrote {0} fonts, {1} ASCII glyphs, {2} shape/spacing differences, {3} pixel differences, {4} data-center routed visual-unsafe fonts",
                    stats.Fonts,
                    stats.Glyphs,
                    stats.ShapeSpacingDifferences,
                    stats.PixelDifferences,
                    stats.DataCenterVisualUnsafeFonts);
            }

            private LobbyTtmpAsciiDeltaStats WriteLobbyTtmpAsciiDeltaReports(string reportDir)
            {
                string summaryPath = Path.Combine(reportDir, "lobby-ttmp-ascii-delta-summary.tsv");
                string detailPath = Path.Combine(reportDir, "lobby-ttmp-ascii-delta-detail.tsv");
                HashSet<string> dataCenterFonts = ResolveDataCenterRoutedFonts();
                LobbyTtmpAsciiDeltaStats stats = new LobbyTtmpAsciiDeltaStats();

                using (StreamWriter summaryWriter = CreateUtf8Writer(summaryPath))
                using (StreamWriter detailWriter = CreateUtf8Writer(detailPath))
                {
                    summaryWriter.WriteLine("font_path\tdata_center_routed\tclean_present\tttmp_present\tchecked_glyphs\tmissing_clean\tmissing_ttmp\tsjis_differences\tshape_spacing_differences\tpixel_differences\ttotal_pixel_diff\tvisual_shape_safe_for_clean_ascii\twhole_ttmp_replacement_safe_for_clean_ascii");
                    detailWriter.WriteLine("font_path\tdata_center_routed\tcodepoint\tchar\tsjis_match\tshape_spacing_match\tpixel_diff\tclean_visible\tttmp_visible\tclean_spacing\tttmp_spacing");

                    for (int i = 0; i < LobbyPhraseFontPaths.Length; i++)
                    {
                        WriteLobbyTtmpAsciiDeltaFontRows(
                            summaryWriter,
                            detailWriter,
                            LobbyPhraseFontPaths[i],
                            dataCenterFonts.Contains(LobbyPhraseFontPaths[i]),
                            stats);
                    }
                }

                return stats;
            }

            private HashSet<string> ResolveDataCenterRoutedFonts()
            {
                Dictionary<string, HashSet<string>> fontsByScreen = CollectLobbyFontsByScreen();
                HashSet<string> fonts;
                if (fontsByScreen.TryGetValue("data-center-select", out fonts))
                {
                    return fonts;
                }

                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            private void WriteLobbyTtmpAsciiDeltaFontRows(
                StreamWriter summaryWriter,
                StreamWriter detailWriter,
                string fontPath,
                bool dataCenterRouted,
                LobbyTtmpAsciiDeltaStats stats)
            {
                bool cleanPresent = _cleanFont.ContainsPath(fontPath);
                bool ttmpPresent = _ttmpFont != null && _ttmpFont.ContainsPath(fontPath);
                int checkedGlyphs = 0;
                int missingClean = 0;
                int missingTtmp = 0;
                int shiftJisDifferences = 0;
                int shapeSpacingDifferences = 0;
                int pixelDifferences = 0;
                long totalPixelDiff = 0;

                if (cleanPresent && ttmpPresent)
                {
                    byte[] cleanFdt = _cleanFont.ReadFile(fontPath);
                    byte[] ttmpFdt = _ttmpFont.ReadFile(fontPath);
                    uint[] codepoints = CreateAsciiCodepoints();
                    for (int i = 0; i < codepoints.Length; i++)
                    {
                        uint codepoint = codepoints[i];
                        FdtGlyphEntry cleanGlyph;
                        if (!TryFindGlyph(cleanFdt, codepoint, out cleanGlyph))
                        {
                            missingClean++;
                            continue;
                        }

                        FdtGlyphEntry ttmpGlyph;
                        if (!TryFindGlyph(ttmpFdt, codepoint, out ttmpGlyph))
                        {
                            missingTtmp++;
                            continue;
                        }

                        checkedGlyphs++;
                        stats.Glyphs++;
                        bool shiftJisMatch = cleanGlyph.ShiftJisValue == ttmpGlyph.ShiftJisValue;
                        if (!shiftJisMatch)
                        {
                            shiftJisDifferences++;
                            stats.ShiftJisDifferences++;
                        }

                        bool shapeSpacingMatch = GlyphShapeSpacingMatches(cleanGlyph, ttmpGlyph);
                        if (!shapeSpacingMatch)
                        {
                            shapeSpacingDifferences++;
                            stats.ShapeSpacingDifferences++;
                        }

                        GlyphCanvas cleanCanvas = RenderGlyph(_cleanFont, fontPath, codepoint);
                        GlyphCanvas ttmpCanvas = RenderGlyph(_ttmpFont, fontPath, codepoint);
                        long pixelDiff = Diff(cleanCanvas.Alpha, ttmpCanvas.Alpha);
                        totalPixelDiff += pixelDiff;
                        if (pixelDiff != 0)
                        {
                            pixelDifferences++;
                            stats.PixelDifferences++;
                        }

                        if (!shiftJisMatch || !shapeSpacingMatch || pixelDiff != 0)
                        {
                            WriteTsvRow(
                                detailWriter,
                                fontPath,
                                dataCenterRouted ? "yes" : "no",
                                "U+" + codepoint.ToString("X4"),
                                FormatAsciiCodepointChar(codepoint),
                                shiftJisMatch ? "yes" : "no",
                                shapeSpacingMatch ? "yes" : "no",
                                pixelDiff.ToString(),
                                cleanCanvas.VisiblePixels.ToString(),
                                ttmpCanvas.VisiblePixels.ToString(),
                                FormatGlyphSpacing(cleanGlyph),
                                FormatGlyphSpacing(ttmpGlyph));
                        }
                    }
                }

                bool safe = cleanPresent &&
                    ttmpPresent &&
                    checkedGlyphs > 0 &&
                    missingClean == 0 &&
                    missingTtmp == 0 &&
                    shiftJisDifferences == 0 &&
                    shapeSpacingDifferences == 0 &&
                    pixelDifferences == 0;
                bool visuallySafe = cleanPresent &&
                    ttmpPresent &&
                    checkedGlyphs > 0 &&
                    missingClean == 0 &&
                    missingTtmp == 0 &&
                    shapeSpacingDifferences == 0 &&
                    pixelDifferences == 0;
                if (dataCenterRouted && !visuallySafe)
                {
                    stats.DataCenterVisualUnsafeFonts++;
                }

                stats.Fonts++;
                WriteTsvRow(
                    summaryWriter,
                    fontPath,
                    dataCenterRouted ? "yes" : "no",
                    cleanPresent ? "yes" : "no",
                    ttmpPresent ? "yes" : "no",
                    checkedGlyphs.ToString(),
                    missingClean.ToString(),
                    missingTtmp.ToString(),
                    shiftJisDifferences.ToString(),
                    shapeSpacingDifferences.ToString(),
                    pixelDifferences.ToString(),
                    totalPixelDiff.ToString(),
                    visuallySafe ? "yes" : "no",
                    safe ? "yes" : "no");
            }

            private static bool GlyphShapeSpacingMatches(FdtGlyphEntry left, FdtGlyphEntry right)
            {
                return left.Width == right.Width &&
                    left.Height == right.Height &&
                    left.OffsetX == right.OffsetX &&
                    left.OffsetY == right.OffsetY;
            }

            private static string FormatAsciiCodepointChar(uint codepoint)
            {
                if (codepoint == 0x22u)
                {
                    return "\\u0022";
                }

                if (codepoint == 0x5Cu)
                {
                    return "\\\\";
                }

                return char.ConvertFromUtf32(checked((int)codepoint));
            }

            private sealed class LobbyTtmpAsciiDeltaStats
            {
                public int Fonts;
                public int Glyphs;
                public int ShiftJisDifferences;
                public int ShapeSpacingDifferences;
                public int PixelDifferences;
                public int DataCenterVisualUnsafeFonts;
            }
        }
    }
}
