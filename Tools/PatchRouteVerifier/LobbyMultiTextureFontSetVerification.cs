using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

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

                    HashSet<string> cleanTexturePages = CollectCleanLobbyFontTexturePages(fontPath, ref failures);
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

                        if (IsClientUnsafeLobbyTexturePath(texturePath))
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} references client-unsafe lobby texture {3}; lobby runtime scale reload is limited to clean lobby pages",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex,
                                texturePath);
                            continue;
                        }

                        if (cleanTexturePages != null &&
                            !cleanTexturePages.Contains(texturePath))
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} references {3}, but clean {0} never references that lobby texture page (clean pages: {4}); keep each lobby FDT inside its clean page set for non-4K UI-resolution boot safety",
                                fontPath,
                                glyphIndex,
                                glyph.ImageIndex,
                                texturePath,
                                FormatLobbyTexturePageSet(cleanTexturePages));
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

            private HashSet<string> CollectCleanLobbyFontTexturePages(string fontPath, ref int failures)
            {
                byte[] cleanFdt;
                try
                {
                    cleanFdt = _cleanFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    failures = FailLobbyMultiTextureOnce(
                        failures,
                        "{0} clean lobby FDT is missing or unreadable for page-ceiling check: {1}",
                        fontPath,
                        ex.Message);
                    return null;
                }

                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(cleanFdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    failures = FailLobbyMultiTextureOnce(
                        failures,
                        "{0} clean lobby FDT glyph table is invalid for page-ceiling check",
                        fontPath);
                    return null;
                }

                HashSet<string> pages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    int glyphOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                    FdtGlyphEntry glyph = ReadGlyphEntry(cleanFdt, glyphOffset);
                    if (glyph.Width == 0 || glyph.Height == 0)
                    {
                        continue;
                    }

                    string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                    if (!string.IsNullOrEmpty(texturePath))
                    {
                        pages.Add(texturePath);
                    }
                }

                return pages;
            }

            private static string FormatLobbyTexturePageSet(HashSet<string> pages)
            {
                if (pages == null || pages.Count == 0)
                {
                    return "<none>";
                }

                string[] values = new string[pages.Count];
                pages.CopyTo(values);
                Array.Sort(values, StringComparer.OrdinalIgnoreCase);
                return string.Join(",", values);
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
                        failures = FailLobbyMultiTextureOnce(
                            failures,
                            "{0} exists in patched font archive without a clean baseline entry; added lobby texture pages are not client-safe",
                            texturePath);
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
                    Pass("lobby texture dimensions are not expanded: textures={0}, added_pages=0", checkedTextures);
                }
            }

            private static bool IsClientUnsafeLobbyTexturePath(string texturePath)
            {
                return string.Equals(texturePath, FontLobby7TexturePath, StringComparison.OrdinalIgnoreCase);
            }

            private int FailLobbyMultiTextureOnce(int failures, string format, params object[] args)
            {
                if (failures < MaxLobbyMultiTextureFontSetFailures)
                {
                    Fail(format, args);
                }

                return failures + 1;
            }

            private void VerifyLobbyTextureCellMargin()
            {
                Console.WriteLine("[FDT] Lobby texture cell margin");

                Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
                int checkedFonts = 0;
                int checkedGlyphs = 0;
                int failures = 0;

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    byte[] patchedFdt;
                    byte[] cleanFdt;
                    try
                    {
                        patchedFdt = _patchedFont.ReadFile(fontPath);
                        cleanFdt = _cleanFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        failures = FailLobbyMultiTextureOnce(
                            failures,
                            "{0} could not be read for texture margin check: {1}",
                            fontPath,
                            ex.Message);
                        continue;
                    }

                    int patchedFontTableOffset;
                    uint patchedGlyphCount;
                    int patchedGlyphStart;
                    if (!TryGetFdtGlyphTable(patchedFdt, out patchedFontTableOffset, out patchedGlyphCount, out patchedGlyphStart))
                    {
                        failures = FailLobbyMultiTextureOnce(failures, "{0} patched FDT glyph table is invalid for texture margin check", fontPath);
                        continue;
                    }

                    Dictionary<uint, FdtGlyphEntry> cleanGlyphs = ReadGlyphEntriesByUtf8(cleanFdt);
                    checkedFonts++;
                    for (int glyphIndex = 0; glyphIndex < patchedGlyphCount; glyphIndex++)
                    {
                        int glyphOffset = patchedGlyphStart + glyphIndex * FdtGlyphEntrySize;
                        FdtGlyphEntry glyph = ReadGlyphEntry(patchedFdt, glyphOffset);
                        if (glyph.Width == 0 || glyph.Height == 0)
                        {
                            continue;
                        }

                        string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                        if (string.IsNullOrEmpty(texturePath) || !_patchedFont.ContainsPath(texturePath))
                        {
                            continue;
                        }

                        Texture texture;
                        if (!TryReadLobbyTexture(textureCache, texturePath, out texture))
                        {
                            continue;
                        }

                        bool touchesBottom = glyph.Y + glyph.Height >= texture.Height;
                        if (!touchesBottom)
                        {
                            checkedGlyphs++;
                            continue;
                        }

                        uint utf8Value = Endian.ReadUInt32LE(patchedFdt, glyphOffset);
                        FdtGlyphEntry cleanGlyph;
                        bool cleanAlreadyTouched = cleanGlyphs.TryGetValue(utf8Value, out cleanGlyph) &&
                            cleanGlyph.ImageIndex == glyph.ImageIndex &&
                            cleanGlyph.Y + cleanGlyph.Height >= texture.Height;
                        if (!cleanAlreadyTouched)
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} image_index={2} cell {3},{4} {5}x{6} touches texture bottom edge {7} {8}x{9}; keep a 1px bottom margin for UI-resolution reload safety",
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
                        }

                        checkedGlyphs++;
                    }
                }

                if (failures == 0)
                {
                    Pass("lobby texture cells keep clean-safe edge margins: fonts={0}, glyphs={1}", checkedFonts, checkedGlyphs);
                }
            }

            private static Dictionary<uint, FdtGlyphEntry> ReadGlyphEntriesByUtf8(byte[] fdt)
            {
                Dictionary<uint, FdtGlyphEntry> entries = new Dictionary<uint, FdtGlyphEntry>();
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return entries;
                }

                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    int glyphOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                    uint utf8Value = Endian.ReadUInt32LE(fdt, glyphOffset);
                    if (!entries.ContainsKey(utf8Value))
                    {
                        entries.Add(utf8Value, ReadGlyphEntry(fdt, glyphOffset));
                    }
                }

                return entries;
            }

        }
    }
}
