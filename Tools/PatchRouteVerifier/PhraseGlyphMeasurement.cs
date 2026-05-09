using System;
using System.Globalization;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private bool TryMeasurePhraseGlyph(
                CompositeArchive archive,
                string fontPath,
                byte[] fdt,
                uint codepoint,
                bool validateGlyphShape,
                out PhraseGlyphMeasurement measurement,
                out string error)
            {
                measurement = new PhraseGlyphMeasurement();
                error = null;

                FdtGlyphEntry glyph;
                if (!TryFindGlyph(fdt, codepoint, out glyph))
                {
                    error = "missing U+" + codepoint.ToString("X4");
                    return false;
                }

                GlyphCanvas canvas = RenderGlyph(archive, fontPath, codepoint);
                if (validateGlyphShape && !TryValidatePhraseGlyphShape(fontPath, codepoint, canvas, out error))
                {
                    return false;
                }

                measurement = new PhraseGlyphMeasurement(Math.Max(1, glyph.Width + glyph.OffsetX), canvas.Alpha);
                return true;
            }

            private bool TryValidatePhraseGlyphShape(string fontPath, uint codepoint, GlyphCanvas canvas, out string error)
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

                error = null;
                return true;
            }
        }

        private struct PhraseGlyphMeasurement
        {
            public readonly int Advance;
            public readonly byte[] Alpha;

            public PhraseGlyphMeasurement(int advance, byte[] alpha)
            {
                Advance = advance;
                Alpha = alpha;
            }
        }
    }
}
