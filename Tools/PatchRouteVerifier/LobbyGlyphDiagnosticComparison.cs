using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void CompareAndDumpLobbyPhraseGlyph(string fontPath, uint codepoint, GlyphStats referenceStats)
            {
                try
                {
                    GlyphCanvas target = RenderGlyph(_patchedFont, fontPath, codepoint);
                    GlyphStats targetStats = AnalyzeGlyph(target);

                    DumpGlyph(LobbyPhraseGlyphGroup, fontPath, codepoint, target, targetStats);
                    if (LobbyGlyphHasInvalidShape(fontPath, codepoint, target, targetStats, referenceStats))
                    {
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

            private bool LobbyGlyphHasInvalidShape(
                string fontPath,
                uint codepoint,
                GlyphCanvas target,
                GlyphStats targetStats,
                GlyphStats referenceStats)
            {
                if (target.VisiblePixels < 10)
                {
                    Fail("{0} U+{1:X4} visible={2}, expected at least 10", fontPath, codepoint, target.VisiblePixels);
                    return true;
                }

                if (GlyphMatchesFallback(fontPath, target, codepoint, '-'))
                {
                    Fail("{0} U+{1:X4} matches fallback U+002D", fontPath, codepoint);
                    return true;
                }

                if (GlyphMatchesFallback(fontPath, target, codepoint, '='))
                {
                    Fail("{0} U+{1:X4} matches fallback U+003D", fontPath, codepoint);
                    return true;
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
                    return true;
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
                    return true;
                }

                return false;
            }
        }
    }
}
