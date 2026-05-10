using System;
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
            private int _minimumGapPixels = int.MaxValue;
            private int _maximumGapPixels = int.MinValue;
            private int _requiredMinimumGapPixels;
            private int _minimumRequiredGapDeficitPixels = int.MaxValue;
            private int _minimumRequiredGapActualPixels;
            private int _minimumRequiredGapPixels;
            private bool _hasPreviousGlyphBounds;
            private int _previousGlyphMaxX;
            private uint _previousGlyphCodepoint;
            private int _previousGlyphRequiredGapPixels;
            private uint _minimumGapLeftCodepoint;
            private uint _minimumGapRightCodepoint;
            private uint _maximumGapLeftCodepoint;
            private uint _maximumGapRightCodepoint;
            private uint _minimumRequiredGapLeftCodepoint;
            private uint _minimumRequiredGapRightCodepoint;

            public void AddSpace()
            {
                _cursor += PhraseLayoutSpaceAdvance;
                _hasPreviousGlyphBounds = false;
                _previousGlyphRequiredGapPixels = 0;
            }

            public void AddKerning(int adjustment)
            {
                _cursor += adjustment;
            }

            public void AddGlyph(PhraseGlyphMeasurement glyph)
            {
                _overlapPixels += AddGlyphPixels(glyph.Alpha);
                AddGlyphBounds(glyph);
                _cursor += glyph.Advance;
                _glyphs++;
            }

            public PhraseLayoutResult ToResult()
            {
                PhraseLayoutResult result = new PhraseLayoutResult();
                result.Glyphs = _glyphs;
                result.Width = _cursor;
                result.OverlapPixels = _overlapPixels;
                result.MinimumGapPixels = _minimumGapPixels == int.MaxValue ? 0 : _minimumGapPixels;
                result.MaximumGapPixels = _maximumGapPixels == int.MinValue ? 0 : _maximumGapPixels;
                result.RequiredMinimumGapPixels = _requiredMinimumGapPixels;
                result.MinimumGapLeftCodepoint = _minimumGapLeftCodepoint;
                result.MinimumGapRightCodepoint = _minimumGapRightCodepoint;
                result.MaximumGapLeftCodepoint = _maximumGapLeftCodepoint;
                result.MaximumGapRightCodepoint = _maximumGapRightCodepoint;
                result.MinimumRequiredGapActualPixels = _minimumRequiredGapDeficitPixels == int.MaxValue ? 0 : _minimumRequiredGapActualPixels;
                result.MinimumRequiredGapPixels = _minimumRequiredGapPixels;
                result.MinimumRequiredGapLeftCodepoint = _minimumRequiredGapLeftCodepoint;
                result.MinimumRequiredGapRightCodepoint = _minimumRequiredGapRightCodepoint;
                return result;
            }

            private void AddGlyphBounds(PhraseGlyphMeasurement glyph)
            {
                if (glyph.MinX > glyph.MaxX)
                {
                    return;
                }

                int minX = _cursor + glyph.MinX - 32;
                int maxX = _cursor + glyph.MaxX - 32;
                int requiredGap = ComputeRequiredMinimumGap(glyph.Height);
                if (requiredGap > _requiredMinimumGapPixels)
                {
                    _requiredMinimumGapPixels = requiredGap;
                }

                if (_hasPreviousGlyphBounds)
                {
                    int gap = minX - _previousGlyphMaxX - 1;
                    if (gap < _minimumGapPixels)
                    {
                        _minimumGapPixels = gap;
                        _minimumGapLeftCodepoint = _previousGlyphCodepoint;
                        _minimumGapRightCodepoint = glyph.Codepoint;
                    }

                    if (gap > _maximumGapPixels)
                    {
                        _maximumGapPixels = gap;
                        _maximumGapLeftCodepoint = _previousGlyphCodepoint;
                        _maximumGapRightCodepoint = glyph.Codepoint;
                    }

                    int pairRequiredGap = Math.Max(_previousGlyphRequiredGapPixels, requiredGap);
                    int deficit = gap - pairRequiredGap;
                    if (deficit < _minimumRequiredGapDeficitPixels)
                    {
                        _minimumRequiredGapDeficitPixels = deficit;
                        _minimumRequiredGapActualPixels = gap;
                        _minimumRequiredGapPixels = pairRequiredGap;
                        _minimumRequiredGapLeftCodepoint = _previousGlyphCodepoint;
                        _minimumRequiredGapRightCodepoint = glyph.Codepoint;
                    }
                }

                _previousGlyphMaxX = maxX;
                _previousGlyphCodepoint = glyph.Codepoint;
                _previousGlyphRequiredGapPixels = requiredGap;
                _hasPreviousGlyphBounds = true;
            }

            private static int ComputeRequiredMinimumGap(int glyphHeight)
            {
                return Math.Max(2, (Math.Max(0, glyphHeight) + 13) / 14 + 1);
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
