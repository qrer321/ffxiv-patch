namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const string TextPrefix = "0a0000.win32";
        private const string FontPrefix = "000000.win32";
        private const string UiPrefix = "060000.win32";
        private const string DataCenterTitleUldPath = "ui/uld/Title_DataCenter.uld";
        private const string DataCenterWorldmapUldPath = "ui/uld/Title_Worldmap.uld";
        private const string DefaultGlyphDumpFolderName = "patch-route-glyph-dumps";
        private const string LobbyPhraseGlyphGroup = "lobby-phrase";
        private const string DialoguePhraseGlyphGroup = "dialogue-phrase";
        private const string LobbyPhraseTargetFontPath = "common/font/AXIS_14_lobby.fdt";
        private const string LobbyPhraseReferenceFontPath = "common/font/AXIS_12_lobby.fdt";
        private const int ProtectedHangulMinimumVisiblePixels = 300;
        private const uint UncompressedBlock = 32000;

        private static readonly string[] LobbyPhraseFontPaths = new string[]
        {
            "common/font/AXIS_12_lobby.fdt",
            "common/font/AXIS_14_lobby.fdt",
            "common/font/AXIS_18_lobby.fdt",
            "common/font/AXIS_36_lobby.fdt",
            "common/font/Jupiter_16_lobby.fdt",
            "common/font/Jupiter_20_lobby.fdt",
            "common/font/Jupiter_23_lobby.fdt",
            "common/font/Jupiter_45_lobby.fdt",
            "common/font/Jupiter_46_lobby.fdt",
            "common/font/Jupiter_90_lobby.fdt",
            "common/font/Meidinger_16_lobby.fdt",
            "common/font/Meidinger_20_lobby.fdt",
            "common/font/Meidinger_40_lobby.fdt",
            "common/font/MiedingerMid_10_lobby.fdt",
            "common/font/MiedingerMid_12_lobby.fdt",
            "common/font/MiedingerMid_14_lobby.fdt",
            "common/font/MiedingerMid_18_lobby.fdt",
            "common/font/MiedingerMid_36_lobby.fdt",
            "common/font/TrumpGothic_23_lobby.fdt",
            "common/font/TrumpGothic_34_lobby.fdt",
            "common/font/TrumpGothic_68_lobby.fdt",
            "common/font/TrumpGothic_184_lobby.fdt"
        };

        private static readonly string[,] Derived4kLobbyFontPairs = new string[,]
        {
            { "common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36.fdt" },
            { "common/font/Jupiter_46_lobby.fdt", "common/font/Jupiter_46.fdt" },
            { "common/font/Jupiter_90_lobby.fdt", "common/font/Jupiter_46.fdt" },
            { "common/font/Meidinger_40_lobby.fdt", "common/font/MiedingerMid_36.fdt" },
            { "common/font/MiedingerMid_36_lobby.fdt", "common/font/MiedingerMid_36.fdt" },
            { "common/font/TrumpGothic_68_lobby.fdt", "common/font/TrumpGothic_68.fdt" }
        };

        private static readonly string[] Derived4kLobbyRequiredHangulPhrases = new string[]
        {
            "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
            "\uC2DC\uC2A4\uD15C \uC124\uC815",
            "\uAE00\uAF34 \uD06C\uAE30",
            "\uD30C\uD2F0 \uBAA9\uB85D",
            "\uB370\uC774\uD130 \uC13C\uD130",
            "\uB370\uC774\uD130 \uC13C\uD130 Mana\uC5D0 \uC811\uC18D \uC911\uC785\uB2C8\uB2E4.",
            "\uC885\uB8CC",
            "\uB098\uAC00\uAE30",
            "\uCDE8\uC18C",
            "\uD655\uC778",
            "\uC989\uC2DC \uBC1C\uB3D9",
            "\uCD08\uC2B9\uB2EC \uB808\uBCA8"
        };

        private static readonly uint[] Derived4kLobbyRequiredHangulCodepoints = CreateHangulCodepoints(Derived4kLobbyRequiredHangulPhrases);

        private static readonly UldRouteCandidate[] StartScreenSystemSettingsUldCandidates = new UldRouteCandidate[]
        {
            new UldRouteCandidate("ui/uld/Config.uld", false),
            new UldRouteCandidate("ui/uld/ConfigSystem.uld", false),
            new UldRouteCandidate("ui/uld/ConfigCharacter.uld", false),
            new UldRouteCandidate("ui/uld/ConfigControl.uld", false),
            new UldRouteCandidate("ui/uld/ConfigLog.uld", false),
            new UldRouteCandidate("ui/uld/ConfigKey.uld", false),
            new UldRouteCandidate("ui/uld/ConfigPad.uld", false),
            new UldRouteCandidate("ui/uld/ConfigHud.uld", false),
            new UldRouteCandidate("ui/uld/SystemConfig.uld", false),
            new UldRouteCandidate("ui/uld/TitleConfig.uld", true),
            new UldRouteCandidate("ui/uld/TitleSystemConfig.uld", true)
        };

        private static readonly uint[] PartyListProtectedPuaGlyphs = new uint[]
        {
            0xE031, // self marker
            0xE037, // Occult Crescent / phantom level
            0xE0E1, 0xE0E2, 0xE0E3, 0xE0E4,
            0xE0E5, 0xE0E6, 0xE0E7, 0xE0E8
        };

        private static readonly string[] DialoguePhraseFontPaths = new string[]
        {
            "common/font/AXIS_12.fdt",
            "common/font/AXIS_14.fdt",
            "common/font/AXIS_18.fdt",
            "common/font/AXIS_36.fdt",
            "common/font/KrnAXIS_120.fdt",
            "common/font/KrnAXIS_140.fdt",
            "common/font/KrnAXIS_180.fdt",
            "common/font/KrnAXIS_360.fdt",
            "common/font/Jupiter_16.fdt",
            "common/font/Jupiter_20.fdt",
            "common/font/Jupiter_23.fdt",
            "common/font/Jupiter_45.fdt",
            "common/font/Jupiter_46.fdt",
            "common/font/Jupiter_90.fdt",
            "common/font/Meidinger_16.fdt",
            "common/font/Meidinger_20.fdt",
            "common/font/Meidinger_40.fdt",
            "common/font/MiedingerMid_10.fdt",
            "common/font/MiedingerMid_12.fdt",
            "common/font/MiedingerMid_14.fdt",
            "common/font/MiedingerMid_18.fdt",
            "common/font/MiedingerMid_36.fdt",
            "common/font/TrumpGothic_23.fdt",
            "common/font/TrumpGothic_34.fdt",
            "common/font/TrumpGothic_68.fdt",
            "common/font/TrumpGothic_184.fdt"
        };

        private struct UldRouteCandidate
        {
            public readonly string Path;
            public readonly bool UsesLobbyFonts;

            public UldRouteCandidate(string path, bool usesLobbyFonts)
            {
                Path = path;
                UsesLobbyFonts = usesLobbyFonts;
            }
        }
    }
}
