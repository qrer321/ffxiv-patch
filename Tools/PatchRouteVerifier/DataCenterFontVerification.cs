using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyDataCenterTitleUldRoute()
            {
                VerifyUldFontPreservation(DataCenterTitleUldPath, "data-center title", "data-center-title");
            }

            private void VerifyDataCenterWorldmapUldRoute()
            {
                VerifyUldFontPreservation(DataCenterWorldmapUldPath, "data-center world map", "data-center-worldmap");
            }

            private void VerifyUldFontPreservation(string uldPath, string label, string dumpGroup)
            {
                Console.WriteLine("[ULD/FDT] {0} font preservation", label);
                HashSet<string> routedFontPaths;
                if (TryCollectPreservedUldFontRoutes(uldPath, label, out routedFontPaths))
                {
                    VerifyDataCenterRoutedFontLabels(dumpGroup, routedFontPaths);
                }
            }
        }
    }
}
