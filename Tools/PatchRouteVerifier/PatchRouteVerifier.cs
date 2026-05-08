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

    }
}
