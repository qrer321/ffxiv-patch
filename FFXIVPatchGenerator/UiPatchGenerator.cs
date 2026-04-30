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
        private const string ScreenImageExhPath = "exd/screenimage.exh";
        private const string ScreenImageExdPrefix = "exd/screenimage_";
        private const byte PatchDatId = 4;
        private const uint PatchDataFileCount = PatchDatId + 1u;
        private const string OrigIndexFileName = "orig.060000.win32.index";
        private const string OrigIndex2FileName = "orig.060000.win32.index2";
        private const string PartyListTargetBaseTexPath = "ui/uld/PartyListTargetBase.tex";
        private const ushort ScreenImageIconIdOffset = 0;
        private const ushort ScreenImageLangFlagOffset = 11;
        private const int ScreenImageLangFlagBit = 0;

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
                patched += CopyRequiredUiTexture(
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter,
                    PartyListTargetBaseTexPath,
                    PartyListTargetBaseTexPath,
                    "Korean PartyList target texture");
                patched += CopyLocalizedScreenImages(
                    globalExcelArchive,
                    globalUiArchive,
                    koreaUiArchive,
                    mutableIndex,
                    mutableIndex2,
                    datWriter);

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

        private int CopyLocalizedScreenImages(
            SqPackArchive globalExcelArchive,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter)
        {
            byte[] headerBytes;
            if (!globalExcelArchive.TryReadFile(ScreenImageExhPath, out headerBytes))
            {
                _report.Warnings.Add("ScreenImage EXH was not found. Area/title image localization was skipped.");
                return 0;
            }

            ExcelHeader header = ExcelHeader.Parse(headerBytes);
            if (header.Variant != ExcelVariant.Default ||
                !HasColumn(header, 7, ScreenImageIconIdOffset) ||
                !HasColumn(header, 25, ScreenImageLangFlagOffset))
            {
                _report.Warnings.Add("ScreenImage schema was not recognized. Area/title image localization was skipped.");
                return 0;
            }

            HashSet<string> copiedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int patched = 0;
            int candidates = 0;
            int missingGlobalTargets = 0;
            int missingKoreanSources = 0;
            int identical = 0;

            for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
            {
                ExcelPageDefinition page = header.Pages[pageIndex];
                string exdPath = ScreenImageExdPrefix + page.StartId.ToString() + ".exd";
                byte[] exdBytes;
                if (!globalExcelArchive.TryReadFile(exdPath, out exdBytes))
                {
                    _report.Warnings.Add("ScreenImage page was not found and was skipped: " + exdPath);
                    continue;
                }

                ExcelDataFile dataFile = ExcelDataFile.Parse(exdBytes);
                for (int rowIndex = 0; rowIndex < dataFile.Rows.Count; rowIndex++)
                {
                    ExcelDataRow row = dataFile.Rows[rowIndex];
                    if (!ReadPackedBool(dataFile, row, ScreenImageLangFlagOffset, ScreenImageLangFlagBit))
                    {
                        continue;
                    }

                    uint iconId = ReadUInt32(dataFile, row, ScreenImageIconIdOffset);
                    if (iconId == 0)
                    {
                        continue;
                    }

                    patched += CopyLocalizedScreenImageVariant(
                        iconId,
                        ".tex",
                        copiedTargets,
                        globalUiArchive,
                        koreaUiArchive,
                        mutableIndex,
                        mutableIndex2,
                        datWriter,
                        ref candidates,
                        ref missingGlobalTargets,
                        ref missingKoreanSources,
                        ref identical);
                    patched += CopyLocalizedScreenImageVariant(
                        iconId,
                        "_hr1.tex",
                        copiedTargets,
                        globalUiArchive,
                        koreaUiArchive,
                        mutableIndex,
                        mutableIndex2,
                        datWriter,
                        ref candidates,
                        ref missingGlobalTargets,
                        ref missingKoreanSources,
                        ref identical);
                }
            }

            Console.WriteLine(
                "Localized ScreenImage textures patched: {0} (candidates={1}, identical={2}, missingGlobal={3}, missingKorea={4})",
                patched,
                candidates,
                identical,
                missingGlobalTargets,
                missingKoreanSources);

            if (patched == 0)
            {
                _report.Warnings.Add("No localized ScreenImage textures were patched. Check target/source language folders.");
            }

            return patched;
        }

        private int CopyLocalizedScreenImageVariant(
            uint iconId,
            string suffix,
            HashSet<string> copiedTargets,
            SqPackArchive globalUiArchive,
            SqPackArchive koreaUiArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            ref int candidates,
            ref int missingGlobalTargets,
            ref int missingKoreanSources,
            ref int identical)
        {
            string targetPath = BuildLocalizedIconPath(iconId, _options.TargetLanguage, suffix);
            if (!copiedTargets.Add(targetPath))
            {
                return 0;
            }

            candidates++;
            byte[] globalPacked;
            if (!globalUiArchive.TryReadPackedFile(targetPath, out globalPacked))
            {
                missingGlobalTargets++;
                return 0;
            }

            string sourcePath = BuildLocalizedIconPath(iconId, _options.SourceLanguage, suffix);
            byte[] koreaPacked;
            if (!koreaUiArchive.TryReadPackedFile(sourcePath, out koreaPacked))
            {
                missingKoreanSources++;
                return 0;
            }

            if (BytesEqual(globalPacked, koreaPacked))
            {
                identical++;
                return 0;
            }

            long offset = datWriter.WritePackedFile(koreaPacked);
            mutableIndex.SetFileOffset(targetPath, PatchDatId, offset);
            mutableIndex2.SetFileOffset(targetPath, PatchDatId, offset);
            return 1;
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
                throw new InvalidDataException("ScreenImage field is outside row body.");
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
    }
}
