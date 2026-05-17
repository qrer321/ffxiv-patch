using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private readonly string _output;
            private readonly string _patchedSqpack;
            private readonly string _globalTextSqpack;
            private readonly string _globalFontSqpack;
            private readonly string _globalUiSqpack;
            private readonly string _cleanFontIndexPath;
            private readonly string _cleanUiIndexPath;
            private readonly string _koreaSqpack;
            private readonly string _language;
            private readonly CompositeArchive _patchedText;
            private readonly CompositeArchive _patchedFont;
            private readonly CompositeArchive _patchedUi;
            private readonly CompositeArchive _generatedFont;
            private readonly CompositeArchive _generatedUi;
            private readonly CompositeArchive _cleanFont;
            private readonly CompositeArchive _cleanUi;
            private readonly CompositeArchive _koreanFont;
            private readonly TtmpFontPackage _ttmpFont;
            private readonly Dictionary<string, byte[]> _textureCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            private readonly string _glyphDumpDir;
            private readonly string[] _selectedChecks;
            private readonly bool _compareAppliedOutput;

            public bool Failed { get; private set; }

            public Verifier(
                string output,
                string patchedSqpack,
                string globalTextSqpack,
                string globalFontSqpack,
                string globalUiSqpack,
                string koreaSqpack,
                string language,
                string glyphDumpDir,
                string[] selectedChecks,
                string fontPackDir,
                bool compareAppliedOutput,
                string cleanFontIndexPath,
                string cleanUiIndexPath)
            {
                _output = output;
                _patchedSqpack = patchedSqpack;
                _globalTextSqpack = globalTextSqpack;
                _globalFontSqpack = globalFontSqpack;
                _globalUiSqpack = globalUiSqpack;
                _cleanFontIndexPath = ResolveCleanIndexPath(
                    output,
                    cleanFontIndexPath,
                    language,
                    "orig." + FontPrefix + ".index",
                    globalFontSqpack,
                    FontPrefix);
                _cleanUiIndexPath = ResolveCleanIndexPath(
                    output,
                    cleanUiIndexPath,
                    language,
                    "orig." + UiPrefix + ".index",
                    globalUiSqpack,
                    UiPrefix);
                _koreaSqpack = koreaSqpack;
                _language = language;
                _glyphDumpDir = glyphDumpDir;
                _selectedChecks = selectedChecks;
                _compareAppliedOutput = compareAppliedOutput;

                _patchedText = new CompositeArchive(
                    Path.Combine(patchedSqpack, TextPrefix + ".index"),
                    patchedSqpack,
                    globalTextSqpack,
                    TextPrefix);
                _patchedFont = new CompositeArchive(
                    Path.Combine(patchedSqpack, FontPrefix + ".index"),
                    patchedSqpack,
                    globalFontSqpack,
                    FontPrefix,
                    _compareAppliedOutput ? _cleanFontIndexPath : null);
                _patchedUi = new CompositeArchive(
                    Path.Combine(patchedSqpack, UiPrefix + ".index"),
                    patchedSqpack,
                    globalUiSqpack,
                    UiPrefix,
                    _compareAppliedOutput ? _cleanUiIndexPath : null);
                if (_compareAppliedOutput)
                {
                    _generatedFont = new CompositeArchive(
                        Path.Combine(output, FontPrefix + ".index"),
                        output,
                        globalFontSqpack,
                        FontPrefix,
                        _cleanFontIndexPath);
                    _generatedUi = new CompositeArchive(
                        Path.Combine(output, UiPrefix + ".index"),
                        output,
                        globalUiSqpack,
                        UiPrefix,
                        _cleanUiIndexPath);
                }

                _cleanFont = new CompositeArchive(
                    _cleanFontIndexPath,
                    globalFontSqpack,
                    globalFontSqpack,
                    FontPrefix);
                _cleanUi = new CompositeArchive(
                    _cleanUiIndexPath,
                    globalUiSqpack,
                    globalUiSqpack,
                    UiPrefix);
                if (!string.IsNullOrWhiteSpace(koreaSqpack))
                {
                    _koreanFont = new CompositeArchive(
                        Path.Combine(koreaSqpack, FontPrefix + ".index"),
                        koreaSqpack,
                        koreaSqpack,
                        FontPrefix);
                }

                _ttmpFont = TtmpFontPackage.TryOpen(fontPackDir);

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
                using (_generatedFont)
                using (_generatedUi)
                using (_cleanFont)
                using (_cleanUi)
                using (_koreanFont)
                using (_ttmpFont)
                {
                    Console.WriteLine("Patch route verification");
                    Console.WriteLine("  output: {0}", _output);
                    Console.WriteLine("  patched sqpack: {0}", _patchedSqpack);
                    Console.WriteLine("  global text sqpack: {0}", _globalTextSqpack);
                    Console.WriteLine("  global font sqpack: {0}", _globalFontSqpack);
                    Console.WriteLine("  global ui sqpack: {0}", _globalUiSqpack);
                    Console.WriteLine("  clean font index: {0}", _cleanFontIndexPath);
                    Console.WriteLine("  clean ui index: {0}", _cleanUiIndexPath);
                    if (!string.IsNullOrWhiteSpace(_koreaSqpack))
                    {
                        Console.WriteLine("  korea sqpack: {0}", _koreaSqpack);
                    }
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

            private static string ResolveCleanIndexPath(
                string output,
                string explicitIndexPath,
                string language,
                string origIndexFileName,
                string fallbackSqpack,
                string fallbackIndexPrefix)
            {
                if (!string.IsNullOrWhiteSpace(explicitIndexPath))
                {
                    string explicitFullPath = Path.GetFullPath(explicitIndexPath);
                    if (!File.Exists(explicitFullPath))
                    {
                        throw new FileNotFoundException("explicit clean index file was not found", explicitFullPath);
                    }

                    return explicitFullPath;
                }

                string outputOrig = Path.Combine(output, origIndexFileName);
                if (File.Exists(outputOrig))
                {
                    return Path.GetFullPath(outputOrig);
                }

                string restoreBaseline = ResolveLocalRestoreBaselineIndex(language, origIndexFileName);
                if (!string.IsNullOrEmpty(restoreBaseline))
                {
                    return restoreBaseline;
                }

                return Path.Combine(fallbackSqpack, fallbackIndexPrefix + ".index");
            }

            private static string ResolveLocalRestoreBaselineIndex(string language, string origIndexFileName)
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(localAppData))
                {
                    localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                }

                if (string.IsNullOrWhiteSpace(localAppData))
                {
                    return null;
                }

                string restoreRoot = Path.Combine(localAppData, "FFXIVKoreanPatch", "restore-baseline");
                if (!Directory.Exists(restoreRoot))
                {
                    return null;
                }

                string languageRoot = string.IsNullOrWhiteSpace(language)
                    ? null
                    : Path.Combine(restoreRoot, language);
                string candidate = ResolveLatestRestoreBaselineIndex(languageRoot, origIndexFileName);
                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate;
                }

                string[] languageDirs = Directory.GetDirectories(restoreRoot);
                Array.Sort(languageDirs, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(languageDirs);
                for (int i = 0; i < languageDirs.Length; i++)
                {
                    candidate = ResolveLatestRestoreBaselineIndex(languageDirs[i], origIndexFileName);
                    if (!string.IsNullOrEmpty(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            private static string ResolveLatestRestoreBaselineIndex(string languageRoot, string origIndexFileName)
            {
                if (string.IsNullOrWhiteSpace(languageRoot) || !Directory.Exists(languageRoot))
                {
                    return null;
                }

                string[] versionDirs = Directory.GetDirectories(languageRoot);
                Array.Sort(versionDirs, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(versionDirs);
                for (int i = 0; i < versionDirs.Length; i++)
                {
                    string indexPath = Path.Combine(versionDirs[i], origIndexFileName);
                    if (File.Exists(indexPath))
                    {
                        return Path.GetFullPath(indexPath);
                    }
                }

                return null;
            }
        }
    }
}
