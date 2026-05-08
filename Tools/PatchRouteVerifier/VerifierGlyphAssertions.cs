using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyLabelGlyphsEqualClean(string fdtPath, string[] labels)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
                {
                    string label = labels[labelIndex];
                    for (int charIndex = 0; charIndex < label.Length; charIndex++)
                    {
                        char ch = label[charIndex];
                        if (!char.IsWhiteSpace(ch))
                        {
                            codepoints.Add(ch);
                        }
                    }
                }

                foreach (uint codepoint in codepoints)
                {
                    ExpectGlyphEqual(_cleanFont, fdtPath, codepoint, _patchedFont, fdtPath, codepoint);
                    ExpectGlyphNotEqualToFallback(fdtPath, codepoint, '-');
                    ExpectGlyphNotEqualToFallback(fdtPath, codepoint, '=');
                }
            }

            private void ExpectGlyphNotEqualToFallback(string fdtPath, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return;
                }

                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fdtPath);
                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        Fail("{0} U+{1:X4} is missing", fdtPath, codepoint);
                        return;
                    }

                    if (!TryFindGlyph(fdt, fallbackCodepoint, out ignored))
                    {
                        Warn("{0} U+{1:X4} fallback comparison skipped; missing U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fdtPath, codepoint);
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fdtPath, fallbackCodepoint);
                    long score = Diff(glyph.Alpha, fallback.Alpha);
                    if (score != 0 && glyph.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} is not fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    Fail("{0} U+{1:X4} matches fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} fallback comparison error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private void ExpectGlyphEqual(
                CompositeArchive sourceArchive,
                string sourceFdtPath,
                uint sourceCodepoint,
                CompositeArchive targetArchive,
                string targetFdtPath,
                uint targetCodepoint)
            {
                try
                {
                    GlyphCanvas source = RenderGlyph(sourceArchive, sourceFdtPath, sourceCodepoint);
                    GlyphCanvas target = RenderGlyph(targetArchive, targetFdtPath, targetCodepoint);
                    long score = Diff(source.Alpha, target.Alpha);
                    bool spacingMatch = GlyphSpacingMetricsMatch(source.Glyph, target.Glyph);
                    if (score == 0 && spacingMatch && source.VisiblePixels > 0 && target.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} -> {2} U+{3:X4}", sourceFdtPath, sourceCodepoint, targetFdtPath, targetCodepoint);
                        return;
                    }

                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} mismatch score={4} visible={5}/{6}, target={7}, clean={8}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        score,
                        source.VisiblePixels,
                        target.VisiblePixels,
                        FormatGlyphSpacing(target.Glyph),
                        FormatGlyphSpacing(source.Glyph));
                }
                catch (Exception ex)
                {
                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} error: {4}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        ex.Message);
                }
            }

            private void ExpectGlyphEqualIfSourceExists(
                CompositeArchive sourceArchive,
                string sourceFdtPath,
                uint sourceCodepoint,
                CompositeArchive targetArchive,
                string targetFdtPath,
                uint targetCodepoint)
            {
                byte[] sourceFdt = sourceArchive.ReadFile(sourceFdtPath);
                FdtGlyphEntry ignored;
                if (!TryFindGlyph(sourceFdt, sourceCodepoint, out ignored))
                {
                    return;
                }

                ExpectGlyphEqual(sourceArchive, sourceFdtPath, sourceCodepoint, targetArchive, targetFdtPath, targetCodepoint);
            }

            private void ExpectGlyphVisible(CompositeArchive archive, string fdtPath, uint codepoint)
            {
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fdtPath, codepoint);
                    if (canvas.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} visible={2}", fdtPath, codepoint, canvas.VisiblePixels);
                        return;
                    }

                    Fail("{0} U+{1:X4} is invisible", fdtPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private void ExpectGlyphVisibleAtLeast(CompositeArchive archive, string fdtPath, uint codepoint, int minimumVisiblePixels)
            {
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fdtPath, codepoint);
                    if (canvas.VisiblePixels >= minimumVisiblePixels)
                    {
                        Pass("{0} U+{1:X4} visible={2}", fdtPath, codepoint, canvas.VisiblePixels);
                        return;
                    }

                    Fail("{0} U+{1:X4} visible={2}, expected at least {3}", fdtPath, codepoint, canvas.VisiblePixels, minimumVisiblePixels);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private static long Diff(byte[] left, byte[] right)
            {
                long score = 0;
                for (int i = 0; i < left.Length; i++)
                {
                    score += Math.Abs(left[i] - right[i]);
                }

                return score;
            }
        }
    }
}
