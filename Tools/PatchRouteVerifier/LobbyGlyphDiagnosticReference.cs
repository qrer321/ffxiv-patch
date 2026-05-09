using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private bool TryRenderLobbyPhraseReference(uint codepoint, out GlyphStats referenceStats)
            {
                referenceStats = new GlyphStats();
                try
                {
                    GlyphCanvas reference = RenderGlyph(_patchedFont, LobbyPhraseReferenceFontPath, codepoint);
                    referenceStats = AnalyzeGlyph(reference);
                    DumpGlyph(LobbyPhraseGlyphGroup, LobbyPhraseReferenceFontPath, codepoint, reference, referenceStats);
                    Pass(
                        "{0} U+{1:X4} reference visible={2}, components={3}/{4}",
                        LobbyPhraseReferenceFontPath,
                        codepoint,
                        reference.VisiblePixels,
                        referenceStats.ComponentCount,
                        referenceStats.SmallComponentCount);
                    return true;
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} reference diagnostic error: {2}", LobbyPhraseReferenceFontPath, codepoint, ex.Message);
                    return false;
                }
            }
        }
    }
}
