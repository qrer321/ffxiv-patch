using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

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

                Verifier verifier = new Verifier(output, appliedSqpack, globalSqpack, language, glyphDumpDir, options.Checks);
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
            Console.WriteLine("PatchRouteVerifier.exe --output <patch-output-dir> --global <global-game-dir> [--applied-game <game-dir>] [--target-language ja] [--glyph-dump-dir <dir>] [--no-glyph-dump] [--checks <name[,name]>]");
        }

        private sealed partial class Verifier
        {
            private readonly string _output;
            private readonly string _patchedSqpack;
            private readonly string _globalSqpack;
            private readonly string _language;
            private readonly CompositeArchive _patchedText;
            private readonly CompositeArchive _patchedFont;
            private readonly CompositeArchive _patchedUi;
            private readonly CompositeArchive _cleanFont;
            private readonly CompositeArchive _cleanUi;
            private readonly Dictionary<string, byte[]> _textureCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            private readonly string _glyphDumpDir;
            private readonly string[] _selectedChecks;

            public bool Failed { get; private set; }

            public Verifier(string output, string patchedSqpack, string globalSqpack, string language, string glyphDumpDir, string[] selectedChecks)
            {
                _output = output;
                _patchedSqpack = patchedSqpack;
                _globalSqpack = globalSqpack;
                _language = language;
                _glyphDumpDir = glyphDumpDir;
                _selectedChecks = selectedChecks;

                _patchedText = new CompositeArchive(
                    Path.Combine(patchedSqpack, TextPrefix + ".index"),
                    patchedSqpack,
                    globalSqpack,
                    TextPrefix);
                _patchedFont = new CompositeArchive(
                    Path.Combine(patchedSqpack, FontPrefix + ".index"),
                    patchedSqpack,
                    globalSqpack,
                    FontPrefix);
                _patchedUi = new CompositeArchive(
                    Path.Combine(patchedSqpack, UiPrefix + ".index"),
                    patchedSqpack,
                    globalSqpack,
                    UiPrefix);
                _cleanFont = new CompositeArchive(
                    Path.Combine(output, "orig." + FontPrefix + ".index"),
                    globalSqpack,
                    globalSqpack,
                    FontPrefix);
                _cleanUi = new CompositeArchive(
                    Path.Combine(output, "orig." + UiPrefix + ".index"),
                    globalSqpack,
                    globalSqpack,
                    UiPrefix);

                if (!string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    Directory.CreateDirectory(_glyphDumpDir);
                    File.WriteAllText(
                        Path.Combine(_glyphDumpDir, "glyph-report.tsv"),
                        "group\tfont\tcodepoint\tchar\tvisible\tcomponents\tsmall_components\tbbox\timage_index\ttexture\tx\ty\twidth\theight\toffset_x\toffset_y\tpng" + Environment.NewLine,
                        Encoding.UTF8);
                }
            }

            public void Run()
            {
                using (_patchedText)
                using (_patchedFont)
                using (_patchedUi)
                using (_cleanFont)
                using (_cleanUi)
                {
                    Console.WriteLine("Patch route verification");
                    Console.WriteLine("  output: {0}", _output);
                    Console.WriteLine("  patched sqpack: {0}", _patchedSqpack);
                    Console.WriteLine("  global sqpack: {0}", _globalSqpack);
                    if (!string.IsNullOrWhiteSpace(_glyphDumpDir))
                    {
                        Console.WriteLine("  glyph dumps: {0}", _glyphDumpDir);
                    }

                    Console.WriteLine();

                    RunVerificationSteps();

                    Console.WriteLine();
                    Console.WriteLine(Failed ? "RESULT: FAIL" : "RESULT: PASS");
                }
            }

        }

    }
}
