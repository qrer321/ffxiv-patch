using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class FontPatchGenerator
    {
        // FFXIV sqpack repository that contains common/font resources.
        private const string RepositoryDir = "sqpack\\ffxiv";

        // 000000 is the common package. The font patch writes selected font entries into dat1.
        private const string IndexFileName = "000000.win32.index";
        private const string Index2FileName = "000000.win32.index2";
        private const string Dat0FileName = "000000.win32.dat0";
        private const string Dat1FileName = "000000.win32.dat1";
        private const string TextIndexFileName = "0a0000.win32.index";
        private const string TextDatPrefix = "0a0000.win32";

        // Clean index copies used by the UI for rollback without deleting dat1 manually.
        private const string OrigIndexFileName = "orig.000000.win32.index";
        private const string OrigIndex2FileName = "orig.000000.win32.index2";

        // TTMP package exported from the original generator flow; preferred over raw Korean client copy.
        private const string TtmpMpdFileName = "TTMPD.mpd";
        private const string TtmpMplFileName = "TTMPL.mpl";

        // FDT/FontCsv layout used by common/font/*.fdt.
        // The first glyph key stores the UTF-8 byte sequence as a big-endian integer
        // value, then the file stores that uint32 little-endian. For U+E0E1
        // (EE 83 A1), the table value is 0x00EE83A1 and the file bytes are
        // A1 83 EE 00.
        // Some Japanese-client UI render paths still consult Shift-JIS glyph keys.
        private const int FdtHeaderSize = 0x20;
        private const int FdtFontTableHeaderSize = 0x20;
        private const int FdtGlyphEntrySize = 0x10;
        private const int FdtKerningHeaderSize = 0x10;
        private const int FdtKerningEntrySize = 0x10;
        private static readonly uint[] PartyListSelfMarkerPrimaryStarts = new uint[] { 0xE031u, 0xE0E1u };
        private static readonly int[] PartyListSelfMarkerPrimaryCounts = new int[] { 1, 8 };
        private static readonly uint[] PartyListProtectedPuaGlyphs = new uint[]
        {
            0xE031u,
            0xE037u,
            0xE0E1u, 0xE0E2u, 0xE0E3u, 0xE0E4u,
            0xE0E5u, 0xE0E6u, 0xE0E7u, 0xE0E8u
        };
        private const uint PartyListSelfMarkerLegacyStart = 0xE0B1u;
        private const uint HangulFirst = 0xAC00u;
        private const uint HangulLast = 0xD7A3u;
        private const uint DialogueByeonCodepoint = 0xBCC0u;
        private const string DialogueGlyphArtifactSourcePath = "common/font/KrnAXIS_180.fdt";
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
        private const int CleanAsciiFirst = 0x20;
        private const int CleanAsciiLast = 0x7E;
        private const int CleanAsciiTexturePadding = 4;
        private const int LobbyGlyphTextureNeighborhoodPadding = 0;
        private const int InGameGlyphTextureNeighborhoodPadding = 8;
        private const int DamageNumberGlyphTextureNeighborhoodPadding = 16;
        private const int ActionDetailHighScaleGlyphTexturePadding = 2;
        private static readonly bool EnableLobbyHangulAllocatedGlyphs = true;
        private static readonly bool EnableLegacyLobbyHangulSourceCellGrafting = false;

        // Explicit font resource set used by the global client for in-game and lobby text rendering.
        private static readonly string[] FontPaths = new string[]
        {
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
            "common/font/Meidinger_16_lobby.fdt",
            "common/font/Meidinger_20_lobby.fdt",
            "common/font/Meidinger_40.fdt",
            "common/font/Meidinger_40_lobby.fdt",
            "common/font/MiedingerMid_10_lobby.fdt",
            "common/font/MiedingerMid_12_lobby.fdt",
            "common/font/MiedingerMid_14_lobby.fdt",
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
            "common/font/TrumpGothic_184_lobby.fdt",
            "common/font/AXIS_12.fdt",
            "common/font/AXIS_12_lobby.fdt",
            "common/font/AXIS_14.fdt",
            "common/font/AXIS_14_lobby.fdt",
            "common/font/AXIS_18.fdt",
            "common/font/AXIS_18_lobby.fdt",
            "common/font/AXIS_36.fdt",
            "common/font/AXIS_36_lobby.fdt",
            "common/font/AXIS_96.fdt",
            "common/font/KrnAXIS_120.fdt",
            "common/font/KrnAXIS_140.fdt",
            "common/font/KrnAXIS_180.fdt",
            "common/font/KrnAXIS_360.fdt",
            "common/font/Jupiter_16.fdt",
            "common/font/Jupiter_20.fdt",
            "common/font/Meidinger_16.fdt",
            "common/font/Meidinger_20.fdt",
            "common/font/MiedingerMid_10.fdt",
            "common/font/MiedingerMid_12.fdt",
            "common/font/MiedingerMid_14.fdt",
            "common/font/MiedingerMid_18.fdt",
            "common/font/font1.tex",
            "common/font/font_lobby1.tex",
            "common/font/font2.tex",
            "common/font/font_lobby2.tex",
            "common/font/font3.tex",
            "common/font/font4.tex",
            "common/font/font5.tex",
            "common/font/font6.tex",
            "common/font/font7.tex",
            "common/font/font_lobby3.tex",
            "common/font/font_lobby4.tex",
            "common/font/font_lobby5.tex",
            "common/font/font_lobby6.tex",
            "common/font/font_lobby7.tex",
            "common/font/font_krn_1.tex"
        };

        private static readonly string[] FontOnlyPatchPaths = new string[]
        {
            "common/font/KrnAXIS_120.fdt",
            "common/font/KrnAXIS_140.fdt",
            "common/font/KrnAXIS_180.fdt",
            "common/font/KrnAXIS_360.fdt",
            "common/font/font_krn_1.tex"
        };

        private static readonly DerivedLobbyFontSpec[] Derived4kLobbyFonts = new DerivedLobbyFontSpec[]
        {
            new DerivedLobbyFontSpec("common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/Jupiter_46_lobby.fdt", "common/font/Jupiter_46.fdt"),
            new DerivedLobbyFontSpec("common/font/Jupiter_90_lobby.fdt", "common/font/Jupiter_46.fdt"),
            new DerivedLobbyFontSpec("common/font/Meidinger_40_lobby.fdt", "common/font/MiedingerMid_36.fdt"),
            new DerivedLobbyFontSpec("common/font/MiedingerMid_36_lobby.fdt", "common/font/MiedingerMid_36.fdt"),
            new DerivedLobbyFontSpec("common/font/TrumpGothic_68_lobby.fdt", "common/font/TrumpGothic_68.fdt")
        };

        private static readonly DerivedLobbyFontSpec[] LobbyHangulSourceFonts = new DerivedLobbyFontSpec[]
        {
            new DerivedLobbyFontSpec("common/font/MiedingerMid_12_lobby.fdt", "common/font/AXIS_12_lobby.fdt"),
            new DerivedLobbyFontSpec("common/font/MiedingerMid_14_lobby.fdt", "common/font/AXIS_14_lobby.fdt"),
            new DerivedLobbyFontSpec("common/font/MiedingerMid_18_lobby.fdt", "common/font/AXIS_18_lobby.fdt"),
            new DerivedLobbyFontSpec("common/font/TrumpGothic_23_lobby.fdt", "common/font/AXIS_18_lobby.fdt"),
            new DerivedLobbyFontSpec("common/font/TrumpGothic_34_lobby.fdt", "common/font/AXIS_18_lobby.fdt"),
            new DerivedLobbyFontSpec("common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/Jupiter_46_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/Jupiter_90_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/Meidinger_40_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/MiedingerMid_36_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/TrumpGothic_68_lobby.fdt", "common/font/AXIS_36.fdt")
        };

        private static readonly LargeUiLabelVisualScaleSpec[] LargeUiLabelVisualScaleFonts = new LargeUiLabelVisualScaleSpec[]
        {
            new LargeUiLabelVisualScaleSpec("common/font/TrumpGothic_23.fdt", "common/font/TrumpGothic_23.fdt", "common/font/TrumpGothic_23.fdt"),
            new LargeUiLabelVisualScaleSpec("common/font/TrumpGothic_34.fdt", "common/font/TrumpGothic_34.fdt", "common/font/TrumpGothic_34.fdt"),
            new LargeUiLabelVisualScaleSpec("common/font/TrumpGothic_68.fdt", "common/font/TrumpGothic_34.fdt", "common/font/TrumpGothic_68.fdt")
        };

        private static readonly LobbyLargeLabelVisualScaleSpec[] LobbyLargeLabelVisualScaleFonts = new LobbyLargeLabelVisualScaleSpec[]
        {
        };

        private readonly BuildOptions _options;
        private readonly BuildReport _report;

        public FontPatchGenerator(BuildOptions options, BuildReport report)
        {
            _options = options;
            _report = report;
        }

        public void Build()
        {
            string globalGame = Path.GetFullPath(_options.GlobalGamePath);
            string koreaGame = Path.GetFullPath(_options.KoreaGamePath);
            string outputDir = Path.GetFullPath(_options.OutputPath);
            string globalSqpack = Path.Combine(globalGame, RepositoryDir);
            string koreaSqpack = Path.Combine(koreaGame, RepositoryDir);
            uint[] actionDetailHighScaleHangulCodepoints = _options.FontOnly
                ? null
                : CreateActionDetailHighScaleHangulCodepoints(outputDir, globalSqpack);
            LobbyHangulCodepointSets lobbyHangulCodepoints = _options.FontOnly
                ? null
                : CreateLobbyHangulCodepointSets(koreaSqpack);
            if (_options.FontOnly)
            {
                Console.WriteLine("Font-only scope: skipping full-patch lobby and ActionDetail font repairs.");
            }

            RequireFile(Path.Combine(globalSqpack, IndexFileName));
            RequireFile(Path.Combine(globalSqpack, Index2FileName));
            RequireFile(Path.Combine(globalSqpack, Dat0FileName));

            FontPatchPackage fontPackage = ResolveFontPatchPackage();
            if (fontPackage == null)
            {
                if (!_options.AllowKoreanFontFallback)
                {
                    throw new FileNotFoundException(
                        "TTMP font package files are required for reliable Korean font patching. " +
                        "Place TTMPD.mpd and TTMPL.mpl next to FFXIVPatchGenerator.exe, under FontPatchAssets, " +
                        "or pass --font-pack-dir <dir>. Use --allow-korean-font-fallback only for experiments.");
                }

                RequireFile(Path.Combine(koreaSqpack, IndexFileName));
                RequireFile(Path.Combine(koreaSqpack, Dat0FileName));
                AddLimitedWarning("TTMP font package was not found. Falling back to direct Korean client font copy.");
                Console.WriteLine("TTMP font package was not found. Falling back to direct Korean client font copy.");
            }
            else
            {
                Console.WriteLine("Using TTMP font package: {0}", fontPackage.DirectoryPath);
            }

            Console.WriteLine("Using font profile:      {0}", _options.FontPatchProfile);

            string currentGlobalIndex = Path.Combine(globalSqpack, IndexFileName);
            string originalGlobalIndex = Path.Combine(globalSqpack, OrigIndexFileName);
            string baseIndex = ResolveBaseIndex(currentGlobalIndex, originalGlobalIndex);
            string currentGlobalIndex2 = Path.Combine(globalSqpack, Index2FileName);
            string originalGlobalIndex2 = Path.Combine(globalSqpack, OrigIndex2FileName);
            string baseIndex2 = ResolveBaseIndex2(currentGlobalIndex2, originalGlobalIndex2);

            string outputIndex = Path.Combine(outputDir, IndexFileName);
            string outputIndex2 = Path.Combine(outputDir, Index2FileName);
            string outputOrigIndex = Path.Combine(outputDir, OrigIndexFileName);
            string outputOrigIndex2 = Path.Combine(outputDir, OrigIndex2FileName);
            string outputDat1 = Path.Combine(outputDir, Dat1FileName);

            File.Copy(baseIndex, outputOrigIndex, true);
            File.Copy(baseIndex, outputIndex, true);
            File.Copy(baseIndex2, outputOrigIndex2, true);
            File.Copy(baseIndex2, outputIndex2, true);

            Console.WriteLine("Using base global font index: {0}", baseIndex);
            Console.WriteLine("Using base global font index2:{0}", baseIndex2);

            using (SqPackArchive globalArchive = new SqPackArchive(baseIndex, globalSqpack, "000000.win32"))
            using (SqPackIndexFile mutableIndex = new SqPackIndexFile(outputIndex))
            using (SqPackIndex2File mutableIndex2 = new SqPackIndex2File(outputIndex2))
            using (SqPackDatWriter datWriter = new SqPackDatWriter(outputDat1, Path.Combine(globalSqpack, Dat0FileName)))
            {
                mutableIndex.EnsureDataFileCount(2);
                mutableIndex2.EnsureDataFileCount(2);

                if (fontPackage != null)
                {
                    WriteTtmpFontFiles(fontPackage, globalArchive, mutableIndex, mutableIndex2, datWriter, actionDetailHighScaleHangulCodepoints, lobbyHangulCodepoints);
                }
                else
                {
                    using (SqPackArchive koreaArchive = new SqPackArchive(Path.Combine(koreaSqpack, IndexFileName), koreaSqpack, "000000.win32"))
                    {
                        WriteKoreanFontFiles(koreaArchive, mutableIndex, mutableIndex2, datWriter);
                    }
                }

                mutableIndex.Save();
                mutableIndex2.Save();
            }

            ProgressReporter.Report(98, "폰트 패치 저장 완료");
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required file is missing.", path);
            }
        }

        private void WriteKoreanFontFiles(SqPackArchive koreaArchive, SqPackIndexFile mutableIndex, SqPackIndex2File mutableIndex2, SqPackDatWriter datWriter)
        {
            for (int i = 0; i < FontPaths.Length; i++)
            {
                string path = FontPaths[i];
                ProgressReporter.Report(90 + i * 8 / FontPaths.Length, "폰트 처리 중: " + (i + 1).ToString() + "/" + FontPaths.Length.ToString());
                if (!ShouldIncludeFontPath(path))
                {
                    _report.FontFilesSkippedByProfile++;
                    continue;
                }

                if (ShouldPreserveCleanGlobalLobbyPayload(path))
                {
                    _report.FontFilesSkippedByProfile++;
                    Console.WriteLine("  Preserved clean global lobby font payload: {0}", path);
                    continue;
                }

                if (!mutableIndex.ContainsPath(path))
                {
                    AddLimitedWarning("Missing global font target: " + path);
                    continue;
                }

                if (!mutableIndex2.ContainsPath(path))
                {
                    AddLimitedWarning("Missing global font index2 target: " + path);
                    continue;
                }

                byte[] packedFile;
                if (!koreaArchive.TryReadPackedFile(path, out packedFile))
                {
                    AddLimitedWarning("Missing Korean font source: " + path);
                    continue;
                }

                int normalized;
                long datOffset = WriteFontPayload(datWriter, path, packedFile, null, null, null, null, null, null, null, null, null, out normalized);
                LogFontPayloadAdjustments(path, normalized);
                mutableIndex.SetFileOffset(path, 1, datOffset);
                mutableIndex2.SetFileOffset(path, 1, datOffset);
                _report.FontFilesPatched++;
            }
        }

        private void WriteTtmpFontFiles(
            FontPatchPackage fontPackage,
            SqPackArchive globalArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            uint[] actionDetailHighScaleHangulCodepoints,
            LobbyHangulCodepointSets lobbyHangulCodepoints)
        {
            using (FileStream mpdStream = new FileStream(fontPackage.MpdPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                FontGlyphRepairContext glyphRepair = TryCreateFontGlyphRepairContext(fontPackage, mpdStream, globalArchive, _options.FontOnly);
                TargetedGlyphRepairContext dialogueGlyphRepair = TryCreateDialogueGlyphArtifactRepairContext(fontPackage, mpdStream);
                ProtectedHangulGlyphContext protectedHangulGlyphs = _options.FontOnly
                    ? TryCreateFontOnlyProtectedCleanGlyphContext(globalArchive)
                    : TryCreateProtectedHangulGlyphContext(fontPackage, mpdStream, globalArchive);
                Dictionary<string, List<FontTexturePatch>> texturePatches = new Dictionary<string, List<FontTexturePatch>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, byte[]> patchedTexturePayloadsByPath = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                LobbyHangulGlyphAllocationCache lobbyHangulAllocationCache = new LobbyHangulGlyphAllocationCache();
                Dictionary<string, FontPayload> payloadsByPath = new Dictionary<string, FontPayload>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fontPackage.Payloads.Count; i++)
                {
                    FontPayload payload = fontPackage.Payloads[i];
                    string payloadPath = NormalizeGamePath(payload.FullPath);
                    if (!payloadsByPath.ContainsKey(payloadPath))
                    {
                        payloadsByPath.Add(payloadPath, payload);
                    }
                }

                for (int i = 0; i < fontPackage.Payloads.Count; i++)
                {
                    FontPayload payload = fontPackage.Payloads[i];
                    string path = NormalizeGamePath(payload.FullPath);
                    ProgressReporter.Report(90 + i * 8 / fontPackage.Payloads.Count, "Font patching " + (i + 1).ToString() + "/" + fontPackage.Payloads.Count.ToString());

                    if (!ShouldIncludeFontPath(path))
                    {
                        _report.FontFilesSkippedByProfile++;
                        continue;
                    }

                    if (!mutableIndex.ContainsPath(path))
                    {
                        AddLimitedWarning("Missing global font target: " + path);
                        continue;
                    }

                    if (!mutableIndex2.ContainsPath(path))
                    {
                        AddLimitedWarning("Missing global font index2 target: " + path);
                        continue;
                    }

                    bool patchLobbyHangulFont = ShouldPatchLobbyHangulFont(path, SelectLobbyHangulCodepointsForFont(path, lobbyHangulCodepoints));
                    List<FontTexturePatch> pendingTexturePatches;
                    if (ShouldPreserveCleanGlobalLobbyPayload(path) && !patchLobbyHangulFont)
                    {
                        if (IsLobbyFontTexturePath(path) &&
                            texturePatches.TryGetValue(path, out pendingTexturePatches) &&
                            pendingTexturePatches.Count > 0)
                        {
                            byte[] cleanPackedTexture;
                            if (!globalArchive.TryReadPackedFile(path, out cleanPackedTexture))
                            {
                                AddLimitedWarning("Clean lobby texture source was not found: " + path);
                                continue;
                            }

                            byte[] patchedPackedTexture = PatchPackedFontTexture(path, cleanPackedTexture, pendingTexturePatches);
                            patchedTexturePayloadsByPath[path] = patchedPackedTexture;
                            long lobbyTextureOffset = datWriter.WritePackedFile(patchedPackedTexture);
                            mutableIndex.SetFileOffset(path, 1, lobbyTextureOffset);
                            mutableIndex2.SetFileOffset(path, 1, lobbyTextureOffset);
                            _report.FontFilesPatched++;
                            Console.WriteLine("  Patched clean-lobby Hangul texture cells: {0} ({1})", pendingTexturePatches.Count, path);
                            pendingTexturePatches.Clear();
                            continue;
                        }

                        _report.FontFilesSkippedByProfile++;
                        Console.WriteLine("  Preserved clean global lobby font payload: {0}", path);
                        continue;
                    }

                    byte[] packedFile;
                    if (patchLobbyHangulFont)
                    {
                        if (!globalArchive.TryReadPackedFile(path, out packedFile))
                        {
                            AddLimitedWarning("Clean lobby font source was not found: " + path);
                            continue;
                        }
                    }
                    else
                    {
                        packedFile = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, path);
                    }

                    int normalized;
                    long datOffset;
                    if (texturePatches.TryGetValue(path, out pendingTexturePatches) && pendingTexturePatches.Count > 0)
                    {
                        int protectedRestores = AppendProtectedHangulGlyphTexturePatches(path, pendingTexturePatches, protectedHangulGlyphs);
                        byte[] patchedPackedTexture = PatchPackedFontTexture(path, packedFile, pendingTexturePatches);
                        if (path.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
                        {
                            patchedTexturePayloadsByPath[path] = patchedPackedTexture;
                        }

                        datOffset = datWriter.WritePackedFile(patchedPackedTexture);
                        normalized = 0;
                        if (protectedRestores > 0)
                        {
                            Console.WriteLine("  Restored protected glyph cells: {0} ({1})", protectedRestores, path);
                        }

                        Console.WriteLine("  Patched repaired font texture cells: {0} ({1})", pendingTexturePatches.Count, path);
                        pendingTexturePatches.Clear();
                    }
                    else
                    {
                        datOffset = WriteFontPayload(datWriter, path, packedFile, mpdStream, payloadsByPath, dialogueGlyphRepair, glyphRepair, globalArchive, texturePatches, lobbyHangulAllocationCache, actionDetailHighScaleHangulCodepoints, lobbyHangulCodepoints, out normalized);
                    }

                    LogFontPayloadAdjustments(path, normalized);
                    mutableIndex.SetFileOffset(path, 1, datOffset);
                    mutableIndex2.SetFileOffset(path, 1, datOffset);
                    _report.FontFilesPatched++;
                }

                WriteSupplementalLobbyFontFiles(
                    globalArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    mpdStream,
                    payloadsByPath,
                    dialogueGlyphRepair,
                    glyphRepair,
                    texturePatches,
                    lobbyHangulAllocationCache,
                    actionDetailHighScaleHangulCodepoints,
                    lobbyHangulCodepoints);

                foreach (KeyValuePair<string, List<FontTexturePatch>> pair in texturePatches)
                {
                    if (pair.Value.Count > 0)
                    {
                        if (!ShouldIncludeFontPath(pair.Key))
                        {
                            AddLimitedWarning("Pending font texture patches were skipped by profile: " + pair.Key);
                            continue;
                        }

                        if (!mutableIndex.ContainsPath(pair.Key) || !mutableIndex2.ContainsPath(pair.Key))
                        {
                            AddLimitedWarning("Pending font texture target was not found: " + pair.Key);
                            continue;
                        }

                        byte[] basePackedTexture;
                        if (!patchedTexturePayloadsByPath.TryGetValue(pair.Key, out basePackedTexture) &&
                            !globalArchive.TryReadPackedFile(pair.Key, out basePackedTexture))
                        {
                            AddLimitedWarning("Pending font texture source was not found: " + pair.Key);
                            continue;
                        }

                        int protectedRestores = AppendProtectedHangulGlyphTexturePatches(pair.Key, pair.Value, protectedHangulGlyphs);
                        byte[] patchedPackedTexture = PatchPackedFontTexture(pair.Key, basePackedTexture, pair.Value);
                        patchedTexturePayloadsByPath[pair.Key] = patchedPackedTexture;
                        long datOffset = datWriter.WritePackedFile(patchedPackedTexture);
                        mutableIndex.SetFileOffset(pair.Key, 1, datOffset);
                        mutableIndex2.SetFileOffset(pair.Key, 1, datOffset);
                        _report.FontFilesPatched++;
                        if (protectedRestores > 0)
                        {
                            Console.WriteLine("  Restored protected glyph cells: {0} ({1})", protectedRestores, pair.Key);
                        }

                        Console.WriteLine(
                            ShouldPreserveCleanGlobalLobbyPayload(pair.Key)
                                ? "  Patched clean-lobby Hangul texture cells: {0} ({1})"
                                : "  Patched repaired global font texture cells: {0} ({1})",
                            pair.Value.Count,
                            pair.Key);
                        pair.Value.Clear();
                    }
                }
            }
        }

        private void WriteSupplementalLobbyFontFiles(
            SqPackArchive globalArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            TargetedGlyphRepairContext dialogueGlyphRepair,
            FontGlyphRepairContext glyphRepair,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            LobbyHangulGlyphAllocationCache lobbyHangulAllocationCache,
            uint[] actionDetailHighScaleHangulCodepoints,
            LobbyHangulCodepointSets lobbyHangulCodepoints)
        {
            for (int i = 0; i < Derived4kLobbyFonts.Length; i++)
            {
                string path = NormalizeGamePath(Derived4kLobbyFonts[i].TargetPath);
                if (payloadsByPath.ContainsKey(path))
                {
                    continue;
                }

                uint[] requiredCodepoints = SelectLobbyHangulCodepointsForFont(
                    path,
                    lobbyHangulCodepoints);
                if (!ShouldPatchLobbyHangulFont(path, requiredCodepoints))
                {
                    continue;
                }

                if (!ShouldIncludeFontPath(path))
                {
                    _report.FontFilesSkippedByProfile++;
                    continue;
                }

                if (!mutableIndex.ContainsPath(path))
                {
                    AddLimitedWarning("Missing global supplemental lobby font target: " + path);
                    continue;
                }

                if (!mutableIndex2.ContainsPath(path))
                {
                    AddLimitedWarning("Missing global supplemental lobby font index2 target: " + path);
                    continue;
                }

                byte[] packedFile;
                if (!globalArchive.TryReadPackedFile(path, out packedFile))
                {
                    AddLimitedWarning("Clean supplemental lobby font source was not found: " + path);
                    continue;
                }

                int normalized;
                long datOffset = WriteFontPayload(
                    datWriter,
                    path,
                    packedFile,
                    mpdStream,
                    payloadsByPath,
                    dialogueGlyphRepair,
                    glyphRepair,
                    globalArchive,
                    texturePatches,
                    lobbyHangulAllocationCache,
                    actionDetailHighScaleHangulCodepoints,
                    lobbyHangulCodepoints,
                    out normalized);

                LogFontPayloadAdjustments(path, normalized);
                mutableIndex.SetFileOffset(path, 1, datOffset);
                mutableIndex2.SetFileOffset(path, 1, datOffset);
                _report.FontFilesPatched++;

                Console.WriteLine("  Patched supplemental clean-lobby font: {0}", path);
            }
        }

        private static byte[] RewriteFdtGlyphTable(byte[] fdt, int fontTableOffset, int glyphStart, int glyphEnd, List<byte[]> entries)
        {
            entries.Sort(delegate(byte[] left, byte[] right)
            {
                return Endian.ReadUInt32LE(left, 0).CompareTo(Endian.ReadUInt32LE(right, 0));
            });

            int kerningHeaderOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x0C));
            int newGlyphBytes = checked(entries.Count * FdtGlyphEntrySize);
            int oldGlyphBytes = glyphEnd - glyphStart;
            int delta = newGlyphBytes - oldGlyphBytes;
            byte[] rewritten = new byte[checked(fdt.Length + delta)];
            Buffer.BlockCopy(fdt, 0, rewritten, 0, glyphStart);
            for (int i = 0; i < entries.Count; i++)
            {
                Buffer.BlockCopy(entries[i], 0, rewritten, glyphStart + i * FdtGlyphEntrySize, FdtGlyphEntrySize);
            }

            Buffer.BlockCopy(fdt, glyphEnd, rewritten, glyphStart + newGlyphBytes, fdt.Length - glyphEnd);
            Endian.WriteUInt32LE(rewritten, fontTableOffset + 0x04, checked((uint)entries.Count));
            if (kerningHeaderOffset >= glyphEnd)
            {
                Endian.WriteUInt32LE(rewritten, 0x0C, checked((uint)(kerningHeaderOffset + delta)));
            }

            return rewritten;
        }

        private static bool TryAllocateCleanAsciiGlyphCell(
            FontGlyphRepairContext glyphRepair,
            string fdtPath,
            string preferredTexturePath,
            string sourceTexturePath,
            int width,
            int height,
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            string normalizedFdtPath = NormalizeGamePath(fdtPath);
            if (normalizedFdtPath.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[] { preferredTexturePath, sourceTexturePath, FontKrnTexturePath },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            if (normalizedFdtPath.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string sourceCandidate = IsLobbyFontTexturePath(sourceTexturePath) ? sourceTexturePath : null;
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[]
                    {
                        preferredTexturePath,
                        sourceCandidate,
                        FontLobby1TexturePath,
                        FontLobby2TexturePath,
                        FontLobby3TexturePath,
                        FontLobby4TexturePath,
                        FontLobby5TexturePath,
                        FontLobby6TexturePath,
                        FontLobby7TexturePath
                    },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            return TryAllocateFromCandidateTextures(
                glyphRepair,
                new string[]
                {
                    preferredTexturePath,
                    sourceTexturePath,
                    Font1TexturePath,
                    Font2TexturePath,
                    Font3TexturePath,
                    Font4TexturePath,
                    Font5TexturePath,
                    Font6TexturePath,
                    Font7TexturePath
                },
                width,
                height,
                out allocatedTexturePath,
                out allocatedCell);
        }

        private static bool TryAllocateRelocatedHangulGlyphCell(
            FontGlyphRepairContext glyphRepair,
            string fdtPath,
            string skippedTexturePath,
            int width,
            int height,
            bool useAllGlobalTextureTargets,
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            string normalizedFdtPath = NormalizeGamePath(fdtPath);
            if (useAllGlobalTextureTargets)
            {
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[]
                    {
                        Font1TexturePath,
                        Font2TexturePath,
                        Font3TexturePath,
                        Font4TexturePath,
                        Font5TexturePath,
                        Font6TexturePath,
                        Font7TexturePath
                    },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            if (normalizedFdtPath.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[] { FontKrnTexturePath },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            if (normalizedFdtPath.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[]
                    {
                        FontLobby1TexturePath,
                        FontLobby2TexturePath,
                        FontLobby3TexturePath,
                        FontLobby4TexturePath,
                        FontLobby5TexturePath,
                        FontLobby6TexturePath,
                        FontLobby7TexturePath
                    },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            return TryAllocateFromCandidateTextures(
                glyphRepair,
                new string[] { Font1TexturePath, Font2TexturePath, skippedTexturePath },
                width,
                height,
                out allocatedTexturePath,
                out allocatedCell);
        }

        private static bool TryAllocateFromCandidateTextures(
            FontGlyphRepairContext glyphRepair,
            string[] candidates,
            int width,
            int height,
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            allocatedTexturePath = null;
            allocatedCell = new AllocatedFontGlyphCell();
            if (glyphRepair == null || candidates == null)
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate))
                {
                    continue;
                }

                if (glyphRepair.TryAllocate(candidate, width, height, out allocatedCell))
                {
                    allocatedTexturePath = NormalizeGamePath(candidate);
                    return true;
                }
            }

            return false;
        }

        private static bool PatchFitsAllocatedFontTexture(
            FontGlyphRepairContext glyphRepair,
            string texturePath,
            int x,
            int y,
            int width,
            int height)
        {
            if (glyphRepair == null || string.IsNullOrEmpty(texturePath))
            {
                return false;
            }

            FontAtlasAllocator allocator;
            if (!glyphRepair.TryGetAllocator(texturePath, out allocator))
            {
                return false;
            }

            return x >= 0 &&
                   y >= 0 &&
                   width >= 0 &&
                   height >= 0 &&
                   x + width <= allocator.Width &&
                   y + height <= allocator.Height;
        }

        private static bool TryAllocateProtectedPuaGlyphCell(
            FontGlyphRepairContext glyphRepair,
            string fdtPath,
            string preferredTexturePath,
            int width,
            int height,
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            allocatedTexturePath = null;
            allocatedCell = new AllocatedFontGlyphCell();
            if (glyphRepair == null)
            {
                return false;
            }

            string normalizedFdtPath = NormalizeGamePath(fdtPath);
            string[] candidates;
            if (normalizedFdtPath.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidates = new string[]
                {
                    preferredTexturePath,
                    FontKrnTexturePath
                };
            }
            else
            {
                candidates = new string[]
                {
                    preferredTexturePath,
                    Font1TexturePath,
                    Font2TexturePath,
                    Font3TexturePath,
                    Font4TexturePath,
                    Font5TexturePath,
                    Font6TexturePath,
                    Font7TexturePath
                };
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate))
                {
                    continue;
                }

                if (glyphRepair.TryAllocate(candidate, width, height, out allocatedCell))
                {
                    allocatedTexturePath = candidate;
                    return true;
                }
            }

            return false;
        }

        private long WriteFontPayload(
            SqPackDatWriter datWriter,
            string path,
            byte[] packedFile,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            TargetedGlyphRepairContext dialogueGlyphRepair,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            LobbyHangulGlyphAllocationCache lobbyHangulAllocationCache,
            uint[] actionDetailHighScaleHangulCodepoints,
            LobbyHangulCodepointSets lobbyHangulCodepoints,
            out int normalized)
        {
            normalized = 0;
            if (!path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
            {
                return datWriter.WritePackedFile(packedFile);
            }

            byte[] fdt = SqPackArchive.UnpackStandardFile(packedFile);
            return WritePreparedFontFdtPayload(datWriter, path, fdt, packedFile, 0, mpdStream, payloadsByPath, dialogueGlyphRepair, glyphRepair, globalArchive, texturePatches, lobbyHangulAllocationCache, actionDetailHighScaleHangulCodepoints, lobbyHangulCodepoints, out normalized);
        }

        private long WritePreparedFontFdtPayload(
            SqPackDatWriter datWriter,
            string path,
            byte[] fdt,
            byte[] originalPackedFile,
            int preChanged,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            TargetedGlyphRepairContext dialogueGlyphRepair,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            LobbyHangulGlyphAllocationCache lobbyHangulAllocationCache,
            uint[] actionDetailHighScaleHangulCodepoints,
            LobbyHangulCodepointSets lobbyHangulCodepoints,
            out int normalized)
        {
            normalized = NormalizeFdtShiftJisValues(fdt);
            int aliases = AddPartyListSelfMarkerAliases(path, ref fdt);
            int relocatedSkippedTextureHangulGlyphs = RelocateHangulGlyphsFromSkippedTextures(path, fdt, mpdStream, payloadsByPath, glyphRepair, texturePatches);
            int dialogueGlyphFixes = _options.FontOnly ? 0 : ApplyDialogueGlyphArtifactFix(path, fdt, dialogueGlyphRepair, glyphRepair, texturePatches);
            int actionDetailHighScaleFixes = 0;
            int lobbyHangulFixes = ApplyLobbyHangulAllocatedGlyphs(path, ref fdt, mpdStream, payloadsByPath, glyphRepair, globalArchive, texturePatches, lobbyHangulAllocationCache, SelectLobbyHangulCodepointsForFont(path, lobbyHangulCodepoints));
            if (EnableLegacyLobbyHangulSourceCellGrafting)
            {
                lobbyHangulFixes = ApplyLobbyHangulSourceCells(path, ref fdt, mpdStream, payloadsByPath, texturePatches, SelectLobbyHangulCodepointsForFont(path, lobbyHangulCodepoints));
            }

            actionDetailHighScaleFixes = ApplyLargeUiLabelVisualScaleGlyphs(path, ref fdt, mpdStream, payloadsByPath, glyphRepair, texturePatches, actionDetailHighScaleHangulCodepoints);
            int partyShapeFixes = ApplyPartyListSelfMarkerCleanShapes(path, ref fdt, glyphRepair, globalArchive, texturePatches);
            int cleanAsciiFixes = ApplyCleanAsciiGlyphShapes(path, fdt, glyphRepair, globalArchive, texturePatches);
            int cleanAsciiKerningFixes = ApplyCleanAsciiKerning(path, ref fdt, globalArchive);
            int startScreenKerningFixes = _options.FontOnly ? 0 : ApplyStartScreenSystemSettingsKerning(path, ref fdt);
            // FDT edits are written as standard files when key normalization,
            // targeted Hangul repair, party marker allocation, or ASCII/numeric
            // glyph repair changed the render contract for the file.
            if (preChanged == 0 &&
                normalized == 0 &&
                aliases == 0 &&
                relocatedSkippedTextureHangulGlyphs == 0 &&
                dialogueGlyphFixes == 0 &&
                actionDetailHighScaleFixes == 0 &&
                lobbyHangulFixes == 0 &&
                partyShapeFixes == 0 &&
                cleanAsciiFixes == 0 &&
                cleanAsciiKerningFixes == 0 &&
                startScreenKerningFixes == 0 &&
                originalPackedFile != null)
            {
                return datWriter.WritePackedFile(originalPackedFile);
            }

            if (aliases > 0)
            {
                Console.WriteLine("  Added party-list self marker glyph aliases: {0} ({1})", aliases, path);
            }

            if (relocatedSkippedTextureHangulGlyphs > 0)
            {
                Console.WriteLine("  Relocated Hangul glyphs from skipped texture cells: {0} ({1})", relocatedSkippedTextureHangulGlyphs, path);
            }

            if (dialogueGlyphFixes > 0)
            {
                Console.WriteLine("  Queued dialogue Hangul glyph artifact fixes: {0} ({1})", dialogueGlyphFixes, path);
            }

            if (actionDetailHighScaleFixes > 0)
            {
                Console.WriteLine("  Queued large UI high-scale Hangul glyph cells: {0} ({1})", actionDetailHighScaleFixes, path);
            }

            if (lobbyHangulFixes > 0)
            {
                Console.WriteLine("  Queued lobby Hangul allocated glyph cells: {0} ({1})", lobbyHangulFixes, path);
            }

            if (partyShapeFixes > 0)
            {
                Console.WriteLine("  Queued party-list protected PUA clean glyph cells: {0} ({1})", partyShapeFixes, path);
            }

            if (cleanAsciiFixes > 0)
            {
                Console.WriteLine("  Queued clean ASCII/numeric glyph cells: {0} ({1})", cleanAsciiFixes, path);
            }

            if (cleanAsciiKerningFixes > 0)
            {
                Console.WriteLine("  Restored clean ASCII kerning pairs: {0} ({1})", cleanAsciiKerningFixes, path);
            }

            if (startScreenKerningFixes > 0)
            {
                Console.WriteLine("  Added start-screen system-settings kerning pairs: {0} ({1})", startScreenKerningFixes, path);
            }

            return datWriter.WriteStandardFile(fdt);
        }

        private static void LogFontPayloadAdjustments(string path, int normalized)
        {
            if (normalized > 0)
            {
                Console.WriteLine("  Normalized FDT Shift-JIS glyph keys: {0} ({1})", normalized, path);
            }
        }

        private static int NormalizeFdtShiftJisValues(byte[] fdt)
        {
            if (fdt == null || fdt.Length < FdtHeaderSize)
            {
                return 0;
            }

            if (!HasAsciiSignature(fdt, 0, "fcsv0100"))
            {
                return 0;
            }

            return NormalizeFdtGlyphShiftJisValues(fdt) + NormalizeFdtKerningShiftJisValues(fdt);
        }

        private static int AddPartyListSelfMarkerAliases(string path, ref byte[] fdt)
        {
            // Party-list self marker text uses Addon 10952 (U+E031) and some HUD
            // routes/settings draw U+E0E1..E0E8 directly. Add missing rows from
            // the same FDT's legacy marker range only as a temporary fallback;
            // existing rows must not be overwritten before clean-global pixels
            // are copied into safe cells.
            if (!ShouldPatchPartyListSelfMarkerFont(path))
            {
                return 0;
            }

            if (fdt == null ||
                fdt.Length < FdtHeaderSize ||
                !HasAsciiSignature(fdt, 0, "fcsv0100"))
            {
                return 0;
            }

            int fontTableOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x08));
            int kerningHeaderOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x0C));
            if (fontTableOffset < FdtHeaderSize ||
                fontTableOffset > fdt.Length - FdtFontTableHeaderSize ||
                !HasAsciiSignature(fdt, fontTableOffset, "fthd"))
            {
                return 0;
            }

            uint glyphCount = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x04);
            int glyphStart = fontTableOffset + FdtFontTableHeaderSize;
            long glyphBytes = (long)glyphCount * FdtGlyphEntrySize;
            if (glyphBytes < 0 || glyphStart > fdt.Length || glyphStart + glyphBytes > fdt.Length)
            {
                return 0;
            }

            int glyphEnd = checked(glyphStart + (int)glyphBytes);
            Dictionary<uint, byte[]> entriesByCodepoint = new Dictionary<uint, byte[]>();
            List<byte[]> entries = new List<byte[]>((int)glyphCount + 16);
            for (int i = 0; i < glyphCount; i++)
            {
                byte[] entry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(fdt, glyphStart + i * FdtGlyphEntrySize, entry, 0, FdtGlyphEntrySize);
                uint codepoint = Endian.ReadUInt32LE(entry, 0);
                entries.Add(entry);
                if (!entriesByCodepoint.ContainsKey(codepoint))
                {
                    entriesByCodepoint.Add(codepoint, entry);
                }
            }

            int added = 0;
            for (int i = 0; i < PartyListSelfMarkerPrimaryStarts.Length; i++)
            {
                added += AddPartyListSelfMarkerAliasRange(
                    entries,
                    entriesByCodepoint,
                    PartyListSelfMarkerPrimaryStarts[i],
                    PartyListSelfMarkerLegacyStart,
                    PartyListSelfMarkerPrimaryCounts[i]);
            }
            if (added == 0)
            {
                return 0;
            }

            entries.Sort(delegate(byte[] left, byte[] right)
            {
                return Endian.ReadUInt32LE(left, 0).CompareTo(Endian.ReadUInt32LE(right, 0));
            });

            int newGlyphBytes = checked(entries.Count * FdtGlyphEntrySize);
            int delta = newGlyphBytes - (int)glyphBytes;
            byte[] rewritten = new byte[checked(fdt.Length + delta)];
            Buffer.BlockCopy(fdt, 0, rewritten, 0, glyphStart);
            for (int i = 0; i < entries.Count; i++)
            {
                Buffer.BlockCopy(entries[i], 0, rewritten, glyphStart + i * FdtGlyphEntrySize, FdtGlyphEntrySize);
            }

            Buffer.BlockCopy(fdt, glyphEnd, rewritten, glyphStart + newGlyphBytes, fdt.Length - glyphEnd);
            Endian.WriteUInt32LE(rewritten, fontTableOffset + 0x04, checked((uint)entries.Count));
            if (kerningHeaderOffset >= glyphEnd)
            {
                Endian.WriteUInt32LE(rewritten, 0x0C, checked((uint)(kerningHeaderOffset + delta)));
            }

            fdt = rewritten;
            return added;
        }

        private static bool ShouldPatchPartyListSelfMarkerFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, "common/font/AXIS_12.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_14.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_36.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_120.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_140.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_180.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_360.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/MiedingerMid_12.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldPatchPartyListSelfMarkerCleanShapeFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, "common/font/AXIS_12.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_14.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_36.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_120.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_140.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_180.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_360.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/MiedingerMid_12.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<uint, byte[]> ReadGlyphEntriesByUtf8Value(byte[] fdt)
        {
            Dictionary<uint, byte[]> entries = new Dictionary<uint, byte[]>();
            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return entries;
            }

            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                uint utf8Value = Endian.ReadUInt32LE(fdt, offset);
                if (entries.ContainsKey(utf8Value))
                {
                    continue;
                }

                byte[] entry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(fdt, offset, entry, 0, FdtGlyphEntrySize);
                entries.Add(utf8Value, entry);
            }

            return entries;
        }

        private int RelocateHangulGlyphsFromSkippedTextures(
            string path,
            byte[] targetFdt,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            FontGlyphRepairContext glyphRepair,
            Dictionary<string, List<FontTexturePatch>> texturePatches)
        {
            if (targetFdt == null ||
                mpdStream == null ||
                payloadsByPath == null ||
                glyphRepair == null ||
                texturePatches == null)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            int changed = 0;
            int allocationFailures = 0;
            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                uint utf8Value = Endian.ReadUInt32LE(targetFdt, offset);
                uint codepoint;
                if (!TryDecodeFdtUtf8Value(utf8Value, out codepoint) ||
                    codepoint < HangulFirst ||
                    codepoint > HangulLast)
                {
                    continue;
                }

                FdtGlyphEntry glyph = ReadFdtGlyphEntry(targetFdt, offset);
                if (glyph.Width == 0 || glyph.Height == 0)
                {
                    continue;
                }

                string sourceTexturePath = ResolveFontTexturePath(normalizedPath, glyph.ImageIndex);
                if (sourceTexturePath == null)
                {
                    continue;
                }

                bool forceCleanAtlasRelocation = ShouldRelocateFontOnlyAxisHangulGlyph(normalizedPath, sourceTexturePath);
                if (!forceCleanAtlasRelocation && ShouldIncludeFontPath(sourceTexturePath))
                {
                    continue;
                }

                byte[] sourceTexture;
                if (!sourceTextures.TryGetValue(sourceTexturePath, out sourceTexture))
                {
                    sourceTexture = TryLoadTtmpTexturePayload(payloadsByPath, mpdStream, sourceTexturePath);
                    if (sourceTexture == null)
                    {
                        continue;
                    }

                    sourceTextures.Add(sourceTexturePath, sourceTexture);
                }

                byte[] sourceAlpha;
                try
                {
                    sourceAlpha = ExtractFontTextureAlpha(sourceTexture, glyph);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    continue;
                }

                AllocatedFontGlyphCell allocatedCell;
                string allocatedTexturePath;
                if (!TryAllocateRelocatedHangulGlyphCell(
                    glyphRepair,
                    normalizedPath,
                    sourceTexturePath,
                    glyph.Width,
                    glyph.Height,
                    forceCleanAtlasRelocation,
                    out allocatedTexturePath,
                    out allocatedCell))
                {
                    allocationFailures++;
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = allocatedCell.X;
                patch.TargetY = allocatedCell.Y;
                patch.TargetChannel = allocatedCell.Channel;
                patch.ClearWidth = glyph.Width;
                patch.ClearHeight = glyph.Height;
                patch.SourceWidth = glyph.Width;
                patch.SourceHeight = glyph.Height;
                patch.SourceAlpha = sourceAlpha;
                patch.SourceFdtPath = normalizedPath;
                patch.SourceCodepoint = codepoint;
                AddTexturePatch(texturePatches, allocatedTexturePath, patch);

                Endian.WriteUInt16LE(targetFdt, offset + 6, checked((ushort)allocatedCell.ImageIndex));
                Endian.WriteUInt16LE(targetFdt, offset + 8, checked((ushort)allocatedCell.X));
                Endian.WriteUInt16LE(targetFdt, offset + 10, checked((ushort)allocatedCell.Y));
                changed++;
            }

            if (allocationFailures > 0 && IsFontOnlyGameAxisFontPath(normalizedPath))
            {
                AddLimitedWarning("Font-only AXIS Hangul clean-atlas allocation failures for " + normalizedPath + ": " + allocationFailures.ToString());
            }

            return changed;
        }

        private int ApplyDialogueGlyphArtifactFix(
            string path,
            byte[] targetFdt,
            TargetedGlyphRepairContext repair,
            FontGlyphRepairContext glyphRepair,
            Dictionary<string, List<FontTexturePatch>> texturePatches)
        {
            if (!ShouldPatchDialogueGlyphArtifactFont(path) ||
                repair == null ||
                glyphRepair == null ||
                texturePatches == null ||
                targetFdt == null)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            TargetedGlyphSource source;
            if (!repair.TryGet(DialogueByeonCodepoint, out source))
            {
                return 0;
            }

            uint utf8Value = PackFdtUtf8Value(DialogueByeonCodepoint);
            int targetOffset;
            if (!TryFindGlyphEntryOffset(targetFdt, glyphStart, glyphCount, utf8Value, out targetOffset))
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            FdtGlyphEntry targetEntry = ReadFdtGlyphEntry(targetFdt, targetOffset);
            string targetTexturePath = ResolveFontTexturePath(normalizedPath, targetEntry.ImageIndex);
            if (targetTexturePath == null || !ShouldIncludeFontPath(targetTexturePath))
            {
                return 0;
            }

            AllocatedFontGlyphCell allocatedCell;
            if (!glyphRepair.TryAllocate(targetTexturePath, source.Width, source.Height, out allocatedCell))
            {
                return 0;
            }

            FontTexturePatch patch = new FontTexturePatch();
            patch.TargetX = allocatedCell.X;
            patch.TargetY = allocatedCell.Y;
            patch.TargetChannel = allocatedCell.Channel;
            patch.ClearWidth = source.Width;
            patch.ClearHeight = source.Height;
            patch.SourceWidth = source.Width;
            patch.SourceHeight = source.Height;
            patch.SourceAlpha = source.Alpha;
            patch.SourceFdtPath = source.SourceFdtPath;
            patch.SourceCodepoint = source.Codepoint;
            AddTexturePatch(texturePatches, targetTexturePath, patch);

            Endian.WriteUInt16LE(targetFdt, targetOffset + 6, checked((ushort)allocatedCell.ImageIndex));
            Endian.WriteUInt16LE(targetFdt, targetOffset + 8, checked((ushort)allocatedCell.X));
            Endian.WriteUInt16LE(targetFdt, targetOffset + 10, checked((ushort)allocatedCell.Y));
            targetFdt[targetOffset + 12] = checked((byte)source.Width);
            targetFdt[targetOffset + 13] = checked((byte)source.Height);
            targetFdt[targetOffset + 14] = unchecked((byte)source.OffsetX);
            targetFdt[targetOffset + 15] = unchecked((byte)source.OffsetY);
            return 1;
        }

        private int ApplyLargeUiLabelVisualScaleGlyphs(
            string path,
            ref byte[] targetFdt,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            FontGlyphRepairContext glyphRepair,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            uint[] requiredCodepoints)
        {
            LargeUiLabelVisualScaleSpec spec;
            if (!TryGetLargeUiLabelVisualScaleSpec(path, out spec) ||
                targetFdt == null ||
                mpdStream == null ||
                payloadsByPath == null ||
                glyphRepair == null ||
                texturePatches == null ||
                requiredCodepoints == null ||
                requiredCodepoints.Length == 0)
            {
                return 0;
            }

            byte[] sourceFdt = TryLoadTtmpStandardPayload(payloadsByPath, mpdStream, spec.SourceFontPath);
            if (sourceFdt == null)
            {
                AddLimitedWarning("Large UI label Hangul source font missing: " + spec.SourceFontPath + " -> " + spec.TargetFontPath);
                return 0;
            }

            byte[] metricFdt = TryLoadTtmpStandardPayload(payloadsByPath, mpdStream, spec.MetricFontPath);
            if (metricFdt == null)
            {
                AddLimitedWarning("Large UI label Hangul metric font missing: " + spec.MetricFontPath + " -> " + spec.TargetFontPath);
                return 0;
            }

            Dictionary<uint, byte[]> sourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            Dictionary<uint, byte[]> metricEntries = ReadGlyphEntriesByUtf8Value(metricFdt);
            if (sourceEntries.Count == 0 || metricEntries.Count == 0)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            if (normalizedPath.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0;
            }

            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            double targetDigitHeight = MeasureMeanVisibleHeightFromTtmpPayloads(
                metricFdt,
                spec.MetricFontPath,
                ActionDetailNumericCodepoints,
                payloadsByPath,
                mpdStream);
            double sourceHangulHeight = MeasureMeanVisibleHeightFromTtmpPayloads(
                sourceFdt,
                spec.SourceFontPath,
                requiredCodepoints,
                payloadsByPath,
                mpdStream);
            double verticalScale = targetDigitHeight > 0d && sourceHangulHeight > 0d
                ? (targetDigitHeight * spec.HangulToDigitRatio) / sourceHangulHeight
                : 0d;
            int changed = 0;
            int allocationFailures = 0;
            for (int codepointIndex = 0; codepointIndex < requiredCodepoints.Length; codepointIndex++)
            {
                uint codepoint = requiredCodepoints[codepointIndex];
                if (!IsHangulCodepoint(codepoint))
                {
                    continue;
                }

                uint utf8Value = PackFdtUtf8Value(codepoint);
                int targetOffset;
                if (!TryFindGlyphEntryOffset(targetFdt, glyphStart, glyphCount, utf8Value, out targetOffset))
                {
                    continue;
                }

                byte[] sourceEntryBytes;
                if (!sourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                {
                    continue;
                }

                byte[] metricEntryBytes;
                if (!metricEntries.TryGetValue(utf8Value, out metricEntryBytes))
                {
                    continue;
                }

                FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
                FdtGlyphEntry metricEntry = ReadFdtGlyphEntry(metricEntryBytes, 0);
                if (sourceEntry.Width == 0 ||
                    sourceEntry.Height == 0 ||
                    metricEntry.Width == 0 ||
                    metricEntry.Height == 0)
                {
                    continue;
                }

                string sourceTexturePath = ResolveFontTexturePath(spec.SourceFontPath, sourceEntry.ImageIndex);
                string targetTexturePath = ResolveFontTexturePath(normalizedPath, metricEntry.ImageIndex);
                if (sourceTexturePath == null || targetTexturePath == null)
                {
                    continue;
                }

                byte[] sourceTexture;
                if (!sourceTextures.TryGetValue(sourceTexturePath, out sourceTexture))
                {
                    sourceTexture = TryLoadTtmpTexturePayload(payloadsByPath, mpdStream, sourceTexturePath);
                    if (sourceTexture == null)
                    {
                        continue;
                    }

                    sourceTextures.Add(sourceTexturePath, sourceTexture);
                }

                byte[] sourceAlpha;
                try
                {
                    sourceAlpha = ExtractFontTextureAlpha(sourceTexture, sourceEntry);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    continue;
                }

                int scaledWidth = sourceEntry.Width;
                int scaledHeight = sourceEntry.Height;
                if (verticalScale > 0d)
                {
                    scaledWidth = ClampInt(
                        (int)Math.Round(sourceEntry.Width * verticalScale),
                        1,
                        byte.MaxValue);
                    scaledHeight = ClampInt(
                        (int)Math.Round(sourceEntry.Height * verticalScale),
                        1,
                        metricEntry.Height);
                }

                byte[] scaledAlpha = ScaleGlyphAlphaBilinear(
                    sourceAlpha,
                    sourceEntry.Width,
                    sourceEntry.Height,
                    scaledWidth,
                    scaledHeight);

                int sourceAdvance = Math.Max(1, sourceEntry.Width + sourceEntry.OffsetX);
                int scaledAdvance = verticalScale > 0d
                    ? ClampInt((int)Math.Round(sourceAdvance * verticalScale), 1, byte.MaxValue)
                    : sourceAdvance;
                int scaledOffsetX = ClampInt(scaledAdvance - scaledWidth, sbyte.MinValue, sbyte.MaxValue);

                AllocatedFontGlyphCell allocatedCell;
                string allocatedTexturePath;
                int paddedWidth = checked(scaledWidth + ActionDetailHighScaleGlyphTexturePadding * 2);
                int paddedHeight = checked(scaledHeight + ActionDetailHighScaleGlyphTexturePadding * 2);
                if (!TryAllocateActionDetailHighScaleGlyphCell(
                    glyphRepair,
                    targetTexturePath,
                    paddedWidth,
                    paddedHeight,
                    out allocatedTexturePath,
                    out allocatedCell))
                {
                    allocationFailures++;
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = allocatedCell.X;
                patch.TargetY = allocatedCell.Y;
                patch.TargetChannel = allocatedCell.Channel;
                patch.ClearWidth = paddedWidth;
                patch.ClearHeight = paddedHeight;
                patch.SourceWidth = paddedWidth;
                patch.SourceHeight = paddedHeight;
                patch.SourceAlpha = PadGlyphAlpha(
                    scaledAlpha,
                    scaledWidth,
                    scaledHeight,
                    ActionDetailHighScaleGlyphTexturePadding);
                patch.SourceFdtPath = spec.SourceFontPath;
                patch.SourceCodepoint = codepoint;
                AddTexturePatch(texturePatches, allocatedTexturePath, patch);

                Buffer.BlockCopy(metricEntryBytes, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                Endian.WriteUInt16LE(targetFdt, targetOffset + 6, checked((ushort)allocatedCell.ImageIndex));
                Endian.WriteUInt16LE(targetFdt, targetOffset + 8, checked((ushort)(allocatedCell.X + ActionDetailHighScaleGlyphTexturePadding)));
                Endian.WriteUInt16LE(targetFdt, targetOffset + 10, checked((ushort)(allocatedCell.Y + ActionDetailHighScaleGlyphTexturePadding)));
                targetFdt[targetOffset + 12] = checked((byte)scaledWidth);
                targetFdt[targetOffset + 13] = checked((byte)scaledHeight);
                targetFdt[targetOffset + 14] = unchecked((byte)(sbyte)scaledOffsetX);
                targetFdt[targetOffset + 15] = unchecked((byte)sourceEntry.OffsetY);
                changed++;
            }

            if (allocationFailures > 0)
            {
                AddLimitedWarning("Large UI label Hangul atlas allocation failures for " + normalizedPath + ": " + allocationFailures.ToString());
            }

            return changed;
        }

        private static bool TryGetLargeUiLabelVisualScaleSpec(string path, out LargeUiLabelVisualScaleSpec spec)
        {
            string normalized = NormalizeGamePath(path);
            for (int i = 0; i < LargeUiLabelVisualScaleFonts.Length; i++)
            {
                if (string.Equals(normalized, LargeUiLabelVisualScaleFonts[i].TargetFontPath, StringComparison.OrdinalIgnoreCase))
                {
                    spec = LargeUiLabelVisualScaleFonts[i];
                    return true;
                }
            }

            spec = null;
            return false;
        }

        private static bool TryGetLobbyLargeLabelVisualScaleSpec(string path, out LobbyLargeLabelVisualScaleSpec spec)
        {
            string normalized = NormalizeGamePath(path);
            for (int i = 0; i < LobbyLargeLabelVisualScaleFonts.Length; i++)
            {
                if (string.Equals(normalized, LobbyLargeLabelVisualScaleFonts[i].TargetFontPath, StringComparison.OrdinalIgnoreCase))
                {
                    spec = LobbyLargeLabelVisualScaleFonts[i];
                    return true;
                }
            }

            spec = null;
            return false;
        }

        private int ApplyLobbyHangulAllocatedGlyphs(
            string path,
            ref byte[] targetFdt,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            LobbyHangulGlyphAllocationCache lobbyHangulAllocationCache,
            uint[] requiredCodepoints)
        {
            if (!ShouldPatchLobbyHangulFont(path, requiredCodepoints) ||
                targetFdt == null ||
                mpdStream == null ||
                payloadsByPath == null ||
                glyphRepair == null ||
                texturePatches == null)
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            LobbyLargeLabelVisualScaleSpec visualScaleSpec;
            bool hasVisualScaleSpec = TryGetLobbyLargeLabelVisualScaleSpec(normalizedPath, out visualScaleSpec);
            string sourceFdtPath = hasVisualScaleSpec
                ? visualScaleSpec.SourceFontPath
                : ResolveLobbyHangulSourceFdtPath(normalizedPath);

            byte[] sourceFdt = TryLoadTtmpStandardPayload(payloadsByPath, mpdStream, sourceFdtPath);
            if (sourceFdt == null)
            {
                AddLimitedWarning("Lobby Hangul source font missing: " + sourceFdtPath + " -> " + normalizedPath);
                return 0;
            }

            Dictionary<uint, byte[]> sourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            if (sourceEntries.Count == 0)
            {
                return 0;
            }

            string placementFdtPath = null;
            Dictionary<uint, byte[]> placementEntries = null;
            if (hasVisualScaleSpec && visualScaleSpec.UsePlacementCells)
            {
                placementFdtPath = visualScaleSpec.PlacementFontPath;
                byte[] placementFdt = TryLoadTtmpStandardPayload(payloadsByPath, mpdStream, placementFdtPath);
                if (placementFdt == null)
                {
                    AddLimitedWarning("Lobby visual placement font missing: " + placementFdtPath + " -> " + normalizedPath);
                    return 0;
                }

                placementEntries = ReadGlyphEntriesByUtf8Value(placementFdt);
                if (placementEntries.Count == 0)
                {
                    return 0;
                }
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            int glyphEnd = checked(glyphStart + checked((int)glyphCount) * FdtGlyphEntrySize);
            List<byte[]> targetEntries = new List<byte[]>(checked((int)glyphCount + requiredCodepoints.Length));
            Dictionary<uint, int> targetEntryIndexes = new Dictionary<uint, int>();
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                byte[] entry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(targetFdt, glyphStart + glyphIndex * FdtGlyphEntrySize, entry, 0, FdtGlyphEntrySize);
                uint utf8Value = Endian.ReadUInt32LE(entry, 0);
                if (!targetEntryIndexes.ContainsKey(utf8Value))
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                }

                targetEntries.Add(entry);
            }

            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, byte[]> targetTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            double visualScale = 0d;
            if (hasVisualScaleSpec)
            {
                double targetDigitHeight = MeasureMeanVisibleHeightFromArchive(
                    targetFdt,
                    normalizedPath,
                    ActionDetailNumericCodepoints,
                    globalArchive,
                    targetTextures);
                double sourceHangulHeight = MeasureMeanVisibleHeightFromTtmpPayloads(
                    sourceFdt,
                    sourceFdtPath,
                    requiredCodepoints,
                    payloadsByPath,
                    mpdStream);
                if (targetDigitHeight > 0d && sourceHangulHeight > 0d)
                {
                    visualScale = (targetDigitHeight * visualScaleSpec.HangulToDigitRatio) / sourceHangulHeight;
                }
            }

            int changed = 0;
            int missingSources = 0;
            int textureFailures = 0;
            int allocationFailures = 0;
            for (int codepointIndex = 0; codepointIndex < requiredCodepoints.Length; codepointIndex++)
            {
                uint codepoint = requiredCodepoints[codepointIndex];
                if (!IsHangulCodepoint(codepoint))
                {
                    continue;
                }

                uint utf8Value = PackFdtUtf8Value(codepoint);
                byte[] sourceEntryBytes;
                if (!sourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                {
                    missingSources++;
                    continue;
                }

                byte[] placementEntryBytes = null;
                if (placementEntries != null &&
                    !placementEntries.TryGetValue(utf8Value, out placementEntryBytes))
                {
                    missingSources++;
                    continue;
                }

                FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
                if (sourceEntry.Width == 0 || sourceEntry.Height == 0)
                {
                    continue;
                }

                FdtGlyphEntry placementEntry = placementEntryBytes == null
                    ? sourceEntry
                    : ReadFdtGlyphEntry(placementEntryBytes, 0);

                string sourceTexturePath = ResolveFontTexturePath(sourceFdtPath, sourceEntry.ImageIndex);
                if (!ShouldIncludeFontPath(sourceTexturePath))
                {
                    textureFailures++;
                    continue;
                }

                byte[] sourceTexture;
                if (!sourceTextures.TryGetValue(sourceTexturePath, out sourceTexture))
                {
                    sourceTexture = TryLoadTtmpTexturePayload(payloadsByPath, mpdStream, sourceTexturePath);
                    if (sourceTexture == null)
                    {
                        textureFailures++;
                        continue;
                    }

                    sourceTextures.Add(sourceTexturePath, sourceTexture);
                }

                CleanAsciiTextureRegion sourceRegion;
                try
                {
                    sourceRegion = ExtractFontTextureAlphaRegion(sourceTexture, sourceEntry, LobbyGlyphTextureNeighborhoodPadding);
                }
                catch (ArgumentOutOfRangeException)
                {
                    textureFailures++;
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    textureFailures++;
                    continue;
                }

                if (!sourceRegion.IsValid)
                {
                    textureFailures++;
                    continue;
                }

                int patchClearWidth = sourceRegion.Width;
                int patchClearHeight = sourceRegion.Height;
                byte[] patchAlpha = sourceRegion.Alpha;
                int replacementWidth = sourceEntry.Width;
                int replacementHeight = sourceEntry.Height;
                int replacementOffsetX = sourceEntry.OffsetX;
                int replacementOffsetY = sourceEntry.OffsetY;
                if (hasVisualScaleSpec && visualScale > 0d)
                {
                    replacementWidth = ClampInt(
                        (int)Math.Round(sourceEntry.Width * visualScale),
                        1,
                        byte.MaxValue);
                    replacementHeight = ClampInt(
                        (int)Math.Round(sourceEntry.Height * visualScale),
                        1,
                        byte.MaxValue);
                    int sourceAdvance = Math.Max(1, sourceEntry.Width + sourceEntry.OffsetX);
                    int scaledAdvance = ClampInt(
                        (int)Math.Round(sourceAdvance * visualScale),
                        1,
                        byte.MaxValue);
                    replacementOffsetX = ClampInt(scaledAdvance - replacementWidth, sbyte.MinValue, sbyte.MaxValue);
                    patchAlpha = ScaleGlyphAlphaBilinear(
                        sourceRegion.Alpha,
                        sourceRegion.Width,
                        sourceRegion.Height,
                        replacementWidth,
                        replacementHeight);
                    patchClearWidth = replacementWidth;
                    patchClearHeight = replacementHeight;
                }

                LobbyHangulGlyphAllocation allocation = new LobbyHangulGlyphAllocation();
                bool hasAllocation = false;
                bool addTexturePatch = false;
                string allocationKey = CreateLobbyHangulAllocationKey(sourceFdtPath, codepoint, sourceEntry, sourceRegion);
                if (hasVisualScaleSpec)
                {
                    allocationKey = normalizedPath + "|visual-scale|" +
                        replacementWidth.ToString() + "x" + replacementHeight.ToString() + "|" +
                        replacementOffsetX.ToString() + "|" +
                        allocationKey;
                }

                if (hasVisualScaleSpec && visualScaleSpec.UsePlacementCells)
                {
                    string placementTexturePath = ResolveFontTexturePath(placementFdtPath, placementEntry.ImageIndex);
                    if (!IsLobbyFontTexturePath(placementTexturePath) ||
                        !ShouldIncludeFontPath(placementTexturePath))
                    {
                        textureFailures++;
                        continue;
                    }

                    AllocatedFontGlyphCell placementCell = new AllocatedFontGlyphCell();
                    placementCell.ImageIndex = placementEntry.ImageIndex;
                    placementCell.X = placementEntry.X;
                    placementCell.Y = placementEntry.Y;
                    placementCell.Channel = placementEntry.ImageIndex % 4;
                    allocation = new LobbyHangulGlyphAllocation(
                        placementTexturePath,
                        placementCell,
                        0,
                        0);
                    patchClearWidth = Math.Max(patchClearWidth, placementEntry.Width);
                    patchClearHeight = Math.Max(patchClearHeight, placementEntry.Height);
                    hasAllocation = true;
                    addTexturePatch = true;
                }
                else if (lobbyHangulAllocationCache != null &&
                    lobbyHangulAllocationCache.TryGet(allocationKey, out allocation))
                {
                    hasAllocation = true;
                }

                if (!hasAllocation)
                {
                    AllocatedFontGlyphCell allocatedCell;
                    string allocatedTexturePath;
                    if (!TryAllocateLobbyHangulGlyphCell(
                        glyphRepair,
                        normalizedPath,
                        patchClearWidth,
                        patchClearHeight,
                        out allocatedTexturePath,
                        out allocatedCell))
                    {
                        allocationFailures++;
                        continue;
                    }

                    allocation = new LobbyHangulGlyphAllocation(
                        allocatedTexturePath,
                        allocatedCell,
                        sourceRegion.LeftPadding,
                        sourceRegion.TopPadding);
                    if (lobbyHangulAllocationCache != null)
                    {
                        lobbyHangulAllocationCache.Add(allocationKey, allocation);
                    }

                    addTexturePatch = true;
                }

                if (addTexturePatch)
                {
                    FontTexturePatch patch = new FontTexturePatch();
                    patch.TargetX = allocation.Cell.X;
                    patch.TargetY = allocation.Cell.Y;
                    patch.TargetChannel = allocation.Cell.Channel;
                    patch.ClearWidth = patchClearWidth;
                    patch.ClearHeight = patchClearHeight;
                    patch.SourceWidth = sourceRegion.Width;
                    patch.SourceHeight = sourceRegion.Height;
                    if (hasVisualScaleSpec && visualScale > 0d)
                    {
                        patch.SourceWidth = replacementWidth;
                        patch.SourceHeight = replacementHeight;
                    }

                    patch.SourceAlpha = patchAlpha;
                    patch.SourceFdtPath = sourceFdtPath;
                    patch.SourceCodepoint = codepoint;
                    AddTexturePatch(texturePatches, allocation.TexturePath, patch);
                }

                byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);

                NormalizeAllocatedLobbyHangulGlyphSpacing(normalizedPath, codepoint, replacementEntry);
                Endian.WriteUInt16LE(replacementEntry, 6, checked((ushort)allocation.Cell.ImageIndex));
                Endian.WriteUInt16LE(replacementEntry, 8, checked((ushort)(allocation.Cell.X + allocation.LeftPadding)));
                Endian.WriteUInt16LE(replacementEntry, 10, checked((ushort)(allocation.Cell.Y + allocation.TopPadding)));
                if (hasVisualScaleSpec && visualScale > 0d)
                {
                    replacementEntry[12] = checked((byte)replacementWidth);
                    replacementEntry[13] = checked((byte)replacementHeight);
                    replacementEntry[14] = unchecked((byte)(sbyte)replacementOffsetX);
                    replacementEntry[15] = unchecked((byte)(sbyte)replacementOffsetY);
                }

                int targetEntryIndex;
                if (targetEntryIndexes.TryGetValue(utf8Value, out targetEntryIndex))
                {
                    if (!GlyphEntryBytesEqual(targetEntries[targetEntryIndex], replacementEntry))
                    {
                        targetEntries[targetEntryIndex] = replacementEntry;
                        changed++;
                    }
                }
                else
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                    targetEntries.Add(replacementEntry);
                    changed++;
                }
            }

            if (changed > 0)
            {
                targetFdt = RewriteFdtGlyphTable(targetFdt, fontTableOffset, glyphStart, glyphEnd, targetEntries);
            }

            if (missingSources > 0)
            {
                AddLimitedWarning("Lobby Hangul source glyphs missing for " + normalizedPath + ": " + missingSources.ToString());
            }

            if (textureFailures > 0)
            {
                AddLimitedWarning("Lobby Hangul source texture failures for " + normalizedPath + ": " + textureFailures.ToString());
            }

            if (allocationFailures > 0)
            {
                AddLimitedWarning("Lobby Hangul atlas allocation failures for " + normalizedPath + ": " + allocationFailures.ToString());
            }

            return changed;
        }

        private static readonly uint[] ActionDetailNumericCodepoints = new uint[]
        {
            '0',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9'
        };

        private static double MeasureMeanVisibleHeightFromTtmpPayloads(
            byte[] fdt,
            string fdtPath,
            uint[] codepoints,
            Dictionary<string, FontPayload> payloadsByPath,
            FileStream mpdStream)
        {
            if (fdt == null ||
                string.IsNullOrWhiteSpace(fdtPath) ||
                codepoints == null ||
                codepoints.Length == 0 ||
                payloadsByPath == null ||
                mpdStream == null)
            {
                return 0d;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0d;
            }

            Dictionary<string, byte[]> textures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            double total = 0d;
            int measured = 0;
            for (int i = 0; i < codepoints.Length; i++)
            {
                uint codepoint = codepoints[i];
                if (!IsHangulCodepoint(codepoint) && (codepoint < '0' || codepoint > '9'))
                {
                    continue;
                }

                int glyphOffset;
                if (!TryFindGlyphEntryOffset(fdt, glyphStart, glyphCount, PackFdtUtf8Value(codepoint), out glyphOffset))
                {
                    continue;
                }

                FdtGlyphEntry glyph = ReadFdtGlyphEntry(fdt, glyphOffset);
                if (glyph.Width == 0 || glyph.Height == 0)
                {
                    continue;
                }

                string texturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    continue;
                }

                byte[] texture;
                if (!textures.TryGetValue(texturePath, out texture))
                {
                    texture = TryLoadTtmpTexturePayload(payloadsByPath, mpdStream, texturePath);
                    if (texture == null)
                    {
                        continue;
                    }

                    textures.Add(texturePath, texture);
                }

                byte[] alpha;
                try
                {
                    alpha = ExtractFontTextureAlpha(texture, glyph);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    continue;
                }

                int minY;
                int maxY;
                if (!TryFindAlphaVisibleYBounds(alpha, glyph.Width, glyph.Height, out minY, out maxY))
                {
                    continue;
                }

                total += maxY - minY + 1;
                measured++;
            }

            return measured > 0 ? total / measured : 0d;
        }

        private static double MeasureMeanVisibleHeightFromArchive(
            byte[] fdt,
            string fdtPath,
            uint[] codepoints,
            SqPackArchive archive,
            Dictionary<string, byte[]> textures)
        {
            if (fdt == null ||
                string.IsNullOrWhiteSpace(fdtPath) ||
                codepoints == null ||
                codepoints.Length == 0 ||
                archive == null ||
                textures == null)
            {
                return 0d;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0d;
            }

            double total = 0d;
            int measured = 0;
            for (int i = 0; i < codepoints.Length; i++)
            {
                uint codepoint = codepoints[i];
                if (!IsHangulCodepoint(codepoint) && (codepoint < '0' || codepoint > '9'))
                {
                    continue;
                }

                int glyphOffset;
                if (!TryFindGlyphEntryOffset(fdt, glyphStart, glyphCount, PackFdtUtf8Value(codepoint), out glyphOffset))
                {
                    continue;
                }

                FdtGlyphEntry glyph = ReadFdtGlyphEntry(fdt, glyphOffset);
                if (glyph.Width == 0 || glyph.Height == 0)
                {
                    continue;
                }

                string texturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                if (texturePath == null)
                {
                    continue;
                }

                byte[] texture;
                if (!TryReadCachedRawTexture(archive, textures, texturePath, out texture))
                {
                    continue;
                }

                byte[] alpha;
                try
                {
                    alpha = ExtractFontTextureAlpha(texture, glyph);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    continue;
                }

                int minY;
                int maxY;
                if (!TryFindAlphaVisibleYBounds(alpha, glyph.Width, glyph.Height, out minY, out maxY))
                {
                    continue;
                }

                total += maxY - minY + 1;
                measured++;
            }

            return measured > 0 ? total / measured : 0d;
        }

        private static bool TryFindAlphaVisibleYBounds(byte[] alpha, int width, int height, out int minY, out int maxY)
        {
            minY = height;
            maxY = -1;
            if (alpha == null || width <= 0 || height <= 0)
            {
                return false;
            }

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (alpha[row + x] == 0)
                    {
                        continue;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            return minY <= maxY;
        }

        private static byte[] PlaceGlyphAlphaAtTop(byte[] sourceAlpha, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            byte[] target = new byte[checked(targetWidth * targetHeight)];
            if (sourceAlpha == null ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                targetWidth <= 0 ||
                targetHeight <= 0)
            {
                return target;
            }

            int copyWidth = Math.Min(sourceWidth, targetWidth);
            int copyHeight = Math.Min(sourceHeight, targetHeight);
            int targetX = Math.Max(0, (targetWidth - copyWidth) / 2);
            for (int y = 0; y < copyHeight; y++)
            {
                Buffer.BlockCopy(
                    sourceAlpha,
                    y * sourceWidth,
                    target,
                    y * targetWidth + targetX,
                    copyWidth);
            }

            return target;
        }

        private bool ShouldRelocateFontOnlyAxisHangulGlyph(string fdtPath, string sourceTexturePath)
        {
            // Clean-atlas relocation cannot cover every AXIS Hangul glyph without
            // atlas exhaustion. Font-only therefore keeps the TTMP AXIS Hangul
            // atlas cells and restores clean shared ASCII/number/symbol cells
            // around them instead.
            return false;
        }

        private static void NormalizeAllocatedLobbyHangulGlyphSpacing(string path, uint codepoint, byte[] glyphEntry)
        {
            // Preserve the source FDT advance/offset. Earlier normalization made
            // lobby Hangul wider than its TTMP source and regressed menu layout.
        }

        private static string CreateLobbyHangulAllocationKey(
            string sourceFdtPath,
            uint codepoint,
            FdtGlyphEntry sourceEntry,
            CleanAsciiTextureRegion sourceRegion)
        {
            return NormalizeGamePath(sourceFdtPath) + "|" +
                   codepoint.ToString("X8") + "|" +
                   sourceEntry.ImageIndex.ToString() + "|" +
                   sourceEntry.X.ToString() + "|" +
                   sourceEntry.Y.ToString() + "|" +
                   sourceEntry.Width.ToString() + "|" +
                   sourceEntry.Height.ToString() + "|" +
                   sourceRegion.Width.ToString() + "|" +
                   sourceRegion.Height.ToString() + "|" +
                   sourceRegion.LeftPadding.ToString() + "|" +
                   sourceRegion.TopPadding.ToString();
        }

        private int ApplyLobbyHangulSourceCells(
            string path,
            ref byte[] targetFdt,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            uint[] requiredCodepoints)
        {
            if (!ShouldPatchLobbyHangulFont(path, requiredCodepoints) ||
                targetFdt == null ||
                mpdStream == null ||
                payloadsByPath == null ||
                texturePatches == null)
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            string sourceFdtPath = ResolveLobbyHangulSourceFdtPath(normalizedPath);
            byte[] sourceFdt = TryLoadTtmpStandardPayload(payloadsByPath, mpdStream, sourceFdtPath);
            if (sourceFdt == null)
            {
                AddLimitedWarning("Lobby Hangul source font missing: " + sourceFdtPath + " -> " + normalizedPath);
                return 0;
            }

            Dictionary<uint, byte[]> sourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            if (sourceEntries.Count == 0)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            int glyphEnd = checked(glyphStart + checked((int)glyphCount) * FdtGlyphEntrySize);
            List<byte[]> targetEntries = new List<byte[]>(checked((int)glyphCount + requiredCodepoints.Length));
            Dictionary<uint, int> targetEntryIndexes = new Dictionary<uint, int>();
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                byte[] entry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(targetFdt, glyphStart + glyphIndex * FdtGlyphEntrySize, entry, 0, FdtGlyphEntrySize);
                uint utf8Value = Endian.ReadUInt32LE(entry, 0);
                if (!targetEntryIndexes.ContainsKey(utf8Value))
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                }

                targetEntries.Add(entry);
            }

            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            int changed = 0;
            int missingSources = 0;
            int textureFailures = 0;
            for (int codepointIndex = 0; codepointIndex < requiredCodepoints.Length; codepointIndex++)
            {
                uint codepoint = requiredCodepoints[codepointIndex];
                if (!IsHangulCodepoint(codepoint))
                {
                    continue;
                }

                uint utf8Value = PackFdtUtf8Value(codepoint);
                int targetEntryIndex;
                bool hasTargetEntry = targetEntryIndexes.TryGetValue(utf8Value, out targetEntryIndex);

                byte[] sourceEntryBytes;
                if (!sourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                {
                    missingSources++;
                    continue;
                }

                FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
                if (sourceEntry.Width == 0 || sourceEntry.Height == 0)
                {
                    continue;
                }

                string sourceTexturePath = ResolveFontTexturePath(sourceFdtPath, sourceEntry.ImageIndex);
                if (!IsLobbyFontTexturePath(sourceTexturePath) || !ShouldIncludeFontPath(sourceTexturePath))
                {
                    textureFailures++;
                    continue;
                }

                byte[] sourceTexture;
                if (!sourceTextures.TryGetValue(sourceTexturePath, out sourceTexture))
                {
                    sourceTexture = TryLoadTtmpTexturePayload(payloadsByPath, mpdStream, sourceTexturePath);
                    if (sourceTexture == null)
                    {
                        textureFailures++;
                        continue;
                    }

                    sourceTextures.Add(sourceTexturePath, sourceTexture);
                }

                byte[] sourceAlpha;
                try
                {
                    sourceAlpha = ExtractFontTextureAlpha(sourceTexture, sourceEntry);
                }
                catch (ArgumentOutOfRangeException)
                {
                    textureFailures++;
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    textureFailures++;
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = sourceEntry.X;
                patch.TargetY = sourceEntry.Y;
                patch.TargetChannel = sourceEntry.ImageIndex % 4;
                patch.ClearWidth = sourceEntry.Width;
                patch.ClearHeight = sourceEntry.Height;
                patch.SourceWidth = sourceEntry.Width;
                patch.SourceHeight = sourceEntry.Height;
                patch.SourceAlpha = sourceAlpha;
                patch.SourceFdtPath = sourceFdtPath;
                patch.SourceCodepoint = codepoint;
                AddTexturePatch(texturePatches, sourceTexturePath, patch);

                byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);
                if (hasTargetEntry)
                {
                    if (!GlyphEntryBytesEqual(targetEntries[targetEntryIndex], replacementEntry))
                    {
                        targetEntries[targetEntryIndex] = replacementEntry;
                        changed++;
                    }
                }
                else
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                    targetEntries.Add(replacementEntry);
                    changed++;
                }
            }

            if (changed > 0)
            {
                targetFdt = RewriteFdtGlyphTable(targetFdt, fontTableOffset, glyphStart, glyphEnd, targetEntries);
            }

            if (missingSources > 0)
            {
                AddLimitedWarning("Lobby Hangul source glyphs missing for " + normalizedPath + ": " + missingSources.ToString());
            }

            if (textureFailures > 0)
            {
                AddLimitedWarning("Lobby Hangul texture cell failures for " + normalizedPath + ": " + textureFailures.ToString());
            }

            return changed;
        }

        private static bool GlyphEntryBytesEqual(byte[] left, byte[] right)
        {
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

        private static bool ShouldPatchLobbyHangulFont(string path, uint[] requiredCodepoints)
        {
            if (!EnableLobbyHangulAllocatedGlyphs && !EnableLegacyLobbyHangulSourceCellGrafting)
            {
                return false;
            }

            if (requiredCodepoints == null || requiredCodepoints.Length == 0)
            {
                return false;
            }

            return LobbyHangulCoverage.IsTargetFontPath(NormalizeGamePath(path));
        }

        private static bool TryAllocateLobbyHangulGlyphCell(
            FontGlyphRepairContext glyphRepair,
            string fdtPath,
            int width,
            int height,
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            string normalizedFdtPath = NormalizeGamePath(fdtPath);
            if (normalizedFdtPath.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) < 0)
            {
                allocatedTexturePath = null;
                allocatedCell = new AllocatedFontGlyphCell();
                return false;
            }

            if (IsLobbyLargeLabelVisualScaleFont(normalizedFdtPath))
            {
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[]
                    {
                        FontLobby2TexturePath,
                        FontLobby1TexturePath,
                        FontLobby3TexturePath,
                        FontLobby4TexturePath,
                        FontLobby5TexturePath,
                        FontLobby6TexturePath,
                        FontLobby7TexturePath
                    },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            if (IsHighScaleLobbyFont(normalizedFdtPath))
            {
                return TryAllocateFromCandidateTextures(
                    glyphRepair,
                    new string[]
                    {
                        FontLobby7TexturePath,
                        FontLobby6TexturePath,
                        FontLobby5TexturePath,
                        FontLobby4TexturePath,
                        FontLobby3TexturePath,
                        FontLobby2TexturePath,
                        FontLobby1TexturePath
                    },
                    width,
                    height,
                    out allocatedTexturePath,
                    out allocatedCell);
            }

            return TryAllocateFromCandidateTextures(
                glyphRepair,
                new string[]
                {
                    FontLobby3TexturePath,
                    FontLobby4TexturePath,
                    FontLobby5TexturePath,
                    FontLobby6TexturePath,
                    FontLobby7TexturePath,
                    FontLobby2TexturePath,
                    FontLobby1TexturePath
                },
                width,
                height,
                out allocatedTexturePath,
                out allocatedCell);
        }

        private static bool TryAllocateActionDetailHighScaleGlyphCell(
            FontGlyphRepairContext glyphRepair,
            string preferredTexturePath,
            int width,
            int height,
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            return TryAllocateFromCandidateTextures(
                glyphRepair,
                new string[]
                {
                    preferredTexturePath,
                    Font2TexturePath,
                    Font1TexturePath,
                    Font3TexturePath,
                    Font4TexturePath,
                    Font5TexturePath,
                    Font6TexturePath,
                    Font7TexturePath
                },
                width,
                height,
                out allocatedTexturePath,
                out allocatedCell);
        }

        private static byte[] ScaleGlyphAlphaBilinear(byte[] sourceAlpha, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            byte[] target = new byte[checked(targetWidth * targetHeight)];
            if (sourceAlpha == null ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                targetWidth <= 0 ||
                targetHeight <= 0)
            {
                return target;
            }

            double scaleX = (double)sourceWidth / targetWidth;
            double scaleY = (double)sourceHeight / targetHeight;
            for (int y = 0; y < targetHeight; y++)
            {
                double sourceY = (y + 0.5d) * scaleY - 0.5d;
                int y0 = ClampInt((int)Math.Floor(sourceY), 0, sourceHeight - 1);
                int y1 = ClampInt(y0 + 1, 0, sourceHeight - 1);
                double fy = sourceY - Math.Floor(sourceY);
                for (int x = 0; x < targetWidth; x++)
                {
                    double sourceX = (x + 0.5d) * scaleX - 0.5d;
                    int x0 = ClampInt((int)Math.Floor(sourceX), 0, sourceWidth - 1);
                    int x1 = ClampInt(x0 + 1, 0, sourceWidth - 1);
                    double fx = sourceX - Math.Floor(sourceX);

                    double top = sourceAlpha[y0 * sourceWidth + x0] * (1d - fx) +
                                 sourceAlpha[y0 * sourceWidth + x1] * fx;
                    double bottom = sourceAlpha[y1 * sourceWidth + x0] * (1d - fx) +
                                    sourceAlpha[y1 * sourceWidth + x1] * fx;
                    int value = (int)Math.Round(top * (1d - fy) + bottom * fy);
                    target[y * targetWidth + x] = (byte)ClampInt(value, 0, 255);
                }
            }

            return target;
        }

        private static byte[] PadGlyphAlpha(byte[] sourceAlpha, int sourceWidth, int sourceHeight, int padding)
        {
            if (sourceAlpha == null || sourceWidth <= 0 || sourceHeight <= 0 || padding <= 0)
            {
                return sourceAlpha;
            }

            int targetWidth = checked(sourceWidth + padding * 2);
            int targetHeight = checked(sourceHeight + padding * 2);
            byte[] target = new byte[checked(targetWidth * targetHeight)];
            for (int y = 0; y < sourceHeight; y++)
            {
                Buffer.BlockCopy(
                    sourceAlpha,
                    y * sourceWidth,
                    target,
                    (y + padding) * targetWidth + padding,
                    sourceWidth);
            }

            return target;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static bool ShouldPatchDialogueGlyphArtifactFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            // The TTMP source has a stray lower-circle fragment in U+BCC0 on these
            // dialogue-sized render paths. Repoint only this glyph to a clean
            // KrnAXIS_180 cell instead of changing broad font families.
            return string.Equals(normalized, "common/font/AXIS_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/MiedingerMid_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/TrumpGothic_184.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetFdtGlyphTable(byte[] fdt, out int fontTableOffset, out uint glyphCount, out int glyphStart)
        {
            fontTableOffset = 0;
            glyphCount = 0;
            glyphStart = 0;
            if (fdt == null ||
                fdt.Length < FdtHeaderSize ||
                !HasAsciiSignature(fdt, 0, "fcsv0100"))
            {
                return false;
            }

            fontTableOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x08));
            if (fontTableOffset < FdtHeaderSize ||
                fontTableOffset > fdt.Length - FdtFontTableHeaderSize ||
                !HasAsciiSignature(fdt, fontTableOffset, "fthd"))
            {
                return false;
            }

            glyphCount = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x04);
            glyphStart = fontTableOffset + FdtFontTableHeaderSize;
            long glyphBytes = (long)glyphCount * FdtGlyphEntrySize;
            return glyphBytes >= 0 && glyphStart <= fdt.Length && glyphStart + glyphBytes <= fdt.Length;
        }

        private static bool EntriesEqual(byte[] fdt, int offset, byte[] entry)
        {
            for (int i = 0; i < FdtGlyphEntrySize; i++)
            {
                if (fdt[offset + i] != entry[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int ApplyPartyListSelfMarkerCleanShapes(
            string path,
            ref byte[] targetFdt,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches)
        {
            if (!ShouldPatchPartyListSelfMarkerCleanShapeFont(path) ||
                glyphRepair == null ||
                globalArchive == null ||
                texturePatches == null ||
                targetFdt == null)
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            string sourceFdtPath = ResolvePartyListSelfMarkerSourceFdtPath(normalizedPath);
            byte[] sourceFdt;
            try
            {
                sourceFdt = globalArchive.ReadFile(sourceFdtPath);
            }
            catch (IOException)
            {
                return 0;
            }
            catch (InvalidDataException)
            {
                return 0;
            }

            Dictionary<uint, byte[]> sourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            if (sourceEntries.Count == 0)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            int glyphEnd = checked(glyphStart + checked((int)glyphCount) * FdtGlyphEntrySize);
            List<byte[]> targetEntries = new List<byte[]>(checked((int)glyphCount + PartyListProtectedPuaGlyphs.Length));
            Dictionary<uint, int> targetEntryIndexes = new Dictionary<uint, int>();
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                byte[] entry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(targetFdt, glyphStart + glyphIndex * FdtGlyphEntrySize, entry, 0, FdtGlyphEntrySize);
                uint utf8Value = Endian.ReadUInt32LE(entry, 0);
                if (!targetEntryIndexes.ContainsKey(utf8Value))
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                }

                targetEntries.Add(entry);
            }

            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            int changed = 0;
            for (int codepointIndex = 0; codepointIndex < PartyListProtectedPuaGlyphs.Length; codepointIndex++)
            {
                uint codepoint = PartyListProtectedPuaGlyphs[codepointIndex];
                uint utf8Value = PackFdtUtf8Value(codepoint);
                byte[] sourceEntryBytes;
                if (!sourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                {
                    continue;
                }

                FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
                if (sourceEntry.Width == 0 || sourceEntry.Height == 0)
                {
                    continue;
                }

                int targetEntryIndex;
                bool hasTargetEntry = targetEntryIndexes.TryGetValue(utf8Value, out targetEntryIndex);
                FdtGlyphEntry targetEntry = hasTargetEntry
                    ? ReadFdtGlyphEntry(targetEntries[targetEntryIndex], 0)
                    : sourceEntry;

                string sourceTexturePath = ResolveFontTexturePath(sourceFdtPath, sourceEntry.ImageIndex);
                string targetTexturePath = ResolveFontTexturePath(normalizedPath, targetEntry.ImageIndex);
                if (sourceTexturePath == null || targetTexturePath == null)
                {
                    continue;
                }

                byte[] sourceTexture;
                if (!sourceTextures.TryGetValue(sourceTexturePath, out sourceTexture))
                {
                    byte[] packedTexture;
                    if (!globalArchive.TryReadPackedFile(sourceTexturePath, out packedTexture))
                    {
                        continue;
                    }

                    List<TextureSubBlock> ignored;
                    sourceTexture = UnpackTextureFile(packedTexture, out ignored);
                    sourceTextures.Add(sourceTexturePath, sourceTexture);
                }

                AllocatedFontGlyphCell allocatedCell = new AllocatedFontGlyphCell();
                string allocatedTexturePath = targetTexturePath;
                bool useAllocatedCell = false;
                bool canReuseTargetCell =
                    hasTargetEntry &&
                    PatchFitsAllocatedFontTexture(
                        glyphRepair,
                        targetTexturePath,
                        targetEntry.X,
                        targetEntry.Y,
                        sourceEntry.Width,
                        sourceEntry.Height);
                if (!canReuseTargetCell)
                {
                    useAllocatedCell = TryAllocateProtectedPuaGlyphCell(
                        glyphRepair,
                        normalizedPath,
                        targetTexturePath,
                        sourceEntry.Width,
                        sourceEntry.Height,
                        out allocatedTexturePath,
                        out allocatedCell);
                }

                if (!canReuseTargetCell && !useAllocatedCell)
                {
                    continue;
                }

                int patchTargetX = useAllocatedCell ? allocatedCell.X : targetEntry.X;
                int patchTargetY = useAllocatedCell ? allocatedCell.Y : targetEntry.Y;
                int patchClearWidth = sourceEntry.Width;
                int patchClearHeight = sourceEntry.Height;
                string patchTexturePath = useAllocatedCell ? allocatedTexturePath : targetTexturePath;
                if (!PatchFitsAllocatedFontTexture(
                    glyphRepair,
                    patchTexturePath,
                    patchTargetX,
                    patchTargetY,
                    Math.Max(patchClearWidth, sourceEntry.Width),
                    Math.Max(patchClearHeight, sourceEntry.Height)))
                {
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = patchTargetX;
                patch.TargetY = patchTargetY;
                patch.TargetChannel = useAllocatedCell ? allocatedCell.Channel : targetEntry.ImageIndex % 4;
                patch.ClearWidth = patchClearWidth;
                patch.ClearHeight = patchClearHeight;
                patch.SourceWidth = sourceEntry.Width;
                patch.SourceHeight = sourceEntry.Height;
                patch.SourceAlpha = ExtractFontTextureAlpha(sourceTexture, sourceEntry);
                patch.SourceFdtPath = sourceFdtPath;
                patch.SourceCodepoint = codepoint;
                AddTexturePatch(texturePatches, patchTexturePath, patch);

                byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);
                if (useAllocatedCell)
                {
                    Endian.WriteUInt16LE(replacementEntry, 6, checked((ushort)allocatedCell.ImageIndex));
                    Endian.WriteUInt16LE(replacementEntry, 8, checked((ushort)allocatedCell.X));
                    Endian.WriteUInt16LE(replacementEntry, 10, checked((ushort)allocatedCell.Y));
                }
                else
                {
                    Endian.WriteUInt16LE(replacementEntry, 6, checked((ushort)targetEntry.ImageIndex));
                    Endian.WriteUInt16LE(replacementEntry, 8, checked((ushort)targetEntry.X));
                    Endian.WriteUInt16LE(replacementEntry, 10, checked((ushort)targetEntry.Y));
                }

                if (hasTargetEntry)
                {
                    targetEntries[targetEntryIndex] = replacementEntry;
                }
                else
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                    targetEntries.Add(replacementEntry);
                }

                changed++;
            }

            if (changed > 0)
            {
                targetFdt = RewriteFdtGlyphTable(targetFdt, fontTableOffset, glyphStart, glyphEnd, targetEntries);
            }

            return changed;
        }

        private static string ResolvePartyListSelfMarkerSourceFdtPath(string path)
        {
            string normalized = NormalizeGamePath(path);
            if (normalized.IndexOf("/krnaxis_120.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_12.fdt";
            }

            if (normalized.IndexOf("/krnaxis_140.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_14.fdt";
            }

            if (normalized.IndexOf("/krnaxis_180.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_18.fdt";
            }

            if (normalized.IndexOf("/krnaxis_360.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_36.fdt";
            }

            return normalized;
        }

        private int ApplyCleanAsciiGlyphShapes(
            string path,
            byte[] targetFdt,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches)
        {
            if (!ShouldRepairCleanAsciiFont(path) ||
                globalArchive == null ||
                texturePatches == null ||
                targetFdt == null)
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            string sourceFdtPath = ResolveCleanAsciiSourceFdtPath(normalizedPath);
            byte[] sourceFdt;
            try
            {
                sourceFdt = globalArchive.ReadFile(sourceFdtPath);
            }
            catch (IOException)
            {
                return 0;
            }
            catch (InvalidDataException)
            {
                return 0;
            }

            Dictionary<uint, byte[]> sourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            if (sourceEntries.Count == 0)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(targetFdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            int changed = 0;
            bool allocateCleanAsciiCell = !_options.FontOnly && ShouldAllocateCleanAsciiCell(normalizedPath);
            int cleanAsciiTexturePadding = GetCleanAsciiTexturePadding(normalizedPath);
            bool repointFullEntry = !ShouldAllocateCleanAsciiInOriginalTargetTexture(normalizedPath) &&
                                    ShouldRepointCleanAsciiFullEntry(normalizedPath);
            bool digitsOnly = ShouldRepairCleanDigitsOnlyFont(normalizedPath) && !ShouldRepairCleanFullAsciiAxisFont(normalizedPath);
            int firstCodepoint = digitsOnly ? '0' : CleanAsciiFirst;
            int lastCodepoint = digitsOnly ? '9' : CleanAsciiLast;
            for (int codepoint = firstCodepoint; codepoint <= lastCodepoint; codepoint++)
            {
                uint utf8Value = PackFdtUtf8Value((uint)codepoint);
                int targetOffset;
                byte[] sourceEntryBytes;
                if (!TryFindGlyphEntryOffset(targetFdt, glyphStart, glyphCount, utf8Value, out targetOffset) ||
                    !sourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                {
                    continue;
                }

                FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
                FdtGlyphEntry originalTargetEntry = ReadFdtGlyphEntry(targetFdt, targetOffset);
                string sourceTexturePath = ResolveFontTexturePath(sourceFdtPath, sourceEntry.ImageIndex);
                string originalTargetTexturePath = ResolveFontTexturePath(normalizedPath, originalTargetEntry.ImageIndex);
                if (sourceTexturePath == null || originalTargetTexturePath == null)
                {
                    continue;
                }

                CleanAsciiTextureRegion sourceRegion = new CleanAsciiTextureRegion();
                CleanAsciiTextureRegion allocationRegion = new CleanAsciiTextureRegion();
                byte[] sourceTexture = null;
                if (sourceEntry.Width > 0 && sourceEntry.Height > 0)
                {
                    if (!TryReadCachedRawTexture(globalArchive, sourceTextures, sourceTexturePath, out sourceTexture))
                    {
                        continue;
                    }

                    sourceRegion = ExtractFontTextureAlphaRegion(sourceTexture, sourceEntry, 0);
                    allocationRegion = cleanAsciiTexturePadding > 0
                        ? ExtractFontTextureAlphaRegion(sourceTexture, sourceEntry, cleanAsciiTexturePadding)
                        : sourceRegion;
                }

                bool useAllocatedCell = false;
                AllocatedFontGlyphCell allocatedCell = new AllocatedFontGlyphCell();
                string allocatedTexturePath = null;
                bool requiresAllocatedCell = allocateCleanAsciiCell && glyphRepair != null && allocationRegion.IsValid;
                if (allocateCleanAsciiCell &&
                    glyphRepair != null &&
                    allocationRegion.IsValid &&
                    TryAllocateCleanAsciiGlyphCell(
                        glyphRepair,
                        normalizedPath,
                        originalTargetTexturePath,
                        sourceTexturePath,
                        allocationRegion.Width,
                        allocationRegion.Height,
                        out allocatedTexturePath,
                        out allocatedCell))
                {
                    byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                    Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);
                    NormalizeCleanAsciiLobbyGlyphSpacing(normalizedPath, (uint)codepoint, replacementEntry);
                    Endian.WriteUInt16LE(replacementEntry, 6, checked((ushort)allocatedCell.ImageIndex));
                    Endian.WriteUInt16LE(replacementEntry, 8, checked((ushort)(allocatedCell.X + allocationRegion.LeftPadding)));
                    Endian.WriteUInt16LE(replacementEntry, 10, checked((ushort)(allocatedCell.Y + allocationRegion.TopPadding)));
                    Buffer.BlockCopy(replacementEntry, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                    useAllocatedCell = true;
                }
                else if (requiresAllocatedCell)
                {
                    AddLimitedWarning(
                        "Skipped clean ASCII glyph repair because no isolated atlas cell was available: " +
                        normalizedPath + " U+" + codepoint.ToString("X4"));
                    continue;
                }
                else if (repointFullEntry)
                {
                    byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                    Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);
                    NormalizeCleanAsciiLobbyGlyphSpacing(normalizedPath, (uint)codepoint, replacementEntry);
                    Buffer.BlockCopy(replacementEntry, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                }

                FdtGlyphEntry targetEntry = ReadFdtGlyphEntry(targetFdt, targetOffset);
                string targetTexturePath = ResolveFontTexturePath(normalizedPath, targetEntry.ImageIndex);
                if (targetTexturePath == null)
                {
                    continue;
                }

                CleanAsciiTextureRegion effectivePatchRegion = useAllocatedCell ? allocationRegion : sourceRegion;
                int effectiveTargetX = useAllocatedCell ? allocatedCell.X : targetEntry.X - effectivePatchRegion.LeftPadding;
                int effectiveTargetY = useAllocatedCell ? allocatedCell.Y : targetEntry.Y - effectivePatchRegion.TopPadding;
                int effectiveClearWidth = useAllocatedCell ? effectivePatchRegion.Width : Math.Max(targetEntry.Width, effectivePatchRegion.Width);
                int effectiveClearHeight = useAllocatedCell ? effectivePatchRegion.Height : Math.Max(targetEntry.Height, effectivePatchRegion.Height);
                string effectiveTexturePath = useAllocatedCell ? allocatedTexturePath : targetTexturePath;
                if (effectivePatchRegion.IsValid &&
                    !PatchFitsAllocatedFontTexture(
                        glyphRepair,
                        effectiveTexturePath,
                        effectiveTargetX,
                        effectiveTargetY,
                        Math.Max(effectiveClearWidth, effectivePatchRegion.Width),
                        Math.Max(effectiveClearHeight, effectivePatchRegion.Height)))
                {
                    continue;
                }

                if (!ShouldIncludeFontPath(targetTexturePath))
                {
                    // Diagnostic profiles can exclude a texture while still
                    // patching FDTs that point to it. Move ASCII/numeric glyphs
                    // to an included clean cell when possible so the excluded
                    // atlas remains untouched.
                    CleanAsciiTextureRegion patchRegion = effectivePatchRegion;
                    if (ShouldIncludeFontPath(sourceTexturePath) && patchRegion.IsValid)
                    {
                        FontTexturePatch patch = CreateCleanAsciiTexturePatch(
                            useAllocatedCell ? allocatedCell.X : sourceEntry.X - patchRegion.LeftPadding,
                            useAllocatedCell ? allocatedCell.Y : sourceEntry.Y - patchRegion.TopPadding,
                            useAllocatedCell ? allocatedCell.Channel : sourceEntry.ImageIndex % 4,
                            patchRegion,
                            patchRegion.Width,
                            patchRegion.Height,
                            sourceFdtPath,
                            (uint)codepoint);
                        AddTexturePatch(texturePatches, useAllocatedCell ? allocatedTexturePath : sourceTexturePath, patch);
                    }

                    if (!useAllocatedCell)
                    {
                        byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                        Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);
                        NormalizeCleanAsciiLobbyGlyphSpacing(normalizedPath, (uint)codepoint, replacementEntry);
                        Buffer.BlockCopy(replacementEntry, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                    }

                    changed++;
                    continue;
                }

                if (sourceRegion.IsValid)
                {
                    CleanAsciiTextureRegion patchRegion = effectivePatchRegion;
                FontTexturePatch patch = CreateCleanAsciiTexturePatch(
                    effectiveTargetX,
                    effectiveTargetY,
                    useAllocatedCell ? allocatedCell.Channel : targetEntry.ImageIndex % 4,
                    patchRegion,
                        effectiveClearWidth,
                        effectiveClearHeight,
                        sourceFdtPath,
                        (uint)codepoint);
                    AddTexturePatch(texturePatches, useAllocatedCell ? allocatedTexturePath : targetTexturePath, patch);
                }

                if (!useAllocatedCell && !repointFullEntry)
                {
                    targetFdt[targetOffset + 12] = sourceEntry.Width;
                    targetFdt[targetOffset + 13] = sourceEntry.Height;
                    targetFdt[targetOffset + 14] = unchecked((byte)NormalizeCleanAsciiLobbyAdvanceAdjustment(normalizedPath, (uint)codepoint, sourceEntry.OffsetX));
                    targetFdt[targetOffset + 15] = unchecked((byte)sourceEntry.OffsetY);
                }

                changed++;
            }

            return changed;
        }

        private int ApplyCleanAsciiKerning(string path, ref byte[] targetFdt, SqPackArchive globalArchive)
        {
            if (!ShouldRepairCleanAsciiFont(path) ||
                globalArchive == null ||
                targetFdt == null)
            {
                return 0;
            }

            string normalizedPath = NormalizeGamePath(path);
            string sourceFdtPath = ResolveCleanAsciiSourceFdtPath(normalizedPath);
            byte[] sourceFdt;
            try
            {
                sourceFdt = globalArchive.ReadFile(sourceFdtPath);
            }
            catch (IOException)
            {
                return 0;
            }
            catch (InvalidDataException)
            {
                return 0;
            }

            int changed = ReplaceAsciiKerningEntries(ref targetFdt, sourceFdt, ShouldNormalizeCleanAsciiLobbySpacing(normalizedPath));
            return changed;
        }

        private static readonly StartScreenKerningRoute[] StartScreenKerningRoutes = new StartScreenKerningRoute[]
        {
            new StartScreenKerningRoute("common/font/AXIS_12.fdt", true, true, false),
            new StartScreenKerningRoute("common/font/KrnAXIS_120.fdt", true, true, false),
            new StartScreenKerningRoute("common/font/AXIS_14.fdt", true, true, true, 2, 1),
            new StartScreenKerningRoute("common/font/KrnAXIS_140.fdt", true, true, true, 2, 1),
            new StartScreenKerningRoute("common/font/AXIS_18.fdt", false, true, false),
            new StartScreenKerningRoute("common/font/KrnAXIS_180.fdt", false, true, false),
            new StartScreenKerningRoute("common/font/AXIS_12_lobby.fdt", true, true, false, 2, 2, true, true),
            new StartScreenKerningRoute("common/font/AXIS_14_lobby.fdt", true, true, false, 1, 1, true),
            new StartScreenKerningRoute("common/font/AXIS_18_lobby.fdt", true, true, false, 1, 1, true)
        };

        private static int ApplyStartScreenSystemSettingsKerning(string path, ref byte[] targetFdt)
        {
            StartScreenKerningRoute route;
            if (!TryGetStartScreenKerningRoute(path, out route) || targetFdt == null)
            {
                return 0;
            }

            return UpsertKerningEntries(
                ref targetFdt,
                CollectStartScreenKerningPairs(route),
                route.MinimumAdjustment);
        }

        private static bool TryGetStartScreenKerningRoute(string path, out StartScreenKerningRoute route)
        {
            string normalized = NormalizeGamePath(path);
            for (int i = 0; i < StartScreenKerningRoutes.Length; i++)
            {
                if (string.Equals(normalized, StartScreenKerningRoutes[i].FontPath, StringComparison.OrdinalIgnoreCase))
                {
                    route = StartScreenKerningRoutes[i];
                    return true;
                }
            }

            route = new StartScreenKerningRoute();
            return false;
        }

        private static List<byte[]> CollectStartScreenKerningPairs(StartScreenKerningRoute route)
        {
            Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (route.IncludeResultMessageHangulPairs)
            {
                AddAdjacentHangulKerningPairs(entries, LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages, route.MinimumAdjustment);
            }

            if (route.IncludeSystemSettingsHangulPairs)
            {
                AddAdjacentHangulKerningPairs(entries, LobbyScaledHangulPhrases.StartScreenSystemSettings, route.MinimumAdjustment);
            }

            if (route.IncludeHighResolutionHangulPairs)
            {
                AddAdjacentHangulKerningPairs(entries, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions, route.MinimumAdjustment);
            }

            if (route.IncludeTerminalPunctuationPairs)
            {
                AddTerminalPunctuationKerningPairs(entries, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions, route.MinimumAdjustment);
                AddTerminalPunctuationKerningPairs(entries, LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages, route.MinimumAdjustment);
            }

            if (route.IncludeHighResolutionPercentPairs)
            {
                AddAsciiPercentKerningPairs(entries, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions, route.PercentAdjustment);
            }

            List<byte[]> result = new List<byte[]>(entries.Values);
            result.Sort(CompareKerningEntries);
            return result;
        }

        private static void AddAdjacentHangulKerningPairs(Dictionary<string, byte[]> entries, string[] phrases, int adjustment)
        {
            AddStartScreenKerningPairs(
                entries,
                phrases,
                adjustment,
                delegate(uint left, uint right)
                {
                    return IsHangulCodepoint(left) && IsHangulCodepoint(right);
                });
        }

        private static void AddTerminalPunctuationKerningPairs(Dictionary<string, byte[]> entries, string[] phrases, int adjustment)
        {
            AddStartScreenKerningPairs(
                entries,
                phrases,
                adjustment,
                delegate(uint left, uint right)
                {
                    return IsHangulCodepoint(left) && IsTerminalPunctuationCodepoint(right);
                });
        }

        private static void AddAsciiPercentKerningPairs(Dictionary<string, byte[]> entries, string[] phrases, int adjustment)
        {
            AddStartScreenKerningPairs(
                entries,
                phrases,
                adjustment,
                delegate(uint left, uint right)
                {
                    return left >= '0' && left <= '9' && right == '%';
                });
        }

        private static void AddStartScreenKerningPairs(
            Dictionary<string, byte[]> entries,
            string[] phrases,
            int adjustment,
            KerningPairPredicate predicate)
        {
            if (phrases == null)
            {
                return;
            }

            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                string phrase = phrases[phraseIndex] ?? string.Empty;
                uint previous = 0;
                bool hasPrevious = false;
                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (hasPrevious && predicate(previous, codepoint))
                    {
                        byte[] entry = CreateKerningEntry(previous, codepoint, adjustment);
                        entries[BuildKerningKey(Endian.ReadUInt32LE(entry, 0), Endian.ReadUInt32LE(entry, 4))] = entry;
                    }

                    previous = codepoint;
                    hasPrevious = !IsPhraseSeparatorCodepoint(codepoint);
                }
            }
        }

        private delegate bool KerningPairPredicate(uint left, uint right);

        private struct StartScreenKerningRoute
        {
            public readonly string FontPath;
            public readonly bool IncludeResultMessageHangulPairs;
            public readonly bool IncludeTerminalPunctuationPairs;
            public readonly bool IncludeHighResolutionPercentPairs;
            public readonly bool IncludeSystemSettingsHangulPairs;
            public readonly bool IncludeHighResolutionHangulPairs;
            public readonly int MinimumAdjustment;
            public readonly int PercentAdjustment;

            public StartScreenKerningRoute(
                string fontPath,
                bool includeResultMessageHangulPairs,
                bool includeTerminalPunctuationPairs,
                bool includeHighResolutionPercentPairs)
                : this(fontPath, includeResultMessageHangulPairs, includeTerminalPunctuationPairs, includeHighResolutionPercentPairs, 2, 2, false)
            {
            }

            public StartScreenKerningRoute(
                string fontPath,
                bool includeResultMessageHangulPairs,
                bool includeTerminalPunctuationPairs,
                bool includeHighResolutionPercentPairs,
                int minimumAdjustment)
                : this(fontPath, includeResultMessageHangulPairs, includeTerminalPunctuationPairs, includeHighResolutionPercentPairs, minimumAdjustment, minimumAdjustment, false)
            {
            }

            public StartScreenKerningRoute(
                string fontPath,
                bool includeResultMessageHangulPairs,
                bool includeTerminalPunctuationPairs,
                bool includeHighResolutionPercentPairs,
                int minimumAdjustment,
                int percentAdjustment)
                : this(fontPath, includeResultMessageHangulPairs, includeTerminalPunctuationPairs, includeHighResolutionPercentPairs, minimumAdjustment, percentAdjustment, false)
            {
            }

            public StartScreenKerningRoute(
                string fontPath,
                bool includeResultMessageHangulPairs,
                bool includeTerminalPunctuationPairs,
                bool includeHighResolutionPercentPairs,
                int minimumAdjustment,
                int percentAdjustment,
                bool includeSystemSettingsHangulPairs)
                : this(fontPath, includeResultMessageHangulPairs, includeTerminalPunctuationPairs, includeHighResolutionPercentPairs, minimumAdjustment, percentAdjustment, includeSystemSettingsHangulPairs, false)
            {
            }

            public StartScreenKerningRoute(
                string fontPath,
                bool includeResultMessageHangulPairs,
                bool includeTerminalPunctuationPairs,
                bool includeHighResolutionPercentPairs,
                int minimumAdjustment,
                int percentAdjustment,
                bool includeSystemSettingsHangulPairs,
                bool includeHighResolutionHangulPairs)
            {
                FontPath = fontPath;
                IncludeResultMessageHangulPairs = includeResultMessageHangulPairs;
                IncludeTerminalPunctuationPairs = includeTerminalPunctuationPairs;
                IncludeHighResolutionPercentPairs = includeHighResolutionPercentPairs;
                IncludeSystemSettingsHangulPairs = includeSystemSettingsHangulPairs;
                IncludeHighResolutionHangulPairs = includeHighResolutionHangulPairs;
                MinimumAdjustment = minimumAdjustment;
                PercentAdjustment = percentAdjustment;
            }
        }

        private static bool IsTerminalPunctuationCodepoint(uint codepoint)
        {
            return codepoint == 0x002Eu ||
                   codepoint == 0x3002u ||
                   codepoint == 0xFF0Eu;
        }

        private static bool IsPhraseSeparatorCodepoint(uint codepoint)
        {
            return codepoint == 0 || codepoint == 0x20u || codepoint == 0x3000u;
        }

        private static byte[] CreateKerningEntry(uint leftCodepoint, uint rightCodepoint, int adjustment)
        {
            byte[] entry = new byte[FdtKerningEntrySize];
            Endian.WriteUInt32LE(entry, 0, PackFdtUtf8Value(leftCodepoint));
            Endian.WriteUInt32LE(entry, 4, PackFdtUtf8Value(rightCodepoint));

            ushort leftShiftJis;
            ushort rightShiftJis;
            if (TryEncodeShiftJisValue(leftCodepoint, out leftShiftJis))
            {
                Endian.WriteUInt16LE(entry, 8, leftShiftJis);
            }

            if (TryEncodeShiftJisValue(rightCodepoint, out rightShiftJis))
            {
                Endian.WriteUInt16LE(entry, 10, rightShiftJis);
            }

            Endian.WriteUInt32LE(entry, 12, unchecked((uint)adjustment));
            return entry;
        }

        private static int UpsertKerningEntries(ref byte[] targetFdt, List<byte[]> requiredEntries, int minimumAdjustment)
        {
            if (requiredEntries == null || requiredEntries.Count == 0)
            {
                return 0;
            }

            int targetHeaderOffset;
            int targetStart;
            int targetCount;
            if (!TryGetFdtKerningTable(targetFdt, out targetHeaderOffset, out targetStart, out targetCount))
            {
                return 0;
            }

            Dictionary<string, byte[]> requiredByKey = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            for (int i = 0; i < requiredEntries.Count; i++)
            {
                byte[] requiredEntry = requiredEntries[i];
                requiredByKey[BuildKerningKey(Endian.ReadUInt32LE(requiredEntry, 0), Endian.ReadUInt32LE(requiredEntry, 4))] = requiredEntry;
            }

            List<byte[]> entries = new List<byte[]>(targetCount + requiredEntries.Count);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int changed = 0;
            for (int i = 0; i < targetCount; i++)
            {
                int offset = targetStart + i * FdtKerningEntrySize;
                byte[] entry = new byte[FdtKerningEntrySize];
                Buffer.BlockCopy(targetFdt, offset, entry, 0, FdtKerningEntrySize);
                string key = BuildKerningKey(Endian.ReadUInt32LE(entry, 0), Endian.ReadUInt32LE(entry, 4));
                byte[] required;
                if (requiredByKey.TryGetValue(key, out required))
                {
                    int current = unchecked((int)Endian.ReadUInt32LE(entry, 12));
                    int requiredAdjustment = unchecked((int)Endian.ReadUInt32LE(required, 12));
                    if (current < requiredAdjustment)
                    {
                        Endian.WriteUInt32LE(entry, 12, unchecked((uint)requiredAdjustment));
                        changed++;
                    }

                    seen.Add(key);
                }

                entries.Add(entry);
            }

            for (int i = 0; i < requiredEntries.Count; i++)
            {
                byte[] requiredEntry = requiredEntries[i];
                string key = BuildKerningKey(Endian.ReadUInt32LE(requiredEntry, 0), Endian.ReadUInt32LE(requiredEntry, 4));
                if (seen.Contains(key))
                {
                    continue;
                }

                byte[] entry = new byte[FdtKerningEntrySize];
                Buffer.BlockCopy(requiredEntry, 0, entry, 0, FdtKerningEntrySize);
                entries.Add(entry);
                changed++;
            }

            if (changed == 0)
            {
                return 0;
            }

            entries.Sort(CompareKerningEntries);
            int targetEnd = checked(targetStart + targetCount * FdtKerningEntrySize);
            int oldKerningBytes = checked(targetCount * FdtKerningEntrySize);
            int newKerningBytes = checked(entries.Count * FdtKerningEntrySize);
            byte[] rewritten = new byte[checked(targetFdt.Length + newKerningBytes - oldKerningBytes)];
            Buffer.BlockCopy(targetFdt, 0, rewritten, 0, targetStart);
            for (int i = 0; i < entries.Count; i++)
            {
                Buffer.BlockCopy(entries[i], 0, rewritten, targetStart + i * FdtKerningEntrySize, FdtKerningEntrySize);
            }

            Buffer.BlockCopy(targetFdt, targetEnd, rewritten, targetStart + newKerningBytes, targetFdt.Length - targetEnd);
            Endian.WriteUInt32LE(rewritten, targetHeaderOffset + 0x04, checked((uint)entries.Count));
            targetFdt = rewritten;
            return changed;
        }

        private static int ReplaceAsciiKerningEntries(ref byte[] targetFdt, byte[] sourceFdt, bool clampNegativeAsciiKerning)
        {
            int targetHeaderOffset;
            int targetStart;
            int targetCount;
            if (!TryGetFdtKerningTable(targetFdt, out targetHeaderOffset, out targetStart, out targetCount))
            {
                return 0;
            }

            int sourceHeaderOffset;
            int sourceStart;
            int sourceCount;
            Dictionary<string, byte[]> sourceAsciiEntries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (TryGetFdtKerningTable(sourceFdt, out sourceHeaderOffset, out sourceStart, out sourceCount))
            {
                for (int i = 0; i < sourceCount; i++)
                {
                    int offset = sourceStart + i * FdtKerningEntrySize;
                    uint left = Endian.ReadUInt32LE(sourceFdt, offset);
                    uint right = Endian.ReadUInt32LE(sourceFdt, offset + 4);
                    if (!IsAsciiKerningKey(left) || !IsAsciiKerningKey(right))
                    {
                        continue;
                    }

                    byte[] entry = new byte[FdtKerningEntrySize];
                    Buffer.BlockCopy(sourceFdt, offset, entry, 0, FdtKerningEntrySize);
                    if (clampNegativeAsciiKerning)
                    {
                        int adjustment = unchecked((int)Endian.ReadUInt32LE(entry, 12));
                        if (adjustment < 0)
                        {
                            Endian.WriteUInt32LE(entry, 12, 0);
                        }
                    }

                    sourceAsciiEntries[BuildKerningKey(left, right)] = entry;
                }
            }

            List<byte[]> entries = new List<byte[]>(targetCount + sourceAsciiEntries.Count);
            int changed = 0;
            for (int i = 0; i < targetCount; i++)
            {
                int offset = targetStart + i * FdtKerningEntrySize;
                uint left = Endian.ReadUInt32LE(targetFdt, offset);
                uint right = Endian.ReadUInt32LE(targetFdt, offset + 4);
                if (IsAsciiKerningKey(left) && IsAsciiKerningKey(right))
                {
                    changed++;
                    continue;
                }

                byte[] entry = new byte[FdtKerningEntrySize];
                Buffer.BlockCopy(targetFdt, offset, entry, 0, FdtKerningEntrySize);
                entries.Add(entry);
            }

            foreach (byte[] sourceEntry in sourceAsciiEntries.Values)
            {
                byte[] entry = new byte[FdtKerningEntrySize];
                Buffer.BlockCopy(sourceEntry, 0, entry, 0, FdtKerningEntrySize);
                entries.Add(entry);
            }

            entries.Sort(CompareKerningEntries);
            int targetEnd = checked(targetStart + targetCount * FdtKerningEntrySize);
            if (changed == sourceAsciiEntries.Count &&
                targetCount == entries.Count &&
                KerningEntriesMatch(targetFdt, targetStart, entries))
            {
                return 0;
            }

            int newKerningBytes = checked(entries.Count * FdtKerningEntrySize);
            int oldKerningBytes = checked(targetCount * FdtKerningEntrySize);
            int delta = newKerningBytes - oldKerningBytes;
            byte[] rewritten = new byte[checked(targetFdt.Length + delta)];
            Buffer.BlockCopy(targetFdt, 0, rewritten, 0, targetStart);
            for (int i = 0; i < entries.Count; i++)
            {
                Buffer.BlockCopy(entries[i], 0, rewritten, targetStart + i * FdtKerningEntrySize, FdtKerningEntrySize);
            }

            Buffer.BlockCopy(targetFdt, targetEnd, rewritten, targetStart + newKerningBytes, targetFdt.Length - targetEnd);
            Endian.WriteUInt32LE(rewritten, targetHeaderOffset + 0x04, checked((uint)entries.Count));
            targetFdt = rewritten;
            return Math.Max(changed, sourceAsciiEntries.Count);
        }

        private static bool TryGetFdtKerningTable(byte[] fdt, out int headerOffset, out int entryStart, out int entryCount)
        {
            headerOffset = 0;
            entryStart = 0;
            entryCount = 0;
            if (fdt == null || fdt.Length < FdtHeaderSize)
            {
                return false;
            }

            uint rawHeaderOffset = Endian.ReadUInt32LE(fdt, 0x0C);
            if (rawHeaderOffset == 0 ||
                rawHeaderOffset > int.MaxValue ||
                rawHeaderOffset < FdtHeaderSize ||
                rawHeaderOffset > fdt.Length - FdtKerningHeaderSize)
            {
                return false;
            }

            headerOffset = checked((int)rawHeaderOffset);
            if (!HasAsciiSignature(fdt, headerOffset, "knhd"))
            {
                return false;
            }

            uint rawCount = Endian.ReadUInt32LE(fdt, headerOffset + 0x04);
            if (rawCount > int.MaxValue)
            {
                return false;
            }

            entryCount = checked((int)rawCount);
            entryStart = headerOffset + FdtKerningHeaderSize;
            long entryBytes = (long)entryCount * FdtKerningEntrySize;
            return entryBytes >= 0 && entryStart <= fdt.Length && entryStart + entryBytes <= fdt.Length;
        }

        private static int CompareKerningEntries(byte[] left, byte[] right)
        {
            int compare = Endian.ReadUInt32LE(left, 0).CompareTo(Endian.ReadUInt32LE(right, 0));
            if (compare != 0)
            {
                return compare;
            }

            return Endian.ReadUInt32LE(left, 4).CompareTo(Endian.ReadUInt32LE(right, 4));
        }

        private static bool KerningEntriesMatch(byte[] fdt, int entryStart, List<byte[]> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                int offset = entryStart + i * FdtKerningEntrySize;
                for (int b = 0; b < FdtKerningEntrySize; b++)
                {
                    if (fdt[offset + b] != entries[i][b])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsAsciiKerningKey(uint value)
        {
            return value >= 0x20 && value <= 0x7E;
        }

        private static string BuildKerningKey(uint left, uint right)
        {
            return left.ToString("X2") + ":" + right.ToString("X2");
        }

        private static bool ShouldRepairCleanAsciiFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            if (!normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (ShouldPreserveCleanGlobalLobbyPayload(normalized))
            {
                return false;
            }

            return IsCleanDamageNumberFontPath(normalized) ||
                   ShouldRepairCleanDigitsOnlyFont(normalized) ||
                   ShouldRepairCleanFullAsciiAxisFont(normalized);
        }

        private static bool ShouldPreserveTtmpCombatFlyTextGlyphs(string path)
        {
            string normalized = NormalizeGamePath(path);
            return normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase) &&
                   normalized.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) < 0 &&
                   normalized.IndexOf("/trumpgothic_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldRepairCombatFlyTextCleanDamageFont(string path)
        {
            return ShouldPreserveTtmpCombatFlyTextGlyphs(path);
        }

        private static bool ShouldRepairCleanDigitsOnlyFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            return normalized.IndexOf("/axis_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldRepairCleanFullAsciiAxisFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            // AXIS fonts are used by character/data-center selection and by
            // high-scale in-game UI. Restoring full ASCII keeps global labels
            // readable without changing Hangul glyph routing.
            return string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_18_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_36_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_12.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_14.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_36.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_96.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_120.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_140.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_180.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/KrnAXIS_360.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldRepointCleanAsciiFullEntry(string path)
        {
            string normalized = NormalizeGamePath(path);
            return IsCleanDamageNumberFontPath(normalized) ||
                   ShouldRepairCleanFullAsciiAxisFont(normalized) ||
                   IsDerived4kLobbyFont(normalized);
        }

        private static bool ShouldAllocateCleanAsciiCell(string path)
        {
            string normalized = NormalizeGamePath(path);
            return ShouldRepairCleanAsciiFont(normalized);
        }

        private static bool ShouldPreserveCleanAsciiTexturePadding(string path)
        {
            string normalized = NormalizeGamePath(path);
            return normalized.IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetCleanAsciiTexturePadding(string path)
        {
            string normalized = NormalizeGamePath(path);
            if (IsCleanDamageNumberFontPath(normalized))
            {
                return DamageNumberGlyphTextureNeighborhoodPadding;
            }

            return ShouldPreserveCleanAsciiTexturePadding(normalized)
                ? CleanAsciiTexturePadding
                : 0;
        }

        private static void NormalizeCleanAsciiLobbyGlyphSpacing(string path, uint codepoint, byte[] glyphEntry)
        {
            if (glyphEntry == null || glyphEntry.Length < FdtGlyphEntrySize)
            {
                return;
            }

            sbyte offsetX = unchecked((sbyte)glyphEntry[14]);
            glyphEntry[14] = unchecked((byte)NormalizeCleanAsciiLobbyAdvanceAdjustment(path, codepoint, offsetX));
        }

        private static sbyte NormalizeCleanAsciiLobbyAdvanceAdjustment(string path, uint codepoint, sbyte offsetX)
        {
            if (!ShouldNormalizeCleanAsciiLobbySpacing(path) ||
                codepoint <= CleanAsciiFirst ||
                codepoint > CleanAsciiLast ||
                offsetX >= 0)
            {
                return offsetX;
            }

            return 0;
        }

        private static bool ShouldNormalizeCleanAsciiLobbySpacing(string path)
        {
            // Clean lobby ASCII spacing is the baseline; widening it caused the
            // reported 100% lobby text gap regression.
            return false;
        }

        private static bool ShouldAllocateCleanAsciiInOriginalTargetTexture(string path)
        {
            string normalized = NormalizeGamePath(path);
            return normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDerived4kLobbyFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            for (int i = 0; i < Derived4kLobbyFonts.Length; i++)
            {
                if (string.Equals(normalized, Derived4kLobbyFonts[i].TargetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveDerived4kLobbySourceFdtPath(string targetPath)
        {
            string normalized = NormalizeGamePath(targetPath);
            for (int i = 0; i < Derived4kLobbyFonts.Length; i++)
            {
                if (string.Equals(normalized, NormalizeGamePath(Derived4kLobbyFonts[i].TargetPath), StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeGamePath(Derived4kLobbyFonts[i].SourcePath);
                }
            }

            return null;
        }

        private static string ResolveLobbyHangulSourceFdtPath(string targetPath)
        {
            string normalized = NormalizeGamePath(targetPath);
            for (int i = 0; i < LobbyHangulSourceFonts.Length; i++)
            {
                if (string.Equals(normalized, NormalizeGamePath(LobbyHangulSourceFonts[i].TargetPath), StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeGamePath(LobbyHangulSourceFonts[i].SourcePath);
                }
            }

            string derivedSource = ResolveDerived4kLobbySourceFdtPath(targetPath);
            return derivedSource ?? normalized;
        }

        private static sbyte ToAdvanceAdjustment(byte glyphWidth, int targetAdvance)
        {
            int normalizedOffsetX = targetAdvance - glyphWidth;
            if (normalizedOffsetX < 0)
            {
                return 0;
            }

            if (normalizedOffsetX > sbyte.MaxValue)
            {
                return sbyte.MaxValue;
            }

            return (sbyte)normalizedOffsetX;
        }

        private static bool IsLobbyFontPath(string fontPath)
        {
            return !string.IsNullOrEmpty(fontPath) &&
                   NormalizeGamePath(fontPath).IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldPreserveCleanGlobalLobbyPayload(string path)
        {
            string normalized = NormalizeGamePath(path);
            return IsLobbyFontPath(normalized) ||
                   IsLobbyFontTexturePath(normalized);
        }

        private static int ComputeNoCjkFallbackSafetyAdvance(byte glyphWidth)
        {
            return Math.Max(2, glyphWidth / 8);
        }

        private static int ComputeMedianCjkAdvance(List<byte[]> glyphEntries)
        {
            if (glyphEntries == null || glyphEntries.Count == 0)
            {
                return 0;
            }

            List<int> advances = new List<int>();
            for (int i = 0; i < glyphEntries.Count; i++)
            {
                byte[] entryBytes = glyphEntries[i];
                uint codepoint;
                if (!TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(entryBytes, 0), out codepoint) ||
                    !IsCjkSpacingBaselineCodepoint(codepoint))
                {
                    continue;
                }

                FdtGlyphEntry entry = ReadFdtGlyphEntry(entryBytes, 0);
                if (entry.Width == 0 || entry.Height == 0)
                {
                    continue;
                }

                advances.Add(Math.Max(1, entry.Width + entry.OffsetX));
            }

            if (advances.Count == 0)
            {
                return 0;
            }

            advances.Sort();
            return advances[advances.Count / 2];
        }

        private static bool IsCjkSpacingBaselineCodepoint(uint codepoint)
        {
            return (codepoint >= 0x3040u && codepoint <= 0x30FFu) ||
                   (codepoint >= 0x3400u && codepoint <= 0x4DBFu) ||
                   (codepoint >= 0x4E00u && codepoint <= 0x9FFFu) ||
                   (codepoint >= 0xF900u && codepoint <= 0xFAFFu);
        }

        private static string ResolveCleanAsciiSourceFdtPath(string path)
        {
            string normalized = NormalizeGamePath(path);
            string derivedSource = ResolveDerived4kLobbySourceFdtPath(normalized);
            if (derivedSource != null)
            {
                return derivedSource;
            }

            if (normalized.IndexOf("/krnaxis_120.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_12.fdt";
            }

            if (normalized.IndexOf("/krnaxis_140.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_14.fdt";
            }

            if (normalized.IndexOf("/krnaxis_180.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_18.fdt";
            }

            if (normalized.IndexOf("/krnaxis_360.fdt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "common/font/AXIS_36.fdt";
            }

            return normalized;
        }

        private static bool IsLobbyFontTexturePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   NormalizeGamePath(path).IndexOf("/font_lobby", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGlobalFontTexturePath(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, Font1TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Font2TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Font3TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Font4TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Font5TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Font6TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Font7TexturePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, FontKrnTexturePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryFindGlyphEntryOffset(byte[] fdt, int glyphStart, uint glyphCount, uint utf8Value, out int offset)
        {
            for (int i = 0; i < glyphCount; i++)
            {
                offset = glyphStart + i * FdtGlyphEntrySize;
                if (Endian.ReadUInt32LE(fdt, offset) == utf8Value)
                {
                    return true;
                }
            }

            offset = 0;
            return false;
        }

        private static FdtGlyphEntry ReadFdtGlyphEntry(byte[] bytes, int offset)
        {
            FdtGlyphEntry entry = new FdtGlyphEntry();
            entry.ImageIndex = Endian.ReadUInt16LE(bytes, offset + 6);
            entry.X = Endian.ReadUInt16LE(bytes, offset + 8);
            entry.Y = Endian.ReadUInt16LE(bytes, offset + 10);
            entry.Width = bytes[offset + 12];
            entry.Height = bytes[offset + 13];
            entry.OffsetX = unchecked((sbyte)bytes[offset + 14]);
            entry.OffsetY = unchecked((sbyte)bytes[offset + 15]);
            return entry;
        }

        private static string ResolveFontTexturePath(string fdtPath, int imageIndex)
        {
            string normalized = NormalizeGamePath(fdtPath);
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

        private static int ResolveImageIndexForTexturePath(string texturePath, int channel)
        {
            if (channel < 0 || channel > 3)
            {
                return -1;
            }

            string normalized = NormalizeGamePath(texturePath);
            if (string.Equals(normalized, Font1TexturePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, FontLobby1TexturePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, FontKrnTexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }

            if (string.Equals(normalized, Font2TexturePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, FontLobby2TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 4 + channel;
            }

            if (string.Equals(normalized, FontLobby3TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 8 + channel;
            }

            if (string.Equals(normalized, FontLobby4TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 12 + channel;
            }

            if (string.Equals(normalized, FontLobby5TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 16 + channel;
            }

            if (string.Equals(normalized, FontLobby6TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 20 + channel;
            }

            if (string.Equals(normalized, FontLobby7TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 24 + channel;
            }

            if (string.Equals(normalized, Font3TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 8 + channel;
            }

            if (string.Equals(normalized, Font4TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 12 + channel;
            }

            if (string.Equals(normalized, Font5TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 16 + channel;
            }

            if (string.Equals(normalized, Font6TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 20 + channel;
            }

            if (string.Equals(normalized, Font7TexturePath, StringComparison.OrdinalIgnoreCase))
            {
                return 24 + channel;
            }

            return -1;
        }

        private static void AddTexturePatch(Dictionary<string, List<FontTexturePatch>> patchesByPath, string texturePath, FontTexturePatch patch)
        {
            List<FontTexturePatch> patches;
            if (!patchesByPath.TryGetValue(texturePath, out patches))
            {
                patches = new List<FontTexturePatch>();
                patchesByPath.Add(texturePath, patches);
            }

            patches.Add(patch);
        }

        private static int AddPartyListSelfMarkerAliasRange(
            List<byte[]> entries,
            Dictionary<uint, byte[]> entriesByCodepoint,
            uint aliasStart,
            uint sourceStart,
            int count)
        {
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                uint sourceCodepoint = sourceStart + (uint)i;
                uint aliasCodepoint = aliasStart + (uint)i;
                uint sourceValue = PackFdtUtf8Value(sourceCodepoint);
                uint aliasValue = PackFdtUtf8Value(aliasCodepoint);
                byte[] sourceEntry;
                if (!entriesByCodepoint.TryGetValue(sourceValue, out sourceEntry))
                {
                    continue;
                }

                byte[] aliasEntry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(sourceEntry, 0, aliasEntry, 0, FdtGlyphEntrySize);
                Endian.WriteUInt32LE(aliasEntry, 0, aliasValue);
                ushort aliasShiftJis;
                if (TryEncodeShiftJisValue(aliasCodepoint, out aliasShiftJis))
                {
                    Endian.WriteUInt16LE(aliasEntry, 4, aliasShiftJis);
                }

                if (!entriesByCodepoint.ContainsKey(aliasValue))
                {
                    entries.Add(aliasEntry);
                    entriesByCodepoint.Add(aliasValue, aliasEntry);
                    added++;
                }
            }

            return added;
        }

        private static int NormalizeFdtGlyphShiftJisValues(byte[] fdt)
        {
            int fontTableOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x08));
            if (fontTableOffset < FdtHeaderSize ||
                fontTableOffset > fdt.Length - FdtFontTableHeaderSize ||
                !HasAsciiSignature(fdt, fontTableOffset, "fthd"))
            {
                return 0;
            }

            uint glyphCount = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x04);
            int glyphStart = fontTableOffset + FdtFontTableHeaderSize;
            long glyphBytes = (long)glyphCount * FdtGlyphEntrySize;
            if (glyphBytes < 0 || glyphStart > fdt.Length || glyphStart + glyphBytes > fdt.Length)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                uint utf8Value = Endian.ReadUInt32LE(fdt, offset);
                uint codepoint;
                ushort shiftJis;
                if (!TryDecodeFdtUtf8Value(utf8Value, out codepoint) ||
                    !TryEncodeShiftJisValue(codepoint, out shiftJis))
                {
                    continue;
                }

                ushort current = Endian.ReadUInt16LE(fdt, offset + 4);
                if (current == shiftJis)
                {
                    continue;
                }

                Endian.WriteUInt16LE(fdt, offset + 4, shiftJis);
                changed++;
            }

            return changed;
        }

        private static int NormalizeFdtKerningShiftJisValues(byte[] fdt)
        {
            int kerningHeaderOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x0C));
            if (kerningHeaderOffset == 0 ||
                kerningHeaderOffset < FdtHeaderSize ||
                kerningHeaderOffset > fdt.Length - FdtKerningHeaderSize ||
                !HasAsciiSignature(fdt, kerningHeaderOffset, "knhd"))
            {
                return 0;
            }

            uint kerningCount = Endian.ReadUInt32LE(fdt, kerningHeaderOffset + 0x04);
            int kerningStart = kerningHeaderOffset + FdtKerningHeaderSize;
            long kerningBytes = (long)kerningCount * FdtKerningEntrySize;
            if (kerningBytes < 0 || kerningStart > fdt.Length || kerningStart + kerningBytes > fdt.Length)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < kerningCount; i++)
            {
                int offset = kerningStart + i * FdtKerningEntrySize;
                changed += NormalizeFdtKerningShiftJisValue(fdt, offset, offset + 8);
                changed += NormalizeFdtKerningShiftJisValue(fdt, offset + 4, offset + 10);
            }

            return changed;
        }

        private static int NormalizeFdtKerningShiftJisValue(byte[] fdt, int utf8Offset, int shiftJisOffset)
        {
            uint utf8Value = Endian.ReadUInt32LE(fdt, utf8Offset);
            uint codepoint;
            ushort shiftJis;
            if (!TryDecodeFdtUtf8Value(utf8Value, out codepoint) ||
                !TryEncodeShiftJisValue(codepoint, out shiftJis))
            {
                return 0;
            }

            ushort current = Endian.ReadUInt16LE(fdt, shiftJisOffset);
            if (current == shiftJis)
            {
                return 0;
            }

            Endian.WriteUInt16LE(fdt, shiftJisOffset, shiftJis);
            return 1;
        }

        private static uint PackFdtUtf8Value(uint codepoint)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(checked((int)codepoint)));
            uint value = 0;
            for (int i = 0; i < bytes.Length && i < 4; i++)
            {
                value = (value << 8) | bytes[i];
            }

            return value;
        }

        private uint[] CreateActionDetailHighScaleHangulCodepoints(string patchedTextSqpack, string fallbackTextSqpack)
        {
            HashSet<uint> codepoints = new HashSet<uint>();
            AddHangulPhraseCodepoints(codepoints, ActionDetailHighScaleHangulGlyphs.FallbackPhrases);
            int staticCount = codepoints.Count;
            int sheetDerived = AddSheetHangulCodepoints(
                codepoints,
                patchedTextSqpack,
                fallbackTextSqpack,
                _options.TargetLanguage,
                ActionDetailHighScaleHangulGlyphs.LargeUiLabelSheetNames,
                "large UI label high-scale glyph coverage");
            int addonRangeDerived = AddAddonHangulCodepoints(
                codepoints,
                patchedTextSqpack,
                fallbackTextSqpack,
                _options.TargetLanguage,
                ActionDetailHighScaleHangulGlyphs.AddonRowRanges,
                "action-detail high-scale glyph coverage");
            int beforeCombatFlyTextExclusion = codepoints.Count;
            RemoveHangulPhraseCodepoints(codepoints, ActionDetailHighScaleHangulGlyphs.CombatFlyTextPreservePhrases);
            int combatFlyTextExcluded = beforeCombatFlyTextExclusion - codepoints.Count;

            uint[] values = ToSortedCodepointArray(codepoints);
            Console.WriteLine(
                "Large UI high-scale Hangul codepoints: {0} static, {1} sheet-derived, {2} addon-range-derived, {3} combat-flytext-preserved, {4} total",
                staticCount,
                sheetDerived,
                addonRangeDerived,
                combatFlyTextExcluded,
                values.Length);
            return values;
        }

        private LobbyHangulCodepointSets CreateLobbyHangulCodepointSets(string koreaSqpack)
        {
            LobbyHangulCodepointSets sets = new LobbyHangulCodepointSets();
            sets.All = CreateLobbyHangulCodepoints(
                koreaSqpack,
                LobbyHangulCoverage.Rows,
                LobbyScaledHangulPhrases.All,
                "lobby all-route glyph coverage");
            sets.SystemSettings = CreateLobbyHangulCodepoints(
                koreaSqpack,
                LobbyHangulCoverage.SystemSettingsRows,
                CombinePhraseGroups(
                    LobbyScaledHangulPhrases.StartScreenSystemSettings,
                    LobbyScaledHangulPhrases.HighResolutionUiScaleOptions,
                    LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages),
                "lobby system-settings glyph coverage");
            sets.CharacterSelect = CreateLobbyHangulCodepoints(
                koreaSqpack,
                LobbyHangulCoverage.CharacterSelectRows,
                new string[0],
                "lobby character-select glyph coverage");
            sets.LargeLabels = CreateLobbyHangulCodepoints(
                koreaSqpack,
                LobbyHangulCoverage.LargeLabelRows,
                CombinePhraseGroups(
                    LobbyScaledHangulPhrases.StartScreenSystemSettings,
                    LobbyScaledHangulPhrases.HighResolutionUiScaleOptions,
                    LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages,
                    LobbyScaledHangulPhrases.CharacterSelectLargeLabels),
                "lobby large-label glyph coverage");
            sets.LargeCharacterLabels = CreateLobbyHangulCodepoints(
                koreaSqpack,
                LobbyHangulCoverage.LargeLabelRows,
                LobbyScaledHangulPhrases.CharacterSelectLargeLabels,
                "lobby character large-label glyph coverage");
            sets.StartMainMenu = CreateLobbyHangulCodepoints(
                koreaSqpack,
                LobbyHangulCoverage.StartMainMenuRows,
                LobbyScaledHangulPhrases.StartScreenMainMenu,
                "lobby start-main-menu glyph coverage");
            sets.SystemAndCharacter = UnionCodepointArrays(sets.SystemSettings, sets.CharacterSelect);
            sets.HighScale = CreatePriorityCodepointArray(
                CombinePhraseGroups(
                    LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages,
                    LobbyScaledHangulPhrases.HighResolutionUiScaleOptions,
                    LobbyScaledHangulPhrases.StartScreenSystemSettings,
                    LobbyScaledHangulPhrases.StartScreenMainMenu),
                sets.SystemSettings,
                sets.CharacterSelect,
                sets.StartMainMenu);
            Console.WriteLine(
                "Lobby Hangul route sets: all={0}, system={1}, character={2}, main-menu={3}, system+character={4}, high-scale={5}",
                sets.All.Length,
                sets.SystemSettings.Length,
                sets.CharacterSelect.Length,
                sets.StartMainMenu.Length,
                sets.SystemAndCharacter.Length,
                sets.HighScale.Length);
            return sets;
        }

        private uint[] CreateLobbyHangulCodepoints(
            string koreaSqpack,
            LobbyHangulCoverageRowSpec[] rows,
            string[] staticPhrases,
            string label)
        {
            HashSet<uint> codepoints = new HashSet<uint>();
            if (staticPhrases != null && staticPhrases.Length > 0)
            {
                AddHangulPhraseCodepoints(codepoints, staticPhrases);
            }

            int staticCount = codepoints.Count;
            int sheetDerived = AddLobbySheetHangulCodepoints(
                codepoints,
                koreaSqpack,
                rows,
                label);

            uint[] values = ToSortedCodepointArray(codepoints);
            Console.WriteLine(
                "{0}: {1} static, {2} sheet-derived, {3} total",
                label,
                staticCount,
                sheetDerived,
                values.Length);
            return values;
        }

        private static uint[] SelectLobbyHangulCodepointsForFont(
            string path,
            LobbyHangulCodepointSets sets)
        {
            if (sets == null)
            {
                return null;
            }

            string normalized = NormalizeGamePath(path);
            if (LobbyHangulCoverage.IsMainMenuOnlyTargetFontPath(normalized))
            {
                return sets.StartMainMenu;
            }

            if (IsLobbyLargeLabelVisualScaleFont(normalized))
            {
                return sets.LargeCharacterLabels;
            }

            if (IsCharacterSelectOnlyLobbyFont(normalized))
            {
                return sets.CharacterSelect;
            }

            if (IsHighScaleLobbyFont(normalized))
            {
                return sets.HighScale;
            }

            return sets.SystemAndCharacter;
        }

        private static bool IsCharacterSelectOnlyLobbyFont(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, "common/font/MiedingerMid_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/MiedingerMid_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/TrumpGothic_34_lobby.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHighScaleLobbyFont(string path)
        {
            return LobbyHangulCoverage.IsHighScaleTargetFontPath(path);
        }

        private static bool IsLobbyLargeLabelVisualScaleFont(string path)
        {
            LobbyLargeLabelVisualScaleSpec ignored;
            return TryGetLobbyLargeLabelVisualScaleSpec(path, out ignored);
        }

        private static string[] CombinePhraseGroups(params string[][] groups)
        {
            List<string> phrases = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                string[] group = groups[groupIndex];
                if (group == null)
                {
                    continue;
                }

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

        private static uint[] CreatePriorityCodepointArray(string[] priorityPhrases, params uint[][] fallbackGroups)
        {
            List<uint> ordered = new List<uint>();
            HashSet<uint> seen = new HashSet<uint>();
            AddHangulPhraseCodepointsInOrder(ordered, seen, priorityPhrases);
            for (int groupIndex = 0; groupIndex < fallbackGroups.Length; groupIndex++)
            {
                uint[] group = fallbackGroups[groupIndex];
                if (group == null)
                {
                    continue;
                }

                for (int i = 0; i < group.Length; i++)
                {
                    if (seen.Add(group[i]))
                    {
                        ordered.Add(group[i]);
                    }
                }
            }

            return ordered.ToArray();
        }

        private static void AddHangulPhraseCodepointsInOrder(List<uint> codepoints, HashSet<uint> seen, string[] phrases)
        {
            if (codepoints == null || seen == null || phrases == null)
            {
                return;
            }

            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                string phrase = phrases[phraseIndex] ?? string.Empty;
                for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref charIndex);
                    if (IsHangulCodepoint(codepoint) && seen.Add(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }
            }
        }

        private static uint[] UnionCodepointArrays(params uint[][] groups)
        {
            HashSet<uint> values = new HashSet<uint>();
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                uint[] group = groups[groupIndex];
                if (group == null)
                {
                    continue;
                }

                for (int i = 0; i < group.Length; i++)
                {
                    values.Add(group[i]);
                }
            }

            uint[] array = new uint[values.Count];
            values.CopyTo(array);
            Array.Sort(array);
            return array;
        }

        private int AddLobbySheetHangulCodepoints(
            HashSet<uint> codepoints,
            string koreaSqpack,
            LobbyHangulCoverageRowSpec[] ranges,
            string label)
        {
            if (codepoints == null || string.IsNullOrEmpty(koreaSqpack) || ranges == null || ranges.Length == 0)
            {
                return 0;
            }

            string textIndexPath = Path.Combine(koreaSqpack, TextIndexFileName);
            if (!File.Exists(textIndexPath))
            {
                AddLimitedWarning("Patched text index missing for " + label + ": " + textIndexPath);
                return 0;
            }

            int before = codepoints.Count;
            try
            {
                using (SqPackArchive textArchive = new SqPackArchive(textIndexPath, koreaSqpack, TextDatPrefix))
                {
                    Dictionary<string, List<LobbyHangulCoverageRowSpec>> rangesBySheet =
                        new Dictionary<string, List<LobbyHangulCoverageRowSpec>>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < ranges.Length; i++)
                    {
                        LobbyHangulCoverageRowSpec range = ranges[i];
                        if (string.IsNullOrEmpty(range.Sheet))
                        {
                            continue;
                        }

                        List<LobbyHangulCoverageRowSpec> sheetRanges;
                        if (!rangesBySheet.TryGetValue(range.Sheet, out sheetRanges))
                        {
                            sheetRanges = new List<LobbyHangulCoverageRowSpec>();
                            rangesBySheet.Add(range.Sheet, sheetRanges);
                        }

                        sheetRanges.Add(range);
                    }

                    foreach (KeyValuePair<string, List<LobbyHangulCoverageRowSpec>> pair in rangesBySheet)
                    {
                        AddLobbySheetHangulCodepointsFromSheet(textArchive, codepoints, pair.Key, pair.Value, label);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLimitedWarning("Could not collect lobby Hangul glyph coverage for " + label + ": " + ex.Message);
                return 0;
            }

            return codepoints.Count - before;
        }

        private void AddLobbySheetHangulCodepointsFromSheet(
            SqPackArchive textArchive,
            HashSet<uint> codepoints,
            string sheet,
            List<LobbyHangulCoverageRowSpec> ranges,
            string label)
        {
            ExcelHeader header;
            try
            {
                header = ExcelHeader.Parse(textArchive.ReadFile("exd/" + sheet + ".exh"));
            }
            catch (Exception ex)
            {
                AddLimitedWarning("Lobby glyph coverage sheet header missing for " + sheet + ": " + ex.Message);
                return;
            }

            if (header.Variant != ExcelVariant.Default)
            {
                AddLimitedWarning("Lobby glyph coverage sheet variant is not supported for " + sheet + ": " + header.Variant.ToString());
                return;
            }

            byte languageId = LanguageCodes.ToId(_options.SourceLanguage);
            bool hasLanguageSuffix = header.HasLanguage(languageId);
            List<int> allStringColumns = header.GetStringColumnIndexes();
            if (allStringColumns.Count == 0)
            {
                return;
            }

            for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
            {
                ExcelPageDefinition page = header.Pages[pageIndex];
                if (!LobbyPageOverlaps(page, ranges))
                {
                    continue;
                }

                string exdPath = BuildExdPath(sheet, page.StartId, hasLanguageSuffix ? _options.SourceLanguage : null);
                ExcelDataFile file;
                try
                {
                    file = ExcelDataFile.Parse(textArchive.ReadFile(exdPath));
                }
                catch (Exception ex)
                {
                    AddLimitedWarning("Lobby glyph coverage page missing for " + exdPath + ": " + ex.Message);
                    continue;
                }

                for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = file.Rows[rowIndex];
                    for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                    {
                        LobbyHangulCoverageRowSpec range = ranges[rangeIndex];
                        if (!range.Contains(row.RowId))
                        {
                            continue;
                        }

                        if (range.ColumnOffset.HasValue)
                        {
                            byte[] bytes = file.GetStringBytesByColumnOffset(row, header, range.ColumnOffset.Value);
                            AddUtf8HangulCodepoints(codepoints, bytes);
                        }
                        else
                        {
                            for (int columnIndex = 0; columnIndex < allStringColumns.Count; columnIndex++)
                            {
                                byte[] bytes = file.GetStringBytes(row, header, allStringColumns[columnIndex]);
                                AddUtf8HangulCodepoints(codepoints, bytes);
                            }
                        }
                    }
                }
            }
        }

        private int AddAddonHangulCodepoints(HashSet<uint> codepoints, string textSqpack, string fallbackTextSqpack, string language, AddonRowRange[] ranges, string label)
        {
            if (codepoints == null || string.IsNullOrEmpty(textSqpack) || string.IsNullOrEmpty(fallbackTextSqpack) || ranges == null || ranges.Length == 0)
            {
                return 0;
            }

            string textIndexPath = Path.Combine(textSqpack, TextIndexFileName);
            string fallbackTextIndexPath = Path.Combine(fallbackTextSqpack, TextIndexFileName);
            if (!File.Exists(textIndexPath))
            {
                AddLimitedWarning("Patched text index missing for " + label + ": " + textIndexPath);
                return 0;
            }

            if (!File.Exists(fallbackTextIndexPath))
            {
                AddLimitedWarning("Fallback text index missing for " + label + ": " + fallbackTextIndexPath);
                return 0;
            }

            int before = codepoints.Count;
            try
            {
                using (SqPackArchive textArchive = new SqPackArchive(textIndexPath, textSqpack, TextDatPrefix))
                using (SqPackArchive fallbackArchive = new SqPackArchive(fallbackTextIndexPath, fallbackTextSqpack, TextDatPrefix))
                {
                    ExcelHeader header = ExcelHeader.Parse(ReadTextFile(textArchive, fallbackArchive, "exd/Addon.exh"));
                    if (header.Variant != ExcelVariant.Default)
                    {
                        AddLimitedWarning("Addon header variant is not supported for " + label + ": " + header.Variant.ToString());
                        return 0;
                    }

                    byte languageId = LanguageCodes.ToId(language);
                    bool hasLanguageSuffix = header.HasLanguage(languageId);
                    List<int> stringColumns = header.GetStringColumnIndexes();
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        if (!AddonPageOverlaps(page, ranges))
                        {
                            continue;
                        }

                        string exdPath = BuildExdPath("Addon", page.StartId, hasLanguageSuffix ? language : null);
                        ExcelDataFile file = ExcelDataFile.Parse(ReadTextFile(textArchive, fallbackArchive, exdPath));
                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            if (!RowInRanges(row.RowId, ranges))
                            {
                                continue;
                            }

                            for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                            {
                                byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]);
                                AddUtf8HangulCodepoints(codepoints, bytes);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLimitedWarning("Could not collect Addon Hangul glyph coverage for " + label + ": " + ex.Message);
                return 0;
            }

            return codepoints.Count - before;
        }

        private int AddSheetHangulCodepoints(HashSet<uint> codepoints, string textSqpack, string fallbackTextSqpack, string language, string[] sheets, string label)
        {
            if (codepoints == null || string.IsNullOrEmpty(textSqpack) || string.IsNullOrEmpty(fallbackTextSqpack) || sheets == null || sheets.Length == 0)
            {
                return 0;
            }

            string textIndexPath = Path.Combine(textSqpack, TextIndexFileName);
            string fallbackTextIndexPath = Path.Combine(fallbackTextSqpack, TextIndexFileName);
            if (!File.Exists(textIndexPath))
            {
                AddLimitedWarning("Patched text index missing for " + label + ": " + textIndexPath);
                return 0;
            }

            if (!File.Exists(fallbackTextIndexPath))
            {
                AddLimitedWarning("Fallback text index missing for " + label + ": " + fallbackTextIndexPath);
                return 0;
            }

            int before = codepoints.Count;
            try
            {
                using (SqPackArchive textArchive = new SqPackArchive(textIndexPath, textSqpack, TextDatPrefix))
                using (SqPackArchive fallbackArchive = new SqPackArchive(fallbackTextIndexPath, fallbackTextSqpack, TextDatPrefix))
                {
                    for (int sheetIndex = 0; sheetIndex < sheets.Length; sheetIndex++)
                    {
                        AddSheetHangulCodepointsFromSheet(textArchive, fallbackArchive, codepoints, language, sheets[sheetIndex], label);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLimitedWarning("Could not collect sheet Hangul glyph coverage for " + label + ": " + ex.Message);
                return 0;
            }

            return codepoints.Count - before;
        }

        private void AddSheetHangulCodepointsFromSheet(SqPackArchive textArchive, SqPackArchive fallbackArchive, HashSet<uint> codepoints, string language, string sheet, string label)
        {
            if (string.IsNullOrWhiteSpace(sheet))
            {
                return;
            }

            ExcelHeader header;
            try
            {
                header = ExcelHeader.Parse(ReadTextFile(textArchive, fallbackArchive, "exd/" + sheet + ".exh"));
            }
            catch (Exception ex)
            {
                AddLimitedWarning("Sheet glyph coverage header missing for " + sheet + " (" + label + "): " + ex.Message);
                return;
            }

            if (header.Variant != ExcelVariant.Default)
            {
                AddLimitedWarning("Sheet glyph coverage variant is not supported for " + sheet + " (" + label + "): " + header.Variant.ToString());
                return;
            }

            byte languageId = LanguageCodes.ToId(language);
            bool hasLanguageSuffix = header.HasLanguage(languageId);
            List<int> stringColumns = header.GetStringColumnIndexes();
            if (stringColumns.Count == 0)
            {
                return;
            }

            for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
            {
                ExcelPageDefinition page = header.Pages[pageIndex];
                string exdPath = BuildExdPath(sheet, page.StartId, hasLanguageSuffix ? language : null);
                ExcelDataFile file;
                try
                {
                    file = ExcelDataFile.Parse(ReadTextFile(textArchive, fallbackArchive, exdPath));
                }
                catch (Exception ex)
                {
                    AddLimitedWarning("Sheet glyph coverage page missing for " + exdPath + " (" + label + "): " + ex.Message);
                    continue;
                }

                for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = file.Rows[rowIndex];
                    for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                    {
                        byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]);
                        AddUtf8HangulCodepoints(codepoints, bytes);
                    }
                }
            }
        }

        private static bool AddonPageOverlaps(ExcelPageDefinition page, AddonRowRange[] ranges)
        {
            uint pageEnd = page.RowCount == 0 ? page.StartId : page.StartId + page.RowCount - 1;
            for (int i = 0; i < ranges.Length; i++)
            {
                if (ranges[i].StartId <= pageEnd && ranges[i].EndId >= page.StartId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RowInRanges(uint rowId, AddonRowRange[] ranges)
        {
            for (int i = 0; i < ranges.Length; i++)
            {
                if (ranges[i].Contains(rowId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LobbyPageOverlaps(ExcelPageDefinition page, List<LobbyHangulCoverageRowSpec> ranges)
        {
            uint pageEnd = page.RowCount == 0 ? page.StartId : page.StartId + page.RowCount - 1;
            for (int i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].StartId <= pageEnd && ranges[i].EndId >= page.StartId)
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] ReadTextFile(SqPackArchive primaryArchive, SqPackArchive fallbackArchive, string path)
        {
            try
            {
                return primaryArchive.ReadFile(path);
            }
            catch (FileNotFoundException)
            {
                return fallbackArchive.ReadFile(path);
            }
        }

        private static string BuildExdPath(string sheet, uint pageStartId, string language)
        {
            return "exd/" + sheet + "_" + pageStartId + (string.IsNullOrEmpty(language) ? string.Empty : "_" + language) + ".exd";
        }

        private static void AddUtf8HangulCodepoints(HashSet<uint> codepoints, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            AddHangulPhraseCodepoints(codepoints, Encoding.UTF8.GetString(bytes));
        }

        private static void AddHangulPhraseCodepoints(HashSet<uint> codepoints, string[] phrases)
        {
            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                AddHangulPhraseCodepoints(codepoints, phrases[phraseIndex]);
            }
        }

        private static void AddHangulPhraseCodepoints(HashSet<uint> codepoints, string phrase)
        {
            phrase = phrase ?? string.Empty;
            for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
            {
                uint codepoint = ReadCodepoint(phrase, ref charIndex);
                if (IsHangulCodepoint(codepoint))
                {
                    codepoints.Add(codepoint);
                }
            }
        }

        private static void RemoveHangulPhraseCodepoints(HashSet<uint> codepoints, string[] phrases)
        {
            if (codepoints == null || phrases == null)
            {
                return;
            }

            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                RemoveHangulPhraseCodepoints(codepoints, phrases[phraseIndex]);
            }
        }

        private static void RemoveHangulPhraseCodepoints(HashSet<uint> codepoints, string phrase)
        {
            phrase = phrase ?? string.Empty;
            for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
            {
                uint codepoint = ReadCodepoint(phrase, ref charIndex);
                if (IsHangulCodepoint(codepoint))
                {
                    codepoints.Remove(codepoint);
                }
            }
        }

        private static uint[] ToSortedCodepointArray(HashSet<uint> codepoints)
        {
            uint[] values = new uint[codepoints.Count];
            codepoints.CopyTo(values);
            Array.Sort(values);
            return values;
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

        private static bool IsHangulCodepoint(uint codepoint)
        {
            return (codepoint >= 0xAC00 && codepoint <= 0xD7A3) ||
                   (codepoint >= 0x1100 && codepoint <= 0x11FF) ||
                   (codepoint >= 0x3130 && codepoint <= 0x318F) ||
                   (codepoint >= 0xA960 && codepoint <= 0xA97F) ||
                   (codepoint >= 0xD7B0 && codepoint <= 0xD7FF);
        }

        private static bool TryDecodeFdtUtf8Value(uint value, out uint codepoint)
        {
            if ((value & 0xFFFFFF80u) == 0)
            {
                codepoint = value & 0x7Fu;
                return true;
            }

            if ((value & 0xFFFFE0C0u) == 0x0000C080u)
            {
                codepoint = (((value >> 8) & 0x1Fu) << 6) |
                            (((value >> 0) & 0x3Fu) << 0);
                return true;
            }

            if ((value & 0x00F0C0C0u) == 0x00E08080u)
            {
                codepoint = (((value >> 16) & 0x0Fu) << 12) |
                            (((value >> 8) & 0x3Fu) << 6) |
                            (((value >> 0) & 0x3Fu) << 0);
                return true;
            }

            if ((value & 0xF8C0C0C0u) == 0xF0808080u)
            {
                codepoint = (((value >> 24) & 0x07u) << 18) |
                            (((value >> 16) & 0x3Fu) << 12) |
                            (((value >> 8) & 0x3Fu) << 6) |
                            (((value >> 0) & 0x3Fu) << 0);
                return codepoint <= 0x10FFFFu;
            }

            codepoint = 0;
            return false;
        }

        private static bool TryEncodeShiftJisValue(uint codepoint, out ushort shiftJis)
        {
            shiftJis = 0;
            if (codepoint > 0x10FFFFu)
            {
                return false;
            }

            try
            {
                string text = char.ConvertFromUtf32(checked((int)codepoint));
                Encoding encoding = Encoding.GetEncoding(
                    932,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback);
                byte[] bytes = encoding.GetBytes(text);
                if (bytes.Length == 1)
                {
                    shiftJis = bytes[0];
                    return true;
                }

                if (bytes.Length == 2)
                {
                    shiftJis = (ushort)((bytes[0] << 8) | bytes[1]);
                    return true;
                }
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            return false;
        }

        private static bool HasAsciiSignature(byte[] bytes, int offset, string signature)
        {
            if (offset < 0 || offset + signature.Length > bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < signature.Length; i++)
            {
                if (bytes[offset + i] != (byte)signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldIncludeFontPath(string path)
        {
            if (_options.FontOnly)
            {
                return IsFontOnlyPatchPath(path);
            }

            string profile = string.IsNullOrEmpty(_options.FontPatchProfile)
                ? FontPatchProfiles.Default
                : _options.FontPatchProfile;
            if (string.Equals(profile, FontPatchProfiles.Full, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string normalized = NormalizeGamePath(path).ToLowerInvariant();
            if (string.Equals(profile, FontPatchProfiles.NoMiedingerMid, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.IndexOf("/miedingermid_", StringComparison.OrdinalIgnoreCase) < 0;
            }

            if (string.Equals(profile, FontPatchProfiles.NoJupiter, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.IndexOf("/jupiter_", StringComparison.OrdinalIgnoreCase) < 0;
            }

            if (string.Equals(profile, FontPatchProfiles.NoAxis, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.IndexOf("/axis_", StringComparison.OrdinalIgnoreCase) < 0 &&
                       normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) < 0;
            }

            if (string.Equals(profile, FontPatchProfiles.UiNumericSafe, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.IndexOf("/miedingermid_", StringComparison.OrdinalIgnoreCase) < 0 &&
                       normalized.IndexOf("/trumpgothic_", StringComparison.OrdinalIgnoreCase) < 0 &&
                       normalized.IndexOf("/jupiter_", StringComparison.OrdinalIgnoreCase) < 0 &&
                       !normalized.EndsWith("/font3.tex", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(profile, FontPatchProfiles.FdtOnly, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(profile, FontPatchProfiles.TexturesOnly, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.EndsWith(".tex", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static bool IsFontOnlyPatchPath(string path)
        {
            string normalized = NormalizeGamePath(path);
            for (int i = 0; i < FontOnlyPatchPaths.Length; i++)
            {
                if (string.Equals(normalized, FontOnlyPatchPaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFontOnlyGameAxisFontPath(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, "common/font/AXIS_12.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_14.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_36.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_96.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private FontPatchPackage ResolveFontPatchPackage()
        {
            foreach (string directory in EnumerateFontPackageCandidateDirs())
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string fullDirectory = Path.GetFullPath(directory);
                string mpdPath = Path.Combine(fullDirectory, TtmpMpdFileName);
                string mplPath = Path.Combine(fullDirectory, TtmpMplFileName);
                if (!File.Exists(mpdPath) || !File.Exists(mplPath))
                {
                    continue;
                }

                FontPatchPackage fontPackage = new FontPatchPackage();
                fontPackage.DirectoryPath = fullDirectory;
                fontPackage.MpdPath = mpdPath;
                fontPackage.MplPath = mplPath;
                fontPackage.Payloads = LoadFontPayloads(mplPath);
                if (fontPackage.Payloads.Count == 0)
                {
                    throw new InvalidDataException("TTMPL.mpl does not contain any font payloads: " + mplPath);
                }

                return fontPackage;
            }

            return null;
        }

        private IEnumerable<string> EnumerateFontPackageCandidateDirs()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] candidates = new string[]
            {
                _options.FontPackDir,
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FontPatchAssets"),
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "FontPatchAssets")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(candidate);
                if (seen.Add(fullPath))
                {
                    yield return fullPath;
                }
            }
        }

        private static List<FontPayload> LoadFontPayloads(string mplPath)
        {
            List<FontPayload> payloads = new List<FontPayload>();
            foreach (string rawLine in File.ReadLines(mplPath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                FontPayload payload = new FontPayload();
                payload.FullPath = ExtractJsonString(rawLine, "FullPath");
                payload.ModOffset = ExtractJsonInt(rawLine, "ModOffset");
                payload.ModSize = ExtractJsonInt(rawLine, "ModSize");
                if (payload.ModSize <= 0)
                {
                    throw new InvalidDataException("Invalid TTMP payload size for " + payload.FullPath);
                }

                payloads.Add(payload);
            }

            return payloads;
        }

        private static string ExtractJsonString(string jsonLine, string fieldName)
        {
            Match match = Regex.Match(jsonLine, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"");
            if (!match.Success)
            {
                throw new InvalidDataException("Missing string field " + fieldName + " in TTMPL line: " + jsonLine);
            }

            return Regex.Unescape(match.Groups["value"].Value);
        }

        private static int ExtractJsonInt(string jsonLine, string fieldName)
        {
            Match match = Regex.Match(jsonLine, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*(?<value>-?\\d+)");
            if (!match.Success)
            {
                throw new InvalidDataException("Missing integer field " + fieldName + " in TTMPL line: " + jsonLine);
            }

            return int.Parse(match.Groups["value"].Value);
        }

        private static byte[] TryLoadTtmpStandardPayload(FontPatchPackage fontPackage, FileStream mpdStream, string gamePath)
        {
            string normalizedPath = NormalizeGamePath(gamePath);
            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                FontPayload payload = fontPackage.Payloads[i];
                if (!string.Equals(NormalizeGamePath(payload.FullPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, normalizedPath);
                return SqPackArchive.UnpackStandardFile(packed);
            }

            return null;
        }

        private static byte[] TryLoadTtmpStandardPayload(Dictionary<string, FontPayload> payloadsByPath, FileStream mpdStream, string gamePath)
        {
            string normalizedPath = NormalizeGamePath(gamePath);
            FontPayload payload;
            if (payloadsByPath == null || !payloadsByPath.TryGetValue(normalizedPath, out payload))
            {
                return null;
            }

            byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, normalizedPath);
            return SqPackArchive.UnpackStandardFile(packed);
        }

        private static string NormalizeGamePath(string path)
        {
            return path.Replace('\\', '/').Trim();
        }

        private static byte[] ReadPackedPayload(FileStream mpdStream, int offset, int size, string path)
        {
            if (offset < 0 || size <= 0 || offset + (long)size > mpdStream.Length)
            {
                throw new InvalidDataException("TTMP payload is outside TTMPD.mpd: " + path);
            }

            byte[] buffer = new byte[size];
            mpdStream.Position = offset;
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = mpdStream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead != buffer.Length)
            {
                throw new EndOfStreamException("Could not read TTMP payload: " + path);
            }

            return buffer;
        }

        private static TargetedGlyphRepairContext TryCreateDialogueGlyphArtifactRepairContext(FontPatchPackage fontPackage, FileStream mpdStream)
        {
            byte[] sourceFdt = TryLoadTtmpStandardPayload(fontPackage, mpdStream, DialogueGlyphArtifactSourcePath);
            if (sourceFdt == null)
            {
                return null;
            }

            Dictionary<uint, byte[]> sourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            uint utf8Value = PackFdtUtf8Value(DialogueByeonCodepoint);
            byte[] sourceEntryBytes;
            if (!sourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
            {
                return null;
            }

            FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
            string sourceTexturePath = ResolveFontTexturePath(DialogueGlyphArtifactSourcePath, sourceEntry.ImageIndex);
            if (sourceTexturePath == null)
            {
                return null;
            }

            byte[] sourceTexture = TryLoadTtmpTexturePayload(fontPackage, mpdStream, sourceTexturePath);
            if (sourceTexture == null)
            {
                return null;
            }

            TargetedGlyphRepairContext context = new TargetedGlyphRepairContext();
            TargetedGlyphSource source = new TargetedGlyphSource();
            source.Codepoint = DialogueByeonCodepoint;
            source.SourceFdtPath = DialogueGlyphArtifactSourcePath;
            source.Width = sourceEntry.Width;
            source.Height = sourceEntry.Height;
            source.OffsetX = sourceEntry.OffsetX;
            source.OffsetY = sourceEntry.OffsetY;
            source.Alpha = ExtractFontTextureAlpha(sourceTexture, sourceEntry);
            context.Add(source);
            return context;
        }

        private static FontGlyphRepairContext TryCreateFontGlyphRepairContext(FontPatchPackage fontPackage, FileStream mpdStream, SqPackArchive globalArchive, bool fontOnly)
        {
            if (fontPackage == null || mpdStream == null)
            {
                return null;
            }

            FontGlyphRepairContext context = new FontGlyphRepairContext();
            if (fontOnly)
            {
                AddGlobalFontAtlasAllocator(context, globalArchive, Font1TexturePath);
                AddGlobalFontAtlasAllocator(context, globalArchive, Font2TexturePath);
            }
            else
            {
                AddFontAtlasAllocator(context, fontPackage, mpdStream, Font1TexturePath);
                AddFontAtlasAllocator(context, fontPackage, mpdStream, Font2TexturePath);
                AddFontAtlasAllocator(context, fontPackage, mpdStream, Font3TexturePath);
            }

            AddFontAtlasAllocator(context, fontPackage, mpdStream, FontKrnTexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby1TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby2TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby3TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby4TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby5TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby6TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby7TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, Font3TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, Font4TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, Font5TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, Font6TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, Font7TexturePath);

            if (context.AllocatorCount == 0)
            {
                return null;
            }

            MarkGlobalFontGlyphOccupancy(context, globalArchive);

            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                FontPayload payload = fontPackage.Payloads[i];
                string path = NormalizeGamePath(payload.FullPath);
                if (!ShouldProtectHangulGlyphSourceFdt(path))
                {
                    continue;
                }

                if (fontOnly && !ShouldMarkTtmpFontOccupancyForFontOnly(path))
                {
                    continue;
                }

                byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, path);
                byte[] fdt = SqPackArchive.UnpackStandardFile(packed);
                MarkFontGlyphOccupancy(context, path, fdt);
            }

            return context;
        }

        private static bool ShouldMarkTtmpFontOccupancyForFontOnly(string path)
        {
            string normalized = NormalizeGamePath(path);
            return normalized.IndexOf("/krnaxis_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void MarkGlobalFontGlyphOccupancy(FontGlyphRepairContext context, SqPackArchive globalArchive)
        {
            if (context == null || globalArchive == null)
            {
                return;
            }

            for (int i = 0; i < FontPaths.Length; i++)
            {
                string path = NormalizeGamePath(FontPaths[i]);
                if (!path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] fdt;
                try
                {
                    fdt = globalArchive.ReadFile(path);
                }
                catch
                {
                    continue;
                }

                MarkFontGlyphOccupancy(context, path, fdt);
            }
        }

        private static ProtectedHangulGlyphContext TryCreateProtectedHangulGlyphContext(FontPatchPackage fontPackage, FileStream mpdStream, SqPackArchive globalArchive)
        {
            if (fontPackage == null || mpdStream == null)
            {
                return null;
            }

            ProtectedHangulGlyphContext context = new ProtectedHangulGlyphContext();
            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenCells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                FontPayload payload = fontPackage.Payloads[i];
                string path = NormalizeGamePath(payload.FullPath);
                if (!path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, path);
                byte[] fdt = SqPackArchive.UnpackStandardFile(packed);
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    continue;
                }

                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    int entryOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                    uint utf8Value = Endian.ReadUInt32LE(fdt, entryOffset);
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(utf8Value, out codepoint) ||
                        codepoint < HangulFirst ||
                        codepoint > HangulLast)
                    {
                        continue;
                    }

                    FdtGlyphEntry entry = ReadFdtGlyphEntry(fdt, entryOffset);
                    string texturePath = ResolveFontTexturePath(path, entry.ImageIndex);
                    if (!ShouldProtectHangulTexturePath(texturePath) ||
                        entry.Width == 0 ||
                        entry.Height == 0)
                    {
                        continue;
                    }

                    byte[] sourceTexture;
                    if (!sourceTextures.TryGetValue(texturePath, out sourceTexture))
                    {
                        sourceTexture = TryLoadTtmpTexturePayload(fontPackage, mpdStream, texturePath);
                        if (sourceTexture == null)
                        {
                            continue;
                        }

                        sourceTextures.Add(texturePath, sourceTexture);
                    }

                    int padding = GetReservedFontGlyphTextureNeighborhoodPadding(path, texturePath, codepoint);
                    CleanAsciiTextureRegion sourceRegion = ExtractFontTextureAlphaRegion(
                        sourceTexture,
                        entry,
                        padding);
                    if (!sourceRegion.IsValid)
                    {
                        continue;
                    }

                    string cellKey = texturePath + "|" + (entry.ImageIndex % 4).ToString() + "|" +
                                     (entry.X - sourceRegion.LeftPadding).ToString() + "|" +
                                     (entry.Y - sourceRegion.TopPadding).ToString() + "|" +
                                     sourceRegion.Width.ToString() + "|" + sourceRegion.Height.ToString();
                    if (seenCells.Contains(cellKey))
                    {
                        continue;
                    }

                    FontTexturePatch patch = new FontTexturePatch();
                    patch.TargetX = entry.X - sourceRegion.LeftPadding;
                    patch.TargetY = entry.Y - sourceRegion.TopPadding;
                    patch.TargetChannel = entry.ImageIndex % 4;
                    patch.ClearWidth = sourceRegion.Width;
                    patch.ClearHeight = sourceRegion.Height;
                    patch.SourceWidth = sourceRegion.Width;
                    patch.SourceHeight = sourceRegion.Height;
                    patch.SourceAlpha = sourceRegion.Alpha;
                    patch.SourceMipRegions = sourceRegion.MipRegions;
                    patch.SourceFdtPath = path;
                    patch.SourceCodepoint = codepoint;
                    context.AddPatch(texturePath, patch);
                    seenCells.Add(cellKey);
                }
            }

            return context.PatchCount == 0 ? null : context;
        }

        private static ProtectedHangulGlyphContext TryCreateFontOnlyProtectedCleanGlyphContext(SqPackArchive globalArchive)
        {
            return null;
        }

        private static void AddProtectedCleanGlyphs(
            ProtectedHangulGlyphContext context,
            SqPackArchive globalArchive,
            HashSet<string> seenCells,
            string fdtPath,
            bool asciiOnly,
            int padding)
        {
            if (context == null || globalArchive == null || seenCells == null || string.IsNullOrEmpty(fdtPath))
            {
                return;
            }

            byte[] fdt;
            try
            {
                fdt = globalArchive.ReadFile(fdtPath);
            }
            catch
            {
                return;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return;
            }

            Dictionary<string, byte[]> sourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                int entryOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                uint utf8Value = Endian.ReadUInt32LE(fdt, entryOffset);
                uint codepoint;
                if (!TryDecodeFdtUtf8Value(utf8Value, out codepoint) ||
                    (asciiOnly && (codepoint < 0x20u || codepoint > 0x7Eu)))
                {
                    continue;
                }

                FdtGlyphEntry entry = ReadFdtGlyphEntry(fdt, entryOffset);
                string texturePath = ResolveFontTexturePath(fdtPath, entry.ImageIndex);
                if (!IsGlobalFontTexturePath(texturePath) ||
                    entry.Width == 0 ||
                    entry.Height == 0)
                {
                    continue;
                }

                string cellKey = texturePath + "|" + (entry.ImageIndex % 4).ToString() + "|" +
                                 entry.X.ToString() + "|" + entry.Y.ToString() + "|" +
                                 entry.Width.ToString() + "|" + entry.Height.ToString();
                if (seenCells.Contains(cellKey))
                {
                    continue;
                }

                byte[] sourceTexture;
                if (!sourceTextures.TryGetValue(texturePath, out sourceTexture))
                {
                    if (!TryReadCachedRawTexture(globalArchive, sourceTextures, texturePath, out sourceTexture))
                    {
                        continue;
                    }
                }

                CleanAsciiTextureRegion sourceRegion = ExtractFontTextureAlphaRegion(
                    sourceTexture,
                    entry,
                    padding);
                if (!sourceRegion.IsValid)
                {
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = entry.X - sourceRegion.LeftPadding;
                patch.TargetY = entry.Y - sourceRegion.TopPadding;
                patch.TargetChannel = entry.ImageIndex % 4;
                patch.ClearWidth = sourceRegion.Width;
                patch.ClearHeight = sourceRegion.Height;
                patch.SourceWidth = sourceRegion.Width;
                patch.SourceHeight = sourceRegion.Height;
                patch.SourceAlpha = sourceRegion.Alpha;
                patch.SourceMipRegions = sourceRegion.MipRegions;
                patch.SourceFdtPath = NormalizeGamePath(fdtPath);
                patch.SourceCodepoint = codepoint;
                context.AddPatch(texturePath, patch);
                seenCells.Add(cellKey);
            }
        }

        private static bool ShouldProtectHangulTexturePath(string texturePath)
        {
            // Clean ASCII/numeric repair may touch shared global font atlas
            // cells used by in-game Korean text. Protect every Hangul cell that overlaps a
            // later repair instead of whitelisting individual syllables.
            // "호" or "혼".
            return IsGlobalFontTexturePath(texturePath);
        }

        private static bool ShouldProtectHangulGlyphSourceFdt(string path)
        {
            string normalized = NormalizeGamePath(path);
            return normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static int AppendProtectedHangulGlyphTexturePatches(
            string texturePath,
            List<FontTexturePatch> pendingPatches,
            ProtectedHangulGlyphContext context)
        {
            if (context == null || pendingPatches == null || pendingPatches.Count == 0)
            {
                return 0;
            }

            List<FontTexturePatch> protectedPatches;
            if (!context.TryGetPatches(texturePath, out protectedPatches))
            {
                return 0;
            }

            int originalPendingCount = pendingPatches.Count;
            int appended = 0;
            for (int i = 0; i < protectedPatches.Count; i++)
            {
                FontTexturePatch protectedPatch = protectedPatches[i];
                if (IsProtectedCleanAsciiTexturePatch(protectedPatch))
                {
                    pendingPatches.Add(protectedPatch);
                    appended++;
                    continue;
                }

                bool overlapsAnyPatch = false;
                bool overlapsCriticalAsciiPatch = false;
                for (int pendingIndex = 0; pendingIndex < originalPendingCount; pendingIndex++)
                {
                    FontTexturePatch pendingPatch = pendingPatches[pendingIndex];
                    if (!OverlapsTexturePatch(protectedPatch, pendingPatch))
                    {
                        continue;
                    }

                    overlapsAnyPatch = true;
                    if (IsCriticalAsciiTexturePatch(pendingPatch))
                    {
                        overlapsCriticalAsciiPatch = true;
                    }

                    break;
                }

                if (overlapsAnyPatch && (!overlapsCriticalAsciiPatch || IsProtectedCleanAsciiTexturePatch(protectedPatch)))
                {
                    pendingPatches.Add(protectedPatch);
                    appended++;
                }
            }

            return appended;
        }

        private static bool IsCriticalAsciiTexturePatch(FontTexturePatch patch)
        {
            return patch != null &&
                   patch.SourceCodepoint >= 0x20 &&
                   patch.SourceCodepoint <= 0x7E;
        }

        private static bool IsProtectedCleanAsciiTexturePatch(FontTexturePatch patch)
        {
            return patch != null &&
                   patch.SourceCodepoint >= 0x20u &&
                   patch.SourceCodepoint <= 0x7Eu;
        }

        private static bool IsCleanDamageNumberFontPath(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, "common/font/Jupiter_45.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/Jupiter_90.fdt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool OverlapsTexturePatch(FontTexturePatch left, FontTexturePatch right)
        {
            if (left.TargetChannel != right.TargetChannel)
            {
                return false;
            }

            int leftRight = left.TargetX + Math.Max(left.ClearWidth, left.SourceWidth);
            int leftBottom = left.TargetY + Math.Max(left.ClearHeight, left.SourceHeight);
            int rightRight = right.TargetX + Math.Max(right.ClearWidth, right.SourceWidth);
            int rightBottom = right.TargetY + Math.Max(right.ClearHeight, right.SourceHeight);
            return left.TargetX < rightRight &&
                   leftRight > right.TargetX &&
                   left.TargetY < rightBottom &&
                   leftBottom > right.TargetY;
        }

        private static void AddFontAtlasAllocator(FontGlyphRepairContext context, FontPatchPackage fontPackage, FileStream mpdStream, string texturePath)
        {
            byte[] rawTexture = TryLoadTtmpTexturePayload(fontPackage, mpdStream, texturePath);
            if (rawTexture == null)
            {
                return;
            }

            context.AddAllocator(texturePath, new FontAtlasAllocator(rawTexture));
        }

        private static void AddGlobalFontAtlasAllocator(FontGlyphRepairContext context, SqPackArchive globalArchive, string texturePath)
        {
            if (context == null || globalArchive == null || string.IsNullOrEmpty(texturePath))
            {
                return;
            }

            FontAtlasAllocator existing;
            if (context.TryGetAllocator(texturePath, out existing))
            {
                return;
            }

            byte[] packedTexture;
            if (!globalArchive.TryReadPackedFile(texturePath, out packedTexture))
            {
                return;
            }

            List<TextureSubBlock> ignored;
            byte[] rawTexture = UnpackTextureFile(packedTexture, out ignored);
            context.AddAllocator(texturePath, new FontAtlasAllocator(rawTexture));
        }

        private static void MarkFontGlyphOccupancy(FontGlyphRepairContext context, string fdtPath, byte[] fdt)
        {
            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return;
            }

            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                uint codepoint;
                bool hasCodepoint = TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(fdt, offset), out codepoint);
                FdtGlyphEntry entry = ReadFdtGlyphEntry(fdt, offset);
                string texturePath = ResolveFontTexturePath(fdtPath, entry.ImageIndex);
                FontAtlasAllocator allocator;
                if (texturePath == null || !context.TryGetAllocator(texturePath, out allocator))
                {
                    continue;
                }

                int padding = GetReservedFontGlyphTextureNeighborhoodPadding(
                    fdtPath,
                    texturePath,
                    hasCodepoint ? codepoint : 0);
                allocator.MarkOccupied(
                    entry.X - padding,
                    entry.Y - padding,
                    entry.Width + padding * 2,
                    entry.Height + padding * 2,
                    entry.ImageIndex % 4);
            }
        }

        private static int GetReservedFontGlyphTextureNeighborhoodPadding(string fdtPath, string texturePath, uint codepoint)
        {
            if (IsLobbyFontPath(fdtPath) && IsLobbyFontTexturePath(texturePath))
            {
                return LobbyGlyphTextureNeighborhoodPadding;
            }

            if (ShouldPreserveTtmpCombatFlyTextGlyphs(fdtPath) &&
                IsGlobalFontTexturePath(texturePath))
            {
                return InGameGlyphTextureNeighborhoodPadding;
            }

            if (IsCleanDamageNumberFontPath(fdtPath) &&
                IsGlobalFontTexturePath(texturePath))
            {
                return DamageNumberGlyphTextureNeighborhoodPadding;
            }

            if (!IsLobbyFontPath(fdtPath) &&
                IsGlobalFontTexturePath(texturePath) &&
                IsHangulCodepoint(codepoint))
            {
                return InGameGlyphTextureNeighborhoodPadding;
            }

            return 0;
        }

        private static byte[] TryLoadTtmpTexturePayload(FontPatchPackage fontPackage, FileStream mpdStream, string gamePath)
        {
            string normalizedPath = NormalizeGamePath(gamePath);
            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                FontPayload payload = fontPackage.Payloads[i];
                if (!string.Equals(NormalizeGamePath(payload.FullPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, normalizedPath);
                List<TextureSubBlock> ignored;
                return UnpackTextureFile(packed, out ignored);
            }

            return null;
        }

        private static byte[] TryLoadTtmpTexturePayload(Dictionary<string, FontPayload> payloadsByPath, FileStream mpdStream, string gamePath)
        {
            string normalizedPath = NormalizeGamePath(gamePath);
            FontPayload payload;
            if (!payloadsByPath.TryGetValue(normalizedPath, out payload))
            {
                return null;
            }

            byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, normalizedPath);
            List<TextureSubBlock> ignored;
            return UnpackTextureFile(packed, out ignored);
        }

        private static byte[] PatchPackedFontTexture(string texturePath, byte[] packedFile, List<FontTexturePatch> patches)
        {
            List<TextureSubBlock> subBlocks;
            byte[] rawTexture = UnpackTextureFile(packedFile, out subBlocks);
            rawTexture = EnsureFontTextureCapacityForPatches(texturePath, rawTexture, patches);
            ApplyFontTexturePatches(rawTexture, patches);
            return PackTextureFile(rawTexture);
        }

        private static byte[] EnsureFontTextureCapacityForPatches(string texturePath, byte[] rawTexture, List<FontTexturePatch> patches)
        {
            if (patches == null || patches.Count == 0)
            {
                return rawTexture;
            }

            FontTexture texture = ReadFontTexture(rawTexture);
            int requiredWidth = texture.Width;
            int requiredHeight = texture.Height;
            for (int i = 0; i < patches.Count; i++)
            {
                FontTexturePatch patch = patches[i];
                requiredWidth = Math.Max(requiredWidth, patch.TargetX + Math.Max(patch.ClearWidth, patch.SourceWidth));
                requiredHeight = Math.Max(requiredHeight, patch.TargetY + Math.Max(patch.ClearHeight, patch.SourceHeight));
            }

            if (requiredWidth <= texture.Width && requiredHeight <= texture.Height)
            {
                return rawTexture;
            }

            throw new InvalidOperationException(
                "Font texture patch would resize " + NormalizeGamePath(texturePath) +
                " from " + texture.Width.ToString() + "x" + texture.Height.ToString() +
                " to at least " + requiredWidth.ToString() + "x" + requiredHeight.ToString() +
                ". Offender: " + DescribeFirstOutOfBoundsTexturePatch(texture, patches) +
                ". Treat this as a missing texture page route, not an atlas expansion.");
        }

        private static string DescribeFirstOutOfBoundsTexturePatch(FontTexture texture, List<FontTexturePatch> patches)
        {
            if (patches == null)
            {
                return "none";
            }

            for (int i = 0; i < patches.Count; i++)
            {
                FontTexturePatch patch = patches[i];
                int width = Math.Max(patch.ClearWidth, patch.SourceWidth);
                int height = Math.Max(patch.ClearHeight, patch.SourceHeight);
                if (patch.TargetX < 0 ||
                    patch.TargetY < 0 ||
                    patch.TargetX + width > texture.Width ||
                    patch.TargetY + height > texture.Height)
                {
                    return "index=" + i.ToString() +
                           ", source=" + (patch.SourceFdtPath == null ? "(unknown)" : NormalizeGamePath(patch.SourceFdtPath)) +
                           ", codepoint=U+" + patch.SourceCodepoint.ToString("X4") +
                           ", xy=" + patch.TargetX.ToString() + "," + patch.TargetY.ToString() +
                           ", clear=" + patch.ClearWidth.ToString() + "x" + patch.ClearHeight.ToString() +
                           ", sourceSize=" + patch.SourceWidth.ToString() + "x" + patch.SourceHeight.ToString();
                }
            }

            return "not found";
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value)
            {
                result = checked(result * 2);
            }

            return result;
        }

        private static byte[] PackTextureFile(byte[] rawTexture)
        {
            if (rawTexture == null || rawTexture.Length < 0x20)
            {
                throw new ArgumentException("Raw texture must not be empty.");
            }

            int textureHeaderSize = checked((int)Endian.ReadUInt32LE(rawTexture, 0x1C));
            if (textureHeaderSize <= 0 || textureHeaderSize > rawTexture.Length)
            {
                throw new InvalidDataException("Invalid texture header size.");
            }

            const int maxBlockSize = 16000;
            int rawPayloadSize = rawTexture.Length - textureHeaderSize;
            int subBlockCount = Math.Max(1, (rawPayloadSize + maxBlockSize - 1) / maxBlockSize);
            int fileHeaderSize = Align(24 + 20 + subBlockCount * 2, 128);

            List<TextureSubBlockPayload> payloads = new List<TextureSubBlockPayload>();
            int sourceOffset = textureHeaderSize;
            int totalStoredSize = 0;
            for (int i = 0; i < subBlockCount; i++)
            {
                int length = Math.Min(maxBlockSize, rawTexture.Length - sourceOffset);
                byte[] compressed = Deflate(rawTexture, sourceOffset, length);
                TextureSubBlockPayload payload = new TextureSubBlockPayload();
                payload.RawOffset = sourceOffset;
                payload.RawLength = length;
                payload.Payload = compressed.Length < length ? compressed : null;
                payload.StoredSize = Align(16 + (payload.Payload == null ? length : payload.Payload.Length), 128);
                payloads.Add(payload);
                totalStoredSize += payload.StoredSize;
                sourceOffset += length;
            }

            int fileLength = fileHeaderSize + textureHeaderSize + totalStoredSize;
            byte[] packed = new byte[fileLength];
            Endian.WriteUInt32LE(packed, 0, checked((uint)fileHeaderSize));
            Endian.WriteUInt32LE(packed, 4, 4);
            Endian.WriteUInt32LE(packed, 8, checked((uint)rawTexture.Length));
            Endian.WriteUInt32LE(packed, 12, 0);
            Endian.WriteUInt32LE(packed, 16, checked((uint)(fileLength / 128)));
            Endian.WriteUInt32LE(packed, 20, 1);

            Endian.WriteUInt32LE(packed, 24, checked((uint)textureHeaderSize));
            Endian.WriteUInt32LE(packed, 28, checked((uint)(rawPayloadSize + subBlockCount * 16)));
            Endian.WriteUInt32LE(packed, 32, checked((uint)rawPayloadSize));
            Endian.WriteUInt32LE(packed, 36, 0);
            Endian.WriteUInt32LE(packed, 40, checked((uint)subBlockCount));
            int subBlockSizeOffset = 44;
            for (int i = 0; i < payloads.Count; i++)
            {
                Endian.WriteUInt16LE(packed, subBlockSizeOffset + i * 2, checked((ushort)payloads[i].StoredSize));
            }

            Buffer.BlockCopy(rawTexture, 0, packed, fileHeaderSize, textureHeaderSize);
            int packedOffset = fileHeaderSize + textureHeaderSize;
            for (int i = 0; i < payloads.Count; i++)
            {
                TextureSubBlockPayload payload = payloads[i];
                Endian.WriteUInt32LE(packed, packedOffset, 16);
                Endian.WriteUInt32LE(packed, packedOffset + 4, 0);
                Endian.WriteUInt32LE(
                    packed,
                    packedOffset + 8,
                    payload.Payload == null ? DatBlockTypes.Uncompressed : checked((uint)payload.Payload.Length));
                Endian.WriteUInt32LE(packed, packedOffset + 12, checked((uint)payload.RawLength));

                if (payload.Payload == null)
                {
                    Buffer.BlockCopy(rawTexture, payload.RawOffset, packed, packedOffset + 16, payload.RawLength);
                }
                else
                {
                    Buffer.BlockCopy(payload.Payload, 0, packed, packedOffset + 16, payload.Payload.Length);
                }

                packedOffset += payload.StoredSize;
            }

            return packed;
        }

        private static byte[] UnpackTextureFile(byte[] packedFile, out List<TextureSubBlock> subBlocks)
        {
            if (packedFile == null || packedFile.Length < 24)
            {
                throw new InvalidDataException("Packed texture is too short.");
            }

            uint headerSize = Endian.ReadUInt32LE(packedFile, 0);
            uint fileType = Endian.ReadUInt32LE(packedFile, 4);
            uint decompressedSize = Endian.ReadUInt32LE(packedFile, 8);
            uint blockCount = Endian.ReadUInt32LE(packedFile, 20);
            if (fileType != 4)
            {
                throw new InvalidDataException("Only texture SqPack files are supported. Type=" + fileType);
            }

            int locatorOffset = 24;
            int subBlockSizeOffset = checked(locatorOffset + (int)blockCount * 20);
            MemoryStream output = new MemoryStream((int)decompressedSize);
            subBlocks = new List<TextureSubBlock>();

            using (MemoryStream packedStream = new MemoryStream(packedFile, false))
            using (BinaryReader reader = new BinaryReader(packedStream))
            {
                for (int i = 0; i < blockCount; i++)
                {
                    int locator = locatorOffset + i * 20;
                    uint firstBlockOffset = Endian.ReadUInt32LE(packedFile, locator);
                    uint decompressedBlockSize = Endian.ReadUInt32LE(packedFile, locator + 8);
                    uint firstSubBlockIndex = Endian.ReadUInt32LE(packedFile, locator + 12);
                    uint subBlockCount = Endian.ReadUInt32LE(packedFile, locator + 16);

                    if (i == 0)
                    {
                        int textureHeaderStart = checked((int)headerSize);
                        output.Write(packedFile, textureHeaderStart, checked((int)firstBlockOffset));
                    }

                    int blockOffset = checked((int)headerSize + (int)firstBlockOffset);
                    long before = output.Length;
                    for (int s = 0; s < subBlockCount; s++)
                    {
                        int blockHeaderOffset = blockOffset;
                        uint blockHeaderSize = Endian.ReadUInt32LE(packedFile, blockHeaderOffset);
                        uint compressedSize = Endian.ReadUInt32LE(packedFile, blockHeaderOffset + 8);
                        uint rawSize = Endian.ReadUInt32LE(packedFile, blockHeaderOffset + 12);
                        if (blockHeaderSize != 16)
                        {
                            throw new InvalidDataException("Unexpected texture block header size.");
                        }

                        int rawOffset = checked((int)output.Length);
                        if (compressedSize == DatBlockTypes.Uncompressed)
                        {
                            output.Write(packedFile, blockHeaderOffset + 16, checked((int)rawSize));
                        }
                        else
                        {
                            packedStream.Position = blockHeaderOffset + 16;
                            using (DeflateStream deflate = new DeflateStream(packedStream, CompressionMode.Decompress, true))
                            {
                                byte[] inflated = new byte[rawSize];
                                int totalRead = 0;
                                while (totalRead < inflated.Length)
                                {
                                    int read = deflate.Read(inflated, totalRead, inflated.Length - totalRead);
                                    if (read == 0)
                                    {
                                        break;
                                    }

                                    totalRead += read;
                                }

                                if (totalRead != inflated.Length)
                                {
                                    throw new InvalidDataException("Failed to inflate texture sub-block.");
                                }

                                output.Write(inflated, 0, inflated.Length);
                            }
                        }

                        ushort paddedBlockSize = Endian.ReadUInt16LE(packedFile, subBlockSizeOffset + checked(((int)firstSubBlockIndex + s) * 2));
                        TextureSubBlock block = new TextureSubBlock();
                        block.PackedOffset = blockHeaderOffset;
                        block.PaddedSize = paddedBlockSize;
                        block.RawOffset = rawOffset;
                        block.RawLength = checked((int)rawSize);
                        subBlocks.Add(block);
                        blockOffset += paddedBlockSize;
                    }

                    if (output.Length - before != decompressedBlockSize)
                    {
                        throw new InvalidDataException("Unexpected texture block size.");
                    }
                }
            }

            byte[] result = output.ToArray();
            if (result.Length != decompressedSize)
            {
                throw new InvalidDataException("Unexpected texture size.");
            }

            return result;
        }

        private static FontTexture ReadFontTexture(byte[] rawTexture)
        {
            if (rawTexture == null || rawTexture.Length < 0x20)
            {
                throw new InvalidDataException("Raw font texture is too short.");
            }

            uint format = Endian.ReadUInt32LE(rawTexture, 4);
            if (format != 0x1440)
            {
                throw new InvalidDataException("Only A4R4G4B4 font textures are supported. Format=0x" + format.ToString("X"));
            }

            FontTexture texture = new FontTexture();
            texture.Raw = rawTexture;
            texture.Width = Endian.ReadUInt16LE(rawTexture, 8);
            texture.Height = Endian.ReadUInt16LE(rawTexture, 10);
            int mipmapCount = Endian.ReadUInt16LE(rawTexture, 0x0E);
            if (mipmapCount <= 0)
            {
                mipmapCount = 1;
            }

            if (0x1C + mipmapCount * 4 > rawTexture.Length)
            {
                mipmapCount = 1;
            }

            texture.MipmapCount = mipmapCount;
            texture.MipmapOffsets = new int[mipmapCount];
            for (int i = 0; i < mipmapCount; i++)
            {
                int offset = checked((int)Endian.ReadUInt32LE(rawTexture, 0x1C + i * 4));
                texture.MipmapOffsets[i] = offset;
            }

            texture.DataOffset = checked((int)Endian.ReadUInt32LE(rawTexture, 0x1C));
            int expected = checked(texture.Width * texture.Height * 2);
            if (texture.DataOffset < 0 || texture.DataOffset + expected > rawTexture.Length)
            {
                throw new InvalidDataException("Invalid font texture payload size.");
            }

            return texture;
        }

        private static byte[] ExtractFontTextureAlpha(byte[] rawTexture, FdtGlyphEntry glyph)
        {
            FontTexture texture = ReadFontTexture(rawTexture);
            byte[] alpha = new byte[glyph.Width * glyph.Height];
            int p = 0;
            int channel = glyph.ImageIndex % 4;
            for (int y = 0; y < glyph.Height; y++)
            {
                int sourceY = glyph.Y + y;
                for (int x = 0; x < glyph.Width; x++)
                {
                    int sourceX = glyph.X + x;
                    alpha[p++] = (byte)(GetFontTextureChannel(texture, sourceX, sourceY, channel) * 17);
                }
            }

            return alpha;
        }

        private static bool TryReadCachedRawTexture(SqPackArchive archive, Dictionary<string, byte[]> cache, string texturePath, out byte[] rawTexture)
        {
            rawTexture = null;
            if (cache.TryGetValue(texturePath, out rawTexture))
            {
                return true;
            }

            byte[] packedTexture;
            if (!archive.TryReadPackedFile(texturePath, out packedTexture))
            {
                return false;
            }

            List<TextureSubBlock> ignored;
            rawTexture = UnpackTextureFile(packedTexture, out ignored);
            cache.Add(texturePath, rawTexture);
            return true;
        }

        private static CleanAsciiTextureRegion ExtractFontTextureAlphaRegion(byte[] rawTexture, FdtGlyphEntry glyph, int padding)
        {
            FontTexture texture = ReadFontTexture(rawTexture);
            int width = glyph.Width + padding * 2;
            int height = glyph.Height + padding * 2;
            if (width <= 0 || height <= 0)
            {
                return new CleanAsciiTextureRegion();
            }

            byte[] alpha = new byte[width * height];
            int p = 0;
            int channel = glyph.ImageIndex % 4;
            for (int y = -padding; y < glyph.Height + padding; y++)
            {
                int sourceY = glyph.Y + y;
                for (int x = -padding; x < glyph.Width + padding; x++)
                {
                    int sourceX = glyph.X + x;
                    alpha[p++] = (byte)(GetFontTextureChannel(texture, sourceX, sourceY, channel) * 17);
                }
            }

            CleanAsciiTextureRegion region = new CleanAsciiTextureRegion();
            region.Alpha = alpha;
            region.Width = width;
            region.Height = height;
            region.LeftPadding = padding;
            region.TopPadding = padding;
            region.MipRegions = ExtractFontTextureMipAlphaRegions(texture, glyph, padding);
            return region;
        }

        private static CleanAsciiTextureMipRegion[] ExtractFontTextureMipAlphaRegions(FontTexture texture, FdtGlyphEntry glyph, int padding)
        {
            if (texture == null || texture.MipmapCount <= 1)
            {
                return null;
            }

            List<CleanAsciiTextureMipRegion> regions = new List<CleanAsciiTextureMipRegion>();
            int channel = glyph.ImageIndex % 4;
            int baseLeft = glyph.X - padding;
            int baseTop = glyph.Y - padding;
            int baseRight = glyph.X + glyph.Width + padding;
            int baseBottom = glyph.Y + glyph.Height + padding;
            for (int level = 1; level < texture.MipmapCount; level++)
            {
                if (!IsFontTextureMipAvailable(texture, level))
                {
                    continue;
                }

                int x0 = FloorDivPow2(baseLeft, level);
                int y0 = FloorDivPow2(baseTop, level);
                int x1 = CeilDivPow2(baseRight, level);
                int y1 = CeilDivPow2(baseBottom, level);
                int width = Math.Max(1, x1 - x0);
                int height = Math.Max(1, y1 - y0);
                byte[] alpha = new byte[checked(width * height)];
                int p = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        alpha[p++] = (byte)(GetFontTextureChannel(texture, x0 + x, y0 + y, channel, level) * 17);
                    }
                }

                CleanAsciiTextureMipRegion region = new CleanAsciiTextureMipRegion();
                region.Level = level;
                region.TargetX = x0;
                region.TargetY = y0;
                region.Width = width;
                region.Height = height;
                region.Alpha = alpha;
                regions.Add(region);
            }

            return regions.Count == 0 ? null : regions.ToArray();
        }

        private static FontTexturePatch CreateCleanAsciiTexturePatch(
            int targetX,
            int targetY,
            int targetChannel,
            CleanAsciiTextureRegion sourceRegion,
            int clearWidth,
            int clearHeight,
            string sourceFdtPath,
            uint sourceCodepoint)
        {
            FontTexturePatch patch = new FontTexturePatch();
            patch.TargetX = targetX;
            patch.TargetY = targetY;
            patch.TargetChannel = targetChannel;
            patch.ClearWidth = clearWidth;
            patch.ClearHeight = clearHeight;
            patch.SourceWidth = sourceRegion.Width;
            patch.SourceHeight = sourceRegion.Height;
            patch.SourceAlpha = sourceRegion.Alpha;
            patch.SourceMipRegions = sourceRegion.MipRegions;
            patch.SourceFdtPath = sourceFdtPath;
            patch.SourceCodepoint = sourceCodepoint;
            return patch;
        }

        private static void ApplyFontTexturePatches(byte[] rawTexture, List<FontTexturePatch> patches)
        {
            FontTexture texture = ReadFontTexture(rawTexture);
            for (int i = 0; i < patches.Count; i++)
            {
                FontTexturePatch patch = patches[i];
                for (int y = 0; y < patch.ClearHeight; y++)
                {
                    for (int x = 0; x < patch.ClearWidth; x++)
                    {
                        SetFontTextureChannel(texture, patch.TargetX + x, patch.TargetY + y, patch.TargetChannel, 0);
                    }
                }

                int p = 0;
                for (int y = 0; y < patch.SourceHeight; y++)
                {
                    for (int x = 0; x < patch.SourceWidth; x++)
                    {
                        byte alpha = patch.SourceAlpha[p++];
                        SetFontTextureChannel(texture, patch.TargetX + x, patch.TargetY + y, patch.TargetChannel, (byte)(alpha / 17));
                    }
                }

                ApplyFontTextureMipPatches(texture, patch);
            }
        }

        private static void ApplyFontTextureMipPatches(FontTexture texture, FontTexturePatch patch)
        {
            if (texture == null || patch == null || patch.SourceMipRegions == null)
            {
                return;
            }

            for (int regionIndex = 0; regionIndex < patch.SourceMipRegions.Length; regionIndex++)
            {
                CleanAsciiTextureMipRegion region = patch.SourceMipRegions[regionIndex];
                if (region.Alpha == null ||
                    region.Width <= 0 ||
                    region.Height <= 0 ||
                    !IsFontTextureMipAvailable(texture, region.Level))
                {
                    continue;
                }

                int targetX = FloorDivPow2(patch.TargetX, region.Level);
                int targetY = FloorDivPow2(patch.TargetY, region.Level);
                for (int y = 0; y < region.Height; y++)
                {
                    for (int x = 0; x < region.Width; x++)
                    {
                        SetFontTextureChannel(texture, targetX + x, targetY + y, patch.TargetChannel, 0, region.Level);
                    }
                }

                int p = 0;
                for (int y = 0; y < region.Height; y++)
                {
                    for (int x = 0; x < region.Width; x++)
                    {
                        byte alpha = region.Alpha[p++];
                        SetFontTextureChannel(texture, targetX + x, targetY + y, patch.TargetChannel, (byte)(alpha / 17), region.Level);
                    }
                }
            }
        }

        private static byte GetFontTextureChannel(FontTexture texture, int x, int y, int channel)
        {
            return GetFontTextureChannel(texture, x, y, channel, 0);
        }

        private static byte GetFontTextureChannel(FontTexture texture, int x, int y, int channel, int mipLevel)
        {
            int mipWidth = GetFontTextureMipWidth(texture, mipLevel);
            int mipHeight = GetFontTextureMipHeight(texture, mipLevel);
            if (x < 0 || y < 0 || x >= mipWidth || y >= mipHeight)
            {
                return 0;
            }

            int offset = GetFontTextureMipOffset(texture, mipLevel) + (y * mipWidth + x) * 2;
            byte lo = texture.Raw[offset];
            byte hi = texture.Raw[offset + 1];
            switch (channel)
            {
                case 0: return (byte)(hi & 0x0F);
                case 1: return (byte)((lo >> 4) & 0x0F);
                case 2: return (byte)(lo & 0x0F);
                default: return (byte)((hi >> 4) & 0x0F);
            }
        }

        private static void SetFontTextureChannel(FontTexture texture, int x, int y, int channel, byte value)
        {
            SetFontTextureChannel(texture, x, y, channel, value, 0);
        }

        private static void SetFontTextureChannel(FontTexture texture, int x, int y, int channel, byte value, int mipLevel)
        {
            if (!IsFontTextureMipAvailable(texture, mipLevel))
            {
                return;
            }

            int mipWidth = GetFontTextureMipWidth(texture, mipLevel);
            int mipHeight = GetFontTextureMipHeight(texture, mipLevel);
            if (x < 0 || y < 0 || x >= mipWidth || y >= mipHeight)
            {
                return;
            }

            value = (byte)(value & 0x0F);
            int offset = GetFontTextureMipOffset(texture, mipLevel) + (y * mipWidth + x) * 2;
            byte lo = texture.Raw[offset];
            byte hi = texture.Raw[offset + 1];
            switch (channel)
            {
                case 0:
                    hi = (byte)((hi & 0xF0) | value);
                    break;
                case 1:
                    lo = (byte)((lo & 0x0F) | (value << 4));
                    break;
                case 2:
                    lo = (byte)((lo & 0xF0) | value);
                    break;
                default:
                    hi = (byte)((hi & 0x0F) | (value << 4));
                    break;
            }

            texture.Raw[offset] = lo;
            texture.Raw[offset + 1] = hi;
        }

        private static int GetFontTextureMipWidth(FontTexture texture, int mipLevel)
        {
            return Math.Max(1, texture.Width >> Math.Max(0, mipLevel));
        }

        private static int GetFontTextureMipHeight(FontTexture texture, int mipLevel)
        {
            return Math.Max(1, texture.Height >> Math.Max(0, mipLevel));
        }

        private static int GetFontTextureMipOffset(FontTexture texture, int mipLevel)
        {
            if (texture == null || texture.MipmapOffsets == null || mipLevel < 0 || mipLevel >= texture.MipmapOffsets.Length)
            {
                return texture == null ? 0 : texture.DataOffset;
            }

            return texture.MipmapOffsets[mipLevel];
        }

        private static bool IsFontTextureMipAvailable(FontTexture texture, int mipLevel)
        {
            if (texture == null || mipLevel < 0 || texture.MipmapOffsets == null || mipLevel >= texture.MipmapOffsets.Length)
            {
                return false;
            }

            int offset = texture.MipmapOffsets[mipLevel];
            int size = checked(GetFontTextureMipWidth(texture, mipLevel) * GetFontTextureMipHeight(texture, mipLevel) * 2);
            return offset >= 0 && size >= 0 && offset + size <= texture.Raw.Length;
        }

        private static int FloorDivPow2(int value, int shift)
        {
            if (shift <= 0)
            {
                return value;
            }

            int divisor = 1 << Math.Min(shift, 30);
            if (value >= 0)
            {
                return value / divisor;
            }

            return -(((-value) + divisor - 1) / divisor);
        }

        private static int CeilDivPow2(int value, int shift)
        {
            return -FloorDivPow2(-value, shift);
        }

        private static byte[] Deflate(byte[] data, int offset, int length)
        {
            MemoryStream memory = new MemoryStream();
            using (DeflateStream deflate = new DeflateStream(memory, CompressionLevel.Optimal, true))
            {
                deflate.Write(data, offset, length);
            }

            return memory.ToArray();
        }

        private static int Align(int value, int alignment)
        {
            int remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        private string ResolveBaseIndex(string currentGlobalIndex, string originalGlobalIndex)
        {
            string baseIndex;
            bool explicitBaseIndex = !string.IsNullOrEmpty(_options.BaseFontIndexPath);
            bool foundOrigIndex = File.Exists(originalGlobalIndex);

            if (explicitBaseIndex)
            {
                baseIndex = Path.GetFullPath(_options.BaseFontIndexPath);
                RequireFile(baseIndex);
            }
            else if (foundOrigIndex)
            {
                baseIndex = originalGlobalIndex;
            }
            else
            {
                baseIndex = currentGlobalIndex;
            }

            using (SqPackIndexFile probe = new SqPackIndexFile(baseIndex))
            {
                Dictionary<byte, int> counts = probe.CountEntriesByDataFile();
                int dat1Count = counts.ContainsKey(1) ? counts[1] : 0;
                if (dat1Count > 0)
                {
                    if (!_options.AllowPatchedGlobal)
                    {
                        throw new InvalidOperationException(
                            "The selected base 000000.win32.index already contains " + dat1Count +
                            " dat1 entries. Use a clean client, restore the original index, or pass --base-font-index <clean index>. " +
                            "Use --allow-patched-global only for experiments.");
                    }

                    Console.WriteLine("WARNING: selected base 000000.win32.index contains {0} dat1 entries. Experimental output only.", dat1Count);
                }
            }

            return baseIndex;
        }

        private string ResolveBaseIndex2(string currentGlobalIndex2, string originalGlobalIndex2)
        {
            string baseIndex2 = null;

            if (!string.IsNullOrEmpty(_options.BaseFontIndex2Path))
            {
                baseIndex2 = Path.GetFullPath(_options.BaseFontIndex2Path);
                RequireFile(baseIndex2);
            }
            else if (!string.IsNullOrEmpty(_options.BaseFontIndexPath))
            {
                string sibling = _options.BaseFontIndexPath.Trim('"') + "2";
                if (File.Exists(sibling))
                {
                    baseIndex2 = Path.GetFullPath(sibling);
                }
            }

            if (string.IsNullOrEmpty(baseIndex2) && File.Exists(originalGlobalIndex2))
            {
                baseIndex2 = originalGlobalIndex2;
            }

            if (string.IsNullOrEmpty(baseIndex2))
            {
                baseIndex2 = currentGlobalIndex2;
            }

            RequireFile(baseIndex2);
            using (SqPackIndex2File probe = new SqPackIndex2File(baseIndex2))
            {
                Dictionary<byte, int> counts = probe.CountEntriesByDataFile();
                int dat1Count = counts.ContainsKey(1) ? counts[1] : 0;
                if (dat1Count > 0)
                {
                    if (!_options.AllowPatchedGlobal)
                    {
                        throw new InvalidOperationException(
                            "The selected base 000000.win32.index2 already contains " + dat1Count +
                            " dat1 entries. Use a clean client, restore the original index2, or pass --base-font-index2 <clean index2>. " +
                            "Use --allow-patched-global only for experiments.");
                    }

                    Console.WriteLine("WARNING: selected base 000000.win32.index2 contains {0} dat1 entries. Experimental output only.", dat1Count);
                }
            }

            return baseIndex2;
        }

        private void AddLimitedWarning(string message)
        {
            if (_report.Warnings.Count < 30)
            {
                _report.Warnings.Add(message);
            }
        }

        private sealed class FontPatchPackage
        {
            public string DirectoryPath;
            public string MpdPath;
            public string MplPath;
            public List<FontPayload> Payloads;
        }

        private struct FontPayload
        {
            public string FullPath;
            public int ModOffset;
            public int ModSize;
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

        private struct AllocatedFontGlyphCell
        {
            public int ImageIndex;
            public int X;
            public int Y;
            public int Channel;
        }

        private sealed class TargetedGlyphRepairContext
        {
            private readonly Dictionary<uint, TargetedGlyphSource> _sources =
                new Dictionary<uint, TargetedGlyphSource>();

            public void Add(TargetedGlyphSource source)
            {
                _sources[source.Codepoint] = source;
            }

            public bool TryGet(uint codepoint, out TargetedGlyphSource source)
            {
                return _sources.TryGetValue(codepoint, out source);
            }
        }

        private sealed class TargetedGlyphSource
        {
            public uint Codepoint;
            public string SourceFdtPath;
            public int Width;
            public int Height;
            public sbyte OffsetX;
            public sbyte OffsetY;
            public byte[] Alpha;
        }

        private sealed class FontGlyphRepairContext
        {
            private readonly Dictionary<string, FontAtlasAllocator> _allocators =
                new Dictionary<string, FontAtlasAllocator>(StringComparer.OrdinalIgnoreCase);

            public int AllocatorCount
            {
                get { return _allocators.Count; }
            }

            public void AddAllocator(string texturePath, FontAtlasAllocator allocator)
            {
                _allocators[NormalizeGamePath(texturePath)] = allocator;
            }

            public bool TryGetAllocator(string texturePath, out FontAtlasAllocator allocator)
            {
                return _allocators.TryGetValue(NormalizeGamePath(texturePath), out allocator);
            }

            public bool TryAllocate(string texturePath, int width, int height, out AllocatedFontGlyphCell cell)
            {
                cell = new AllocatedFontGlyphCell();
                FontAtlasAllocator allocator;
                string normalized = NormalizeGamePath(texturePath);
                if (!_allocators.TryGetValue(normalized, out allocator))
                {
                    return false;
                }

                if (!allocator.TryAllocate(width, height, out cell))
                {
                    return false;
                }

                cell.ImageIndex = ResolveImageIndexForTexturePath(normalized, cell.Channel);
                return cell.ImageIndex >= 0;
            }

        }

        private sealed class LobbyHangulCodepointSets
        {
            public uint[] All;
            public uint[] SystemSettings;
            public uint[] CharacterSelect;
            public uint[] LargeLabels;
            public uint[] LargeCharacterLabels;
            public uint[] StartMainMenu;
            public uint[] SystemAndCharacter;
            public uint[] HighScale;
        }

        private sealed class LobbyHangulGlyphAllocationCache
        {
            private readonly Dictionary<string, LobbyHangulGlyphAllocation> _allocations =
                new Dictionary<string, LobbyHangulGlyphAllocation>(StringComparer.OrdinalIgnoreCase);

            public bool TryGet(string key, out LobbyHangulGlyphAllocation allocation)
            {
                if (string.IsNullOrEmpty(key))
                {
                    allocation = new LobbyHangulGlyphAllocation();
                    return false;
                }

                return _allocations.TryGetValue(key, out allocation);
            }

            public void Add(string key, LobbyHangulGlyphAllocation allocation)
            {
                if (!string.IsNullOrEmpty(key) && !_allocations.ContainsKey(key))
                {
                    _allocations.Add(key, allocation);
                }
            }
        }

        private struct LobbyHangulGlyphAllocation
        {
            public readonly string TexturePath;
            public readonly AllocatedFontGlyphCell Cell;
            public readonly int LeftPadding;
            public readonly int TopPadding;

            public LobbyHangulGlyphAllocation(
                string texturePath,
                AllocatedFontGlyphCell cell,
                int leftPadding,
                int topPadding)
            {
                TexturePath = texturePath;
                Cell = cell;
                LeftPadding = leftPadding;
                TopPadding = topPadding;
            }
        }

        private struct CleanAsciiTextureRegion
        {
            public byte[] Alpha;
            public int Width;
            public int Height;
            public int LeftPadding;
            public int TopPadding;
            public CleanAsciiTextureMipRegion[] MipRegions;

            public bool IsValid
            {
                get { return Alpha != null && Width > 0 && Height > 0; }
            }
        }

        private struct CleanAsciiTextureMipRegion
        {
            public int Level;
            public int TargetX;
            public int TargetY;
            public int Width;
            public int Height;
            public byte[] Alpha;
        }

        private sealed class ProtectedHangulGlyphContext
        {
            private readonly Dictionary<string, List<FontTexturePatch>> _patchesByTexturePath =
                new Dictionary<string, List<FontTexturePatch>>(StringComparer.OrdinalIgnoreCase);

            public int PatchCount { get; private set; }

            public void AddPatch(string texturePath, FontTexturePatch patch)
            {
                List<FontTexturePatch> patches;
                string normalizedPath = NormalizeGamePath(texturePath);
                if (!_patchesByTexturePath.TryGetValue(normalizedPath, out patches))
                {
                    patches = new List<FontTexturePatch>();
                    _patchesByTexturePath.Add(normalizedPath, patches);
                }

                patches.Add(patch);
                PatchCount++;
            }

            public bool TryGetPatches(string texturePath, out List<FontTexturePatch> patches)
            {
                return _patchesByTexturePath.TryGetValue(NormalizeGamePath(texturePath), out patches);
            }
        }

        private sealed class FontAtlasAllocator
        {
            private readonly FontTexture _texture;
            private readonly bool[][] _occupied;

            public int Width
            {
                get { return _texture.Width; }
            }

            public int Height
            {
                get { return _texture.Height; }
            }

            public FontAtlasAllocator(byte[] rawTexture)
            {
                _texture = ReadFontTexture(rawTexture);
                _occupied = new bool[4][];
                int pixels = checked(_texture.Width * _texture.Height);
                for (int channel = 0; channel < _occupied.Length; channel++)
                {
                    _occupied[channel] = new bool[pixels];
                }

                int p = 0;
                for (int y = 0; y < _texture.Height; y++)
                {
                    for (int x = 0; x < _texture.Width; x++)
                    {
                        int offset = _texture.DataOffset + p * 2;
                        byte lo = _texture.Raw[offset];
                        byte hi = _texture.Raw[offset + 1];
                        if ((hi & 0x0F) != 0)
                        {
                            _occupied[0][p] = true;
                        }

                        if ((lo & 0xF0) != 0)
                        {
                            _occupied[1][p] = true;
                        }

                        if ((lo & 0x0F) != 0)
                        {
                            _occupied[2][p] = true;
                        }

                        if ((hi & 0xF0) != 0)
                        {
                            _occupied[3][p] = true;
                        }

                        p++;
                    }
                }
            }

            public void MarkOccupied(int x, int y, int width, int height, int channel)
            {
                if (channel < 0 || channel >= _occupied.Length)
                {
                    return;
                }

                int left = Math.Max(0, x);
                int top = Math.Max(0, y);
                int right = Math.Min(_texture.Width, x + Math.Max(0, width));
                int bottom = Math.Min(_texture.Height, y + Math.Max(0, height));
                for (int yy = top; yy < bottom; yy++)
                {
                    int row = yy * _texture.Width;
                    for (int xx = left; xx < right; xx++)
                    {
                        _occupied[channel][row + xx] = true;
                    }
                }
            }

            public bool TryAllocate(int width, int height, out AllocatedFontGlyphCell cell)
            {
                cell = new AllocatedFontGlyphCell();
                int w = Math.Max(1, width);
                int h = Math.Max(1, height);
                int stepX = Math.Max(1, w + 1);
                int stepY = Math.Max(1, h + 1);
                for (int channel = 0; channel < _occupied.Length; channel++)
                {
                    for (int y = _texture.Height - h - 1; y >= 0; y -= stepY)
                    {
                        for (int x = 0; x <= _texture.Width - w; x += stepX)
                        {
                            if (!IsFree(x, y, w, h, channel))
                            {
                                continue;
                            }

                            MarkOccupied(x, y, w, h, channel);
                            cell.X = x;
                            cell.Y = y;
                            cell.Channel = channel;
                            return true;
                        }
                    }
                }

                return false;
            }

            private bool IsFree(int x, int y, int width, int height, int channel)
            {
                for (int yy = 0; yy < height; yy++)
                {
                    int row = (y + yy) * _texture.Width;
                    for (int xx = 0; xx < width; xx++)
                    {
                        if (_occupied[channel][row + x + xx])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        private sealed class FontTexture
        {
            public byte[] Raw;
            public int Width;
            public int Height;
            public int DataOffset;
            public int MipmapCount;
            public int[] MipmapOffsets;
        }

        private sealed class DerivedLobbyFontSpec
        {
            public readonly string TargetPath;
            public readonly string SourcePath;

            public DerivedLobbyFontSpec(string targetPath, string sourcePath)
            {
                TargetPath = targetPath;
                SourcePath = sourcePath;
            }
        }

        private sealed class LargeUiLabelVisualScaleSpec
        {
            public readonly string TargetFontPath;
            public readonly string SourceFontPath;
            public readonly string MetricFontPath;
            public readonly double HangulToDigitRatio;

            public LargeUiLabelVisualScaleSpec(string targetFontPath, string sourceFontPath, string metricFontPath)
            {
                TargetFontPath = NormalizeGamePath(targetFontPath);
                SourceFontPath = NormalizeGamePath(sourceFontPath);
                MetricFontPath = NormalizeGamePath(metricFontPath);
                HangulToDigitRatio = ActionDetailHighScaleHangulGlyphs.LargeUiHangulToDigitRatio;
            }
        }

        private sealed class LobbyLargeLabelVisualScaleSpec
        {
            public readonly string TargetFontPath;
            public readonly string SourceFontPath;
            public readonly string PlacementFontPath;
            public readonly double HangulToDigitRatio;
            public readonly bool UsePlacementCells;

            public LobbyLargeLabelVisualScaleSpec(string targetFontPath, string sourceFontPath, double hangulToDigitRatio)
                : this(targetFontPath, sourceFontPath, null, hangulToDigitRatio)
            {
            }

            public LobbyLargeLabelVisualScaleSpec(string targetFontPath, string sourceFontPath, string placementFontPath, double hangulToDigitRatio)
            {
                TargetFontPath = NormalizeGamePath(targetFontPath);
                SourceFontPath = NormalizeGamePath(sourceFontPath);
                PlacementFontPath = string.IsNullOrWhiteSpace(placementFontPath)
                    ? SourceFontPath
                    : NormalizeGamePath(placementFontPath);
                HangulToDigitRatio = hangulToDigitRatio;
                UsePlacementCells = !string.Equals(PlacementFontPath, SourceFontPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class FontTexturePatch
        {
            public int TargetX;
            public int TargetY;
            public int TargetChannel;
            public int ClearWidth;
            public int ClearHeight;
            public int SourceWidth;
            public int SourceHeight;
            public byte[] SourceAlpha;
            public CleanAsciiTextureMipRegion[] SourceMipRegions;
            public string SourceFdtPath;
            public uint SourceCodepoint;
        }

        private struct TextureSubBlock
        {
            public int PackedOffset;
            public int PaddedSize;
            public int RawOffset;
            public int RawLength;
        }

        private sealed class TextureSubBlockPayload
        {
            public int RawOffset;
            public int RawLength;
            public byte[] Payload;
            public int StoredSize;
        }

    }

    internal static class FontPatchProfiles
    {
        public const string Full = "full";
        public const string UiNumericSafe = "ui-numeric-safe";
        public const string NoMiedingerMid = "no-miedingermid";
        public const string NoTrumpGothic = "no-trumpgothic";
        public const string Default = Full;
        public const string NoJupiter = "no-jupiter";
        public const string NoAxis = "no-axis";
        public const string FdtOnly = "fdt-only";
        public const string TexturesOnly = "textures-only";

        public static string Normalize(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case Full:
                case UiNumericSafe:
                case NoMiedingerMid:
                case NoJupiter:
                case NoAxis:
                case FdtOnly:
                case TexturesOnly:
                    return normalized;
                case NoTrumpGothic:
                    return Full;
                default:
                    throw new ArgumentException(
                        "Unsupported font profile: " + value +
                        ". Supported values: full, ui-numeric-safe, no-miedingermid, no-trumpgothic(alias of full), no-jupiter, no-axis, fdt-only, textures-only.");
            }
        }
    }

}
