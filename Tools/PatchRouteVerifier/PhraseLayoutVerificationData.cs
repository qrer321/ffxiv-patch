using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly FontPair[] HighScaleAsciiFontPairs = new FontPair[]
            {
                new FontPair("common/font/AXIS_12.fdt", "common/font/AXIS_12.fdt"),
                new FontPair("common/font/AXIS_14.fdt", "common/font/AXIS_14.fdt"),
                new FontPair("common/font/AXIS_18.fdt", "common/font/AXIS_18.fdt"),
                new FontPair("common/font/AXIS_36.fdt", "common/font/AXIS_36.fdt"),
                new FontPair("common/font/AXIS_96.fdt", "common/font/AXIS_96.fdt"),
                new FontPair("common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt"),
                new FontPair("common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt"),
                new FontPair("common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt"),
                new FontPair("common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt"),
                new FontPair("common/font/AXIS_12_lobby.fdt", "common/font/AXIS_12_lobby.fdt"),
                new FontPair("common/font/AXIS_14_lobby.fdt", "common/font/AXIS_14_lobby.fdt"),
                new FontPair("common/font/AXIS_18_lobby.fdt", "common/font/AXIS_18_lobby.fdt"),
                new FontPair("common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36_lobby.fdt"),
                new FontPair("common/font/Jupiter_23.fdt", "common/font/Jupiter_23.fdt"),
                new FontPair("common/font/Jupiter_46.fdt", "common/font/Jupiter_46.fdt"),
                new FontPair("common/font/Jupiter_23_lobby.fdt", "common/font/Jupiter_23_lobby.fdt"),
                new FontPair("common/font/Jupiter_46_lobby.fdt", "common/font/Jupiter_46_lobby.fdt"),
                new FontPair("common/font/MiedingerMid_18.fdt", "common/font/MiedingerMid_18.fdt"),
                new FontPair("common/font/MiedingerMid_36.fdt", "common/font/MiedingerMid_36.fdt"),
                new FontPair("common/font/MiedingerMid_18_lobby.fdt", "common/font/MiedingerMid_18_lobby.fdt"),
                new FontPair("common/font/MiedingerMid_36_lobby.fdt", "common/font/MiedingerMid_36_lobby.fdt"),
                new FontPair("common/font/TrumpGothic_34.fdt", "common/font/TrumpGothic_34.fdt"),
                new FontPair("common/font/TrumpGothic_68.fdt", "common/font/TrumpGothic_68.fdt"),
                new FontPair("common/font/TrumpGothic_184.fdt", "common/font/TrumpGothic_184.fdt"),
                new FontPair("common/font/TrumpGothic_34_lobby.fdt", "common/font/TrumpGothic_34_lobby.fdt"),
                new FontPair("common/font/TrumpGothic_68_lobby.fdt", "common/font/TrumpGothic_68_lobby.fdt"),
                new FontPair("common/font/TrumpGothic_184_lobby.fdt", "common/font/TrumpGothic_184_lobby.fdt")
            };

            private static readonly string[] HighScaleAsciiPhrases = CreateHighScaleAsciiPhrases();

            private static readonly string[] SystemSettingsScaledFonts = new string[]
            {
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/AXIS_96.fdt",
                "common/font/KrnAXIS_180.fdt",
                "common/font/KrnAXIS_360.fdt",
                "common/font/Jupiter_23.fdt",
                "common/font/Jupiter_46.fdt",
                "common/font/MiedingerMid_18.fdt",
                "common/font/MiedingerMid_36.fdt",
                "common/font/TrumpGothic_34.fdt",
                "common/font/TrumpGothic_68.fdt",
                "common/font/TrumpGothic_184.fdt"
            };

            private static readonly string[] SystemSettingsScaledPhrases = CombinePhraseGroups(
                LobbyScaledHangulPhrases.StartScreenSystemSettings,
                LobbyScaledHangulPhrases.HighResolutionUiScaleOptions,
                LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);

            private static readonly string[] SystemSettingsMixedScalePhrases = CombinePhraseGroups(
                LobbyScaledHangulPhrases.HighResolutionUiScaleOptions,
                LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);

            private static readonly FontPair[] StartScreenMainMenuFontPairs = new FontPair[]
            {
                new FontPair("common/font/AXIS_12_lobby.fdt", "common/font/AXIS_12_lobby.fdt"),
                new FontPair("common/font/AXIS_14_lobby.fdt", "common/font/AXIS_14_lobby.fdt"),
                new FontPair("common/font/AXIS_18_lobby.fdt", "common/font/AXIS_18_lobby.fdt"),
                new FontPair("common/font/AXIS_36.fdt", "common/font/AXIS_36_lobby.fdt"),
                new FontPair("common/font/Jupiter_46.fdt", "common/font/Jupiter_46_lobby.fdt"),
                new FontPair("common/font/Jupiter_46.fdt", "common/font/Jupiter_90_lobby.fdt"),
                new FontPair("common/font/MiedingerMid_36.fdt", "common/font/Meidinger_40_lobby.fdt"),
                new FontPair("common/font/MiedingerMid_36.fdt", "common/font/MiedingerMid_36_lobby.fdt"),
                new FontPair("common/font/TrumpGothic_68.fdt", "common/font/TrumpGothic_68_lobby.fdt")
            };

            private static readonly string[] FourKLobbyPhrases = Derived4kLobbyRequiredHangulPhrases;

            private static string[] CombinePhraseGroups(params string[][] groups)
            {
                List<string> phrases = new List<string>();
                HashSet<string> seen = new HashSet<string>();
                for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    string[] group = groups[groupIndex];
                    for (int i = 0; i < group.Length; i++)
                    {
                        if (seen.Add(group[i]))
                        {
                            phrases.Add(group[i]);
                        }
                    }
                }

                return phrases.ToArray();
            }

            private static string[] CreateHighScaleAsciiPhrases()
            {
                List<string> phrases = new List<string>();
                phrases.Add("DATA CENTER SELECT");
                phrases.Add("INFORMATION");
                phrases.Add("Data Center Selection");
                phrases.Add("Information");
                phrases.Add("NA Cloud DC (Beta)");
                phrases.Add("NA Cloud Data Center (Beta)");
                phrases.Add("Proceed");
                phrases.Add("Cancel");
                phrases.Add("Exit");
                phrases.Add("HP 100%");
                phrases.Add("Lv. 100");
                phrases.Add("FHD 150% QHD 200% UHD 300%");
                AddLabels(phrases, WorldDcGroupTypeLabels);
                AddLabels(phrases, DataCenterWorldLabels);
                return phrases.ToArray();
            }

            private struct FontPair
            {
                public readonly string SourceFontPath;
                public readonly string TargetFontPath;

                public FontPair(string sourceFontPath, string targetFontPath)
                {
                    SourceFontPath = sourceFontPath;
                    TargetFontPath = targetFontPath;
                }
            }
        }
    }
}
