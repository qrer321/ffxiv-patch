namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] NumericGlyphSameFontChecks = new string[]
            {
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_12_lobby.fdt",
                "common/font/AXIS_14_lobby.fdt",
                "common/font/Jupiter_45.fdt",
                "common/font/Jupiter_45_lobby.fdt",
                "common/font/Jupiter_90.fdt",
                "common/font/Jupiter_90_lobby.fdt",
                "common/font/Jupiter_20_lobby.fdt",
                "common/font/Jupiter_23_lobby.fdt",
                "common/font/Jupiter_23.fdt",
                "common/font/Jupiter_46.fdt",
                "common/font/Jupiter_46_lobby.fdt",
                "common/font/Jupiter_16_lobby.fdt",
                "common/font/Meidinger_16.fdt",
                "common/font/Meidinger_16_lobby.fdt",
                "common/font/Meidinger_20.fdt",
                "common/font/Meidinger_20_lobby.fdt",
                "common/font/Meidinger_40.fdt",
                "common/font/Meidinger_40_lobby.fdt",
                "common/font/MiedingerMid_10.fdt",
                "common/font/MiedingerMid_10_lobby.fdt",
                "common/font/MiedingerMid_12.fdt",
                "common/font/MiedingerMid_12_lobby.fdt",
                "common/font/MiedingerMid_14.fdt",
                "common/font/MiedingerMid_14_lobby.fdt",
                "common/font/MiedingerMid_18.fdt",
                "common/font/MiedingerMid_18_lobby.fdt",
                "common/font/MiedingerMid_36.fdt",
                "common/font/MiedingerMid_36_lobby.fdt",
                "common/font/TrumpGothic_23.fdt",
                "common/font/TrumpGothic_23_lobby.fdt",
                "common/font/TrumpGothic_34.fdt",
                "common/font/TrumpGothic_34_lobby.fdt",
                "common/font/TrumpGothic_68.fdt",
                "common/font/TrumpGothic_68_lobby.fdt",
                "common/font/TrumpGothic_184.fdt",
                "common/font/TrumpGothic_184_lobby.fdt"
            };

            private static readonly uint[] NumericGlyphCodepoints = new uint[]
            {
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                '!', '+', '-', '.', ',', '%'
            };

            private static readonly string[,] NumericGlyphKoreanFontChecks = new string[,]
            {
                { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" }
            };

            private static readonly uint[] ProtectedHangulCodepoints = new uint[]
            {
                0xD638,
                0xD63C
            };

            private static readonly string[] ProtectedHangulFonts = new string[]
            {
                "common/font/AXIS_36.fdt",
                "common/font/MiedingerMid_36.fdt",
                "common/font/TrumpGothic_34.fdt"
            };

            private static readonly string[] PartyListSelfMarkerSameFontChecks = new string[]
            {
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/MiedingerMid_10.fdt",
                "common/font/MiedingerMid_12.fdt",
                "common/font/MiedingerMid_14.fdt",
                "common/font/MiedingerMid_18.fdt",
                "common/font/MiedingerMid_36.fdt"
            };

            private static readonly string[,] PartyListSelfMarkerKoreanFontChecks = new string[,]
            {
                { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" }
            };

            private static readonly string[] LobbyHangulVisibilityFonts = new string[]
            {
                "common/font/AXIS_12_lobby.fdt",
                "common/font/AXIS_14_lobby.fdt"
            };

            private static readonly uint[] LobbyHangulVisibilityCodepoints = new uint[]
            {
                0xB85C,
                0xC2A4,
                0xAC00,
                0xC774,
                0xC544
            };
        }
    }
}
