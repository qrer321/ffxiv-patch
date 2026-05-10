using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyCleanAsciiFontRoutes()
            {
                Console.WriteLine("[FDT] Clean ASCII glyph and kerning routes");
                HashSet<string> checkedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < LobbyPhraseFontPaths.Length; i++)
                {
                    string targetFontPath = LobbyPhraseFontPaths[i];
                    if (checkedTargets.Add(targetFontPath))
                    {
                        VerifyCleanAsciiFontRoute(ResolveCleanAsciiReferenceFontPath(targetFontPath), targetFontPath);
                    }
                }

                for (int i = 0; i < DialoguePhraseFontPaths.Length; i++)
                {
                    string targetFontPath = DialoguePhraseFontPaths[i];
                    if (checkedTargets.Add(targetFontPath))
                    {
                        VerifyCleanAsciiFontRoute(ResolveCleanAsciiReferenceFontPath(targetFontPath), targetFontPath);
                    }
                }
            }

            private void VerifyCleanAsciiFontRoute(string sourceFontPath, string targetFontPath)
            {
                string normalizedTarget = targetFontPath.Replace('\\', '/');
                if (!string.Equals(sourceFontPath, normalizedTarget, StringComparison.OrdinalIgnoreCase) &&
                    IsDerived4kLobbyFontPath(normalizedTarget))
                {
                    VerifyDerived4kLobbyAsciiFontRoute(sourceFontPath, targetFontPath);
                    return;
                }

                try
                {
                    byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                    uint[] codepoints = CreateAsciiCodepoints();
                    int checkedGlyphs = 0;
                    bool ok = true;

                    for (int i = 0; i < codepoints.Length; i++)
                    {
                        uint codepoint = codepoints[i];
                        FdtGlyphEntry sourceGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            continue;
                        }

                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} clean ASCII route missing U+{1:X4} from {2}", targetFontPath, codepoint, sourceFontPath);
                            ok = false;
                            continue;
                        }

                        if (!GlyphSpacingMetricsMatchOrLobbySafe(targetFontPath, codepoint, sourceGlyph, targetGlyph))
                        {
                            Fail(
                                "{0} U+{1:X4} spacing differs from {2}: target={3}, clean={4}",
                                targetFontPath,
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            ok = false;
                            continue;
                        }

                        GlyphCanvas sourceCanvas = RenderGlyph(_cleanFont, sourceFontPath, codepoint);
                        GlyphCanvas targetCanvas = RenderGlyph(_patchedFont, targetFontPath, codepoint);
                        long score = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                        if (score != 0)
                        {
                            Fail(
                                "{0} U+{1:X4} pixels differ from {2}: score={3}, visible={4}/{5}",
                                targetFontPath,
                                codepoint,
                                sourceFontPath,
                                score,
                                sourceCanvas.VisiblePixels,
                                targetCanvas.VisiblePixels);
                            ok = false;
                            continue;
                        }

                        checkedGlyphs++;
                    }

                    int checkedKerning = VerifyCleanAsciiKerningRoute(sourceFdt, targetFdt, sourceFontPath, targetFontPath, ref ok);
                    if (ok && checkedGlyphs > 0)
                    {
                        Pass("{0} clean ASCII route matches {1}: glyphs={2}, kerning={3}", targetFontPath, sourceFontPath, checkedGlyphs, checkedKerning);
                    }
                    else if (ok)
                    {
                        Warn("{0} clean ASCII route had no source glyphs in {1}", targetFontPath, sourceFontPath);
                    }
                }
                catch (Exception ex)
                {
                    Fail("{0} clean ASCII route check error: {1}", targetFontPath, ex.Message);
                }
            }

            private void VerifyDerived4kLobbyAsciiFontRoute(string sourceFontPath, string targetFontPath)
            {
                try
                {
                    byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                    uint[] codepoints = CollectAsciiCodepoints(Derived4kLobbyRequiredHangulPhrases);
                    int checkedGlyphs = 0;
                    bool ok = true;

                    for (int i = 0; i < codepoints.Length; i++)
                    {
                        uint codepoint = codepoints[i];
                        FdtGlyphEntry sourceGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            continue;
                        }

                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} derived lobby ASCII route missing required U+{1:X4} from {2}", targetFontPath, codepoint, sourceFontPath);
                            ok = false;
                            continue;
                        }

                        if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                        {
                            Fail(
                                "{0} required U+{1:X4} spacing differs from derived source {2}: target={3}, source={4}",
                                targetFontPath,
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            ok = false;
                            continue;
                        }

                        GlyphCanvas sourceCanvas = RenderGlyph(_cleanFont, sourceFontPath, codepoint);
                        GlyphCanvas targetCanvas = RenderGlyph(_patchedFont, targetFontPath, codepoint);
                        long score = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                        if (score != 0)
                        {
                            Fail(
                                "{0} required U+{1:X4} pixels differ from derived source {2}: score={3}, visible={4}/{5}",
                                targetFontPath,
                                codepoint,
                                sourceFontPath,
                                score,
                                sourceCanvas.VisiblePixels,
                                targetCanvas.VisiblePixels);
                            ok = false;
                            continue;
                        }

                        checkedGlyphs++;
                    }

                    if (ok && checkedGlyphs > 0)
                    {
                        Pass("{0} derived lobby required ASCII route matches {1}: glyphs={2}", targetFontPath, sourceFontPath, checkedGlyphs);
                    }
                    else if (ok)
                    {
                        Warn("{0} derived lobby required ASCII route had no source glyphs in {1}", targetFontPath, sourceFontPath);
                    }
                }
                catch (Exception ex)
                {
                    Fail("{0} derived lobby ASCII route check error: {1}", targetFontPath, ex.Message);
                }
            }

            private static string ResolveCleanAsciiReferenceFontPath(string targetFontPath)
            {
                string normalized = targetFontPath.Replace('\\', '/');
                string derivedSource = ResolveLobbyHangulSourceFontPath(normalized);
                if (!string.Equals(derivedSource, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return derivedSource;
                }

                for (int i = 0; i < CleanAsciiReferenceRoutes.Length; i++)
                {
                    CleanAsciiReferenceRoute route = CleanAsciiReferenceRoutes[i];
                    if (normalized.IndexOf(route.TargetSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return route.SourceFontPath;
                    }
                }

                return normalized;
            }

            private static bool IsDerived4kLobbyFontPath(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    if (string.Equals(normalized, Derived4kLobbyFontPairs[i, 0], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static uint[] CollectAsciiCodepoints(string[] phrases)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (codepoint > 0x20u && codepoint <= 0x7Eu)
                        {
                            codepoints.Add(codepoint);
                        }
                    }
                }

                uint[] values = new uint[codepoints.Count];
                codepoints.CopyTo(values);
                Array.Sort(values);
                return values;
            }

            private static uint[] CreateAsciiCodepoints()
            {
                uint[] codepoints = new uint[0x7E - 0x21 + 1];
                for (int i = 0; i < codepoints.Length; i++)
                {
                    codepoints[i] = (uint)(0x21 + i);
                }

                return codepoints;
            }
        }
    }
}
