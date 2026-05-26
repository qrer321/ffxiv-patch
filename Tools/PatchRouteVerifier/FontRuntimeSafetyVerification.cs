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

                    VerifyNoRuntimeUtf8OnlyStartScreenKerning(fontPath, fdt, ref failures);

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

            private static readonly Lazy<HashSet<string>> RuntimeUnsafeStartScreenKerningPairs =
                new Lazy<HashSet<string>>(CreateRuntimeUnsafeStartScreenKerningPairs);

            private void VerifyNoRuntimeUtf8OnlyStartScreenKerning(string fontPath, byte[] fdt, ref int failures)
            {
                if (IsLobbyFontPath(fontPath) || fdt == null)
                {
                    return;
                }

                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
                {
                    return;
                }

                HashSet<string> unsafePairs = RuntimeUnsafeStartScreenKerningPairs.Value;
                for (int i = 0; i < kerningCount; i++)
                {
                    int offset = kerningStart + i * FdtKerningEntrySize;
                    uint leftValue = Endian.ReadUInt32LE(fdt, offset);
                    uint rightValue = Endian.ReadUInt32LE(fdt, offset + 4);
                    string key = BuildKerningAdjustmentKey(leftValue, rightValue);
                    if (!unsafePairs.Contains(key))
                    {
                        continue;
                    }

                    ushort leftShiftJis = Endian.ReadUInt16LE(fdt, offset + 8);
                    ushort rightShiftJis = Endian.ReadUInt16LE(fdt, offset + 10);
                    if (leftShiftJis != 0 && rightShiftJis != 0)
                    {
                        continue;
                    }

                    uint leftCodepoint;
                    uint rightCodepoint;
                    TryDecodeFdtUtf8Value(leftValue, out leftCodepoint);
                    TryDecodeFdtUtf8Value(rightValue, out rightCodepoint);
                    if (IsRuntimeSafeTerminalPunctuationKerning(leftCodepoint, rightCodepoint, leftShiftJis, rightShiftJis))
                    {
                        continue;
                    }

                    failures = FailFontRuntimeGlyphBoundsOnce(
                        failures,
                        "{0} has start-screen UTF-8-only kerning U+{1:X4}:U+{2:X4} with Shift-JIS fallback {3:X4}:{4:X4}; non-lobby runtime fonts are used by HD/non-4K lobby UI and must not receive Hangul synthetic kerning",
                        fontPath,
                        leftCodepoint,
                        rightCodepoint,
                        leftShiftJis,
                        rightShiftJis);
                }
            }

            private static HashSet<string> CreateRuntimeUnsafeStartScreenKerningPairs()
            {
                HashSet<string> pairs = new HashSet<string>(StringComparer.Ordinal);
                AddRuntimeUnsafeStartScreenKerningPairs(pairs, LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);
                AddRuntimeUnsafeStartScreenKerningPairs(pairs, LobbyScaledHangulPhrases.StartScreenSystemSettings);
                AddRuntimeUnsafeStartScreenKerningPairs(pairs, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions);
                return pairs;
            }

            private static void AddRuntimeUnsafeStartScreenKerningPairs(HashSet<string> pairs, string[] phrases)
            {
                if (pairs == null || phrases == null)
                {
                    return;
                }

                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    uint previous = 0;
                    bool hasPrevious = false;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPrevious && IsRuntimeUnsafeStartScreenKerningPair(previous, codepoint))
                        {
                            pairs.Add(BuildKerningAdjustmentKey(PackUtf8(previous), PackUtf8(codepoint)));
                        }

                        previous = codepoint;
                        hasPrevious = !IsPhraseLayoutSpace(codepoint);
                    }
                }
            }

            private static bool IsRuntimeUnsafeStartScreenKerningPair(uint left, uint right)
            {
                ushort ignored;
                bool leftEncodes = TryEncodeShiftJisValue(left, out ignored);
                bool rightEncodes = TryEncodeShiftJisValue(right, out ignored);
                return (!leftEncodes || !rightEncodes) &&
                       (IsHangulCodepoint(left) || IsHangulCodepoint(right));
            }

            private static bool IsRuntimeSafeTerminalPunctuationKerning(
                uint left,
                uint right,
                ushort leftShiftJis,
                ushort rightShiftJis)
            {
                return IsHangulCodepoint(left) &&
                       IsRuntimeSafeTerminalPunctuationCodepoint(right) &&
                       leftShiftJis == 0 &&
                       rightShiftJis != 0;
            }

            private static bool IsRuntimeSafeTerminalPunctuationCodepoint(uint codepoint)
            {
                return codepoint == 0x002Eu ||
                       codepoint == 0x3002u ||
                       codepoint == 0xFF0Eu;
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
