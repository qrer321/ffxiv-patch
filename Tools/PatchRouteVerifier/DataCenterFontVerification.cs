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

            private void VerifyStartScreenSystemSettingsUldRoutes()
            {
                Console.WriteLine("[ULD/FDT] start-screen system settings font routes");
                int found = 0;
                HashSet<string> checkedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int candidateIndex = 0; candidateIndex < StartScreenSystemSettingsUldCandidates.Length; candidateIndex++)
                {
                    UldRouteCandidate candidate = StartScreenSystemSettingsUldCandidates[candidateIndex];
                    HashSet<string> routedFontPaths;
                    if (!TryCollectOptionalPreservedUldFontRoutes(
                        candidate.Path,
                        "start-screen system settings",
                        candidate.UsesLobbyFonts,
                        out routedFontPaths))
                    {
                        continue;
                    }

                    found++;
                    foreach (string fontPath in routedFontPaths)
                    {
                        if (!checkedRoutes.Add(fontPath))
                        {
                            continue;
                        }

                        for (int phraseIndex = 0; phraseIndex < SystemSettingsScaledPhrases.Length; phraseIndex++)
                        {
                            VerifyNoPhraseOverlap(fontPath, SystemSettingsScaledPhrases[phraseIndex]);
                        }
                    }
                }

                if (found == 0)
                {
                    Fail("No start-screen system settings ULD candidate was found; verifier is not covering the reported start-screen scaling route");
                }
                else
                {
                    Pass("start-screen system settings ULD candidates found: {0}", found);
                }
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
