using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyConfigurationSharingRows()
            {
                Console.WriteLine("[EXD] Configuration Sharing labels");
                ExpectText("Addon", 17300, "\uC124\uC815 \uACF5\uC720");
                ExpectText("Addon", 17301, "\uC124\uC815 \uACF5\uC720");
                ExpectAnyTextColumnNotContains("Addon", 17300, "\u30B3\u30F3\u30D5\u30A3\u30B0\u30B7\u30A7\u30A2");
                ExpectAnyTextColumnNotContains("Addon", 17300, "\u30B3\u30F3\u30C6\u30F3\u30C4\u30B7\u30A7\u30A2");
                ExpectAnyTextColumnNotContains("Addon", 17300, "Configuration Sharing");
                ExpectTextNotContains("Addon", 17301, "\u30B3\u30F3\u30D5\u30A3\u30B0\u30B7\u30A7\u30A2");
                ExpectTextNotContains("Addon", 17301, "\u30B3\u30F3\u30C6\u30F3\u30C4\u30B7\u30A7\u30A2");
                ExpectTextNotContains("Addon", 17301, "Configuration Sharing");
            }
        }
    }
}
