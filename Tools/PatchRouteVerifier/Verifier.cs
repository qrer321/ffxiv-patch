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
                bool compareAppliedOutput)
            {
                _output = output;
                _patchedSqpack = patchedSqpack;
                _globalTextSqpack = globalTextSqpack;
                _globalFontSqpack = globalFontSqpack;
                _globalUiSqpack = globalUiSqpack;
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
                    FontPrefix);
                _patchedUi = new CompositeArchive(
                    Path.Combine(patchedSqpack, UiPrefix + ".index"),
                    patchedSqpack,
                    globalUiSqpack,
                    UiPrefix);
                if (_compareAppliedOutput)
                {
                    _generatedFont = new CompositeArchive(
                        Path.Combine(output, FontPrefix + ".index"),
                        output,
                        globalFontSqpack,
                        FontPrefix);
                    _generatedUi = new CompositeArchive(
                        Path.Combine(output, UiPrefix + ".index"),
                        output,
                        globalUiSqpack,
                        UiPrefix);
                }

                _cleanFont = new CompositeArchive(
                    Path.Combine(output, "orig." + FontPrefix + ".index"),
                    globalFontSqpack,
                    globalFontSqpack,
                    FontPrefix);
                _cleanUi = new CompositeArchive(
                    Path.Combine(output, "orig." + UiPrefix + ".index"),
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
        }
    }
}
