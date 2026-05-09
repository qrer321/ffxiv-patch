using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void DumpAndCheckDialoguePhraseGlyph(string fontPath, uint codepoint)
            {
                try
                {
                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                    GlyphStats stats = AnalyzeGlyph(glyph);
                    DumpGlyph(DialoguePhraseGlyphGroup, fontPath, codepoint, glyph, stats);
                    if (DialogueGlyphHasInvalidShape(fontPath, codepoint, glyph, stats))
                    {
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

            private bool DialogueGlyphHasInvalidShape(string fontPath, uint codepoint, GlyphCanvas glyph, GlyphStats stats)
            {
                if (glyph.VisiblePixels < 10)
                {
                    ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} visible={2}, expected at least 10", glyph.VisiblePixels);
                    return true;
                }

                if (GlyphMatchesFallback(fontPath, glyph, codepoint, '-'))
                {
                    ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} matches fallback U+002D");
                    return true;
                }

                if (GlyphMatchesFallback(fontPath, glyph, codepoint, '='))
                {
                    ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} matches fallback U+003D");
                    return true;
                }

                if (codepoint == 0xBCC0 && stats.ComponentCount >= 3 && stats.SmallComponentCount >= 3)
                {
                    ReportDialogueGlyphIssue(
                        fontPath,
                        codepoint,
                        "{0} U+{1:X4} has suspected overlap artifact: components={2}/{3}",
                        stats.ComponentCount,
                        stats.SmallComponentCount);
                    return true;
                }

                return false;
            }
        }
    }
}
