using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed class PhraseLayoutAccumulator
        {
            private readonly HashSet<long> _occupiedPixels = new HashSet<long>();
            private int _cursor;
            private int _glyphs;
            private int _overlapPixels;

            public void AddSpace()
            {
                _cursor += PhraseLayoutSpaceAdvance;
            }

            public void AddKerning(int adjustment)
            {
                _cursor += adjustment;
            }

            public void AddGlyph(PhraseGlyphMeasurement glyph)
            {
                _overlapPixels += AddGlyphPixels(glyph.Alpha);
                _cursor += glyph.Advance;
                _glyphs++;
            }

            public PhraseLayoutResult ToResult()
            {
                PhraseLayoutResult result = new PhraseLayoutResult();
                result.Glyphs = _glyphs;
                result.Width = _cursor;
                result.OverlapPixels = _overlapPixels;
                return result;
            }

            private int AddGlyphPixels(byte[] alpha)
            {
                int overlap = 0;
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    int rowOffset = y * GlyphCanvasSize;
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        if (alpha[rowOffset + x] == 0)
                        {
                            continue;
                        }

                        int pixelX = _cursor + x - 32;
                        int pixelY = y - 32;
                        long key = ((long)pixelY << 32) ^ (uint)pixelX;
                        if (!_occupiedPixels.Add(key))
                        {
                            overlap++;
                        }
                    }
                }

                return overlap;
            }
        }
    }
}
