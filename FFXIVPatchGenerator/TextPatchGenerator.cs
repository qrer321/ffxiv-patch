using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class TextPatchGenerator
    {
        // FFXIV sqpack repository that contains Excel/EXD text data.
        private const string RepositoryDir = "sqpack\\ffxiv";

        // 0a0000 is the Excel package. The patch writes a new dat1 and points selected index entries to it.
        private const string IndexFileName = "0a0000.win32.index";
        private const string Index2FileName = "0a0000.win32.index2";
        private const string Dat0FileName = "0a0000.win32.dat0";
        private const string Dat1FileName = "0a0000.win32.dat1";

        // Clean index copies used by the UI for rollback without deleting dat1 manually.
        private const string OrigIndexFileName = "orig.0a0000.win32.index";
        private const string OrigIndex2FileName = "orig.0a0000.win32.index2";
        private const string VersionFileName = "ffxivgame.ver";

        // Some sheets do not expose stable string keys, but their Korean/global rows line up by row id.
        // Keep this allowlist explicit so risky row-id swaps are not applied to every sheet.
        private static readonly Regex[] RowKeySwappableSheets = new Regex[]
        {
            new Regex("^Achievement.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Addon$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Action.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^AttackType$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Attributive$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Balloon$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^BgcArmy.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Beast.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Buddy.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Chocobo.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^ClassJob.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^CollectablesShop.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Companion.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Company.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^CompleteJournal.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Content.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Craft.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^CustomTalk$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^DefaultTalk$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Description.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Emote.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^ENpcResident$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^EObjName$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Fate.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^FC.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GcArmy.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GC.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GeneralAction$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GilShop$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Gimmick.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GoldSaucer.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GrandCompany$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^GroupPoseFrame$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Guildleve.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Housing.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Item.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^JobDef.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Journal.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Leve.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Lobby$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^LogMessage$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^MainCommand.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Minion.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^MonsterNote$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Mount.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^NpcYell$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^OnlineStatus$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^PartyContent.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Perform$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Pet.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^PlaceName$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^PublicContent.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^QTE$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Quest.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Race$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Retainer.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^SpecialShop.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Status$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Title$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^TopicSelect$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Town$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Trait.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Treasure$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Tribe$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^TripleTriad.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Voice.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^Weather$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("^WeeklyBingoText$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        private readonly BuildOptions _options;
        private readonly BuildReport _report = new BuildReport();

        public TextPatchGenerator(BuildOptions options)
        {
            _options = options;
        }

        public BuildReport Build()
        {
            string globalGame = Path.GetFullPath(_options.GlobalGamePath);
            string koreaGame = Path.GetFullPath(_options.KoreaGamePath);
            string outputDir = Path.GetFullPath(_options.OutputPath);
            string globalSqpack = Path.Combine(globalGame, RepositoryDir);
            string koreaSqpack = Path.Combine(koreaGame, RepositoryDir);

            ValidateInput(globalGame, globalSqpack, koreaGame, koreaSqpack);
            EnsureOutputIsOutsideInputs(outputDir, globalGame, koreaGame);
            Directory.CreateDirectory(outputDir);
            ProgressReporter.Report(2, "입력 파일 확인 완료");

            string currentGlobalIndex = Path.Combine(globalSqpack, IndexFileName);
            string originalGlobalIndex = Path.Combine(globalSqpack, OrigIndexFileName);
            string baseIndex = ResolveBaseIndex(currentGlobalIndex, originalGlobalIndex);
            string currentGlobalIndex2 = Path.Combine(globalSqpack, Index2FileName);
            string originalGlobalIndex2 = Path.Combine(globalSqpack, OrigIndex2FileName);
            string baseIndex2 = ResolveBaseIndex2(currentGlobalIndex2, originalGlobalIndex2, baseIndex);

            string outputIndex = Path.Combine(outputDir, IndexFileName);
            string outputIndex2 = Path.Combine(outputDir, Index2FileName);
            string outputOrigIndex = Path.Combine(outputDir, OrigIndexFileName);
            string outputOrigIndex2 = Path.Combine(outputDir, OrigIndex2FileName);
            string outputDat1 = Path.Combine(outputDir, Dat1FileName);

            File.Copy(baseIndex, outputOrigIndex, true);
            File.Copy(baseIndex, outputIndex, true);
            File.Copy(baseIndex2, outputOrigIndex2, true);
            File.Copy(baseIndex2, outputIndex2, true);
            File.Copy(Path.Combine(globalGame, VersionFileName), Path.Combine(outputDir, VersionFileName), true);

            Console.WriteLine("Using base global index: {0}", baseIndex);
            Console.WriteLine("Using base global index2:{0}", baseIndex2);
            Console.WriteLine("Writing output:          {0}", outputDir);

            byte targetLanguageId = LanguageCodes.ToId(_options.TargetLanguage);
            byte sourceLanguageId = LanguageCodes.ToId(_options.SourceLanguage);

            using (SqPackArchive globalArchive = new SqPackArchive(baseIndex, globalSqpack, "0a0000.win32"))
            using (SqPackArchive koreaArchive = new SqPackArchive(Path.Combine(koreaSqpack, IndexFileName), koreaSqpack, "0a0000.win32"))
            using (SqPackIndexFile mutableIndex = new SqPackIndexFile(outputIndex))
            using (SqPackIndex2File mutableIndex2 = new SqPackIndex2File(outputIndex2))
            using (SqPackDatWriter datWriter = new SqPackDatWriter(outputDat1, Path.Combine(globalSqpack, Dat0FileName)))
            {
                mutableIndex.EnsureDataFileCount(2);
                mutableIndex2.EnsureDataFileCount(2);

                byte[] rootBytes = globalArchive.ReadFile("exd/root.exl");
                List<string> sheetNames = ExcelRootList.Parse(rootBytes);
                int totalSheets = CountTargetSheets(sheetNames);
                int processedSheets = 0;
                ProgressReporter.Report(5, "시트 목록 로딩 완료");

                for (int i = 0; i < sheetNames.Count; i++)
                {
                    string sheetName = sheetNames[i];
                    if (!string.IsNullOrEmpty(_options.SheetLimit) &&
                        !string.Equals(sheetName, _options.SheetLimit, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    processedSheets++;
                    ProgressReporter.Report(
                        5 + (processedSheets - 1) * 85 / totalSheets,
                        "EXD 처리 중: " + sheetName + " (" + processedSheets + "/" + totalSheets + ")");
                    ProcessSheet(sheetName, globalArchive, koreaArchive, mutableIndex, mutableIndex2, datWriter, targetLanguageId, sourceLanguageId);
                }

                mutableIndex.Save();
                mutableIndex2.Save();
            }

            if (_options.IncludeFont)
            {
                ProgressReporter.Report(90, "폰트 패치 생성 중");
                new FontPatchGenerator(_options, _report).Build();
            }
            else
            {
                ProgressReporter.Report(95, "텍스트 패치 저장 완료");
            }

            return _report;
        }

        private int CountTargetSheets(List<string> sheetNames)
        {
            int total = 0;
            for (int i = 0; i < sheetNames.Count; i++)
            {
                if (!string.IsNullOrEmpty(_options.SheetLimit) &&
                    !string.Equals(sheetNames[i], _options.SheetLimit, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                total++;
            }

            return total == 0 ? 1 : total;
        }

        private static void ValidateInput(string globalGame, string globalSqpack, string koreaGame, string koreaSqpack)
        {
            RequireFile(Path.Combine(globalGame, VersionFileName));
            RequireFile(Path.Combine(globalSqpack, IndexFileName));
            RequireFile(Path.Combine(globalSqpack, Index2FileName));
            RequireFile(Path.Combine(globalSqpack, Dat0FileName));
            RequireFile(Path.Combine(koreaSqpack, IndexFileName));
            RequireFile(Path.Combine(koreaSqpack, Dat0FileName));
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required file is missing.", path);
            }
        }

        private static void EnsureOutputIsOutsideInputs(string outputDir, string globalGame, string koreaGame)
        {
            string output = NormalizeDirectory(outputDir);
            string global = NormalizeDirectory(globalGame);
            string korea = NormalizeDirectory(koreaGame);

            if (IsSameOrChild(output, global) || IsSameOrChild(output, korea))
            {
                throw new InvalidOperationException("--output must not be inside either source game directory. Choose a directory under E:\\codex or another staging path.");
            }
        }

        private static string NormalizeDirectory(string path)
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full + Path.DirectorySeparatorChar;
        }

        private static bool IsSameOrChild(string path, string parent)
        {
            return path.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveBaseIndex(string currentGlobalIndex, string originalGlobalIndex)
        {
            string baseIndex;
            bool explicitBaseIndex = !string.IsNullOrEmpty(_options.BaseIndexPath);
            bool foundOrigIndex = File.Exists(originalGlobalIndex);

            if (explicitBaseIndex)
            {
                baseIndex = Path.GetFullPath(_options.BaseIndexPath);
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
                            "The selected base 0a0000.win32.index already contains " + dat1Count +
                            " dat1 entries. Use a clean client, restore the original index, or pass --base-index <clean index>. " +
                            "Use --allow-patched-global only for experiments.");
                    }

                    Console.WriteLine("WARNING: selected base 0a0000.win32.index contains {0} dat1 entries. Experimental output only.", dat1Count);
                }
            }

            return baseIndex;
        }

        private string ResolveBaseIndex2(string currentGlobalIndex2, string originalGlobalIndex2, string baseIndex)
        {
            string baseIndex2 = null;

            if (!string.IsNullOrEmpty(_options.BaseIndex2Path))
            {
                baseIndex2 = Path.GetFullPath(_options.BaseIndex2Path);
                RequireFile(baseIndex2);
            }
            else if (!string.IsNullOrEmpty(_options.BaseIndexPath))
            {
                string sibling = _options.BaseIndexPath.Trim('"') + "2";
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
                            "The selected base 0a0000.win32.index2 already contains " + dat1Count +
                            " dat1 entries. Use a clean client, restore the original index2, or pass --base-index2 <clean index2>. " +
                            "Use --allow-patched-global only for experiments.");
                    }

                    Console.WriteLine("WARNING: selected base 0a0000.win32.index2 contains {0} dat1 entries. Experimental output only.", dat1Count);
                }
            }

            return baseIndex2;
        }

        private void ProcessSheet(
            string sheetName,
            SqPackArchive globalArchive,
            SqPackArchive koreaArchive,
            SqPackIndexFile mutableIndex,
            SqPackIndex2File mutableIndex2,
            SqPackDatWriter datWriter,
            byte targetLanguageId,
            byte sourceLanguageId)
        {
            _report.SheetsScanned++;

            string headerPath = "exd/" + sheetName + ".exh";
            byte[] globalHeaderBytes;
            if (!globalArchive.TryReadFile(headerPath, out globalHeaderBytes))
            {
                AddLimitedWarning("Missing global EXH: " + headerPath);
                return;
            }

            ExcelHeader globalHeader = ExcelHeader.Parse(globalHeaderBytes);
            if (!globalHeader.HasLanguage(targetLanguageId))
            {
                return;
            }

            if (globalHeader.Variant != ExcelVariant.Default)
            {
                _report.UnsupportedSheets++;
                AddLimitedWarning("Skipped unsupported EXH variant " + globalHeader.Variant + ": " + sheetName);
                return;
            }

            List<int> stringColumns = globalHeader.GetStringColumnIndexes();
            if (stringColumns.Count == 0)
            {
                return;
            }

            if (IsKnownUnsafeSheet(sheetName))
            {
                AddLimitedWarning("Skipped known unsafe sheet: " + sheetName);
                return;
            }

            ExcelHeader sourceHeader = globalHeader;
            byte[] sourceHeaderBytes;
            if (koreaArchive.TryReadFile(headerPath, out sourceHeaderBytes))
            {
                try
                {
                    sourceHeader = ExcelHeader.Parse(sourceHeaderBytes);
                }
                catch
                {
                    sourceHeader = globalHeader;
                }
            }

            for (int i = 0; i < globalHeader.Pages.Count; i++)
            {
                ExcelPageDefinition page = globalHeader.Pages[i];
                string targetPath = "exd/" + sheetName + "_" + page.StartId.ToString() + "_" + _options.TargetLanguage + ".exd";
                string sourcePath = "exd/" + sheetName + "_" + page.StartId.ToString() + "_" + _options.SourceLanguage + ".exd";

                byte[] targetExdBytes;
                if (!globalArchive.TryReadFile(targetPath, out targetExdBytes))
                {
                    _report.MissingTargetPages++;
                    continue;
                }

                byte[] sourceExdBytes;
                if (!koreaArchive.TryReadFile(sourcePath, out sourceExdBytes))
                {
                    _report.MissingSourcePages++;
                    continue;
                }

                ExcelDataFile targetExd = ExcelDataFile.Parse(targetExdBytes);
                ExcelDataFile sourceExd = ExcelDataFile.Parse(sourceExdBytes);
                bool allowRowKeyFallback = IsRowKeyFallbackAllowed(sheetName);
                ExdPatchResult patchResult = ExdStringPatcher.PatchDefaultVariant(
                    targetExd,
                    sourceExd,
                    globalHeader,
                    sourceHeader,
                    stringColumns,
                    allowRowKeyFallback);

                if (!patchResult.Changed)
                {
                    _report.PagesSkippedNoMapping++;
                    continue;
                }

                long datOffset = datWriter.WriteStandardFile(patchResult.Data);
                mutableIndex.SetFileOffset(targetPath, 1, datOffset);
                mutableIndex2.SetFileOffset(targetPath, 1, datOffset);
                _report.PagesPatched++;
                _report.RowsPatched += patchResult.RowsPatched;
                _report.StringKeyRowsPatched += patchResult.StringKeyRows;
                _report.RowKeyRowsPatched += patchResult.RowKeyRows;

                if (_report.PagesPatched % 100 == 0)
                {
                    Console.WriteLine("  Patched EXD pages: {0}", _report.PagesPatched);
                }
            }
        }

        private static bool IsKnownUnsafeSheet(string sheetName)
        {
            return sheetName.IndexOf("CtsMycEntrance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sheetName.IndexOf("CtsErkKuganeEntrance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRowKeyFallbackAllowed(string sheetName)
        {
            for (int i = 0; i < RowKeySwappableSheets.Length; i++)
            {
                if (RowKeySwappableSheets[i].IsMatch(sheetName))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddLimitedWarning(string message)
        {
            if (_report.Warnings.Count < 30)
            {
                _report.Warnings.Add(message);
            }
        }
    }
}
