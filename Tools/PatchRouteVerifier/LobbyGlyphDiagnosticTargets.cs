using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyAndDumpLobbyPhraseGlyph(uint codepoint)
            {
                GlyphStats referenceStats;
                if (!TryRenderLobbyPhraseReference(codepoint, out referenceStats))
                {
                    return;
                }

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    if (ShouldSkipLobbyPhraseFont(fontPath, codepoint))
                    {
                        continue;
                    }

                    CompareAndDumpLobbyPhraseGlyph(fontPath, codepoint, referenceStats);
                }
            }

            private bool ShouldSkipLobbyPhraseFont(string fontPath, uint codepoint)
            {
                byte[] fdt;
                try
                {
                    fdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Warn("{0} could not be read for lobby glyph diagnostics: {1}", fontPath, ex.Message);
                    return true;
                }

                FdtGlyphEntry ignored;
                if (!TryFindGlyph(fdt, codepoint, out ignored))
                {
                    return true;
                }

                return string.Equals(fontPath, LobbyPhraseReferenceFontPath, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
