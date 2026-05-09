using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
        }
    }
}
