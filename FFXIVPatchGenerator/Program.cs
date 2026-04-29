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
                Console.WriteLine("  Pages without mapping:{0}", report.PagesSkippedNoMapping);
                Console.WriteLine("  Missing source pages: {0}", report.MissingSourcePages);
                Console.WriteLine("  Missing target pages: {0}", report.MissingTargetPages);
                Console.WriteLine("  Unsupported sheets:   {0}", report.UnsupportedSheets);
                Console.WriteLine("  Font files patched:   {0}", report.FontFilesPatched);
                Console.WriteLine("  Output:               {0}", Path.GetFullPath(options.OutputPath));

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
            Console.WriteLine("  --base-index       Clean global 0a0000.win32.index to use instead of installed index.");
            Console.WriteLine("  --base-index2      Clean global 0a0000.win32.index2 to use instead of installed index2.");
            Console.WriteLine("  --include-font     Also build 000000 font patch files.");
            Console.WriteLine("  --font-only        Build only 000000 font patch files.");
            Console.WriteLine("  --font-pack-dir    Directory containing TTMPD.mpd and TTMPL.mpl for font patching.");
            Console.WriteLine("  --allow-korean-font-fallback");
            Console.WriteLine("                     Experimental: copy Korean client font resources if TTMP files are missing.");
            Console.WriteLine("  --base-font-index  Clean global 000000.win32.index to use instead of installed index.");
            Console.WriteLine("  --base-font-index2 Clean global 000000.win32.index2 to use instead of installed index2.");
            Console.WriteLine("  --allow-patched-global");
            Console.WriteLine("                     Allow using a global index that already points at dat1.");
        }

        private static BuildReport BuildFontOnly(BuildOptions options)
        {
            string globalGame = Path.GetFullPath(options.GlobalGamePath);
            string koreaGame = Path.GetFullPath(options.KoreaGamePath);
            string outputDir = Path.GetFullPath(options.OutputPath);

            RequireFile(Path.Combine(globalGame, "ffxivgame.ver"));
            EnsureOutputIsOutsideInputs(outputDir, globalGame, koreaGame);
            Directory.CreateDirectory(outputDir);
            File.Copy(Path.Combine(globalGame, "ffxivgame.ver"), Path.Combine(outputDir, "ffxivgame.ver"), true);

            BuildReport report = new BuildReport();
            ProgressReporter.Report(2, "입력 파일 확인 완료");
            ProgressReporter.Report(90, "폰트 패치 생성 중");
            new FontPatchGenerator(options, report).Build();
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
        public string TargetLanguage = "ja";
        public string SourceLanguage = "ko";
        public string OutputPath;
        public string SheetLimit;
        public string BaseIndexPath;
        public string BaseIndex2Path;
        public string BaseFontIndexPath;
        public string BaseFontIndex2Path;
        public string FontPackDir;
        public bool IncludeFont;
        public bool FontOnly;
        public bool AllowPatchedGlobal;
        public bool AllowKoreanFontFallback;

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

            if (values.TryGetValue("--font-pack-dir", out value))
            {
                options.FontPackDir = value.Trim('"');
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
        public int PagesSkippedNoMapping;
        public int MissingSourcePages;
        public int MissingTargetPages;
        public int UnsupportedSheets;
        public int FontFilesPatched;
        public readonly List<string> Warnings = new List<string>();
    }
}
