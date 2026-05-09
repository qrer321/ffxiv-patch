using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyHighScaleAsciiPhraseLayouts()
            {
                Console.WriteLine("[FDT] High-scale ASCII phrase metrics");
                for (int fontIndex = 0; fontIndex < HighScaleAsciiFontPairs.Length; fontIndex++)
                {
                    FontPair fontPair = HighScaleAsciiFontPairs[fontIndex];
                    for (int phraseIndex = 0; phraseIndex < HighScaleAsciiPhrases.Length; phraseIndex++)
                    {
                        VerifyPhraseMetricsMatchClean(fontPair.SourceFontPath, fontPair.TargetFontPath, HighScaleAsciiPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifyPhraseMetricsMatchClean(string sourceFontPath, string targetFontPath, string phrase)
            {
                try
                {
                    byte[] sourceFdt = _cleanFont.ReadFile(sourceFontPath);
                    byte[] targetFdt = _patchedFont.ReadFile(targetFontPath);
                    Dictionary<string, int> sourceKerningAdjustments = ReadKerningAdjustments(sourceFdt);
                    int advance = 0;
                    int glyphs = 0;
                    bool hasPreviousCodepoint = false;
                    uint previousCodepoint = 0;

                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPreviousCodepoint)
                        {
                            advance += GetKerningAdjustment(sourceKerningAdjustments, previousCodepoint, codepoint);
                        }

                        if (codepoint <= 0x20)
                        {
                            advance += 8;
                            previousCodepoint = codepoint;
                            hasPreviousCodepoint = true;
                            continue;
                        }

                        FdtGlyphEntry sourceGlyph;
                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            Fail("{0} phrase [{1}] missing clean U+{2:X4}", sourceFontPath, Escape(phrase), codepoint);
                            return;
                        }

                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} phrase [{1}] missing patched U+{2:X4}", targetFontPath, Escape(phrase), codepoint);
                            return;
                        }

                        if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                        {
                            Fail(
                                "{0} phrase [{1}] U+{2:X4} metrics differ from {3}: target={4}, clean={5}",
                                targetFontPath,
                                Escape(phrase),
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            return;
                        }

                        advance += Math.Max(1, sourceGlyph.Width + sourceGlyph.OffsetX);
                        glyphs++;
                        previousCodepoint = codepoint;
                        hasPreviousCodepoint = true;
                    }

                    Pass("{0} phrase [{1}] metrics match {2}, glyphs={3}, width={4}", targetFontPath, Escape(phrase), sourceFontPath, glyphs, advance);
                }
                catch (Exception ex)
                {
                    Fail("{0} phrase [{1}] metric check error: {2}", targetFontPath, Escape(phrase), ex.Message);
                }
            }

            private void VerifyPhrasePixelsMatchClean(string sourceFontPath, string targetFontPath, string phrase)
            {
                PhraseRenderSnapshot source;
                PhraseRenderSnapshot target;
                string error;
                if (!TryRenderPhrasePixels(_cleanFont, sourceFontPath, phrase, false, out source, out error))
                {
                    Fail("{0} phrase [{1}] clean render error: {2}", sourceFontPath, Escape(phrase), error);
                    return;
                }

                if (!TryRenderPhrasePixels(_patchedFont, targetFontPath, phrase, true, out target, out error))
                {
                    Fail("{0} phrase [{1}] patched render error: {2}", targetFontPath, Escape(phrase), error);
                    return;
                }

                if (source.Width == target.Width &&
                    source.Glyphs == target.Glyphs &&
                    PhrasePixelsEqual(source.Pixels, target.Pixels))
                {
                    Pass(
                        "{0} phrase [{1}] pixels match {2}, glyphs={3}, width={4}, pixels={5}",
                        targetFontPath,
                        Escape(phrase),
                        sourceFontPath,
                        target.Glyphs,
                        target.Width,
                        target.Pixels.Count);
                    return;
                }

                Fail(
                    "{0} phrase [{1}] pixels differ from {2}: glyphs={3}/{4}, width={5}/{6}, pixels={7}/{8}",
                    targetFontPath,
                    Escape(phrase),
                    sourceFontPath,
                    target.Glyphs,
                    source.Glyphs,
                    target.Width,
                    source.Width,
                    target.Pixels.Count,
                    source.Pixels.Count);
            }

            private bool TryRenderPhrasePixels(
                CompositeArchive archive,
                string fontPath,
                string phrase,
                bool validateGlyphShape,
                out PhraseRenderSnapshot snapshot,
                out string error)
            {
                snapshot = new PhraseRenderSnapshot();
                error = null;
                try
                {
                    byte[] fdt = archive.ReadFile(fontPath);
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
                        if (!TryMeasurePhraseGlyph(archive, fontPath, fdt, codepoint, validateGlyphShape, out glyph, out error))
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

            private static void AddPhraseGlyphPixels(Dictionary<long, byte> pixels, int cursor, byte[] alpha)
            {
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    int rowOffset = y * GlyphCanvasSize;
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        byte value = alpha[rowOffset + x];
                        if (value == 0)
                        {
                            continue;
                        }

                        int pixelX = cursor + x - 32;
                        int pixelY = y - 32;
                        long key = ((long)pixelY << 32) ^ (uint)pixelX;
                        byte existing;
                        if (!pixels.TryGetValue(key, out existing) || value > existing)
                        {
                            pixels[key] = value;
                        }
                    }
                }
            }

            private static bool PhrasePixelsEqual(Dictionary<long, byte> left, Dictionary<long, byte> right)
            {
                if (left.Count != right.Count)
                {
                    return false;
                }

                foreach (KeyValuePair<long, byte> pair in left)
                {
                    byte rightValue;
                    if (!right.TryGetValue(pair.Key, out rightValue) || rightValue != pair.Value)
                    {
                        return false;
                    }
                }

                return true;
            }

            private struct PhraseRenderSnapshot
            {
                public Dictionary<long, byte> Pixels;
                public int Width;
                public int Glyphs;
            }
        }
    }
}
