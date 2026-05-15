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
                string globalTextGame = string.IsNullOrWhiteSpace(options.GlobalTextGamePath)
                    ? globalGame
                    : Path.GetFullPath(options.GlobalTextGamePath);
                string globalFontGame = string.IsNullOrWhiteSpace(options.GlobalFontGamePath)
                    ? globalGame
                    : Path.GetFullPath(options.GlobalFontGamePath);
                string globalUiGame = string.IsNullOrWhiteSpace(options.GlobalUiGamePath)
                    ? globalGame
                    : Path.GetFullPath(options.GlobalUiGamePath);
                string globalTextSqpack = Path.Combine(globalTextGame, "sqpack", "ffxiv");
                string globalFontSqpack = Path.Combine(globalFontGame, "sqpack", "ffxiv");
                string globalUiSqpack = Path.Combine(globalUiGame, "sqpack", "ffxiv");
                string koreaSqpack = string.IsNullOrWhiteSpace(options.KoreaGamePath)
                    ? null
                    : Path.Combine(Path.GetFullPath(options.KoreaGamePath), "sqpack", "ffxiv");
                string appliedSqpack = string.IsNullOrWhiteSpace(options.AppliedGamePath)
                    ? output
                    : Path.Combine(Path.GetFullPath(options.AppliedGamePath), "sqpack", "ffxiv");
                string language = string.IsNullOrWhiteSpace(options.TargetLanguage) ? "ja" : options.TargetLanguage;
                string glyphDumpDir = options.NoGlyphDump
                    ? null
                    : (string.IsNullOrWhiteSpace(options.GlyphDumpDir)
                        ? Path.Combine(output, DefaultGlyphDumpFolderName)
                        : Path.GetFullPath(options.GlyphDumpDir));

                bool compareAppliedOutput = !string.IsNullOrWhiteSpace(options.AppliedGamePath);
                Verifier verifier = new Verifier(
                    output,
                    appliedSqpack,
                    globalTextSqpack,
                    globalFontSqpack,
                    globalUiSqpack,
                    koreaSqpack,
                    language,
                    glyphDumpDir,
                    options.Checks,
                    options.FontPackDir,
                    compareAppliedOutput);
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
            Console.WriteLine("PatchRouteVerifier.exe --output <patch-output-dir> --global <global-game-dir> [--global-text <game-dir>] [--global-font <game-dir>] [--global-ui <game-dir>] [--korea <korean-game-dir>] [--applied-game <game-dir>] [--target-language ja] [--font-pack-dir <dir>] [--glyph-dump-dir <dir>] [--no-glyph-dump] [--checks <name[,name]>]");
        }

    }
}
