using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
