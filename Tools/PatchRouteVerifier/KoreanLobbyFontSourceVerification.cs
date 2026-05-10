using System;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly FontPair[] KoreanLobbySourceFontPairs = new FontPair[]
            {
                new FontPair("common/font/KrnAXIS_120.fdt", "common/font/AXIS_12_lobby.fdt"),
                new FontPair("common/font/KrnAXIS_140.fdt", "common/font/AXIS_14_lobby.fdt"),
                new FontPair("common/font/KrnAXIS_180.fdt", "common/font/AXIS_18_lobby.fdt"),
                new FontPair("common/font/KrnAXIS_360.fdt", "common/font/AXIS_36_lobby.fdt")
            };

            private void VerifyKoreanLobbyFontSourceRoutes()
            {
                Console.WriteLine("[FDT] Korean client lobby font source routes");
                if (_koreanFont == null)
                {
                    Fail("Korean client font source route verification requires --korea");
                    return;
                }

                uint[] codepoints = CollectLobbyScaleSensitiveHangulCodepoints();
                for (int i = 0; i < KoreanLobbySourceFontPairs.Length; i++)
                {
                    VerifyKoreanLobbyFontSourceRoute(KoreanLobbySourceFontPairs[i], codepoints);
                }
            }

            private static bool IsKoreanLobbyAxisSourceTarget(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                for (int i = 0; i < KoreanLobbySourceFontPairs.Length; i++)
                {
                    if (string.Equals(normalized, KoreanLobbySourceFontPairs[i].TargetFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void VerifyKoreanLobbyFontSourceRoute(FontPair pair, uint[] codepoints)
            {
                if (!_koreanFont.ContainsPath(pair.SourceFontPath))
                {
                    Fail("{0} is missing from the Korean client font source for {1}", pair.SourceFontPath, pair.TargetFontPath);
                    return;
                }

                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _koreanFont.ReadFile(pair.SourceFontPath);
                    targetFdt = _patchedFont.ReadFile(pair.TargetFontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} Korean lobby source-route read error from {1}: {2}", pair.TargetFontPath, pair.SourceFontPath, ex.Message);
                    return;
                }

                int checkedGlyphs = 0;
                int reportedFailures = 0;
                const int failureReportLimit = 8;
                for (int i = 0; i < codepoints.Length; i++)
                {
                    uint codepoint = codepoints[i];
                    if (VerifyKoreanLobbyFontSourceGlyph(pair, sourceFdt, targetFdt, codepoint))
                    {
                        checkedGlyphs++;
                        continue;
                    }

                    reportedFailures++;
                    if (reportedFailures >= failureReportLimit)
                    {
                        Fail("{0} Korean lobby source route stopped after {1} reported glyph mismatches from {2}", pair.TargetFontPath, reportedFailures, pair.SourceFontPath);
                        return;
                    }
                }

                if (reportedFailures == 0)
                {
                    Pass("{0} lobby Hangul glyph shapes match Korean client source {1} with normalized advance: glyphs={2}", pair.TargetFontPath, pair.SourceFontPath, checkedGlyphs);
                }
            }

            private bool VerifyKoreanLobbyFontSourceGlyph(FontPair pair, byte[] sourceFdt, byte[] targetFdt, uint codepoint)
            {
                FdtGlyphEntry sourceGlyph;
                if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                {
                    Fail("{0} Korean source {1} is missing U+{2:X4}", pair.TargetFontPath, pair.SourceFontPath, codepoint);
                    return false;
                }

                FdtGlyphEntry targetGlyph;
                if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                {
                    Fail("{0} patched target is missing Korean-source U+{1:X4}", pair.TargetFontPath, codepoint);
                    return false;
                }

                int expectedAdvance = GetExpectedKoreanLobbyAxisAdvance(sourceGlyph);
                int targetAdvance = GetGlyphAdvance(targetGlyph);
                if (targetGlyph.Width != sourceGlyph.Width ||
                    targetGlyph.Height != sourceGlyph.Height ||
                    targetGlyph.OffsetY != sourceGlyph.OffsetY ||
                    targetAdvance != expectedAdvance)
                {
                    Fail(
                        "{0} U+{1:X4} metrics differ from Korean source route: target={2}, source={3}, targetAdvance={4}, expectedAdvance={5}",
                        pair.TargetFontPath,
                        codepoint,
                        FormatGlyphSpacing(targetGlyph),
                        FormatGlyphSpacing(sourceGlyph),
                        targetAdvance,
                        expectedAdvance);
                    return false;
                }

                GlyphCanvas sourceCanvas;
                GlyphCanvas targetCanvas;
                try
                {
                    sourceCanvas = RenderGlyph(_koreanFont, pair.SourceFontPath, codepoint);
                    targetCanvas = RenderGlyph(_patchedFont, pair.TargetFontPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} Korean source render error from {2}: {3}", pair.TargetFontPath, codepoint, pair.SourceFontPath, ex.Message);
                    return false;
                }

                if (sourceCanvas.VisiblePixels < 10 || targetCanvas.VisiblePixels < 10)
                {
                    Fail(
                        "{0} U+{1:X4} Korean source visibility is too low: target={2}, source={3}",
                        pair.TargetFontPath,
                        codepoint,
                        targetCanvas.VisiblePixels,
                        sourceCanvas.VisiblePixels);
                    return false;
                }

                long diff = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                if (diff != 0)
                {
                    Fail(
                        "{0} U+{1:X4} pixels differ from Korean source: score={2}, visible={3}/{4}, textures={5}/{6}",
                        pair.TargetFontPath,
                        codepoint,
                        diff,
                        targetCanvas.VisiblePixels,
                        sourceCanvas.VisiblePixels,
                        targetCanvas.TexturePath,
                        sourceCanvas.TexturePath);
                    return false;
                }

                return true;
            }

            private static int GetExpectedKoreanLobbyAxisAdvance(FdtGlyphEntry sourceGlyph)
            {
                return Math.Max(sourceGlyph.Width, GetGlyphAdvance(sourceGlyph));
            }
        }
    }
}
