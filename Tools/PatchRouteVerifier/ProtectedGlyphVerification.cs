using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
                    ExpectGlyphEqualIfSourceExists(_cleanFont, PartyListSelfMarkerSameFontChecks[i], codepoint, _patchedFont, PartyListSelfMarkerSameFontChecks[i], codepoint);
                }

                for (int i = 0; i < PartyListSelfMarkerKoreanFontChecks.GetLength(0); i++)
                {
                    ExpectGlyphEqualIfSourceExists(_cleanFont, PartyListSelfMarkerKoreanFontChecks[i, 0], codepoint, _patchedFont, PartyListSelfMarkerKoreanFontChecks[i, 1], codepoint);
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
