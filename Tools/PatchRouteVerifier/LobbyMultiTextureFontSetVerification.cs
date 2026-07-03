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

                    Dictionary<uint, FdtGlyphEntry> cleanHangulGlyphs = ReadCleanLobbyHangulGlyphs(fontPath);
                    Dictionary<uint, FdtGlyphEntry> cleanGlyphRoutes;
                    HashSet<int> cleanPages = ReadCleanLobbyFontRoutes(fontPath, out cleanGlyphRoutes);
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
                        uint codepoint;
                        bool decodedCodepoint = TryDecodeFdtUtf8Value(FfxivKoreanPatch.FFXIVPatchGenerator.Endian.ReadUInt32LE(fdt, glyphOffset), out codepoint);
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

                        if (IsClientUnsafeLobbyTexturePath(texturePath) ||
                            IsPatchedGlyphOutsideRuntimePageBudget(decodedCodepoint, codepoint, glyph, cleanGlyphRoutes, cleanPages))
                        {
                            failures = FailLobbyMultiTextureOnce(
                                failures,
                                "{0} glyph#{1} U+{2:X4} image_index={3} (page {4}) routes a patched glyph to {5}, which the client only loads for this font's clean page set; HD/FHD runtimes load just the first two lobby pages",
                                fontPath,
                                glyphIndex,
                                decodedCodepoint ? codepoint : 0u,
                                glyph.ImageIndex,
                                glyph.ImageIndex / 4,
                                texturePath);
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

                        if (IsPatchedLobbyHangulGlyph(decodedCodepoint, codepoint, glyph, cleanHangulGlyphs))
                        {
                            if (glyph.Y + glyph.Height >= texture.Height)
                            {
                                failures = FailLobbyMultiTextureOnce(
                                    failures,
                                    "{0} glyph#{1} U+{2:X4} image_index={3} cell {4},{5} {6}x{7} touches {8} bottom edge {9}; HD/FHD runtime scale reload must keep injected Hangul away from texture bounds",
                                    fontPath,
                                    glyphIndex,
                                    codepoint,
                                    glyph.ImageIndex,
                                    glyph.X,
                                    glyph.Y,
                                    glyph.Width,
                                    glyph.Height,
                                    texturePath,
                                    texture.Height);
                                continue;
                            }

                            int ringInkPixels;
                            int ringMaxAlpha;
                            if (!GlyphGuardRingIsBlank(texture, glyph, out ringInkPixels, out ringMaxAlpha))
                            {
                                failures = FailLobbyMultiTextureOnce(
                                    failures,
                                    "{0} glyph#{1} U+{2:X4} image_index={3} cell {4},{5} {6}x{7} has ink in its 1px guard ring on {8} (pixels={9}, max={10}); bilinear sampling at the quad edge renders a faint rectangular border",
                                    fontPath,
                                    glyphIndex,
                                    codepoint,
                                    glyph.ImageIndex,
                                    glyph.X,
                                    glyph.Y,
                                    glyph.Width,
                                    glyph.Height,
                                    texturePath,
                                    ringInkPixels,
                                    ringMaxAlpha);
                                continue;
                            }
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

            private Dictionary<uint, FdtGlyphEntry> ReadCleanLobbyHangulGlyphs(string fontPath)
            {
                Dictionary<uint, FdtGlyphEntry> result = new Dictionary<uint, FdtGlyphEntry>();
                if (!_cleanFont.ContainsPath(fontPath))
                {
                    return result;
                }

                byte[] cleanFdt;
                try
                {
                    cleanFdt = _cleanFont.ReadFile(fontPath);
                }
                catch
                {
                    return result;
                }

                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(cleanFdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return result;
                }

                for (int i = 0; i < glyphCount; i++)
                {
                    int offset = glyphStart + i * FdtGlyphEntrySize;
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(FfxivKoreanPatch.FFXIVPatchGenerator.Endian.ReadUInt32LE(cleanFdt, offset), out codepoint) ||
                        !IsHangulCodepoint(codepoint))
                    {
                        continue;
                    }

                    result[codepoint] = ReadGlyphEntry(cleanFdt, offset);
                }

                return result;
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

            // A lobby texture page is loaded at runtime only when the current
            // UI resolution's font set needs it: HD/FHD loads just the first
            // two pages, QHD/4K loads all six. A patched glyph route is
            // therefore valid only on an always-loaded HD page or on a page
            // the clean version of the same FDT already referenced (live
            // crash dumps show AtkFontAnalyzerRenderer dying with R10 = the
            // first out-of-budget page index).
            private const int HdRuntimeLobbyTexturePageCount = 2;

            private static bool IsPatchedGlyphOutsideRuntimePageBudget(
                bool decodedCodepoint,
                uint codepoint,
                FdtGlyphEntry glyph,
                Dictionary<uint, FdtGlyphEntry> cleanGlyphRoutes,
                HashSet<int> cleanPages)
            {
                int page = glyph.ImageIndex / 4;
                if (page < HdRuntimeLobbyTexturePageCount)
                {
                    return false;
                }

                bool fontBelongsToUpperPageSet = false;
                if (cleanPages != null)
                {
                    foreach (int cleanPage in cleanPages)
                    {
                        if (cleanPage >= HdRuntimeLobbyTexturePageCount)
                        {
                            fontBelongsToUpperPageSet = true;
                            break;
                        }
                    }
                }

                if (page > 5 || !fontBelongsToUpperPageSet)
                {
                    if (!decodedCodepoint)
                    {
                        return true;
                    }

                    FdtGlyphEntry cleanGlyph;
                    return cleanGlyphRoutes == null ||
                        !cleanGlyphRoutes.TryGetValue(codepoint, out cleanGlyph) ||
                        !GlyphRouteMatches(cleanGlyph, glyph);
                }

                return false;
            }

            private HashSet<int> ReadCleanLobbyFontRoutes(string fontPath, out Dictionary<uint, FdtGlyphEntry> cleanGlyphRoutes)
            {
                cleanGlyphRoutes = new Dictionary<uint, FdtGlyphEntry>();
                HashSet<int> pages = new HashSet<int>();
                if (!_cleanFont.ContainsPath(fontPath))
                {
                    return pages;
                }

                byte[] cleanFdt;
                try
                {
                    cleanFdt = _cleanFont.ReadFile(fontPath);
                }
                catch
                {
                    return pages;
                }

                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(cleanFdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return pages;
                }

                for (int i = 0; i < glyphCount; i++)
                {
                    int offset = glyphStart + i * FdtGlyphEntrySize;
                    FdtGlyphEntry glyph = ReadGlyphEntry(cleanFdt, offset);
                    pages.Add(glyph.ImageIndex / 4);
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(FfxivKoreanPatch.FFXIVPatchGenerator.Endian.ReadUInt32LE(cleanFdt, offset), out codepoint))
                    {
                        continue;
                    }

                    if (!cleanGlyphRoutes.ContainsKey(codepoint))
                    {
                        cleanGlyphRoutes.Add(codepoint, glyph);
                    }
                }

                return pages;
            }

            // Bilinear sampling reads up to half a texel outside the glyph
            // quad, so the 1px ring around every patched Hangul rect must stay
            // blank or the neighbor's pixels render as a faint rectangular
            // border (2026-06-13 HD/FHD character select live report).
            private static bool GlyphGuardRingIsBlank(Texture texture, FdtGlyphEntry glyph, out int inkPixels, out int maxAlpha)
            {
                inkPixels = 0;
                maxAlpha = 0;
                int channel = glyph.ImageIndex % 4;
                int left = glyph.X - 1;
                int top = glyph.Y - 1;
                int right = glyph.X + glyph.Width;
                int bottom = glyph.Y + glyph.Height;
                for (int y = top; y <= bottom; y++)
                {
                    if (y < 0 || y >= texture.Height)
                    {
                        continue;
                    }

                    for (int x = left; x <= right; x++)
                    {
                        if (x < 0 || x >= texture.Width)
                        {
                            continue;
                        }

                        if (x != left && x != right && y != top && y != bottom)
                        {
                            continue;
                        }

                        int alpha = ReadFontTextureAlpha(texture.Data, (y * texture.Width + x) * 2, channel);
                        if (alpha != 0)
                        {
                            inkPixels++;
                            if (alpha > maxAlpha)
                            {
                                maxAlpha = alpha;
                            }
                        }
                    }
                }

                return inkPixels == 0;
            }

            private static bool IsPatchedLobbyHangulGlyph(
                bool decodedCodepoint,
                uint codepoint,
                FdtGlyphEntry glyph,
                Dictionary<uint, FdtGlyphEntry> cleanHangulGlyphs)
            {
                if (!decodedCodepoint || !IsHangulCodepoint(codepoint))
                {
                    return false;
                }

                FdtGlyphEntry cleanGlyph;
                return !cleanHangulGlyphs.TryGetValue(codepoint, out cleanGlyph) ||
                       !GlyphRouteAndSpacingMatch(cleanGlyph, glyph);
            }

            private static bool GlyphRouteAndSpacingMatch(FdtGlyphEntry left, FdtGlyphEntry right)
            {
                return left.ShiftJisValue == right.ShiftJisValue &&
                       left.ImageIndex == right.ImageIndex &&
                       left.X == right.X &&
                       left.Y == right.Y &&
                       left.Width == right.Width &&
                       left.Height == right.Height &&
                       left.OffsetX == right.OffsetX &&
                       left.OffsetY == right.OffsetY;
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
