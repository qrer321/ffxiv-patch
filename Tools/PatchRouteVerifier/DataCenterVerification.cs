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
        }

    }
}
