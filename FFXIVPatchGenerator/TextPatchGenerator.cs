using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            // Content-specific dialogue sheets under custom/... usually lack string keys.
            // On the same game version, Korean/global row ids are expected to line up.
            new Regex("^custom/.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
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
            string diagnosticsPath = Path.Combine(outputDir, "patch-diagnostics.tsv");
            _report.DiagnosticsPath = diagnosticsPath;

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
            using (StreamWriter diagnostics = new StreamWriter(diagnosticsPath, false, new UTF8Encoding(false)))
            {
                diagnostics.WriteLine("sheet\tpage\tstatus\trows\tstringKeyRows\trowKeyRows\tnote");
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
                    ProcessSheet(sheetName, globalArchive, koreaArchive, mutableIndex, mutableIndex2, datWriter, diagnostics, targetLanguageId, sourceLanguageId);
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
            StreamWriter diagnostics,
            byte targetLanguageId,
            byte sourceLanguageId)
        {
            _report.SheetsScanned++;

            string headerPath = "exd/" + sheetName + ".exh";
            byte[] globalHeaderBytes;
            if (!globalArchive.TryReadFile(headerPath, out globalHeaderBytes))
            {
                AddLimitedWarning("Missing global EXH: " + headerPath);
                WriteDiagnostic(diagnostics, sheetName, "-", "missing-global-exh", 0, 0, 0, headerPath);
                return;
            }

            ExcelHeader globalHeader = ExcelHeader.Parse(globalHeaderBytes);
            bool targetUsesLanguageSuffix = globalHeader.HasLanguage(targetLanguageId);
            bool targetUsesNeutralPath = !targetUsesLanguageSuffix && HasAnyPageFile(globalArchive, sheetName, globalHeader, null);
            if (!targetUsesLanguageSuffix && !targetUsesNeutralPath)
            {
                WriteDiagnostic(diagnostics, sheetName, "-", "missing-target-language", 0, 0, 0, _options.TargetLanguage);
                return;
            }

            List<int> stringColumns = globalHeader.GetStringColumnIndexes();
            if (stringColumns.Count == 0)
            {
                WriteDiagnostic(diagnostics, sheetName, "-", "no-string-columns", 0, 0, 0, string.Empty);
                return;
            }

            if (globalHeader.Variant != ExcelVariant.Default)
            {
                _report.UnsupportedSheets++;
                AddLimitedWarning("Skipped unsupported EXH variant " + globalHeader.Variant + ": " + sheetName);
                WriteDiagnostic(diagnostics, sheetName, "-", "unsupported-variant", 0, 0, 0, globalHeader.Variant.ToString());
                return;
            }

            if (IsKnownUnsafeSheet(sheetName))
            {
                AddLimitedWarning("Skipped known unsafe sheet: " + sheetName);
                WriteDiagnostic(diagnostics, sheetName, "-", "known-unsafe-skip", 0, 0, 0, string.Empty);
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

            if (sourceHeader.Variant != ExcelVariant.Default)
            {
                sourceHeader = globalHeader;
                WriteDiagnostic(diagnostics, sheetName, "-", "source-header-fallback", 0, 0, 0, "Korean EXH variant was not Default");
            }

            bool sourceUsesLanguageSuffix = sourceHeader.HasLanguage(sourceLanguageId);
            bool sourceUsesNeutralPath = !sourceUsesLanguageSuffix && HasAnyPageFile(koreaArchive, sheetName, sourceHeader, null);
            if (!sourceUsesLanguageSuffix && !sourceUsesNeutralPath)
            {
                WriteDiagnostic(diagnostics, sheetName, "-", "missing-source-language", 0, 0, 0, _options.SourceLanguage);
                return;
            }

            bool allowRowKeyFallback = IsRowKeyFallbackAllowed(sheetName);
            List<ExcelDataFile> sourceExds = LoadSourcePages(sheetName, sourceHeader, koreaArchive, diagnostics, sourceUsesLanguageSuffix);
            if (sourceExds.Count == 0)
            {
                _report.MissingSourcePages += globalHeader.Pages.Count;
                WriteDiagnostic(diagnostics, sheetName, "-", "missing-all-source-pages", 0, 0, 0, _options.SourceLanguage);
                return;
            }

            ExdSourceMaps sourceMaps = ExdStringPatcher.BuildSourceMaps(sourceExds, sourceHeader, allowRowKeyFallback);
            StringPatchPolicy patchPolicy = IsAddonSheet(sheetName)
                ? StringPatchPolicy.ProtectAddonUiGlyphs
                : StringPatchPolicy.Default;

            for (int i = 0; i < globalHeader.Pages.Count; i++)
            {
                ExcelPageDefinition page = globalHeader.Pages[i];
                string targetPath = BuildExdPath(sheetName, page.StartId, targetUsesLanguageSuffix ? _options.TargetLanguage : null);

                byte[] targetExdBytes;
                if (!globalArchive.TryReadFile(targetPath, out targetExdBytes))
                {
                    _report.MissingTargetPages++;
                    WriteDiagnostic(diagnostics, sheetName, page.StartId.ToString(), "missing-target-page", 0, 0, 0, targetPath);
                    continue;
                }

                ExcelDataFile targetExd = ExcelDataFile.Parse(targetExdBytes);
                ExdPatchResult patchResult = ExdStringPatcher.PatchDefaultVariant(
                    targetExd,
                    globalHeader,
                    sourceHeader,
                    stringColumns,
                    sourceMaps,
                    allowRowKeyFallback,
                    patchPolicy);
                _report.ProtectedUiStrings += patchResult.ProtectedUiStrings;

                if (!patchResult.Changed)
                {
                    _report.PagesSkippedNoMapping++;
                    WriteDiagnostic(
                        diagnostics,
                        sheetName,
                        page.StartId.ToString(),
                        "no-mapping",
                        0,
                        0,
                        0,
                        BuildPatchNote(allowRowKeyFallback ? "row-key fallback allowed" : "row-key fallback not allowed", patchResult.ProtectedUiStrings));
                    continue;
                }

                long datOffset = datWriter.WriteStandardFile(patchResult.Data);
                mutableIndex.SetFileOffset(targetPath, 1, datOffset);
                mutableIndex2.SetFileOffset(targetPath, 1, datOffset);
                _report.PagesPatched++;
                _report.RowsPatched += patchResult.RowsPatched;
                _report.StringKeyRowsPatched += patchResult.StringKeyRows;
                _report.RowKeyRowsPatched += patchResult.RowKeyRows;
                WriteDiagnostic(
                    diagnostics,
                    sheetName,
                    page.StartId.ToString(),
                    "patched",
                    patchResult.RowsPatched,
                    patchResult.StringKeyRows,
                    patchResult.RowKeyRows,
                    BuildPatchNote(string.Empty, patchResult.ProtectedUiStrings));

                if (_report.PagesPatched % 100 == 0)
                {
                    Console.WriteLine("  Patched EXD pages: {0}", _report.PagesPatched);
                }
            }
        }

        private List<ExcelDataFile> LoadSourcePages(
            string sheetName,
            ExcelHeader sourceHeader,
            SqPackArchive koreaArchive,
            StreamWriter diagnostics,
            bool sourceUsesLanguageSuffix)
        {
            List<ExcelDataFile> sourceExds = new List<ExcelDataFile>();
            for (int i = 0; i < sourceHeader.Pages.Count; i++)
            {
                ExcelPageDefinition page = sourceHeader.Pages[i];
                string sourcePath = BuildExdPath(sheetName, page.StartId, sourceUsesLanguageSuffix ? _options.SourceLanguage : null);
                byte[] sourceExdBytes;
                if (!koreaArchive.TryReadFile(sourcePath, out sourceExdBytes))
                {
                    _report.MissingSourcePages++;
                    WriteDiagnostic(diagnostics, sheetName, page.StartId.ToString(), "missing-source-page", 0, 0, 0, sourcePath);
                    continue;
                }

                try
                {
                    sourceExds.Add(ExcelDataFile.Parse(sourceExdBytes));
                }
                catch (Exception exception)
                {
                    _report.MissingSourcePages++;
                    AddLimitedWarning("Invalid Korean EXD: " + sourcePath);
                    WriteDiagnostic(diagnostics, sheetName, page.StartId.ToString(), "invalid-source-page", 0, 0, 0, exception.Message);
                }
            }

            return sourceExds;
        }

        private static string BuildExdPath(string sheetName, uint startId, string languageCode)
        {
            string path = "exd/" + sheetName + "_" + startId.ToString();
            if (!string.IsNullOrEmpty(languageCode))
            {
                path += "_" + languageCode;
            }

            return path + ".exd";
        }

        private static bool HasAnyPageFile(
            SqPackArchive archive,
            string sheetName,
            ExcelHeader header,
            string languageCode)
        {
            for (int i = 0; i < header.Pages.Count; i++)
            {
                byte[] ignored;
                if (archive.TryReadFile(BuildExdPath(sheetName, header.Pages[i].StartId, languageCode), out ignored))
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteDiagnostic(
            StreamWriter diagnostics,
            string sheetName,
            string page,
            string status,
            int rows,
            int stringKeyRows,
            int rowKeyRows,
            string note)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.Write(EscapeTsv(sheetName));
            diagnostics.Write('\t');
            diagnostics.Write(EscapeTsv(page));
            diagnostics.Write('\t');
            diagnostics.Write(EscapeTsv(status));
            diagnostics.Write('\t');
            diagnostics.Write(rows.ToString());
            diagnostics.Write('\t');
            diagnostics.Write(stringKeyRows.ToString());
            diagnostics.Write('\t');
            diagnostics.Write(rowKeyRows.ToString());
            diagnostics.Write('\t');
            diagnostics.WriteLine(EscapeTsv(note));
        }

        private static string EscapeTsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        private static bool IsKnownUnsafeSheet(string sheetName)
        {
            return sheetName.IndexOf("CtsMycEntrance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sheetName.IndexOf("CtsErkKuganeEntrance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAddonSheet(string sheetName)
        {
            return string.Equals(sheetName, "Addon", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPatchNote(string baseNote, int protectedUiStrings)
        {
            if (protectedUiStrings <= 0)
            {
                return baseNote;
            }

            string protectedNote = "protected-ui-tokens=" + protectedUiStrings.ToString();
            if (string.IsNullOrEmpty(baseNote))
            {
                return protectedNote;
            }

            return baseNote + "; " + protectedNote;
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
