using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int InGameCleanAsciiGlyphNeighborhoodPadding = 8;

        private sealed partial class Verifier
        {
            private void VerifyInGameCleanAsciiGlyphs()
            {
                Console.WriteLine("[FDT] In-game clean ASCII glyph routes");
                bool ok = true;
                int fonts = 0;
                int glyphs = 0;
                HashSet<string> checkedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < DialoguePhraseFontPaths.Length; i++)
                {
                    string targetFontPath = DialoguePhraseFontPaths[i];
                    if (IsLobbyFontPath(targetFontPath) ||
                        !IsInGameCleanAsciiRepairedFontPath(targetFontPath) ||
                        !checkedFonts.Add(targetFontPath))
                    {
                        continue;
                    }

                    int checkedGlyphs = VerifyInGameCleanAsciiGlyphsForFont(targetFontPath, ref ok);
                    if (checkedGlyphs > 0)
                    {
                        fonts++;
                        glyphs += checkedGlyphs;
                    }
                }

                if (glyphs == 0)
                {
                    Fail("No in-game clean ASCII glyphs were compared");
                    return;
                }

                if (ok)
                {
                    Pass(
                        "In-game ASCII/number/symbol glyphs match clean source: fonts={0}, glyphs={1}, damage_padding=16",
                        fonts,
                        glyphs);
                }
            }

            private int VerifyInGameCleanAsciiGlyphsForFont(string targetFontPath, ref bool ok)
            {
                string sourceFontPath = ResolveCleanAsciiReferenceFontPath(targetFontPath);
                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    targetFdt = _patchedFont.ReadFile(targetFontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} clean ASCII read error from {1}: {2}", targetFontPath, sourceFontPath, ex.Message);
                    ok = false;
                    return 0;
                }

                uint[] codepoints = CreateAsciiCodepoints();
                int compared = 0;
                int failures = 0;
                for (int i = 0; i < codepoints.Length; i++)
                {
                    uint codepoint = codepoints[i];
                    FdtGlyphEntry sourceGlyph;
                    if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                    {
                        continue;
                    }

                    FdtGlyphEntry targetGlyph;
                    if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                    {
                        Fail("{0} clean ASCII U+{1:X4} exists in {2} but is missing in patched font", targetFontPath, codepoint, sourceFontPath);
                        ok = false;
                        failures++;
                        if (failures >= MaxTexturePaddingFailuresPerFont)
                        {
                            Warn("{0} clean ASCII scan stopped after {1} failures", targetFontPath, failures);
                            break;
                        }

                        continue;
                    }

                    GlyphCanvas sourceCanvas;
                    GlyphCanvas targetCanvas;
                    try
                    {
                        sourceCanvas = RenderGlyph(_cleanFont, sourceFontPath, codepoint);
                        targetCanvas = RenderGlyph(_patchedFont, targetFontPath, codepoint);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} clean ASCII U+{1:X4} render error: {2}", targetFontPath, codepoint, ex.Message);
                        ok = false;
                        failures++;
                        if (failures >= MaxTexturePaddingFailuresPerFont)
                        {
                            Warn("{0} clean ASCII scan stopped after {1} failures", targetFontPath, failures);
                            break;
                        }

                        continue;
                    }

                    long score = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                    string error = "skipped for dedicated KrnAXIS texture";
                    int neighborhoodPadding = GetInGameCleanAsciiGlyphNeighborhoodPadding(targetFontPath);
                    bool neighborhoodMatch = true;
                    if (!IsKrnAxisFontPath(targetFontPath) && neighborhoodPadding > 0)
                    {
                        neighborhoodMatch = VerifyGlyphTextureNeighborhoodMatchesClean(
                            sourceFontPath,
                            targetFontPath,
                            codepoint,
                            neighborhoodPadding,
                            out error);
                    }

                    if (score == 0 &&
                        GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph) &&
                        neighborhoodMatch)
                    {
                        compared++;
                        continue;
                    }

                    Fail(
                        "{0} clean ASCII U+{1:X4} differs from {2}: score={3}, visible={4}/{5}, target={6}, clean={7}, neighborhood={8}",
                        targetFontPath,
                        codepoint,
                        sourceFontPath,
                        score,
                        sourceCanvas.VisiblePixels,
                        targetCanvas.VisiblePixels,
                        FormatGlyphSpacing(targetGlyph),
                        FormatGlyphSpacing(sourceGlyph),
                        neighborhoodMatch ? "ok" : error);
                    ok = false;
                    failures++;
                    if (failures >= MaxTexturePaddingFailuresPerFont)
                    {
                        Warn("{0} clean ASCII scan stopped after {1} failures", targetFontPath, failures);
                        break;
                    }
                }

                return compared;
            }

            private static bool IsKrnAxisFontPath(string fontPath)
            {
                return !string.IsNullOrEmpty(fontPath) &&
                       fontPath.IndexOf("KrnAXIS_", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static bool IsInGameCleanAsciiRepairedFontPath(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                return string.Equals(normalized, "common/font/Jupiter_45.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/Jupiter_90.fdt", StringComparison.OrdinalIgnoreCase) ||
                       normalized.IndexOf("/AXIS_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       normalized.IndexOf("/KrnAXIS_", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static int GetInGameCleanAsciiGlyphNeighborhoodPadding(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                return string.Equals(normalized, "common/font/Jupiter_45.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/Jupiter_90.fdt", StringComparison.OrdinalIgnoreCase)
                    ? 16
                    : 0;
            }
        }
    }
}
