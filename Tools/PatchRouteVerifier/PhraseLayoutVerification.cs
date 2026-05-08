using System;
using System.Collections.Generic;
using System.Globalization;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyHighScaleAsciiPhraseLayouts()
            {
                Console.WriteLine("[FDT] High-scale ASCII phrase metrics");
                for (int fontIndex = 0; fontIndex < HighScaleAsciiFontPairs.Length; fontIndex++)
                {
                    FontPair fontPair = HighScaleAsciiFontPairs[fontIndex];
                    for (int phraseIndex = 0; phraseIndex < HighScaleAsciiPhrases.Length; phraseIndex++)
                    {
                        VerifyPhraseMetricsMatchClean(fontPair.SourceFontPath, fontPair.TargetFontPath, HighScaleAsciiPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifySystemSettingsScaledPhraseLayouts()
            {
                Console.WriteLine("[FDT] System settings scaled phrase layout");
                for (int fontIndex = 0; fontIndex < SystemSettingsScaledFonts.Length; fontIndex++)
                {
                    for (int phraseIndex = 0; phraseIndex < SystemSettingsScaledPhrases.Length; phraseIndex++)
                    {
                        VerifyNoPhraseOverlap(SystemSettingsScaledFonts[fontIndex], SystemSettingsScaledPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifyPhraseMetricsMatchClean(string sourceFontPath, string targetFontPath, string phrase)
            {
                try
                {
                    byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                    int advance = 0;
                    int glyphs = 0;

                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (codepoint <= 0x20)
                        {
                            advance += 8;
                            continue;
                        }

                        FdtGlyphEntry sourceGlyph;
                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            Fail("{0} phrase [{1}] missing clean U+{2:X4}", sourceFontPath, Escape(phrase), codepoint);
                            return;
                        }

                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} phrase [{1}] missing patched U+{2:X4}", targetFontPath, Escape(phrase), codepoint);
                            return;
                        }

                        if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                        {
                            Fail(
                                "{0} phrase [{1}] U+{2:X4} metrics differ from {3}: target={4}, clean={5}",
                                targetFontPath,
                                Escape(phrase),
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            return;
                        }

                        advance += Math.Max(1, sourceGlyph.Width + sourceGlyph.OffsetX);
                        glyphs++;
                    }

                    Pass("{0} phrase [{1}] metrics match {2}, glyphs={3}, width={4}", targetFontPath, Escape(phrase), sourceFontPath, glyphs, advance);
                }
                catch (Exception ex)
                {
                    Fail("{0} phrase [{1}] metric check error: {2}", targetFontPath, Escape(phrase), ex.Message);
                }
            }

            private void Verify4kLobbyPhraseLayouts()
            {
                Console.WriteLine("[FDT] 4K lobby phrase layout");
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string fontPath = Derived4kLobbyFontPairs[i, 0];
                    for (int phraseIndex = 0; phraseIndex < FourKLobbyPhrases.Length; phraseIndex++)
                    {
                        VerifyNoPhraseOverlap(fontPath, FourKLobbyPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifyNoPhraseOverlap(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (layout.OverlapPixels > 0)
                {
                    PhraseLayoutResult cleanLayout;
                    string cleanError;
                    if (IsAsciiPhrase(phrase) &&
                        TryMeasurePhraseLayout(_cleanFont, fontPath, phrase, false, out cleanLayout, out cleanError) &&
                        layout.OverlapPixels <= cleanLayout.OverlapPixels)
                    {
                        Pass(
                            "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches clean baseline={5}",
                            fontPath,
                            Escape(phrase),
                            layout.Glyphs,
                            layout.Width,
                            layout.OverlapPixels,
                            cleanLayout.OverlapPixels);
                        return;
                    }

                    Fail("{0} phrase [{1}] has overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
                    return;
                }

                Pass("{0} phrase [{1}] layout glyphs={2}, width={3}", fontPath, Escape(phrase), layout.Glyphs, layout.Width);
            }

            private bool TryMeasurePhraseLayout(
                CompositeArchive archive,
                string fontPath,
                string phrase,
                bool validateGlyphShape,
                out PhraseLayoutResult result,
                out string error)
            {
                result = new PhraseLayoutResult();
                error = null;
                try
                {
                    byte[] fdt = archive.ReadFile(fontPath);
                    HashSet<long> occupiedPixels = new HashSet<long>();
                    int cursor = 0;
                    int glyphs = 0;
                    int overlap = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);

                        if (codepoint <= 0x20)
                        {
                            cursor += 8;
                            continue;
                        }

                        FdtGlyphEntry glyph;
                        if (!TryFindGlyph(fdt, codepoint, out glyph))
                        {
                            error = "missing U+" + codepoint.ToString("X4");
                            return false;
                        }

                        GlyphCanvas canvas = RenderGlyph(archive, fontPath, codepoint);
                        if (validateGlyphShape)
                        {
                            int minimumVisiblePixels = IsHangulCodepoint(codepoint) ? 10 : 1;
                            if (canvas.VisiblePixels < minimumVisiblePixels)
                            {
                                error = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "U+{0:X4} visible={1}, expected at least {2}",
                                    codepoint,
                                    canvas.VisiblePixels,
                                    minimumVisiblePixels);
                                return false;
                            }

                            if (IsHangulCodepoint(codepoint) &&
                                (GlyphMatchesFallback(fontPath, canvas, codepoint, '-') ||
                                 GlyphMatchesFallback(fontPath, canvas, codepoint, '=')))
                            {
                                error = "U+" + codepoint.ToString("X4") + " matches fallback glyph";
                                return false;
                            }
                        }

                        overlap += AddGlyphPixelsToLayout(occupiedPixels, cursor, canvas.Alpha);
                        cursor += Math.Max(1, glyph.Width + glyph.OffsetX);
                        glyphs++;
                    }

                    result.Glyphs = glyphs;
                    result.Width = cursor;
                    result.OverlapPixels = overlap;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static int AddGlyphPixelsToLayout(HashSet<long> occupiedPixels, int cursor, byte[] alpha)
            {
                int overlap = 0;
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    int rowOffset = y * GlyphCanvasSize;
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        if (alpha[rowOffset + x] == 0)
                        {
                            continue;
                        }

                        int pixelX = cursor + x - 32;
                        int pixelY = y - 32;
                        long key = ((long)pixelY << 32) ^ (uint)pixelX;
                        if (!occupiedPixels.Add(key))
                        {
                            overlap++;
                        }
                    }
                }

                return overlap;
            }

            private static bool IsAsciiPhrase(string phrase)
            {
                for (int i = 0; i < phrase.Length; i++)
                {
                    if (phrase[i] > 0x7E)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static uint ReadCodepoint(string value, ref int index)
        {
            if (char.IsHighSurrogate(value[index]) &&
                index + 1 < value.Length &&
                char.IsLowSurrogate(value[index + 1]))
            {
                uint codepoint = (uint)char.ConvertToUtf32(value[index], value[index + 1]);
                index++;
                return codepoint;
            }

            return value[index];
        }

        private struct PhraseLayoutResult
        {
            public int Glyphs;
            public int Width;
            public int OverlapPixels;
        }
    }
}
