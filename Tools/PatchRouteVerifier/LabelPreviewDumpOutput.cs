using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
