using System;
using System.Collections.Generic;
using System.Globalization;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private bool TryMeasurePhraseLayout(
                CompositeArchive archive,
                string fontPath,
                string phrase,
                bool validateGlyphShape,
                out PhraseLayoutResult result,
                out string error)
            {
                result = new PhraseLayoutResult();
                error = null;
                try
                {
                    byte[] fdt = archive.ReadFile(fontPath);
                    HashSet<long> occupiedPixels = new HashSet<long>();
                    int cursor = 0;
                    int glyphs = 0;
                    int overlap = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);

                        if (codepoint <= 0x20)
                        {
                            cursor += 8;
                            continue;
                        }

                        FdtGlyphEntry glyph;
                        if (!TryFindGlyph(fdt, codepoint, out glyph))
                        {
                            error = "missing U+" + codepoint.ToString("X4");
                            return false;
                        }

                        GlyphCanvas canvas = RenderGlyph(archive, fontPath, codepoint);
                        if (validateGlyphShape)
                        {
                            int minimumVisiblePixels = IsHangulCodepoint(codepoint) ? 10 : 1;
                            if (canvas.VisiblePixels < minimumVisiblePixels)
                            {
                                error = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "U+{0:X4} visible={1}, expected at least {2}",
                                    codepoint,
                                    canvas.VisiblePixels,
                                    minimumVisiblePixels);
                                return false;
                            }

                            if (IsHangulCodepoint(codepoint) &&
                                (GlyphMatchesFallback(fontPath, canvas, codepoint, '-') ||
                                 GlyphMatchesFallback(fontPath, canvas, codepoint, '=')))
                            {
                                error = "U+" + codepoint.ToString("X4") + " matches fallback glyph";
                                return false;
                            }
                        }

                        overlap += AddGlyphPixelsToLayout(occupiedPixels, cursor, canvas.Alpha);
                        cursor += Math.Max(1, glyph.Width + glyph.OffsetX);
                        glyphs++;
                    }

                    result.Glyphs = glyphs;
                    result.Width = cursor;
                    result.OverlapPixels = overlap;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static int AddGlyphPixelsToLayout(HashSet<long> occupiedPixels, int cursor, byte[] alpha)
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

                        int pixelX = cursor + x - 32;
                        int pixelY = y - 32;
                        long key = ((long)pixelY << 32) ^ (uint)pixelX;
                        if (!occupiedPixels.Add(key))
                        {
                            overlap++;
                        }
                    }
                }

                return overlap;
            }
        }

        private struct PhraseLayoutResult
        {
            public int Glyphs;
            public int Width;
            public int OverlapPixels;
        }
    }
}
