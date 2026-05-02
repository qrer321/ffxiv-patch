// 이 프로젝트는 FFXIV 한글 패치 원작자 https://github.com/korean-patch 의 작업을 참고했습니다.
// 한글 패치의 기반과 구현 흐름을 만들어주신 원작자에게 감사드립니다.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.SetError(new StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true });

                if (args.Length == 0 || HasArg(args, "--help") || HasArg(args, "-h"))
                {
                    PrintUsage();
                    return args.Length == 0 ? 1 : 0;
                }

                BuildOptions options = BuildOptions.Parse(args);
                BuildReport report;
                if (options.FontOnly)
                {
                    report = BuildFontOnly(options);
                }
                else
                {
                    report = new TextPatchGenerator(options).Build();
                }

                ProgressReporter.Report(100, "완료");

                Console.WriteLine();
                Console.WriteLine("Patch build completed.");
                Console.WriteLine("  Sheets scanned:       {0}", report.SheetsScanned);
                Console.WriteLine("  EXD pages patched:    {0}", report.PagesPatched);
                Console.WriteLine("  EXD rows patched:     {0}", report.RowsPatched);
                Console.WriteLine("  String-key rows:      {0}", report.StringKeyRowsPatched);
                Console.WriteLine("  Row-key rows:         {0}", report.RowKeyRowsPatched);
                Console.WriteLine("  Protected UI tokens:  {0}", report.ProtectedUiStrings);
                Console.WriteLine("  RSV rows:             {0}", report.RsvRows);
                Console.WriteLine("  RSV strings:          {0}", report.RsvStrings);
                Console.WriteLine("  Say quest phrases:    {0}", report.QuestChatPhrasesAnonymized);
                Console.WriteLine("  Say quest rows:       {0}", report.QuestChatRowsAnonymized);
                Console.WriteLine("  Pages without mapping:{0}", report.PagesSkippedNoMapping);
                Console.WriteLine("  Missing source pages: {0}", report.MissingSourcePages);
                Console.WriteLine("  Missing target pages: {0}", report.MissingTargetPages);
                Console.WriteLine("  Unsupported sheets:   {0}", report.UnsupportedSheets);
                Console.WriteLine("  Font files patched:   {0}", report.FontFilesPatched);
                Console.WriteLine("  Font files skipped:   {0}", report.FontFilesSkippedByProfile);
                Console.WriteLine("  UI files patched:     {0}", report.UiFilesPatched);
                Console.WriteLine("  Output:               {0}", Path.GetFullPath(options.OutputPath));
                if (!string.IsNullOrEmpty(report.DiagnosticsPath))
                {
                    Console.WriteLine("  Diagnostics:          {0}", report.DiagnosticsPath);
                }

                if (report.Warnings.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Warnings:");
                    foreach (string warning in report.Warnings)
                    {
                        Console.WriteLine("  - {0}", warning);
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Patch build failed.");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("FFXIVPatchGenerator.exe --global <global game dir> --korea <korea game dir> --target-language ja --output <dir>");
            Console.WriteLine();
            Console.WriteLine("Required:");
            Console.WriteLine("  --global           Global client game directory.");
            Console.WriteLine("  --korea            Korean client game directory.");
            Console.WriteLine("  --output           Release output directory.");
            Console.WriteLine();
            Console.WriteLine("Optional:");
            Console.WriteLine("  --target-language  Global language slot to replace. Default: ja");
            Console.WriteLine("  --source-language  Korean source language slot. Default: ko");
            Console.WriteLine("  --sheet            Limit to one root.exl sheet name for testing.");
            Console.WriteLine("  --policy           Optional JSON patch policy file.");
            Console.WriteLine("  --anonymize-quest-chat-phrases");
            Console.WriteLine("                     Replace quest /say input phrases with short ASCII tokens.");
            Console.WriteLine("  --diagnostic-csv   Export row/column comparison CSV for a sheet.");
            Console.WriteLine("  --base-index       Clean global 0a0000.win32.index to use instead of installed index.");
            Console.WriteLine("  --base-index2      Clean global 0a0000.win32.index2 to use instead of installed index2.");
            Console.WriteLine("  --include-font     Also build 000000 font patch files.");
            Console.WriteLine("  --font-only        Build only 000000 font patch files.");
            Console.WriteLine("  --font-pack-dir    Directory containing TTMPD.mpd and TTMPL.mpl for font patching.");
            Console.WriteLine("  --font-profile     Font patch profile for diagnostics. Default: full");
            Console.WriteLine("  --allow-korean-font-fallback");
            Console.WriteLine("                     Experimental: copy Korean client font resources if TTMP files are missing.");
            Console.WriteLine("  --base-font-index  Clean global 000000.win32.index to use instead of installed index.");
            Console.WriteLine("  --base-font-index2 Clean global 000000.win32.index2 to use instead of installed index2.");
            Console.WriteLine("  --skip-ui-texture-fix");
            Console.WriteLine("                     Do not build the 060000 UI texture fixes with font patches.");
            Console.WriteLine("  --base-ui-index    Clean global 060000.win32.index to use instead of installed index.");
            Console.WriteLine("  --base-ui-index2   Clean global 060000.win32.index2 to use instead of installed index2.");
            Console.WriteLine("  --allow-patched-global");
            Console.WriteLine("                     Allow using a global index that already points at dat1.");
            Console.WriteLine("  --allow-version-mismatch");
            Console.WriteLine("                     Allow global/Korean ffxivgame.ver mismatch for diagnostics.");
        }

        private static BuildReport BuildFontOnly(BuildOptions options)
        {
            string globalGame = Path.GetFullPath(options.GlobalGamePath);
            string koreaGame = Path.GetFullPath(options.KoreaGamePath);
            string outputDir = Path.GetFullPath(options.OutputPath);

            RequireFile(Path.Combine(globalGame, "ffxivgame.ver"));
            ClientVersionGuard.Validate(globalGame, koreaGame, options.AllowVersionMismatch);
            EnsureOutputIsOutsideInputs(outputDir, globalGame, koreaGame);
            Directory.CreateDirectory(outputDir);
            File.Copy(Path.Combine(globalGame, "ffxivgame.ver"), Path.Combine(outputDir, "ffxivgame.ver"), true);

            BuildReport report = new BuildReport();
            ProgressReporter.Report(2, "입력 파일 확인 완료");
            ProgressReporter.Report(90, "폰트 패치 생성 중");
            new FontPatchGenerator(options, report).Build();
            if (options.ShouldBuildUiTextureFix)
            {
                ProgressReporter.Report(98, "UI texture patch build");
                new UiPatchGenerator(options, report).Build();
            }
            return report;
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
    }

    internal static class ProgressReporter
    {
        // UI watches stdout for this token and updates the progress bar from "percent|message".
        private const string Prefix = "@@FFXIVPATCHGENERATOR_PROGRESS|";

        public static void Report(int percent, string message)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            Console.WriteLine(
                Prefix + percent.ToString() + "|" +
                (message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '));
        }
    }

    internal sealed class BuildOptions
    {
        public string GlobalGamePath;
        public string KoreaGamePath;

        // ja is the default global target because the primary workflow is Japanese client Korean patching.
        public string TargetLanguage = "ja";

        // Korean server EXD files use the ko suffix and are the only supported source language today.
        public string SourceLanguage = "ko";
        public string OutputPath;
        public string SheetLimit;
        public string PolicyPath;
        public string DiagnosticCsvSheet;
        public string BaseIndexPath;
        public string BaseIndex2Path;
        public string BaseFontIndexPath;
        public string BaseFontIndex2Path;
        public string BaseUiIndexPath;
        public string BaseUiIndex2Path;
        public string FontPackDir;
        public string FontPatchProfile = FontPatchProfiles.Default;
        public bool IncludeFont;
        public bool FontOnly;
        public bool AllowPatchedGlobal;
        public bool AllowKoreanFontFallback;
        public bool AllowVersionMismatch;
        public bool SkipUiTextureFix;
        public bool IncludeCommandSheets = true;
        public bool AnonymizeQuestChatPhrases;

        public bool ShouldBuildUiTextureFix
        {
            get { return IncludeFont && !SkipUiTextureFix; }
        }

        public static BuildOptions Parse(string[] args)
        {
            BuildOptions options = new BuildOptions();
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Unexpected argument: " + arg);
                }

                if (string.Equals(arg, "--allow-patched-global", StringComparison.OrdinalIgnoreCase))
                {
                    options.AllowPatchedGlobal = true;
                    continue;
                }

                if (string.Equals(arg, "--include-font", StringComparison.OrdinalIgnoreCase))
                {
                    options.IncludeFont = true;
                    continue;
                }

                if (string.Equals(arg, "--allow-korean-font-fallback", StringComparison.OrdinalIgnoreCase))
                {
                    options.AllowKoreanFontFallback = true;
                    continue;
                }

                if (string.Equals(arg, "--allow-version-mismatch", StringComparison.OrdinalIgnoreCase))
                {
                    options.AllowVersionMismatch = true;
                    continue;
                }

                if (string.Equals(arg, "--anonymize-quest-chat-phrases", StringComparison.OrdinalIgnoreCase))
                {
                    options.AnonymizeQuestChatPhrases = true;
                    continue;
                }

                if (string.Equals(arg, "--skip-ui-texture-fix", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--skip-partylist-ui-fix", StringComparison.OrdinalIgnoreCase))
                {
                    options.SkipUiTextureFix = true;
                    continue;
                }

                if (string.Equals(arg, "--font-only", StringComparison.OrdinalIgnoreCase))
                {
                    options.FontOnly = true;
                    options.IncludeFont = true;
                    continue;
                }

                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Missing value for " + arg);
                }

                values[arg] = args[++i];
            }

            options.GlobalGamePath = GetRequired(values, "--global");
            options.KoreaGamePath = GetRequired(values, "--korea");
            options.OutputPath = GetRequired(values, "--output");

            string value;
            if (values.TryGetValue("--target-language", out value))
            {
                options.TargetLanguage = value.ToLowerInvariant();
            }

            if (values.TryGetValue("--source-language", out value))
            {
                options.SourceLanguage = value.ToLowerInvariant();
            }

            if (values.TryGetValue("--sheet", out value))
            {
                options.SheetLimit = value.Replace('\\', '/').ToLowerInvariant();
            }

            if (values.TryGetValue("--policy", out value))
            {
                options.PolicyPath = value.Trim('"');
            }

            if (values.TryGetValue("--diagnostic-csv", out value))
            {
                options.DiagnosticCsvSheet = value.Replace('\\', '/').ToLowerInvariant();
            }

            if (values.TryGetValue("--base-index", out value))
            {
                options.BaseIndexPath = value.Trim('"');
            }

            if (values.TryGetValue("--base-index2", out value))
            {
                options.BaseIndex2Path = value.Trim('"');
            }

            if (values.TryGetValue("--base-font-index", out value))
            {
                options.BaseFontIndexPath = value.Trim('"');
            }

            if (values.TryGetValue("--base-font-index2", out value))
            {
                options.BaseFontIndex2Path = value.Trim('"');
            }

            if (values.TryGetValue("--base-ui-index", out value))
            {
                options.BaseUiIndexPath = value.Trim('"');
            }

            if (values.TryGetValue("--base-ui-index2", out value))
            {
                options.BaseUiIndex2Path = value.Trim('"');
            }

            if (values.TryGetValue("--font-pack-dir", out value))
            {
                options.FontPackDir = value.Trim('"');
            }

            if (values.TryGetValue("--font-profile", out value))
            {
                options.FontPatchProfile = FontPatchProfiles.Normalize(value.Trim('"'));
            }

            if (LanguageCodes.ToId(options.TargetLanguage) == 0)
            {
                throw new ArgumentException("Unsupported target language: " + options.TargetLanguage);
            }

            if (LanguageCodes.ToId(options.SourceLanguage) == 0)
            {
                throw new ArgumentException("Unsupported source language: " + options.SourceLanguage);
            }

            return options;
        }

        private static string GetRequired(Dictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Missing required option " + key);
            }

            return value.Trim('"');
        }
    }

    internal sealed class BuildReport
    {
        public int SheetsScanned;
        public int PagesPatched;
        public int RowsPatched;
        public int StringKeyRowsPatched;
        public int RowKeyRowsPatched;
        public int ProtectedUiStrings;
        public int RsvRows;
        public int RsvStrings;
        public int QuestChatPhrasesAnonymized;
        public int QuestChatRowsAnonymized;
        public int PagesSkippedNoMapping;
        public int MissingSourcePages;
        public int MissingTargetPages;
        public int UnsupportedSheets;
        public int FontFilesPatched;
        public int FontFilesSkippedByProfile;
        public int UiFilesPatched;
        public string DiagnosticsPath;
        public readonly List<string> Warnings = new List<string>();
    }

    internal static class ClientVersionGuard
    {
        private const string VersionFileName = "ffxivgame.ver";

        public static void Validate(string globalGame, string koreaGame, bool allowVersionMismatch)
        {
            string globalVersion = ReadVersion(globalGame);
            string koreaVersion = ReadVersion(koreaGame);
            if (string.Equals(globalVersion, koreaVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string message = "Global and Korean client versions do not match. " +
                "Global: " + globalVersion + ", Korean: " + koreaVersion + ". " +
                "Row-id fallback patching is unsafe across different game versions.";

            if (!allowVersionMismatch)
            {
                throw new InvalidOperationException(message + " Use --allow-version-mismatch only for diagnostics.");
            }

            Console.WriteLine("WARNING: " + message);
        }

        private static string ReadVersion(string gameDir)
        {
            string path = Path.Combine(gameDir, VersionFileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required version file is missing.", path);
            }

            string version = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(version))
            {
                throw new InvalidDataException("Version file is empty: " + path);
            }

            return version;
        }
    }
}
