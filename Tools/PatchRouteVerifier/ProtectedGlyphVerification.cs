using System;
using System.Collections.Generic;

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

                for (int i = 0; i < PartyListSelfMarkerSameFontChecks.Length; i++)
                {
                    VerifyPartyListSelfMarkerGlyphRouteSet(PartyListSelfMarkerSameFontChecks[i], PartyListSelfMarkerSameFontChecks[i]);
                }

                for (int i = 0; i < PartyListSelfMarkerKoreanFontChecks.GetLength(0); i++)
                {
                    VerifyPartyListSelfMarkerGlyphRouteSet(
                        PartyListSelfMarkerKoreanFontChecks[i, 0],
                        PartyListSelfMarkerKoreanFontChecks[i, 1]);
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

            private void VerifyPartyListSelfMarkerGlyphRouteSet(string sourceFontPath, string targetFontPath)
            {
                uint[] codepoints = CollectProtectedPuaGlyphsForRoute(sourceFontPath, targetFontPath);
                Pass(
                    "{0} -> {1} protected PUA glyph candidates={2}",
                    sourceFontPath,
                    targetFontPath,
                    codepoints.Length);
                for (int i = 0; i < codepoints.Length; i++)
                {
                    VerifyPartyListSelfMarkerGlyphRoute(sourceFontPath, targetFontPath, codepoints[i]);
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

            private uint[] CollectProtectedPuaGlyphsForRoute(string sourceFontPath, string targetFontPath)
            {
                byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                HashSet<uint> sourcePua = CollectPrivateUseCodepoints(sourceFdt);
                HashSet<uint> targetPua = CollectPrivateUseCodepoints(targetFdt);
                SortedSet<uint> codepoints = new SortedSet<uint>();

                for (int i = 0; i < PartyListProtectedPuaGlyphSeeds.Length; i++)
                {
                    uint codepoint = PartyListProtectedPuaGlyphSeeds[i];
                    if (sourcePua.Contains(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }

                foreach (uint codepoint in targetPua)
                {
                    if (sourcePua.Contains(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }

                uint[] result = new uint[codepoints.Count];
                codepoints.CopyTo(result);
                return result;
            }

            private static HashSet<uint> CollectPrivateUseCodepoints(byte[] fdt)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return codepoints;
                }

                for (int i = 0; i < glyphCount; i++)
                {
                    int offset = glyphStart + i * FdtGlyphEntrySize;
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(FfxivKoreanPatch.FFXIVPatchGenerator.Endian.ReadUInt32LE(fdt, offset), out codepoint) ||
                        !IsPrivateUseCodepoint(codepoint))
                    {
                        continue;
                    }

                    codepoints.Add(codepoint);
                }

                return codepoints;
            }

            private static bool IsPrivateUseCodepoint(uint codepoint)
            {
                return (codepoint >= 0xE000u && codepoint <= 0xF8FFu) ||
                       (codepoint >= 0xF0000u && codepoint <= 0xFFFFDu) ||
                       (codepoint >= 0x100000u && codepoint <= 0x10FFFDu);
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
