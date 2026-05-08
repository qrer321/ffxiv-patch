using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void DumpGlyph(string group, string fdtPath, uint codepoint, GlyphCanvas canvas, GlyphStats stats)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                string fileName = group + "_" + SanitizeFileName(fdtPath) + "_U" + codepoint.ToString("X4") + ".png";
                string pngPath = Path.Combine(_glyphDumpDir, fileName);
                WriteGlyphPng(canvas, pngPath);

                string reportLine = string.Join(
                    "\t",
                    new string[]
                    {
                        EscapeTsv(group),
                        EscapeTsv(fdtPath),
                        "U+" + codepoint.ToString("X4"),
                        EscapeTsv(char.ConvertFromUtf32((int)codepoint)),
                        canvas.VisiblePixels.ToString(),
                        stats.ComponentCount.ToString(),
                        stats.SmallComponentCount.ToString(),
                        EscapeTsv(FormatBounds(stats)),
                        canvas.Glyph.ImageIndex.ToString(),
                        EscapeTsv(canvas.TexturePath),
                        canvas.Glyph.X.ToString(),
                        canvas.Glyph.Y.ToString(),
                        canvas.Glyph.Width.ToString(),
                        canvas.Glyph.Height.ToString(),
                        canvas.Glyph.OffsetX.ToString(),
                        canvas.Glyph.OffsetY.ToString(),
                        EscapeTsv(pngPath)
                    });
                File.AppendAllText(Path.Combine(_glyphDumpDir, "glyph-report.tsv"), reportLine + Environment.NewLine, Encoding.UTF8);
            }

            private void DumpLobbyPhraseSheets(uint[] codepoints)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
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
                            // Not every lobby font carries Hangul. Individual glyph checks already report
                            // the fonts that do, so missing entries are skipped in the visual sheet.
                        }
                    }

                    if (sheetGlyphs.Count == 0)
                    {
                        continue;
                    }

                    string fileName = LobbyPhraseGlyphGroup + "_" + SanitizeFileName(fontPath) + "_sheet.png";
                    WriteGlyphSheetPng(sheetCodepoints, sheetGlyphs, Path.Combine(_glyphDumpDir, fileName));
                }
            }

            private void DumpDialoguePhraseSheets(uint[] codepoints)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                for (int fontIndex = 0; fontIndex < DialoguePhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = DialoguePhraseFontPaths[fontIndex];
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
                            // Not every in-game font carries every Hangul glyph.
                        }
                    }

                    if (sheetGlyphs.Count == 0)
                    {
                        continue;
                    }

                    string fileName = DialoguePhraseGlyphGroup + "_" + SanitizeFileName(fontPath) + "_sheet.png";
                    WriteGlyphSheetPng(sheetCodepoints, sheetGlyphs, Path.Combine(_glyphDumpDir, fileName));
                }
            }

            private void DumpLabelPreview(string group, string fontPath, string[] labels)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                try
                {
                    List<GlyphCanvas?[]> rows = new List<GlyphCanvas?[]>();
                    int maxGlyphs = 0;
                    for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
                    {
                        string label = labels[labelIndex];
                        List<GlyphCanvas?> glyphs = new List<GlyphCanvas?>();
                        for (int charIndex = 0; charIndex < label.Length; charIndex++)
                        {
                            char ch = label[charIndex];
                            if (char.IsWhiteSpace(ch))
                            {
                                glyphs.Add(null);
                                continue;
                            }

                            glyphs.Add(RenderGlyph(_patchedFont, fontPath, ch));
                        }

                        if (glyphs.Count > maxGlyphs)
                        {
                            maxGlyphs = glyphs.Count;
                        }

                        rows.Add(glyphs.ToArray());
                    }

                    if (rows.Count == 0 || maxGlyphs == 0)
                    {
                        return;
                    }

                    const int scale = 1;
                    const int spacing = 2;
                    const int rowSpacing = 8;
                    int cellSize = GlyphCanvasSize * scale;
                    int width = Math.Max(1, maxGlyphs * (cellSize + spacing) + 8);
                    int height = Math.Max(1, rows.Count * (cellSize + rowSpacing) + 8);
                    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            graphics.Clear(System.Drawing.Color.Black);
                        }

                        for (int row = 0; row < rows.Count; row++)
                        {
                            GlyphCanvas?[] glyphs = rows[row];
                            int originY = 4 + row * (cellSize + rowSpacing);
                            int originX = 4;
                            for (int i = 0; i < glyphs.Length; i++)
                            {
                                GlyphCanvas? glyph = glyphs[i];
                                if (glyph.HasValue)
                                {
                                    DrawGlyphToBitmap(bitmap, glyph.Value, originX, originY, scale);
                                }

                                originX += cellSize + spacing;
                            }
                        }

                        string fileName = group + "_" + SanitizeFileName(fontPath) + "_preview.png";
                        bitmap.Save(Path.Combine(_glyphDumpDir, fileName), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    Warn("{0} {1} preview dump error: {2}", group, fontPath, ex.Message);
                }
            }

        }
    }
}
