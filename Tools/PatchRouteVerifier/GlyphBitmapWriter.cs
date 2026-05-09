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
        }
    }
}
