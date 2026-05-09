using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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

            private void VerifySystemSettingsMixedScalePhraseLayouts()
            {
                Console.WriteLine("[FDT] System settings mixed high-scale phrase layout");
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string fontPath = Derived4kLobbyFontPairs[i, 0];
                    string asciiSourceFontPath = ToLobbyFontPath(Derived4kLobbyFontPairs[i, 1]);
                    for (int phraseIndex = 0; phraseIndex < SystemSettingsMixedScalePhrases.Length; phraseIndex++)
                    {
                        VerifyMixedScalePhraseLayout(fontPath, asciiSourceFontPath, SystemSettingsMixedScalePhrases[phraseIndex]);
                    }
                }
            }

            private void Verify4kLobbyPhraseLayouts()
            {
                Console.WriteLine("[FDT] 4K lobby phrase layout");
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string fontPath = Derived4kLobbyFontPairs[i, 0];
                    string sourceFontPath = Derived4kLobbyFontPairs[i, 1];
                    for (int phraseIndex = 0; phraseIndex < FourKLobbyPhrases.Length; phraseIndex++)
                    {
                        VerifyNoDerived4kLobbyPhraseOverlap(fontPath, sourceFontPath, FourKLobbyPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifyMixedScalePhraseLayout(string fontPath, string asciiSourceFontPath, string phrase)
            {
                const int antiAliasOverlapTolerance = 1;
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} high-scale mixed phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                byte[] cleanTargetFdt;
                byte[] cleanAsciiSourceFdt;
                byte[] patchedFdt;
                try
                {
                    cleanTargetFdt = _cleanFont.ReadFile(fontPath);
                    cleanAsciiSourceFdt = _cleanFont.ReadFile(asciiSourceFontPath);
                    patchedFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} high-scale mixed phrase [{1}] FDT read error: {2}", fontPath, Escape(phrase), ex.Message);
                    return;
                }

                int cjkMedianAdvance;
                int cjkSamples;
                bool hasCjkBaseline = TryComputeMedianCjkAdvance(cleanTargetFdt, out cjkMedianAdvance, out cjkSamples);

                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (IsPhraseLayoutSpace(codepoint))
                    {
                        continue;
                    }

                    FdtGlyphEntry patchedGlyph;
                    if (!TryFindGlyph(patchedFdt, codepoint, out patchedGlyph))
                    {
                        Fail("{0} high-scale mixed phrase [{1}] missing patched U+{2:X4}", fontPath, Escape(phrase), codepoint);
                        return;
                    }

                    if (codepoint <= 0x7Eu)
                    {
                        FdtGlyphEntry cleanGlyph;
                        string cleanGlyphFontPath = fontPath;
                        if (!TryFindGlyph(cleanTargetFdt, codepoint, out cleanGlyph))
                        {
                            cleanGlyphFontPath = asciiSourceFontPath;
                            if (!TryFindGlyph(cleanAsciiSourceFdt, codepoint, out cleanGlyph))
                            {
                                Fail("{0} high-scale mixed phrase [{1}] missing clean ASCII U+{2:X4} in {3} and fallback {4}", fontPath, Escape(phrase), codepoint, fontPath, asciiSourceFontPath);
                                return;
                            }
                        }

                        if (!GlyphSpacingMetricsMatch(cleanGlyph, patchedGlyph))
                        {
                            Fail(
                                "{0} high-scale mixed phrase [{1}] ASCII U+{2:X4} metrics differ from clean {3}: target={4}, clean={5}",
                                fontPath,
                                Escape(phrase),
                                codepoint,
                                cleanGlyphFontPath,
                                FormatGlyphSpacing(patchedGlyph),
                                FormatGlyphSpacing(cleanGlyph));
                            return;
                        }

                        if (!VerifyGlyphTextureNeighborhoodMatchesClean(cleanGlyphFontPath, fontPath, codepoint, DataCenterGlyphTexturePadding, out error))
                        {
                            Fail(
                                "{0} high-scale mixed phrase [{1}] ASCII U+{2:X4} texture padding differs from clean {3}: {4}",
                                fontPath,
                                Escape(phrase),
                                codepoint,
                                cleanGlyphFontPath,
                                error);
                            return;
                        }

                        continue;
                    }

                    if (hasCjkBaseline && IsHangulCodepoint(codepoint))
                    {
                        int advance = GetGlyphAdvance(patchedGlyph);
                        if (advance < cjkMedianAdvance)
                        {
                            Fail(
                                "{0} high-scale mixed phrase [{1}] Hangul U+{2:X4} advance {3} is narrower than clean CJK median {4} from {5} samples",
                                fontPath,
                                Escape(phrase),
                                codepoint,
                                advance,
                                cjkMedianAdvance,
                                cjkSamples);
                            return;
                        }
                    }
                }

                if (layout.OverlapPixels > 0)
                {
                    int cleanAsciiOverlap;
                    if (!TryMeasureCleanAsciiPhraseOverlap(asciiSourceFontPath, phrase, out cleanAsciiOverlap, out error) ||
                        layout.OverlapPixels > cleanAsciiOverlap + antiAliasOverlapTolerance)
                    {
                        Fail("{0} high-scale mixed phrase [{1}] has overlap pixels={2}, clean ASCII baseline={3}", fontPath, Escape(phrase), layout.OverlapPixels, error ?? cleanAsciiOverlap.ToString());
                        return;
                    }
                }

                Pass(
                    "{0} high-scale mixed phrase [{1}] layout glyphs={2}, width={3}, cleanCjkMedian={4}",
                    fontPath,
                    Escape(phrase),
                    layout.Glyphs,
                    layout.Width,
                    hasCjkBaseline ? cjkMedianAdvance.ToString() : "n/a");
            }

            private bool TryMeasureCleanAsciiPhraseOverlap(string fontPath, string phrase, out int overlapPixels, out string error)
            {
                overlapPixels = 0;
                PhraseLayoutResult cleanLayout;
                if (!TryMeasurePhraseLayout(_cleanFont, fontPath, ToAsciiOnlyPhrase(phrase), false, out cleanLayout, out error))
                {
                    return false;
                }

                overlapPixels = cleanLayout.OverlapPixels;
                return true;
            }

            private static string ToAsciiOnlyPhrase(string phrase)
            {
                char[] chars = (phrase ?? string.Empty).ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (chars[i] > 0x7E)
                    {
                        chars[i] = ' ';
                    }
                }

                return new string(chars);
            }

            private static string ToLobbyFontPath(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                if (normalized.EndsWith("_lobby.fdt", StringComparison.OrdinalIgnoreCase))
                {
                    return normalized;
                }

                if (normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(0, normalized.Length - 4) + "_lobby.fdt";
                }

                return normalized;
            }

            private void VerifyNoDerived4kLobbyPhraseOverlap(string fontPath, string sourceFontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (layout.OverlapPixels == 0)
                {
                    Pass("{0} phrase [{1}] layout glyphs={2}, width={3}", fontPath, Escape(phrase), layout.Glyphs, layout.Width);
                    return;
                }

                if (layout.OverlapPixels <= 1)
                {
                    Pass("{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} within anti-alias tolerance", fontPath, Escape(phrase), layout.Glyphs, layout.Width, layout.OverlapPixels);
                    return;
                }

                PhraseLayoutResult sourceLayout;
                string sourceError;
                if (TryMeasurePhraseLayout(_patchedFont, sourceFontPath, phrase, true, out sourceLayout, out sourceError) &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels)
                {
                    Pass(
                        "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches source {5} baseline={6}",
                        fontPath,
                        Escape(phrase),
                        layout.Glyphs,
                        layout.Width,
                        layout.OverlapPixels,
                        sourceFontPath,
                        sourceLayout.OverlapPixels);
                    return;
                }

                Fail("{0} phrase [{1}] has overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
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
                    if (IsAsciiPhrase(phrase))
                    {
                        PhraseLayoutResult cleanLayout;
                        string cleanError;
                        if (TryMeasurePhraseLayout(_cleanFont, fontPath, phrase, false, out cleanLayout, out cleanError) &&
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

                        Fail("{0} phrase [{1}] has ASCII overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
                        return;
                    }

                    PhraseLayoutResult sourceLayout;
                    string sourceError;
                    if (_ttmpFont != null &&
                        _ttmpFont.ContainsPath(fontPath) &&
                        TryMeasurePhraseLayout(_ttmpFont, fontPath, phrase, out sourceLayout, out sourceError) &&
                        layout.OverlapPixels <= sourceLayout.OverlapPixels)
                    {
                        Pass(
                            "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches TTMP baseline={5}",
                            fontPath,
                            Escape(phrase),
                            layout.Glyphs,
                            layout.Width,
                            layout.OverlapPixels,
                            sourceLayout.OverlapPixels);
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
