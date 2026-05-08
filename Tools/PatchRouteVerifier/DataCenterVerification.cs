using System;
using System.Collections.Generic;

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

            private DataCenterLabelExpectation[] CreateDataCenterLabelExpectations()
            {
                List<DataCenterLabelExpectation> expectations = new List<DataCenterLabelExpectation>();
                AddLobbyRegionLabels(expectations, 791, false);
                expectations.Add(new DataCenterLabelExpectation("Lobby", 800, IsEnglishTargetLanguage() ? "Data Center Selection" : "DATA CENTER SELECT"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 801, GetLobbyDataCenterPrompt()));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 802, "Data Center"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 803, IsEnglishTargetLanguage() ? "Information" : "INFORMATION"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 806, GetLobbyInformationBodySubstring(), true));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 808, GetLobbyDataCenterConnectingSubstring(), true));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 809, GetLobbyCharacterListSubstring(), true));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 810, IsEnglishTargetLanguage() ? "Proceed" : "OK"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 811, IsEnglishTargetLanguage() ? "Cancel" : "\u30AD\u30E3\u30F3\u30BB\u30EB"));
                AddLobbyRegionLabels(expectations, 812, true);
                expectations.Add(new DataCenterLabelExpectation("Lobby", 816, "NA Cloud Data Center (Beta)"));
                AddOneBasedLabelExpectations(expectations, "WorldRegionGroup", WorldRegionGroupLabels);
                AddOneBasedLabelExpectations(expectations, "WorldPhysicalDC", WorldPhysicalDcLabels);
                AddOneBasedLabelExpectations(expectations, "WorldDCGroupType", WorldDcGroupTypeLabels);
                return expectations.ToArray();
            }

            private void AddLobbyRegionLabels(List<DataCenterLabelExpectation> expectations, uint firstRowId, bool regionalHeading)
            {
                for (int i = 0; i < DataCenterRegions.Length; i++)
                {
                    string expected = regionalHeading
                        ? GetLobbyRegionDataCenterLabel(DataCenterRegions[i])
                        : GetLobbyDataCenterLabel(DataCenterRegions[i]);
                    expectations.Add(new DataCenterLabelExpectation("Lobby", firstRowId + (uint)i, expected));
                }
            }

            private static void AddOneBasedLabelExpectations(List<DataCenterLabelExpectation> expectations, string sheet, string[] labels)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    expectations.Add(new DataCenterLabelExpectation(sheet, (uint)(i + 1), labels[i]));
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

            private bool IsEnglishTargetLanguage()
            {
                return string.Equals(_language, "en", StringComparison.OrdinalIgnoreCase);
            }

            private string GetLobbyInformationBodySubstring()
            {
                if (IsEnglishTargetLanguage())
                {
                    return "FINAL FANTASY XIV requires connecting";
                }

                return "\u30D5\u30A1\u30A4\u30CA\u30EB\u30D5\u30A1\u30F3\u30BF\u30B8\u30FCXIV";
            }

            private string GetLobbyDataCenterPrompt()
            {
                if (IsEnglishTargetLanguage())
                {
                    return string.Empty;
                }

                return "\u63A5\u7D9A\u3059\u308B\u30C7\u30FC\u30BF\u30BB\u30F3\u30BF\u30FC\u3092\u9078\u629E";
            }

            private string GetLobbyDataCenterConnectingSubstring()
            {
                if (IsEnglishTargetLanguage())
                {
                    return "Connecting to the";
                }

                return "\u30C7\u30FC\u30BF\u30BB\u30F3\u30BF\u30FC";
            }

            private string GetLobbyCharacterListSubstring()
            {
                if (IsEnglishTargetLanguage())
                {
                    return "Acquiring character list.";
                }

                return "\u30AD\u30E3\u30E9\u30AF\u30BF\u30FC\u30EA\u30B9\u30C8";
            }

            private string GetLobbyDataCenterLabel(string region)
            {
                if (!IsEnglishTargetLanguage())
                {
                    return region + " Data Center";
                }

                return GetEnglishDataCenterRegionLabel(region, false);
            }

            private string GetLobbyRegionDataCenterLabel(string region)
            {
                if (!IsEnglishTargetLanguage())
                {
                    return region + " Data Center";
                }

                return GetEnglishDataCenterRegionLabel(region, true);
            }

            private static string GetEnglishDataCenterRegionLabel(string region, bool plural)
            {
                for (int i = 0; i < DataCenterRegionLabels.Length; i++)
                {
                    if (string.Equals(region, DataCenterRegionLabels[i].Region, StringComparison.Ordinal))
                    {
                        return plural ? DataCenterRegionLabels[i].Plural : DataCenterRegionLabels[i].Singular;
                    }
                }

                return region + " Data Center";
            }

        }

    }
}
