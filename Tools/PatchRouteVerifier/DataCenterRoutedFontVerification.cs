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
                    DumpLabelPreview(dumpGroup, fontPath, DataCenterWorldmapLabels);
                }
            }
        }
    }
}
