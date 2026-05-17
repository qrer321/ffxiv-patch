using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int DataCenterGlyphTexturePadding = 4;
        private const int MaxTexturePaddingFailuresPerFont = 20;

        private sealed partial class Verifier
        {
            private bool VerifyGlyphTextureNeighborhoodMatchesClean(
                string sourceFontPath,
                string targetFontPath,
                uint codepoint,
                int padding,
                out string error)
            {
                error = null;
                byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);

                FdtGlyphEntry sourceGlyph;
                if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                {
                    error = "clean glyph missing";
                    return false;
                }

                FdtGlyphEntry targetGlyph;
                if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                {
                    error = "patched glyph missing";
                    return false;
                }

                GlyphTextureNeighborhood sourceNeighborhood;
                if (!TryReadGlyphTextureNeighborhood(_cleanFont, sourceFontPath, sourceGlyph, padding, out sourceNeighborhood, out error))
                {
                    return false;
                }

                GlyphTextureNeighborhood targetNeighborhood;
                if (!TryReadGlyphTextureNeighborhood(_patchedFont, targetFontPath, targetGlyph, padding, out targetNeighborhood, out error))
                {
                    return false;
                }

                if (sourceNeighborhood.Width != targetNeighborhood.Width ||
                    sourceNeighborhood.Height != targetNeighborhood.Height)
                {
                    error = "neighborhood size differs clean=" +
                        sourceNeighborhood.Width.ToString() + "x" + sourceNeighborhood.Height.ToString() +
                        ", patched=" + targetNeighborhood.Width.ToString() + "x" + targetNeighborhood.Height.ToString();
                    return false;
                }

                long score = Diff(sourceNeighborhood.Alpha, targetNeighborhood.Alpha);
                if (score != 0)
                {
                    error = "score=" + score.ToString() +
                        ", cleanTexture=" + sourceNeighborhood.TexturePath +
                        ", patchedTexture=" + targetNeighborhood.TexturePath +
                        ", size=" + targetNeighborhood.Width.ToString() + "x" + targetNeighborhood.Height.ToString();
                    return false;
                }

                return true;
            }

            private bool VerifyGlyphTextureMipNeighborhoodsMatchClean(
                string sourceFontPath,
                string targetFontPath,
                uint codepoint,
                int padding,
                out string error)
            {
                error = null;
                byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);

                FdtGlyphEntry sourceGlyph;
                if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                {
                    error = "clean glyph missing";
                    return false;
                }

                FdtGlyphEntry targetGlyph;
                if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                {
                    error = "patched glyph missing";
                    return false;
                }

                string sourceTexturePath = ResolveFontTexturePath(sourceFontPath, sourceGlyph.ImageIndex);
                string targetTexturePath = ResolveFontTexturePath(targetFontPath, targetGlyph.ImageIndex);
                if (sourceTexturePath == null || targetTexturePath == null)
                {
                    error = "could not resolve texture";
                    return false;
                }

                try
                {
                    Texture sourceTexture = ReadFontTexture(_cleanFont, sourceTexturePath);
                    Texture targetTexture = ReadFontTexture(_patchedFont, targetTexturePath);
                    int maxLevel = System.Math.Min(sourceTexture.MipmapCount, targetTexture.MipmapCount);
                    for (int level = 1; level < maxLevel; level++)
                    {
                        GlyphTextureNeighborhood sourceNeighborhood;
                        if (!TryReadGlyphTextureMipNeighborhood(sourceTexture, sourceGlyph, padding, level, out sourceNeighborhood, out error))
                        {
                            return false;
                        }

                        GlyphTextureNeighborhood targetNeighborhood;
                        if (!TryReadGlyphTextureMipNeighborhood(targetTexture, targetGlyph, padding, level, out targetNeighborhood, out error))
                        {
                            return false;
                        }

                        if (sourceNeighborhood.Width != targetNeighborhood.Width ||
                            sourceNeighborhood.Height != targetNeighborhood.Height)
                        {
                            error = "mip " + level.ToString() + " neighborhood size differs clean=" +
                                sourceNeighborhood.Width.ToString() + "x" + sourceNeighborhood.Height.ToString() +
                                ", patched=" + targetNeighborhood.Width.ToString() + "x" + targetNeighborhood.Height.ToString();
                            return false;
                        }

                        long score = Diff(sourceNeighborhood.Alpha, targetNeighborhood.Alpha);
                        if (score != 0)
                        {
                            error = "mip " + level.ToString() + " score=" + score.ToString() +
                                ", cleanTexture=" + sourceTexturePath +
                                ", patchedTexture=" + targetTexturePath +
                                ", size=" + targetNeighborhood.Width.ToString() + "x" + targetNeighborhood.Height.ToString();
                            return false;
                        }
                    }

                    return true;
                }
                catch (IOException ex)
                {
                    error = ex.Message;
                    return false;
                }
                catch (InvalidDataException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private bool VerifyGlyphTextureNeighborhoodMatchesTtmpSource(
                string sourceFontPath,
                string targetFontPath,
                uint codepoint,
                int padding,
                out string error)
            {
                error = null;
                byte[] sourceFdt = _ttmpFont.ReadFile(sourceFontPath);
                byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);

                FdtGlyphEntry sourceGlyph;
                if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                {
                    error = "TTMP source glyph missing";
                    return false;
                }

                FdtGlyphEntry targetGlyph;
                if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                {
                    error = "patched glyph missing";
                    return false;
                }

                GlyphTextureNeighborhood sourceNeighborhood;
                if (!TryReadGlyphTextureNeighborhood(_ttmpFont, sourceFontPath, sourceGlyph, padding, out sourceNeighborhood, out error))
                {
                    return false;
                }

                GlyphTextureNeighborhood targetNeighborhood;
                if (!TryReadGlyphTextureNeighborhood(_patchedFont, targetFontPath, targetGlyph, padding, out targetNeighborhood, out error))
                {
                    return false;
                }

                if (sourceNeighborhood.Width != targetNeighborhood.Width ||
                    sourceNeighborhood.Height != targetNeighborhood.Height)
                {
                    error = "neighborhood size differs source=" +
                        sourceNeighborhood.Width.ToString() + "x" + sourceNeighborhood.Height.ToString() +
                        ", patched=" + targetNeighborhood.Width.ToString() + "x" + targetNeighborhood.Height.ToString();
                    return false;
                }

                long score = Diff(sourceNeighborhood.Alpha, targetNeighborhood.Alpha);
                if (score != 0)
                {
                    error = "score=" + score.ToString() +
                        ", sourceTexture=" + sourceNeighborhood.TexturePath +
                        ", patchedTexture=" + targetNeighborhood.TexturePath +
                        ", size=" + targetNeighborhood.Width.ToString() + "x" + targetNeighborhood.Height.ToString();
                    return false;
                }

                return true;
            }

            private bool TryReadGlyphTextureNeighborhood(
                CompositeArchive archive,
                string fontPath,
                FdtGlyphEntry glyph,
                int padding,
                out GlyphTextureNeighborhood neighborhood,
                out string error)
            {
                neighborhood = new GlyphTextureNeighborhood();
                error = null;

                string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    error = "could not resolve texture";
                    return false;
                }

                try
                {
                    Texture texture = ReadFontTexture(archive, texturePath);
                    int width = glyph.Width + padding * 2;
                    int height = glyph.Height + padding * 2;
                    if (width <= 0 || height <= 0)
                    {
                        error = "empty neighborhood";
                        return false;
                    }

                    byte[] alpha = new byte[width * height];
                    int channel = glyph.ImageIndex % 4;
                    int write = 0;
                    for (int y = -padding; y < glyph.Height + padding; y++)
                    {
                        int sourceY = glyph.Y + y;
                        for (int x = -padding; x < glyph.Width + padding; x++)
                        {
                            int sourceX = glyph.X + x;
                            alpha[write++] = ReadFontTextureAlphaOrZero(texture, glyph.ImageIndex, sourceX, sourceY, channel);
                        }
                    }

                    neighborhood = new GlyphTextureNeighborhood
                    {
                        Alpha = alpha,
                        TexturePath = texturePath,
                        Width = width,
                        Height = height
                    };
                    return true;
                }
                catch (IOException ex)
                {
                    error = ex.Message;
                    return false;
                }
                catch (InvalidDataException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private bool TryReadGlyphTextureNeighborhood(
                TtmpFontPackage package,
                string fontPath,
                FdtGlyphEntry glyph,
                int padding,
                out GlyphTextureNeighborhood neighborhood,
                out string error)
            {
                neighborhood = new GlyphTextureNeighborhood();
                error = null;

                string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    error = "could not resolve texture";
                    return false;
                }

                try
                {
                    Texture texture = ReadFontTexture(package, texturePath);
                    int width = glyph.Width + padding * 2;
                    int height = glyph.Height + padding * 2;
                    if (width <= 0 || height <= 0)
                    {
                        error = "empty neighborhood";
                        return false;
                    }

                    byte[] alpha = new byte[width * height];
                    int channel = glyph.ImageIndex % 4;
                    int write = 0;
                    for (int y = -padding; y < glyph.Height + padding; y++)
                    {
                        int sourceY = glyph.Y + y;
                        for (int x = -padding; x < glyph.Width + padding; x++)
                        {
                            int sourceX = glyph.X + x;
                            alpha[write++] = ReadFontTextureAlphaOrZero(texture, glyph.ImageIndex, sourceX, sourceY, channel);
                        }
                    }

                    neighborhood = new GlyphTextureNeighborhood
                    {
                        Alpha = alpha,
                        TexturePath = texturePath,
                        Width = width,
                        Height = height
                    };
                    return true;
                }
                catch (IOException ex)
                {
                    error = ex.Message;
                    return false;
                }
                catch (InvalidDataException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static uint[] CollectNonSpaceCodepoints(string[] phrases)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex];
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (!IsPhraseLayoutSpace(codepoint))
                        {
                            codepoints.Add(codepoint);
                        }
                    }
                }

                uint[] values = new uint[codepoints.Count];
                codepoints.CopyTo(values);
                return values;
            }

            private static byte ReadFontTextureAlphaOrZero(Texture texture, int imageIndex, int sourceX, int sourceY, int channel)
            {
                if (sourceX < 0 || sourceY < 0 || sourceX >= texture.Width || sourceY >= texture.Height)
                {
                    return 0;
                }

                int pixelOffset = GetTexturePixelOffset(texture, imageIndex, sourceX, sourceY);
                return ReadFontTextureAlpha(texture.Data, pixelOffset, channel);
            }

            private static bool TryReadGlyphTextureMipNeighborhood(
                Texture texture,
                FdtGlyphEntry glyph,
                int padding,
                int mipLevel,
                out GlyphTextureNeighborhood neighborhood,
                out string error)
            {
                neighborhood = new GlyphTextureNeighborhood();
                error = null;
                if (!IsTextureMipAvailable(texture, mipLevel))
                {
                    error = "mip " + mipLevel.ToString() + " unavailable";
                    return false;
                }

                int left = FloorDivPow2(glyph.X - padding, mipLevel);
                int top = FloorDivPow2(glyph.Y - padding, mipLevel);
                int right = CeilDivPow2(glyph.X + glyph.Width + padding, mipLevel);
                int bottom = CeilDivPow2(glyph.Y + glyph.Height + padding, mipLevel);
                int width = System.Math.Max(1, right - left);
                int height = System.Math.Max(1, bottom - top);
                byte[] alpha = new byte[width * height];
                int channel = glyph.ImageIndex % 4;
                int write = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        alpha[write++] = ReadFontTextureMipAlphaOrZero(texture, left + x, top + y, channel, mipLevel);
                    }
                }

                neighborhood = new GlyphTextureNeighborhood
                {
                    Alpha = alpha,
                    TexturePath = string.Empty,
                    Width = width,
                    Height = height
                };
                return true;
            }

            private static byte ReadFontTextureMipAlphaOrZero(Texture texture, int sourceX, int sourceY, int channel, int mipLevel)
            {
                int width = GetTextureMipWidth(texture, mipLevel);
                int height = GetTextureMipHeight(texture, mipLevel);
                if (sourceX < 0 || sourceY < 0 || sourceX >= width || sourceY >= height || !IsTextureMipAvailable(texture, mipLevel))
                {
                    return 0;
                }

                int pixelOffset = GetTextureMipOffset(texture, mipLevel) + (sourceY * width + sourceX) * 2;
                byte lo = texture.Raw[pixelOffset];
                byte hi = texture.Raw[pixelOffset + 1];
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

            private static bool IsTextureMipAvailable(Texture texture, int mipLevel)
            {
                if (texture.Raw == null || texture.MipmapOffsets == null || mipLevel < 0 || mipLevel >= texture.MipmapOffsets.Length)
                {
                    return false;
                }

                int offset = GetTextureMipOffset(texture, mipLevel);
                int size = GetTextureMipWidth(texture, mipLevel) * GetTextureMipHeight(texture, mipLevel) * 2;
                return offset >= 0 && offset + size <= texture.Raw.Length;
            }

            private static int GetTextureMipOffset(Texture texture, int mipLevel)
            {
                if (texture.MipmapOffsets == null || mipLevel < 0 || mipLevel >= texture.MipmapOffsets.Length)
                {
                    return 0;
                }

                return texture.MipmapOffsets[mipLevel];
            }

            private static int GetTextureMipWidth(Texture texture, int mipLevel)
            {
                return System.Math.Max(1, texture.Width >> System.Math.Max(0, mipLevel));
            }

            private static int GetTextureMipHeight(Texture texture, int mipLevel)
            {
                return System.Math.Max(1, texture.Height >> System.Math.Max(0, mipLevel));
            }

            private static int FloorDivPow2(int value, int shift)
            {
                if (shift <= 0)
                {
                    return value;
                }

                int divisor = 1 << System.Math.Min(shift, 30);
                if (value >= 0)
                {
                    return value / divisor;
                }

                return -(((-value) + divisor - 1) / divisor);
            }

            private static int CeilDivPow2(int value, int shift)
            {
                return -FloorDivPow2(-value, shift);
            }

            private struct GlyphTextureNeighborhood
            {
                public byte[] Alpha;
                public string TexturePath;
                public int Width;
                public int Height;
            }
        }
    }
}
