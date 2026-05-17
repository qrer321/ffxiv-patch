using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int CombatFlyTextDamageGlyphNeighborhoodPadding = 16;

        private sealed partial class Verifier
        {
            private static readonly string[] KnownCombatFlyTextDamageFontPaths = new string[]
            {
                "common/font/Jupiter_45.fdt",
                "common/font/Jupiter_90.fdt"
            };

            private static readonly uint[] CombatFlyTextDamageCodepoints = new uint[]
            {
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                '!'
            };

            private static readonly string[] CombatFlyTextDamagePhrases = new string[]
            {
                "1",
                "1!",
                "1!!",
                "12345",
                "12345!",
                "12345!!",
                "999999",
                "999999!",
                "999999!!"
            };

            private void VerifyCombatFlyTextDamageGlyphs()
            {
                Console.WriteLine("[FDT] Combat fly-text damage number glyphs");
                if (_ttmpFont == null)
                {
                    Warn("TTMP font package was not provided; combat verifier will use explicit clean damage-number font candidates only");
                }

                bool ok = true;
                int glyphs = 0;
                int cleanGlyphs = 0;
                int phrases = 0;
                string[] fontPaths = CollectCombatFlyTextDamageFontPaths();
                for (int i = 0; i < fontPaths.Length; i++)
                {
                    string fontPath = fontPaths[i];
                    glyphs += VerifyCombatFlyTextDamageGlyphsForFont(fontPath, ref ok);
                    cleanGlyphs += VerifyCombatFlyTextNonHangulGlyphsForFont(fontPath, ref ok);
                    phrases += VerifyCombatFlyTextDamagePhrasesForFont(fontPath, ref ok);
                }

                if (fontPaths.Length == 0 || cleanGlyphs == 0)
                {
                    Fail("No combat fly-text damage-number glyphs were compared against clean source");
                    return;
                }

                if (ok)
                {
                    Pass(
                        "Combat fly-text damage-number glyphs match clean source: fonts={0}, focusedGlyphs={1}, cleanGlyphs={2}, phrases={3}, padding={4}",
                        fontPaths.Length,
                        glyphs,
                        cleanGlyphs,
                        phrases,
                        CombatFlyTextDamageGlyphNeighborhoodPadding);
                }
            }

            private string[] CollectCombatFlyTextDamageFontPaths()
            {
                System.Collections.Generic.List<string> fontPaths = new System.Collections.Generic.List<string>();
                System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < KnownCombatFlyTextDamageFontPaths.Length; i++)
                {
                    string path = KnownCombatFlyTextDamageFontPaths[i];
                    if (seen.Add(path))
                    {
                        fontPaths.Add(path);
                    }
                }

                if (fontPaths.Count == 0)
                {
                    Warn("No TTMP combat damage-number font candidates were found");
                }

                return fontPaths.ToArray();
            }

            private static bool IsCombatFlyTextDamageFontPath(string path)
            {
                string normalized = (path ?? string.Empty).Replace('\\', '/');
                return normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase) &&
                       normalized.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) < 0 &&
                       (string.Equals(normalized, "common/font/Jupiter_45.fdt", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "common/font/Jupiter_90.fdt", StringComparison.OrdinalIgnoreCase));
            }

            private int VerifyCombatFlyTextDamageGlyphsForFont(string fontPath, ref bool ok)
            {
                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _cleanFont.ReadFile(fontPath);
                    targetFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} combat damage glyph read error: {1}", fontPath, ex.Message);
                    ok = false;
                    return 0;
                }

                int compared = 0;
                for (int i = 0; i < CombatFlyTextDamageCodepoints.Length; i++)
                {
                    uint codepoint = CombatFlyTextDamageCodepoints[i];
                    FdtGlyphEntry sourceGlyph;
                    if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                    {
                        continue;
                    }

                    FdtGlyphEntry targetGlyph;
                    if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                    {
                        Fail("{0} combat damage U+{1:X4} exists in clean source but is missing in patched font", fontPath, codepoint);
                        ok = false;
                        continue;
                    }

                    GlyphCanvas sourceCanvas;
                    GlyphCanvas targetCanvas;
                    try
                    {
                        sourceCanvas = RenderGlyph(_cleanFont, fontPath, codepoint);
                        targetCanvas = RenderGlyph(_patchedFont, fontPath, codepoint);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} combat damage U+{1:X4} render error: {2}", fontPath, codepoint, ex.Message);
                        ok = false;
                        continue;
                    }

                    long score = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                    bool visualMetricsMatch = GlyphVisualMetricsMatch(sourceGlyph, targetGlyph);
                    string error;
                    bool neighborhoodMatch = VerifyGlyphTextureNeighborhoodMatchesClean(
                        fontPath,
                        fontPath,
                        codepoint,
                        CombatFlyTextDamageGlyphNeighborhoodPadding,
                        out error);
                    string mipError;
                    bool mipNeighborhoodMatch = VerifyGlyphTextureMipNeighborhoodsMatchClean(
                        fontPath,
                        fontPath,
                        codepoint,
                        CombatFlyTextDamageGlyphNeighborhoodPadding,
                        out mipError);

                    if (score == 0 &&
                        visualMetricsMatch &&
                        neighborhoodMatch &&
                        mipNeighborhoodMatch &&
                        sourceCanvas.VisiblePixels > 0 &&
                        targetCanvas.VisiblePixels > 0)
                    {
                        compared++;
                        continue;
                    }

                    Fail(
                        "{0} combat damage U+{1:X4} differs from clean source: score={2}, visible={3}/{4}, target={5}, clean={6}, neighborhood={7}",
                        fontPath,
                        codepoint,
                        score,
                        sourceCanvas.VisiblePixels,
                        targetCanvas.VisiblePixels,
                        FormatGlyphRoute(targetGlyph),
                        FormatGlyphRoute(sourceGlyph),
                        neighborhoodMatch && mipNeighborhoodMatch ? "ok" : (neighborhoodMatch ? mipError : error));
                    ok = false;
                }

                return compared;
            }

            private int VerifyCombatFlyTextNonHangulGlyphsForFont(string fontPath, ref bool ok)
            {
                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _cleanFont.ReadFile(fontPath);
                    targetFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} combat non-Hangul glyph scan read error: {1}", fontPath, ex.Message);
                    ok = false;
                    return 0;
                }

                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(sourceFdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    Fail("{0} combat non-Hangul glyph scan could not read source glyph table", fontPath);
                    ok = false;
                    return 0;
                }

                int compared = 0;
                int failures = 0;
                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    int offset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(FfxivKoreanPatch.FFXIVPatchGenerator.Endian.ReadUInt32LE(sourceFdt, offset), out codepoint) ||
                        IsHangulCodepoint(codepoint))
                    {
                        continue;
                    }

                    FdtGlyphEntry sourceGlyph = ReadGlyphEntry(sourceFdt, offset);
                    if (sourceGlyph.Width == 0 || sourceGlyph.Height == 0)
                    {
                        continue;
                    }

                    FdtGlyphEntry targetGlyph;
                    if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                    {
                        Fail("{0} combat non-Hangul U+{1:X4} exists in clean source but is missing in patched font", fontPath, codepoint);
                        ok = false;
                        failures++;
                        if (failures >= MaxTexturePaddingFailuresPerFont)
                        {
                            Warn("{0} combat non-Hangul scan stopped after {1} failures", fontPath, failures);
                            break;
                        }

                        continue;
                    }

                    GlyphCanvas sourceCanvas;
                    GlyphCanvas targetCanvas;
                    try
                    {
                        sourceCanvas = RenderGlyph(_cleanFont, fontPath, codepoint);
                        targetCanvas = RenderGlyph(_patchedFont, fontPath, codepoint);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} combat non-Hangul U+{1:X4} render error: {2}", fontPath, codepoint, ex.Message);
                        ok = false;
                        failures++;
                        if (failures >= MaxTexturePaddingFailuresPerFont)
                        {
                            Warn("{0} combat non-Hangul scan stopped after {1} failures", fontPath, failures);
                            break;
                        }

                        continue;
                    }

                    string error;
                    bool neighborhoodMatch = VerifyGlyphTextureNeighborhoodMatchesClean(
                        fontPath,
                        fontPath,
                        codepoint,
                        CombatFlyTextDamageGlyphNeighborhoodPadding,
                        out error);
                    string mipError;
                    bool mipNeighborhoodMatch = VerifyGlyphTextureMipNeighborhoodsMatchClean(
                        fontPath,
                        fontPath,
                        codepoint,
                        CombatFlyTextDamageGlyphNeighborhoodPadding,
                        out mipError);
                    long score = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                    if (score == 0 &&
                        GlyphVisualMetricsMatch(sourceGlyph, targetGlyph) &&
                        neighborhoodMatch &&
                        mipNeighborhoodMatch &&
                        sourceCanvas.VisiblePixels == targetCanvas.VisiblePixels)
                    {
                        compared++;
                        continue;
                    }

                    Fail(
                        "{0} combat non-Hangul U+{1:X4} differs from clean source: score={2}, visible={3}/{4}, target={5}, clean={6}, neighborhood={7}",
                        fontPath,
                        codepoint,
                        score,
                        sourceCanvas.VisiblePixels,
                        targetCanvas.VisiblePixels,
                        FormatGlyphRoute(targetGlyph),
                        FormatGlyphRoute(sourceGlyph),
                        neighborhoodMatch && mipNeighborhoodMatch ? "ok" : (neighborhoodMatch ? mipError : error));
                    ok = false;
                    failures++;
                    if (failures >= MaxTexturePaddingFailuresPerFont)
                    {
                        Warn("{0} combat non-Hangul scan stopped after {1} failures", fontPath, failures);
                        break;
                    }
                }

                return compared;
            }

            private int VerifyCombatFlyTextDamagePhrasesForFont(string fontPath, ref bool ok)
            {
                int compared = 0;
                for (int i = 0; i < CombatFlyTextDamagePhrases.Length; i++)
                {
                    string phrase = CombatFlyTextDamagePhrases[i];
                    PhraseRenderSnapshot sourcePixels;
                    PhraseRenderSnapshot targetPixels;
                    PhraseLayoutResult sourceLayout;
                    PhraseLayoutResult targetLayout;
                    string error;

                    if (!TryRenderPhrasePixels(_cleanFont, fontPath, phrase, false, out sourcePixels, out error))
                    {
                        Warn("{0} combat damage phrase [{1}] clean render skipped: {2}", fontPath, phrase, error);
                        continue;
                    }

                    if (!TryRenderPhrasePixels(_patchedFont, fontPath, phrase, true, out targetPixels, out error))
                    {
                        Fail("{0} combat damage phrase [{1}] patched render error: {2}", fontPath, phrase, error);
                        ok = false;
                        continue;
                    }

                    if (!TryMeasurePhraseLayout(_cleanFont, fontPath, phrase, false, out sourceLayout, out error))
                    {
                        Warn("{0} combat damage phrase [{1}] clean layout skipped: {2}", fontPath, phrase, error);
                        continue;
                    }

                    if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out targetLayout, out error))
                    {
                        Fail("{0} combat damage phrase [{1}] patched layout error: {2}", fontPath, phrase, error);
                        ok = false;
                        continue;
                    }

                    if (sourcePixels.Width == targetPixels.Width &&
                        sourcePixels.Glyphs == targetPixels.Glyphs &&
                        sourceLayout.Width == targetLayout.Width &&
                        sourceLayout.Glyphs == targetLayout.Glyphs &&
                        sourceLayout.OverlapPixels == targetLayout.OverlapPixels &&
                        PhrasePixelsEqual(sourcePixels.Pixels, targetPixels.Pixels))
                    {
                        compared++;
                        continue;
                    }

                    Fail(
                        "{0} combat damage phrase [{1}] differs from clean source: glyphs={2}/{3}, width={4}/{5}, overlap={6}/{7}, pixels={8}/{9}",
                        fontPath,
                        phrase,
                        targetLayout.Glyphs,
                        sourceLayout.Glyphs,
                        targetLayout.Width,
                        sourceLayout.Width,
                        targetLayout.OverlapPixels,
                        sourceLayout.OverlapPixels,
                        targetPixels.Pixels.Count,
                        sourcePixels.Pixels.Count);
                    ok = false;
                }

                return compared;
            }

            private static bool GlyphVisualMetricsMatch(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
            {
                return sourceGlyph.Width == targetGlyph.Width &&
                       sourceGlyph.Height == targetGlyph.Height &&
                       sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                       sourceGlyph.OffsetY == targetGlyph.OffsetY;
            }

            private static bool GlyphRouteMatches(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
            {
                return GlyphVisualMetricsMatch(sourceGlyph, targetGlyph) &&
                       sourceGlyph.ImageIndex == targetGlyph.ImageIndex &&
                       sourceGlyph.X == targetGlyph.X &&
                       sourceGlyph.Y == targetGlyph.Y;
            }
        }
    }
}
