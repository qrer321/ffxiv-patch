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

            private static readonly string[] HighScaleAsciiPhrases = new string[]
            {
                "DATA CENTER SELECT",
                "INFORMATION",
                "Elemental",
                "NA Cloud DC (Beta)",
                "HP 100%",
                "Lv. 100",
                "150%"
            };

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

            private static readonly string[] SystemSettingsScaledPhrases = new string[]
            {
                "\uC2DC\uC2A4\uD15C \uC124\uC815 150%",
                "\uC2DC\uC2A4\uD15C \uC124\uC815 200%",
                "\uC2DC\uC2A4\uD15C \uC124\uC815 300%",
                "FHD 150% QHD 200% UHD 300%",
                "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0"
            };

            private static readonly string[] FourKLobbyPhrases = new string[]
            {
                "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
                "\uC2DC\uC2A4\uD15C \uC124\uC815",
                "\uAE00\uAF34 \uD06C\uAE30",
                "\uD30C\uD2F0 \uBAA9\uB85D",
                "\uB370\uC774\uD130 \uC13C\uD130",
                "\uCD08\uC2B9\uB2EC \uB808\uBCA8"
            };

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
