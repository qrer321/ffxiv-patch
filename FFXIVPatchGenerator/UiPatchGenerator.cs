using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class UiPatchGenerator
    {
        private const string RepositoryDir = "sqpack\\ffxiv";
        private const string IndexFileName = "060000.win32.index";
        private const string Index2FileName = "060000.win32.index2";
        private const string Dat0FileName = "060000.win32.dat0";
        private const string Dat4FileName = "060000.win32.dat4";
        private const string ExcelIndexFileName = "0a0000.win32.index";
        private const byte PatchDatId = 4;
        private const uint PatchDataFileCount = PatchDatId + 1u;
        private const string OrigIndexFileName = "orig.060000.win32.index";
        private const string OrigIndex2FileName = "orig.060000.win32.index2";
        private const string PartyListTargetBaseTexPath = "ui/uld/PartyListTargetBase.tex";
        // ScreenImage.Lang marks image IDs that are stored under language folders such as ui/icon/120000/ja.
        private const int ScreenImageLangFlagBit = 0;
        private static readonly IconSheetSpec ScreenImageSpec = new IconSheetSpec(
            "screenimage",
            "ScreenImage",
            true,
            true,
            11,
            new IconColumnSpec(7, 0),
            new IconColumnSpec(6, 4));
        private static readonly IconSheetSpec CutScreenImageSpec = new IconSheetSpec(
            "cutscreenimage",
            "CutScreenImage",
            true,
            false,
            0,
            new IconColumnSpec(6, 0),
            new IconColumnSpec(6, 4));
        private static readonly IconSheetSpec DynamicEventScreenImageSpec = new IconSheetSpec(
            "dynamiceventscreenimage",
            "DynamicEventScreenImage",
            false,
            false,
            0,
            new IconColumnSpec(7, 0),
            new IconColumnSpec(7, 4),
            new IconColumnSpec(7, 8));
        private static readonly IconSheetSpec EventImageSpec = new IconSheetSpec(
            "eventimage",
            "EventImage",
            false,
            false,
            0,
            new IconColumnSpec(6, 0));
        private static readonly IconSheetSpec TradeScreenImageSpec = new IconSheetSpec(
            "tradescreenimage",
            "TradeScreenImage",
            false,
            false,
            0,
            new IconColumnSpec(7, 0),
            new IconColumnSpec(7, 4),
            new IconColumnSpec(7, 8),
            new IconColumnSpec(7, 12));
        private static readonly IconSheetSpec TerritoryTypeSpec = new IconSheetSpec(
            "territorytype",
            "TerritoryType",
            true,
            false,
            0,
            // TerritoryType points at localized zone title images used during area transitions.
            // The second image also has a +2000 subtitle layer used by ImageLocationTitle.
            new IconColumnSpec(6, 12),
            new IconColumnSpec(6, 16, 2000u));

        private readonly BuildOptions _options;
        private readonly BuildReport _report;

        public UiPatchGenerator(BuildOptions options, BuildReport report)
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
            RequireFile(Path.Combine(globalSqpack, ExcelIndexFileName));
            RequireFile(Path.Combine(koreaSqpack, IndexFileName));

            string currentGlobalIndex = Path.Combine(globalSqpack, IndexFileName);
            string originalGlobalIndex = Path.Combine(globalSqpack, OrigIndexFileName);
            string baseIndex = ResolveBaseIndex(currentGlobalIndex, originalGlobalIndex, _options.BaseUiIndexPath, IndexFileName);
            string currentGlobalIndex2 = Path.Combine(globalSqpack, Index2FileName);
            string originalGlobalIndex2 = Path.Combine(globalSqpack, OrigIndex2FileName);
            string baseIndex2 = ResolveBaseIndex(currentGlobalIndex2, originalGlobalIndex2, _options.BaseUiIndex2Path, Index2FileName);

            string outputIndex = Path.Combine(outputDir, IndexFileName);
            string outputIndex2 = Path.Combine(outputDir, Index2FileName);
            string outputOrigIndex = Path.Combine(outputDir, OrigIndexFileName);
            string outputOrigIndex2 = Path.Combine(outputDir, OrigIndex2FileName);
            string outputDat4 = Path.Combine(outputDir, Dat4FileName);

            File.Copy(baseIndex, outputOrigIndex, true);
            File.Copy(baseIndex, outputIndex, true);
            File.Copy(baseIndex2, outputOrigIndex2, true);
            File.Copy(baseIndex2, outputIndex2, true);

            Console.WriteLine("Using base global UI index: {0}", baseIndex);
            Console.WriteLine("Using base global UI index2:{0}", baseIndex2);
            Console.WriteLine("Using Korean UI texture resources.");

            using (SqPackArchive globalUiArchive = new SqPackArchive(baseIndex, globalSqpack, "060000.win32"))
            using (SqPackArchive koreaUiArchive = new SqPackArchive(Path.Combine(koreaSqpack, IndexFileName), koreaSqpack, "060000.win32"))
            using (SqPackArchive globalExcelArchive = new SqPackArchive(ResolveExcelIndexForRead(globalSqpack), globalSqpack, "0a0000.win32"))
            using (SqPackIndexFile mutableIndex = new SqPackIndexFile(outputIndex))
            using (SqPackIndex2File mutableIndex2 = new SqPackIndex2File(outputIndex2))
            using (SqPackDatWriter datWriter = new SqPackDatWriter(outputDat4, Path.Combine(globalSqpack, Dat0FileName), PatchDataFileCount))
            {
                mutableIndex.EnsureDataFileCount(PatchDataFileCount);
                mutableIndex2.EnsureDataFileCount(PatchDataFileCount);

                int patched = 0;
                HashSet<string> copiedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                patched += CopyRequiredUiTexture(
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    PartyListTargetBaseTexPath,
                    PartyListTargetBaseTexPath,
                    "Korean PartyList target texture");
                patched += CopyIconSheetImages(
                    ScreenImageSpec,
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);
                patched += CopyIconSheetImages(
                    CutScreenImageSpec,
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);
                patched += CopyIconSheetImages(
                    DynamicEventScreenImageSpec,
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);
                patched += CopyIconSheetImages(
                    EventImageSpec,
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);
                patched += CopyIconSheetImages(
                    TradeScreenImageSpec,
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);
                patched += CopyIconSheetImages(
                    TerritoryTypeSpec,
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);
                patched += CopyLoadingImages(
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    copiedTargets);

                mutableIndex.Save();
                mutableIndex2.Save();

                _report.UiFilesPatched += patched;
            }

            ProgressReporter.Report(99, "UI texture patch saved");
        }

        private string ResolveExcelIndexForRead(string globalSqpack)
        {
            if (!string.IsNullOrWhiteSpace(_options.BaseIndexPath))
            {
                string fullPath = Path.GetFullPath(_options.BaseIndexPath);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return Path.Combine(globalSqpack, ExcelIndexFileName);
        }

        private int CopyRequiredUiTexture(
            SqPackArchive sourceArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            string sourcePath,
            string targetPath,
            string displayName)
        {
            byte[] packedTexture;
            if (!sourceArchive.TryReadPackedFile(sourcePath, out packedTexture))
            {
                throw new FileNotFoundException(displayName + " was not found.", sourcePath);
            }

            long offset = datWriter.WritePackedFile(packedTexture);
            mutableIndex.SetFileOffset(targetPath, PatchDatId, offset);
            mutableIndex2.SetFileOffset(targetPath, PatchDatId, offset);
            Console.WriteLine("UI texture patched: {0}", targetPath);
            return 1;
        }

        private int CopyIconSheetImages(
            IconSheetSpec spec,
            SqPackArchive globalExcelArchive,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            HashSet<string> copiedTargets)
        {
            byte[] headerBytes;
            string exhPath = "exd/" + spec.SheetName + ".exh";
            if (!globalExcelArchive.TryReadFile(exhPath, out headerBytes))
            {
                _report.Warnings.Add(spec.DisplayName + " EXH was not found. UI image localization was skipped.");
                return 0;
            }

            ExcelHeader header = ExcelHeader.Parse(headerBytes);
            if (header.Variant != ExcelVariant.Default || !SpecColumnsExist(header, spec))
            {
                _report.Warnings.Add(spec.DisplayName + " schema was not recognized. UI image localization was skipped.");
                return 0;
            }

            IconPatchStats stats = new IconPatchStats();

            for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
            {
                ExcelPageDefinition page = header.Pages[pageIndex];
                string exdPath = "exd/" + spec.SheetName + "_" + page.StartId.ToString() + ".exd";
                byte[] exdBytes;
                if (!globalExcelArchive.TryReadFile(exdPath, out exdBytes))
                {
                    _report.Warnings.Add(spec.DisplayName + " page was not found and was skipped: " + exdPath);
                    continue;
                }

                ExcelDataFile dataFile = ExcelDataFile.Parse(exdBytes);
                for (int rowIndex = 0; rowIndex < dataFile.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = dataFile.Rows[rowIndex];
                    if (spec.RequiresLanguageFlag &&
                        !ReadPackedBool(dataFile, row, spec.LanguageFlagOffset, ScreenImageLangFlagBit))
                    {
                        continue;
                    }

                    for (int columnIndex = 0; columnIndex < spec.IconColumns.Length; columnIndex++)
                    {
                        uint iconId = ReadIconId(dataFile, row, spec.IconColumns[columnIndex]);
                        if (iconId == 0)
                        {
                            continue;
                        }

                        CopyIconVariants(
                            iconId,
                            spec.UsesLanguageFolders,
                            copiedTargets,
                            globalUiArchive,
                            koreaUiArchive,
                            mutableIndex,
                            mutableIndex2,
                            datWriter,
                            stats);

                        for (int additionalIndex = 0; additionalIndex < spec.IconColumns[columnIndex].AdditionalIconIdOffsets.Length; additionalIndex++)
                        {
                            uint additionalIconId;
                            if (!TryAddIconId(iconId, spec.IconColumns[columnIndex].AdditionalIconIdOffsets[additionalIndex], out additionalIconId))
                            {
                                continue;
                            }

                            CopyIconVariants(
                                additionalIconId,
                                spec.UsesLanguageFolders,
                                copiedTargets,
                                globalUiArchive,
                                koreaUiArchive,
                                mutableIndex,
                                mutableIndex2,
                                datWriter,
                                stats);
                        }
                    }
                }
            }

            Console.WriteLine(
                "{0} textures patched: {1} (candidates={2}, identical={3}, missingGlobal={4}, missingKorea={5})",
                spec.DisplayName,
                stats.Patched,
                stats.Candidates,
                stats.Identical,
                stats.MissingGlobalTargets,
                stats.MissingKoreanSources);

            if (spec.DisplayName == "ScreenImage" && stats.Patched == 0)
            {
                _report.Warnings.Add("No localized ScreenImage textures were patched. Check target/source language folders.");
            }

            return stats.Patched;
        }

        private int CopyLoadingImages(
            SqPackArchive globalExcelArchive,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            HashSet<string> copiedTargets)
        {
            const string sheetName = "loadingimage";
            const string displayName = "LoadingImage";
            byte[] headerBytes;
            if (!globalExcelArchive.TryReadFile("exd/" + sheetName + ".exh", out headerBytes))
            {
                _report.Warnings.Add(displayName + " EXH was not found. Loading image localization was skipped.");
                return 0;
            }

            ExcelHeader header = ExcelHeader.Parse(headerBytes);
            if (header.Variant != ExcelVariant.Default || !HasColumn(header, 0, 0))
            {
                _report.Warnings.Add(displayName + " schema was not recognized. Loading image localization was skipped.");
                return 0;
            }

            IconPatchStats stats = new IconPatchStats();
            for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
            {
                ExcelPageDefinition page = header.Pages[pageIndex];
                string exdPath = "exd/" + sheetName + "_" + page.StartId.ToString() + ".exd";
                byte[] exdBytes;
                if (!globalExcelArchive.TryReadFile(exdPath, out exdBytes))
                {
                    _report.Warnings.Add(displayName + " page was not found and was skipped: " + exdPath);
                    continue;
                }

                ExcelDataFile dataFile = ExcelDataFile.Parse(exdBytes);
                for (int rowIndex = 0; rowIndex < dataFile.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = dataFile.Rows[rowIndex];
                    byte[] fileNameBytes = dataFile.GetStringBytesByColumnOffset(row, header, 0);
                    if (fileNameBytes == null || fileNameBytes.Length == 0)
                    {
                        continue;
                    }

                    string fileName = System.Text.Encoding.UTF8.GetString(fileNameBytes);
                    if (fileName.Length == 0 || fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0)
                    {
                        continue;
                    }

                    CopySamePathTextureVariant(
                        "ui/loadingimage/" + fileName + ".tex",
                        copiedTargets,
                        globalUiArchive,
                        koreaUiArchive,
                        mutableIndex,
                        mutableIndex2,
                        datWriter,
                        stats);
                    CopySamePathTextureVariant(
                        "ui/loadingimage/" + fileName + "_hr1.tex",
                        copiedTargets,
                        globalUiArchive,
                        koreaUiArchive,
                        mutableIndex,
                        mutableIndex2,
                        datWriter,
                        stats);
                }
            }

            Console.WriteLine(
                "{0} textures patched: {1} (candidates={2}, identical={3}, missingGlobal={4}, missingKorea={5})",
                displayName,
                stats.Patched,
                stats.Candidates,
                stats.Identical,
                stats.MissingGlobalTargets,
                stats.MissingKoreanSources);

            return stats.Patched;
        }

        private void CopyIconVariants(
            uint iconId,
            bool usesLanguageFolders,
            HashSet<string> copiedTargets,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            IconPatchStats stats)
        {
            CopyIconVariant(
                iconId,
                ".tex",
                usesLanguageFolders,
                copiedTargets,
                globalUiArchive,
                koreaUiArchive,
                mutableIndex,
                mutableIndex2,
                datWriter,
                stats);
            CopyIconVariant(
                iconId,
                "_hr1.tex",
                usesLanguageFolders,
                copiedTargets,
                globalUiArchive,
                koreaUiArchive,
                mutableIndex,
                mutableIndex2,
                datWriter,
                stats);
        }

        private void CopyIconVariant(
            uint iconId,
            string suffix,
            bool usesLanguageFolders,
            HashSet<string> copiedTargets,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            IconPatchStats stats)
        {
            string targetPath = usesLanguageFolders
                ? BuildLocalizedIconPath(iconId, _options.TargetLanguage, suffix)
                : BuildIconPath(iconId, suffix);
            string sourcePath = usesLanguageFolders
                ? BuildLocalizedIconPath(iconId, _options.SourceLanguage, suffix)
                : targetPath;
            CopyTextureVariant(
                sourcePath,
                targetPath,
                copiedTargets,
                globalUiArchive,
                koreaUiArchive,
                mutableIndex,
                mutableIndex2,
                datWriter,
                stats);
        }

        private void CopySamePathTextureVariant(
            string path,
            HashSet<string> copiedTargets,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            IconPatchStats stats)
        {
            CopyTextureVariant(
                path,
                path,
                copiedTargets,
                globalUiArchive,
                koreaUiArchive,
                mutableIndex,
                mutableIndex2,
                datWriter,
                stats);
        }

        private void CopyTextureVariant(
            string sourcePath,
            string targetPath,
            HashSet<string> copiedTargets,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            IconPatchStats stats)
        {
            if (!copiedTargets.Add(targetPath))
            {
                return;
            }

            stats.Candidates++;
            byte[] globalPacked;
            if (!globalUiArchive.TryReadPackedFile(targetPath, out globalPacked))
            {
                stats.MissingGlobalTargets++;
                return;
            }

            byte[] koreaPacked;
            if (!koreaUiArchive.TryReadPackedFile(sourcePath, out koreaPacked))
            {
                stats.MissingKoreanSources++;
                return;
            }

            if (BytesEqual(globalPacked, koreaPacked))
            {
                stats.Identical++;
                return;
            }

            long offset = datWriter.WritePackedFile(koreaPacked);
            mutableIndex.SetFileOffset(targetPath, PatchDatId, offset);
            mutableIndex2.SetFileOffset(targetPath, PatchDatId, offset);
            stats.Patched++;
        }

        private static string BuildLocalizedIconPath(uint iconId, string language, string suffix)
        {
            uint folder = iconId / 1000u * 1000u;
            return string.Format(
                "ui/icon/{0:D6}/{1}/{2:D6}{3}",
                folder,
                (language ?? string.Empty).ToLowerInvariant(),
                iconId,
                suffix);
        }

        private static string BuildIconPath(uint iconId, string suffix)
        {
            uint folder = iconId / 1000u * 1000u;
            return string.Format(
                "ui/icon/{0:D6}/{1:D6}{2}",
                folder,
                iconId,
                suffix);
        }

        private static bool SpecColumnsExist(ExcelHeader header, IconSheetSpec spec)
        {
            for (int i = 0; i < spec.IconColumns.Length; i++)
            {
                if (!HasColumn(header, spec.IconColumns[i].Type, spec.IconColumns[i].Offset))
                {
                    return false;
                }
            }

            return !spec.RequiresLanguageFlag || HasColumn(header, 25, spec.LanguageFlagOffset);
        }

        private static bool HasColumn(ExcelHeader header, ushort type, ushort offset)
        {
            for (int i = 0; i < header.Columns.Count; i++)
            {
                if (header.Columns[i].Type == type && header.Columns[i].Offset == offset)
                {
                    return true;
                }
            }

            return false;
        }

        private static uint ReadIconId(ExcelDataFile file, ExcelDataRow row, IconColumnSpec column)
        {
            if (column.Type == 7)
            {
                return ReadUInt32(file, row, column.Offset);
            }

            if (column.Type == 6)
            {
                int value = unchecked((int)ReadUInt32(file, row, column.Offset));
                return value > 0 ? (uint)value : 0;
            }

            throw new InvalidDataException("Unsupported icon ID column type: " + column.Type);
        }

        private static bool TryAddIconId(uint iconId, uint additionalOffset, out uint additionalIconId)
        {
            additionalIconId = 0;
            if (additionalOffset == 0 || iconId > 999999u - additionalOffset)
            {
                return false;
            }

            additionalIconId = iconId + additionalOffset;
            return true;
        }

        private static uint ReadUInt32(ExcelDataFile file, ExcelDataRow row, ushort columnOffset)
        {
            return Endian.ReadUInt32BE(file.Data, GetFieldOffset(file, row, columnOffset, 4));
        }

        private static bool ReadPackedBool(ExcelDataFile file, ExcelDataRow row, ushort columnOffset, int bit)
        {
            int offset = GetFieldOffset(file, row, columnOffset, 1);
            return (file.Data[offset] & (1 << bit)) != 0;
        }

        private static int GetFieldOffset(ExcelDataFile file, ExcelDataRow row, ushort columnOffset, int size)
        {
            int rowOffset = checked((int)row.Offset);
            int rowBodySize = checked((int)Endian.ReadUInt32BE(file.Data, rowOffset));
            int rowDataOffset = rowOffset + 6;
            int rowEnd = rowDataOffset + rowBodySize;
            int fieldOffset = rowDataOffset + columnOffset;
            if (fieldOffset < rowDataOffset || fieldOffset + size > rowEnd)
            {
                throw new InvalidDataException("UI image field is outside row body.");
            }

            return fieldOffset;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
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

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required file is missing.", path);
            }
        }

        private string ResolveBaseIndex(string currentPath, string origPath, string explicitPath, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string fullPath = Path.GetFullPath(explicitPath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Explicit base UI index file was not found.", fullPath);
                }

                EnsureCleanOrAllowed(fullPath, displayName);
                return fullPath;
            }

            int patchDatEntryCount = CountPatchDatEntries(currentPath);
            if (patchDatEntryCount <= 0)
            {
                return currentPath;
            }

            if (File.Exists(origPath))
            {
                int origPatchDatEntryCount = CountPatchDatEntries(origPath);
                if (origPatchDatEntryCount == 0)
                {
                    return origPath;
                }

                throw new InvalidOperationException(
                    "The selected base " + displayName + " already contains " + patchDatEntryCount +
                    " dat4 entries and orig." + displayName + " is not clean. Restore the global client or provide a clean --base-ui-index/--base-ui-index2.");
            }

            if (_options.AllowPatchedGlobal)
            {
                Console.WriteLine("WARNING: selected base {0} contains {1} dat4 entries. Experimental output only.", displayName, patchDatEntryCount);
                return currentPath;
            }

            throw new InvalidOperationException(
                "The selected base " + displayName + " already contains " + patchDatEntryCount +
                " dat4 entries, but orig." + displayName + " was not found. Restore the global client or provide a clean --base-ui-index/--base-ui-index2.");
        }

        private void EnsureCleanOrAllowed(string indexPath, string displayName)
        {
            int patchDatEntryCount = CountPatchDatEntries(indexPath);
            if (patchDatEntryCount <= 0)
            {
                return;
            }

            if (!_options.AllowPatchedGlobal)
            {
                throw new InvalidOperationException(
                    "The selected base " + displayName + " already contains " + patchDatEntryCount +
                    " dat4 entries. Use a clean base index or remove the existing patch first.");
            }

            Console.WriteLine("WARNING: selected base {0} contains {1} dat4 entries. Experimental output only.", displayName, patchDatEntryCount);
        }

        private static int CountPatchDatEntries(string indexPath)
        {
            if (indexPath.EndsWith(".index2", StringComparison.OrdinalIgnoreCase))
            {
                using (SqPackIndex2File index2 = new SqPackIndex2File(indexPath))
                {
                    Dictionary<byte, int> counts = index2.CountEntriesByDataFile();
                    int count;
                    return counts.TryGetValue(PatchDatId, out count) ? count : 0;
                }
            }

            using (SqPackIndexFile index = new SqPackIndexFile(indexPath))
            {
                Dictionary<byte, int> counts = index.CountEntriesByDataFile();
                int count;
                return counts.TryGetValue(PatchDatId, out count) ? count : 0;
            }
        }

        private sealed class IconSheetSpec
        {
            public readonly string SheetName;
            public readonly string DisplayName;
            public readonly bool UsesLanguageFolders;
            public readonly bool RequiresLanguageFlag;
            public readonly ushort LanguageFlagOffset;
            public readonly IconColumnSpec[] IconColumns;

            public IconSheetSpec(
                string sheetName,
                string displayName,
                bool usesLanguageFolders,
                bool requiresLanguageFlag,
                ushort languageFlagOffset,
                params IconColumnSpec[] iconColumns)
            {
                SheetName = sheetName;
                DisplayName = displayName;
                UsesLanguageFolders = usesLanguageFolders;
                RequiresLanguageFlag = requiresLanguageFlag;
                LanguageFlagOffset = languageFlagOffset;
                IconColumns = iconColumns;
            }
        }

        private struct IconColumnSpec
        {
            public readonly ushort Type;
            public readonly ushort Offset;
            public readonly uint[] AdditionalIconIdOffsets;

            public IconColumnSpec(ushort type, ushort offset, params uint[] additionalIconIdOffsets)
            {
                Type = type;
                Offset = offset;
                AdditionalIconIdOffsets = additionalIconIdOffsets ?? new uint[0];
            }
        }

        private sealed class IconPatchStats
        {
            public int Patched;
            public int Candidates;
            public int MissingGlobalTargets;
            public int MissingKoreanSources;
            public int Identical;
        }
    }
}
