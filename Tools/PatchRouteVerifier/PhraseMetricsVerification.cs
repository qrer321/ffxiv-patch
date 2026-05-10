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
                    string sourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPair.TargetFontPath);
                    for (int phraseIndex = 0; phraseIndex < HighScaleAsciiPhrases.Length; phraseIndex++)
                    {
                        VerifyPhraseMetricsMatchClean(sourceFontPath, fontPair.TargetFontPath, HighScaleAsciiPhrases[phraseIndex]);
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
                    Dictionary<string, int> targetKerningAdjustments = ReadKerningAdjustments(targetFdt);
                    int sourceAdvance = 0;
                    int targetAdvance = 0;
                    int glyphs = 0;
                    bool hasPreviousCodepoint = false;
                    uint previousCodepoint = 0;

                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPreviousCodepoint)
                        {
                            int sourceKerning = GetKerningAdjustment(sourceKerningAdjustments, previousCodepoint, codepoint);
                            int targetKerning = GetKerningAdjustment(targetKerningAdjustments, previousCodepoint, codepoint);
                            if (!KerningAdjustmentMatchesOrLobbySafe(targetFontPath, sourceKerning, targetKerning))
                            {
                                Fail(
                                    "{0} phrase [{1}] kerning U+{2:X4}->U+{3:X4} differs from {4}: target={5}, clean={6}",
                                    targetFontPath,
                                    Escape(phrase),
                                    previousCodepoint,
                                    codepoint,
                                    sourceFontPath,
                                    targetKerning,
                                    sourceKerning);
                                return;
                            }

                            sourceAdvance += sourceKerning;
                            targetAdvance += targetKerning;
                        }

                        if (codepoint <= 0x20)
                        {
                            sourceAdvance += 8;
                            targetAdvance += 8;
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

                        if (!GlyphSpacingMetricsMatchOrLobbySafe(targetFontPath, codepoint, sourceGlyph, targetGlyph))
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

                        sourceAdvance += GetGlyphAdvance(sourceGlyph);
                        targetAdvance += GetGlyphAdvance(targetGlyph);
                        glyphs++;
                        previousCodepoint = codepoint;
                        hasPreviousCodepoint = true;
                    }

                    if (targetAdvance != sourceAdvance)
                    {
                        Fail(
                            "{0} phrase [{1}] width differs from {2}: target={3}, clean={4}",
                            targetFontPath,
                            Escape(phrase),
                            sourceFontPath,
                            targetAdvance,
                            sourceAdvance);
                        return;
                    }

                    Pass("{0} phrase [{1}] metrics match {2}, glyphs={3}, width={4}/{5}", targetFontPath, Escape(phrase), sourceFontPath, glyphs, targetAdvance, sourceAdvance);
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

            private bool TryPhraseGlyphPixelsMatchClean(
                string sourceFontPath,
                string targetFontPath,
                string phrase,
                out int checkedGlyphs,
                out string error)
            {
                checkedGlyphs = 0;
                error = null;
                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (IsPhraseLayoutSpace(codepoint))
                    {
                        continue;
                    }

                    try
                    {
                        GlyphCanvas source = RenderGlyph(_cleanFont, sourceFontPath, codepoint);
                        GlyphCanvas target = RenderGlyph(_patchedFont, targetFontPath, codepoint);
                        long score = Diff(source.Alpha, target.Alpha);
                        if (score != 0 || source.VisiblePixels == 0 || target.VisiblePixels == 0)
                        {
                            error = "U+" + codepoint.ToString("X4") +
                                " score=" + score.ToString() +
                                ", visible=" + source.VisiblePixels.ToString() + "/" + target.VisiblePixels.ToString();
                            return false;
                        }

                        checkedGlyphs++;
                    }
                    catch (Exception ex)
                    {
                        error = "U+" + codepoint.ToString("X4") + " " + ex.Message;
                        return false;
                    }
                }

                return checkedGlyphs > 0;
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
