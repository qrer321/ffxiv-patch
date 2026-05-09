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
                ExpectTextColumn("MainCommand", 99, 0, "\uC124\uC815 \uACF5\uC720");
                ExpectTextColumn(
                    "MainCommand",
                    99,
                    4,
                    "\uB2E8\uCD95\uBC14\uB098 \uAC01\uC885 \uC124\uC815 \uB370\uC774\uD130\uB97C \uC11C\uBC84\uC5D0 \uC77C\uC2DC \uC800\uC7A5\uD558\uACE0 \uB2E4\uB978 \uCE90\uB9AD\uD130\uC640 \uACF5\uC720\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.");
                ExpectAnyTextColumnNotContains("MainCommand", 99, "\u30B3\u30F3\u30D5\u30A3\u30B0\u30B7\u30A7\u30A2");
                ExpectAnyTextColumnNotContains("MainCommand", 99, "\u30B3\u30F3\u30C6\u30F3\u30C4\u30B7\u30A7\u30A2");
                ExpectAnyTextColumnNotContains("MainCommand", 99, "Configuration Sharing");
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
