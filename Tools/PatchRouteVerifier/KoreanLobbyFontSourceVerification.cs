using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyKoreanLobbyFontSourceRoutes()
            {
                Console.WriteLine("[FDT] Korean client lobby font source routes");
                Pass("KrnAXIS lobby source routing is disabled; lobby AXIS Hangul must be covered by lobby-scale-font-sources");
            }
        }
    }
}
