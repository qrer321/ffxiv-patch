using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int PhraseLayoutSpaceAdvance = 8;

        private sealed partial class Verifier
        {
            private bool TryMeasurePhraseLayout(
                CompositeArchive archive,
                string fontPath,
                string phrase,
                bool validateGlyphShape,
                out PhraseLayoutResult result,
                out string error)
            {
                result = new PhraseLayoutResult();
                error = null;
                try
                {
                    byte[] fdt = archive.ReadFile(fontPath);
                    Dictionary<string, int> kerningAdjustments = ReadKerningAdjustments(fdt);
                    PhraseLayoutAccumulator accumulator = new PhraseLayoutAccumulator();
                    bool hasPreviousCodepoint = false;
                    uint previousCodepoint = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPreviousCodepoint)
                        {
                            accumulator.AddKerning(GetKerningAdjustment(kerningAdjustments, previousCodepoint, codepoint));
                        }

                        if (IsPhraseLayoutSpace(codepoint))
                        {
                            accumulator.AddSpace();
                            previousCodepoint = codepoint;
                            hasPreviousCodepoint = true;
                            continue;
                        }

                        PhraseGlyphMeasurement glyph;
                        if (!TryMeasurePhraseGlyph(archive, fontPath, fdt, codepoint, validateGlyphShape, out glyph, out error))
                        {
                            return false;
                        }

                        accumulator.AddGlyph(glyph);
                        previousCodepoint = codepoint;
                        hasPreviousCodepoint = true;
                    }

                    result = accumulator.ToResult();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private bool TryMeasurePhraseLayout(
                TtmpFontPackage package,
                string fontPath,
                string phrase,
                out PhraseLayoutResult result,
                out string error)
            {
                result = new PhraseLayoutResult();
                error = null;
                try
                {
                    byte[] fdt = package.ReadFile(fontPath);
                    Dictionary<string, int> kerningAdjustments = ReadKerningAdjustments(fdt);
                    PhraseLayoutAccumulator accumulator = new PhraseLayoutAccumulator();
                    bool hasPreviousCodepoint = false;
                    uint previousCodepoint = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPreviousCodepoint)
                        {
                            accumulator.AddKerning(GetKerningAdjustment(kerningAdjustments, previousCodepoint, codepoint));
                        }

                        if (IsPhraseLayoutSpace(codepoint))
                        {
                            accumulator.AddSpace();
                            previousCodepoint = codepoint;
                            hasPreviousCodepoint = true;
                            continue;
                        }

                        PhraseGlyphMeasurement glyph;
                        if (!TryMeasurePhraseGlyph(package, fontPath, fdt, codepoint, out glyph, out error))
                        {
                            return false;
                        }

                        accumulator.AddGlyph(glyph);
                        previousCodepoint = codepoint;
                        hasPreviousCodepoint = true;
                    }

                    result = accumulator.ToResult();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static bool IsPhraseLayoutSpace(uint codepoint)
            {
                return codepoint <= 0x20;
            }
        }
    }
}
