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
