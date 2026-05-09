using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void RunVerificationSteps()
            {
                VerificationStep[] steps = CreateVerificationSteps();
                HashSet<string> selected = CreateSelectedCheckSet(steps);
                if (selected != null)
                {
                    Console.WriteLine("  checks: {0}", string.Join(",", _selectedChecks));
                }

                for (int i = 0; i < steps.Length; i++)
                {
                    if (selected == null || selected.Contains(steps[i].Name))
                    {
                        steps[i].Run();
                    }
                }
            }

            private HashSet<string> CreateSelectedCheckSet(VerificationStep[] steps)
            {
                if (_selectedChecks == null || _selectedChecks.Length == 0)
                {
                    return null;
                }

                HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < steps.Length; i++)
                {
                    known.Add(steps[i].Name);
                }

                HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _selectedChecks.Length; i++)
                {
                    string check = _selectedChecks[i];
                    if (string.IsNullOrWhiteSpace(check))
                    {
                        continue;
                    }

                    if (!known.Contains(check))
                    {
                        throw new ArgumentException("unknown check: " + check);
                    }

                    selected.Add(check);
                }

                return selected;
            }

            private VerificationStep[] CreateVerificationSteps()
            {
                return new VerificationStep[]
                {
                    new VerificationStep("data-center-rows", VerifyDataCenterRows),
                    new VerificationStep("data-center-language-slots", VerifyDataCenterRowsAllGlobalLanguageSlots),
                    new VerificationStep("data-center-title-uld", VerifyDataCenterTitleUldRoute),
                    new VerificationStep("data-center-worldmap-uld", VerifyDataCenterWorldmapUldRoute),
                    new VerificationStep("compact-time", VerifyCompactTimeRows),
                    new VerificationStep("world-visit", VerifyWorldVisitRows),
                    new VerificationStep("configuration-sharing", VerifyConfigurationSharingRows),
                    new VerificationStep("bozja-entrance", VerifyBozjaEntranceRows),
                    new VerificationStep("occult-crescent-support-jobs", VerifyOccultCrescentSupportJobRows),
                    new VerificationStep("data-center-title-glyphs", VerifyDataCenterTitleGlyphs),
                    new VerificationStep("clean-ascii-font-routes", VerifyCleanAsciiFontRoutes),
                    new VerificationStep("high-scale-ascii-phrase-layouts", VerifyHighScaleAsciiPhraseLayouts),
                    new VerificationStep("system-settings-scaled-phrase-layouts", VerifySystemSettingsScaledPhraseLayouts),
                    new VerificationStep("4k-lobby-font-derivations", Verify4kLobbyFontDerivations),
                    new VerificationStep("4k-lobby-phrase-layouts", Verify4kLobbyPhraseLayouts),
                    new VerificationStep("numeric-glyphs", VerifyNumericGlyphs),
                    new VerificationStep("protected-hangul-glyphs", VerifyProtectedHangulGlyphs),
                    new VerificationStep("party-list-self-marker", VerifyPartyListSelfMarker),
                    new VerificationStep("lobby-hangul-visibility", VerifyLobbyHangulVisibility),
                    new VerificationStep("lobby-phrase-glyph-diagnostics", VerifyLobbyPhraseGlyphDiagnostics),
                    new VerificationStep("dialogue-phrase-glyph-diagnostics", VerifyDialoguePhraseGlyphDiagnostics)
                };
            }

            private sealed class VerificationStep
            {
                public readonly string Name;
                public readonly Action Run;

                public VerificationStep(string name, Action run)
                {
                    Name = name;
                    Run = run;
                }
            }
        }
    }
}
