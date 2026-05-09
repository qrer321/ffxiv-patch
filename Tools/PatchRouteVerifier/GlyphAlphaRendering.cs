using System.Globalization;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
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
                    int pixelOffset = GetTexturePixelOffset(texture, glyph.ImageIndex, sourceX, sourceY);
                    canvas[dy * GlyphCanvasSize + dx] = ReadFontTextureAlpha(texture.Data, pixelOffset, channel);
                }
            }

            return canvas;
        }

        private static int GetTexturePixelOffset(Texture texture, int imageIndex, int sourceX, int sourceY)
        {
            if (sourceX < 0 || sourceY < 0 || sourceX >= texture.Width || sourceY >= texture.Height)
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "glyph source outside texture: image={0}, source=({1},{2}), texture={3}x{4}",
                        imageIndex,
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
                        imageIndex,
                        pixelOffset,
                        texture.Data.Length));
            }

            return pixelOffset;
        }

        private static byte ReadFontTextureAlpha(byte[] textureData, int pixelOffset, int channel)
        {
            byte lo = textureData[pixelOffset];
            byte hi = textureData[pixelOffset + 1];
            byte nibble;
            switch (channel)
            {
                case 0: nibble = (byte)(hi & 0x0F); break;
                case 1: nibble = (byte)((lo >> 4) & 0x0F); break;
                case 2: nibble = (byte)(lo & 0x0F); break;
                default: nibble = (byte)((hi >> 4) & 0x0F); break;
            }

            return (byte)(nibble * 17);
        }

        private static int CountVisiblePixels(byte[] alpha)
        {
            int visible = 0;
            for (int i = 0; i < alpha.Length; i++)
            {
                if (alpha[i] != 0)
                {
                    visible++;
                }
            }

            return visible;
        }
    }
}
