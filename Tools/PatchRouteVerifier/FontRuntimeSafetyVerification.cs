using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int MaxFontRuntimeGlyphBoundsFailures = 80;

            private void VerifyFontRuntimeGlyphBounds()
            {
                Console.WriteLine("[FDT] Font runtime glyph bounds");

                Dictionary<string, Texture> textures = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
                int checkedFonts = 0;
                int checkedGlyphs = 0;
                int checkedTextures = 0;
                int failures = 0;

                for (int fontIndex = 0; fontIndex < RuntimeFontSafetyFontPaths.Length; fontIndex++)
                {
                    string fontPath = RuntimeFontSafetyFontPaths[fontIndex];
                    if (!_patchedFont.ContainsPath(fontPath))
                    {
                        failures = FailFontRuntimeGlyphBoundsOnce(failures, "{0} patched FDT is missing", fontPath);
                        continue;
                    }

                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        failures = FailFontRuntimeGlyphBoundsOnce(failures, "{0} patched FDT read error: {1}", fontPath, ex.Message);
                        continue;
                    }

                    int fontTableOffset;
                    uint glyphCount;
                    int glyphStart;
                    if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                    {
                        failures = FailFontRuntimeGlyphBoundsOnce(failures, "{0} glyph table is invalid", fontPath);
                        continue;
                    }

                    if (!VerifyFontRuntimeKerningOffset(fontPath, fdt, ref failures))
                    {
                        continue;
                    }

                    checkedFonts++;
                    for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                    {
                        int glyphOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                        FdtGlyphEntry glyph = ReadGlyphEntry(fdt, glyphOffset);
                        if (glyph.Width == 0 || glyph.Height == 0)
                        {
                            continue;
                        }

                        string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                        if (string.IsNullOrEmpty(texturePath))
                        {
                            failures = FailFontRuntimeGlyphBoundsOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} does not resolve to a font texture",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex);
                            continue;
                        }

                        if (!_patchedFont.ContainsPath(texturePath))
                        {
                            failures = FailFontRuntimeGlyphBoundsOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} references missing texture {3}",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex,
                                texturePath);
                            continue;
                        }

                        Texture texture;
                        if (!TryReadRuntimeTexture(textures, texturePath, out texture))
                        {
                            failures = FailFontRuntimeGlyphBoundsOnce(
                                failures,
                                "{0} glyph#{1} cannot read referenced texture {2}",
                                fontPath,
                                glyphIndex,
                                texturePath);
                            continue;
                        }

                        if (glyph.X + glyph.Width > texture.Width || glyph.Y + glyph.Height > texture.Height)
                        {
                            failures = FailFontRuntimeGlyphBoundsOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} cell {3},{4} {5}x{6} exceeds {7} bounds {8}x{9}",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex,
                                glyph.X,
                                glyph.Y,
                                glyph.Width,
                                glyph.Height,
                                texturePath,
                                texture.Width,
                                texture.Height);
                            continue;
                        }

                        if (!VerifyRuntimeTextureMipsAvailable(texture, out string mipError))
                        {
                            failures = FailFontRuntimeGlyphBoundsOnce(
                                failures,
                                "{0} texture {1} has invalid mip data: {2}",
                                fontPath,
                                texturePath,
                                mipError);
                            continue;
                        }

                        checkedGlyphs++;
                    }
                }

                checkedTextures = textures.Count;
                if (failures >= MaxFontRuntimeGlyphBoundsFailures)
                {
                    Warn("font runtime glyph bounds check stopped after {0} failures", MaxFontRuntimeGlyphBoundsFailures);
                }

                if (failures == 0)
                {
                    Pass(
                        "font runtime glyph bounds passed: fonts={0}, glyphs={1}, textures={2}",
                        checkedFonts,
                        checkedGlyphs,
                        checkedTextures);
                }
            }

            private bool VerifyFontRuntimeKerningOffset(string fontPath, byte[] fdt, ref int failures)
            {
                if (fdt == null || fdt.Length < FdtHeaderSize)
                {
                    failures = FailFontRuntimeGlyphBoundsOnce(failures, "{0} FDT is too short", fontPath);
                    return false;
                }

                uint kerningHeaderOffset = Endian.ReadUInt32LE(fdt, 0x0C);
                if (kerningHeaderOffset == 0)
                {
                    return true;
                }

                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
                {
                    failures = FailFontRuntimeGlyphBoundsOnce(
                        failures,
                        "{0} kerning table offset 0x{1:X8} is invalid",
                        fontPath,
                        kerningHeaderOffset);
                    return false;
                }

                return true;
            }

            private bool TryReadRuntimeTexture(Dictionary<string, Texture> cache, string texturePath, out Texture texture)
            {
                if (cache.TryGetValue(texturePath, out texture))
                {
                    return true;
                }

                try
                {
                    texture = ReadFontTexture(_patchedFont, texturePath);
                    cache.Add(texturePath, texture);
                    return true;
                }
                catch
                {
                    texture = new Texture();
                    return false;
                }
            }

            private static bool VerifyRuntimeTextureMipsAvailable(Texture texture, out string error)
            {
                error = null;
                if (texture.Raw == null || texture.MipmapOffsets == null || texture.MipmapOffsets.Length == 0)
                {
                    error = "missing texture mip table";
                    return false;
                }

                for (int level = 0; level < texture.MipmapOffsets.Length; level++)
                {
                    if (!IsTextureMipAvailable(texture, level))
                    {
                        error = "mip " + level.ToString() + " is outside raw texture bounds";
                        return false;
                    }
                }

                return true;
            }

            private int FailFontRuntimeGlyphBoundsOnce(int failures, string format, params object[] args)
            {
                if (failures < MaxFontRuntimeGlyphBoundsFailures)
                {
                    Fail(format, args);
                }

                return failures + 1;
            }
        }
    }
}
