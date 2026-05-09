using System;

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
                    PhraseLayoutAccumulator accumulator = new PhraseLayoutAccumulator();
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);

                        if (IsPhraseLayoutSpace(codepoint))
                        {
                            accumulator.AddSpace();
                            continue;
                        }

                        PhraseGlyphMeasurement glyph;
                        if (!TryMeasurePhraseGlyph(archive, fontPath, fdt, codepoint, validateGlyphShape, out glyph, out error))
                        {
                            return false;
                        }

                        accumulator.AddGlyph(glyph);
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
