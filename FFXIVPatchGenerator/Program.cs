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
                BuildReport report = new TextPatchGenerator(options).Build();
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
            Console.WriteLine("  --include-font     Also build 000000 font patch files.");
            Console.WriteLine("  --base-font-index  Clean global 000000.win32.index to use instead of installed index.");
            Console.WriteLine("  --allow-patched-global");
            Console.WriteLine("                     Allow using a global index that already points at dat1.");
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
        public string BaseFontIndexPath;
        public bool IncludeFont;
        public bool AllowPatchedGlobal;

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

            if (values.TryGetValue("--base-font-index", out value))
            {
                options.BaseFontIndexPath = value.Trim('"');
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
