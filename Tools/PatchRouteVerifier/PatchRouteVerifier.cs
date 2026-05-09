using System;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Options options = Options.Parse(args);
                if (options.ShowHelp)
                {
                    PrintUsage();
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(options.OutputPath) ||
                    string.IsNullOrWhiteSpace(options.GlobalGamePath))
                {
                    PrintUsage();
                    return 2;
                }

                string output = Path.GetFullPath(options.OutputPath);
                string globalGame = Path.GetFullPath(options.GlobalGamePath);
                string globalSqpack = Path.Combine(globalGame, "sqpack", "ffxiv");
                string appliedSqpack = string.IsNullOrWhiteSpace(options.AppliedGamePath)
                    ? output
                    : Path.Combine(Path.GetFullPath(options.AppliedGamePath), "sqpack", "ffxiv");
                string language = string.IsNullOrWhiteSpace(options.TargetLanguage) ? "ja" : options.TargetLanguage;
                string glyphDumpDir = options.NoGlyphDump
                    ? null
                    : (string.IsNullOrWhiteSpace(options.GlyphDumpDir)
                        ? Path.Combine(output, DefaultGlyphDumpFolderName)
                        : Path.GetFullPath(options.GlyphDumpDir));

                Verifier verifier = new Verifier(output, appliedSqpack, globalSqpack, language, glyphDumpDir, options.Checks, options.FontPackDir);
                verifier.Run();
                return verifier.Failed ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("verification failed with exception");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("PatchRouteVerifier.exe --output <patch-output-dir> --global <global-game-dir> [--applied-game <game-dir>] [--target-language ja] [--font-pack-dir <dir>] [--glyph-dump-dir <dir>] [--no-glyph-dump] [--checks <name[,name]>]");
        }

    }
}
