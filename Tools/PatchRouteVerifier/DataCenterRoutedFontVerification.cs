using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyDataCenterRoutedFontLabels(string dumpGroup, HashSet<string> routedFontPaths)
            {
                foreach (string fontPath in routedFontPaths)
                {
                    VerifyLabelGlyphsEqualClean(fontPath, DataCenterWorldmapLabels);
                    VerifyDataCenterRoutedAsciiPhraseMetrics(fontPath);
                    VerifyDataCenterRoutedAsciiPhrasePixels(fontPath);
                    VerifyDataCenterRoutedAsciiPhraseVisualSpacing(fontPath);
                    VerifyDataCenterRoutedAsciiStrictSpacing(fontPath);
                    VerifyDataCenterRoutedAsciiTexturePadding(fontPath);
                    VerifyDataCenterRoutedKoreanPhraseLayouts(fontPath);
                    DumpLabelPreview(dumpGroup, fontPath, DataCenterWorldmapLabels);
                }
            }

            private void VerifyDataCenterRoutedAsciiPhraseMetrics(string fontPath)
            {
                string sourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPath);
                for (int phraseIndex = 0; phraseIndex < DataCenterWorldmapLabels.Length; phraseIndex++)
                {
                    VerifyPhraseMetricsMatchClean(sourceFontPath, fontPath, DataCenterWorldmapLabels[phraseIndex]);
                }
            }

            private void VerifyDataCenterRoutedAsciiPhrasePixels(string fontPath)
            {
                string sourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPath);
                for (int phraseIndex = 0; phraseIndex < DataCenterWorldmapLabels.Length; phraseIndex++)
                {
                    VerifyPhrasePixelsMatchClean(sourceFontPath, fontPath, DataCenterWorldmapLabels[phraseIndex]);
                }
            }

            private void VerifyDataCenterRoutedAsciiPhraseVisualSpacing(string fontPath)
            {
                for (int phraseIndex = 0; phraseIndex < DataCenterCriticalRenderLabels.Length; phraseIndex++)
                {
                    VerifyPhraseMinimumVisualGap(fontPath, DataCenterCriticalRenderLabels[phraseIndex]);
                }
            }

            private void VerifyDataCenterRoutedAsciiStrictSpacing(string fontPath)
            {
                const int failureReportLimit = 12;

                if (!IsDataCenterStrictAsciiSpacingFont(fontPath))
                {
                    return;
                }

                int checkedPhrases = 0;
                int failures = 0;
                for (int phraseIndex = 0; phraseIndex < DataCenterStrictAsciiSpacingLabels.Length; phraseIndex++)
                {
                    string phrase = DataCenterStrictAsciiSpacingLabels[phraseIndex];
                    PhraseLayoutResult layout;
                    string error;
                    if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                    {
                        Fail("{0} data-center strict ASCII phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                        failures++;
                    }
                    else if (layout.Glyphs > 1)
                    {
                        checkedPhrases++;
                        if (layout.MinimumGapPixels < 0)
                        {
                            string sourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPath);
                            PhraseLayoutResult cleanLayout;
                            string cleanError;
                            if (!string.IsNullOrEmpty(sourceFontPath) &&
                                TryMeasurePhraseLayout(_cleanFont, sourceFontPath, phrase, false, out cleanLayout, out cleanError) &&
                                layout.MinimumGapPixels >= cleanLayout.MinimumGapPixels &&
                                layout.OverlapPixels <= cleanLayout.OverlapPixels + 2)
                            {
                                continue;
                            }

                            string pairDetail = DescribeScaledLobbyPairSpacing(
                                fontPath,
                                layout.MinimumGapLeftCodepoint,
                                layout.MinimumGapRightCodepoint);
                            Fail(
                                "{0} data-center strict ASCII phrase [{1}] minGap={2} is below 0, pair=U+{3:X4}/U+{4:X4}{5}",
                                fontPath,
                                Escape(phrase),
                                layout.MinimumGapPixels,
                                layout.MinimumGapLeftCodepoint,
                                layout.MinimumGapRightCodepoint,
                                pairDetail);
                            failures++;
                        }
                    }

                    if (failures >= failureReportLimit)
                    {
                        Warn("{0} data-center strict ASCII spacing stopped after {1} reported failures", fontPath, failures);
                        return;
                    }
                }

                if (failures == 0)
                {
                    Pass("{0} data-center strict ASCII spacing checked: phrases={1}", fontPath, checkedPhrases);
                }
            }

            private void VerifyDataCenterRoutedAsciiTexturePadding(string fontPath)
            {
                string sourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPath);
                uint[] codepoints = CollectNonSpaceCodepoints(DataCenterWorldmapLabels);
                int checkedGlyphs = 0;
                int failures = 0;
                for (int codepointIndex = 0; codepointIndex < codepoints.Length; codepointIndex++)
                {
                    uint codepoint = codepoints[codepointIndex];
                    if (codepoint > 0x7E)
                    {
                        continue;
                    }

                    string error;
                    if (!VerifyGlyphTextureNeighborhoodMatchesClean(sourceFontPath, fontPath, codepoint, DataCenterGlyphTexturePadding, out error))
                    {
                        Fail(
                            "{0} U+{1:X4} texture padding differs from clean route {2}: {3}",
                            fontPath,
                            codepoint,
                            sourceFontPath,
                            error);
                        failures++;
                        if (failures >= MaxTexturePaddingFailuresPerFont)
                        {
                            Warn("{0} texture padding check stopped after {1} failures", fontPath, failures);
                            break;
                        }

                        continue;
                    }

                    checkedGlyphs++;
                }

                if (failures == 0)
                {
                    Pass("{0} data-center ASCII texture padding matches clean route: glyphs={1}, padding={2}", fontPath, checkedGlyphs, DataCenterGlyphTexturePadding);
                }
            }

            private void VerifyDataCenterRoutedKoreanPhraseLayouts(string fontPath)
            {
                for (int phraseIndex = 0; phraseIndex < DataCenterKoreanRoutePhrases.Length; phraseIndex++)
                {
                    VerifyNoPhraseOverlap(fontPath, DataCenterKoreanRoutePhrases[phraseIndex]);
                }
            }

            private static bool IsDataCenterStrictAsciiSpacingFont(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                return IsLobbyFontPath(normalized);
            }
        }
    }
}
