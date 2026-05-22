using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int PartyListSelfMarkerTexturePadding = 8;

            private void VerifyPartyListSelfMarker()
            {
                Console.WriteLine("[EXD/FDT] Party-list self marker");
                byte[] addonText = GetFirstStringBytes(_patchedText, "Addon", 10952, _language);
                ExpectBytes("Addon 10952", addonText, new byte[] { 0xEE, 0x80, 0xB1 });

                for (int i = 0; i < PartyListProtectedPuaGlyphs.Length; i++)
                {
                    VerifyPartyListSelfMarkerGlyph(PartyListProtectedPuaGlyphs[i]);
                }
            }

            private void VerifyProtectedHangulGlyphs()
            {
                Console.WriteLine("[FDT] Protected Hangul glyphs");
                for (int f = 0; f < ProtectedHangulFonts.Length; f++)
                {
                    for (int c = 0; c < ProtectedHangulCodepoints.Length; c++)
                    {
                        ExpectGlyphVisibleAtLeast(_patchedFont, ProtectedHangulFonts[f], ProtectedHangulCodepoints[c], ProtectedHangulMinimumVisiblePixels);
                        ExpectGlyphNotEqualToFallback(ProtectedHangulFonts[f], ProtectedHangulCodepoints[c], '-');
                        ExpectGlyphNotEqualToFallback(ProtectedHangulFonts[f], ProtectedHangulCodepoints[c], '=');
                    }
                }
            }

            private void VerifyPartyListSelfMarkerGlyph(uint codepoint)
            {
                for (int i = 0; i < PartyListSelfMarkerSameFontChecks.Length; i++)
                {
                    VerifyPartyListSelfMarkerGlyphRoute(PartyListSelfMarkerSameFontChecks[i], PartyListSelfMarkerSameFontChecks[i], codepoint);
                }

                for (int i = 0; i < PartyListSelfMarkerKoreanFontChecks.GetLength(0); i++)
                {
                    VerifyPartyListSelfMarkerGlyphRoute(PartyListSelfMarkerKoreanFontChecks[i, 0], PartyListSelfMarkerKoreanFontChecks[i, 1], codepoint);
                }
            }

            private void VerifyPartyListSelfMarkerGlyphRoute(string sourceFontPath, string targetFontPath, uint codepoint)
            {
                byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                FdtGlyphEntry ignored;
                if (!TryFindGlyph(sourceFdt, codepoint, out ignored))
                {
                    return;
                }

                ExpectGlyphEqual(_cleanFont, sourceFontPath, codepoint, _patchedFont, targetFontPath, codepoint);

                string error;
                if (!VerifyGlyphTextureNeighborhoodMatchesClean(
                    sourceFontPath,
                    targetFontPath,
                    codepoint,
                    PartyListSelfMarkerTexturePadding,
                    out error))
                {
                    Fail(
                        "{0} U+{1:X4} -> {2} party-list marker base texture neighborhood differs: {3}",
                        sourceFontPath,
                        codepoint,
                        targetFontPath,
                        error);
                }
                else
                {
                    Pass(
                        "{0} U+{1:X4} -> {2} party-list marker base texture neighborhood matches clean",
                        sourceFontPath,
                        codepoint,
                        targetFontPath);
                }

                if (!VerifyGlyphTextureMipNeighborhoodsMatchClean(
                    sourceFontPath,
                    targetFontPath,
                    codepoint,
                    PartyListSelfMarkerTexturePadding,
                    out error))
                {
                    Fail(
                        "{0} U+{1:X4} -> {2} party-list marker mip texture neighborhood differs: {3}",
                        sourceFontPath,
                        codepoint,
                        targetFontPath,
                        error);
                }
                else
                {
                    Pass(
                        "{0} U+{1:X4} -> {2} party-list marker mip texture neighborhood matches clean",
                        sourceFontPath,
                        codepoint,
                        targetFontPath);
                }
            }

            private void VerifyLobbyHangulVisibility()
            {
                Console.WriteLine("[FDT] Lobby Hangul visibility guard");
                for (int f = 0; f < LobbyHangulVisibilityFonts.Length; f++)
                {
                    for (int c = 0; c < LobbyHangulVisibilityCodepoints.Length; c++)
                    {
                        ExpectGlyphVisible(_patchedFont, LobbyHangulVisibilityFonts[f], LobbyHangulVisibilityCodepoints[c]);
                    }
                }
            }
        }
    }
}
