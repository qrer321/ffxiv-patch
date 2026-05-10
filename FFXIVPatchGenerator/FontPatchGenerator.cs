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
        private const string LobbyHangulAliasSourcePath = "common/font/AXIS_12_lobby.fdt";
        private const string LobbyHangulAliasTargetPath = "common/font/AXIS_14_lobby.fdt";
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

        private static readonly DerivedLobbyFontSpec[] Derived4kLobbyFonts = new DerivedLobbyFontSpec[]
        {
            new DerivedLobbyFontSpec("common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36.fdt"),
            new DerivedLobbyFontSpec("common/font/Jupiter_46_lobby.fdt", "common/font/Jupiter_46.fdt"),
            new DerivedLobbyFontSpec("common/font/Jupiter_90_lobby.fdt", "common/font/Jupiter_46.fdt"),
            new DerivedLobbyFontSpec("common/font/Meidinger_40_lobby.fdt", "common/font/MiedingerMid_36.fdt"),
            new DerivedLobbyFontSpec("common/font/MiedingerMid_36_lobby.fdt", "common/font/MiedingerMid_36.fdt"),
            new DerivedLobbyFontSpec("common/font/TrumpGothic_68_lobby.fdt", "common/font/TrumpGothic_68.fdt")
        };

        private static readonly string[] Derived4kLobbyRequiredHangulPhrases = LobbyScaledHangulPhrases.All;

        private static readonly uint[] Derived4kLobbyRequiredPhraseCodepoints = CreatePhraseCodepoints(Derived4kLobbyRequiredHangulPhrases);
        private static readonly uint[] Derived4kLobbyRequiredHangulCodepoints = CreateHangulCodepoints(Derived4kLobbyRequiredHangulPhrases);

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
            uint[] derived4kLobbyRequiredPhraseCodepoints = CreateDerived4kLobbyRequiredPhraseCodepoints(koreaSqpack);

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
                    WriteTtmpFontFiles(fontPackage, globalArchive, mutableIndex, mutableIndex2, datWriter, derived4kLobbyRequiredPhraseCodepoints);
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
                long datOffset = WriteFontPayload(datWriter, path, packedFile, null, null, null, null, null, null, null, out normalized);
                LogFontPayloadAdjustments(path, normalized);
                mutableIndex.SetFileOffset(path, 1, datOffset);
                mutableIndex2.SetFileOffset(path, 1, datOffset);
                _report.FontFilesPatched++;
            }
        }

        private void WriteTtmpFontFiles(FontPatchPackage fontPackage, SqPackArchive globalArchive, SqPackIndexFile mutableIndex, SqPackIndex2File mutableIndex2, SqPackDatWriter datWriter, uint[] derived4kLobbyRequiredPhraseCodepoints)
        {
            using (FileStream mpdStream = new FileStream(fontPackage.MpdPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                LobbyHangulRepairContext lobbyHangulRepair = TryCreateLobbyHangulRepairContext(fontPackage, mpdStream);
                FontGlyphRepairContext glyphRepair = TryCreateFontGlyphRepairContext(fontPackage, mpdStream, globalArchive);
                TargetedGlyphRepairContext dialogueGlyphRepair = TryCreateDialogueGlyphArtifactRepairContext(fontPackage, mpdStream);
                ProtectedHangulGlyphContext protectedHangulGlyphs = TryCreateProtectedHangulGlyphContext(fontPackage, mpdStream);
                Dictionary<string, List<FontTexturePatch>> texturePatches = new Dictionary<string, List<FontTexturePatch>>(StringComparer.OrdinalIgnoreCase);
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

                HashSet<string> writtenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool derived4kLobbyFontsWritten = false;
                for (int i = 0; i < fontPackage.Payloads.Count; i++)
                {
                    FontPayload payload = fontPackage.Payloads[i];
                    string path = NormalizeGamePath(payload.FullPath);
                    ProgressReporter.Report(90 + i * 8 / fontPackage.Payloads.Count, "Font patching " + (i + 1).ToString() + "/" + fontPackage.Payloads.Count.ToString());
                    if (!derived4kLobbyFontsWritten && !path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteDerived4kLobbyFontFiles(mpdStream, globalArchive, mutableIndex, mutableIndex2, datWriter, payloadsByPath, writtenPaths, lobbyHangulRepair, dialogueGlyphRepair, glyphRepair, texturePatches, derived4kLobbyRequiredPhraseCodepoints);
                        derived4kLobbyFontsWritten = true;
                    }

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

                    byte[] packedFile = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, path);
                    int normalized;
                    long datOffset;
                    List<FontTexturePatch> pendingTexturePatches;
                    if (texturePatches.TryGetValue(path, out pendingTexturePatches) && pendingTexturePatches.Count > 0)
                    {
                        int protectedRestores = AppendProtectedHangulGlyphTexturePatches(path, pendingTexturePatches, protectedHangulGlyphs);
                        byte[] patchedPackedTexture = PatchPackedFontTexture(packedFile, pendingTexturePatches);
                        datOffset = datWriter.WritePackedFile(patchedPackedTexture);
                        normalized = 0;
                        if (protectedRestores > 0)
                        {
                            Console.WriteLine("  Restored protected Hangul glyph cells: {0} ({1})", protectedRestores, path);
                        }

                        Console.WriteLine("  Patched repaired font texture cells: {0} ({1})", pendingTexturePatches.Count, path);
                        pendingTexturePatches.Clear();
                    }
                    else
                    {
                        datOffset = WriteFontPayload(datWriter, path, packedFile, mpdStream, payloadsByPath, lobbyHangulRepair, dialogueGlyphRepair, glyphRepair, globalArchive, texturePatches, out normalized);
                    }

                    LogFontPayloadAdjustments(path, normalized);
                    mutableIndex.SetFileOffset(path, 1, datOffset);
                    mutableIndex2.SetFileOffset(path, 1, datOffset);
                    writtenPaths.Add(path);
                    _report.FontFilesPatched++;
                }

                if (!derived4kLobbyFontsWritten)
                {
                    WriteDerived4kLobbyFontFiles(mpdStream, globalArchive, mutableIndex, mutableIndex2, datWriter, payloadsByPath, writtenPaths, lobbyHangulRepair, dialogueGlyphRepair, glyphRepair, texturePatches, derived4kLobbyRequiredPhraseCodepoints);
                }

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

                        byte[] globalPackedTexture;
                        if (!globalArchive.TryReadPackedFile(pair.Key, out globalPackedTexture))
                        {
                            AddLimitedWarning("Pending font texture source was not found: " + pair.Key);
                            continue;
                        }

                        int protectedRestores = AppendProtectedHangulGlyphTexturePatches(pair.Key, pair.Value, protectedHangulGlyphs);
                        byte[] patchedPackedTexture = PatchPackedFontTexture(globalPackedTexture, pair.Value);
                        long datOffset = datWriter.WritePackedFile(patchedPackedTexture);
                        mutableIndex.SetFileOffset(pair.Key, 1, datOffset);
                        mutableIndex2.SetFileOffset(pair.Key, 1, datOffset);
                        _report.FontFilesPatched++;
                        if (protectedRestores > 0)
                        {
                            Console.WriteLine("  Restored protected Hangul glyph cells: {0} ({1})", protectedRestores, pair.Key);
                        }

                        Console.WriteLine("  Patched repaired global font texture cells: {0} ({1})", pair.Value.Count, pair.Key);
                        pair.Value.Clear();
                    }
                }
            }
        }

        private void WriteDerived4kLobbyFontFiles(
            FileStream mpdStream,
            SqPackArchive globalArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            Dictionary<string, FontPayload> payloadsByPath,
            HashSet<string> writtenPaths,
            LobbyHangulRepairContext lobbyHangulRepair,
            TargetedGlyphRepairContext dialogueGlyphRepair,
            FontGlyphRepairContext glyphRepair,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            uint[] requiredPhraseCodepoints)
        {
            for (int i = 0; i < Derived4kLobbyFonts.Length; i++)
            {
                string targetPath = NormalizeGamePath(Derived4kLobbyFonts[i].TargetPath);
                string sourcePath = NormalizeGamePath(Derived4kLobbyFonts[i].SourcePath);

                if (payloadsByPath.ContainsKey(targetPath) || writtenPaths.Contains(targetPath))
                {
                    continue;
                }

                if (!ShouldIncludeFontPath(targetPath))
                {
                    _report.FontFilesSkippedByProfile++;
                    continue;
                }

                if (!mutableIndex.ContainsPath(targetPath))
                {
                    AddLimitedWarning("4K lobby font target missing from index: " + targetPath);
                    continue;
                }

                if (!mutableIndex2.ContainsPath(targetPath))
                {
                    AddLimitedWarning("4K lobby font target missing from index2: " + targetPath);
                    continue;
                }

                FontPayload sourcePayload;
                if (!payloadsByPath.TryGetValue(sourcePath, out sourcePayload))
                {
                    AddLimitedWarning("4K lobby font source missing from TTMP package: " + sourcePath);
                    continue;
                }

                byte[] packedFile = ReadPackedPayload(mpdStream, sourcePayload.ModOffset, sourcePayload.ModSize, sourcePath);
                byte[] sourceFdt = SqPackArchive.UnpackStandardFile(packedFile);
                byte[] fdt;
                try
                {
                    fdt = globalArchive.ReadFile(targetPath);
                }
                catch (IOException)
                {
                    AddLimitedWarning("4K lobby font clean target missing: " + targetPath);
                    continue;
                }
                catch (InvalidDataException)
                {
                    AddLimitedWarning("4K lobby font clean target invalid: " + targetPath);
                    continue;
                }

                int derivedGlyphCells = QueueDerived4kLobbyPhraseTextureCells(
                    targetPath,
                    sourcePath,
                    sourceFdt,
                    ref fdt,
                    globalArchive,
                    mpdStream,
                    payloadsByPath,
                    glyphRepair,
                    texturePatches,
                    requiredPhraseCodepoints);
                int normalized;
                long datOffset = WritePreparedFontFdtPayload(datWriter, targetPath, fdt, null, derivedGlyphCells, mpdStream, payloadsByPath, lobbyHangulRepair, dialogueGlyphRepair, glyphRepair, globalArchive, texturePatches, out normalized);
                if (derivedGlyphCells > 0)
                {
                    Console.WriteLine("  Queued derived 4K lobby phrase glyph cells: {0} ({1})", derivedGlyphCells, targetPath);
                }

                LogFontPayloadAdjustments(targetPath, normalized);
                mutableIndex.SetFileOffset(targetPath, 1, datOffset);
                mutableIndex2.SetFileOffset(targetPath, 1, datOffset);
                writtenPaths.Add(targetPath);
                _report.FontFilesPatched++;
                Console.WriteLine("  Patched clean 4K lobby font FDT: {0} <= phrase glyphs from {1}", targetPath, sourcePath);
            }
        }

        private int QueueDerived4kLobbyPhraseTextureCells(
            string targetPath,
            string sourcePath,
            byte[] sourceFdt,
            ref byte[] targetFdt,
            SqPackArchive globalArchive,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            FontGlyphRepairContext glyphRepair,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            uint[] requiredPhraseCodepoints)
        {
            if (sourceFdt == null || targetFdt == null || glyphRepair == null || texturePatches == null)
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

            if (requiredPhraseCodepoints == null)
            {
                requiredPhraseCodepoints = new uint[0];
            }

            int glyphEnd = checked(glyphStart + checked((int)glyphCount) * FdtGlyphEntrySize);
            List<byte[]> targetEntries = new List<byte[]>(checked((int)glyphCount + requiredPhraseCodepoints.Length));
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

            Dictionary<uint, byte[]> hangulSourceEntries = ReadGlyphEntriesByUtf8Value(sourceFdt);
            Dictionary<uint, byte[]> cleanAsciiSourceEntries = new Dictionary<uint, byte[]>();
            Dictionary<uint, byte[]> cleanAsciiFallbackSourceEntries = new Dictionary<uint, byte[]>();
            byte[] cleanAsciiSourceFdt = null;
            string cleanAsciiSourcePath = ResolveDerivedLobbyCleanAsciiSourceFdtPath(targetPath);
            string cleanAsciiFallbackSourcePath = ResolveDerivedLobbyFallbackCleanAsciiSourceFdtPath(targetPath);
            if (globalArchive != null)
            {
                try
                {
                    cleanAsciiSourceFdt = globalArchive.ReadFile(cleanAsciiSourcePath);
                    cleanAsciiSourceEntries = ReadGlyphEntriesByUtf8Value(cleanAsciiSourceFdt);
                }
                catch (IOException)
                {
                    cleanAsciiSourceEntries = new Dictionary<uint, byte[]>();
                }
                catch (InvalidDataException)
                {
                    cleanAsciiSourceEntries = new Dictionary<uint, byte[]>();
                }

                if (!string.IsNullOrEmpty(cleanAsciiFallbackSourcePath) &&
                    !string.Equals(cleanAsciiFallbackSourcePath, cleanAsciiSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        byte[] cleanAsciiFallbackSourceFdt = globalArchive.ReadFile(cleanAsciiFallbackSourcePath);
                        cleanAsciiFallbackSourceEntries = ReadGlyphEntriesByUtf8Value(cleanAsciiFallbackSourceFdt);
                    }
                    catch (IOException)
                    {
                        cleanAsciiFallbackSourceEntries = new Dictionary<uint, byte[]>();
                    }
                    catch (InvalidDataException)
                    {
                        cleanAsciiFallbackSourceEntries = new Dictionary<uint, byte[]>();
                    }
                }
            }

            if (hangulSourceEntries.Count == 0 && cleanAsciiSourceEntries.Count == 0)
            {
                return 0;
            }

            int minimumCjkAdvance = ComputeMedianCjkAdvance(targetEntries);
            bool cleanAsciiSourceIsTarget = string.Equals(cleanAsciiSourcePath, NormalizeGamePath(targetPath), StringComparison.OrdinalIgnoreCase);
            Dictionary<string, byte[]> hangulSourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, byte[]> cleanAsciiSourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, byte[]> cleanAsciiFallbackSourceTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            int queued = 0;
            int missingTextures = 0;
            int allocationFailures = 0;
            int extractionFailures = 0;
            for (int requiredIndex = 0; requiredIndex < requiredPhraseCodepoints.Length; requiredIndex++)
            {
                uint codepoint = requiredPhraseCodepoints[requiredIndex];
                uint utf8Value = PackFdtUtf8Value(codepoint);
                bool ascii = codepoint > 0x20u && codepoint <= 0x7Eu;
                bool hangul = IsHangulCodepoint(codepoint);
                if (ascii && cleanAsciiSourceIsTarget && targetEntryIndexes.ContainsKey(utf8Value))
                {
                    continue;
                }

                byte[] sourceEntryBytes;
                string sourceFdtPath;
                if (ascii)
                {
                    if (!cleanAsciiSourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                    {
                        if (!cleanAsciiFallbackSourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                        {
                            continue;
                        }

                        sourceFdtPath = cleanAsciiFallbackSourcePath;
                    }
                    else
                    {
                        sourceFdtPath = cleanAsciiSourcePath;
                    }
                }
                else if (hangul)
                {
                    if (!hangulSourceEntries.TryGetValue(utf8Value, out sourceEntryBytes))
                    {
                        continue;
                    }

                    sourceFdtPath = sourcePath;
                }
                else
                {
                    continue;
                }

                FdtGlyphEntry sourceEntry = ReadFdtGlyphEntry(sourceEntryBytes, 0);
                if (sourceEntry.Width == 0 || sourceEntry.Height == 0)
                {
                    continue;
                }

                string sourceTexturePath = ResolveFontTexturePath(sourceFdtPath, sourceEntry.ImageIndex);
                string targetTexturePath = ResolveFontTexturePath(targetPath, sourceEntry.ImageIndex);
                if (sourceTexturePath == null)
                {
                    continue;
                }

                byte[] sourceTexture;
                if (ascii)
                {
                    Dictionary<string, byte[]> asciiSourceTextures = string.Equals(sourceFdtPath, cleanAsciiFallbackSourcePath, StringComparison.OrdinalIgnoreCase)
                        ? cleanAsciiFallbackSourceTextures
                        : cleanAsciiSourceTextures;
                    if (!TryReadCachedRawTexture(globalArchive, asciiSourceTextures, sourceTexturePath, out sourceTexture))
                    {
                        missingTextures++;
                        continue;
                    }
                }
                else
                {
                    if (!hangulSourceTextures.TryGetValue(sourceTexturePath, out sourceTexture))
                    {
                        sourceTexture = TryLoadTtmpTexturePayload(payloadsByPath, mpdStream, sourceTexturePath);
                        if (sourceTexture == null)
                        {
                            missingTextures++;
                            continue;
                        }

                        hangulSourceTextures.Add(sourceTexturePath, sourceTexture);
                    }
                }

                byte[] sourceAlpha;
                int sourceAlphaWidth = sourceEntry.Width;
                int sourceAlphaHeight = sourceEntry.Height;
                int entryLeftPadding = 0;
                int entryTopPadding = 0;
                try
                {
                    if (ascii)
                    {
                        CleanAsciiTextureRegion region = ExtractFontTextureAlphaRegion(sourceTexture, sourceEntry, CleanAsciiTexturePadding);
                        if (!region.IsValid)
                        {
                            extractionFailures++;
                            continue;
                        }

                        sourceAlpha = region.Alpha;
                        sourceAlphaWidth = region.Width;
                        sourceAlphaHeight = region.Height;
                        entryLeftPadding = region.LeftPadding;
                        entryTopPadding = region.TopPadding;
                    }
                    else
                    {
                        sourceAlpha = ExtractFontTextureAlpha(sourceTexture, sourceEntry);
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    extractionFailures++;
                    continue;
                }
                catch (IndexOutOfRangeException)
                {
                    extractionFailures++;
                    continue;
                }

                AllocatedFontGlyphCell allocatedCell;
                string allocatedTexturePath;
                if (!TryAllocateDerivedLobbyGlyphCell(glyphRepair, targetTexturePath, sourceAlphaWidth, sourceAlphaHeight, out allocatedTexturePath, out allocatedCell))
                {
                    allocationFailures++;
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = allocatedCell.X;
                patch.TargetY = allocatedCell.Y;
                patch.TargetChannel = allocatedCell.Channel;
                patch.ClearWidth = sourceAlphaWidth;
                patch.ClearHeight = sourceAlphaHeight;
                patch.SourceWidth = sourceAlphaWidth;
                patch.SourceHeight = sourceAlphaHeight;
                patch.SourceAlpha = sourceAlpha;
                patch.SourceFdtPath = sourceFdtPath;
                patch.SourceCodepoint = codepoint;
                AddTexturePatch(texturePatches, allocatedTexturePath, patch);

                byte[] targetEntry = new byte[FdtGlyphEntrySize];
                Buffer.BlockCopy(sourceEntryBytes, 0, targetEntry, 0, FdtGlyphEntrySize);
                Endian.WriteUInt16LE(targetEntry, 6, checked((ushort)allocatedCell.ImageIndex));
                Endian.WriteUInt16LE(targetEntry, 8, checked((ushort)(allocatedCell.X + entryLeftPadding)));
                Endian.WriteUInt16LE(targetEntry, 10, checked((ushort)(allocatedCell.Y + entryTopPadding)));
                if (hangul)
                {
                    targetEntry[14] = unchecked((byte)NormalizeDerivedLobbyAdvanceAdjustment(sourceEntry.Width, sourceEntry.Height, sourceEntry.OffsetX, minimumCjkAdvance));
                }

                int targetEntryIndex;
                if (targetEntryIndexes.TryGetValue(utf8Value, out targetEntryIndex))
                {
                    targetEntries[targetEntryIndex] = targetEntry;
                }
                else
                {
                    targetEntryIndexes.Add(utf8Value, targetEntries.Count);
                    targetEntries.Add(targetEntry);
                }

                queued++;
            }

            if (queued > 0)
            {
                targetFdt = RewriteFdtGlyphTable(targetFdt, fontTableOffset, glyphStart, glyphEnd, targetEntries);
            }

            if (missingTextures > 0)
            {
                AddLimitedWarning("4K lobby font source textures missing for " + targetPath + ": " + missingTextures.ToString());
            }

            if (allocationFailures > 0)
            {
                AddLimitedWarning("4K lobby font atlas allocation failures for " + targetPath + ": " + allocationFailures.ToString());
            }

            if (extractionFailures > 0)
            {
                AddLimitedWarning("4K lobby font source glyph extraction failures for " + targetPath + ": " + extractionFailures.ToString());
            }

            return queued;
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

        private static bool TryAllocateDerivedLobbyGlyphCell(
            FontGlyphRepairContext glyphRepair,
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

            string[] candidates = new string[]
            {
                preferredTexturePath,
                FontLobby1TexturePath,
                FontLobby2TexturePath,
                FontLobby3TexturePath,
                FontLobby4TexturePath,
                FontLobby5TexturePath,
                FontLobby6TexturePath,
                FontLobby7TexturePath
            };

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
                new string[] { preferredTexturePath, sourceTexturePath, Font1TexturePath, Font2TexturePath },
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
            out string allocatedTexturePath,
            out AllocatedFontGlyphCell allocatedCell)
        {
            string normalizedFdtPath = NormalizeGamePath(fdtPath);
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
                    Font2TexturePath
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
            LobbyHangulRepairContext lobbyHangulRepair,
            TargetedGlyphRepairContext dialogueGlyphRepair,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            out int normalized)
        {
            normalized = 0;
            if (!path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
            {
                return datWriter.WritePackedFile(packedFile);
            }

            byte[] fdt = SqPackArchive.UnpackStandardFile(packedFile);
            return WritePreparedFontFdtPayload(datWriter, path, fdt, packedFile, 0, mpdStream, payloadsByPath, lobbyHangulRepair, dialogueGlyphRepair, glyphRepair, globalArchive, texturePatches, out normalized);
        }

        private long WritePreparedFontFdtPayload(
            SqPackDatWriter datWriter,
            string path,
            byte[] fdt,
            byte[] originalPackedFile,
            int preChanged,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            LobbyHangulRepairContext lobbyHangulRepair,
            TargetedGlyphRepairContext dialogueGlyphRepair,
            FontGlyphRepairContext glyphRepair,
            SqPackArchive globalArchive,
            Dictionary<string, List<FontTexturePatch>> texturePatches,
            out int normalized)
        {
            normalized = NormalizeFdtShiftJisValues(fdt);
            int aliases = AddPartyListSelfMarkerAliases(path, ref fdt);
            int lobbyHangulAliases = ReplaceBlankLobbyHangulGlyphsFromAxis12(path, fdt, lobbyHangulRepair);
            int relocatedSkippedTextureHangulGlyphs = RelocateHangulGlyphsFromSkippedTextures(path, fdt, mpdStream, payloadsByPath, glyphRepair, texturePatches);
            int dialogueGlyphFixes = ApplyDialogueGlyphArtifactFix(path, fdt, dialogueGlyphRepair, glyphRepair, texturePatches);
            int partyShapeFixes = ApplyPartyListSelfMarkerCleanShapes(path, ref fdt, glyphRepair, globalArchive, texturePatches);
            int cleanAsciiFixes = ApplyCleanAsciiGlyphShapes(path, fdt, glyphRepair, globalArchive, texturePatches);
            int cleanAsciiKerningFixes = ApplyCleanAsciiKerning(path, ref fdt, globalArchive);
            int lobbyAxisHangulAdvanceFixes = NormalizeLobbyAxisHangulAdvances(path, fdt);
            // FDT edits are written as standard files when key normalization,
            // targeted Hangul repair, party marker allocation, or ASCII/numeric
            // glyph repair changed the render contract for the file.
            if (preChanged == 0 &&
                normalized == 0 &&
                aliases == 0 &&
                lobbyHangulAliases == 0 &&
                relocatedSkippedTextureHangulGlyphs == 0 &&
                dialogueGlyphFixes == 0 &&
                partyShapeFixes == 0 &&
                cleanAsciiFixes == 0 &&
                cleanAsciiKerningFixes == 0 &&
                lobbyAxisHangulAdvanceFixes == 0 &&
                originalPackedFile != null)
            {
                return datWriter.WritePackedFile(originalPackedFile);
            }

            if (aliases > 0)
            {
                Console.WriteLine("  Added party-list self marker glyph aliases: {0} ({1})", aliases, path);
            }

            if (lobbyHangulAliases > 0)
            {
                Console.WriteLine("  Remapped blank lobby Hangul glyphs to AXIS_12 atlas cells: {0} ({1})", lobbyHangulAliases, path);
            }

            if (relocatedSkippedTextureHangulGlyphs > 0)
            {
                Console.WriteLine("  Relocated Hangul glyphs from skipped texture cells: {0} ({1})", relocatedSkippedTextureHangulGlyphs, path);
            }

            if (dialogueGlyphFixes > 0)
            {
                Console.WriteLine("  Queued dialogue Hangul glyph artifact fixes: {0} ({1})", dialogueGlyphFixes, path);
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

            if (lobbyAxisHangulAdvanceFixes > 0)
            {
                Console.WriteLine("  Normalized lobby AXIS Hangul advances: {0} ({1})", lobbyAxisHangulAdvanceFixes, path);
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

        private static int ReplaceBlankLobbyHangulGlyphsFromAxis12(string path, byte[] targetFdt, LobbyHangulRepairContext repair)
        {
            // Character-select lobby text uses AXIS_14_lobby, but the TTMP atlas has
            // a small set of blank Hangul cells. Repoint only blank glyph cells to
            // AXIS_12_lobby so normal AXIS_14_lobby text does not shrink globally.
            if (!string.Equals(NormalizeGamePath(path), LobbyHangulAliasTargetPath, StringComparison.OrdinalIgnoreCase) ||
                targetFdt == null ||
                repair == null)
            {
                return 0;
            }

            Dictionary<uint, byte[]> sourceEntries = repair.SourceEntriesByUtf8Value;
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

            int changed = 0;
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

                byte[] sourceEntry;
                if (!sourceEntries.TryGetValue(utf8Value, out sourceEntry) ||
                    EntriesEqual(targetFdt, offset, sourceEntry) ||
                    !repair.IsBlank(targetFdt, offset))
                {
                    continue;
                }

                Buffer.BlockCopy(sourceEntry, 0, targetFdt, offset, FdtGlyphEntrySize);
                changed++;
            }

            return changed;
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
                if (sourceTexturePath == null || ShouldIncludeFontPath(sourceTexturePath))
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
                    out allocatedTexturePath,
                    out allocatedCell))
                {
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
                bool useAllocatedCell;
                if (hasTargetEntry)
                {
                    useAllocatedCell = glyphRepair.TryAllocate(targetTexturePath, sourceEntry.Width, sourceEntry.Height, out allocatedCell);
                }
                else
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

                if (!hasTargetEntry && !useAllocatedCell)
                {
                    continue;
                }

                FontTexturePatch patch = new FontTexturePatch();
                patch.TargetX = useAllocatedCell ? allocatedCell.X : targetEntry.X;
                patch.TargetY = useAllocatedCell ? allocatedCell.Y : targetEntry.Y;
                patch.TargetChannel = useAllocatedCell ? allocatedCell.Channel : targetEntry.ImageIndex % 4;
                patch.ClearWidth = hasTargetEntry ? Math.Max(targetEntry.Width, sourceEntry.Width) : sourceEntry.Width;
                patch.ClearHeight = hasTargetEntry ? Math.Max(targetEntry.Height, sourceEntry.Height) : sourceEntry.Height;
                patch.SourceWidth = sourceEntry.Width;
                patch.SourceHeight = sourceEntry.Height;
                patch.SourceAlpha = ExtractFontTextureAlpha(sourceTexture, sourceEntry);
                AddTexturePatch(texturePatches, allocatedTexturePath, patch);

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
            bool allocateCleanAsciiCell = ShouldAllocateCleanAsciiCell(normalizedPath);
            bool preserveCleanAsciiTexturePadding = ShouldPreserveCleanAsciiTexturePadding(normalizedPath);
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
                    allocationRegion = preserveCleanAsciiTexturePadding
                        ? ExtractFontTextureAlphaRegion(sourceTexture, sourceEntry, CleanAsciiTexturePadding)
                        : sourceRegion;
                }

                bool useAllocatedCell = false;
                AllocatedFontGlyphCell allocatedCell = new AllocatedFontGlyphCell();
                string allocatedTexturePath = null;
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
                    Endian.WriteUInt16LE(replacementEntry, 6, checked((ushort)allocatedCell.ImageIndex));
                    Endian.WriteUInt16LE(replacementEntry, 8, checked((ushort)(allocatedCell.X + allocationRegion.LeftPadding)));
                    Endian.WriteUInt16LE(replacementEntry, 10, checked((ushort)(allocatedCell.Y + allocationRegion.TopPadding)));
                    Buffer.BlockCopy(replacementEntry, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                    useAllocatedCell = true;
                }
                else if (repointFullEntry)
                {
                    byte[] replacementEntry = new byte[FdtGlyphEntrySize];
                    Buffer.BlockCopy(sourceEntryBytes, 0, replacementEntry, 0, FdtGlyphEntrySize);
                    Buffer.BlockCopy(replacementEntry, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                }

                FdtGlyphEntry targetEntry = ReadFdtGlyphEntry(targetFdt, targetOffset);
                string targetTexturePath = ResolveFontTexturePath(normalizedPath, targetEntry.ImageIndex);
                if (targetTexturePath == null)
                {
                    continue;
                }

                if (!ShouldIncludeFontPath(targetTexturePath))
                {
                    // Diagnostic profiles can exclude a texture while still
                    // patching FDTs that point to it. Move ASCII/numeric glyphs
                    // to an included clean cell when possible so the excluded
                    // atlas remains untouched.
                    CleanAsciiTextureRegion patchRegion = useAllocatedCell ? allocationRegion : sourceRegion;
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
                        Buffer.BlockCopy(sourceEntryBytes, 0, targetFdt, targetOffset, FdtGlyphEntrySize);
                    }

                    changed++;
                    continue;
                }

                if (sourceRegion.IsValid)
                {
                    CleanAsciiTextureRegion patchRegion = useAllocatedCell ? allocationRegion : sourceRegion;
                    FontTexturePatch patch = CreateCleanAsciiTexturePatch(
                        useAllocatedCell ? allocatedCell.X : targetEntry.X - patchRegion.LeftPadding,
                        useAllocatedCell ? allocatedCell.Y : targetEntry.Y - patchRegion.TopPadding,
                        useAllocatedCell ? allocatedCell.Channel : targetEntry.ImageIndex % 4,
                        patchRegion,
                        useAllocatedCell ? patchRegion.Width : Math.Max(targetEntry.Width, patchRegion.Width),
                        useAllocatedCell ? patchRegion.Height : Math.Max(targetEntry.Height, patchRegion.Height),
                        sourceFdtPath,
                        (uint)codepoint);
                    AddTexturePatch(texturePatches, useAllocatedCell ? allocatedTexturePath : targetTexturePath, patch);
                }

                if (!useAllocatedCell && !repointFullEntry)
                {
                    targetFdt[targetOffset + 12] = sourceEntry.Width;
                    targetFdt[targetOffset + 13] = sourceEntry.Height;
                    targetFdt[targetOffset + 14] = unchecked((byte)sourceEntry.OffsetX);
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

            int changed = ReplaceAsciiKerningEntries(ref targetFdt, sourceFdt);
            return changed;
        }

        private static int ReplaceAsciiKerningEntries(ref byte[] targetFdt, byte[] sourceFdt)
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

            return ShouldRepairCleanDigitsOnlyFont(normalized) ||
                   ShouldRepairCleanFullAsciiAxisFont(normalized) ||
                   normalized.IndexOf("/jupiter_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/miedingermid_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/meidinger_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/trumpgothic_", StringComparison.OrdinalIgnoreCase) >= 0;
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
            return ShouldRepairCleanFullAsciiAxisFont(normalized) ||
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

        private static bool IsDerived4kLobbyRequiredHangulCodepoint(uint codepoint)
        {
            for (int i = 0; i < Derived4kLobbyRequiredHangulCodepoints.Length; i++)
            {
                if (Derived4kLobbyRequiredHangulCodepoints[i] == codepoint)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveDerivedLobbyCleanAsciiSourceFdtPath(string targetPath)
        {
            return NormalizeGamePath(targetPath);
        }

        private static string ResolveDerivedLobbyFallbackCleanAsciiSourceFdtPath(string targetPath)
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

        private static string ToLobbyFontPath(string fontPath)
        {
            string normalized = NormalizeGamePath(fontPath);
            if (normalized.EndsWith("_lobby.fdt", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(0, normalized.Length - 4) + "_lobby.fdt";
            }

            return normalized;
        }

        private static sbyte NormalizeDerivedLobbyAdvanceAdjustment(byte glyphWidth, byte glyphHeight, sbyte sourceAdvanceAdjustment, int minimumCjkAdvance)
        {
            int sourceAdvance = Math.Max(1, glyphWidth + sourceAdvanceAdjustment);
            int baseAdvance = Math.Max(sourceAdvance, glyphWidth);
            int visualGapAdvance = glyphWidth + ComputeLobbyMinimumVisualGap(glyphHeight);
            int targetAdvance = minimumCjkAdvance > 0
                ? Math.Max(Math.Max(baseAdvance, minimumCjkAdvance), visualGapAdvance)
                : Math.Max(baseAdvance + ComputeNoCjkFallbackSafetyAdvance(glyphWidth), visualGapAdvance);
            return ToAdvanceAdjustment(glyphWidth, targetAdvance);
        }

        private static int NormalizeLobbyAxisHangulAdvances(string path, byte[] fdt)
        {
            if (!ShouldNormalizeLobbyAxisHangulAdvances(path) || fdt == null)
            {
                return 0;
            }

            int fontTableOffset;
            uint glyphCount;
            int glyphStart;
            if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                uint codepoint;
                if (!TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(fdt, offset), out codepoint) ||
                    !IsHangulCodepoint(codepoint))
                {
                    continue;
                }

                FdtGlyphEntry entry = ReadFdtGlyphEntry(fdt, offset);
                if (entry.Width == 0 || entry.Height == 0)
                {
                    continue;
                }

                int currentAdvance = Math.Max(1, entry.Width + entry.OffsetX);
                if (currentAdvance >= entry.Width)
                {
                    continue;
                }

                fdt[offset + 14] = unchecked((byte)ToAdvanceAdjustment(entry.Width, entry.Width));
                changed++;
            }

            return changed;
        }

        private static bool ShouldNormalizeLobbyAxisHangulAdvances(string path)
        {
            string normalized = NormalizeGamePath(path);
            return string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "common/font/AXIS_18_lobby.fdt", StringComparison.OrdinalIgnoreCase);
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

        private static int ComputeLobbyMinimumVisualGap(byte glyphHeight)
        {
            return Math.Max(2, (glyphHeight + 13) / 14 + 1);
        }

        private static bool IsLobbyFontPath(string fontPath)
        {
            return !string.IsNullOrEmpty(fontPath) &&
                   NormalizeGamePath(fontPath).IndexOf("_lobby.fdt", StringComparison.OrdinalIgnoreCase) >= 0;
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

        private uint[] CreateDerived4kLobbyRequiredPhraseCodepoints(string koreaSqpack)
        {
            HashSet<uint> codepoints = new HashSet<uint>();
            AddPhraseCodepoints(codepoints, Derived4kLobbyRequiredHangulPhrases);
            int staticCount = codepoints.Count;
            int dynamicAdded = AddStartScreenSystemSettingsAddonCodepoints(codepoints, koreaSqpack);

            uint[] values = ToSortedCodepointArray(codepoints);
            Console.WriteLine(
                "4K lobby required codepoints: {0} static, {1} addon-derived, {2} total",
                staticCount,
                dynamicAdded,
                values.Length);
            return values;
        }

        private int AddStartScreenSystemSettingsAddonCodepoints(HashSet<uint> codepoints, string koreaSqpack)
        {
            if (codepoints == null || string.IsNullOrEmpty(koreaSqpack))
            {
                return 0;
            }

            string textIndexPath = Path.Combine(koreaSqpack, TextIndexFileName);
            if (!File.Exists(textIndexPath))
            {
                AddLimitedWarning("Korean text index missing for 4K lobby Addon glyph coverage: " + textIndexPath);
                return 0;
            }

            int before = codepoints.Count;
            try
            {
                using (SqPackArchive textArchive = new SqPackArchive(textIndexPath, koreaSqpack, TextDatPrefix))
                {
                    ExcelHeader header = ExcelHeader.Parse(textArchive.ReadFile("exd/Addon.exh"));
                    if (header.Variant != ExcelVariant.Default)
                    {
                        AddLimitedWarning("Addon header variant is not supported for dynamic 4K lobby glyph coverage: " + header.Variant.ToString());
                        return 0;
                    }

                    byte languageId = LanguageCodes.ToId(_options.SourceLanguage);
                    bool hasLanguageSuffix = header.HasLanguage(languageId);
                    List<int> stringColumns = header.GetStringColumnIndexes();
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        if (!AddonPageOverlaps(page, LobbyScaledHangulPhrases.StartScreenSystemSettingsAddonRowRanges))
                        {
                            continue;
                        }

                        string exdPath = BuildExdPath("Addon", page.StartId, hasLanguageSuffix ? _options.SourceLanguage : null);
                        ExcelDataFile file = ExcelDataFile.Parse(textArchive.ReadFile(exdPath));
                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            if (!RowInRanges(row.RowId, LobbyScaledHangulPhrases.StartScreenSystemSettingsAddonRowRanges))
                            {
                                continue;
                            }

                            for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                            {
                                byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]);
                                AddUtf8PhraseCodepoints(codepoints, bytes);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLimitedWarning("Could not collect Addon glyph coverage for 4K lobby fonts: " + ex.Message);
                return 0;
            }

            return codepoints.Count - before;
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

        private static string BuildExdPath(string sheet, uint pageStartId, string language)
        {
            return "exd/" + sheet + "_" + pageStartId + (string.IsNullOrEmpty(language) ? string.Empty : "_" + language) + ".exd";
        }

        private static void AddPhraseCodepoints(HashSet<uint> codepoints, string[] phrases)
        {
            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                AddPhraseCodepoints(codepoints, phrases[phraseIndex]);
            }
        }

        private static void AddUtf8PhraseCodepoints(HashSet<uint> codepoints, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            AddPhraseCodepoints(codepoints, Encoding.UTF8.GetString(bytes));
        }

        private static void AddPhraseCodepoints(HashSet<uint> codepoints, string phrase)
        {
            phrase = phrase ?? string.Empty;
            for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
            {
                uint codepoint = ReadCodepoint(phrase, ref charIndex);
                if (ShouldIncludeDerived4kLobbyCodepoint(codepoint))
                {
                    codepoints.Add(codepoint);
                }
            }
        }

        private static bool ShouldIncludeDerived4kLobbyCodepoint(uint codepoint)
        {
            return (codepoint > 0x20 && codepoint <= 0x7E) || IsHangulCodepoint(codepoint);
        }

        private static uint[] ToSortedCodepointArray(HashSet<uint> codepoints)
        {
            uint[] values = new uint[codepoints.Count];
            codepoints.CopyTo(values);
            Array.Sort(values);
            return values;
        }

        private static uint[] CreateHangulCodepoints(string[] phrases)
        {
            HashSet<uint> codepoints = new HashSet<uint>();
            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                string phrase = phrases[phraseIndex] ?? string.Empty;
                for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref charIndex);
                    if (IsHangulCodepoint(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }
            }

            uint[] values = new uint[codepoints.Count];
            codepoints.CopyTo(values);
            Array.Sort(values);
            return values;
        }

        private static uint[] CreatePhraseCodepoints(string[] phrases)
        {
            HashSet<uint> codepoints = new HashSet<uint>();
            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                string phrase = phrases[phraseIndex] ?? string.Empty;
                for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref charIndex);
                    if (codepoint > 0x20)
                    {
                        codepoints.Add(codepoint);
                    }
                }
            }

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

        private static LobbyHangulRepairContext TryCreateLobbyHangulRepairContext(FontPatchPackage fontPackage, FileStream mpdStream)
        {
            byte[] axis12Lobby = TryLoadTtmpStandardPayload(fontPackage, mpdStream, LobbyHangulAliasSourcePath);
            if (axis12Lobby == null)
            {
                return null;
            }

            byte[] fontLobby1 = TryLoadTtmpTexturePayload(fontPackage, mpdStream, FontLobby1TexturePath);
            byte[] fontLobby2 = TryLoadTtmpTexturePayload(fontPackage, mpdStream, FontLobby2TexturePath);
            if (fontLobby1 == null || fontLobby2 == null)
            {
                return null;
            }

            LobbyHangulRepairContext context = new LobbyHangulRepairContext();
            context.SourceEntriesByUtf8Value = ReadGlyphEntriesByUtf8Value(axis12Lobby);
            context.Textures = new List<FontTexture>();
            context.Textures.Add(ReadFontTexture(fontLobby1));
            context.Textures.Add(ReadFontTexture(fontLobby2));
            return context;
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

        private static FontGlyphRepairContext TryCreateFontGlyphRepairContext(FontPatchPackage fontPackage, FileStream mpdStream, SqPackArchive globalArchive)
        {
            if (fontPackage == null || mpdStream == null)
            {
                return null;
            }

            FontGlyphRepairContext context = new FontGlyphRepairContext();
            AddFontAtlasAllocator(context, fontPackage, mpdStream, Font1TexturePath);
            AddFontAtlasAllocator(context, fontPackage, mpdStream, Font2TexturePath);
            AddFontAtlasAllocator(context, fontPackage, mpdStream, FontLobby1TexturePath);
            AddFontAtlasAllocator(context, fontPackage, mpdStream, FontLobby2TexturePath);
            AddFontAtlasAllocator(context, fontPackage, mpdStream, FontKrnTexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby3TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby4TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby5TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby6TexturePath);
            AddGlobalFontAtlasAllocator(context, globalArchive, FontLobby7TexturePath);

            if (context.AllocatorCount == 0)
            {
                return null;
            }

            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                FontPayload payload = fontPackage.Payloads[i];
                string path = NormalizeGamePath(payload.FullPath);
                if (!ShouldProtectHangulGlyphSourceFdt(path))
                {
                    continue;
                }

                byte[] packed = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, path);
                byte[] fdt = SqPackArchive.UnpackStandardFile(packed);
                MarkFontGlyphOccupancy(context, path, fdt);
            }

            return context;
        }

        private static ProtectedHangulGlyphContext TryCreateProtectedHangulGlyphContext(FontPatchPackage fontPackage, FileStream mpdStream)
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
                        sourceTexture = TryLoadTtmpTexturePayload(fontPackage, mpdStream, texturePath);
                        if (sourceTexture == null)
                        {
                            continue;
                        }

                        sourceTextures.Add(texturePath, sourceTexture);
                    }

                    FontTexturePatch patch = new FontTexturePatch();
                    patch.TargetX = entry.X;
                    patch.TargetY = entry.Y;
                    patch.TargetChannel = entry.ImageIndex % 4;
                    patch.ClearWidth = entry.Width;
                    patch.ClearHeight = entry.Height;
                    patch.SourceWidth = entry.Width;
                    patch.SourceHeight = entry.Height;
                    patch.SourceAlpha = ExtractFontTextureAlpha(sourceTexture, entry);
                    patch.SourceFdtPath = path;
                    patch.SourceCodepoint = codepoint;
                    context.AddPatch(texturePath, patch);
                    seenCells.Add(cellKey);
                }
            }

            return context.PatchCount == 0 ? null : context;
        }

        private static bool ShouldProtectHangulTexturePath(string texturePath)
        {
            // Clean ASCII/numeric repair may touch shared font2 cells used by
            // in-game Korean text. Protect every Hangul cell that overlaps a
            // later repair instead of whitelisting individual syllables such as
            // "호" or "혼".
            return string.Equals(texturePath, Font2TexturePath, StringComparison.OrdinalIgnoreCase);
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

                if (overlapsAnyPatch && !overlapsCriticalAsciiPatch)
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
                FdtGlyphEntry entry = ReadFdtGlyphEntry(fdt, offset);
                string texturePath = ResolveFontTexturePath(fdtPath, entry.ImageIndex);
                FontAtlasAllocator allocator;
                if (texturePath == null || !context.TryGetAllocator(texturePath, out allocator))
                {
                    continue;
                }

                allocator.MarkOccupied(entry.X, entry.Y, entry.Width, entry.Height, entry.ImageIndex % 4);
            }
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

        private static byte[] PatchPackedFontTexture(byte[] packedFile, List<FontTexturePatch> patches)
        {
            List<TextureSubBlock> subBlocks;
            byte[] rawTexture = UnpackTextureFile(packedFile, out subBlocks);
            ApplyFontTexturePatches(rawTexture, patches);
            return PackTextureFile(rawTexture);
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
            return region;
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
            }
        }

        private static byte GetFontTextureChannel(FontTexture texture, int x, int y, int channel)
        {
            if (x < 0 || y < 0 || x >= texture.Width || y >= texture.Height)
            {
                return 0;
            }

            int offset = texture.DataOffset + (y * texture.Width + x) * 2;
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
            if (x < 0 || y < 0 || x >= texture.Width || y >= texture.Height)
            {
                return;
            }

            value = (byte)(value & 0x0F);
            int offset = texture.DataOffset + (y * texture.Width + x) * 2;
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

        private sealed class LobbyHangulRepairContext
        {
            public Dictionary<uint, byte[]> SourceEntriesByUtf8Value;
            public List<FontTexture> Textures;

            public bool IsBlank(byte[] fdt, int entryOffset)
            {
                FdtGlyphEntry entry = ReadFdtGlyphEntry(fdt, entryOffset);
                int textureIndex = entry.ImageIndex / 4;
                if (textureIndex < 0 || textureIndex >= Textures.Count)
                {
                    return false;
                }

                FontTexture texture = Textures[textureIndex];
                int channel = entry.ImageIndex % 4;
                int visible = 0;
                int area = Math.Max(1, entry.Width * entry.Height);
                int maxVisibleForBlank = Math.Max(1, area / 100);
                for (int y = 0; y < entry.Height; y++)
                {
                    for (int x = 0; x < entry.Width; x++)
                    {
                        if (GetFontTextureChannel(texture, entry.X + x, entry.Y + y, channel) == 0)
                        {
                            continue;
                        }

                        visible++;
                        if (visible > maxVisibleForBlank)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
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

        private struct CleanAsciiTextureRegion
        {
            public byte[] Alpha;
            public int Width;
            public int Height;
            public int LeftPadding;
            public int TopPadding;

            public bool IsValid
            {
                get { return Alpha != null && Width > 0 && Height > 0; }
            }
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
                int stepX = Math.Max(1, w + 2);
                int stepY = Math.Max(1, h + 2);
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
