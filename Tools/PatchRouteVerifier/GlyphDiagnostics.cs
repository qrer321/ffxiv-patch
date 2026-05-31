using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const int GlyphDumpScale = 4;
        private static readonly uint[] MissingGlyphFallbackCodepoints = new uint[]
        {
            '-',
            '=',
            '|'
        };

        private sealed partial class Verifier
        {
            private bool GlyphMatchesFallback(string fontPath, GlyphCanvas target, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return false;
                }

                try
                {
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fontPath, fallbackCodepoint);
                    return Diff(target.Alpha, fallback.Alpha) == 0 && target.VisiblePixels > 0;
                }
                catch
                {
                    return false;
                }
            }

            private bool GlyphMatchesAnyMissingGlyphFallback(string fontPath, GlyphCanvas target, uint codepoint, out uint fallbackCodepoint)
            {
                for (int i = 0; i < MissingGlyphFallbackCodepoints.Length; i++)
                {
                    uint candidate = MissingGlyphFallbackCodepoints[i];
                    if (GlyphMatchesFallback(fontPath, target, codepoint, candidate))
                    {
                        fallbackCodepoint = candidate;
                        return true;
                    }
                }

                fallbackCodepoint = 0;
                return false;
            }

            private static uint[] CollectCodepoints(string[] phrases)
            {
                List<uint> result = new List<uint>();
                HashSet<uint> seen = new HashSet<uint>();
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (codepoint <= 0x20)
                        {
                            continue;
                        }

                        if (seen.Add(codepoint))
                        {
                            result.Add(codepoint);
                        }
                    }
                }

                return result.ToArray();
            }
        }
    }
}
