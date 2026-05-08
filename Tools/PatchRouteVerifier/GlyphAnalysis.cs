using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static GlyphStats AnalyzeGlyph(GlyphCanvas canvas)
            {
                GlyphStats stats = new GlyphStats();
                stats.MinX = GlyphCanvasSize;
                stats.MinY = GlyphCanvasSize;
                stats.MaxX = -1;
                stats.MaxY = -1;

                bool[] seen = new bool[canvas.Alpha.Length];
                int[] stack = new int[canvas.Alpha.Length];
                for (int y = 0; y < GlyphCanvasSize; y++)
                {
                    for (int x = 0; x < GlyphCanvasSize; x++)
                    {
                        int start = y * GlyphCanvasSize + x;
                        if (seen[start] || canvas.Alpha[start] == 0)
                        {
                            continue;
                        }

                        int area = 0;
                        int minX = x;
                        int minY = y;
                        int maxX = x;
                        int maxY = y;
                        int stackCount = 0;
                        stack[stackCount++] = start;
                        seen[start] = true;

                        while (stackCount > 0)
                        {
                            int current = stack[--stackCount];
                            int cx = current % GlyphCanvasSize;
                            int cy = current / GlyphCanvasSize;
                            area++;
                            if (cx < minX) minX = cx;
                            if (cy < minY) minY = cy;
                            if (cx > maxX) maxX = cx;
                            if (cy > maxY) maxY = cy;

                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0)
                                    {
                                        continue;
                                    }

                                    int nx = cx + dx;
                                    int ny = cy + dy;
                                    if (nx < 0 || ny < 0 || nx >= GlyphCanvasSize || ny >= GlyphCanvasSize)
                                    {
                                        continue;
                                    }

                                    int next = ny * GlyphCanvasSize + nx;
                                    if (seen[next] || canvas.Alpha[next] == 0)
                                    {
                                        continue;
                                    }

                                    seen[next] = true;
                                    stack[stackCount++] = next;
                                }
                            }
                        }

                        stats.ComponentCount++;
                        if (area >= 2 && area <= 96 && Math.Max(maxX - minX + 1, maxY - minY + 1) <= 18)
                        {
                            stats.SmallComponentCount++;
                        }

                        if (minX < stats.MinX) stats.MinX = minX;
                        if (minY < stats.MinY) stats.MinY = minY;
                        if (maxX > stats.MaxX) stats.MaxX = maxX;
                        if (maxY > stats.MaxY) stats.MaxY = maxY;
                    }
                }

                return stats;
            }
        }

        private struct GlyphStats
        {
            public int ComponentCount;
            public int SmallComponentCount;
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
        }
    }
}
