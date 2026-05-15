namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] LobbyDiagnosticPhrases = new string[]
            {
                "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
                "\uB85C\uC2A4\uAC00\uB974",
                "\uB85C\uC2A4\uD2B8",
                "\uB2C8\uBA54\uC774\uC544",
                "\uADF8\uB9BC\uC790"
            };

            private static readonly string[] DialogueDiagnosticPhrases = new string[]
            {
                "\uD1A0\uB974\uB2F9 7\uC138\uCC9C\uB144\uC758 \uC545\uC5F0\uC744 \uB04A\uAE30 \uC704\uD55C \uC77C\uC774\uB2E4",
                "\uC9C4\uC815\uD55C \uBCC0\uD601\uC744 \uC704\uD574\uC11C\uB77C\uBA74",
                "\uBAB8\uC5D0 \uD76C\uC0DD\uB4E4\uC774 \uC5B4\uC5D0 \uB5A0\uC624\uB974\uB9AC",
                "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0"
            };

            private static readonly string[] ReportedInGameHangulPhrases = new string[]
            {
                "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0",
                "\uC989\uC2DC \uBC1C\uB3D9",
                "\uC2DC\uC804 \uC2DC\uAC04",
                "\uC7AC\uC0AC\uC6A9 \uB300\uAE30 \uC2DC\uAC04",
                "\uBC1C\uB3D9 \uC870\uAC74",
                "\uC9C1\uACA9",
                "\uC9C1\uACA9!",
                "\uADF9\uB300",
                "\uADF9\uB300\uD654",
                "\uADF9\uB300\uD654!",
                "\uD06C\uB9AC\uD2F0\uCEEC",
                "\uADF9\uB300 \uC9C1\uACA9"
            };
        }
    }
}
