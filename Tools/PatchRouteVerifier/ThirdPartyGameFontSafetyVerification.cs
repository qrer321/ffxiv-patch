using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int ThirdPartyGameFontHangulPadding = 2;

            private static readonly string[] ThirdPartyGameFontSafetyFonts = new string[]
            {
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/AXIS_96.fdt",
                "common/font/Meidinger_16.fdt",
                "common/font/Meidinger_20.fdt",
                "common/font/Meidinger_40.fdt",
                "common/font/MiedingerMid_10.fdt",
                "common/font/MiedingerMid_12.fdt",
                "common/font/MiedingerMid_14.fdt",
                "common/font/MiedingerMid_18.fdt",
                "common/font/MiedingerMid_36.fdt",
                "common/font/TrumpGothic_23.fdt",
                "common/font/TrumpGothic_34.fdt",
                "common/font/TrumpGothic_68.fdt",
                "common/font/TrumpGothic_184.fdt"
            };

            private void VerifyThirdPartyGameFontSafety()
            {
                Console.WriteLine("[FDT] Third-party game-font Hangul safety");
                if (_ttmpFont == null)
                {
                    Warn("Third-party game-font safety skipped; pass --font-pack-dir with TTMPD.mpd and TTMPL.mpl");
                    return;
                }

                bool ok = true;
                int fonts = 0;
                int compared = 0;
                HashSet<uint> actionDetailHighScaleCodepoints = CollectActionDetailHighScaleHangulCodepointSet();
                for (int i = 0; i < ThirdPartyGameFontSafetyFonts.Length; i++)
                {
                    string fontPath = ThirdPartyGameFontSafetyFonts[i];
                    if (!_ttmpFont.ContainsPath(fontPath))
                    {
                        continue;
                    }

                    int comparedForFont = VerifyThirdPartyGameFont(fontPath, actionDetailHighScaleCodepoints, ref ok);
                    if (comparedForFont > 0)
                    {
                        fonts++;
                        compared += comparedForFont;
                    }
                }

                if (compared == 0)
                {
                    Fail("No third-party game-font Hangul glyphs were compared");
                    return;
                }

                if (ok)
                {
                    Pass(
                        "Third-party game-font Hangul glyph routes and {0}px texture neighborhoods match TTMP source: fonts={1}, glyphs={2}",
                        ThirdPartyGameFontHangulPadding,
                        fonts,
                        compared);
                }
            }

            private int VerifyThirdPartyGameFont(
                string fontPath,
                HashSet<uint> actionDetailHighScaleCodepoints,
                ref bool ok)
            {
                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(fontPath);
                    targetFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} third-party safety read error: {1}", fontPath, ex.Message);
                    ok = false;
                    return 0;
                }

                Dictionary<uint, FdtGlyphEntry> sourceGlyphs = ReadHangulGlyphEntries(sourceFdt);
                Dictionary<uint, FdtGlyphEntry> targetGlyphs = ReadHangulGlyphEntries(targetFdt);
                Dictionary<string, Texture> sourceTextures = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Texture> targetTextures = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
                int compared = 0;
                int failures = 0;
                foreach (KeyValuePair<uint, FdtGlyphEntry> pair in sourceGlyphs)
                {
                    uint codepoint = pair.Key;
                    FdtGlyphEntry sourceGlyph = pair.Value;
                    if (sourceGlyph.Width <= 0 ||
                        sourceGlyph.Height <= 0 ||
                        IsIntentionalHangulSourceChange(fontPath, codepoint, actionDetailHighScaleCodepoints))
                    {
                        continue;
                    }

                    FdtGlyphEntry targetGlyph;
                    if (!targetGlyphs.TryGetValue(codepoint, out targetGlyph))
                    {
                        Fail("{0} U+{1:X4} third-party Hangul glyph missing in patched font", fontPath, codepoint);
                        ok = false;
                        failures++;
                    }
                    else if (!ThirdPartyGameFontGlyphRouteMatches(sourceGlyph, targetGlyph))
                    {
                        Fail(
                            "{0} U+{1:X4} third-party Hangul route changed: target={2}, source={3}",
                            fontPath,
                            codepoint,
                            FormatGlyphEntryRoute(targetGlyph),
                            FormatGlyphEntryRoute(sourceGlyph));
                        ok = false;
                        failures++;
                    }
                    else
                    {
                        string error;
                        if (!ThirdPartyGameFontTextureNeighborhoodMatches(
                            fontPath,
                            sourceGlyph,
                            targetGlyph,
                            sourceTextures,
                            targetTextures,
                            out error))
                        {
                            Fail(
                                "{0} U+{1:X4} third-party Hangul texture neighborhood differs: {2}",
                                fontPath,
                                codepoint,
                                error);
                            ok = false;
                            failures++;
                        }
                        else
                        {
                            compared++;
                        }
                    }

                    if (failures >= MaxTexturePaddingFailuresPerFont)
                    {
                        Warn("{0} third-party safety scan stopped after {1} failures", fontPath, failures);
                        break;
                    }
                }

                return compared;
            }

            private static bool ThirdPartyGameFontGlyphRouteMatches(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
            {
                return sourceGlyph.ImageIndex == targetGlyph.ImageIndex &&
                       sourceGlyph.X == targetGlyph.X &&
                       sourceGlyph.Y == targetGlyph.Y &&
                       sourceGlyph.Width == targetGlyph.Width &&
                       sourceGlyph.Height == targetGlyph.Height &&
                       sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                       sourceGlyph.OffsetY == targetGlyph.OffsetY;
            }

            private bool ThirdPartyGameFontTextureNeighborhoodMatches(
                string fontPath,
                FdtGlyphEntry sourceGlyph,
                FdtGlyphEntry targetGlyph,
                Dictionary<string, Texture> sourceTextures,
                Dictionary<string, Texture> targetTextures,
                out string error)
            {
                error = null;
                string texturePath = ResolveFontTexturePath(fontPath, sourceGlyph.ImageIndex);
                if (texturePath == null)
                {
                    error = "could not resolve texture";
                    return false;
                }

                Texture sourceTexture;
                if (!sourceTextures.TryGetValue(texturePath, out sourceTexture))
                {
                    sourceTexture = ReadFontTexture(_ttmpFont, texturePath);
                    sourceTextures[texturePath] = sourceTexture;
                }

                Texture targetTexture;
                if (!targetTextures.TryGetValue(texturePath, out targetTexture))
                {
                    targetTexture = ReadFontTexture(_patchedFont, texturePath);
                    targetTextures[texturePath] = targetTexture;
                }

                Texture cleanTexture = ReadFontTexture(_cleanFont, texturePath);

                long score = 0;
                int firstDiffX = int.MinValue;
                int firstDiffY = int.MinValue;
                int firstSourceAlpha = 0;
                int firstTargetAlpha = 0;
                int firstCleanAlpha = 0;
                int channel = sourceGlyph.ImageIndex % 4;
                int width = sourceGlyph.Width + ThirdPartyGameFontHangulPadding * 2;
                int height = sourceGlyph.Height + ThirdPartyGameFontHangulPadding * 2;
                for (int y = -ThirdPartyGameFontHangulPadding; y < sourceGlyph.Height + ThirdPartyGameFontHangulPadding; y++)
                {
                    int sourceY = sourceGlyph.Y + y;
                    int targetY = targetGlyph.Y + y;
                    for (int x = -ThirdPartyGameFontHangulPadding; x < sourceGlyph.Width + ThirdPartyGameFontHangulPadding; x++)
                    {
                        int sourceX = sourceGlyph.X + x;
                        int targetX = targetGlyph.X + x;
                        int sourceAlpha = ReadFontTextureAlphaOrZero(sourceTexture, sourceGlyph.ImageIndex, sourceX, sourceY, channel);
                        int targetAlpha = ReadFontTextureAlphaOrZero(targetTexture, targetGlyph.ImageIndex, targetX, targetY, channel);
                        if (firstDiffX == int.MinValue && sourceAlpha != targetAlpha)
                        {
                            firstDiffX = x;
                            firstDiffY = y;
                            firstSourceAlpha = sourceAlpha;
                            firstTargetAlpha = targetAlpha;
                            firstCleanAlpha = ReadFontTextureAlphaOrZero(cleanTexture, sourceGlyph.ImageIndex, sourceX, sourceY, channel);
                        }

                        score += Math.Abs(sourceAlpha - targetAlpha);
                    }
                }

                if (score == 0)
                {
                    return true;
                }

                error = "score=" + score.ToString() +
                    ", texture=" + texturePath +
                    ", route=" + FormatGlyphEntryRoute(targetGlyph) +
                    ", firstDiff=" + firstDiffX.ToString() + "," + firstDiffY.ToString() +
                    ":" + firstSourceAlpha.ToString() + "->" + firstTargetAlpha.ToString() +
                    "(clean=" + firstCleanAlpha.ToString() + ")" +
                    ", size=" + width.ToString() + "x" + height.ToString();
                return false;
            }
        }
    }
}
