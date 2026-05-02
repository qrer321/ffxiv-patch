using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static class PatchRouteVerifier
    {
        private const string TextPrefix = "0a0000.win32";
        private const string FontPrefix = "000000.win32";
        private const string UiPrefix = "060000.win32";
        private const string DataCenterTitleUldPath = "ui/uld/Title_DataCenter.uld";
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
        private const int FdtHeaderSize = 0x20;
        private const int FdtFontTableHeaderSize = 0x20;
        private const int FdtGlyphEntrySize = 0x10;
        private const byte UldTrumpGothicFontId = 3;
        private const byte UldJupiterFontId = 4;
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

                Verifier verifier = new Verifier(output, appliedSqpack, globalSqpack, language);
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
            Console.WriteLine("PatchRouteVerifier.exe --output <patch-output-dir> --global <global-game-dir> [--applied-game <game-dir>] [--target-language ja]");
        }

        private sealed class Verifier
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

            public bool Failed { get; private set; }

            public Verifier(string output, string patchedSqpack, string globalSqpack, string language)
            {
                _output = output;
                _patchedSqpack = patchedSqpack;
                _globalSqpack = globalSqpack;
                _language = language;

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
                    Console.WriteLine();

                    VerifyDataCenterRows();
                    VerifyDataCenterRowsAllGlobalLanguageSlots();
                    VerifyDataCenterTitleUldRoute();
                    VerifyCompactTimeRows();
                    VerifyWorldVisitRows();
                    VerifyDataCenterTitleGlyphs();
                    VerifyNumericGlyphs();
                    VerifyProtectedHangulGlyphs();
                    VerifyPartyListSelfMarker();
                    VerifyLobbyHangulVisibility();

                    Console.WriteLine();
                    Console.WriteLine(Failed ? "RESULT: FAIL" : "RESULT: PASS");
                }
            }

            private void VerifyDataCenterRows()
            {
                Console.WriteLine("[EXD] Data center selection labels");
                ExpectText("Lobby", 791, "Japan Data Center");
                ExpectText("Lobby", 792, "North America Data Center");
                ExpectText("Lobby", 793, "Europe Data Center");
                ExpectText("Lobby", 794, "Oceania Data Center");
                ExpectText("WorldRegionGroup", 1, "Japan");
                ExpectText("WorldRegionGroup", 2, "North America");
                ExpectText("WorldRegionGroup", 3, "Europe");
                ExpectText("WorldRegionGroup", 4, "Oceania");
                ExpectText("WorldPhysicalDC", 1, "Japan");
                ExpectText("WorldPhysicalDC", 2, "North America");
                ExpectText("WorldPhysicalDC", 3, "Europe");
                ExpectText("WorldPhysicalDC", 4, "Oceania");
                ExpectText("WorldDCGroupType", 1, "Elemental");
                ExpectText("WorldDCGroupType", 2, "Gaia");
                ExpectText("WorldDCGroupType", 3, "Mana");
                ExpectText("WorldDCGroupType", 4, "Aether");
                ExpectText("WorldDCGroupType", 5, "Primal");
                ExpectText("WorldDCGroupType", 6, "Chaos");
                ExpectText("WorldDCGroupType", 7, "Light");
                ExpectText("WorldDCGroupType", 8, "Crystal");
                ExpectText("WorldDCGroupType", 9, "Materia");
                ExpectText("WorldDCGroupType", 10, "Meteor");
                ExpectText("WorldDCGroupType", 11, "Dynamis");
                ExpectText("WorldDCGroupType", 12, "Shadow");
                ExpectText("WorldDCGroupType", 13, "NA Cloud DC (Beta)");
            }

            private void VerifyDataCenterRowsAllGlobalLanguageSlots()
            {
                Console.WriteLine("[EXD] Data center labels in all global language slots");
                string[] languages = { "ja", "en", "de", "fr" };

                DataCenterLabelExpectation[] expectations =
                {
                    new DataCenterLabelExpectation("Lobby", 791, "Japan Data Center"),
                    new DataCenterLabelExpectation("Lobby", 792, "North America Data Center"),
                    new DataCenterLabelExpectation("Lobby", 793, "Europe Data Center"),
                    new DataCenterLabelExpectation("Lobby", 794, "Oceania Data Center"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 1, "Japan"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 2, "North America"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 3, "Europe"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 4, "Oceania"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 5, "China"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 6, "Korea"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 7, "NA Cloud"),
                    new DataCenterLabelExpectation("WorldRegionGroup", 8, "Traditional Chinese regions"),
                    new DataCenterLabelExpectation("WorldPhysicalDC", 1, "Japan"),
                    new DataCenterLabelExpectation("WorldPhysicalDC", 2, "North America"),
                    new DataCenterLabelExpectation("WorldPhysicalDC", 3, "Europe"),
                    new DataCenterLabelExpectation("WorldPhysicalDC", 4, "Oceania"),
                    new DataCenterLabelExpectation("WorldPhysicalDC", 5, "NA Cloud Data Center (Beta)"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 1, "Elemental"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 2, "Gaia"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 3, "Mana"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 4, "Aether"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 5, "Primal"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 6, "Chaos"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 7, "Light"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 8, "Crystal"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 9, "Materia"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 10, "Meteor"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 11, "Dynamis"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 12, "Shadow"),
                    new DataCenterLabelExpectation("WorldDCGroupType", 13, "NA Cloud DC (Beta)")
                };

                for (int i = 0; i < expectations.Length; i++)
                {
                    for (int languageIndex = 0; languageIndex < languages.Length; languageIndex++)
                    {
                        ExpectDataCenterLabel(expectations[i], languages[languageIndex]);
                    }
                }
            }

            private void VerifyDataCenterTitleUldRoute()
            {
                Console.WriteLine("[ULD/FDT] Data center title render route");
                byte[] cleanUld = _cleanUi.ReadFile(DataCenterTitleUldPath);
                byte[] patchedUld = _patchedUi.ReadFile(DataCenterTitleUldPath);
                List<UldTextNodeFont> cleanFonts = GetUldTextNodeFonts(cleanUld);
                Dictionary<int, UldTextNodeFont> patchedByOffset = GetUldTextNodeFontsByOffset(patchedUld);

                int patchedTrumpGothicCount = 0;
                int patchedJupiterCount = 0;
                foreach (UldTextNodeFont patchedNode in patchedByOffset.Values)
                {
                    if (patchedNode.FontId == UldTrumpGothicFontId)
                    {
                        patchedTrumpGothicCount++;
                    }

                    if (patchedNode.FontId == UldJupiterFontId)
                    {
                        patchedJupiterCount++;
                    }
                }

                if (patchedTrumpGothicCount == 0 && patchedJupiterCount >= 6)
                {
                    Pass("{0} text node fonts patched: TrumpGothic={1}, Jupiter={2}", DataCenterTitleUldPath, patchedTrumpGothicCount, patchedJupiterCount);
                }
                else
                {
                    Fail("{0} text node fonts unexpected: TrumpGothic={1}, Jupiter={2}", DataCenterTitleUldPath, patchedTrumpGothicCount, patchedJupiterCount);
                }

                HashSet<string> routedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cleanFonts.Count; i++)
                {
                    UldTextNodeFont cleanNode = cleanFonts[i];
                    UldTextNodeFont patchedNode;
                    if (!patchedByOffset.TryGetValue(cleanNode.NodeOffset, out patchedNode))
                    {
                        Fail("{0} missing patched text node at 0x{1:X}", DataCenterTitleUldPath, cleanNode.NodeOffset);
                        continue;
                    }

                    if (cleanNode.FontId == UldTrumpGothicFontId && patchedNode.FontId != UldJupiterFontId)
                    {
                        Fail("{0} node 0x{1:X} expected font {2}, actual {3}", DataCenterTitleUldPath, cleanNode.NodeOffset, UldJupiterFontId, patchedNode.FontId);
                    }
                    else if (cleanNode.FontId == UldTrumpGothicFontId)
                    {
                        string resolvedFont = ResolveUldFontPath(patchedNode.FontId, patchedNode.FontSize, true);
                        if (resolvedFont == null)
                        {
                            Fail(
                                "{0} node 0x{1:X} font {2}->{3} size={4} has no verifier font mapping",
                                DataCenterTitleUldPath,
                                cleanNode.NodeOffset,
                                cleanNode.FontId,
                                patchedNode.FontId,
                                patchedNode.FontSize);
                        }
                        else
                        {
                            routedFontPaths.Add(resolvedFont);
                            Pass(
                                "{0} node 0x{1:X} font {2}/{3}->{4}/{5} routes to {6}",
                                DataCenterTitleUldPath,
                                cleanNode.NodeOffset,
                                cleanNode.FontId,
                                cleanNode.FontSize,
                                patchedNode.FontId,
                                patchedNode.FontSize,
                                resolvedFont);
                        }
                    }
                }

                string[] labels =
                {
                    "NORTH AMERICA DATA CENTER",
                    "EUROPE DATA CENTER",
                    "JAPAN DATA CENTER",
                    "OCEANIA DATA CENTER",
                    "Elemental",
                    "Gaia",
                    "Mana",
                    "Meteor",
                    "Aether",
                    "Crystal",
                    "Dynamis",
                    "Primal",
                    "Chaos",
                    "Light",
                    "Materia"
                };

                if (routedFontPaths.Count == 0)
                {
                    Fail("{0} did not route any data-center title node to a verifiable font", DataCenterTitleUldPath);
                }

                foreach (string routedFontPath in routedFontPaths)
                {
                    VerifyLabelGlyphsEqualClean(routedFontPath, labels);
                }
            }

            private void VerifyCompactTimeRows()
            {
                Console.WriteLine("[EXD] Compact time labels");
                ExpectTextContains("Addon", 44, "m");
                ExpectTextContains("Addon", 45, "h");
                ExpectTextContains("Addon", 49, "s");
                ExpectTextContains("Addon", 2338, "h");
                ExpectTextContains("Addon", 2338, "m");
                ExpectTextContains("Addon", 6166, "h");
                ExpectTextContains("Addon", 6166, "m");
                ExpectTextContains("Addon", 876, "\uE028");
                ExpectTextNotContains("Addon", 876, "분");
                ExpectText("Addon", 8291, "5m");
                ExpectText("Addon", 8292, "10m");
                ExpectText("Addon", 8293, "30m");
                ExpectText("Addon", 8294, "60m");
                ExpectTextNotContains("Addon", 8291, "분");
                ExpectTextNotContains("Addon", 8292, "분");
                ExpectTextNotContains("Addon", 8293, "분");
                ExpectTextNotContains("Addon", 8294, "분");
            }

            private void VerifyWorldVisitRows()
            {
                Console.WriteLine("[EXD] World visit labels");
                ExpectText("Addon", 12510, "서버 텔레포");
                ExpectText("Addon", 12511, "서버 텔레포");
                ExpectText("Addon", 12520, "서버 텔레포 예약 신청 중");
                ExpectText("Addon", 12524, "서버 텔레포");
                ExpectText("Addon", 12537, "서버 텔레포");
            }

            private void VerifyDataCenterTitleGlyphs()
            {
                Console.WriteLine("[FDT] Data center title ASCII glyphs");
                string[] fonts =
                {
                    "common/font/AXIS_12_lobby.fdt",
                    "common/font/AXIS_14_lobby.fdt",
                    "common/font/AXIS_18_lobby.fdt",
                    "common/font/AXIS_36_lobby.fdt",
                    "common/font/Jupiter_45_lobby.fdt",
                    "common/font/Jupiter_90_lobby.fdt",
                    "common/font/Meidinger_40_lobby.fdt",
                    "common/font/MiedingerMid_36_lobby.fdt",
                    "common/font/TrumpGothic_23_lobby.fdt",
                    "common/font/TrumpGothic_34_lobby.fdt",
                    "common/font/TrumpGothic_68_lobby.fdt",
                    "common/font/TrumpGothic_184_lobby.fdt"
                };
                uint[] codepoints =
                {
                    'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                    'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                    'a', 'c', 'e', 'h', 'i', 'l', 'm', 'n', 'o', 'p', 'r', 's', 't', 'u', 'y'
                };
                for (int f = 0; f < fonts.Length; f++)
                {
                    for (int c = 0; c < codepoints.Length; c++)
                    {
                        ExpectGlyphEqualIfSourceExists(_cleanFont, fonts[f], codepoints[c], _patchedFont, fonts[f], codepoints[c]);
                    }
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

                VerifyPartyListSelfMarkerGlyph(0xE031);

                for (uint codepoint = 0xE0E1; codepoint <= 0xE0E8; codepoint++)
                {
                    VerifyPartyListSelfMarkerGlyph(codepoint);
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
                        ExpectGlyphVisibleAtLeast(_patchedFont, fonts[f], codepoints[c], 350);
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

            private void ExpectDataCenterLabel(DataCenterLabelExpectation expectation, string language)
            {
                List<string> columns = GetStringColumns(_patchedText, expectation.Sheet, expectation.RowId, language);
                bool found = false;
                bool fallback = false;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (string.Equals(columns[i], expectation.Expected, StringComparison.Ordinal))
                    {
                        found = true;
                    }

                    if (LooksLikeMissingGlyphFallback(columns[i]))
                    {
                        fallback = true;
                    }
                }

                if (found && !fallback)
                {
                    Pass("{0}#{1}/{2} contains [{3}]", expectation.Sheet, expectation.RowId, language, expectation.Expected);
                    return;
                }

                Fail(
                    "{0}#{1}/{2} expected [{3}], columns [{4}]",
                    expectation.Sheet,
                    expectation.RowId,
                    language,
                    expectation.Expected,
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
                    if (score == 0 && source.VisiblePixels > 0 && target.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} -> {2} U+{3:X4}", sourceFdtPath, sourceCodepoint, targetFdtPath, targetCodepoint);
                        return;
                    }

                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} mismatch score={4} visible={5}/{6}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        score,
                        source.VisiblePixels,
                        target.VisiblePixels);
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

                return new GlyphCanvas { Alpha = alpha, VisiblePixels = visible };
            }

            private static byte[] RenderGlyphAlpha(Texture texture, FdtGlyphEntry glyph)
            {
                const int canvasSize = 96;
                byte[] canvas = new byte[canvasSize * canvasSize];
                int channel = glyph.ImageIndex % 4;
                int startX = 32 + glyph.OffsetX;
                int startY = 32 + glyph.OffsetY;
                for (int y = 0; y < glyph.Height; y++)
                {
                    int dy = startY + y;
                    for (int x = 0; x < glyph.Width; x++)
                    {
                        int dx = startX + x;
                        if (dx < 0 || dy < 0 || dx >= canvasSize || dy >= canvasSize)
                        {
                            continue;
                        }

                        int sourceX = glyph.X + x;
                        int sourceY = glyph.Y + y;
                        int pixelOffset = (sourceY * texture.Width + sourceX) * 2;
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

                        canvas[dy * canvasSize + dx] = (byte)(nibble * 17);
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
        }

        private struct UldTextNodeFont
        {
            public int NodeOffset;
            public int NodeSize;
            public byte FontId;
            public byte FontSize;
        }

        private sealed class DataCenterLabelExpectation
        {
            public readonly string Sheet;
            public readonly uint RowId;
            public readonly string Expected;

            public DataCenterLabelExpectation(string sheet, uint rowId, string expected)
            {
                Sheet = sheet;
                RowId = rowId;
                Expected = expected;
            }
        }
    }
}
