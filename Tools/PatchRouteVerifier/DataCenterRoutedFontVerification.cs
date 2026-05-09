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
                    VerifyDataCenterRoutedAsciiTexturePadding(fontPath);
                    VerifyDataCenterRoutedKoreanPhraseLayouts(fontPath);
                    DumpLabelPreview(dumpGroup, fontPath, DataCenterWorldmapLabels);
                }
            }

            private void VerifyDataCenterRoutedAsciiPhraseMetrics(string fontPath)
            {
                for (int phraseIndex = 0; phraseIndex < DataCenterWorldmapLabels.Length; phraseIndex++)
                {
                    VerifyPhraseMetricsMatchClean(fontPath, fontPath, DataCenterWorldmapLabels[phraseIndex]);
                }
            }

            private void VerifyDataCenterRoutedAsciiPhrasePixels(string fontPath)
            {
                for (int phraseIndex = 0; phraseIndex < DataCenterWorldmapLabels.Length; phraseIndex++)
                {
                    VerifyPhrasePixelsMatchClean(fontPath, fontPath, DataCenterWorldmapLabels[phraseIndex]);
                }
            }

            private void VerifyDataCenterRoutedAsciiPhraseVisualSpacing(string fontPath)
            {
                for (int phraseIndex = 0; phraseIndex < DataCenterCriticalRenderLabels.Length; phraseIndex++)
                {
                    VerifyPhraseMinimumVisualGap(fontPath, DataCenterCriticalRenderLabels[phraseIndex]);
                }
            }

            private void VerifyDataCenterRoutedAsciiTexturePadding(string fontPath)
            {
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
                    if (!VerifyGlyphTextureNeighborhoodMatchesClean(fontPath, fontPath, codepoint, DataCenterGlyphTexturePadding, out error))
                    {
                        Fail(
                            "{0} U+{1:X4} texture padding differs from clean route: {2}",
                            fontPath,
                            codepoint,
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
        }
    }
}
