using System;
using System.Globalization;
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

                string cacheKey = archive.CacheKey + "|" + texturePath;
                byte[] rawTexture;
                if (!_textureCache.TryGetValue(cacheKey, out rawTexture))
                {
                    byte[] packed;
                    if (!archive.TryReadPackedFile(texturePath, out packed))
                    {
                        throw new FileNotFoundException("texture was not found", texturePath);
                    }

                    rawTexture = UnpackTextureFile(packed);
                    _textureCache.Add(cacheKey, rawTexture);
                }

                Texture texture = ReadA4R4G4B4Texture(rawTexture);
                byte[] alpha = RenderGlyphAlpha(texture, glyph);
                int visible = 0;
                for (int i = 0; i < alpha.Length; i++)
                {
                    if (alpha[i] != 0)
                    {
                        visible++;
                    }
                }

                return new GlyphCanvas
                {
                    Alpha = alpha,
                    VisiblePixels = visible,
                    Glyph = glyph,
                    TexturePath = texturePath
                };
            }
        }

        private static byte[] RenderGlyphAlpha(Texture texture, FdtGlyphEntry glyph)
        {
            byte[] canvas = new byte[GlyphCanvasSize * GlyphCanvasSize];
            int channel = glyph.ImageIndex % 4;
            int startX = 32 + glyph.OffsetX;
            int startY = 32 + glyph.OffsetY;
            for (int y = 0; y < glyph.Height; y++)
            {
                int dy = startY + y;
                for (int x = 0; x < glyph.Width; x++)
                {
                    int dx = startX + x;
                    if (dx < 0 || dy < 0 || dx >= GlyphCanvasSize || dy >= GlyphCanvasSize)
                    {
                        continue;
                    }

                    int sourceX = glyph.X + x;
                    int sourceY = glyph.Y + y;
                    if (sourceX < 0 || sourceY < 0 || sourceX >= texture.Width || sourceY >= texture.Height)
                    {
                        throw new InvalidDataException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "glyph source outside texture: image={0}, source=({1},{2}), texture={3}x{4}",
                                glyph.ImageIndex,
                                sourceX,
                                sourceY,
                                texture.Width,
                                texture.Height));
                    }

                    int pixelOffset = (sourceY * texture.Width + sourceX) * 2;
                    if (pixelOffset < 0 || pixelOffset + 1 >= texture.Data.Length)
                    {
                        throw new InvalidDataException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "glyph source outside texture data: image={0}, offset={1}, bytes={2}",
                                glyph.ImageIndex,
                                pixelOffset,
                                texture.Data.Length));
                    }

                    byte lo = texture.Data[pixelOffset];
                    byte hi = texture.Data[pixelOffset + 1];
                    byte nibble;
                    switch (channel)
                    {
                        case 0: nibble = (byte)(hi & 0x0F); break;
                        case 1: nibble = (byte)((lo >> 4) & 0x0F); break;
                        case 2: nibble = (byte)(lo & 0x0F); break;
                        default: nibble = (byte)((hi >> 4) & 0x0F); break;
                    }

                    canvas[dy * GlyphCanvasSize + dx] = (byte)(nibble * 17);
                }
            }

            return canvas;
        }

        private struct GlyphCanvas
        {
            public byte[] Alpha;
            public int VisiblePixels;
            public FdtGlyphEntry Glyph;
            public string TexturePath;
        }
    }
}
