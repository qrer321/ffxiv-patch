using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
        }
    }
}
