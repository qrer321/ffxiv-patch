using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

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
        private static readonly uint[] Derived4kLobbyRequiredHangulCodepoints = new uint[]
        {
            0xCE90, 0xB9AD, 0xD130, 0xC815, 0xBCF4, 0xB97C, 0xBCC0, 0xACBD,
            0xD558, 0xAE30, 0xC704, 0xD574, 0xB85C, 0xC2A4, 0xAC00, 0xB974,
            0xD2B8, 0xB2C8, 0xBA54, 0xC774, 0xC544, 0xADF8, 0xB9BC, 0xC790,
            0xB370, 0xC13C, 0xC2DC, 0xD15C, 0xC124, 0xAE00, 0xAF34, 0xD06C,
            0xD45C, 0xD30C, 0xD2F0, 0xBAA9, 0xB85D, 0xC785, 0xC7A5, 0xC548,
            0xB0B4, 0xCD08, 0xC2B9, 0xB2EC, 0xB808, 0xBCA8
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
        private const int ProtectedHangulMinimumVisiblePixels = 300;
        private const uint UncompressedBlock = 32000;
        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Options options = Options.Parse(args);
                if (options.ShowHelp)
                {
                    PrintUsage();
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(options.OutputPath) ||
                    string.IsNullOrWhiteSpace(options.GlobalGamePath))
                {
                    PrintUsage();
                    return 2;
                }

                string output = Path.GetFullPath(options.OutputPath);
                string globalGame = Path.GetFullPath(options.GlobalGamePath);
                string globalSqpack = Path.Combine(globalGame, "sqpack", "ffxiv");
                string appliedSqpack = string.IsNullOrWhiteSpace(options.AppliedGamePath)
                    ? output
                    : Path.Combine(Path.GetFullPath(options.AppliedGamePath), "sqpack", "ffxiv");
                string language = string.IsNullOrWhiteSpace(options.TargetLanguage) ? "ja" : options.TargetLanguage;
                string glyphDumpDir = options.NoGlyphDump
                    ? null
                    : (string.IsNullOrWhiteSpace(options.GlyphDumpDir)
                        ? Path.Combine(output, DefaultGlyphDumpFolderName)
                        : Path.GetFullPath(options.GlyphDumpDir));

                Verifier verifier = new Verifier(output, appliedSqpack, globalSqpack, language, glyphDumpDir);
                verifier.Run();
                return verifier.Failed ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("verification failed with exception");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("PatchRouteVerifier.exe --output <patch-output-dir> --global <global-game-dir> [--applied-game <game-dir>] [--target-language ja] [--glyph-dump-dir <dir>] [--no-glyph-dump]");
        }

        private sealed partial class Verifier
        {
            private readonly string _output;
            private readonly string _patchedSqpack;
            private readonly string _globalSqpack;
            private readonly string _language;
            private readonly CompositeArchive _patchedText;
            private readonly CompositeArchive _patchedFont;
            private readonly CompositeArchive _patchedUi;
            private readonly CompositeArchive _cleanFont;
            private readonly CompositeArchive _cleanUi;
            private readonly Dictionary<string, byte[]> _textureCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            private readonly string _glyphDumpDir;

            public bool Failed { get; private set; }

            public Verifier(string output, string patchedSqpack, string globalSqpack, string language, string glyphDumpDir)
            {
                _output = output;
                _patchedSqpack = patchedSqpack;
                _globalSqpack = globalSqpack;
                _language = language;
                _glyphDumpDir = glyphDumpDir;

                _patchedText = new CompositeArchive(
                    Path.Combine(patchedSqpack, TextPrefix + ".index"),
                    patchedSqpack,
                    globalSqpack,
                    TextPrefix);
                _patchedFont = new CompositeArchive(
                    Path.Combine(patchedSqpack, FontPrefix + ".index"),
                    patchedSqpack,
                    globalSqpack,
                    FontPrefix);
                _patchedUi = new CompositeArchive(
                    Path.Combine(patchedSqpack, UiPrefix + ".index"),
                    patchedSqpack,
                    globalSqpack,
                    UiPrefix);
                _cleanFont = new CompositeArchive(
                    Path.Combine(output, "orig." + FontPrefix + ".index"),
                    globalSqpack,
                    globalSqpack,
                    FontPrefix);
                _cleanUi = new CompositeArchive(
                    Path.Combine(output, "orig." + UiPrefix + ".index"),
                    globalSqpack,
                    globalSqpack,
                    UiPrefix);

                if (!string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    Directory.CreateDirectory(_glyphDumpDir);
                    File.WriteAllText(
                        Path.Combine(_glyphDumpDir, "glyph-report.tsv"),
                        "group\tfont\tcodepoint\tchar\tvisible\tcomponents\tsmall_components\tbbox\timage_index\ttexture\tx\ty\twidth\theight\toffset_x\toffset_y\tpng" + Environment.NewLine,
                        Encoding.UTF8);
                }
            }

            public void Run()
            {
                using (_patchedText)
                using (_patchedFont)
                using (_patchedUi)
                using (_cleanFont)
                using (_cleanUi)
                {
                    Console.WriteLine("Patch route verification");
                    Console.WriteLine("  output: {0}", _output);
                    Console.WriteLine("  patched sqpack: {0}", _patchedSqpack);
                    Console.WriteLine("  global sqpack: {0}", _globalSqpack);
                    if (!string.IsNullOrWhiteSpace(_glyphDumpDir))
                    {
                        Console.WriteLine("  glyph dumps: {0}", _glyphDumpDir);
                    }

                    Console.WriteLine();

                    VerifyDataCenterRows();
                    VerifyDataCenterRowsAllGlobalLanguageSlots();
                    VerifyDataCenterTitleUldRoute();
                    VerifyDataCenterWorldmapUldRoute();
                    VerifyCompactTimeRows();
                    VerifyWorldVisitRows();
                    VerifyConfigurationSharingRows();
                    VerifyBozjaEntranceRows();
                    VerifyOccultCrescentSupportJobRows();
                    VerifyDataCenterTitleGlyphs();
                    VerifyCleanAsciiFontRoutes();
                    VerifyHighScaleAsciiPhraseLayouts();
                    VerifySystemSettingsScaledPhraseLayouts();
                    Verify4kLobbyFontDerivations();
                    Verify4kLobbyPhraseLayouts();
                    VerifyNumericGlyphs();
                    VerifyProtectedHangulGlyphs();
                    VerifyPartyListSelfMarker();
                    VerifyLobbyHangulVisibility();
                    VerifyLobbyPhraseGlyphDiagnostics();
                    VerifyDialoguePhraseGlyphDiagnostics();

                    Console.WriteLine();
                    Console.WriteLine(Failed ? "RESULT: FAIL" : "RESULT: PASS");
                }
            }

        }

    }
}
