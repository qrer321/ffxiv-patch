namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
                    new VerificationStep("hangul-source-preservation", VerifyHangulSourcePreservation),
                    new VerificationStep("party-list-self-marker", VerifyPartyListSelfMarker),
                    new VerificationStep("lobby-hangul-visibility", VerifyLobbyHangulVisibility),
                    new VerificationStep("lobby-phrase-glyph-diagnostics", VerifyLobbyPhraseGlyphDiagnostics),
                    new VerificationStep("dialogue-phrase-glyph-diagnostics", VerifyDialoguePhraseGlyphDiagnostics)
                };
            }
        }
    }
}
