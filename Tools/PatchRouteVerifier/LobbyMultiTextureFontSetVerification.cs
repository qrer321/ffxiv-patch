using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int MaxLobbyMultiTextureFontSetFailures = 40;

            private void VerifyLobbyMultiTextureFontSet()
            {
                Console.WriteLine("[FDT] Lobby multi-texture font set");

                Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> referencedTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int checkedFonts = 0;
                int checkedGlyphs = 0;
                int failures = 0;

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    if (!_patchedFont.ContainsPath(fontPath))
                    {
                        failures = FailLobbyMultiTextureOnce(failures, "{0} patched lobby FDT is missing", fontPath);
                        continue;
                    }

                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        failures = FailLobbyMultiTextureOnce(failures, "{0} patched lobby FDT read error: {1}", fontPath, ex.Message);
                        continue;
                    }

                    int fontTableOffset;
                    uint glyphCount;
                    int glyphStart;
                    if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                    {
                        failures = FailLobbyMultiTextureOnce(failures, "{0} lobby FDT glyph table is invalid", fontPath);
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
                        if (!IsLobbyTexturePath(texturePath))
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} does not resolve to a lobby texture",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex);
                            continue;
                        }

                        referencedTextures.Add(texturePath);
                        if (!_patchedFont.ContainsPath(texturePath))
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} references missing texture {3}",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex,
                                texturePath);
                            continue;
                        }

                        Texture texture;
                        if (!TryReadLobbyTexture(textureCache, texturePath, out texture))
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} cannot read referenced texture {2}",
                                fontPath,
                                glyphIndex,
                                texturePath);
                            continue;
                        }

                        if (glyph.X + glyph.Width > texture.Width || glyph.Y + glyph.Height > texture.Height)
                        {
                            failures = FailLobbyMultiTextureOnce(
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

                        checkedGlyphs++;
                    }
                }

                VerifyLobbyTextureDimensionsNotExpanded(ref failures);
                if (failures >= MaxLobbyMultiTextureFontSetFailures)
                {
                    Warn("lobby multi-texture font set check stopped after {0} failures", MaxLobbyMultiTextureFontSetFailures);
                }

                if (failures == 0)
                {
                    Pass(
                        "lobby FDT texture routes are valid: fonts={0}, glyphs={1}, referenced_textures={2}",
                        checkedFonts,
                        checkedGlyphs,
                        referencedTextures.Count);
                }
            }

            private bool TryReadLobbyTexture(Dictionary<string, Texture> cache, string texturePath, out Texture texture)
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

            private void VerifyLobbyTextureDimensionsNotExpanded(ref int failures)
            {
                string[] paths = new string[]
                {
                    FontLobby1TexturePath,
                    FontLobby2TexturePath,
                    FontLobby3TexturePath,
                    FontLobby4TexturePath,
                    FontLobby5TexturePath,
                    FontLobby6TexturePath,
                    FontLobby7TexturePath
                };

                int checkedTextures = 0;
                for (int i = 0; i < paths.Length; i++)
                {
                    string texturePath = paths[i];
                    if (!_patchedFont.ContainsPath(texturePath))
                    {
                        continue;
                    }

                    if (!_cleanFont.ContainsPath(texturePath))
                    {
                        Warn("{0} exists in patched font archive without a clean baseline entry; verify index addition separately", texturePath);
                        continue;
                    }

                    try
                    {
                        Texture clean = ReadFontTexture(_cleanFont, texturePath);
                        Texture patched = ReadFontTexture(_patchedFont, texturePath);
                        checkedTextures++;
                        if (patched.Width != clean.Width || patched.Height != clean.Height)
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} texture dimensions changed from clean {1}x{2} to patched {3}x{4}; lobby overflow must use pages, not resize",
                                texturePath,
                                clean.Width,
                                clean.Height,
                                patched.Width,
                                patched.Height);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures = FailLobbyMultiTextureOnce(
                            failures,
                            "{0} texture dimension check failed: {1}",
                            texturePath,
                            ex.Message);
                    }
                }

                if (failures == 0)
                {
                    Pass("lobby texture dimensions are not expanded: textures={0}", checkedTextures);
                }
            }

            private int FailLobbyMultiTextureOnce(int failures, string format, params object[] args)
            {
                if (failures < MaxLobbyMultiTextureFontSetFailures)
                {
                    Fail(format, args);
                }

                return failures + 1;
            }

        }
    }
}
