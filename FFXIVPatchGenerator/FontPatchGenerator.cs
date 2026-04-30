using System;
using System.Collections.Generic;
using System.IO;
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

        // Explicit font resource set used by the global client for in-game and lobby text rendering.
        private static readonly string[] FontPaths = new string[]
        {
            "common/font/Jupiter_45.fdt",
            "common/font/Jupiter_45_lobby.fdt",
            "common/font/Jupiter_90.fdt",
            "common/font/Jupiter_20_lobby.fdt",
            "common/font/Jupiter_23_lobby.fdt",
            "common/font/Jupiter_23.fdt",
            "common/font/Jupiter_46.fdt",
            "common/font/Jupiter_16_lobby.fdt",
            "common/font/Meidinger_16_lobby.fdt",
            "common/font/Meidinger_20_lobby.fdt",
            "common/font/Meidinger_40.fdt",
            "common/font/MiedingerMid_10_lobby.fdt",
            "common/font/MiedingerMid_12_lobby.fdt",
            "common/font/MiedingerMid_14_lobby.fdt",
            "common/font/MiedingerMid_18_lobby.fdt",
            "common/font/MiedingerMid_36.fdt",
            "common/font/TrumpGothic_23.fdt",
            "common/font/TrumpGothic_23_lobby.fdt",
            "common/font/TrumpGothic_34.fdt",
            "common/font/TrumpGothic_34_lobby.fdt",
            "common/font/TrumpGothic_68.fdt",
            "common/font/TrumpGothic_184.fdt",
            "common/font/TrumpGothic_184_lobby.fdt",
            "common/font/AXIS_12.fdt",
            "common/font/AXIS_12_lobby.fdt",
            "common/font/AXIS_14.fdt",
            "common/font/AXIS_14_lobby.fdt",
            "common/font/AXIS_18.fdt",
            "common/font/AXIS_18_lobby.fdt",
            "common/font/AXIS_36.fdt",
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

                long datOffset;
                if (FdtSjisRepairer.IsFdtPath(path))
                {
                    byte[] baseFdt = null;
                    globalArchive.TryReadFile(path, out baseFdt);
                    byte[] overlayFdt = SqPackArchive.UnpackStandardFile(packedFile);
                    int repairedEntries;
                    byte[] repairedFdt = FdtSjisRepairer.Repair(baseFdt, overlayFdt, out repairedEntries);
                    if (repairedEntries > 0)
                    {
                        datOffset = datWriter.WriteStandardFile(repairedFdt);
                    }
                    else
                    {
                        datOffset = datWriter.WritePackedFile(packedFile);
                    }
                }
                else
                {
                    datOffset = datWriter.WritePackedFile(packedFile);
                }

                mutableIndex.SetFileOffset(path, 1, datOffset);
                mutableIndex2.SetFileOffset(path, 1, datOffset);
                _report.FontFilesPatched++;
            }
        }

        private void WriteTtmpFontFiles(FontPatchPackage fontPackage, SqPackArchive globalArchive, SqPackIndexFile mutableIndex, SqPackIndex2File mutableIndex2, SqPackDatWriter datWriter)
        {
            using (FileStream mpdStream = new FileStream(fontPackage.MpdPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
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
                    long datOffset;
                    if (FdtSjisRepairer.IsFdtPath(path))
                    {
                        byte[] baseFdt = null;
                        globalArchive.TryReadFile(path, out baseFdt);
                        byte[] overlayFdt = SqPackArchive.UnpackStandardFile(packedFile);
                        int repairedEntries;
                        byte[] repairedFdt = FdtSjisRepairer.Repair(baseFdt, overlayFdt, out repairedEntries);
                        if (repairedEntries > 0)
                        {
                            datOffset = datWriter.WriteStandardFile(repairedFdt);
                        }
                        else
                        {
                            datOffset = datWriter.WritePackedFile(packedFile);
                        }
                    }
                    else
                    {
                        datOffset = datWriter.WritePackedFile(packedFile);
                    }

                    mutableIndex.SetFileOffset(path, 1, datOffset);
                    mutableIndex2.SetFileOffset(path, 1, datOffset);
                    _report.FontFilesPatched++;
                }
            }
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

    internal static class FdtSjisRepairer
    {
        private const int FontTableEntryCountOffset = 0x24;
        private const int GlyphTableStart = 0x40;
        private const int GlyphEntrySize = 16;
        private const int CharUtf8Offset = 0;
        private const int CharSjisOffset = 4;

        public static bool IsFdtPath(string path)
        {
            return path != null && path.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase);
        }

        public static byte[] Repair(byte[] baseFdt, byte[] overlayFdt, out int repairedEntries)
        {
            repairedEntries = 0;
            if (!IsValidFdt(overlayFdt))
            {
                return overlayFdt;
            }

            Dictionary<uint, ushort> baseSjisByUtf8 = BuildSjisMap(baseFdt);
            byte[] output = (byte[])overlayFdt.Clone();
            uint glyphCount = Endian.ReadUInt32LE(output, FontTableEntryCountOffset);

            for (int i = 0; i < glyphCount; i++)
            {
                int entryOffset = GlyphTableStart + i * GlyphEntrySize;
                uint charUtf8 = Endian.ReadUInt32LE(output, entryOffset + CharUtf8Offset);
                ushort currentSjis = ReadUInt16LE(output, entryOffset + CharSjisOffset);
                ushort baseSjis;
                ushort targetSjis = GetAsciiSjis(charUtf8);
                if (baseSjisByUtf8.TryGetValue(charUtf8, out baseSjis) && baseSjis != 0)
                {
                    if (targetSjis != 0 || IsPrivateUseUiGlyph(charUtf8))
                    {
                        targetSjis = baseSjis;
                    }
                }

                if (targetSjis == 0)
                {
                    continue;
                }

                if (currentSjis == targetSjis)
                {
                    continue;
                }

                WriteUInt16LE(output, entryOffset + CharSjisOffset, targetSjis);
                repairedEntries++;
            }

            return output;
        }

        private static Dictionary<uint, ushort> BuildSjisMap(byte[] fdt)
        {
            Dictionary<uint, ushort> map = new Dictionary<uint, ushort>();
            if (!IsValidFdt(fdt))
            {
                return map;
            }

            uint glyphCount = Endian.ReadUInt32LE(fdt, FontTableEntryCountOffset);
            for (int i = 0; i < glyphCount; i++)
            {
                int entryOffset = GlyphTableStart + i * GlyphEntrySize;
                uint charUtf8 = Endian.ReadUInt32LE(fdt, entryOffset + CharUtf8Offset);
                ushort charSjis = ReadUInt16LE(fdt, entryOffset + CharSjisOffset);
                if (charSjis != 0 && !map.ContainsKey(charUtf8))
                {
                    map.Add(charUtf8, charSjis);
                }
            }

            return map;
        }

        private static bool IsValidFdt(byte[] fdt)
        {
            if (fdt == null || fdt.Length < GlyphTableStart)
            {
                return false;
            }

            if (fdt[0] != (byte)'f' || fdt[1] != (byte)'c' || fdt[2] != (byte)'s' || fdt[3] != (byte)'v')
            {
                return false;
            }

            uint glyphCount = Endian.ReadUInt32LE(fdt, FontTableEntryCountOffset);
            return GlyphTableStart + (long)glyphCount * GlyphEntrySize <= fdt.Length;
        }

        private static ushort GetAsciiSjis(uint charUtf8)
        {
            byte value = (byte)(charUtf8 & 0xFF);
            if ((charUtf8 & 0xFFFFFF00u) != 0 || value < 0x20 || value > 0x7E)
            {
                return 0;
            }

            return value;
        }

        private static bool IsPrivateUseUiGlyph(uint charUtf8)
        {
            int codePoint;
            return TryDecodeFdtUtf8(charUtf8, out codePoint) && codePoint >= 0xE000 && codePoint <= 0xF8FF;
        }

        private static bool TryDecodeFdtUtf8(uint charUtf8, out int codePoint)
        {
            codePoint = 0;
            // FDT stores the UTF-8 byte sequence as an integer-like field, so rebuild the
            // byte order before checking whether the glyph is in the BMP private-use range.
            byte[] raw = new byte[]
            {
                (byte)(charUtf8 >> 24),
                (byte)(charUtf8 >> 16),
                (byte)(charUtf8 >> 8),
                (byte)charUtf8
            };

            int start = 0;
            while (start < raw.Length && raw[start] == 0)
            {
                start++;
            }

            int length = raw.Length - start;
            if (length <= 0)
            {
                return false;
            }

            byte first = raw[start];
            if (first < 0x80)
            {
                if (length != 1)
                {
                    return false;
                }

                codePoint = first;
                return true;
            }

            if ((first & 0xE0) == 0xC0)
            {
                if (length != 2 || !IsUtf8Continuation(raw[start + 1]))
                {
                    return false;
                }

                codePoint = ((first & 0x1F) << 6) | (raw[start + 1] & 0x3F);
                return codePoint >= 0x80;
            }

            if ((first & 0xF0) == 0xE0)
            {
                if (length != 3 || !IsUtf8Continuation(raw[start + 1]) || !IsUtf8Continuation(raw[start + 2]))
                {
                    return false;
                }

                codePoint = ((first & 0x0F) << 12) |
                            ((raw[start + 1] & 0x3F) << 6) |
                            (raw[start + 2] & 0x3F);
                return codePoint >= 0x800 && (codePoint < 0xD800 || codePoint > 0xDFFF);
            }

            if ((first & 0xF8) == 0xF0)
            {
                if (length != 4 ||
                    !IsUtf8Continuation(raw[start + 1]) ||
                    !IsUtf8Continuation(raw[start + 2]) ||
                    !IsUtf8Continuation(raw[start + 3]))
                {
                    return false;
                }

                codePoint = ((first & 0x07) << 18) |
                            ((raw[start + 1] & 0x3F) << 12) |
                            ((raw[start + 2] & 0x3F) << 6) |
                            (raw[start + 3] & 0x3F);
                return codePoint >= 0x10000 && codePoint <= 0x10FFFF;
            }

            return false;
        }

        private static bool IsUtf8Continuation(byte value)
        {
            return (value & 0xC0) == 0x80;
        }

        private static ushort ReadUInt16LE(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static void WriteUInt16LE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }
    }
}
