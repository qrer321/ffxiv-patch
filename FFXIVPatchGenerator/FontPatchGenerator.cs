using System;
using System.Collections.Generic;
using System.IO;
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
        private const uint PartyListSelfMarkerPrimaryStart = 0xE0E1u;
        private const int PartyListSelfMarkerCount = 8;
        private const int FontTextureFormatA4R4G4B4 = 0x1440;
        private const int FontTextureHeaderSizeOffset = 0x1C;
        private const int FontGlyphTexturePadding = 1;
        private const ushort PartyListMarkerDestinationChannel = 3;

        // Party-list self numbers use U+E0E1..U+E0E8. Korean TTMP fonts can miss
        // those entries, which makes the client fall back to "=" or to the wrong
        // U+E0B1..U+E0B8 circle-number glyphs. Keep the clean global boxed markers
        // by transplanting both FDT entries and the small glyph pixels.
        private static readonly string[] PartyListMarkerFontPaths = new string[]
        {
            "common/font/AXIS_12.fdt",
            "common/font/AXIS_12_lobby.fdt"
        };

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
            "common/font/font_krn_1.tex"
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
                    WriteTtmpFontFiles(fontPackage, globalArchive, mutableIndex, mutableIndex2, datWriter);
                }
                else
                {
                    using (SqPackArchive koreaArchive = new SqPackArchive(Path.Combine(koreaSqpack, IndexFileName), koreaSqpack, "000000.win32"))
                    {
                        WriteKoreanFontFiles(globalArchive, koreaArchive, mutableIndex, mutableIndex2, datWriter);
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

        private void WriteKoreanFontFiles(SqPackArchive globalArchive, SqPackArchive koreaArchive, SqPackIndexFile mutableIndex, SqPackIndex2File mutableIndex2, SqPackDatWriter datWriter)
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
                long datOffset = WriteFontPayload(datWriter, path, packedFile, out normalized);
                LogFontPayloadAdjustments(path, normalized);
                mutableIndex.SetFileOffset(path, 1, datOffset);
                mutableIndex2.SetFileOffset(path, 1, datOffset);
                _report.FontFilesPatched++;
            }
        }

        private void WriteTtmpFontFiles(FontPatchPackage fontPackage, SqPackArchive globalArchive, SqPackIndexFile mutableIndex, SqPackIndex2File mutableIndex2, SqPackDatWriter datWriter)
        {
            using (FileStream mpdStream = new FileStream(fontPackage.MpdPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                PartyListMarkerPatchPlan partyMarkerPlan = BuildPartyListMarkerPatchPlan(fontPackage, mpdStream, globalArchive);
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

                    byte[] packedFile = ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, path);
                    int normalized;
                    long datOffset = WriteFontPayload(datWriter, path, packedFile, partyMarkerPlan, out normalized);
                    LogFontPayloadAdjustments(path, normalized);
                    mutableIndex.SetFileOffset(path, 1, datOffset);
                    mutableIndex2.SetFileOffset(path, 1, datOffset);
                    _report.FontFilesPatched++;
                }
            }
        }

        private long WriteFontPayload(SqPackDatWriter datWriter, string path, byte[] packedFile, out int normalized)
        {
            return WriteFontPayload(datWriter, path, packedFile, null, out normalized);
        }

        private long WriteFontPayload(SqPackDatWriter datWriter, string path, byte[] packedFile, PartyListMarkerPatchPlan partyMarkerPlan, out int normalized)
        {
            normalized = 0;
            if (path.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
            {
                int copied = ApplyPartyListMarkerTexturePatch(path, partyMarkerPlan, ref packedFile);
                if (copied > 0)
                {
                    Console.WriteLine("  Restored party-list self marker glyph pixels: {0} ({1})", copied, path);
                    return datWriter.WriteTextureFile(packedFile);
                }

                return datWriter.WritePackedFile(packedFile);
            }

            if (!path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
            {
                return datWriter.WritePackedFile(packedFile);
            }

            byte[] fdt = SqPackArchive.UnpackStandardFile(packedFile);
            normalized = NormalizeFdtShiftJisValues(fdt);
            int markers = ApplyPartyListMarkerFdtPatch(path, partyMarkerPlan, ref fdt);
            if (normalized == 0 && markers == 0)
            {
                return datWriter.WritePackedFile(packedFile);
            }

            if (markers > 0)
            {
                Console.WriteLine("  Restored party-list self marker glyph entries: {0} ({1})", markers, path);
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

        private PartyListMarkerPatchPlan BuildPartyListMarkerPatchPlan(FontPatchPackage fontPackage, FileStream mpdStream, SqPackArchive globalArchive)
        {
            PartyListMarkerPatchPlan plan = new PartyListMarkerPatchPlan();
            Dictionary<string, FontPayload> payloadsByPath = BuildFontPayloadLookup(fontPackage);

            for (int i = 0; i < PartyListMarkerFontPaths.Length; i++)
            {
                string fontPath = PartyListMarkerFontPaths[i];
                if (!ShouldIncludeFontPath(fontPath))
                {
                    continue;
                }

                string destinationTexturePath = GetPartyListMarkerDestinationTexturePath(fontPath);
                if (string.IsNullOrEmpty(destinationTexturePath) ||
                    !ShouldIncludeFontPath(destinationTexturePath) ||
                    !payloadsByPath.ContainsKey(fontPath) ||
                    !payloadsByPath.ContainsKey(destinationTexturePath))
                {
                    continue;
                }

                byte[] sourceFdt;
                if (!globalArchive.TryReadFile(fontPath, out sourceFdt))
                {
                    AddLimitedWarning("Missing clean global party marker font source: " + fontPath);
                    continue;
                }

                byte[] targetFdt = SqPackArchive.UnpackStandardFile(ReadTtmpPayload(mpdStream, payloadsByPath[fontPath]));
                byte[] destinationTexture = SqPackArchive.UnpackTextureFile(ReadTtmpPayload(mpdStream, payloadsByPath[destinationTexturePath]));
                TextureInfo textureInfo;
                if (!TryReadFontTextureInfo(destinationTexture, out textureInfo))
                {
                    AddLimitedWarning("Unsupported party marker texture target: " + destinationTexturePath);
                    continue;
                }

                TextureOccupancy occupancy = BuildTextureOccupancy(fontPackage, mpdStream, payloadsByPath, destinationTexturePath, PartyListMarkerDestinationChannel, textureInfo);
                Dictionary<uint, byte[]> entries = new Dictionary<uint, byte[]>();
                List<PartyListMarkerTextureCopy> copies = new List<PartyListMarkerTextureCopy>();

                for (int marker = 0; marker < PartyListSelfMarkerCount; marker++)
                {
                    uint codepoint = PartyListSelfMarkerPrimaryStart + (uint)marker;
                    FdtGlyphEntry sourceEntry;
                    if (!TryGetFdtGlyph(sourceFdt, codepoint, out sourceEntry))
                    {
                        AddLimitedWarning("Missing clean global party marker glyph U+" + codepoint.ToString("X4") + " in " + fontPath);
                        continue;
                    }

                    string sourceTexturePath = ResolveFontTexturePath(fontPath, sourceEntry.ImageIndex);
                    if (string.IsNullOrEmpty(sourceTexturePath))
                    {
                        AddLimitedWarning("Could not resolve clean party marker source texture for " + fontPath);
                        continue;
                    }

                    byte[] packedSourceTexture;
                    if (!globalArchive.TryReadPackedFile(sourceTexturePath, out packedSourceTexture))
                    {
                        AddLimitedWarning("Missing clean global party marker texture source: " + sourceTexturePath);
                        continue;
                    }

                    byte[] sourceTexture = SqPackArchive.UnpackTextureFile(packedSourceTexture);
                    byte[] nibbles = ExtractFontTextureNibbles(sourceTexture, sourceEntry);
                    ushort destinationX;
                    ushort destinationY;
                    if (!occupancy.TryReserve(sourceEntry.Width, sourceEntry.Height, out destinationX, out destinationY))
                    {
                        AddLimitedWarning("Could not reserve free party marker glyph space in " + destinationTexturePath);
                        continue;
                    }

                    FdtGlyphEntry destinationEntry = sourceEntry;
                    destinationEntry.ImageIndex = GetTextureImageIndex(destinationTexturePath, PartyListMarkerDestinationChannel);
                    destinationEntry.X = destinationX;
                    destinationEntry.Y = destinationY;
                    byte[] entryBytes = destinationEntry.ToBytes();
                    entries[PackFdtUtf8Value(codepoint)] = entryBytes;

                    PartyListMarkerTextureCopy copy = new PartyListMarkerTextureCopy();
                    copy.Entry = destinationEntry;
                    copy.Nibbles = nibbles;
                    copies.Add(copy);
                }

                if (entries.Count == 0)
                {
                    continue;
                }

                plan.FdtEntriesByPath[NormalizeGamePath(fontPath)] = entries;
                if (!plan.TextureCopiesByPath.ContainsKey(destinationTexturePath))
                {
                    plan.TextureCopiesByPath[destinationTexturePath] = new List<PartyListMarkerTextureCopy>();
                }

                plan.TextureCopiesByPath[destinationTexturePath].AddRange(copies);
            }

            return plan;
        }

        private static Dictionary<string, FontPayload> BuildFontPayloadLookup(FontPatchPackage fontPackage)
        {
            Dictionary<string, FontPayload> result = new Dictionary<string, FontPayload>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                FontPayload payload = fontPackage.Payloads[i];
                string path = NormalizeGamePath(payload.FullPath);
                if (!result.ContainsKey(path))
                {
                    result.Add(path, payload);
                }
            }

            return result;
        }

        private static int ApplyPartyListMarkerFdtPatch(string path, PartyListMarkerPatchPlan plan, ref byte[] fdt)
        {
            if (plan == null)
            {
                return 0;
            }

            Dictionary<uint, byte[]> replacementEntries;
            if (!plan.FdtEntriesByPath.TryGetValue(NormalizeGamePath(path), out replacementEntries) ||
                replacementEntries.Count == 0)
            {
                return 0;
            }

            return ReplaceFdtGlyphEntries(ref fdt, replacementEntries);
        }

        private static int ApplyPartyListMarkerTexturePatch(string path, PartyListMarkerPatchPlan plan, ref byte[] packedFile)
        {
            if (plan == null)
            {
                return 0;
            }

            List<PartyListMarkerTextureCopy> copies;
            if (!plan.TextureCopiesByPath.TryGetValue(NormalizeGamePath(path), out copies) ||
                copies.Count == 0)
            {
                return 0;
            }

            byte[] texture = SqPackArchive.UnpackTextureFile(packedFile);
            for (int i = 0; i < copies.Count; i++)
            {
                ApplyFontTextureNibbles(texture, copies[i]);
            }

            packedFile = texture;
            return copies.Count;
        }

        private static int ReplaceFdtGlyphEntries(ref byte[] fdt, Dictionary<uint, byte[]> replacementEntries)
        {
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
            List<byte[]> entries = new List<byte[]>((int)glyphCount + replacementEntries.Count);
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

            int changed = 0;
            foreach (KeyValuePair<uint, byte[]> replacement in replacementEntries)
            {
                byte[] existingEntry;
                if (entriesByCodepoint.TryGetValue(replacement.Key, out existingEntry))
                {
                    if (!ByteArraysEqual(existingEntry, replacement.Value))
                    {
                        Buffer.BlockCopy(replacement.Value, 0, existingEntry, 0, FdtGlyphEntrySize);
                        changed++;
                    }
                }
                else
                {
                    byte[] entry = new byte[FdtGlyphEntrySize];
                    Buffer.BlockCopy(replacement.Value, 0, entry, 0, FdtGlyphEntrySize);
                    entries.Add(entry);
                    entriesByCodepoint.Add(replacement.Key, entry);
                    changed++;
                }
            }

            if (changed == 0)
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
            return changed;
        }

        private TextureOccupancy BuildTextureOccupancy(
            FontPatchPackage fontPackage,
            FileStream mpdStream,
            Dictionary<string, FontPayload> payloadsByPath,
            string destinationTexturePath,
            ushort destinationChannel,
            TextureInfo textureInfo)
        {
            TextureOccupancy occupancy = new TextureOccupancy(textureInfo.Width, textureInfo.Height);
            for (int i = 0; i < fontPackage.Payloads.Count; i++)
            {
                string fdtPath = NormalizeGamePath(fontPackage.Payloads[i].FullPath);
                if (!fdtPath.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase) ||
                    !ShouldIncludeFontPath(fdtPath))
                {
                    continue;
                }

                FontPayload fdtPayload;
                if (!payloadsByPath.TryGetValue(fdtPath, out fdtPayload))
                {
                    continue;
                }

                byte[] fdt;
                try
                {
                    fdt = SqPackArchive.UnpackStandardFile(ReadTtmpPayload(mpdStream, fdtPayload));
                }
                catch (InvalidDataException)
                {
                    continue;
                }

                List<FdtGlyphEntry> glyphs = ReadFdtGlyphs(fdt);
                for (int glyphIndex = 0; glyphIndex < glyphs.Count; glyphIndex++)
                {
                    FdtGlyphEntry glyph = glyphs[glyphIndex];
                    if (GetFontTextureChannel(glyph.ImageIndex) != destinationChannel)
                    {
                        continue;
                    }

                    string glyphTexturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                    if (!string.Equals(glyphTexturePath, destinationTexturePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    occupancy.Mark(glyph.X, glyph.Y, glyph.Width, glyph.Height);
                }
            }

            return occupancy;
        }

        private static bool TryGetFdtGlyph(byte[] fdt, uint codepoint, out FdtGlyphEntry entry)
        {
            List<FdtGlyphEntry> glyphs = ReadFdtGlyphs(fdt);
            uint value = PackFdtUtf8Value(codepoint);
            for (int i = 0; i < glyphs.Count; i++)
            {
                if (glyphs[i].Utf8Value == value)
                {
                    entry = glyphs[i];
                    return true;
                }
            }

            entry = new FdtGlyphEntry();
            return false;
        }

        private static List<FdtGlyphEntry> ReadFdtGlyphs(byte[] fdt)
        {
            List<FdtGlyphEntry> result = new List<FdtGlyphEntry>();
            if (fdt == null ||
                fdt.Length < FdtHeaderSize ||
                !HasAsciiSignature(fdt, 0, "fcsv0100"))
            {
                return result;
            }

            int fontTableOffset = checked((int)Endian.ReadUInt32LE(fdt, 0x08));
            if (fontTableOffset < FdtHeaderSize ||
                fontTableOffset > fdt.Length - FdtFontTableHeaderSize ||
                !HasAsciiSignature(fdt, fontTableOffset, "fthd"))
            {
                return result;
            }

            uint glyphCount = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x04);
            int glyphStart = fontTableOffset + FdtFontTableHeaderSize;
            long glyphBytes = (long)glyphCount * FdtGlyphEntrySize;
            if (glyphBytes < 0 || glyphStart > fdt.Length || glyphStart + glyphBytes > fdt.Length)
            {
                return result;
            }

            for (int i = 0; i < glyphCount; i++)
            {
                int offset = glyphStart + i * FdtGlyphEntrySize;
                FdtGlyphEntry entry = new FdtGlyphEntry();
                entry.Utf8Value = Endian.ReadUInt32LE(fdt, offset);
                entry.ShiftJisValue = Endian.ReadUInt16LE(fdt, offset + 4);
                entry.ImageIndex = Endian.ReadUInt16LE(fdt, offset + 6);
                entry.X = Endian.ReadUInt16LE(fdt, offset + 8);
                entry.Y = Endian.ReadUInt16LE(fdt, offset + 10);
                entry.Width = fdt[offset + 12];
                entry.Height = fdt[offset + 13];
                entry.OffsetX = unchecked((sbyte)fdt[offset + 14]);
                entry.OffsetY = unchecked((sbyte)fdt[offset + 15]);
                result.Add(entry);
            }

            return result;
        }

        private static bool TryReadFontTextureInfo(byte[] texture, out TextureInfo info)
        {
            info = new TextureInfo();
            if (texture == null || texture.Length < 0x20)
            {
                return false;
            }

            uint format = Endian.ReadUInt32LE(texture, 0x04);
            if (format != FontTextureFormatA4R4G4B4)
            {
                return false;
            }

            info.Width = Endian.ReadUInt16LE(texture, 0x08);
            info.Height = Endian.ReadUInt16LE(texture, 0x0A);
            info.DataOffset = checked((int)Endian.ReadUInt32LE(texture, FontTextureHeaderSizeOffset));
            int expectedBytes = checked(info.Width * info.Height * 2);
            return info.Width > 0 &&
                   info.Height > 0 &&
                   info.DataOffset >= 0 &&
                   info.DataOffset <= texture.Length - expectedBytes;
        }

        private static byte[] ExtractFontTextureNibbles(byte[] texture, FdtGlyphEntry entry)
        {
            TextureInfo info;
            if (!TryReadFontTextureInfo(texture, out info))
            {
                throw new InvalidDataException("Unsupported font texture format.");
            }

            if (entry.X + entry.Width > info.Width || entry.Y + entry.Height > info.Height)
            {
                throw new InvalidDataException("Glyph source rectangle is outside the font texture.");
            }

            int channel = GetFontTextureChannel(entry.ImageIndex);
            byte[] result = new byte[checked(entry.Width * entry.Height)];
            int resultOffset = 0;
            for (int y = 0; y < entry.Height; y++)
            {
                int sourceY = entry.Y + y;
                for (int x = 0; x < entry.Width; x++)
                {
                    int sourceX = entry.X + x;
                    int pixelOffset = info.DataOffset + (sourceY * info.Width + sourceX) * 2;
                    result[resultOffset++] = ReadFontTextureNibble(texture, pixelOffset, channel);
                }
            }

            return result;
        }

        private static void ApplyFontTextureNibbles(byte[] texture, PartyListMarkerTextureCopy copy)
        {
            TextureInfo info;
            if (!TryReadFontTextureInfo(texture, out info))
            {
                throw new InvalidDataException("Unsupported font texture format.");
            }

            if (copy.Entry.X + copy.Entry.Width > info.Width ||
                copy.Entry.Y + copy.Entry.Height > info.Height ||
                copy.Nibbles == null ||
                copy.Nibbles.Length != copy.Entry.Width * copy.Entry.Height)
            {
                throw new InvalidDataException("Invalid party marker glyph destination.");
            }

            int channel = GetFontTextureChannel(copy.Entry.ImageIndex);
            int sourceOffset = 0;
            for (int y = 0; y < copy.Entry.Height; y++)
            {
                int destinationY = copy.Entry.Y + y;
                for (int x = 0; x < copy.Entry.Width; x++)
                {
                    int destinationX = copy.Entry.X + x;
                    int pixelOffset = info.DataOffset + (destinationY * info.Width + destinationX) * 2;
                    WriteFontTextureNibble(texture, pixelOffset, channel, copy.Nibbles[sourceOffset++]);
                }
            }
        }

        private static byte ReadFontTextureNibble(byte[] texture, int pixelOffset, int channel)
        {
            byte lo = texture[pixelOffset];
            byte hi = texture[pixelOffset + 1];
            switch (channel)
            {
                case 0:
                    return (byte)(hi & 0x0F);
                case 1:
                    return (byte)((lo >> 4) & 0x0F);
                case 2:
                    return (byte)(lo & 0x0F);
                default:
                    return (byte)((hi >> 4) & 0x0F);
            }
        }

        private static void WriteFontTextureNibble(byte[] texture, int pixelOffset, int channel, byte value)
        {
            value = (byte)(value & 0x0F);
            byte lo = texture[pixelOffset];
            byte hi = texture[pixelOffset + 1];
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

            texture[pixelOffset] = lo;
            texture[pixelOffset + 1] = hi;
        }

        private static string GetPartyListMarkerDestinationTexturePath(string fdtPath)
        {
            string normalized = NormalizeGamePath(fdtPath).ToLowerInvariant();
            if (normalized.EndsWith("_lobby.fdt", StringComparison.OrdinalIgnoreCase))
            {
                return "common/font/font_lobby2.tex";
            }

            return "common/font/font3.tex";
        }

        private static ushort GetTextureImageIndex(string texturePath, ushort channel)
        {
            int textureIndex = GetTextureIndexFromPath(texturePath);
            if (textureIndex < 0)
            {
                throw new InvalidDataException("Could not resolve texture index: " + texturePath);
            }

            return checked((ushort)(textureIndex * 4 + channel));
        }

        private static int GetTextureIndexFromPath(string texturePath)
        {
            string normalized = NormalizeGamePath(texturePath).ToLowerInvariant();
            if (normalized.EndsWith("/font1.tex", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/font_lobby1.tex", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/font_krn_1.tex", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (normalized.EndsWith("/font2.tex", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/font_lobby2.tex", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (normalized.EndsWith("/font3.tex", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return -1;
        }

        private static string ResolveFontTexturePath(string fdtPath, ushort imageIndex)
        {
            string normalized = NormalizeGamePath(fdtPath).ToLowerInvariant();
            int textureIndex = imageIndex / 4;
            if (normalized.EndsWith("_lobby.fdt", StringComparison.OrdinalIgnoreCase))
            {
                if (textureIndex == 0)
                {
                    return "common/font/font_lobby1.tex";
                }

                if (textureIndex == 1)
                {
                    return "common/font/font_lobby2.tex";
                }

                return null;
            }

            if (Path.GetFileName(normalized).StartsWith("krnaxis_", StringComparison.OrdinalIgnoreCase))
            {
                if (textureIndex == 0)
                {
                    return "common/font/font_krn_1.tex";
                }

                return null;
            }

            switch (textureIndex)
            {
                case 0:
                    return "common/font/font1.tex";
                case 1:
                    return "common/font/font2.tex";
                case 2:
                    return "common/font/font3.tex";
                default:
                    return null;
            }
        }

        private static int GetFontTextureChannel(ushort imageIndex)
        {
            return imageIndex % 4;
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
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

            if (string.Equals(profile, FontPatchProfiles.NoTrumpGothic, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.IndexOf("/trumpgothic_", StringComparison.OrdinalIgnoreCase) < 0;
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
                       normalized.IndexOf("/jupiter_", StringComparison.OrdinalIgnoreCase) < 0;
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

        private static string NormalizeGamePath(string path)
        {
            return path.Replace('\\', '/').Trim();
        }

        private static byte[] ReadTtmpPayload(FileStream mpdStream, FontPayload payload)
        {
            return ReadPackedPayload(mpdStream, payload.ModOffset, payload.ModSize, NormalizeGamePath(payload.FullPath));
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

        private sealed class PartyListMarkerPatchPlan
        {
            public readonly Dictionary<string, Dictionary<uint, byte[]>> FdtEntriesByPath =
                new Dictionary<string, Dictionary<uint, byte[]>>(StringComparer.OrdinalIgnoreCase);

            public readonly Dictionary<string, List<PartyListMarkerTextureCopy>> TextureCopiesByPath =
                new Dictionary<string, List<PartyListMarkerTextureCopy>>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class PartyListMarkerTextureCopy
        {
            public FdtGlyphEntry Entry;
            public byte[] Nibbles;
        }

        private struct FdtGlyphEntry
        {
            public uint Utf8Value;
            public ushort ShiftJisValue;
            public ushort ImageIndex;
            public ushort X;
            public ushort Y;
            public byte Width;
            public byte Height;
            public sbyte OffsetX;
            public sbyte OffsetY;

            public byte[] ToBytes()
            {
                byte[] bytes = new byte[FdtGlyphEntrySize];
                Endian.WriteUInt32LE(bytes, 0, Utf8Value);
                Endian.WriteUInt16LE(bytes, 4, ShiftJisValue);
                Endian.WriteUInt16LE(bytes, 6, ImageIndex);
                Endian.WriteUInt16LE(bytes, 8, X);
                Endian.WriteUInt16LE(bytes, 10, Y);
                bytes[12] = Width;
                bytes[13] = Height;
                bytes[14] = unchecked((byte)OffsetX);
                bytes[15] = unchecked((byte)OffsetY);
                return bytes;
            }
        }

        private struct TextureInfo
        {
            public int Width;
            public int Height;
            public int DataOffset;
        }

        private sealed class TextureOccupancy
        {
            private readonly bool[] _used;

            public TextureOccupancy(int width, int height)
            {
                Width = width;
                Height = height;
                _used = new bool[checked(width * height)];
            }

            public int Width { get; private set; }

            public int Height { get; private set; }

            public void Mark(int x, int y, int width, int height)
            {
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                int left = Math.Max(0, x - FontGlyphTexturePadding);
                int top = Math.Max(0, y - FontGlyphTexturePadding);
                int right = Math.Min(Width, x + width + FontGlyphTexturePadding);
                int bottom = Math.Min(Height, y + height + FontGlyphTexturePadding);
                for (int row = top; row < bottom; row++)
                {
                    int rowOffset = row * Width;
                    for (int col = left; col < right; col++)
                    {
                        _used[rowOffset + col] = true;
                    }
                }
            }

            public bool TryReserve(int width, int height, out ushort x, out ushort y)
            {
                x = 0;
                y = 0;
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                int maxX = Width - width - FontGlyphTexturePadding - 1;
                int maxY = Height - height - FontGlyphTexturePadding - 1;
                for (int row = maxY; row >= FontGlyphTexturePadding; row--)
                {
                    for (int col = maxX; col >= FontGlyphTexturePadding; col--)
                    {
                        if (!IsFree(col - FontGlyphTexturePadding, row - FontGlyphTexturePadding, width + FontGlyphTexturePadding * 2, height + FontGlyphTexturePadding * 2))
                        {
                            continue;
                        }

                        Mark(col, row, width, height);
                        x = checked((ushort)col);
                        y = checked((ushort)row);
                        return true;
                    }
                }

                return false;
            }

            private bool IsFree(int x, int y, int width, int height)
            {
                if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Width || y + height > Height)
                {
                    return false;
                }

                for (int row = y; row < y + height; row++)
                {
                    int rowOffset = row * Width;
                    for (int col = x; col < x + width; col++)
                    {
                        if (_used[rowOffset + col])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

    }

    internal static class FontPatchProfiles
    {
        public const string Full = "full";
        public const string UiNumericSafe = "ui-numeric-safe";
        public const string Default = Full;
        public const string NoMiedingerMid = "no-miedingermid";
        public const string NoTrumpGothic = "no-trumpgothic";
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
                case NoTrumpGothic:
                case NoJupiter:
                case NoAxis:
                case FdtOnly:
                case TexturesOnly:
                    return normalized;
                default:
                    throw new ArgumentException(
                        "Unsupported font profile: " + value +
                        ". Supported values: full, ui-numeric-safe, no-miedingermid, no-trumpgothic, no-jupiter, no-axis, fdt-only, textures-only.");
            }
        }
    }

}
