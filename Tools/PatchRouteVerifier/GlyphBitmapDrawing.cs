namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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

                        DrawScaledGlyphPixel(bitmap, alpha, originX + x * scale, originY + y * scale, scale);
                    }
                }
            }

            private static void DrawScaledGlyphPixel(System.Drawing.Bitmap bitmap, byte alpha, int x, int y, int scale)
            {
                System.Drawing.Color color = System.Drawing.Color.FromArgb(255, alpha, alpha, alpha);
                for (int sy = 0; sy < scale; sy++)
                {
                    for (int sx = 0; sx < scale; sx++)
                    {
                        bitmap.SetPixel(x + sx, y + sy, color);
                    }
                }
            }
        }
    }
}
