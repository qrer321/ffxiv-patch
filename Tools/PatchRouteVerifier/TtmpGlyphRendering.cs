using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private GlyphCanvas RenderGlyph(TtmpFontPackage package, string fdtPath, uint codepoint)
            {
                byte[] fdt = package.ReadFile(fdtPath);
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

                Texture texture = ReadFontTexture(package, texturePath);
                byte[] alpha = RenderGlyphAlpha(texture, glyph);

                return new GlyphCanvas
                {
                    Alpha = alpha,
                    VisiblePixels = CountVisiblePixels(alpha),
                    Glyph = glyph,
                    TexturePath = texturePath
                };
            }

            private Texture ReadFontTexture(TtmpFontPackage package, string texturePath)
            {
                string cacheKey = package.CacheKey + "|" + texturePath;
                Texture texture;
                if (!_textureCache.TryGetValue(cacheKey, out texture))
                {
                    byte[] packed;
                    if (!package.TryReadPackedFile(texturePath, out packed))
                    {
                        throw new FileNotFoundException("TTMP texture was not found", texturePath);
                    }

                    texture = ReadA4R4G4B4Texture(UnpackTextureFile(packed));
                    _textureCache.Add(cacheKey, texture);
                }

                return texture;
            }
        }
    }
}
