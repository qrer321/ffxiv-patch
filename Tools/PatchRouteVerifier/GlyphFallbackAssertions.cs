using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void ExpectGlyphNotEqualToFallback(string fdtPath, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return;
                }

                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fdtPath);
                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        Fail("{0} U+{1:X4} is missing", fdtPath, codepoint);
                        return;
                    }

                    if (!TryFindGlyph(fdt, fallbackCodepoint, out ignored))
                    {
                        Warn("{0} U+{1:X4} fallback comparison skipped; missing U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fdtPath, codepoint);
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fdtPath, fallbackCodepoint);
                    long score = Diff(glyph.Alpha, fallback.Alpha);
                    if (score != 0 && glyph.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} is not fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    Fail("{0} U+{1:X4} matches fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} fallback comparison error: {2}", fdtPath, codepoint, ex.Message);
                }
            }
        }
    }
}
