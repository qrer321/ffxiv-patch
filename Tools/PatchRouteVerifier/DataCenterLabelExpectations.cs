using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private DataCenterLabelExpectation[] CreateDataCenterLabelExpectations()
            {
                List<DataCenterLabelExpectation> expectations = new List<DataCenterLabelExpectation>();
                AddLobbyRegionLabels(expectations, 791, false);
                expectations.Add(new DataCenterLabelExpectation("Lobby", 800, IsEnglishTargetLanguage() ? "Data Center Selection" : "DATA CENTER SELECT"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 801, GetLobbyDataCenterPrompt()));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 802, "Data Center"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 803, IsEnglishTargetLanguage() ? "Information" : "INFORMATION"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 806, GetLobbyInformationBodySubstring(), true));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 809, GetLobbyCharacterListSubstring(), true));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 810, IsEnglishTargetLanguage() ? "Proceed" : "OK"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 811, IsEnglishTargetLanguage() ? "Cancel" : "\u30AD\u30E3\u30F3\u30BB\u30EB"));
                AddLobbyRegionLabels(expectations, 812, true);
                expectations.Add(new DataCenterLabelExpectation("Lobby", 816, "NA Cloud Data Center (Beta)"));
                expectations.Add(new DataCenterLabelExpectation("Lobby", 2002, "EXIT"));
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

        }
    }
}
