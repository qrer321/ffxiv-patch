using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
                                DrawGlyphSheetCell(graphics, bitmap, brush, codepoints[i], glyphs[i], i, cellWidth, cellHeight, columns, labelHeight, scale);
                            }
                        }
                    }

                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            private static void DrawGlyphSheetCell(
                System.Drawing.Graphics graphics,
                System.Drawing.Bitmap bitmap,
                System.Drawing.Brush brush,
                uint codepoint,
                GlyphCanvas glyph,
                int index,
                int cellWidth,
                int cellHeight,
                int columns,
                int labelHeight,
                int scale)
            {
                int column = index % columns;
                int row = index / columns;
                int originX = column * cellWidth;
                int originY = row * cellHeight;
                graphics.DrawString("U+" + codepoint.ToString("X4"), System.Drawing.SystemFonts.DefaultFont, brush, originX + 4, originY + 2);
                DrawGlyphToBitmap(bitmap, glyph, originX, originY + labelHeight, scale);
            }
        }
    }
}
