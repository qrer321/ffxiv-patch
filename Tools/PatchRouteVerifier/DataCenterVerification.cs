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

                if (string.Equals(region, "Japan", StringComparison.Ordinal))
                {
                    return "Japanese Data Center";
                }

                if (string.Equals(region, "North America", StringComparison.Ordinal))
                {
                    return "North American Data Center";
                }

                if (string.Equals(region, "Europe", StringComparison.Ordinal))
                {
                    return "European Data Center";
                }

                if (string.Equals(region, "Oceania", StringComparison.Ordinal))
                {
                    return "Oceanian Data Center";
                }

                return region + " Data Center";
            }

            private string GetLobbyRegionDataCenterLabel(string region)
            {
                if (!IsEnglishTargetLanguage())
                {
                    return region + " Data Center";
                }

                if (string.Equals(region, "Japan", StringComparison.Ordinal))
                {
                    return "Japanese Data Centers";
                }

                if (string.Equals(region, "North America", StringComparison.Ordinal))
                {
                    return "North American Data Centers";
                }

                if (string.Equals(region, "Europe", StringComparison.Ordinal))
                {
                    return "European Data Centers";
                }

                if (string.Equals(region, "Oceania", StringComparison.Ordinal))
                {
                    return "Oceanian Data Center";
                }

                return region + " Data Center";
            }

            private void VerifyDataCenterTitleUldRoute()
            {
                VerifyUldFontPreservation(DataCenterTitleUldPath, "data-center title", "data-center-title");
            }

            private void VerifyDataCenterWorldmapUldRoute()
            {
                VerifyUldFontPreservation(DataCenterWorldmapUldPath, "data-center world map", "data-center-worldmap");
            }

            private void VerifyUldFontPreservation(string uldPath, string label, string dumpGroup)
            {
                Console.WriteLine("[ULD/FDT] {0} font preservation", label);
                byte[] cleanUld = _cleanUi.ReadFile(uldPath);
                byte[] patchedUld = _patchedUi.ReadFile(uldPath);
                List<UldTextNodeFont> cleanFonts = GetUldTextNodeFonts(cleanUld);
                List<UldTextNodeFont> patchedFonts = GetUldTextNodeFonts(patchedUld);
                Dictionary<int, UldTextNodeFont> patchedByOffset = GetUldTextNodeFontsByOffset(patchedUld);

                if (cleanFonts.Count == 0)
                {
                    Fail("{0} clean ULD did not expose text-node fonts", uldPath);
                    return;
                }

                if (cleanFonts.Count != patchedFonts.Count)
                {
                    Fail(
                        "{0} text-node count changed: clean={1}, patched={2}",
                        uldPath,
                        cleanFonts.Count,
                        patchedFonts.Count);
                }

                HashSet<string> routedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cleanFonts.Count; i++)
                {
                    UldTextNodeFont cleanNode = cleanFonts[i];
                    UldTextNodeFont patchedNode;
                    if (!patchedByOffset.TryGetValue(cleanNode.NodeOffset, out patchedNode))
                    {
                        Fail("{0} missing patched text node at 0x{1:X}", uldPath, cleanNode.NodeOffset);
                        continue;
                    }

                    if (patchedNode.FontId != cleanNode.FontId || patchedNode.FontSize != cleanNode.FontSize)
                    {
                        Fail(
                            "{0} node 0x{1:X} font changed: clean={2}/{3}, patched={4}/{5}",
                            uldPath,
                            cleanNode.NodeOffset,
                            cleanNode.FontId,
                            cleanNode.FontSize,
                            patchedNode.FontId,
                            patchedNode.FontSize);
                        continue;
                    }

                    string resolvedFont = ResolveUldFontPath(patchedNode.FontId, patchedNode.FontSize, true);
                    if (resolvedFont == null)
                    {
                        Fail(
                            "{0} node 0x{1:X} font {2}/{3} has no verifier font mapping",
                            uldPath,
                            cleanNode.NodeOffset,
                            patchedNode.FontId,
                            patchedNode.FontSize);
                        continue;
                    }

                    routedFontPaths.Add(resolvedFont);
                    Pass(
                        "{0} node 0x{1:X} preserves font {2}/{3} routes to {4}",
                        uldPath,
                        cleanNode.NodeOffset,
                        patchedNode.FontId,
                        patchedNode.FontSize,
                        resolvedFont);
                }

                if (routedFontPaths.Count == 0)
                {
                    Fail("{0} did not route any {1} node to a verifiable font", uldPath, label);
                    return;
                }

                foreach (string fontPath in routedFontPaths)
                {
                    VerifyLabelGlyphsEqualClean(fontPath, DataCenterWorldmapLabels);
                    DumpLabelPreview(dumpGroup, fontPath, DataCenterWorldmapLabels);
                }
            }
        }

    }
}
