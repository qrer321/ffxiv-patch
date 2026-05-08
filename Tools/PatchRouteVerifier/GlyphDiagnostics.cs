using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int GlyphDumpScale = 4;

        private sealed partial class Verifier
        {
            private void VerifyLobbyPhraseGlyphDiagnostics()
            {
                Console.WriteLine("[FDT] Lobby phrase glyph diagnostics");
                uint[] codepoints = CollectCodepoints(new string[]
                {
                    // "character information change" lobby text and previously broken lobby words.
                    "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
                    "\uB85C\uC2A4\uAC00\uB974",
                    "\uB85C\uC2A4\uD2B8",
                    "\uB2C8\uBA54\uC774\uC544",
                    "\uADF8\uB9BC\uC790"
                });

                for (int i = 0; i < codepoints.Length; i++)
                {
                    VerifyAndDumpLobbyPhraseGlyph(codepoints[i]);
                }

                DumpLobbyPhraseSheets(codepoints);
            }

            private void VerifyDialoguePhraseGlyphDiagnostics()
            {
                Console.WriteLine("[FDT] Dialogue phrase glyph diagnostics");
                uint[] codepoints = CollectCodepoints(new string[]
                {
                    "\uD1A0\uB974\uB2F9 7\uC138\uCC9C\uB144\uC758 \uC545\uC5F0\uC744 \uB04A\uAE30 \uC704\uD55C \uC77C\uC774\uB2E4",
                    "\uC9C4\uC815\uD55C \uBCC0\uD601\uC744 \uC704\uD574\uC11C\uB77C\uBA74",
                    "\uBAB8\uC5D0 \uD76C\uC0DD\uB4E4\uC774 \uC5B4\uC5D0 \uB5A0\uC624\uB974\uB9AC",
                    "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0"
                });

                for (int i = 0; i < codepoints.Length; i++)
                {
                    VerifyAndDumpDialoguePhraseGlyph(codepoints[i]);
                }

                DumpDialoguePhraseSheets(codepoints);
            }

            private void VerifyAndDumpDialoguePhraseGlyph(uint codepoint)
            {
                bool foundAny = false;
                for (int fontIndex = 0; fontIndex < DialoguePhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = DialoguePhraseFontPaths[fontIndex];
                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for dialogue glyph diagnostics: {1}", fontPath, ex.Message);
                        continue;
                    }

                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        continue;
                    }

                    foundAny = true;
                    DumpAndCheckDialoguePhraseGlyph(fontPath, codepoint);
                }

                if (!foundAny)
                {
                    Fail("dialogue phrase U+{0:X4} was not found in any checked in-game font", codepoint);
                }
            }

            private void DumpAndCheckDialoguePhraseGlyph(string fontPath, uint codepoint)
            {
                try
                {
                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                    GlyphStats stats = AnalyzeGlyph(glyph);
                    DumpGlyph(DialoguePhraseGlyphGroup, fontPath, codepoint, glyph, stats);
                    if (glyph.VisiblePixels < 10)
                    {
                        ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} visible={2}, expected at least 10", glyph.VisiblePixels);
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, glyph, codepoint, '-'))
                    {
                        ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} matches fallback U+002D");
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, glyph, codepoint, '='))
                    {
                        ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} matches fallback U+003D");
                        return;
                    }

                    if (codepoint == 0xBCC0 && stats.ComponentCount >= 3 && stats.SmallComponentCount >= 3)
                    {
                        ReportDialogueGlyphIssue(
                            fontPath,
                            codepoint,
                            "{0} U+{1:X4} has suspected overlap artifact: components={2}/{3}",
                            stats.ComponentCount,
                            stats.SmallComponentCount);
                        return;
                    }

                    Pass(
                        "{0} U+{1:X4} visible={2}, components={3}/{4}",
                        fontPath,
                        codepoint,
                        glyph.VisiblePixels,
                        stats.ComponentCount,
                        stats.SmallComponentCount);
                }
                catch (Exception ex)
                {
                    Warn("{0} U+{1:X4} dialogue diagnostic error: {2}", fontPath, codepoint, ex.Message);
                }
            }

            private void ReportDialogueGlyphIssue(string fontPath, uint codepoint, string format, params object[] tailArgs)
            {
                object[] args = new object[2 + tailArgs.Length];
                args[0] = fontPath;
                args[1] = codepoint;
                for (int i = 0; i < tailArgs.Length; i++)
                {
                    args[i + 2] = tailArgs[i];
                }

                Fail(format, args);
            }

            private void VerifyAndDumpLobbyPhraseGlyph(uint codepoint)
            {
                GlyphCanvas reference;
                GlyphStats referenceStats;
                try
                {
                    reference = RenderGlyph(_patchedFont, LobbyPhraseReferenceFontPath, codepoint);
                    referenceStats = AnalyzeGlyph(reference);
                    DumpGlyph(LobbyPhraseGlyphGroup, LobbyPhraseReferenceFontPath, codepoint, reference, referenceStats);
                    Pass(
                        "{0} U+{1:X4} reference visible={2}, components={3}/{4}",
                        LobbyPhraseReferenceFontPath,
                        codepoint,
                        reference.VisiblePixels,
                        referenceStats.ComponentCount,
                        referenceStats.SmallComponentCount);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} reference diagnostic error: {2}", LobbyPhraseReferenceFontPath, codepoint, ex.Message);
                    return;
                }

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for lobby glyph diagnostics: {1}", fontPath, ex.Message);
                        continue;
                    }

                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        continue;
                    }

                    if (string.Equals(fontPath, LobbyPhraseReferenceFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CompareAndDumpLobbyPhraseGlyph(fontPath, codepoint, referenceStats);
                }
            }

            private void CompareAndDumpLobbyPhraseGlyph(string fontPath, uint codepoint, GlyphStats referenceStats)
            {
                try
                {
                    GlyphCanvas target = RenderGlyph(_patchedFont, fontPath, codepoint);
                    GlyphStats targetStats = AnalyzeGlyph(target);

                    DumpGlyph(LobbyPhraseGlyphGroup, fontPath, codepoint, target, targetStats);
                    if (target.VisiblePixels < 10)
                    {
                        Fail("{0} U+{1:X4} visible={2}, expected at least 10", fontPath, codepoint, target.VisiblePixels);
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, target, codepoint, '-'))
                    {
                        Fail("{0} U+{1:X4} matches fallback U+002D", fontPath, codepoint);
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, target, codepoint, '='))
                    {
                        Fail("{0} U+{1:X4} matches fallback U+003D", fontPath, codepoint);
                        return;
                    }

                    int extraComponents = targetStats.ComponentCount - referenceStats.ComponentCount;
                    int extraSmallComponents = targetStats.SmallComponentCount - referenceStats.SmallComponentCount;
                    if (string.Equals(fontPath, LobbyPhraseTargetFontPath, StringComparison.OrdinalIgnoreCase) &&
                        codepoint == 0xBCC0 &&
                        extraComponents > 0 &&
                        extraSmallComponents > 0)
                    {
                        Fail(
                            "{0} U+{1:X4} component profile suspicious: target components={2}/{3}, reference={4}/{5}",
                            fontPath,
                            codepoint,
                            targetStats.ComponentCount,
                            targetStats.SmallComponentCount,
                            referenceStats.ComponentCount,
                            referenceStats.SmallComponentCount);
                        return;
                    }

                    if (extraComponents > 1 && extraSmallComponents > 0)
                    {
                        Warn(
                            "{0} U+{1:X4} has more small components than reference: target={2}/{3}, reference={4}/{5}",
                            fontPath,
                            codepoint,
                            targetStats.ComponentCount,
                            targetStats.SmallComponentCount,
                            referenceStats.ComponentCount,
                            referenceStats.SmallComponentCount);
                        return;
                    }

                    Pass(
                        "{0} U+{1:X4} component profile target={2}/{3}, reference={4}/{5}",
                        fontPath,
                        codepoint,
                        targetStats.ComponentCount,
                        targetStats.SmallComponentCount,
                        referenceStats.ComponentCount,
                        referenceStats.SmallComponentCount);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} diagnostic error: {2}", fontPath, codepoint, ex.Message);
                }
            }

            private bool GlyphMatchesFallback(string fontPath, GlyphCanvas target, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return false;
                }

                try
                {
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fontPath, fallbackCodepoint);
                    return Diff(target.Alpha, fallback.Alpha) == 0 && target.VisiblePixels > 0;
                }
                catch
                {
                    return false;
                }
            }

            private static uint[] CollectCodepoints(string[] phrases)
            {
                List<uint> result = new List<uint>();
                HashSet<uint> seen = new HashSet<uint>();
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (codepoint <= 0x20)
                        {
                            continue;
                        }

                        if (seen.Add(codepoint))
                        {
                            result.Add(codepoint);
                        }
                    }
                }

                return result.ToArray();
            }

            private static GlyphStats AnalyzeGlyph(GlyphCanvas canvas)
            {
                GlyphStats stats = new GlyphStats();
                stats.MinX = GlyphCanvasSize;
                stats.MinY = GlyphCanvasSize;
                stats.MaxX = -1;
                stats.MaxY = -1;

                bool[] seen = new bool[canvas.Alpha.Length];
                int[] stack = new int[canvas.Alpha.Length];
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        int start = y * GlyphCanvasSize + x;
                        if (seen[start] || canvas.Alpha[start] == 0)
                        {
                            continue;
                        }

                        int area = 0;
                        int minX = x;
                        int minY = y;
                        int maxX = x;
                        int maxY = y;
                        int stackCount = 0;
                        stack[stackCount++] = start;
                        seen[start] = true;

                        while (stackCount > 0)
                        {
                            int current = stack[--stackCount];
                            int cx = current % GlyphCanvasSize;
                            int cy = current / GlyphCanvasSize;
                            area++;
                            if (cx < minX) minX = cx;
                            if (cy < minY) minY = cy;
                            if (cx > maxX) maxX = cx;
                            if (cy > maxY) maxY = cy;

                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0)
                                    {
                                        continue;
                                    }

                                    int nx = cx + dx;
                                    int ny = cy + dy;
                                    if (nx < 0 || ny < 0 || nx >= GlyphCanvasSize || ny >= GlyphCanvasSize)
                                    {
                                        continue;
                                    }

                                    int next = ny * GlyphCanvasSize + nx;
                                    if (seen[next] || canvas.Alpha[next] == 0)
                                    {
                                        continue;
                                    }

                                    seen[next] = true;
                                    stack[stackCount++] = next;
                                }
                            }
                        }

                        stats.ComponentCount++;
                        if (area >= 2 && area <= 96 && Math.Max(maxX - minX + 1, maxY - minY + 1) <= 18)
                        {
                            stats.SmallComponentCount++;
                        }

                        if (minX < stats.MinX) stats.MinX = minX;
                        if (minY < stats.MinY) stats.MinY = minY;
                        if (maxX > stats.MaxX) stats.MaxX = maxX;
                        if (maxY > stats.MaxY) stats.MaxY = maxY;
                    }
                }

                return stats;
            }

        }

        private struct GlyphStats
        {
            public int ComponentCount;
            public int SmallComponentCount;
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
        }
    }
}
