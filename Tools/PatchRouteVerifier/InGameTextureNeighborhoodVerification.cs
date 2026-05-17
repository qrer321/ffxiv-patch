using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int InGameGlyphTextureNeighborhoodPadding = 8;

        private sealed partial class Verifier
        {
            private void VerifyInGameTtmpTextureNeighborhoods()
            {
                Console.WriteLine("[FDT] In-game TTMP texture neighborhoods");
                if (_ttmpFont == null)
                {
                    Warn("In-game TTMP texture neighborhood verification skipped; pass --font-pack-dir with TTMPD.mpd and TTMPL.mpl");
                    return;
                }

                bool ok = true;
                VerifyInGameTextureDimensions(ref ok);
                VerifyInGameHangulTextureNeighborhoods(ref ok);

                if (ok)
                {
                    Pass("In-game TTMP texture dimensions and Hangul neighborhoods preserved");
                }
            }

            private void VerifyInGameTextureDimensions(ref bool ok)
            {
                string[] texturePaths = new string[]
                {
                    Font1TexturePath,
                    Font2TexturePath,
                    Font3TexturePath,
                    Font4TexturePath,
                    Font5TexturePath,
                    Font6TexturePath,
                    Font7TexturePath,
                    FontKrnTexturePath
                };

                for (int i = 0; i < texturePaths.Length; i++)
                {
                    string texturePath = texturePaths[i];
                    if (!_ttmpFont.ContainsPath(texturePath))
                    {
                        continue;
                    }

                    try
                    {
                        Texture source = ReadFontTexture(_ttmpFont, texturePath);
                        Texture target = ReadFontTexture(_patchedFont, texturePath);
                        if (source.Width == target.Width && source.Height == target.Height)
                        {
                            continue;
                        }

                        Fail(
                            "{0} dimensions differ from TTMP source: patched={1}x{2}, source={3}x{4}",
                            texturePath,
                            target.Width,
                            target.Height,
                            source.Width,
                            source.Height);
                        ok = false;
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} dimension check error: {1}", texturePath, ex.Message);
                        ok = false;
                    }
                }
            }

            private void VerifyInGameHangulTextureNeighborhoods(ref bool ok)
            {
                HashSet<string> fontPaths = CollectHangulSourcePreservationFontPaths();
                uint[] codepoints = CollectHangulSourcePreservationCodepoints();
                HashSet<uint> actionDetailHighScaleCodepoints = CollectActionDetailHighScaleHangulCodepointSet();
                int compared = 0;
                int skippedIntentional = 0;

                foreach (string fontPath in fontPaths)
                {
                    if (!_ttmpFont.ContainsPath(fontPath) || IsLobbyFontPath(fontPath))
                    {
                        continue;
                    }

                    int failuresForFont = 0;
                    for (int i = 0; i < codepoints.Length; i++)
                    {
                        uint codepoint = codepoints[i];
                        if (IsIntentionalHangulSourceChange(fontPath, codepoint, actionDetailHighScaleCodepoints))
                        {
                            skippedIntentional++;
                            continue;
                        }

                        byte[] sourceFdt;
                        try
                        {
                            sourceFdt = _ttmpFont.ReadFile(fontPath);
                        }
                        catch (Exception ex)
                        {
                            Fail("{0} source neighborhood read error: {1}", fontPath, ex.Message);
                            ok = false;
                            break;
                        }

                        FdtGlyphEntry sourceGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            continue;
                        }

                        string error;
                        if (!VerifyGlyphTextureNeighborhoodMatchesTtmpSource(
                            fontPath,
                            fontPath,
                            codepoint,
                            InGameGlyphTextureNeighborhoodPadding,
                            out error))
                        {
                            Fail(
                                "{0} U+{1:X4} TTMP neighborhood differs: {2}",
                                fontPath,
                                codepoint,
                                error);
                            ok = false;
                            failuresForFont++;
                            if (failuresForFont >= MaxTexturePaddingFailuresPerFont)
                            {
                                Warn("{0} neighborhood check stopped after {1} failures", fontPath, failuresForFont);
                                break;
                            }
                        }
                        else
                        {
                            compared++;
                        }
                    }
                }

                if (compared == 0)
                {
                    Fail("No in-game Hangul texture neighborhoods were compared against TTMP source");
                    ok = false;
                    return;
                }

                if (skippedIntentional > 0)
                {
                    Pass("Skipped intentional action-detail high-scale neighborhood changes: {0}", skippedIntentional);
                }
            }
        }
    }
}
