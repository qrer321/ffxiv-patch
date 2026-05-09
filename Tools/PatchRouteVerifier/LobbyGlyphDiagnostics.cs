using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyLobbyPhraseGlyphDiagnostics()
            {
                Console.WriteLine("[FDT] Lobby phrase glyph diagnostics");
                uint[] codepoints = CollectCodepoints(LobbyDiagnosticPhrases);

                for (int i = 0; i < codepoints.Length; i++)
                {
                    VerifyAndDumpLobbyPhraseGlyph(codepoints[i]);
                }

                DumpLobbyPhraseSheets(codepoints);
            }
        }
    }
}
