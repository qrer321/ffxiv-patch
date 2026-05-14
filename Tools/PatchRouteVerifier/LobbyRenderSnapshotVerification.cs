using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const string LobbyRenderSnapshotGroup = "lobby-render";
            private const double LowScaleSimilarityFailThreshold = 0.035;
            private static readonly string SettingsChangedPhrase = LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages[0];
            private static readonly string HighResolutionFhdPhrase = LobbyScaledHangulPhrases.HighResolutionUiScaleOptions[2];

            private static readonly LobbyRenderCase[] LobbyRenderCases = new LobbyRenderCase[]
            {
                new LobbyRenderCase("dc-axis12-elemental", "common/font/AXIS_12_lobby.fdt", "Elemental", 1.0),
                new LobbyRenderCase("dc-axis14-pandaemonium", "common/font/AXIS_14_lobby.fdt", "Pandaemonium", 1.0),
                new LobbyRenderCase("dc-jupiter20-worlds", "common/font/Jupiter_20_lobby.fdt", "Aegis  Atomos  Carbuncle", 1.0),
                new LobbyRenderCase("dc-trump23-title", "common/font/TrumpGothic_23_lobby.fdt", "JAPAN DATA CENTER", 1.0),
                new LobbyRenderCase("settings-axis12-result", "common/font/AXIS_12_lobby.fdt", SettingsChangedPhrase, 1.0),
                new LobbyRenderCase("settings-axis14-fhd", "common/font/AXIS_14_lobby.fdt", HighResolutionFhdPhrase, 1.0),
                new LobbyRenderCase("settings-axis18-result", "common/font/AXIS_18_lobby.fdt", SettingsChangedPhrase, 1.0),
                new LobbyRenderCase("settings-axis36-result", "common/font/AXIS_36_lobby.fdt", SettingsChangedPhrase, 1.0),
                new LobbyRenderCase("settings-global-axis18-result", "common/font/AXIS_18.fdt", SettingsChangedPhrase, 1.0, true),
                new LobbyRenderCase("settings-global-axis36-result", "common/font/AXIS_36.fdt", SettingsChangedPhrase, 1.0, true),
                new LobbyRenderCase("settings-global-krnaxis180-result", "common/font/KrnAXIS_180.fdt", SettingsChangedPhrase, 1.0, true),
                new LobbyRenderCase("settings-global-krnaxis360-result", "common/font/KrnAXIS_360.fdt", SettingsChangedPhrase, 1.0, true),
                new LobbyRenderCase("settings-axis12-ui150-result", "common/font/AXIS_12.fdt", SettingsChangedPhrase, 1.5, false, 1),
                new LobbyRenderCase("settings-axis14-ui150-fhd", "common/font/AXIS_14.fdt", HighResolutionFhdPhrase, 1.5, false, 1),
                new LobbyRenderCase("settings-axis14-ui200-result", "common/font/AXIS_14.fdt", SettingsChangedPhrase, 2.0, false, 1),
                new LobbyRenderCase("settings-axis14-ui300-result", "common/font/AXIS_14.fdt", SettingsChangedPhrase, 3.0, false, 1)
            };

            private static readonly LobbyScaleComparisonCase[] LobbyScaleComparisonCases = new LobbyScaleComparisonCase[]
            {
                new LobbyScaleComparisonCase("axis18-vs-axis12-150-result", "common/font/AXIS_18_lobby.fdt", "common/font/AXIS_12_lobby.fdt", SettingsChangedPhrase, 1.5),
                new LobbyScaleComparisonCase("axis18-vs-axis12-150-fhd", "common/font/AXIS_18_lobby.fdt", "common/font/AXIS_12_lobby.fdt", HighResolutionFhdPhrase, 1.5),
                new LobbyScaleComparisonCase("axis36-vs-axis18-200-result", "common/font/AXIS_36_lobby.fdt", "common/font/AXIS_18_lobby.fdt", SettingsChangedPhrase, 2.0),
                new LobbyScaleComparisonCase("axis36-vs-axis12-300-result", "common/font/AXIS_36_lobby.fdt", "common/font/AXIS_12_lobby.fdt", SettingsChangedPhrase, 3.0),
                new LobbyScaleComparisonCase("global-axis18-vs-axis12-150-result", "common/font/AXIS_18.fdt", "common/font/AXIS_12.fdt", SettingsChangedPhrase, 1.5, true),
                new LobbyScaleComparisonCase("global-axis36-vs-axis18-200-result", "common/font/AXIS_36.fdt", "common/font/AXIS_18.fdt", SettingsChangedPhrase, 2.0, true),
                new LobbyScaleComparisonCase("global-axis36-vs-axis12-300-result", "common/font/AXIS_36.fdt", "common/font/AXIS_12.fdt", SettingsChangedPhrase, 3.0, true),
                new LobbyScaleComparisonCase("krnaxis180-vs-krnaxis120-150-result", "common/font/KrnAXIS_180.fdt", "common/font/KrnAXIS_120.fdt", SettingsChangedPhrase, 1.5, true),
                new LobbyScaleComparisonCase("krnaxis360-vs-krnaxis180-200-result", "common/font/KrnAXIS_360.fdt", "common/font/KrnAXIS_180.fdt", SettingsChangedPhrase, 2.0, true)
            };

            private void VerifyLobbyRenderSnapshots()
            {
                Console.WriteLine("[FDT] Lobby scaled render snapshots");
                EnsureLobbyRenderReport();

                for (int i = 0; i < LobbyRenderCases.Length; i++)
                {
                    VerifyLobbyRenderCase(LobbyRenderCases[i]);
                }

                for (int i = 0; i < LobbyScaleComparisonCases.Length; i++)
                {
                    VerifyLobbyScaleComparisonCase(LobbyScaleComparisonCases[i]);
                }
            }

            private void VerifyLobbyRenderCase(LobbyRenderCase renderCase)
            {
                PhraseRenderSnapshot snapshot;
                string error;
                if (!TryRenderPhrasePixels(_patchedFont, renderCase.FontPath, renderCase.Phrase, true, out snapshot, out error))
                {
                    Fail("{0} render snapshot error for {1}: {2}", renderCase.Id, renderCase.FontPath, error);
                    return;
                }

                PhraseLayoutResult layout;
                if (!TryMeasurePhraseLayout(_patchedFont, renderCase.FontPath, renderCase.Phrase, true, out layout, out error))
                {
                    Fail("{0} render layout error for {1}: {2}", renderCase.Id, renderCase.FontPath, error);
                    return;
                }

                LobbyRenderStats stats = AnalyzeLobbyRender(snapshot);
                string pngPath = DumpLobbyRenderSnapshot(renderCase.Id, renderCase.FontPath, renderCase.Phrase, snapshot, renderCase.OutputScale);
                WriteLobbyRenderReport(renderCase.Id, renderCase.FontPath, renderCase.Phrase, renderCase.OutputScale, stats, layout, pngPath, null);

                if (layout.OverlapPixels > 0 || layout.MinimumGapPixels < renderCase.MinimumGapFloor)
                {
                    if (!renderCase.DiagnosticOnly)
                    {
                        Fail(
                            "{0} render snapshot has insufficient text gap: overlap={1}, minGap={2}, required={3}, pair={4}, font={5}, phrase=[{6}], png={7}",
                            renderCase.Id,
                            layout.OverlapPixels,
                            layout.MinimumGapPixels,
                            renderCase.MinimumGapFloor,
                            FormatCodepointPair(layout.MinimumGapLeftCodepoint, layout.MinimumGapRightCodepoint),
                            renderCase.FontPath,
                            Escape(renderCase.Phrase),
                            pngPath ?? "n/a");
                        return;
                    }

                    Warn(
                        "{0} diagnostic render snapshot has insufficient text gap: overlap={1}, minGap={2}, required={3}, pair={4}, font={5}, phrase=[{6}], png={7}",
                        renderCase.Id,
                        layout.OverlapPixels,
                        layout.MinimumGapPixels,
                        renderCase.MinimumGapFloor,
                        FormatCodepointPair(layout.MinimumGapLeftCodepoint, layout.MinimumGapRightCodepoint),
                        renderCase.FontPath,
                        Escape(renderCase.Phrase),
                        pngPath ?? "n/a");
                }

                Pass(
                    "{0} render snapshot font={1}, phrase=[{2}], glyphs={3}, width={4}, bbox={5}x{6}, fringe={7:P1}, minGap={8}, pair={9}, png={10}",
                    renderCase.Id,
                    renderCase.FontPath,
                    Escape(renderCase.Phrase),
                    snapshot.Glyphs,
                    snapshot.Width,
                    stats.Width,
                    stats.Height,
                    stats.FringeRatio,
                    layout.MinimumGapPixels,
                    FormatCodepointPair(layout.MinimumGapLeftCodepoint, layout.MinimumGapRightCodepoint),
                    renderCase.DiagnosticOnly && pngPath != null
                        ? pngPath + " (diagnostic route)"
                        : pngPath ?? "disabled");
            }

            private void VerifyLobbyScaleComparisonCase(LobbyScaleComparisonCase comparison)
            {
                PhraseRenderSnapshot highSnapshot;
                PhraseRenderSnapshot lowSnapshot;
                string error;
                if (!TryRenderPhrasePixels(_patchedFont, comparison.HighScaleFontPath, comparison.Phrase, true, out highSnapshot, out error))
                {
                    Fail("{0} high-scale render error for {1}: {2}", comparison.Id, comparison.HighScaleFontPath, error);
                    return;
                }

                if (!TryRenderPhrasePixels(_patchedFont, comparison.LowScaleFontPath, comparison.Phrase, true, out lowSnapshot, out error))
                {
                    Fail("{0} low-scale render error for {1}: {2}", comparison.Id, comparison.LowScaleFontPath, error);
                    return;
                }

                double diff = ComputeScaledRenderDifference(highSnapshot, lowSnapshot, comparison.LowToHighScale);
                string highPng = DumpLobbyRenderSnapshot(comparison.Id + "_high", comparison.HighScaleFontPath, comparison.Phrase, highSnapshot, 1.0);
                string lowPng = DumpLobbyRenderSnapshot(comparison.Id + "_low_scaled", comparison.LowScaleFontPath, comparison.Phrase, lowSnapshot, comparison.LowToHighScale);
                WriteLobbyRenderComparisonReport(comparison, diff, highPng, lowPng);

                if (diff <= LowScaleSimilarityFailThreshold)
                {
                    if (comparison.DiagnosticOnly)
                    {
                        Warn(
                            "{0} diagnostic high-scale render is too close to nearest-neighbor low-scale upscale: diff={1:F4}, threshold={2:F4}, high={3}, low={4}, phrase=[{5}], highPng={6}, lowPng={7}",
                            comparison.Id,
                            diff,
                            LowScaleSimilarityFailThreshold,
                            comparison.HighScaleFontPath,
                            comparison.LowScaleFontPath,
                            Escape(comparison.Phrase),
                            highPng ?? "n/a",
                            lowPng ?? "n/a");
                        return;
                    }

                    Fail(
                        "{0} high-scale render is too close to nearest-neighbor low-scale upscale: diff={1:F4}, threshold={2:F4}, high={3}, low={4}, phrase=[{5}], highPng={6}, lowPng={7}",
                        comparison.Id,
                        diff,
                        LowScaleSimilarityFailThreshold,
                        comparison.HighScaleFontPath,
                        comparison.LowScaleFontPath,
                        Escape(comparison.Phrase),
                        highPng ?? "n/a",
                        lowPng ?? "n/a");
                    return;
                }

                Pass(
                    "{0} high-scale render differs from low-scale nearest upscale: diff={1:F4}, high={2}, low={3}, png={4}",
                    comparison.Id,
                    diff,
                    comparison.HighScaleFontPath,
                    comparison.LowScaleFontPath,
                    highPng ?? "disabled");
            }

            private void EnsureLobbyRenderReport()
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                File.WriteAllText(
                    Path.Combine(_glyphDumpDir, "lobby-render-report.tsv"),
                    "kind\tid\tfont\tphrase\tscale\twidth\theight\tvisible\tfringe_ratio\tlayout_width\tmin_gap\tmin_pair\toverlap\tdiff\tpng\tpng2" + Environment.NewLine,
                    Encoding.UTF8);
            }

            private string DumpLobbyRenderSnapshot(string id, string fontPath, string phrase, PhraseRenderSnapshot snapshot, double outputScale)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return null;
                }

                string fileName = LobbyRenderSnapshotGroup + "_" + SanitizeFileName(id) + "_" + SanitizeFileName(fontPath) + ".png";
                string pngPath = Path.Combine(_glyphDumpDir, fileName);
                WritePhraseSnapshotPng(snapshot, outputScale, pngPath);
                return pngPath;
            }

            private void WriteLobbyRenderReport(string id, string fontPath, string phrase, double outputScale, LobbyRenderStats stats, PhraseLayoutResult layout, string pngPath, double? diff)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                string line = string.Join(
                    "\t",
                    new string[]
                    {
                        "snapshot",
                        EscapeTsv(id),
                        EscapeTsv(fontPath),
                        EscapeTsv(phrase),
                        outputScale.ToString("0.###", CultureInfo.InvariantCulture),
                        stats.Width.ToString(CultureInfo.InvariantCulture),
                        stats.Height.ToString(CultureInfo.InvariantCulture),
                        stats.VisiblePixels.ToString(CultureInfo.InvariantCulture),
                        stats.FringeRatio.ToString("0.####", CultureInfo.InvariantCulture),
                        layout.Width.ToString(CultureInfo.InvariantCulture),
                        layout.MinimumGapPixels.ToString(CultureInfo.InvariantCulture),
                        EscapeTsv(FormatCodepointPair(layout.MinimumGapLeftCodepoint, layout.MinimumGapRightCodepoint)),
                        layout.OverlapPixels.ToString(CultureInfo.InvariantCulture),
                        diff.HasValue ? diff.Value.ToString("0.####", CultureInfo.InvariantCulture) : string.Empty,
                        EscapeTsv(pngPath),
                        string.Empty
                    });
                File.AppendAllText(Path.Combine(_glyphDumpDir, "lobby-render-report.tsv"), line + Environment.NewLine, Encoding.UTF8);
            }

            private void WriteLobbyRenderComparisonReport(LobbyScaleComparisonCase comparison, double diff, string highPng, string lowPng)
            {
                if (string.IsNullOrWhiteSpace(_glyphDumpDir))
                {
                    return;
                }

                string line = string.Join(
                    "\t",
                    new string[]
                    {
                        "comparison",
                        EscapeTsv(comparison.Id),
                        EscapeTsv(comparison.HighScaleFontPath + " <= " + comparison.LowScaleFontPath),
                        EscapeTsv(comparison.Phrase),
                        comparison.LowToHighScale.ToString("0.###", CultureInfo.InvariantCulture),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        diff.ToString("0.####", CultureInfo.InvariantCulture),
                        EscapeTsv(highPng),
                        EscapeTsv(lowPng)
                    });
                File.AppendAllText(Path.Combine(_glyphDumpDir, "lobby-render-report.tsv"), line + Environment.NewLine, Encoding.UTF8);
            }

            private static string FormatCodepointPair(uint left, uint right)
            {
                if (left == 0 && right == 0)
                {
                    return "n/a";
                }

                return "U+" + left.ToString("X4", CultureInfo.InvariantCulture) +
                       "/U+" + right.ToString("X4", CultureInfo.InvariantCulture);
            }

            private static LobbyRenderStats AnalyzeLobbyRender(PhraseRenderSnapshot snapshot)
            {
                LobbySnapshotBounds bounds = GetSnapshotBounds(snapshot);
                int fringePixels = 0;
                foreach (KeyValuePair<long, byte> pair in snapshot.Pixels)
                {
                    byte alpha = pair.Value;
                    if (alpha > 0 && alpha < 240)
                    {
                        fringePixels++;
                    }
                }

                LobbyRenderStats stats = new LobbyRenderStats();
                stats.Width = bounds.HasPixels ? bounds.MaxX - bounds.MinX + 1 : 0;
                stats.Height = bounds.HasPixels ? bounds.MaxY - bounds.MinY + 1 : 0;
                stats.VisiblePixels = snapshot.Pixels == null ? 0 : snapshot.Pixels.Count;
                stats.FringeRatio = stats.VisiblePixels == 0 ? 0.0 : (double)fringePixels / stats.VisiblePixels;
                return stats;
            }

            private static double ComputeScaledRenderDifference(PhraseRenderSnapshot highSnapshot, PhraseRenderSnapshot lowSnapshot, double lowToHighScale)
            {
                LobbySnapshotBounds highBounds = GetSnapshotBounds(highSnapshot);
                LobbySnapshotBounds lowBounds = GetSnapshotBounds(lowSnapshot);
                if (!highBounds.HasPixels || !lowBounds.HasPixels)
                {
                    return 1.0;
                }

                int highWidth = highBounds.MaxX - highBounds.MinX + 1;
                int highHeight = highBounds.MaxY - highBounds.MinY + 1;
                int lowScaledWidth = Math.Max(1, (int)Math.Ceiling((lowBounds.MaxX - lowBounds.MinX + 1) * lowToHighScale));
                int lowScaledHeight = Math.Max(1, (int)Math.Ceiling((lowBounds.MaxY - lowBounds.MinY + 1) * lowToHighScale));
                int width = Math.Max(highWidth, lowScaledWidth);
                int height = Math.Max(highHeight, lowScaledHeight);
                byte[] high = RasterizeSnapshot(highSnapshot, highBounds, 1.0, width, height);
                byte[] low = RasterizeSnapshot(lowSnapshot, lowBounds, lowToHighScale, width, height);
                long diff = 0;
                long denominator = 0;
                for (int i = 0; i < high.Length; i++)
                {
                    diff += Math.Abs(high[i] - low[i]);
                    denominator += Math.Max(high[i], low[i]);
                }

                return denominator == 0 ? 1.0 : (double)diff / denominator;
            }

            private static byte[] RasterizeSnapshot(PhraseRenderSnapshot snapshot, LobbySnapshotBounds bounds, double scale, int width, int height)
            {
                byte[] pixels = new byte[Math.Max(1, width) * Math.Max(1, height)];
                foreach (KeyValuePair<long, byte> pair in snapshot.Pixels)
                {
                    int sourceX = DecodeSnapshotX(pair.Key) - bounds.MinX;
                    int sourceY = DecodeSnapshotY(pair.Key) - bounds.MinY;
                    int x0 = (int)Math.Floor(sourceX * scale);
                    int x1 = Math.Max(x0 + 1, (int)Math.Ceiling((sourceX + 1) * scale));
                    int y0 = (int)Math.Floor(sourceY * scale);
                    int y1 = Math.Max(y0 + 1, (int)Math.Ceiling((sourceY + 1) * scale));
                    for (int y = y0; y < y1 && y < height; y++)
                    {
                        if (y < 0)
                        {
                            continue;
                        }

                        int rowOffset = y * width;
                        for (int x = x0; x < x1 && x < width; x++)
                        {
                            if (x < 0)
                            {
                                continue;
                            }

                            int offset = rowOffset + x;
                            if (pair.Value > pixels[offset])
                            {
                                pixels[offset] = pair.Value;
                            }
                        }
                    }
                }

                return pixels;
            }

            private static void WritePhraseSnapshotPng(PhraseRenderSnapshot snapshot, double scale, string path)
            {
                LobbySnapshotBounds bounds = GetSnapshotBounds(snapshot);
                int margin = 8;
                int contentWidth = bounds.HasPixels ? bounds.MaxX - bounds.MinX + 1 : 1;
                int contentHeight = bounds.HasPixels ? bounds.MaxY - bounds.MinY + 1 : 1;
                int width = Math.Max(1, (int)Math.Ceiling(contentWidth * scale)) + margin * 2;
                int height = Math.Max(1, (int)Math.Ceiling(contentHeight * scale)) + margin * 2;
                byte[] raster = RasterizeSnapshot(snapshot, bounds, scale, width - margin * 2, height - margin * 2);
                using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.FromArgb(255, 24, 24, 24));
                    }

                    for (int y = 0; y < height - margin * 2; y++)
                    {
                        int rowOffset = y * (width - margin * 2);
                        for (int x = 0; x < width - margin * 2; x++)
                        {
                            byte alpha = raster[rowOffset + x];
                            if (alpha == 0)
                            {
                                continue;
                            }

                            bitmap.SetPixel(margin + x, margin + y, Color.FromArgb(255, alpha, alpha, alpha));
                        }
                    }

                    bitmap.Save(path, ImageFormat.Png);
                }
            }

            private static LobbySnapshotBounds GetSnapshotBounds(PhraseRenderSnapshot snapshot)
            {
                LobbySnapshotBounds bounds = new LobbySnapshotBounds();
                bounds.MinX = int.MaxValue;
                bounds.MinY = int.MaxValue;
                bounds.MaxX = int.MinValue;
                bounds.MaxY = int.MinValue;
                if (snapshot.Pixels == null)
                {
                    return bounds;
                }

                foreach (KeyValuePair<long, byte> pair in snapshot.Pixels)
                {
                    int x = DecodeSnapshotX(pair.Key);
                    int y = DecodeSnapshotY(pair.Key);
                    if (x < bounds.MinX)
                    {
                        bounds.MinX = x;
                    }

                    if (x > bounds.MaxX)
                    {
                        bounds.MaxX = x;
                    }

                    if (y < bounds.MinY)
                    {
                        bounds.MinY = y;
                    }

                    if (y > bounds.MaxY)
                    {
                        bounds.MaxY = y;
                    }
                }

                bounds.HasPixels = bounds.MinX <= bounds.MaxX && bounds.MinY <= bounds.MaxY;
                return bounds;
            }

            private static int DecodeSnapshotX(long key)
            {
                return unchecked((int)(uint)key);
            }

            private static int DecodeSnapshotY(long key)
            {
                return (int)(key >> 32);
            }

            private struct LobbyRenderCase
            {
                public readonly string Id;
                public readonly string FontPath;
                public readonly string Phrase;
                public readonly double OutputScale;
                public readonly bool DiagnosticOnly;
                public readonly int MinimumGapFloor;

                public LobbyRenderCase(string id, string fontPath, string phrase, double outputScale)
                    : this(id, fontPath, phrase, outputScale, false, 0)
                {
                }

                public LobbyRenderCase(string id, string fontPath, string phrase, double outputScale, bool diagnosticOnly)
                    : this(id, fontPath, phrase, outputScale, diagnosticOnly, 0)
                {
                }

                public LobbyRenderCase(string id, string fontPath, string phrase, double outputScale, bool diagnosticOnly, int minimumGapFloor)
                {
                    Id = id;
                    FontPath = fontPath;
                    Phrase = phrase;
                    OutputScale = outputScale;
                    DiagnosticOnly = diagnosticOnly;
                    MinimumGapFloor = minimumGapFloor;
                }
            }

            private struct LobbyScaleComparisonCase
            {
                public readonly string Id;
                public readonly string HighScaleFontPath;
                public readonly string LowScaleFontPath;
                public readonly string Phrase;
                public readonly double LowToHighScale;
                public readonly bool DiagnosticOnly;

                public LobbyScaleComparisonCase(string id, string highScaleFontPath, string lowScaleFontPath, string phrase, double lowToHighScale)
                    : this(id, highScaleFontPath, lowScaleFontPath, phrase, lowToHighScale, false)
                {
                }

                public LobbyScaleComparisonCase(string id, string highScaleFontPath, string lowScaleFontPath, string phrase, double lowToHighScale, bool diagnosticOnly)
                {
                    Id = id;
                    HighScaleFontPath = highScaleFontPath;
                    LowScaleFontPath = lowScaleFontPath;
                    Phrase = phrase;
                    LowToHighScale = lowToHighScale;
                    DiagnosticOnly = diagnosticOnly;
                }
            }

            private struct LobbyRenderStats
            {
                public int Width;
                public int Height;
                public int VisiblePixels;
                public double FringeRatio;
            }

            private struct LobbySnapshotBounds
            {
                public bool HasPixels;
                public int MinX;
                public int MinY;
                public int MaxX;
                public int MaxY;
            }
        }
    }
}
