using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyAndDumpDialoguePhraseGlyph(uint codepoint)
            {
                bool foundAny = false;
                for (int fontIndex = 0; fontIndex < DialoguePhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = DialoguePhraseFontPaths[fontIndex];
                    if (!FontContainsDialogueGlyph(fontPath, codepoint))
                    {
                        continue;
                    }

                    foundAny = true;
                    DumpAndCheckDialoguePhraseGlyph(fontPath, codepoint);
                }

                if (!foundAny)
                {
                    Fail("dialogue phrase U+{0:X4} was not found in any checked in-game font", codepoint);
                }
            }

            private bool FontContainsDialogueGlyph(string fontPath, uint codepoint)
            {
                byte[] fdt;
                try
                {
                    fdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Warn("{0} could not be read for dialogue glyph diagnostics: {1}", fontPath, ex.Message);
                    return false;
                }

                FdtGlyphEntry ignored;
                return TryFindGlyph(fdt, codepoint, out ignored);
            }
        }
    }
}
