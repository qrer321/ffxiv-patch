using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyReportedInGameHangulPhraseSourcePreservation()
            {
                Console.WriteLine("[FDT] Reported in-game Hangul phrase source preservation");
                if (_ttmpFont == null)
                {
                    Warn("Reported in-game phrase source preservation skipped; pass --font-pack-dir with TTMPD.mpd and TTMPL.mpl");
                    return;
                }

                HashSet<string> fontPaths = CollectReportedInGamePhraseFontPaths();
                for (int phraseIndex = 0; phraseIndex < ReportedInGameHangulPhrases.Length; phraseIndex++)
                {
                    VerifyReportedInGameHangulPhrase(ReportedInGameHangulPhrases[phraseIndex], fontPaths);
                }
            }

            private void VerifyReportedInGameHangulPhrase(string phrase, HashSet<string> fontPaths)
            {
                int compared = 0;
                foreach (string fontPath in fontPaths)
                {
                    if (!_ttmpFont.ContainsPath(fontPath))
                    {
                        continue;
                    }

                    byte[] sourceFdt;
                    byte[] targetFdt;
                    try
                    {
                        sourceFdt = _ttmpFont.ReadFile(fontPath);
                        targetFdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} phrase [{1}] source preservation read error: {2}", fontPath, Escape(phrase), ex.Message);
                        continue;
                    }

                    if (!PhraseGlyphsExist(sourceFdt, phrase))
                    {
                        continue;
                    }

                    if (!PhraseGlyphsExist(targetFdt, phrase))
                    {
                        Fail("{0} phrase [{1}] exists in TTMP source but is missing patched glyphs", fontPath, Escape(phrase));
                        continue;
                    }

                    PhraseRenderSnapshot sourcePixels;
                    PhraseRenderSnapshot targetPixels;
                    PhraseLayoutResult sourceLayout;
                    PhraseLayoutResult targetLayout;
                    string error;
                    if (!TryRenderPhrasePixels(_ttmpFont, fontPath, phrase, out sourcePixels, out error))
                    {
                        Fail("{0} phrase [{1}] TTMP render error: {2}", fontPath, Escape(phrase), error);
                        continue;
                    }

                    if (!TryRenderPhrasePixels(_patchedFont, fontPath, phrase, true, out targetPixels, out error))
                    {
                        Fail("{0} phrase [{1}] patched render error: {2}", fontPath, Escape(phrase), error);
                        continue;
                    }

                    if (!TryMeasurePhraseLayout(_ttmpFont, fontPath, phrase, out sourceLayout, out error))
                    {
                        Fail("{0} phrase [{1}] TTMP layout error: {2}", fontPath, Escape(phrase), error);
                        continue;
                    }

                    if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out targetLayout, out error))
                    {
                        Fail("{0} phrase [{1}] patched layout error: {2}", fontPath, Escape(phrase), error);
                        continue;
                    }

                    if (sourcePixels.Width == targetPixels.Width &&
                        sourcePixels.Glyphs == targetPixels.Glyphs &&
                        sourceLayout.Width == targetLayout.Width &&
                        sourceLayout.Glyphs == targetLayout.Glyphs &&
                        sourceLayout.OverlapPixels == targetLayout.OverlapPixels &&
                        PhrasePixelsEqual(sourcePixels.Pixels, targetPixels.Pixels))
                    {
                        Pass(
                            "{0} phrase [{1}] matches TTMP source: glyphs={2}, width={3}, overlap={4}, pixels={5}",
                            fontPath,
                            Escape(phrase),
                            targetLayout.Glyphs,
                            targetLayout.Width,
                            targetLayout.OverlapPixels,
                            targetPixels.Pixels.Count);
                        compared++;
                        continue;
                    }

                    Fail(
                        "{0} phrase [{1}] differs from TTMP source: glyphs={2}/{3}, width={4}/{5}, overlap={6}/{7}, pixels={8}/{9}",
                        fontPath,
                        Escape(phrase),
                        targetLayout.Glyphs,
                        sourceLayout.Glyphs,
                        targetLayout.Width,
                        sourceLayout.Width,
                        targetLayout.OverlapPixels,
                        sourceLayout.OverlapPixels,
                        targetPixels.Pixels.Count,
                        sourcePixels.Pixels.Count);
                }

                if (compared == 0)
                {
                    Fail("No TTMP font route covered reported in-game phrase [{0}]", Escape(phrase));
                }
            }

            private static HashSet<string> CollectReportedInGamePhraseFontPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddValues(paths, DialoguePhraseFontPaths);
                AddValues(paths, SystemSettingsScaledFonts);
                return paths;
            }

            private static bool PhraseGlyphsExist(byte[] fdt, string phrase)
            {
                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (IsPhraseLayoutSpace(codepoint))
                    {
                        continue;
                    }

                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool TryRenderPhrasePixels(
                TtmpFontPackage package,
                string fontPath,
                string phrase,
                out PhraseRenderSnapshot snapshot,
                out string error)
            {
                snapshot = new PhraseRenderSnapshot();
                error = null;
                try
                {
                    byte[] fdt = package.ReadFile(fontPath);
                    Dictionary<string, int> kerningAdjustments = ReadKerningAdjustments(fdt);
                    Dictionary<long, byte> pixels = new Dictionary<long, byte>();
                    int cursor = 0;
                    int glyphs = 0;
                    bool hasPreviousCodepoint = false;
                    uint previousCodepoint = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPreviousCodepoint)
                        {
                            cursor += GetKerningAdjustment(kerningAdjustments, previousCodepoint, codepoint);
                        }

                        if (IsPhraseLayoutSpace(codepoint))
                        {
                            cursor += PhraseLayoutSpaceAdvance;
                            previousCodepoint = codepoint;
                            hasPreviousCodepoint = true;
                            continue;
                        }

                        PhraseGlyphMeasurement glyph;
                        if (!TryMeasurePhraseGlyph(package, fontPath, fdt, codepoint, out glyph, out error))
                        {
                            return false;
                        }

                        AddPhraseGlyphPixels(pixels, cursor, glyph.Alpha);
                        cursor += glyph.Advance;
                        glyphs++;
                        previousCodepoint = codepoint;
                        hasPreviousCodepoint = true;
                    }

                    snapshot = new PhraseRenderSnapshot
                    {
                        Pixels = pixels,
                        Width = cursor,
                        Glyphs = glyphs
                    };
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }
    }
}
