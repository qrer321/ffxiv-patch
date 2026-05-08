using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static void WriteGlyphPng(GlyphCanvas canvas, string path)
            {
                int width = GlyphCanvasSize * GlyphDumpScale;
                int height = GlyphCanvasSize * GlyphDumpScale;
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.Black);
                    }

                    for (int y = 0; y < GlyphCanvasSize; y++)
                    {
                        for (int x = 0; x < GlyphCanvasSize; x++)
                        {
                            byte alpha = canvas.Alpha[y * GlyphCanvasSize + x];
                            if (alpha == 0)
                            {
                                continue;
                            }

                            System.Drawing.Color color = System.Drawing.Color.FromArgb(255, alpha, alpha, alpha);
                            int px = x * GlyphDumpScale;
                            int py = y * GlyphDumpScale;
                            for (int sy = 0; sy < GlyphDumpScale; sy++)
                            {
                                for (int sx = 0; sx < GlyphDumpScale; sx++)
                                {
                                    bitmap.SetPixel(px + sx, py + sy, color);
                                }
                            }
                        }
                    }

                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            private static void WriteGlyphSheetPng(List<uint> codepoints, List<GlyphCanvas> glyphs, string path)
            {
                const int scale = 2;
                const int columns = 6;
                const int labelHeight = 18;
                int cellWidth = GlyphCanvasSize * scale;
                int cellHeight = GlyphCanvasSize * scale + labelHeight;
                int rows = (glyphs.Count + columns - 1) / columns;
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(cellWidth * columns, cellHeight * rows, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.Black);
                        using (System.Drawing.Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 180, 220, 255)))
                        {
                            for (int i = 0; i < glyphs.Count; i++)
                            {
                                int column = i % columns;
                                int row = i / columns;
                                int originX = column * cellWidth;
                                int originY = row * cellHeight;
                                graphics.DrawString("U+" + codepoints[i].ToString("X4"), System.Drawing.SystemFonts.DefaultFont, brush, originX + 4, originY + 2);
                                DrawGlyphToBitmap(bitmap, glyphs[i], originX, originY + labelHeight, scale);
                            }
                        }
                    }

                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            private static void DrawGlyphToBitmap(System.Drawing.Bitmap bitmap, GlyphCanvas canvas, int originX, int originY, int scale)
            {
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        byte alpha = canvas.Alpha[y * GlyphCanvasSize + x];
                        if (alpha == 0)
                        {
                            continue;
                        }

                        System.Drawing.Color color = System.Drawing.Color.FromArgb(255, alpha, alpha, alpha);
                        int px = originX + x * scale;
                        int py = originY + y * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            for (int sx = 0; sx < scale; sx++)
                            {
                                bitmap.SetPixel(px + sx, py + sy, color);
                            }
                        }
                    }
                }
            }
        }
    }
}
