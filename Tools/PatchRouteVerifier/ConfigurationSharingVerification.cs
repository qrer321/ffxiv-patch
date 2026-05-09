using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly KeyValuePair<uint, string>[] ConfigurationSharingAddonTitleRows = new KeyValuePair<uint, string>[]
            {
                new KeyValuePair<uint, string>(17300, "\uC124\uC815 \uACF5\uC720"),
                new KeyValuePair<uint, string>(17315, "\uACF5\uC720 \uB370\uC774\uD130 \uD655\uC778"),
                new KeyValuePair<uint, string>(17330, "\uC124\uC815 \uB370\uC774\uD130 \uC5C5\uB85C\uB4DC"),
                new KeyValuePair<uint, string>(17360, "\uACF5\uC720 \uB370\uC774\uD130 \uB2E4\uC6B4\uB85C\uB4DC"),
                new KeyValuePair<uint, string>(17380, "\uACF5\uC720 \uB370\uC774\uD130 \uB2E4\uC6B4\uB85C\uB4DC")
            };

            private static readonly uint[] ConfigurationSharingAddonSubtitleRows = new uint[] { 17301, 17316, 17331, 17361, 17381 };
            private static readonly string[] ConfigurationSharingForeignTerms = new string[]
            {
                "\u30B3\u30F3\u30D5\u30A3\u30B0\u30B7\u30A7\u30A2",
                "\u30B3\u30F3\u30C6\u30F3\u30C4\u30B7\u30A7\u30A2",
                "Configuration Sharing",
                "CONFIG SHARE",
                "Config Share"
            };

            private void VerifyConfigurationSharingRows()
            {
                Console.WriteLine("[EXD] Configuration Sharing labels");
                for (int languageIndex = 0; languageIndex < DataCenterGlobalLanguages.Length; languageIndex++)
                {
                    VerifyConfigurationSharingRows(DataCenterGlobalLanguages[languageIndex]);
                }
            }

            private void VerifyConfigurationSharingRows(string language)
            {
                ExpectTextColumn(
                    "MainCommand",
                    99,
                    0,
                    "\uC124\uC815 \uACF5\uC720",
                    language);
                ExpectTextColumn(
                    "MainCommand",
                    99,
                    4,
                    "\uB2E8\uCD95\uBC14\uB098 \uAC01\uC885 \uC124\uC815 \uB370\uC774\uD130\uB97C \uC11C\uBC84\uC5D0 \uC77C\uC2DC \uC800\uC7A5\uD558\uACE0 \uB2E4\uB978 \uCE90\uB9AD\uD130\uC640 \uACF5\uC720\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    language);
                ExpectNoConfigurationSharingForeignTerms("MainCommand", 99, language);

                for (int titleIndex = 0; titleIndex < ConfigurationSharingAddonTitleRows.Length; titleIndex++)
                {
                    KeyValuePair<uint, string> title = ConfigurationSharingAddonTitleRows[titleIndex];
                    uint subtitleRow = ConfigurationSharingAddonSubtitleRows[titleIndex];
                    ExpectText("Addon", title.Key, title.Value, language);
                    ExpectText("Addon", subtitleRow, string.Empty, language);
                    ExpectConfigurationSharingTitlePair(title.Key, subtitleRow, language);
                    ExpectNoConfigurationSharingForeignTerms("Addon", title.Key, language);
                    ExpectNoConfigurationSharingForeignTerms("Addon", subtitleRow, language);
                }
            }

            private void ExpectConfigurationSharingTitlePair(uint titleRow, uint subtitleRow, string language)
            {
                string title = GetFirstString(_patchedText, "Addon", titleRow, language);
                string subtitle = GetFirstString(_patchedText, "Addon", subtitleRow, language);
                if (title.Length > 0 && subtitle.Length == 0)
                {
                    Pass("Addon#{0}/#{1}/{2} title/subtitle separated", titleRow, subtitleRow, language);
                    return;
                }

                Fail(
                    "Addon#{0}/#{1}/{2} title/subtitle mismatch: title=[{3}], subtitle=[{4}]",
                    titleRow,
                    subtitleRow,
                    language,
                    Escape(title),
                    Escape(subtitle));
            }

            private void ExpectNoConfigurationSharingForeignTerms(string sheet, uint rowId, string language)
            {
                List<string> columns = GetStringColumns(_patchedText, sheet, rowId, language);
                for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    for (int termIndex = 0; termIndex < ConfigurationSharingForeignTerms.Length; termIndex++)
                    {
                        string term = ConfigurationSharingForeignTerms[termIndex];
                        if (columns[columnIndex].IndexOf(term, StringComparison.Ordinal) >= 0)
                        {
                            Fail("{0}#{1}/{2} column {3} still contains [{4}], value=[{5}]", sheet, rowId, language, columnIndex, term, Escape(columns[columnIndex]));
                            return;
                        }
                    }
                }

                Pass("{0}#{1}/{2} contains no foreign configuration-sharing title", sheet, rowId, language);
            }
        }
    }
}
