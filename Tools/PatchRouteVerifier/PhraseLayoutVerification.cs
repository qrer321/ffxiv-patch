using System;

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
    }
}
