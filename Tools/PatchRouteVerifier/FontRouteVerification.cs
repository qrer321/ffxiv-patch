using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyDataCenterTitleGlyphs()
            {
                Console.WriteLine("[FDT] Data center title ASCII glyphs");
                uint[] codepoints = CreateAsciiCodepoints();
                for (int f = 0; f < DataCenterTitleSameFontChecks.Length; f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, DataCenterTitleSameFontChecks[f], codepoints[c], _patchedFont, DataCenterTitleSameFontChecks[f], codepoints[c]);
                    }
                }

                for (int f = 0; f < DataCenterTitleKoreanFontChecks.GetLength(0); f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, DataCenterTitleKoreanFontChecks[f, 0], codepoints[c], _patchedFont, DataCenterTitleKoreanFontChecks[f, 1], codepoints[c]);
                    }
                }
            }
        }
    }
}
