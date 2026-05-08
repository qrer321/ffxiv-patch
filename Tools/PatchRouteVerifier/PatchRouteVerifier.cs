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
        private const string Font1TexturePath = "common/font/font1.tex";
        private const string Font2TexturePath = "common/font/font2.tex";
        private const string Font3TexturePath = "common/font/font3.tex";
        private const string Font4TexturePath = "common/font/font4.tex";
        private const string Font5TexturePath = "common/font/font5.tex";
        private const string Font6TexturePath = "common/font/font6.tex";
        private const string Font7TexturePath = "common/font/font7.tex";
        private const string FontLobby1TexturePath = "common/font/font_lobby1.tex";
        private const string FontLobby2TexturePath = "common/font/font_lobby2.tex";
        private const string FontLobby3TexturePath = "common/font/font_lobby3.tex";
        private const string FontLobby4TexturePath = "common/font/font_lobby4.tex";
        private const string FontLobby5TexturePath = "common/font/font_lobby5.tex";
        private const string FontLobby6TexturePath = "common/font/font_lobby6.tex";
        private const string FontLobby7TexturePath = "common/font/font_lobby7.tex";
        private const string FontKrnTexturePath = "common/font/font_krn_1.tex";
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
        private const int FdtHeaderSize = 0x20;
        private const int FdtFontTableHeaderSize = 0x20;
        private const int FdtGlyphEntrySize = 0x10;
        private const int FdtKerningHeaderSize = 0x10;
        private const int FdtKerningEntrySize = 0x10;
        private const int GlyphCanvasSize = 96;
        private const int GlyphDumpScale = 4;
        private const int ProtectedHangulMinimumVisiblePixels = 300;
        private const byte UldTrumpGothicFontId = 3;
        private const byte UldJupiterFontId = 4;
        private const byte UldMiedingerMedFontId = 1;
        private const byte UldAxisFontId = 0;
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

            private void VerifyDataCenterTitleGlyphs()
            {
                Console.WriteLine("[FDT] Data center title ASCII glyphs");
                string[] sameFontChecks =
                {
                    "common/font/AXIS_12.fdt",
                    "common/font/AXIS_14.fdt",
                    "common/font/AXIS_18.fdt",
                    "common/font/AXIS_36.fdt",
                    "common/font/AXIS_96.fdt",
                    "common/font/AXIS_12_lobby.fdt",
                    "common/font/AXIS_14_lobby.fdt",
                    "common/font/AXIS_18_lobby.fdt",
                    "common/font/AXIS_36_lobby.fdt"
                };
                uint[] codepoints = CreateAsciiCodepoints();
                for (int f = 0; f < sameFontChecks.Length; f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, sameFontChecks[f], codepoints[c], _patchedFont, sameFontChecks[f], codepoints[c]);
                    }
                }

                string[,] krnFontChecks =
                {
                    { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                    { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                    { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                    { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" }
                };
                for (int f = 0; f < krnFontChecks.GetLength(0); f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, krnFontChecks[f, 0], codepoints[c], _patchedFont, krnFontChecks[f, 1], codepoints[c]);
                    }
                }
            }

            private void VerifyCleanAsciiFontRoutes()
            {
                Console.WriteLine("[FDT] Clean ASCII glyph and kerning routes");
                HashSet<string> checkedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < LobbyPhraseFontPaths.Length; i++)
                {
                    string targetFontPath = LobbyPhraseFontPaths[i];
                    if (checkedTargets.Add(targetFontPath))
                    {
                        VerifyCleanAsciiFontRoute(ResolveCleanAsciiReferenceFontPath(targetFontPath), targetFontPath);
                    }
                }

                for (int i = 0; i < DialoguePhraseFontPaths.Length; i++)
                {
                    string targetFontPath = DialoguePhraseFontPaths[i];
                    if (checkedTargets.Add(targetFontPath))
                    {
                        VerifyCleanAsciiFontRoute(ResolveCleanAsciiReferenceFontPath(targetFontPath), targetFontPath);
                    }
                }
            }

            private void VerifyCleanAsciiFontRoute(string sourceFontPath, string targetFontPath)
            {
                try
                {
                    byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                    uint[] codepoints = CreateAsciiCodepoints();
                    int checkedGlyphs = 0;
                    bool ok = true;

                    for (int i = 0; i < codepoints.Length; i++)
                    {
                        uint codepoint = codepoints[i];
                        FdtGlyphEntry sourceGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            continue;
                        }

                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} clean ASCII route missing U+{1:X4} from {2}", targetFontPath, codepoint, sourceFontPath);
                            ok = false;
                            continue;
                        }

                        if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                        {
                            Fail(
                                "{0} U+{1:X4} spacing differs from {2}: target={3}, clean={4}",
                                targetFontPath,
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            ok = false;
                            continue;
                        }

                        GlyphCanvas sourceCanvas = RenderGlyph(_cleanFont, sourceFontPath, codepoint);
                        GlyphCanvas targetCanvas = RenderGlyph(_patchedFont, targetFontPath, codepoint);
                        long score = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                        if (score != 0)
                        {
                            Fail(
                                "{0} U+{1:X4} pixels differ from {2}: score={3}, visible={4}/{5}",
                                targetFontPath,
                                codepoint,
                                sourceFontPath,
                                score,
                                sourceCanvas.VisiblePixels,
                                targetCanvas.VisiblePixels);
                            ok = false;
                            continue;
                        }

                        checkedGlyphs++;
                    }

                    int checkedKerning = VerifyCleanAsciiKerningRoute(sourceFdt, targetFdt, sourceFontPath, targetFontPath, ref ok);
                    if (ok && checkedGlyphs > 0)
                    {
                        Pass("{0} clean ASCII route matches {1}: glyphs={2}, kerning={3}", targetFontPath, sourceFontPath, checkedGlyphs, checkedKerning);
                    }
                    else if (ok)
                    {
                        Warn("{0} clean ASCII route had no source glyphs in {1}", targetFontPath, sourceFontPath);
                    }
                }
                catch (Exception ex)
                {
                    Fail("{0} clean ASCII route check error: {1}", targetFontPath, ex.Message);
                }
            }

            private int VerifyCleanAsciiKerningRoute(
                byte[] sourceFdt,
                byte[] targetFdt,
                string sourceFontPath,
                string targetFontPath,
                ref bool ok)
            {
                Dictionary<string, byte[]> sourceKerning = ReadAsciiKerningEntries(sourceFdt);
                Dictionary<string, byte[]> targetKerning = ReadAsciiKerningEntries(targetFdt);
                int checkedPairs = 0;

                foreach (KeyValuePair<string, byte[]> sourcePair in sourceKerning)
                {
                    byte[] targetEntry;
                    if (!targetKerning.TryGetValue(sourcePair.Key, out targetEntry))
                    {
                        Fail("{0} missing ASCII kerning pair {1} from {2}", targetFontPath, sourcePair.Key, sourceFontPath);
                        ok = false;
                        continue;
                    }

                    if (!BytesEqual(sourcePair.Value, targetEntry))
                    {
                        Fail(
                            "{0} ASCII kerning pair {1} differs from {2}: target={3}, clean={4}",
                            targetFontPath,
                            sourcePair.Key,
                            sourceFontPath,
                            ToHex(targetEntry),
                            ToHex(sourcePair.Value));
                        ok = false;
                        continue;
                    }

                    checkedPairs++;
                }

                foreach (string targetKey in targetKerning.Keys)
                {
                    if (!sourceKerning.ContainsKey(targetKey))
                    {
                        Fail("{0} has extra ASCII kerning pair {1} not present in {2}", targetFontPath, targetKey, sourceFontPath);
                        ok = false;
                    }
                }

                return checkedPairs;
            }

            private static string ResolveCleanAsciiReferenceFontPath(string targetFontPath)
            {
                string normalized = targetFontPath.Replace('\\', '/');
                if (normalized.IndexOf("/KrnAXIS_120.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "common/font/AXIS_12.fdt";
                }

                if (normalized.IndexOf("/KrnAXIS_140.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "common/font/AXIS_14.fdt";
                }

                if (normalized.IndexOf("/KrnAXIS_180.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "common/font/AXIS_18.fdt";
                }

                if (normalized.IndexOf("/KrnAXIS_360.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "common/font/AXIS_36.fdt";
                }

                return normalized;
            }

            private static uint[] CreateAsciiCodepoints()
            {
                uint[] codepoints = new uint[0x7E - 0x21 + 1];
                for (int i = 0; i < codepoints.Length; i++)
                {
                    codepoints[i] = (uint)(0x21 + i);
                }

                return codepoints;
            }

            private void VerifyHighScaleAsciiPhraseLayouts()
            {
                Console.WriteLine("[FDT] High-scale ASCII phrase metrics");
                string[,] fontPairs =
                {
                    { "common/font/AXIS_12.fdt", "common/font/AXIS_12.fdt" },
                    { "common/font/AXIS_14.fdt", "common/font/AXIS_14.fdt" },
                    { "common/font/AXIS_18.fdt", "common/font/AXIS_18.fdt" },
                    { "common/font/AXIS_36.fdt", "common/font/AXIS_36.fdt" },
                    { "common/font/AXIS_96.fdt", "common/font/AXIS_96.fdt" },
                    { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                    { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                    { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                    { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" },
                    { "common/font/AXIS_12_lobby.fdt", "common/font/AXIS_12_lobby.fdt" },
                    { "common/font/AXIS_14_lobby.fdt", "common/font/AXIS_14_lobby.fdt" },
                    { "common/font/AXIS_18_lobby.fdt", "common/font/AXIS_18_lobby.fdt" },
                    { "common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36_lobby.fdt" }
                };
                string[,] highScaleFontPairs =
                {
                    { "common/font/Jupiter_23.fdt", "common/font/Jupiter_23.fdt" },
                    { "common/font/Jupiter_46.fdt", "common/font/Jupiter_46.fdt" },
                    { "common/font/Jupiter_23_lobby.fdt", "common/font/Jupiter_23_lobby.fdt" },
                    { "common/font/Jupiter_46_lobby.fdt", "common/font/Jupiter_46_lobby.fdt" },
                    { "common/font/MiedingerMid_18.fdt", "common/font/MiedingerMid_18.fdt" },
                    { "common/font/MiedingerMid_36.fdt", "common/font/MiedingerMid_36.fdt" },
                    { "common/font/MiedingerMid_18_lobby.fdt", "common/font/MiedingerMid_18_lobby.fdt" },
                    { "common/font/MiedingerMid_36_lobby.fdt", "common/font/MiedingerMid_36_lobby.fdt" },
                    { "common/font/TrumpGothic_34.fdt", "common/font/TrumpGothic_34.fdt" },
                    { "common/font/TrumpGothic_68.fdt", "common/font/TrumpGothic_68.fdt" },
                    { "common/font/TrumpGothic_184.fdt", "common/font/TrumpGothic_184.fdt" },
                    { "common/font/TrumpGothic_34_lobby.fdt", "common/font/TrumpGothic_34_lobby.fdt" },
                    { "common/font/TrumpGothic_68_lobby.fdt", "common/font/TrumpGothic_68_lobby.fdt" },
                    { "common/font/TrumpGothic_184_lobby.fdt", "common/font/TrumpGothic_184_lobby.fdt" }
                };
                string[] phrases =
                {
                    "DATA CENTER SELECT",
                    "INFORMATION",
                    "Elemental",
                    "NA Cloud DC (Beta)",
                    "HP 100%",
                    "Lv. 100",
                    "150%"
                };
                for (int fontIndex = 0; fontIndex < fontPairs.GetLength(0); fontIndex++)
                {
                    for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                    {
                        VerifyPhraseMetricsMatchClean(fontPairs[fontIndex, 0], fontPairs[fontIndex, 1], phrases[phraseIndex]);
                    }
                }

                for (int fontIndex = 0; fontIndex < highScaleFontPairs.GetLength(0); fontIndex++)
                {
                    for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                    {
                        VerifyPhraseMetricsMatchClean(highScaleFontPairs[fontIndex, 0], highScaleFontPairs[fontIndex, 1], phrases[phraseIndex]);
                    }
                }
            }

            private void VerifySystemSettingsScaledPhraseLayouts()
            {
                Console.WriteLine("[FDT] System settings scaled phrase layout");
                string[] fonts =
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
                string[] phrases =
                {
                    "\uC2DC\uC2A4\uD15C \uC124\uC815 150%",
                    "\uC2DC\uC2A4\uD15C \uC124\uC815 200%",
                    "\uC2DC\uC2A4\uD15C \uC124\uC815 300%",
                    "FHD 150% QHD 200% UHD 300%",
                    "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0"
                };

                for (int fontIndex = 0; fontIndex < fonts.Length; fontIndex++)
                {
                    for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                    {
                        VerifyNoPhraseOverlap(fonts[fontIndex], phrases[phraseIndex]);
                    }
                }
            }

            private void VerifyPhraseMetricsMatchClean(string sourceFontPath, string targetFontPath, string phrase)
            {
                try
                {
                    byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                    int advance = 0;
                    int glyphs = 0;

                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint;
                        if (char.IsHighSurrogate(phrase[i]) && i + 1 < phrase.Length && char.IsLowSurrogate(phrase[i + 1]))
                        {
                            codepoint = (uint)char.ConvertToUtf32(phrase[i], phrase[i + 1]);
                            i++;
                        }
                        else
                        {
                            codepoint = phrase[i];
                        }

                        if (codepoint <= 0x20)
                        {
                            advance += 8;
                            continue;
                        }

                        FdtGlyphEntry sourceGlyph;
                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            Fail("{0} phrase [{1}] missing clean U+{2:X4}", sourceFontPath, Escape(phrase), codepoint);
                            return;
                        }

                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} phrase [{1}] missing patched U+{2:X4}", targetFontPath, Escape(phrase), codepoint);
                            return;
                        }

                        if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                        {
                            Fail(
                                "{0} phrase [{1}] U+{2:X4} metrics differ from {3}: target={4}, clean={5}",
                                targetFontPath,
                                Escape(phrase),
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            return;
                        }

                        advance += Math.Max(1, sourceGlyph.Width + sourceGlyph.OffsetX);
                        glyphs++;
                    }

                    Pass("{0} phrase [{1}] metrics match {2}, glyphs={3}, width={4}", targetFontPath, Escape(phrase), sourceFontPath, glyphs, advance);
                }
                catch (Exception ex)
                {
                    Fail("{0} phrase [{1}] metric check error: {2}", targetFontPath, Escape(phrase), ex.Message);
                }
            }

            private void Verify4kLobbyFontDerivations()
            {
                Console.WriteLine("[FDT] 4K lobby font derivations");
                uint[] latinCodepoints = new uint[] { 'A', 'a', '0', '1' };

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string targetFontPath = Derived4kLobbyFontPairs[i, 0];
                    string sourceFontPath = Derived4kLobbyFontPairs[i, 1];

                    for (int latinIndex = 0; latinIndex < latinCodepoints.Length; latinIndex++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, targetFontPath, latinCodepoints[latinIndex], _patchedFont, targetFontPath, latinCodepoints[latinIndex]);
                    }

                    byte[] sourceFdt;
                    try
                    {
                        sourceFdt = _patchedFont.ReadFile(sourceFontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for 4K lobby derivation check: {1}", sourceFontPath, ex.Message);
                        continue;
                    }

                    bool checkedHangul = false;
                    int checkedHangulCount = 0;
                    for (int codepointIndex = 0; codepointIndex < Derived4kLobbyRequiredHangulCodepoints.Length; codepointIndex++)
                    {
                        uint codepoint = Derived4kLobbyRequiredHangulCodepoints[codepointIndex];
                        FdtGlyphEntry ignored;
                        if (!TryFindGlyph(sourceFdt, codepoint, out ignored))
                        {
                            Fail("{0} is missing required 4K lobby source glyph U+{1:X4}", sourceFontPath, codepoint);
                            continue;
                        }

                        checkedHangul = true;
                        ExpectGlyphVisibleAtLeast(_patchedFont, targetFontPath, codepoint, 10);
                        ExpectGlyphNotEqualToFallback(targetFontPath, codepoint, '-');
                        ExpectGlyphNotEqualToFallback(targetFontPath, codepoint, '=');
                        VerifyDerived4kGlyphMetrics(targetFontPath, codepoint);
                        checkedHangulCount++;
                    }

                    if (!checkedHangul)
                    {
                        Fail("{0} has no required Hangul glyphs for 4K lobby target {1}", sourceFontPath, targetFontPath);
                    }
                    else
                    {
                        Pass("{0} required 4K lobby Hangul glyphs checked: {1}", targetFontPath, checkedHangulCount);
                    }
                }
            }

            private void Verify4kLobbyPhraseLayouts()
            {
                Console.WriteLine("[FDT] 4K lobby phrase layout");
                string[] phrases =
                {
                    "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
                    "\uC2DC\uC2A4\uD15C \uC124\uC815",
                    "\uAE00\uAF34 \uD06C\uAE30",
                    "\uD30C\uD2F0 \uBAA9\uB85D",
                    "\uB370\uC774\uD130 \uC13C\uD130",
                    "\uCD08\uC2B9\uB2EC \uB808\uBCA8"
                };

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string fontPath = Derived4kLobbyFontPairs[i, 0];
                    for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                    {
                        VerifyNoPhraseOverlap(fontPath, phrases[phraseIndex]);
                    }
                }
            }

            private void VerifyDerived4kGlyphMetrics(string fontPath, uint codepoint)
            {
                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fontPath);
                    FdtGlyphEntry glyph;
                    if (!TryFindGlyph(fdt, codepoint, out glyph))
                    {
                        Fail("{0} U+{1:X4} is missing", fontPath, codepoint);
                        return;
                    }

                    if (glyph.OffsetX < 0)
                    {
                        Fail("{0} U+{1:X4} has negative advance adjustment {2}", fontPath, codepoint, glyph.OffsetX);
                        return;
                    }

                    Pass("{0} U+{1:X4} advance adjustment={2}", fontPath, codepoint, glyph.OffsetX);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} metric check error: {2}", fontPath, codepoint, ex.Message);
                }
            }

            private void VerifyNoPhraseOverlap(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (layout.OverlapPixels > 0)
                {
                    PhraseLayoutResult cleanLayout;
                    string cleanError;
                    if (IsAsciiPhrase(phrase) &&
                        TryMeasurePhraseLayout(_cleanFont, fontPath, phrase, false, out cleanLayout, out cleanError) &&
                        layout.OverlapPixels <= cleanLayout.OverlapPixels)
                    {
                        Pass(
                            "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches clean baseline={5}",
                            fontPath,
                            Escape(phrase),
                            layout.Glyphs,
                            layout.Width,
                            layout.OverlapPixels,
                            cleanLayout.OverlapPixels);
                        return;
                    }

                    Fail("{0} phrase [{1}] has overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
                    return;
                }

                Pass("{0} phrase [{1}] layout glyphs={2}, width={3}", fontPath, Escape(phrase), layout.Glyphs, layout.Width);
            }

            private bool TryMeasurePhraseLayout(
                CompositeArchive archive,
                string fontPath,
                string phrase,
                bool validateGlyphShape,
                out PhraseLayoutResult result,
                out string error)
            {
                result = new PhraseLayoutResult();
                error = null;
                try
                {
                    byte[] fdt = archive.ReadFile(fontPath);
                    HashSet<long> occupiedPixels = new HashSet<long>();
                    int cursor = 0;
                    int glyphs = 0;
                    int overlap = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);

                        if (codepoint <= 0x20)
                        {
                            cursor += 8;
                            continue;
                        }

                        FdtGlyphEntry glyph;
                        if (!TryFindGlyph(fdt, codepoint, out glyph))
                        {
                            error = "missing U+" + codepoint.ToString("X4");
                            return false;
                        }

                        GlyphCanvas canvas = RenderGlyph(archive, fontPath, codepoint);
                        if (validateGlyphShape)
                        {
                            int minimumVisiblePixels = IsHangulCodepoint(codepoint) ? 10 : 1;
                            if (canvas.VisiblePixels < minimumVisiblePixels)
                            {
                                error = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "U+{0:X4} visible={1}, expected at least {2}",
                                    codepoint,
                                    canvas.VisiblePixels,
                                    minimumVisiblePixels);
                                return false;
                            }

                            if (IsHangulCodepoint(codepoint) &&
                                (GlyphMatchesFallback(fontPath, canvas, codepoint, '-') ||
                                 GlyphMatchesFallback(fontPath, canvas, codepoint, '=')))
                            {
                                error = "U+" + codepoint.ToString("X4") + " matches fallback glyph";
                                return false;
                            }
                        }

                        overlap += AddGlyphPixelsToLayout(occupiedPixels, cursor, canvas.Alpha);
                        cursor += Math.Max(1, glyph.Width + glyph.OffsetX);
                        glyphs++;
                    }

                    result.Glyphs = glyphs;
                    result.Width = cursor;
                    result.OverlapPixels = overlap;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static int AddGlyphPixelsToLayout(HashSet<long> occupiedPixels, int cursor, byte[] alpha)
            {
                int overlap = 0;
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    int rowOffset = y * GlyphCanvasSize;
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        if (alpha[rowOffset + x] == 0)
                        {
                            continue;
                        }

                        int pixelX = cursor + x - 32;
                        int pixelY = y - 32;
                        long key = ((long)pixelY << 32) ^ (uint)pixelX;
                        if (!occupiedPixels.Add(key))
                        {
                            overlap++;
                        }
                    }
                }

                return overlap;
            }

            private static uint ReadCodepoint(string value, ref int index)
            {
                if (char.IsHighSurrogate(value[index]) &&
                    index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]))
                {
                    uint codepoint = (uint)char.ConvertToUtf32(value[index], value[index + 1]);
                    index++;
                    return codepoint;
                }

                return value[index];
            }

            private static bool IsAsciiPhrase(string phrase)
            {
                for (int i = 0; i < phrase.Length; i++)
                {
                    if (phrase[i] > 0x7E)
                    {
                        return false;
                    }
                }

                return true;
            }

            private void VerifyNumericGlyphs()
            {
                Console.WriteLine("[FDT] Numeric glyphs used by duty/reward UI");
                string[] sameFontChecks =
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
                uint[] codepoints = { '0', '1', '2', '9' };
                for (int f = 0; f < sameFontChecks.Length; f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqual(_cleanFont, sameFontChecks[f], codepoints[c], _patchedFont, sameFontChecks[f], codepoints[c]);
                    }
                }

                string[,] krnFontChecks =
                {
                    { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                    { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                    { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                    { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" }
                };
                for (int f = 0; f < krnFontChecks.GetLength(0); f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqual(_cleanFont, krnFontChecks[f, 0], codepoints[c], _patchedFont, krnFontChecks[f, 1], codepoints[c]);
                    }
                }
            }

            private void VerifyPartyListSelfMarker()
            {
                Console.WriteLine("[EXD/FDT] Party-list self marker");
                byte[] addonText = GetFirstStringBytes(_patchedText, "Addon", 10952, _language);
                ExpectBytes("Addon 10952", addonText, new byte[] { 0xEE, 0x80, 0xB1 });

                for (int i = 0; i < PartyListProtectedPuaGlyphs.Length; i++)
                {
                    VerifyPartyListSelfMarkerGlyph(PartyListProtectedPuaGlyphs[i]);
                }
            }

            private void VerifyProtectedHangulGlyphs()
            {
                Console.WriteLine("[FDT] Protected Hangul glyphs");
                uint[] codepoints =
                {
                    0xD638, // 호
                    0xD63C  // 혼
                };
                string[] fonts =
                {
                    "common/font/AXIS_36.fdt",
                    "common/font/MiedingerMid_36.fdt",
                    "common/font/TrumpGothic_34.fdt"
                };
                for (int f = 0; f < fonts.Length; f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphVisibleAtLeast(_patchedFont, fonts[f], codepoints[c], ProtectedHangulMinimumVisiblePixels);
                        ExpectGlyphNotEqualToFallback(fonts[f], codepoints[c], '-');
                        ExpectGlyphNotEqualToFallback(fonts[f], codepoints[c], '=');
                    }
                }
            }

            private void VerifyPartyListSelfMarkerGlyph(uint codepoint)
            {
                string[] sameFontChecks =
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
                for (int i = 0; i < sameFontChecks.Length; i++)
                {
                    ExpectGlyphEqualIfSourceExists(_cleanFont, sameFontChecks[i], codepoint, _patchedFont, sameFontChecks[i], codepoint);
                }

                string[,] krnFontChecks =
                {
                    { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                    { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                    { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                    { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" }
                };
                for (int i = 0; i < krnFontChecks.GetLength(0); i++)
                {
                    ExpectGlyphEqualIfSourceExists(_cleanFont, krnFontChecks[i, 0], codepoint, _patchedFont, krnFontChecks[i, 1], codepoint);
                }
            }

            private void VerifyLobbyHangulVisibility()
            {
                Console.WriteLine("[FDT] Lobby Hangul visibility guard");
                string[] fonts =
                {
                    "common/font/AXIS_12_lobby.fdt",
                    "common/font/AXIS_14_lobby.fdt"
                };
                uint[] codepoints =
                {
                    0xB85C, // ro
                    0xC2A4, // seu
                    0xAC00, // ga
                    0xC774, // i
                    0xC544  // a
                };
                for (int f = 0; f < fonts.Length; f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphVisible(_patchedFont, fonts[f], codepoints[c]);
                    }
                }
            }

            private void VerifyLobbyPhraseGlyphDiagnostics()
            {
                Console.WriteLine("[FDT] Lobby phrase glyph diagnostics");
                uint[] codepoints = CollectCodepoints(new string[]
                {
                    // "character information change" lobby text and previously broken lobby words.
                    "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
                    "\uB85C\uC2A4\uAC00\uB974",
                    "\uB85C\uC2A4\uD2B8",
                    "\uB2C8\uBA54\uC774\uC544",
                    "\uADF8\uB9BC\uC790"
                });

                for (int i = 0; i < codepoints.Length; i++)
                {
                    VerifyAndDumpLobbyPhraseGlyph(codepoints[i]);
                }

                DumpLobbyPhraseSheets(codepoints);
            }

            private void VerifyDialoguePhraseGlyphDiagnostics()
            {
                Console.WriteLine("[FDT] Dialogue phrase glyph diagnostics");
                uint[] codepoints = CollectCodepoints(new string[]
                {
                    "\uD1A0\uB974\uB2F9 7\uC138\uCC9C\uB144\uC758 \uC545\uC5F0\uC744 \uB04A\uAE30 \uC704\uD55C \uC77C\uC774\uB2E4",
                    "\uC9C4\uC815\uD55C \uBCC0\uD601\uC744 \uC704\uD574\uC11C\uB77C\uBA74",
                    "\uBAB8\uC5D0 \uD76C\uC0DD\uB4E4\uC774 \uC5B4\uC5D0 \uB5A0\uC624\uB974\uB9AC",
                    "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0"
                });

                for (int i = 0; i < codepoints.Length; i++)
                {
                    VerifyAndDumpDialoguePhraseGlyph(codepoints[i]);
                }

                DumpDialoguePhraseSheets(codepoints);
            }

            private void VerifyAndDumpDialoguePhraseGlyph(uint codepoint)
            {
                bool foundAny = false;
                for (int fontIndex = 0; fontIndex < DialoguePhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = DialoguePhraseFontPaths[fontIndex];
                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for dialogue glyph diagnostics: {1}", fontPath, ex.Message);
                        continue;
                    }

                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        continue;
                    }

                    foundAny = true;
                    DumpAndCheckDialoguePhraseGlyph(fontPath, codepoint);
                }

                if (!foundAny)
                {
                    Fail("dialogue phrase U+{0:X4} was not found in any checked in-game font", codepoint);
                }
            }

            private void DumpAndCheckDialoguePhraseGlyph(string fontPath, uint codepoint)
            {
                try
                {
                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                    GlyphStats stats = AnalyzeGlyph(glyph);
                    DumpGlyph(DialoguePhraseGlyphGroup, fontPath, codepoint, glyph, stats);
                    if (glyph.VisiblePixels < 10)
                    {
                        ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} visible={2}, expected at least 10", glyph.VisiblePixels);
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, glyph, codepoint, '-'))
                    {
                        ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} matches fallback U+002D");
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, glyph, codepoint, '='))
                    {
                        ReportDialogueGlyphIssue(fontPath, codepoint, "{0} U+{1:X4} matches fallback U+003D");
                        return;
                    }

                    if (codepoint == 0xBCC0 && stats.ComponentCount >= 3 && stats.SmallComponentCount >= 3)
                    {
                        ReportDialogueGlyphIssue(
                            fontPath,
                            codepoint,
                            "{0} U+{1:X4} has suspected overlap artifact: components={2}/{3}",
                            stats.ComponentCount,
                            stats.SmallComponentCount);
                        return;
                    }

                    Pass(
                        "{0} U+{1:X4} visible={2}, components={3}/{4}",
                        fontPath,
                        codepoint,
                        glyph.VisiblePixels,
                        stats.ComponentCount,
                        stats.SmallComponentCount);
                }
                catch (Exception ex)
                {
                    Warn("{0} U+{1:X4} dialogue diagnostic error: {2}", fontPath, codepoint, ex.Message);
                }
            }

            private void ReportDialogueGlyphIssue(string fontPath, uint codepoint, string format, params object[] tailArgs)
            {
                object[] args = new object[2 + tailArgs.Length];
                args[0] = fontPath;
                args[1] = codepoint;
                for (int i = 0; i < tailArgs.Length; i++)
                {
                    args[i + 2] = tailArgs[i];
                }

                Fail(format, args);
            }

            private static bool IsCoreDialogueFont(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                return normalized.IndexOf("/axis_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       normalized.IndexOf("/miedingermid_", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private void VerifyAndDumpLobbyPhraseGlyph(uint codepoint)
            {
                GlyphCanvas reference;
                GlyphStats referenceStats;
                try
                {
                    reference = RenderGlyph(_patchedFont, LobbyPhraseReferenceFontPath, codepoint);
                    referenceStats = AnalyzeGlyph(reference);
                    DumpGlyph(LobbyPhraseGlyphGroup, LobbyPhraseReferenceFontPath, codepoint, reference, referenceStats);
                    Pass(
                        "{0} U+{1:X4} reference visible={2}, components={3}/{4}",
                        LobbyPhraseReferenceFontPath,
                        codepoint,
                        reference.VisiblePixels,
                        referenceStats.ComponentCount,
                        referenceStats.SmallComponentCount);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} reference diagnostic error: {2}", LobbyPhraseReferenceFontPath, codepoint, ex.Message);
                    return;
                }

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for lobby glyph diagnostics: {1}", fontPath, ex.Message);
                        continue;
                    }

                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        continue;
                    }

                    if (string.Equals(fontPath, LobbyPhraseReferenceFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CompareAndDumpLobbyPhraseGlyph(fontPath, codepoint, referenceStats);
                }
            }

            private void CompareAndDumpLobbyPhraseGlyph(string fontPath, uint codepoint, GlyphStats referenceStats)
            {
                try
                {
                    GlyphCanvas target = RenderGlyph(_patchedFont, fontPath, codepoint);
                    GlyphStats targetStats = AnalyzeGlyph(target);

                    DumpGlyph(LobbyPhraseGlyphGroup, fontPath, codepoint, target, targetStats);
                    if (target.VisiblePixels < 10)
                    {
                        Fail("{0} U+{1:X4} visible={2}, expected at least 10", fontPath, codepoint, target.VisiblePixels);
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, target, codepoint, '-'))
                    {
                        Fail("{0} U+{1:X4} matches fallback U+002D", fontPath, codepoint);
                        return;
                    }

                    if (GlyphMatchesFallback(fontPath, target, codepoint, '='))
                    {
                        Fail("{0} U+{1:X4} matches fallback U+003D", fontPath, codepoint);
                        return;
                    }

                    int extraComponents = targetStats.ComponentCount - referenceStats.ComponentCount;
                    int extraSmallComponents = targetStats.SmallComponentCount - referenceStats.SmallComponentCount;
                    if (string.Equals(fontPath, LobbyPhraseTargetFontPath, StringComparison.OrdinalIgnoreCase) &&
                        codepoint == 0xBCC0 &&
                        extraComponents > 0 &&
                        extraSmallComponents > 0)
                    {
                        Fail(
                            "{0} U+{1:X4} component profile suspicious: target components={2}/{3}, reference={4}/{5}",
                            fontPath,
                            codepoint,
                            targetStats.ComponentCount,
                            targetStats.SmallComponentCount,
                            referenceStats.ComponentCount,
                            referenceStats.SmallComponentCount);
                        return;
                    }

                    if (extraComponents > 1 && extraSmallComponents > 0)
                    {
                        Warn(
                            "{0} U+{1:X4} has more small components than reference: target={2}/{3}, reference={4}/{5}",
                            fontPath,
                            codepoint,
                            targetStats.ComponentCount,
                            targetStats.SmallComponentCount,
                            referenceStats.ComponentCount,
                            referenceStats.SmallComponentCount);
                        return;
                    }

                    Pass(
                        "{0} U+{1:X4} component profile target={2}/{3}, reference={4}/{5}",
                        fontPath,
                        codepoint,
                        targetStats.ComponentCount,
                        targetStats.SmallComponentCount,
                        referenceStats.ComponentCount,
                        referenceStats.SmallComponentCount);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} diagnostic error: {2}", fontPath, codepoint, ex.Message);
                }
            }

            private bool GlyphMatchesFallback(string fontPath, GlyphCanvas target, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return false;
                }

                try
                {
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fontPath, fallbackCodepoint);
                    return Diff(target.Alpha, fallback.Alpha) == 0 && target.VisiblePixels > 0;
                }
                catch
                {
                    return false;
                }
            }

            private static uint[] CollectCodepoints(string[] phrases)
            {
                List<uint> result = new List<uint>();
                HashSet<uint> seen = new HashSet<uint>();
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint;
                        if (char.IsHighSurrogate(phrase[i]) && i + 1 < phrase.Length && char.IsLowSurrogate(phrase[i + 1]))
                        {
                            codepoint = (uint)char.ConvertToUtf32(phrase[i], phrase[i + 1]);
                            i++;
                        }
                        else
                        {
                            codepoint = phrase[i];
                        }

                        if (codepoint <= 0x20)
                        {
                            continue;
                        }

                        if (seen.Add(codepoint))
                        {
                            result.Add(codepoint);
                        }
                    }
                }

                return result.ToArray();
            }

            private void DumpGlyph(string group, string fdtPath, uint codepoint, GlyphCanvas canvas, GlyphStats stats)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                string fileName = group + "_" + SanitizeFileName(fdtPath) + "_U" + codepoint.ToString("X4") + ".png";
                string pngPath = Path.Combine(_glyphDumpDir, fileName);
                WriteGlyphPng(canvas, pngPath);

                string reportLine = string.Join(
                    "\t",
                    new string[]
                    {
                        EscapeTsv(group),
                        EscapeTsv(fdtPath),
                        "U+" + codepoint.ToString("X4"),
                        EscapeTsv(char.ConvertFromUtf32((int)codepoint)),
                        canvas.VisiblePixels.ToString(),
                        stats.ComponentCount.ToString(),
                        stats.SmallComponentCount.ToString(),
                        EscapeTsv(FormatBounds(stats)),
                        canvas.Glyph.ImageIndex.ToString(),
                        EscapeTsv(canvas.TexturePath),
                        canvas.Glyph.X.ToString(),
                        canvas.Glyph.Y.ToString(),
                        canvas.Glyph.Width.ToString(),
                        canvas.Glyph.Height.ToString(),
                        canvas.Glyph.OffsetX.ToString(),
                        canvas.Glyph.OffsetY.ToString(),
                        EscapeTsv(pngPath)
                    });
                File.AppendAllText(Path.Combine(_glyphDumpDir, "glyph-report.tsv"), reportLine + Environment.NewLine, Encoding.UTF8);
            }

            private void DumpLobbyPhraseSheets(uint[] codepoints)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    List<uint> sheetCodepoints = new List<uint>();
                    List<GlyphCanvas> sheetGlyphs = new List<GlyphCanvas>();
                    for (int codepointIndex = 0; codepointIndex < codepoints.Length; codepointIndex++)
                    {
                        uint codepoint = codepoints[codepointIndex];
                        try
                        {
                            GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                            sheetCodepoints.Add(codepoint);
                            sheetGlyphs.Add(glyph);
                        }
                        catch
                        {
                            // Not every lobby font carries Hangul. Individual glyph checks already report
                            // the fonts that do, so missing entries are skipped in the visual sheet.
                        }
                    }

                    if (sheetGlyphs.Count == 0)
                    {
                        continue;
                    }

                    string fileName = LobbyPhraseGlyphGroup + "_" + SanitizeFileName(fontPath) + "_sheet.png";
                    WriteGlyphSheetPng(sheetCodepoints, sheetGlyphs, Path.Combine(_glyphDumpDir, fileName));
                }
            }

            private void DumpDialoguePhraseSheets(uint[] codepoints)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                for (int fontIndex = 0; fontIndex < DialoguePhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = DialoguePhraseFontPaths[fontIndex];
                    List<uint> sheetCodepoints = new List<uint>();
                    List<GlyphCanvas> sheetGlyphs = new List<GlyphCanvas>();
                    for (int codepointIndex = 0; codepointIndex < codepoints.Length; codepointIndex++)
                    {
                        uint codepoint = codepoints[codepointIndex];
                        try
                        {
                            GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                            sheetCodepoints.Add(codepoint);
                            sheetGlyphs.Add(glyph);
                        }
                        catch
                        {
                            // Not every in-game font carries every Hangul glyph.
                        }
                    }

                    if (sheetGlyphs.Count == 0)
                    {
                        continue;
                    }

                    string fileName = DialoguePhraseGlyphGroup + "_" + SanitizeFileName(fontPath) + "_sheet.png";
                    WriteGlyphSheetPng(sheetCodepoints, sheetGlyphs, Path.Combine(_glyphDumpDir, fileName));
                }
            }

            private static void WriteGlyphPng(GlyphCanvas canvas, string path)
            {
                int width = GlyphCanvasSize * GlyphDumpScale;
                int height = GlyphCanvasSize * GlyphDumpScale;
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.Black);
                    }

                    for (int y = 0; y < GlyphCanvasSize; y++)
                    {
                        for (int x = 0; x < GlyphCanvasSize; x++)
                        {
                            byte alpha = canvas.Alpha[y * GlyphCanvasSize + x];
                            if (alpha == 0)
                            {
                                continue;
                            }

                            System.Drawing.Color color = System.Drawing.Color.FromArgb(255, alpha, alpha, alpha);
                            int px = x * GlyphDumpScale;
                            int py = y * GlyphDumpScale;
                            for (int sy = 0; sy < GlyphDumpScale; sy++)
                            {
                                for (int sx = 0; sx < GlyphDumpScale; sx++)
                                {
                                    bitmap.SetPixel(px + sx, py + sy, color);
                                }
                            }
                        }
                    }

                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            private static void WriteGlyphSheetPng(List<uint> codepoints, List<GlyphCanvas> glyphs, string path)
            {
                const int scale = 2;
                const int columns = 6;
                const int labelHeight = 18;
                int cellWidth = GlyphCanvasSize * scale;
                int cellHeight = GlyphCanvasSize * scale + labelHeight;
                int rows = (glyphs.Count + columns - 1) / columns;
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(cellWidth * columns, cellHeight * rows, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.Black);
                        using (System.Drawing.Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 180, 220, 255)))
                        {
                            for (int i = 0; i < glyphs.Count; i++)
                            {
                                int column = i % columns;
                                int row = i / columns;
                                int originX = column * cellWidth;
                                int originY = row * cellHeight;
                                graphics.DrawString("U+" + codepoints[i].ToString("X4"), System.Drawing.SystemFonts.DefaultFont, brush, originX + 4, originY + 2);
                                DrawGlyphToBitmap(bitmap, glyphs[i], originX, originY + labelHeight, scale);
                            }
                        }
                    }

                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            private void DumpLabelPreview(string group, string fontPath, string[] labels)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                try
                {
                    List<GlyphCanvas?[]> rows = new List<GlyphCanvas?[]>();
                    int maxGlyphs = 0;
                    for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
                    {
                        string label = labels[labelIndex];
                        List<GlyphCanvas?> glyphs = new List<GlyphCanvas?>();
                        for (int charIndex = 0; charIndex < label.Length; charIndex++)
                        {
                            char ch = label[charIndex];
                            if (char.IsWhiteSpace(ch))
                            {
                                glyphs.Add(null);
                                continue;
                            }

                            glyphs.Add(RenderGlyph(_patchedFont, fontPath, ch));
                        }

                        if (glyphs.Count > maxGlyphs)
                        {
                            maxGlyphs = glyphs.Count;
                        }

                        rows.Add(glyphs.ToArray());
                    }

                    if (rows.Count == 0 || maxGlyphs == 0)
                    {
                        return;
                    }

                    const int scale = 1;
                    const int spacing = 2;
                    const int rowSpacing = 8;
                    int cellSize = GlyphCanvasSize * scale;
                    int width = Math.Max(1, maxGlyphs * (cellSize + spacing) + 8);
                    int height = Math.Max(1, rows.Count * (cellSize + rowSpacing) + 8);
                    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            graphics.Clear(System.Drawing.Color.Black);
                        }

                        for (int row = 0; row < rows.Count; row++)
                        {
                            GlyphCanvas?[] glyphs = rows[row];
                            int originY = 4 + row * (cellSize + rowSpacing);
                            int originX = 4;
                            for (int i = 0; i < glyphs.Length; i++)
                            {
                                GlyphCanvas? glyph = glyphs[i];
                                if (glyph.HasValue)
                                {
                                    DrawGlyphToBitmap(bitmap, glyph.Value, originX, originY, scale);
                                }

                                originX += cellSize + spacing;
                            }
                        }

                        string fileName = group + "_" + SanitizeFileName(fontPath) + "_preview.png";
                        bitmap.Save(Path.Combine(_glyphDumpDir, fileName), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    Warn("{0} {1} preview dump error: {2}", group, fontPath, ex.Message);
                }
            }

            private static void DrawGlyphToBitmap(System.Drawing.Bitmap bitmap, GlyphCanvas canvas, int originX, int originY, int scale)
            {
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        byte alpha = canvas.Alpha[y * GlyphCanvasSize + x];
                        if (alpha == 0)
                        {
                            continue;
                        }

                        System.Drawing.Color color = System.Drawing.Color.FromArgb(255, alpha, alpha, alpha);
                        int px = originX + x * scale;
                        int py = originY + y * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            for (int sx = 0; sx < scale; sx++)
                            {
                                bitmap.SetPixel(px + sx, py + sy, color);
                            }
                        }
                    }
                }
            }

            private static GlyphStats AnalyzeGlyph(GlyphCanvas canvas)
            {
                GlyphStats stats = new GlyphStats();
                stats.MinX = GlyphCanvasSize;
                stats.MinY = GlyphCanvasSize;
                stats.MaxX = -1;
                stats.MaxY = -1;

                bool[] seen = new bool[canvas.Alpha.Length];
                int[] stack = new int[canvas.Alpha.Length];
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        int start = y * GlyphCanvasSize + x;
                        if (seen[start] || canvas.Alpha[start] == 0)
                        {
                            continue;
                        }

                        int area = 0;
                        int minX = x;
                        int minY = y;
                        int maxX = x;
                        int maxY = y;
                        int stackCount = 0;
                        stack[stackCount++] = start;
                        seen[start] = true;

                        while (stackCount > 0)
                        {
                            int current = stack[--stackCount];
                            int cx = current % GlyphCanvasSize;
                            int cy = current / GlyphCanvasSize;
                            area++;
                            if (cx < minX) minX = cx;
                            if (cy < minY) minY = cy;
                            if (cx > maxX) maxX = cx;
                            if (cy > maxY) maxY = cy;

                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0)
                                    {
                                        continue;
                                    }

                                    int nx = cx + dx;
                                    int ny = cy + dy;
                                    if (nx < 0 || ny < 0 || nx >= GlyphCanvasSize || ny >= GlyphCanvasSize)
                                    {
                                        continue;
                                    }

                                    int next = ny * GlyphCanvasSize + nx;
                                    if (seen[next] || canvas.Alpha[next] == 0)
                                    {
                                        continue;
                                    }

                                    seen[next] = true;
                                    stack[stackCount++] = next;
                                }
                            }
                        }

                        stats.ComponentCount++;
                        if (area >= 2 && area <= 96 && Math.Max(maxX - minX + 1, maxY - minY + 1) <= 18)
                        {
                            stats.SmallComponentCount++;
                        }

                        if (minX < stats.MinX) stats.MinX = minX;
                        if (minY < stats.MinY) stats.MinY = minY;
                        if (maxX > stats.MaxX) stats.MaxX = maxX;
                        if (maxY > stats.MaxY) stats.MaxY = maxY;
                    }
                }

                return stats;
            }

            private static string FormatBounds(GlyphStats stats)
            {
                if (stats.ComponentCount == 0)
                {
                    return "-";
                }

                return stats.MinX.ToString() + "," +
                       stats.MinY.ToString() + "-" +
                       stats.MaxX.ToString() + "," +
                       stats.MaxY.ToString();
            }

            private static string SanitizeFileName(string value)
            {
                StringBuilder builder = new StringBuilder(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char ch = value[i];
                    if ((ch >= 'a' && ch <= 'z') ||
                        (ch >= 'A' && ch <= 'Z') ||
                        (ch >= '0' && ch <= '9'))
                    {
                        builder.Append(ch);
                    }
                    else
                    {
                        builder.Append('_');
                    }
                }

                return builder.ToString().Trim('_');
            }

            private static string EscapeTsv(string value)
            {
                return (value ?? string.Empty).Replace("\t", " ").Replace("\r", "\\r").Replace("\n", "\\n");
            }

            private void ExpectEqual(string label, string actual, string expected)
            {
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0} = {1}", label, Escape(actual));
                    return;
                }

                Fail("{0} expected [{1}], actual [{2}]", label, Escape(expected), Escape(actual));
            }

            private void ExpectStartsWith(string label, string actual, string expectedPrefix)
            {
                if ((actual ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    Pass("{0} starts with {1}", label, expectedPrefix);
                    return;
                }

                Fail("{0} does not start with [{1}], actual [{2}]", label, expectedPrefix, Escape(actual));
            }

            private void ExpectContains(string label, string actual, string expected)
            {
                if ((actual ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    Pass("{0} contains {1}", label, expected);
                    return;
                }

                Fail("{0} does not contain [{1}], actual [{2}]", label, expected, Escape(actual));
            }

            private void ExpectNotContains(string label, string actual, string unexpected)
            {
                if ((actual ?? string.Empty).IndexOf(unexpected, StringComparison.Ordinal) < 0)
                {
                    Pass("{0} does not contain {1}", label, unexpected);
                    return;
                }

                Fail("{0} still contains [{1}], actual [{2}]", label, unexpected, Escape(actual));
            }

            private void ExpectText(string sheet, uint rowId, string expected)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, _language);
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0}#{1} = {2}", sheet, rowId, expected);
                    return;
                }

                Fail("{0}#{1} expected [{2}], actual [{3}]", sheet, rowId, expected, Escape(actual));
            }

            private void ExpectTextContains(string sheet, uint rowId, string expected)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, _language);
                if (actual.IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    Pass("{0}#{1} contains {2}", sheet, rowId, expected);
                    return;
                }

                Fail("{0}#{1} does not contain [{2}], actual [{3}]", sheet, rowId, expected, Escape(actual));
            }

            private void ExpectTextNotContains(string sheet, uint rowId, string unexpected)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, _language);
                if (actual.IndexOf(unexpected, StringComparison.Ordinal) < 0)
                {
                    Pass("{0}#{1} does not contain {2}", sheet, rowId, unexpected);
                    return;
                }

                Fail("{0}#{1} still contains [{2}], actual [{3}]", sheet, rowId, unexpected, Escape(actual));
            }

            private void ExpectTextColumn(string sheet, uint rowId, ushort columnOffset, string expected)
            {
                string actual = GetStringColumnByOffset(_patchedText, sheet, rowId, _language, columnOffset);
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0}#{1}@{2} = {3}", sheet, rowId, columnOffset, expected);
                    return;
                }

                Fail("{0}#{1}@{2} expected [{3}], actual [{4}]", sheet, rowId, columnOffset, expected, Escape(actual));
            }

            private void ExpectTextColumnContains(string sheet, uint rowId, ushort columnOffset, string expected)
            {
                string actual = GetStringColumnByOffset(_patchedText, sheet, rowId, _language, columnOffset);
                if (actual.IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    Pass("{0}#{1}@{2} contains {3}", sheet, rowId, columnOffset, expected);
                    return;
                }

                Fail("{0}#{1}@{2} does not contain [{3}], actual [{4}]", sheet, rowId, columnOffset, expected, Escape(actual));
            }

            private void ExpectTextColumnNotContains(string sheet, uint rowId, ushort columnOffset, string unexpected)
            {
                string actual = GetStringColumnByOffset(_patchedText, sheet, rowId, _language, columnOffset);
                if (actual.IndexOf(unexpected, StringComparison.Ordinal) < 0)
                {
                    Pass("{0}#{1}@{2} does not contain {3}", sheet, rowId, columnOffset, unexpected);
                    return;
                }

                Fail("{0}#{1}@{2} still contains [{3}], actual [{4}]", sheet, rowId, columnOffset, unexpected, Escape(actual));
            }

            private void ExpectAnyTextColumnContains(string sheet, uint rowId, string expected)
            {
                List<string> columns = GetStringColumns(_patchedText, sheet, rowId, _language);
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].IndexOf(expected, StringComparison.Ordinal) >= 0)
                    {
                        Pass("{0}#{1} column {2} contains {3}", sheet, rowId, i, expected);
                        return;
                    }
                }

                Fail("{0}#{1} does not contain [{2}], columns=[{3}]", sheet, rowId, expected, Escape(string.Join(" | ", columns.ToArray())));
            }

            private void ExpectAnyTextColumnNotContains(string sheet, uint rowId, string unexpected)
            {
                List<string> columns = GetStringColumns(_patchedText, sheet, rowId, _language);
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].IndexOf(unexpected, StringComparison.Ordinal) >= 0)
                    {
                        Fail("{0}#{1} column {2} still contains [{3}], value=[{4}]", sheet, rowId, i, unexpected, Escape(columns[i]));
                        return;
                    }
                }

                Pass("{0}#{1} does not contain {2}", sheet, rowId, unexpected);
            }

            private void ExpectDataCenterLabel(DataCenterLabelExpectation expectation, string language)
            {
                List<string> columns = GetStringColumns(_patchedText, expectation.Sheet, expectation.RowId, language);
                bool found = false;
                bool fallback = false;
                bool hangul = false;
                bool unexpectedNonEmpty = false;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (expectation.AllowSubstring
                        ? columns[i].IndexOf(expectation.Expected, StringComparison.Ordinal) >= 0
                        : string.Equals(columns[i], expectation.Expected, StringComparison.Ordinal))
                    {
                        found = true;
                    }

                    if (LooksLikeMissingGlyphFallback(columns[i]))
                    {
                        fallback = true;
                    }

                    if (ContainsHangul(columns[i]))
                    {
                        hangul = true;
                    }

                    if (!expectation.AllowSubstring && columns[i].Length > 0 && !string.Equals(columns[i], expectation.Expected, StringComparison.Ordinal))
                    {
                        unexpectedNonEmpty = true;
                    }
                }

                if (found && !fallback && !hangul && !unexpectedNonEmpty)
                {
                    Pass("{0}#{1}/{2} contains [{3}]", expectation.Sheet, expectation.RowId, language, expectation.Expected);
                    return;
                }

                Fail(
                    "{0}#{1}/{2} expected [{3}], fallback={4}, hangul={5}, unexpected={6}, columns [{7}]",
                    expectation.Sheet,
                    expectation.RowId,
                    language,
                    expectation.Expected,
                    fallback,
                    hangul,
                    unexpectedNonEmpty,
                    string.Join("] [", columns.ToArray()));
            }

            private void VerifyLabelGlyphsEqualClean(string fdtPath, string[] labels)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
                {
                    string label = labels[labelIndex];
                    for (int charIndex = 0; charIndex < label.Length; charIndex++)
                    {
                        char ch = label[charIndex];
                        if (!char.IsWhiteSpace(ch))
                        {
                            codepoints.Add(ch);
                        }
                    }
                }

                foreach (uint codepoint in codepoints)
                {
                    ExpectGlyphEqual(_cleanFont, fdtPath, codepoint, _patchedFont, fdtPath, codepoint);
                    ExpectGlyphNotEqualToFallback(fdtPath, codepoint, '-');
                    ExpectGlyphNotEqualToFallback(fdtPath, codepoint, '=');
                }
            }

            private void ExpectGlyphNotEqualToFallback(string fdtPath, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return;
                }

                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fdtPath);
                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        Fail("{0} U+{1:X4} is missing", fdtPath, codepoint);
                        return;
                    }

                    if (!TryFindGlyph(fdt, fallbackCodepoint, out ignored))
                    {
                        Warn("{0} U+{1:X4} fallback comparison skipped; missing U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fdtPath, codepoint);
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fdtPath, fallbackCodepoint);
                    long score = Diff(glyph.Alpha, fallback.Alpha);
                    if (score != 0 && glyph.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} is not fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    Fail("{0} U+{1:X4} matches fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} fallback comparison error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private void ExpectBytes(string label, byte[] actual, byte[] expected)
            {
                bool equal = actual.Length == expected.Length;
                for (int i = 0; equal && i < actual.Length; i++)
                {
                    equal = actual[i] == expected[i];
                }

                if (equal)
                {
                    Pass("{0} bytes = {1}", label, ToHex(expected));
                    return;
                }

                Fail("{0} bytes expected {1}, actual {2}", label, ToHex(expected), ToHex(actual));
            }

            private void ExpectGlyphEqual(
                CompositeArchive sourceArchive,
                string sourceFdtPath,
                uint sourceCodepoint,
                CompositeArchive targetArchive,
                string targetFdtPath,
                uint targetCodepoint)
            {
                try
                {
                    GlyphCanvas source = RenderGlyph(sourceArchive, sourceFdtPath, sourceCodepoint);
                    GlyphCanvas target = RenderGlyph(targetArchive, targetFdtPath, targetCodepoint);
                    long score = Diff(source.Alpha, target.Alpha);
                    bool spacingMatch = GlyphSpacingMetricsMatch(source.Glyph, target.Glyph);
                    if (score == 0 && spacingMatch && source.VisiblePixels > 0 && target.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} -> {2} U+{3:X4}", sourceFdtPath, sourceCodepoint, targetFdtPath, targetCodepoint);
                        return;
                    }

                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} mismatch score={4} visible={5}/{6}, target={7}, clean={8}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        score,
                        source.VisiblePixels,
                        target.VisiblePixels,
                        FormatGlyphSpacing(target.Glyph),
                        FormatGlyphSpacing(source.Glyph));
                }
                catch (Exception ex)
                {
                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} error: {4}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        ex.Message);
                }
            }

            private void ExpectGlyphEqualIfSourceExists(
                CompositeArchive sourceArchive,
                string sourceFdtPath,
                uint sourceCodepoint,
                CompositeArchive targetArchive,
                string targetFdtPath,
                uint targetCodepoint)
            {
                byte[] sourceFdt = sourceArchive.ReadFile(sourceFdtPath);
                FdtGlyphEntry ignored;
                if (!TryFindGlyph(sourceFdt, sourceCodepoint, out ignored))
                {
                    return;
                }

                ExpectGlyphEqual(sourceArchive, sourceFdtPath, sourceCodepoint, targetArchive, targetFdtPath, targetCodepoint);
            }

            private void ExpectGlyphVisible(CompositeArchive archive, string fdtPath, uint codepoint)
            {
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fdtPath, codepoint);
                    if (canvas.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} visible={2}", fdtPath, codepoint, canvas.VisiblePixels);
                        return;
                    }

                    Fail("{0} U+{1:X4} is invisible", fdtPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private void ExpectGlyphVisibleAtLeast(CompositeArchive archive, string fdtPath, uint codepoint, int minimumVisiblePixels)
            {
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fdtPath, codepoint);
                    if (canvas.VisiblePixels >= minimumVisiblePixels)
                    {
                        Pass("{0} U+{1:X4} visible={2}", fdtPath, codepoint, canvas.VisiblePixels);
                        return;
                    }

                    Fail("{0} U+{1:X4} visible={2}, expected at least {3}", fdtPath, codepoint, canvas.VisiblePixels, minimumVisiblePixels);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private GlyphCanvas RenderGlyph(CompositeArchive archive, string fdtPath, uint codepoint)
            {
                byte[] fdt = archive.ReadFile(fdtPath);
                FdtGlyphEntry glyph;
                if (!TryFindGlyph(fdt, codepoint, out glyph))
                {
                    throw new InvalidDataException(fdtPath + " is missing U+" + codepoint.ToString("X4"));
                }

                string texturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    throw new InvalidDataException("Could not resolve texture for " + fdtPath);
                }

                string cacheKey = archive.CacheKey + "|" + texturePath;
                byte[] rawTexture;
                if (!_textureCache.TryGetValue(cacheKey, out rawTexture))
                {
                    byte[] packed;
                    if (!archive.TryReadPackedFile(texturePath, out packed))
                    {
                        throw new FileNotFoundException("texture was not found", texturePath);
                    }

                    rawTexture = UnpackTextureFile(packed);
                    _textureCache.Add(cacheKey, rawTexture);
                }

                Texture texture = ReadA4R4G4B4Texture(rawTexture);
                byte[] alpha = RenderGlyphAlpha(texture, glyph);
                int visible = 0;
                for (int i = 0; i < alpha.Length; i++)
                {
                    if (alpha[i] != 0)
                    {
                        visible++;
                    }
                }

                return new GlyphCanvas
                {
                    Alpha = alpha,
                    VisiblePixels = visible,
                    Glyph = glyph,
                    TexturePath = texturePath
                };
            }

            private static byte[] RenderGlyphAlpha(Texture texture, FdtGlyphEntry glyph)
            {
                byte[] canvas = new byte[GlyphCanvasSize * GlyphCanvasSize];
                int channel = glyph.ImageIndex % 4;
                int startX = 32 + glyph.OffsetX;
                int startY = 32 + glyph.OffsetY;
                for (int y = 0; y < glyph.Height; y++)
                {
                    int dy = startY + y;
                    for (int x = 0; x < glyph.Width; x++)
                    {
                        int dx = startX + x;
                        if (dx < 0 || dy < 0 || dx >= GlyphCanvasSize || dy >= GlyphCanvasSize)
                        {
                            continue;
                        }

                        int sourceX = glyph.X + x;
                        int sourceY = glyph.Y + y;
                        if (sourceX < 0 || sourceY < 0 || sourceX >= texture.Width || sourceY >= texture.Height)
                        {
                            throw new InvalidDataException(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "glyph source outside texture: image={0}, source=({1},{2}), texture={3}x{4}",
                                    glyph.ImageIndex,
                                    sourceX,
                                    sourceY,
                                    texture.Width,
                                    texture.Height));
                        }

                        int pixelOffset = (sourceY * texture.Width + sourceX) * 2;
                        if (pixelOffset < 0 || pixelOffset + 1 >= texture.Data.Length)
                        {
                            throw new InvalidDataException(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "glyph source outside texture data: image={0}, offset={1}, bytes={2}",
                                    glyph.ImageIndex,
                                    pixelOffset,
                                    texture.Data.Length));
                        }

                        byte lo = texture.Data[pixelOffset];
                        byte hi = texture.Data[pixelOffset + 1];
                        byte nibble;
                        switch (channel)
                        {
                            case 0: nibble = (byte)(hi & 0x0F); break;
                            case 1: nibble = (byte)((lo >> 4) & 0x0F); break;
                            case 2: nibble = (byte)(lo & 0x0F); break;
                            default: nibble = (byte)((hi >> 4) & 0x0F); break;
                        }

                        canvas[dy * GlyphCanvasSize + dx] = (byte)(nibble * 17);
                    }
                }

                return canvas;
            }

            private static long Diff(byte[] left, byte[] right)
            {
                long score = 0;
                for (int i = 0; i < left.Length; i++)
                {
                    score += Math.Abs(left[i] - right[i]);
                }

                return score;
            }

            private static void Pass(string format, params object[] args)
            {
                Console.WriteLine("  OK   " + string.Format(format, args));
            }

            private static void Warn(string format, params object[] args)
            {
                Console.WriteLine("  WARN " + string.Format(format, args));
            }

            private void Fail(string format, params object[] args)
            {
                Failed = true;
                Console.WriteLine("  FAIL " + string.Format(format, args));
            }
        }

        private sealed class CompositeArchive : IDisposable
        {
            private readonly SqPackIndexFile _index;
            private readonly string _datDir;
            private readonly string _fallbackDatDir;
            private readonly string _datPrefix;
            private readonly Dictionary<byte, FileStream> _streams = new Dictionary<byte, FileStream>();

            public string CacheKey { get; private set; }

            public CompositeArchive(string indexPath, string datDir, string fallbackDatDir, string datPrefix)
            {
                if (!File.Exists(indexPath))
                {
                    throw new FileNotFoundException("index file was not found", indexPath);
                }

                _index = new SqPackIndexFile(indexPath);
                _datDir = datDir;
                _fallbackDatDir = fallbackDatDir;
                _datPrefix = datPrefix;
                CacheKey = Path.GetFullPath(indexPath);
            }

            public byte[] ReadFile(string gamePath)
            {
                byte[] packed;
                if (!TryReadPackedFile(gamePath, out packed))
                {
                    throw new FileNotFoundException("SqPack file was not found", gamePath);
                }

                return SqPackArchive.UnpackStandardFile(packed);
            }

            public bool TryReadPackedFile(string gamePath, out byte[] data)
            {
                SqPackIndexEntry entry;
                if (!_index.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out entry))
                {
                    data = null;
                    return false;
                }

                FileStream stream = GetStream(entry.DataFileId);
                lock (stream)
                {
                    long nextOffset = _index.GetNextOffset(entry.DataFileId, entry.Offset, stream.Length);
                    long length = nextOffset - entry.Offset;
                    if (length <= 0 || length > int.MaxValue)
                    {
                        throw new InvalidDataException("Invalid packed SqPack file length for " + gamePath);
                    }

                    byte[] bytes = new byte[(int)length];
                    stream.Position = entry.Offset;
                    int totalRead = 0;
                    while (totalRead < bytes.Length)
                    {
                        int read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                        if (read == 0)
                        {
                            break;
                        }

                        totalRead += read;
                    }

                    if (totalRead != bytes.Length)
                    {
                        throw new EndOfStreamException("Could not read packed SqPack file: " + gamePath);
                    }

                    data = bytes;
                    return true;
                }
            }

            private FileStream GetStream(byte datId)
            {
                FileStream stream;
                if (_streams.TryGetValue(datId, out stream))
                {
                    return stream;
                }

                string path = Path.Combine(_datDir, _datPrefix + ".dat" + datId.ToString());
                if (!File.Exists(path))
                {
                    path = Path.Combine(_fallbackDatDir, _datPrefix + ".dat" + datId.ToString());
                }

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("SqPack dat file is missing", path);
                }

                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                _streams.Add(datId, stream);
                return stream;
            }

            public void Dispose()
            {
                _index.Dispose();
                foreach (FileStream stream in _streams.Values)
                {
                    stream.Dispose();
                }
            }
        }

        private sealed class Options
        {
            public string OutputPath;
            public string AppliedGamePath;
            public string GlobalGamePath;
            public string TargetLanguage = "ja";
            public string GlyphDumpDir;
            public bool NoGlyphDump;
            public bool ShowHelp;

            public static Options Parse(string[] args)
            {
                Options options = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ShowHelp = true;
                    }
                    else if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
                    {
                        options.OutputPath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--global", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlobalGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--applied-game", StringComparison.OrdinalIgnoreCase))
                    {
                        options.AppliedGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--target-language", StringComparison.OrdinalIgnoreCase))
                    {
                        options.TargetLanguage = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--glyph-dump-dir", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlyphDumpDir = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--no-glyph-dump", StringComparison.OrdinalIgnoreCase))
                    {
                        options.NoGlyphDump = true;
                    }
                    else
                    {
                        throw new ArgumentException("unknown argument: " + arg);
                    }
                }

                return options;
            }

            private static string RequireValue(string[] args, ref int index, string name)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException(name + " requires a value");
                }

                index++;
                return args[index];
            }
        }

        private static string GetFirstString(CompositeArchive archive, string sheet, uint rowId, string language)
        {
            return Encoding.UTF8.GetString(GetFirstStringBytes(archive, sheet, rowId, language));
        }

        private static List<string> GetStringColumns(CompositeArchive archive, string sheet, uint rowId, string language)
        {
            ExcelHeader header = ExcelHeader.Parse(archive.ReadFile("exd/" + sheet + ".exh"));
            byte languageId = LanguageToId(language);
            bool hasLanguageSuffix = header.HasLanguage(languageId);
            ExcelDataFile file = null;

            for (int i = 0; i < header.Pages.Count; i++)
            {
                ExcelPageDefinition page = header.Pages[i];
                if (rowId < page.StartId || rowId >= page.StartId + page.RowCount)
                {
                    continue;
                }

                string exdPath = "exd/" + sheet + "_" + page.StartId + (hasLanguageSuffix ? "_" + language : string.Empty) + ".exd";
                file = ExcelDataFile.Parse(archive.ReadFile(exdPath));
                break;
            }

            if (file == null)
            {
                throw new InvalidDataException(sheet + "#" + rowId + " is outside all pages");
            }

            ExcelDataRow row;
            if (!file.TryGetRow(rowId, out row))
            {
                throw new InvalidDataException(sheet + "#" + rowId + " was not found");
            }

            List<string> values = new List<string>();
            List<int> stringColumns = header.GetStringColumnIndexes();
            for (int i = 0; i < stringColumns.Count; i++)
            {
                byte[] bytes = file.GetStringBytes(row, header, stringColumns[i]) ?? new byte[0];
                values.Add(Encoding.UTF8.GetString(bytes));
            }

            return values;
        }

        private static string GetStringColumnByOffset(CompositeArchive archive, string sheet, uint rowId, string language, ushort columnOffset)
        {
            ExcelHeader header = ExcelHeader.Parse(archive.ReadFile("exd/" + sheet + ".exh"));
            byte languageId = LanguageToId(language);
            bool hasLanguageSuffix = header.HasLanguage(languageId);
            ExcelDataFile file = null;

            for (int i = 0; i < header.Pages.Count; i++)
            {
                ExcelPageDefinition page = header.Pages[i];
                if (rowId < page.StartId || rowId >= page.StartId + page.RowCount)
                {
                    continue;
                }

                string exdPath = "exd/" + sheet + "_" + page.StartId + (hasLanguageSuffix ? "_" + language : string.Empty) + ".exd";
                file = ExcelDataFile.Parse(archive.ReadFile(exdPath));
                break;
            }

            if (file == null)
            {
                throw new InvalidDataException(sheet + "#" + rowId + " is outside all pages");
            }

            ExcelDataRow row;
            if (!file.TryGetRow(rowId, out row))
            {
                throw new InvalidDataException(sheet + "#" + rowId + " was not found");
            }

            byte[] bytes = file.GetStringBytesByColumnOffset(row, header, columnOffset);
            if (bytes == null)
            {
                throw new InvalidDataException(sheet + "#" + rowId + " does not have string column offset " + columnOffset.ToString());
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static byte[] GetFirstStringBytes(CompositeArchive archive, string sheet, uint rowId, string language)
        {
            ExcelHeader header = ExcelHeader.Parse(archive.ReadFile("exd/" + sheet + ".exh"));
            byte languageId = LanguageToId(language);
            bool hasLanguageSuffix = header.HasLanguage(languageId);
            ExcelDataFile file = null;
            string exdPath = null;

            for (int i = 0; i < header.Pages.Count; i++)
            {
                ExcelPageDefinition page = header.Pages[i];
                if (rowId < page.StartId || rowId >= page.StartId + page.RowCount)
                {
                    continue;
                }

                exdPath = "exd/" + sheet + "_" + page.StartId + (hasLanguageSuffix ? "_" + language : string.Empty) + ".exd";
                file = ExcelDataFile.Parse(archive.ReadFile(exdPath));
                break;
            }

            if (file == null)
            {
                throw new InvalidDataException(sheet + "#" + rowId + " is outside all pages");
            }

            ExcelDataRow row;
            if (!file.TryGetRow(rowId, out row))
            {
                throw new InvalidDataException(sheet + "#" + rowId + " was not found");
            }

            List<int> stringColumns = header.GetStringColumnIndexes();
            if (stringColumns.Count == 0)
            {
                return new byte[0];
            }

            return file.GetStringBytes(row, header, stringColumns[0]) ?? new byte[0];
        }

        private static bool LooksLikeMissingGlyphFallback(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int visible = 0;
            int fallback = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                {
                    continue;
                }

                visible++;
                if (ch == '-' || ch == '=' || ch == '\uFF0D' || ch == '\u30FC')
                {
                    fallback++;
                }
            }

            return visible > 0 && visible == fallback;
        }

        private static bool ContainsHangul(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if ((ch >= '\uAC00' && ch <= '\uD7A3') ||
                    (ch >= '\u1100' && ch <= '\u11FF') ||
                    (ch >= '\u3130' && ch <= '\u318F') ||
                    (ch >= '\uA960' && ch <= '\uA97F') ||
                    (ch >= '\uD7B0' && ch <= '\uD7FF'))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHangulCodepoint(uint codepoint)
        {
            return (codepoint >= 0xAC00 && codepoint <= 0xD7A3) ||
                   (codepoint >= 0x1100 && codepoint <= 0x11FF) ||
                   (codepoint >= 0x3130 && codepoint <= 0x318F) ||
                   (codepoint >= 0xA960 && codepoint <= 0xA97F) ||
                   (codepoint >= 0xD7B0 && codepoint <= 0xD7FF);
        }

        private static List<UldTextNodeFont> GetUldTextNodeFonts(byte[] uld)
        {
            List<UldTextNodeFont> results = new List<UldTextNodeFont>();
            if (!HasRange(uld, 0, 16) || !HasAsciiMagic(uld, 0, "uldh"))
            {
                return results;
            }

            WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, 8), true, results);
            WalkUldAtkTextNodes(uld, Endian.ReadUInt32LE(uld, 12), false, results);
            return results;
        }

        private static string ResolveUldFontPath(byte fontId, byte fontSize, bool lobby)
        {
            string suffix = lobby ? "_lobby.fdt" : ".fdt";
            switch (fontId)
            {
                case UldAxisFontId:
                    switch (fontSize)
                    {
                        case 12:
                        case 14:
                        case 18:
                        case 36:
                        case 96:
                            return "common/font/AXIS_" + fontSize.ToString() + suffix;
                        case 34:
                            return "common/font/AXIS_36" + suffix;
                    }

                    return null;

                case UldMiedingerMedFontId:
                    switch (fontSize)
                    {
                        case 10:
                        case 12:
                        case 14:
                        case 18:
                        case 36:
                            return "common/font/MiedingerMid_" + fontSize.ToString() + suffix;
                    }

                    return null;

                case UldTrumpGothicFontId:
                    switch (fontSize)
                    {
                        case 23:
                        case 34:
                        case 68:
                        case 184:
                            return "common/font/TrumpGothic_" + fontSize.ToString() + suffix;
                    }

                    return null;

                case UldJupiterFontId:
                    switch (fontSize)
                    {
                        case 12:
                            return "common/font/Jupiter_16" + suffix;
                        case 18:
                            return "common/font/Jupiter_20" + suffix;
                        case 16:
                        case 20:
                        case 23:
                        case 45:
                        case 46:
                        case 90:
                            return "common/font/Jupiter_" + fontSize.ToString() + suffix;
                    }

                    return null;

                default:
                    return null;
            }
        }

        private static Dictionary<int, UldTextNodeFont> GetUldTextNodeFontsByOffset(byte[] uld)
        {
            List<UldTextNodeFont> fonts = GetUldTextNodeFonts(uld);
            Dictionary<int, UldTextNodeFont> byOffset = new Dictionary<int, UldTextNodeFont>();
            for (int i = 0; i < fonts.Count; i++)
            {
                byOffset[fonts[i].NodeOffset] = fonts[i];
            }

            return byOffset;
        }

        private static void WalkUldAtkTextNodes(byte[] uld, uint atkOffsetValue, bool patchComponents, List<UldTextNodeFont> results)
        {
            if (atkOffsetValue == 0 || atkOffsetValue > int.MaxValue)
            {
                return;
            }

            int atkOffset = (int)atkOffsetValue;
            if (!HasRange(uld, atkOffset, 36) || !HasAsciiMagic(uld, atkOffset, "atkh"))
            {
                return;
            }

            if (patchComponents)
            {
                WalkUldComponentTextNodes(uld, atkOffset, results);
            }
            else
            {
                WalkUldWidgetTextNodes(uld, atkOffset, results);
            }
        }

        private static void WalkUldComponentTextNodes(byte[] uld, int atkOffset, List<UldTextNodeFont> results)
        {
            uint componentListRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + 16);
            if (componentListRelativeOffset == 0 || componentListRelativeOffset > int.MaxValue)
            {
                return;
            }

            int componentListOffset = atkOffset + (int)componentListRelativeOffset;
            if (!HasRange(uld, componentListOffset, 16) || !HasAsciiMagic(uld, componentListOffset, "cohd"))
            {
                return;
            }

            uint componentCount = Endian.ReadUInt32LE(uld, componentListOffset + 8);
            int entryOffset = componentListOffset + 16;
            for (uint i = 0; i < componentCount && HasRange(uld, entryOffset, 16); i++)
            {
                uint nodeCount = Endian.ReadUInt32LE(uld, entryOffset + 8);
                ushort componentSize = Endian.ReadUInt16LE(uld, entryOffset + 12);
                ushort nodeOffset = Endian.ReadUInt16LE(uld, entryOffset + 14);
                int nodeStart = entryOffset + nodeOffset;
                if (nodeOffset < 16 || !HasRange(uld, nodeStart, 28))
                {
                    nodeStart = entryOffset + 16;
                }

                int cursor = nodeStart;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, 28); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + 24);
                    AddUldTextNodeFont(uld, cursor, results);
                    if (nodeSize <= 0)
                    {
                        break;
                    }

                    cursor += nodeSize;
                }

                if (componentSize == 0)
                {
                    entryOffset = cursor;
                }
                else
                {
                    entryOffset += componentSize;
                }
            }
        }

        private static void WalkUldWidgetTextNodes(byte[] uld, int atkOffset, List<UldTextNodeFont> results)
        {
            uint widgetRelativeOffset = Endian.ReadUInt32LE(uld, atkOffset + 24);
            if (widgetRelativeOffset == 0 || widgetRelativeOffset > int.MaxValue)
            {
                return;
            }

            int widgetOffset = atkOffset + (int)widgetRelativeOffset;
            if (!HasRange(uld, widgetOffset, 16) || !HasAsciiMagic(uld, widgetOffset, "wdhd"))
            {
                return;
            }

            uint widgetCount = Endian.ReadUInt32LE(uld, widgetOffset + 8);
            int cursor = widgetOffset + 16;
            for (uint i = 0; i < widgetCount && HasRange(uld, cursor, 16); i++)
            {
                uint nodeCount = Endian.ReadUInt16LE(uld, cursor + 12);
                cursor += 16;
                for (uint nodeIndex = 0; nodeIndex < nodeCount && HasRange(uld, cursor, 28); nodeIndex++)
                {
                    int nodeSize = Endian.ReadUInt16LE(uld, cursor + 24);
                    AddUldTextNodeFont(uld, cursor, results);
                    if (nodeSize <= 0)
                    {
                        break;
                    }

                    cursor += nodeSize;
                }
            }
        }

        private static void AddUldTextNodeFont(byte[] uld, int nodeOffset, List<UldTextNodeFont> results)
        {
            const int NodeTypeOffset = 20;
            const int NodeSizeOffset = 24;
            const int NodeHeaderSize = 88;
            const int TextExtraMinSize = 24;
            const int TextFontOffsetInExtra = 10;
            const int TextFontSizeOffsetInExtra = 11;

            if (!HasRange(uld, nodeOffset, NodeHeaderSize + TextExtraMinSize))
            {
                return;
            }

            int nodeType = unchecked((int)Endian.ReadUInt32LE(uld, nodeOffset + NodeTypeOffset));
            int nodeSize = Endian.ReadUInt16LE(uld, nodeOffset + NodeSizeOffset);
            if (nodeType != 3 || nodeSize < NodeHeaderSize + TextExtraMinSize)
            {
                return;
            }

            int fontOffset = nodeOffset + NodeHeaderSize + TextFontOffsetInExtra;
            if (!HasRange(uld, fontOffset, 1))
            {
                return;
            }

            int fontSizeOffset = nodeOffset + NodeHeaderSize + TextFontSizeOffsetInExtra;
            results.Add(new UldTextNodeFont
            {
                NodeOffset = nodeOffset,
                NodeSize = nodeSize,
                FontId = uld[fontOffset],
                FontSize = uld[fontSizeOffset]
            });
        }

        private static bool HasRange(byte[] data, int offset, int length)
        {
            return data != null &&
                   offset >= 0 &&
                   length >= 0 &&
                   offset <= data.Length - length;
        }

        private static bool HasAsciiMagic(byte[] data, int offset, string magic)
        {
            if (string.IsNullOrEmpty(magic) || !HasRange(data, offset, magic.Length))
            {
                return false;
            }

            for (int i = 0; i < magic.Length; i++)
            {
                if (data[offset + i] != (byte)magic[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte LanguageToId(string language)
        {
            string normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "ja": return 1;
                case "en": return 2;
                case "de": return 3;
                case "fr": return 4;
                case "chs": return 5;
                case "cht": return 6;
                case "ko": return 7;
                default:
                    throw new ArgumentException("unsupported language: " + language);
            }
        }

        private static bool GlyphSpacingMetricsMatch(FdtGlyphEntry sourceGlyph, FdtGlyphEntry targetGlyph)
        {
            return sourceGlyph.ShiftJisValue == targetGlyph.ShiftJisValue &&
                   sourceGlyph.Width == targetGlyph.Width &&
                   sourceGlyph.Height == targetGlyph.Height &&
                   sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                   sourceGlyph.OffsetY == targetGlyph.OffsetY;
        }

        private static string FormatGlyphSpacing(FdtGlyphEntry glyph)
        {
            return "sjis=0x" + glyph.ShiftJisValue.ToString("X4") +
                ", size=" + glyph.Width.ToString() + "x" + glyph.Height.ToString() +
                ", offset=" + glyph.OffsetX.ToString() + "/" + glyph.OffsetY.ToString();
        }

        private static Dictionary<string, byte[]> ReadAsciiKerningEntries(byte[] fdt)
        {
            Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (fdt == null || fdt.Length < FdtHeaderSize)
            {
                return entries;
            }

            uint headerOffset = Endian.ReadUInt32LE(fdt, 0x0C);
            if (headerOffset == 0 ||
                headerOffset > int.MaxValue ||
                headerOffset < FdtHeaderSize ||
                headerOffset > fdt.Length - FdtKerningHeaderSize)
            {
                return entries;
            }

            int kerningHeaderOffset = checked((int)headerOffset);
            if (!HasAsciiMagic(fdt, kerningHeaderOffset, "knhd"))
            {
                return entries;
            }

            uint kerningCount = Endian.ReadUInt32LE(fdt, kerningHeaderOffset + 0x04);
            int kerningStart = kerningHeaderOffset + FdtKerningHeaderSize;
            long kerningBytes = (long)kerningCount * FdtKerningEntrySize;
            if (kerningBytes < 0 || kerningStart > fdt.Length || kerningStart + kerningBytes > fdt.Length)
            {
                return entries;
            }

            for (int i = 0; i < kerningCount; i++)
            {
                int offset = kerningStart + i * FdtKerningEntrySize;
                uint left = Endian.ReadUInt32LE(fdt, offset);
                uint right = Endian.ReadUInt32LE(fdt, offset + 4);
                if (!IsAsciiKerningKey(left) || !IsAsciiKerningKey(right))
                {
                    continue;
                }

                byte[] entry = new byte[FdtKerningEntrySize];
                Buffer.BlockCopy(fdt, offset, entry, 0, FdtKerningEntrySize);
                entries[left.ToString("X2") + ":" + right.ToString("X2")] = entry;
            }

            return entries;
        }

        private static bool IsAsciiKerningKey(uint value)
        {
            return value >= 0x20 && value <= 0x7E;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryFindGlyph(byte[] fdt, uint codepoint, out FdtGlyphEntry glyph)
        {
            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                glyph = new FdtGlyphEntry();
                return false;
            }

            uint utf8Value = PackUtf8(codepoint);
            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                if (Endian.ReadUInt32LE(fdt, offset) == utf8Value)
                {
                    glyph = new FdtGlyphEntry
                    {
                        ShiftJisValue = Endian.ReadUInt16LE(fdt, offset + 4),
                        ImageIndex = Endian.ReadUInt16LE(fdt, offset + 6),
                        X = Endian.ReadUInt16LE(fdt, offset + 8),
                        Y = Endian.ReadUInt16LE(fdt, offset + 10),
                        Width = fdt[offset + 12],
                        Height = fdt[offset + 13],
                        OffsetX = unchecked((sbyte)fdt[offset + 14]),
                        OffsetY = unchecked((sbyte)fdt[offset + 15])
                    };
                    return true;
                }
            }

            glyph = new FdtGlyphEntry();
            return false;
        }

        private static bool TryGetFdtGlyphTable(byte[] fdt, out int fontTableOffset, out uint glyphCount, out int glyphStart)
        {
            fontTableOffset = 0;
            glyphCount = 0;
            glyphStart = 0;
            if (fdt == null || fdt.Length < FdtHeaderSize + FdtFontTableHeaderSize)
            {
                return false;
            }

            uint fontTableHeaderOffset = Endian.ReadUInt32LE(fdt, 0x08);
            if (fontTableHeaderOffset >= fdt.Length || fontTableHeaderOffset > int.MaxValue)
            {
                return false;
            }

            fontTableOffset = checked((int)fontTableHeaderOffset);
            if (fontTableOffset + FdtFontTableHeaderSize > fdt.Length)
            {
                return false;
            }

            glyphCount = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x04);
            glyphStart = fontTableOffset + FdtFontTableHeaderSize;
            long glyphBytes = (long)glyphCount * FdtGlyphEntrySize;
            return glyphBytes >= 0 && glyphStart <= fdt.Length && glyphStart + glyphBytes <= fdt.Length;
        }

        private static uint PackUtf8(uint codepoint)
        {
            if (codepoint <= 0x7F)
            {
                return codepoint;
            }

            if (codepoint <= 0x7FF)
            {
                return (uint)((0xC0 | (codepoint >> 6)) << 8 | (0x80 | (codepoint & 0x3F)));
            }

            if (codepoint <= 0xFFFF)
            {
                return (uint)((0xE0 | (codepoint >> 12)) << 16 |
                              (0x80 | ((codepoint >> 6) & 0x3F)) << 8 |
                              (0x80 | (codepoint & 0x3F)));
            }

            return (uint)((0xF0 | (codepoint >> 18)) << 24 |
                          (0x80 | ((codepoint >> 12) & 0x3F)) << 16 |
                          (0x80 | ((codepoint >> 6) & 0x3F)) << 8 |
                          (0x80 | (codepoint & 0x3F)));
        }

        private static string ResolveFontTexturePath(string fdtPath, int imageIndex)
        {
            string normalized = fdtPath.Replace('\\', '/').ToLowerInvariant();
            if (normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FontKrnTexturePath;
            }

            int textureIndex = imageIndex / 4;
            bool lobby = normalized.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0;
            if (lobby)
            {
                switch (textureIndex)
                {
                    case 0: return FontLobby1TexturePath;
                    case 1: return FontLobby2TexturePath;
                    case 2: return FontLobby3TexturePath;
                    case 3: return FontLobby4TexturePath;
                    case 4: return FontLobby5TexturePath;
                    case 5: return FontLobby6TexturePath;
                    case 6: return FontLobby7TexturePath;
                    default: return null;
                }
            }

            switch (textureIndex)
            {
                case 0: return Font1TexturePath;
                case 1: return Font2TexturePath;
                case 2: return Font3TexturePath;
                case 3: return Font4TexturePath;
                case 4: return Font5TexturePath;
                case 5: return Font6TexturePath;
                case 6: return Font7TexturePath;
                default: return null;
            }
        }

        private static byte[] UnpackTextureFile(byte[] packed)
        {
            uint headerSize = ReadU32(packed, 0);
            uint fileType = ReadU32(packed, 4);
            uint decompressedSize = ReadU32(packed, 8);
            uint blockCount = ReadU32(packed, 20);
            if (fileType != 4)
            {
                throw new InvalidDataException("Not a texture entry. Type=" + fileType);
            }

            int locatorOffset = 24;
            int subBlockOffset = checked(locatorOffset + (int)blockCount * 20);
            using (MemoryStream output = new MemoryStream((int)decompressedSize))
            {
                for (int i = 0; i < blockCount; i++)
                {
                    int loc = locatorOffset + i * 20;
                    uint firstBlockOffset = ReadU32(packed, loc);
                    uint decompressedBlockSize = ReadU32(packed, loc + 8);
                    uint firstSubBlockIndex = ReadU32(packed, loc + 12);
                    uint subBlockCount = ReadU32(packed, loc + 16);

                    if (i == 0)
                    {
                        int textureHeaderStart = (int)headerSize;
                        int textureHeaderLength = checked((int)firstBlockOffset);
                        output.Write(packed, textureHeaderStart, textureHeaderLength);
                    }

                    int blockOffset = checked((int)headerSize + (int)firstBlockOffset);
                    long before = output.Length;
                    for (int s = 0; s < subBlockCount; s++)
                    {
                        int blockHeaderOffset = blockOffset;
                        uint blockHeaderSize = ReadU32(packed, blockHeaderOffset);
                        uint compressedSize = ReadU32(packed, blockHeaderOffset + 8);
                        uint rawSize = ReadU32(packed, blockHeaderOffset + 12);
                        if (blockHeaderSize != 16)
                        {
                            throw new InvalidDataException("Unexpected texture block header size.");
                        }

                        if (compressedSize == UncompressedBlock)
                        {
                            output.Write(packed, blockHeaderOffset + 16, (int)rawSize);
                        }
                        else
                        {
                            using (MemoryStream source = new MemoryStream(packed, blockHeaderOffset + 16, (int)compressedSize, false))
                            using (DeflateStream deflate = new DeflateStream(source, CompressionMode.Decompress))
                            {
                                deflate.CopyTo(output);
                            }
                        }

                        ushort paddedBlockSize = ReadU16(packed, subBlockOffset + checked(((int)firstSubBlockIndex + s) * 2));
                        blockOffset += paddedBlockSize;
                    }

                    if (output.Length - before != decompressedBlockSize)
                    {
                        throw new InvalidDataException("Unexpected texture block size.");
                    }
                }

                if (output.Length != decompressedSize)
                {
                    throw new InvalidDataException("Unexpected texture size.");
                }

                return output.ToArray();
            }
        }

        private static Texture ReadA4R4G4B4Texture(byte[] tex)
        {
            uint format = ReadU32(tex, 4);
            if (format != 0x1440)
            {
                throw new InvalidDataException("Unsupported texture format: 0x" + format.ToString("X"));
            }

            int width = ReadU16(tex, 8);
            int height = ReadU16(tex, 10);
            int offset = checked((int)ReadU32(tex, 0x1C));
            byte[] data = new byte[checked(width * height * 2)];
            Buffer.BlockCopy(tex, offset, data, 0, data.Length);
            return new Texture { Width = width, Height = height, Data = data };
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return data[offset] |
                ((uint)data[offset + 1] << 8) |
                ((uint)data[offset + 2] << 16) |
                ((uint)data[offset + 3] << 24);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("X2"));
            }

            return builder.ToString();
        }

        private struct FdtGlyphEntry
        {
            public ushort ShiftJisValue;
            public ushort ImageIndex;
            public ushort X;
            public ushort Y;
            public byte Width;
            public byte Height;
            public sbyte OffsetX;
            public sbyte OffsetY;
        }

        private struct Texture
        {
            public int Width;
            public int Height;
            public byte[] Data;
        }

        private struct GlyphCanvas
        {
            public byte[] Alpha;
            public int VisiblePixels;
            public FdtGlyphEntry Glyph;
            public string TexturePath;
        }

        private struct PhraseLayoutResult
        {
            public int Glyphs;
            public int Width;
            public int OverlapPixels;
        }

        private struct GlyphStats
        {
            public int ComponentCount;
            public int SmallComponentCount;
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
        }

        private struct UldTextNodeFont
        {
            public int NodeOffset;
            public int NodeSize;
            public byte FontId;
            public byte FontSize;
        }

    }
}
