using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void Verify4kLobbyFontDerivations()
            {
                Console.WriteLine("[FDT] 4K lobby font derivations");

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string targetFontPath = Derived4kLobbyFontPairs[i, 0];
                    string sourceFontPath = Derived4kLobbyFontPairs[i, 1];

                    for (int latinIndex = 0; latinIndex < FourKLobbyLatinCodepoints.Length; latinIndex++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, targetFontPath, FourKLobbyLatinCodepoints[latinIndex], _patchedFont, targetFontPath, FourKLobbyLatinCodepoints[latinIndex]);
                    }

                    byte[] sourceFdt;
                    try
                    {
                        sourceFdt = _patchedFont.ReadFile(sourceFontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for 4K lobby derivation check: {1}", sourceFontPath, ex.Message);
                        continue;
                    }

                    bool checkedHangul = false;
                    int checkedHangulCount = 0;
                    for (int codepointIndex = 0; codepointIndex < Derived4kLobbyRequiredHangulCodepoints.Length; codepointIndex++)
                    {
                        uint codepoint = Derived4kLobbyRequiredHangulCodepoints[codepointIndex];
                        FdtGlyphEntry ignored;
                        if (!TryFindGlyph(sourceFdt, codepoint, out ignored))
                        {
                            Fail("{0} is missing required 4K lobby source glyph U+{1:X4}", sourceFontPath, codepoint);
                            continue;
                        }

                        checkedHangul = true;
                        ExpectGlyphVisibleAtLeast(_patchedFont, targetFontPath, codepoint, 10);
                        ExpectGlyphNotEqualToFallback(targetFontPath, codepoint, '-');
                        ExpectGlyphNotEqualToFallback(targetFontPath, codepoint, '=');
                        VerifyDerived4kGlyphMetrics(targetFontPath, codepoint);
                        checkedHangulCount++;
                    }

                    if (!checkedHangul)
                    {
                        Fail("{0} has no required Hangul glyphs for 4K lobby target {1}", sourceFontPath, targetFontPath);
                    }
                    else
                    {
                        Pass("{0} required 4K lobby Hangul glyphs checked: {1}", targetFontPath, checkedHangulCount);
                    }
                }
            }

            private void VerifyDerived4kGlyphMetrics(string fontPath, uint codepoint)
            {
                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fontPath);
                    FdtGlyphEntry glyph;
                    if (!TryFindGlyph(fdt, codepoint, out glyph))
                    {
                        Fail("{0} U+{1:X4} is missing", fontPath, codepoint);
                        return;
                    }

                    if (glyph.OffsetX < 0)
                    {
                        Fail("{0} U+{1:X4} has negative advance adjustment {2}", fontPath, codepoint, glyph.OffsetX);
                        return;
                    }

                    Pass("{0} U+{1:X4} advance adjustment={2}", fontPath, codepoint, glyph.OffsetX);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} metric check error: {2}", fontPath, codepoint, ex.Message);
                }
            }

            private void VerifyNumericGlyphs()
            {
                Console.WriteLine("[FDT] Numeric glyphs used by duty/reward UI");
                for (int f = 0; f < NumericGlyphSameFontChecks.Length; f++)
                {
                    for (int c = 0; c < NumericGlyphCodepoints.Length; c++)
                    {
                        ExpectGlyphEqual(_cleanFont, NumericGlyphSameFontChecks[f], NumericGlyphCodepoints[c], _patchedFont, NumericGlyphSameFontChecks[f], NumericGlyphCodepoints[c]);
                    }
                }

                for (int f = 0; f < NumericGlyphKoreanFontChecks.GetLength(0); f++)
                {
                    for (int c = 0; c < NumericGlyphCodepoints.Length; c++)
                    {
                        ExpectGlyphEqual(_cleanFont, NumericGlyphKoreanFontChecks[f, 0], NumericGlyphCodepoints[c], _patchedFont, NumericGlyphKoreanFontChecks[f, 1], NumericGlyphCodepoints[c]);
                    }
                }
            }

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
