using System;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int GlyphCanvasSize = 96;

        private sealed partial class Verifier
        {
            private GlyphCanvas RenderGlyph(CompositeArchive archive, string fdtPath, uint codepoint)
            {
                byte[] fdt = archive.ReadFile(fdtPath);
                FdtGlyphEntry glyph;
                if (!TryFindGlyph(fdt, codepoint, out glyph))
                {
                    throw new InvalidDataException(fdtPath + " is missing U+" + codepoint.ToString("X4"));
                }

                string texturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    throw new InvalidDataException("Could not resolve texture for " + fdtPath);
                }

                Texture texture = ReadFontTexture(archive, texturePath);
                byte[] alpha = RenderGlyphAlpha(texture, glyph);

                return new GlyphCanvas
                {
                    Alpha = alpha,
                    VisiblePixels = CountVisiblePixels(alpha),
                    Glyph = glyph,
                    TexturePath = texturePath
                };
            }
        }
    }
}
