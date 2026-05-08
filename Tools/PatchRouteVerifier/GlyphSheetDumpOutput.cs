using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void DumpLobbyPhraseSheets(uint[] codepoints)
            {
                DumpPhraseSheets(LobbyPhraseGlyphGroup, LobbyPhraseFontPaths, codepoints);
            }

            private void DumpDialoguePhraseSheets(uint[] codepoints)
            {
                DumpPhraseSheets(DialoguePhraseGlyphGroup, DialoguePhraseFontPaths, codepoints);
            }

            private void DumpPhraseSheets(string group, string[] fontPaths, uint[] codepoints)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                for (int fontIndex = 0; fontIndex < fontPaths.Length; fontIndex++)
                {
                    string fontPath = fontPaths[fontIndex];
                    List<uint> sheetCodepoints = new List<uint>();
                    List<GlyphCanvas> sheetGlyphs = new List<GlyphCanvas>();
                    for (int codepointIndex = 0; codepointIndex < codepoints.Length; codepointIndex++)
                    {
                        uint codepoint = codepoints[codepointIndex];
                        try
                        {
                            GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                            sheetCodepoints.Add(codepoint);
                            sheetGlyphs.Add(glyph);
                        }
                        catch
                        {
                        }
                    }

                    if (sheetGlyphs.Count == 0)
                    {
                        continue;
                    }

                    string fileName = group + "_" + SanitizeFileName(fontPath) + "_sheet.png";
                    WriteGlyphSheetPng(sheetCodepoints, sheetGlyphs, Path.Combine(_glyphDumpDir, fileName));
                }
            }
        }
    }
}
