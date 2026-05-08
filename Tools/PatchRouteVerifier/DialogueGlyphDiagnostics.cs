using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyDialoguePhraseGlyphDiagnostics()
            {
                Console.WriteLine("[FDT] Dialogue phrase glyph diagnostics");
                uint[] codepoints = CollectCodepoints(DialogueDiagnosticPhrases);

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
        }
    }
}
