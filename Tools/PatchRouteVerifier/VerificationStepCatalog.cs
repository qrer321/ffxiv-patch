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
                    new VerificationStep("applied-output-files", VerifyAppliedOutputFiles),
                    new VerificationStep("lobby-route-survey", VerifyLobbyRouteSurvey),
                    new VerificationStep("lobby-atlas-capacity", VerifyLobbyAtlasCapacity),
                    new VerificationStep("lobby-multitexture-font-set", VerifyLobbyMultiTextureFontSet),
                    new VerificationStep("lobby-source-cell-conflicts", VerifyLobbySourceCellConflicts),
                    new VerificationStep("lobby-hangul-source-cells", VerifyLobbyHangulSourceCells),
                    new VerificationStep("lobby-ttmp-ascii-delta", VerifyLobbyTtmpAsciiDelta),
                    new VerificationStep("data-center-rows", VerifyDataCenterRows),
                    new VerificationStep("data-center-language-slots", VerifyDataCenterRowsAllGlobalLanguageSlots),
                    new VerificationStep("data-center-title-uld", VerifyDataCenterTitleUldRoute),
                    new VerificationStep("data-center-worldmap-uld", VerifyDataCenterWorldmapUldRoute),
                    new VerificationStep("start-system-settings-uld", VerifyStartScreenSystemSettingsUldRoutes),
                    new VerificationStep("compact-time", VerifyCompactTimeRows),
                    new VerificationStep("world-visit", VerifyWorldVisitRows),
                    new VerificationStep("configuration-sharing", VerifyConfigurationSharingRows),
                    new VerificationStep("bozja-entrance", VerifyBozjaEntranceRows),
                    new VerificationStep("occult-crescent-support-jobs", VerifyOccultCrescentSupportJobRows),
                    new VerificationStep("data-center-title-glyphs", VerifyDataCenterTitleGlyphs),
                    new VerificationStep("clean-ascii-font-routes", VerifyCleanAsciiFontRoutes),
                    new VerificationStep("high-scale-ascii-phrase-layouts", VerifyHighScaleAsciiPhraseLayouts),
                    new VerificationStep("system-settings-scaled-phrase-layouts", VerifySystemSettingsScaledPhraseLayouts),
                    new VerificationStep("system-settings-mixed-scale-layouts", VerifySystemSettingsMixedScalePhraseLayouts),
                    new VerificationStep("start-main-menu-phrase-layouts", VerifyStartScreenMainMenuPhraseLayouts),
                    new VerificationStep("lobby-scale-font-sources", VerifyLobbyScaleFontSourceRoutes),
                    new VerificationStep("korean-lobby-font-sources", VerifyKoreanLobbyFontSourceRoutes),
                    new VerificationStep("lobby-ttmp-payloads", VerifyLobbyTtmpPayloads),
                    new VerificationStep("lobby-clean-payloads", VerifyLobbyCleanPayloads),
                    new VerificationStep("4k-lobby-font-derivations", Verify4kLobbyFontDerivations),
                    new VerificationStep("4k-lobby-phrase-layouts", Verify4kLobbyPhraseLayouts),
                    new VerificationStep("numeric-glyphs", VerifyNumericGlyphs),
                    new VerificationStep("protected-hangul-glyphs", VerifyProtectedHangulGlyphs),
                    new VerificationStep("hangul-source-preservation", VerifyHangulSourcePreservation),
                    new VerificationStep("reported-ingame-hangul-phrases", VerifyReportedInGameHangulPhraseSourcePreservation),
                    new VerificationStep("action-detail-scale-layouts", VerifyActionDetailScaleLayouts),
                    new VerificationStep("party-list-self-marker", VerifyPartyListSelfMarker),
                    new VerificationStep("lobby-hangul-visibility", VerifyLobbyHangulVisibility),
                    new VerificationStep("lobby-render-snapshots", VerifyLobbyRenderSnapshots),
                    new VerificationStep("lobby-phrase-glyph-diagnostics", VerifyLobbyPhraseGlyphDiagnostics),
                    new VerificationStep("dialogue-phrase-glyph-diagnostics", VerifyDialoguePhraseGlyphDiagnostics)
                };
            }
        }
    }
}
