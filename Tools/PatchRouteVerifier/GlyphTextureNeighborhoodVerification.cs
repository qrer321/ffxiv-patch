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
