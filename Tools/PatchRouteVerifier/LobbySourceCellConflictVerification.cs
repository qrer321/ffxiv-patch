using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyLobbySourceCellConflicts()
            {
                Console.WriteLine("[REPORT] lobby source cell conflicts");
                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);

                LobbySourceCellConflictStats stats = WriteLobbySourceCellConflictReports(reportDir);
                Pass(
                    "lobby source cell conflicts wrote {0} candidate glyph cells, {1} known FDT conflicts, {2} texture alpha conflicts, {3} data-center FDT conflicts, {4} source overlap pairs",
                    stats.CandidateCells,
                    stats.KnownFdtConflictCells,
                    stats.TextureAlphaConflictCells,
                    stats.DataCenterFdtConflictCells,
                    stats.SourceOverlapPairs);
            }

            private LobbySourceCellConflictStats WriteLobbySourceCellConflictReports(string reportDir)
            {
                string conflictPath = Path.Combine(reportDir, "lobby-source-cell-conflicts.tsv");
                string summaryPath = Path.Combine(reportDir, "lobby-source-cell-conflict-summary.tsv");
                string sourceOverlapPath = Path.Combine(reportDir, "lobby-source-cell-source-overlaps.tsv");
                Dictionary<string, HashSet<string>> fontsByScreen = CollectLobbyFontsByScreen();
                Dictionary<string, LobbyFontGlyphRequirement> requirements =
                    CollectLobbyFontGlyphRequirements(fontsByScreen, reportDir);
                HashSet<string> dataCenterFonts = ResolveDataCenterRoutedFonts();
                List<LobbyAtlasCellUse> activeCells = CollectActiveLobbyAtlasCellUses(_patchedFont);
                List<LobbyRequiredSourceCell> requiredSourceCells = new List<LobbyRequiredSourceCell>();
                Dictionary<string, byte[]> fdtCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, LobbySourceCellSummary> summaries =
                    new Dictionary<string, LobbySourceCellSummary>(StringComparer.OrdinalIgnoreCase);
                LobbySourceCellConflictStats stats = new LobbySourceCellConflictStats();

                using (StreamWriter writer = CreateUtf8Writer(conflictPath))
                {
                    writer.WriteLine("target_font\tcodepoint\tchar\tsource_path\tsource_texture\tchannel\tx\ty\twidth\theight\tpatched_alpha_pixels\tconflict_font\tconflict_codepoint\tconflict_char\tconflict_data_center_routed\tconflict_x\tconflict_y\tconflict_width\tconflict_height");

                    List<string> fonts = new List<string>(requirements.Keys);
                    fonts.Sort(StringComparer.OrdinalIgnoreCase);
                    for (int fontIndex = 0; fontIndex < fonts.Count; fontIndex++)
                    {
                        string fontPath = fonts[fontIndex];
                        if (!IsLobbyFontPath(fontPath))
                        {
                            continue;
                        }

                        byte[] targetFdt = TryReadFdt(_patchedFont, fontPath, fdtCache);
                        List<uint> codepoints = new List<uint>(requirements[fontPath].Codepoints);
                        codepoints.Sort();
                        for (int i = 0; i < codepoints.Count; i++)
                        {
                            uint codepoint = codepoints[i];
                            FdtGlyphEntry targetGlyph;
                            if (targetFdt != null && TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                            {
                                continue;
                            }

                            LobbySourceGlyph sourceGlyph;
                            if (!TryResolveLobbySourceGlyph(fontPath, codepoint, fdtCache, out sourceGlyph) ||
                                !IsLobbyTexturePath(sourceGlyph.TexturePath))
                            {
                                continue;
                            }

                            stats.CandidateCells++;
                            requiredSourceCells.Add(new LobbyRequiredSourceCell
                            {
                                TargetFontPath = fontPath,
                                Codepoint = codepoint,
                                SourceGlyph = sourceGlyph
                            });
                            LobbySourceCellSummary summary = GetOrAddSourceCellSummary(summaries, sourceGlyph.TexturePath);
                            summary.CandidateCells++;
                            summary.TargetFonts.Add(fontPath);

                            int patchedAlphaPixels = CountTextureAlphaPixels(
                                _patchedFont,
                                sourceGlyph.TexturePath,
                                sourceGlyph.Channel,
                                sourceGlyph.X,
                                sourceGlyph.Y,
                                sourceGlyph.Width,
                                sourceGlyph.Height);
                            if (patchedAlphaPixels > 0)
                            {
                                stats.TextureAlphaConflictCells++;
                                summary.TextureAlphaConflictCells++;
                            }

                            int conflictsForCell = 0;
                            int dataCenterConflictsForCell = 0;
                            for (int cellIndex = 0; cellIndex < activeCells.Count; cellIndex++)
                            {
                                LobbyAtlasCellUse active = activeCells[cellIndex];
                                if (!SourceGlyphOverlapsCell(sourceGlyph, active))
                                {
                                    continue;
                                }

                                conflictsForCell++;
                                bool dataCenterRouted = dataCenterFonts.Contains(active.FontPath);
                                if (dataCenterRouted)
                                {
                                    dataCenterConflictsForCell++;
                                }

                                WriteTsvRow(
                                    writer,
                                    fontPath,
                                    "U+" + codepoint.ToString("X4"),
                                    char.ConvertFromUtf32(checked((int)codepoint)),
                                    sourceGlyph.SourcePath ?? string.Empty,
                                    sourceGlyph.TexturePath ?? string.Empty,
                                    sourceGlyph.Channel.ToString(),
                                    sourceGlyph.X.ToString(),
                                    sourceGlyph.Y.ToString(),
                                    sourceGlyph.Width.ToString(),
                                    sourceGlyph.Height.ToString(),
                                    patchedAlphaPixels.ToString(),
                                    active.FontPath,
                                    "U+" + active.Codepoint.ToString("X4"),
                                    FormatAtlasCellCodepointChar(active.Codepoint),
                                    dataCenterRouted ? "yes" : "no",
                                    active.X.ToString(),
                                    active.Y.ToString(),
                                    active.Width.ToString(),
                                    active.Height.ToString());
                            }

                            if (conflictsForCell > 0)
                            {
                                stats.KnownFdtConflictCells++;
                                summary.KnownFdtConflictCells++;
                            }

                            if (dataCenterConflictsForCell > 0)
                            {
                                stats.DataCenterFdtConflictCells++;
                                summary.DataCenterFdtConflictCells++;
                            }
                        }
                    }
                }

                stats.SourceOverlapPairs = WriteLobbySourceCellSourceOverlapReport(sourceOverlapPath, requiredSourceCells);
                WriteLobbySourceCellConflictSummary(summaryPath, summaries);
                return stats;
            }

            private List<LobbyAtlasCellUse> CollectActiveLobbyAtlasCellUses(CompositeArchive archive)
            {
                List<LobbyAtlasCellUse> cells = new List<LobbyAtlasCellUse>();
                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    if (!archive.ContainsPath(fontPath))
                    {
                        continue;
                    }

                    byte[] fdt;
                    try
                    {
                        fdt = archive.ReadFile(fontPath);
                    }
                    catch
                    {
                        continue;
                    }

                    int fontTableOffset;
                    uint glyphCount;
                    int glyphStart;
                    if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                    {
                        continue;
                    }

                    for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                    {
                        int offset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                        FdtGlyphEntry glyph = ReadGlyphEntry(fdt, offset);
                        string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                        if (!IsLobbyTexturePath(texturePath))
                        {
                            continue;
                        }

                        uint codepoint;
                        if (!TryDecodeFdtUtf8Value(ReadU32LE(fdt, offset), out codepoint))
                        {
                            continue;
                        }

                        cells.Add(new LobbyAtlasCellUse
                        {
                            FontPath = fontPath,
                            Codepoint = codepoint,
                            TexturePath = texturePath,
                            Channel = glyph.ImageIndex % 4,
                            X = glyph.X,
                            Y = glyph.Y,
                            Width = glyph.Width,
                            Height = glyph.Height
                        });
                    }
                }

                return cells;
            }

            private int CountTextureAlphaPixels(
                CompositeArchive archive,
                string texturePath,
                int channel,
                int x,
                int y,
                int width,
                int height)
            {
                try
                {
                    Texture texture = ReadFontTexture(archive, texturePath);
                    int left = Math.Max(0, x);
                    int top = Math.Max(0, y);
                    int right = Math.Min(texture.Width, x + Math.Max(0, width));
                    int bottom = Math.Min(texture.Height, y + Math.Max(0, height));
                    int count = 0;
                    for (int yy = top; yy < bottom; yy++)
                    {
                        int row = yy * texture.Width;
                        for (int xx = left; xx < right; xx++)
                        {
                            if (ReadTextureChannel(texture, row + xx, channel) != 0)
                            {
                                count++;
                            }
                        }
                    }

                    return count;
                }
                catch
                {
                    return 0;
                }
            }

            private static int ReadTextureChannel(Texture texture, int pixel, int channel)
            {
                int offset = pixel * 2;
                byte lo = texture.Data[offset];
                byte hi = texture.Data[offset + 1];
                switch (channel)
                {
                    case 0: return hi & 0x0F;
                    case 1: return (lo >> 4) & 0x0F;
                    case 2: return lo & 0x0F;
                    case 3: return (hi >> 4) & 0x0F;
                    default: return 0;
                }
            }

            private static bool SourceGlyphOverlapsCell(LobbySourceGlyph source, LobbyAtlasCellUse cell)
            {
                return string.Equals(source.TexturePath, cell.TexturePath, StringComparison.OrdinalIgnoreCase) &&
                    source.Channel == cell.Channel &&
                    RectanglesOverlap(source.X, source.Y, source.Width, source.Height, cell.X, cell.Y, cell.Width, cell.Height);
            }

            private static bool SourceGlyphsOverlap(LobbySourceGlyph left, LobbySourceGlyph right)
            {
                return string.Equals(left.TexturePath, right.TexturePath, StringComparison.OrdinalIgnoreCase) &&
                    left.Channel == right.Channel &&
                    RectanglesOverlap(left.X, left.Y, left.Width, left.Height, right.X, right.Y, right.Width, right.Height);
            }

            private static bool SourceGlyphsUseSameCell(LobbySourceGlyph left, LobbySourceGlyph right)
            {
                return string.Equals(left.TexturePath, right.TexturePath, StringComparison.OrdinalIgnoreCase) &&
                    left.Channel == right.Channel &&
                    left.X == right.X &&
                    left.Y == right.Y &&
                    left.Width == right.Width &&
                    left.Height == right.Height;
            }

            private static bool RectanglesOverlap(int leftX, int leftY, int leftWidth, int leftHeight, int rightX, int rightY, int rightWidth, int rightHeight)
            {
                return leftWidth > 0 &&
                    leftHeight > 0 &&
                    rightWidth > 0 &&
                    rightHeight > 0 &&
                    leftX < rightX + rightWidth &&
                    leftX + leftWidth > rightX &&
                    leftY < rightY + rightHeight &&
                    leftY + leftHeight > rightY;
            }

            private static string FormatAtlasCellCodepointChar(uint codepoint)
            {
                if (codepoint < 0x20u || codepoint > 0x10FFFFu)
                {
                    return string.Empty;
                }

                if (codepoint == 0x22u)
                {
                    return "\\u0022";
                }

                if (codepoint == 0x5Cu)
                {
                    return "\\\\";
                }

                try
                {
                    return char.ConvertFromUtf32(checked((int)codepoint));
                }
                catch
                {
                    return string.Empty;
                }
            }

            private static uint ReadU32LE(byte[] data, int offset)
            {
                return data[offset] |
                    ((uint)data[offset + 1] << 8) |
                    ((uint)data[offset + 2] << 16) |
                    ((uint)data[offset + 3] << 24);
            }

            private static LobbySourceCellSummary GetOrAddSourceCellSummary(
                Dictionary<string, LobbySourceCellSummary> summaries,
                string texturePath)
            {
                LobbySourceCellSummary summary;
                if (!summaries.TryGetValue(texturePath, out summary))
                {
                    summary = new LobbySourceCellSummary(texturePath);
                    summaries.Add(texturePath, summary);
                }

                return summary;
            }

            private static void WriteLobbySourceCellConflictSummary(
                string summaryPath,
                Dictionary<string, LobbySourceCellSummary> summaries)
            {
                List<string> texturePaths = new List<string>(summaries.Keys);
                texturePaths.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(summaryPath))
                {
                    writer.WriteLine("source_texture\tcandidate_cells\tknown_fdt_conflict_cells\ttexture_alpha_conflict_cells\tdata_center_fdt_conflict_cells\ttarget_fonts");
                    for (int i = 0; i < texturePaths.Count; i++)
                    {
                        LobbySourceCellSummary summary = summaries[texturePaths[i]];
                        WriteTsvRow(
                            writer,
                            summary.TexturePath,
                            summary.CandidateCells.ToString(),
                            summary.KnownFdtConflictCells.ToString(),
                            summary.TextureAlphaConflictCells.ToString(),
                            summary.DataCenterFdtConflictCells.ToString(),
                            JoinSorted(summary.TargetFonts));
                    }
                }
            }

            private static int WriteLobbySourceCellSourceOverlapReport(
                string reportPath,
                List<LobbyRequiredSourceCell> cells)
            {
                int overlaps = 0;
                using (StreamWriter writer = CreateUtf8Writer(reportPath))
                {
                    writer.WriteLine("left_target_font\tleft_codepoint\tleft_char\tleft_source_texture\tleft_channel\tleft_x\tleft_y\tleft_width\tleft_height\tright_target_font\tright_codepoint\tright_char\tright_source_texture\tright_channel\tright_x\tright_y\tright_width\tright_height");
                    for (int leftIndex = 0; leftIndex < cells.Count; leftIndex++)
                    {
                        LobbyRequiredSourceCell left = cells[leftIndex];
                        for (int rightIndex = leftIndex + 1; rightIndex < cells.Count; rightIndex++)
                        {
                            LobbyRequiredSourceCell right = cells[rightIndex];
                            if (!SourceGlyphsOverlap(left.SourceGlyph, right.SourceGlyph) ||
                                SourceGlyphsUseSameCell(left.SourceGlyph, right.SourceGlyph))
                            {
                                continue;
                            }

                            overlaps++;
                            WriteTsvRow(
                                writer,
                                left.TargetFontPath,
                                "U+" + left.Codepoint.ToString("X4"),
                                char.ConvertFromUtf32(checked((int)left.Codepoint)),
                                left.SourceGlyph.TexturePath ?? string.Empty,
                                left.SourceGlyph.Channel.ToString(),
                                left.SourceGlyph.X.ToString(),
                                left.SourceGlyph.Y.ToString(),
                                left.SourceGlyph.Width.ToString(),
                                left.SourceGlyph.Height.ToString(),
                                right.TargetFontPath,
                                "U+" + right.Codepoint.ToString("X4"),
                                char.ConvertFromUtf32(checked((int)right.Codepoint)),
                                right.SourceGlyph.TexturePath ?? string.Empty,
                                right.SourceGlyph.Channel.ToString(),
                                right.SourceGlyph.X.ToString(),
                                right.SourceGlyph.Y.ToString(),
                                right.SourceGlyph.Width.ToString(),
                                right.SourceGlyph.Height.ToString());
                        }
                    }
                }

                return overlaps;
            }

            private sealed class LobbySourceCellConflictStats
            {
                public int CandidateCells;
                public int KnownFdtConflictCells;
                public int TextureAlphaConflictCells;
                public int DataCenterFdtConflictCells;
                public int SourceOverlapPairs;
            }

            private sealed class LobbySourceCellSummary
            {
                public readonly string TexturePath;
                public readonly HashSet<string> TargetFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                public int CandidateCells;
                public int KnownFdtConflictCells;
                public int TextureAlphaConflictCells;
                public int DataCenterFdtConflictCells;

                public LobbySourceCellSummary(string texturePath)
                {
                    TexturePath = texturePath;
                }
            }

            private struct LobbyAtlasCellUse
            {
                public string FontPath;
                public uint Codepoint;
                public string TexturePath;
                public int Channel;
                public int X;
                public int Y;
                public int Width;
                public int Height;
            }

            private struct LobbyRequiredSourceCell
            {
                public string TargetFontPath;
                public uint Codepoint;
                public LobbySourceGlyph SourceGlyph;
            }
        }
    }
}
