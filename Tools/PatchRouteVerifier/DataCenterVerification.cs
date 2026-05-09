using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyDataCenterRows()
            {
                Console.WriteLine("[EXD] Data center selection labels");
                DataCenterLabelExpectation[] expectations = CreateDataCenterLabelExpectations();
                for (int i = 0; i < expectations.Length; i++)
                {
                    ExpectPrimaryDataCenterLabel(expectations[i]);
                }

                ExpectDataCenterConnectMessage(_language);
                if (IsEnglishTargetLanguage())
                {
                    ExpectTextContains("Lobby", 806, "Japan");
                    ExpectTextContains("Lobby", 806, "North America");
                    ExpectTextContains("Lobby", 806, "Europe");
                    ExpectTextContains("Lobby", 806, "Oceania");
                }
                else
                {
                    ExpectTextNotContains("Lobby", 806, "FINAL FANTASY XIV requires connecting");
                }
            }

            private void VerifyDataCenterRowsAllGlobalLanguageSlots()
            {
                Console.WriteLine("[EXD] Data center labels in all global language slots");
                DataCenterLabelExpectation[] expectations = CreateDataCenterLabelExpectations();

                for (int i = 0; i < expectations.Length; i++)
                {
                    for (int languageIndex = 0; languageIndex < DataCenterGlobalLanguages.Length; languageIndex++)
                    {
                        ExpectDataCenterLabel(expectations[i], DataCenterGlobalLanguages[languageIndex]);
                    }
                }

                for (int languageIndex = 0; languageIndex < DataCenterGlobalLanguages.Length; languageIndex++)
                {
                    ExpectDataCenterConnectMessage(DataCenterGlobalLanguages[languageIndex]);
                }
            }

            private void ExpectPrimaryDataCenterLabel(DataCenterLabelExpectation expectation)
            {
                if (expectation.AllowSubstring)
                {
                    ExpectTextContains(expectation.Sheet, expectation.RowId, expectation.Expected);
                    return;
                }

                ExpectText(expectation.Sheet, expectation.RowId, expectation.Expected);
            }

            private void ExpectDataCenterConnectMessage(string language)
            {
                string actual = GetFirstString(_patchedText, "Lobby", 808, language);
                bool ok = actual.IndexOf("\uB370\uC774\uD130 \uC13C\uD130", StringComparison.Ordinal) >= 0 &&
                          actual.IndexOf("\uC5D0 \uC811\uC18D \uC911\uC785\uB2C8\uB2E4.", StringComparison.Ordinal) >= 0 &&
                          actual.IndexOf("Connecting to the", StringComparison.Ordinal) < 0 &&
                          actual.IndexOf("\u30C7\u30FC\u30BF\u30BB\u30F3\u30BF\u30FC", StringComparison.Ordinal) < 0 &&
                          !LooksLikeMissingGlyphFallback(actual);
                if (ok)
                {
                    Pass("Lobby#808/{0} Korean data-center connect message", language);
                    return;
                }

                Fail("Lobby#808/{0} expected Korean data-center connect message, actual [{1}]", language, Escape(actual));
            }
        }

    }
}
