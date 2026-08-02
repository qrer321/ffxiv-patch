using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int InGameFontScaleSampleLimit = 64;
            private const double InGameFontPartialScaleRiskSpread = 1.05d;
            private const double InGameFontOversizeCandidateRatio = 1.08d;

            private void VerifyInGameFontScaleSurvey()
            {
                Console.WriteLine("[REPORT] in-game font scale survey");
                if (_ttmpFont == null)
                {
                    Fail("In-game font scale survey requires the TTMP source font package");
                    return;
                }

                string reportDir = ResolveInGameFontRiskReportDir();
                Directory.CreateDirectory(reportDir);
                string reportPath = Path.Combine(reportDir, "ingame-font-scale-survey.tsv");
                int fonts = 0;
                int partialScaleCandidates = 0;
                int oversizeCandidates = 0;
                int readErrors = 0;

                using (StreamWriter writer = CreateUtf8Writer(reportPath))
                {
                    writer.WriteLine(
                        "font\tcompared_hangul\tmissing_target\tcell_changed\tmetric_changed\tmetric_change_coverage\tmetric_changed_codepoints" +
                        "\tsource_font_size\tsource_line_height\tsource_ascent\ttarget_font_size\ttarget_line_height\ttarget_ascent\ttarget_hangul_line_ratio\ttarget_hangul_font_size_ratio" +
                        "\tsource_digit_height\ttarget_digit_height\tsource_sample\tsource_hangul_height\tsource_hangul_digit_ratio" +
                        "\ttarget_sample\ttarget_hangul_height\ttarget_hangul_digit_ratio\ttarget_source_height_ratio" +
                        "\tchanged_sample\tchanged_hangul_height\tchanged_hangul_digit_ratio" +
                        "\tunchanged_sample\tunchanged_hangul_height\tunchanged_hangul_digit_ratio\tchanged_unchanged_spread\tstatus\terror");

                    for (int fontIndex = 0; fontIndex < DialoguePhraseFontPaths.Length; fontIndex++)
                    {
                        string fontPath = DialoguePhraseFontPaths[fontIndex];
                        InGameFontScaleSurveyRow row;
                        string error;
                        if (!TrySurveyInGameFontScale(fontPath, out row, out error))
                        {
                            WriteTsvRow(
                                writer,
                                fontPath,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                error);
                            readErrors++;
                            continue;
                        }

                        fonts++;
                        WriteInGameFontScaleSurveyRow(writer, row);
                        if (row.IsPartialScaleRisk)
                        {
                            partialScaleCandidates++;
                            Console.WriteLine(
                                "  CANDIDATE partial scale {0}: metrics={1}/{2}, changed/unchanged={3}",
                                fontPath,
                                row.MetricChanged,
                                row.ComparedHangul,
                                FormatInGameFontSurveyDouble(row.ChangedUnchangedSpread));
                        }

                        if (row.IsOversizeCandidate)
                        {
                            oversizeCandidates++;
                            Console.WriteLine(
                                "  CANDIDATE oversized Hangul {0}: visible/font-size={1}, digit-ratio={2}",
                                fontPath,
                                FormatInGameFontSurveyDouble(row.TargetHangulFontSizeRatio),
                                FormatInGameFontSurveyDouble(row.TargetHangulDigitRatio));
                        }
                    }
                }

                WriteInGameFontCellReuseSurvey(reportDir);

                Pass(
                    "in-game font scale survey wrote {0} fonts, {1} partial-scale candidates, {2} oversize candidates, {3} read errors: {4}",
                    fonts,
                    partialScaleCandidates,
                    oversizeCandidates,
                    readErrors,
                    reportPath);
            }

            private void WriteInGameFontCellReuseSurvey(string reportDir)
            {
                const int BucketSize = 64;
                Dictionary<string, List<InGameFontCellReference>> buckets =
                    new Dictionary<string, List<InGameFontCellReference>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, List<InGameFontCellReference>> targets =
                    new Dictionary<string, List<InGameFontCellReference>>(StringComparer.OrdinalIgnoreCase);
                int referenceId = 0;

                string[] payloadPaths = _ttmpFont.GetPayloadPaths();
                for (int pathIndex = 0; pathIndex < payloadPaths.Length; pathIndex++)
                {
                    string fontPath = payloadPaths[pathIndex];
                    if (!fontPath.EndsWith(".fdt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    byte[] fdt;
                    try
                    {
                        fdt = _ttmpFont.ReadFile(fontPath);
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

                    bool targetFont = ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(fontPath);
                    for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                    {
                        int glyphOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                        uint codepoint;
                        if (!TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(fdt, glyphOffset), out codepoint))
                        {
                            continue;
                        }

                        FdtGlyphEntry glyph = ReadGlyphEntry(fdt, glyphOffset);
                        if (glyph.Width == 0 || glyph.Height == 0)
                        {
                            continue;
                        }

                        string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                        if (string.IsNullOrEmpty(texturePath))
                        {
                            continue;
                        }

                        InGameFontCellReference reference = new InGameFontCellReference
                        {
                            Id = referenceId++,
                            FontPath = fontPath,
                            Codepoint = codepoint,
                            TexturePath = texturePath,
                            ImageIndex = glyph.ImageIndex,
                            Channel = glyph.ImageIndex % 4,
                            X = glyph.X,
                            Y = glyph.Y,
                            Width = glyph.Width,
                            Height = glyph.Height
                        };
                        AddInGameFontCellReferenceToBuckets(buckets, reference, BucketSize);

                        if (targetFont && IsModernHangulSyllable(codepoint))
                        {
                            List<InGameFontCellReference> fontTargets;
                            if (!targets.TryGetValue(fontPath, out fontTargets))
                            {
                                fontTargets = new List<InGameFontCellReference>();
                                targets.Add(fontPath, fontTargets);
                            }

                            fontTargets.Add(reference);
                        }
                    }
                }

                string reportPath = Path.Combine(reportDir, "ingame-font-cell-reuse-survey.tsv");
                using (StreamWriter writer = CreateUtf8Writer(reportPath))
                {
                    writer.WriteLine("font\ttarget_glyphs\texclusive_cells\toverlapping_cells\tcurrent_transformed\ttransformed_fit_original\ttransformed_fit_with_padding\tmax_width_ratio\tmax_height_ratio\toverlap_examples");
                    foreach (KeyValuePair<string, List<InGameFontCellReference>> pair in targets)
                    {
                        string fontPath = pair.Key;
                        Dictionary<uint, FdtGlyphEntry> patchedGlyphs;
                        try
                        {
                            patchedGlyphs = ReadHangulGlyphEntries(_patchedFont.ReadFile(fontPath));
                        }
                        catch
                        {
                            patchedGlyphs = new Dictionary<uint, FdtGlyphEntry>();
                        }

                        int exclusive = 0;
                        int overlapping = 0;
                        int transformed = 0;
                        int fitOriginal = 0;
                        int fitWithPadding = 0;
                        double maxWidthRatio = 0d;
                        double maxHeightRatio = 0d;
                        List<string> examples = new List<string>();
                        for (int targetIndex = 0; targetIndex < pair.Value.Count; targetIndex++)
                        {
                            InGameFontCellReference target = pair.Value[targetIndex];
                            InGameFontCellReference owner;
                            if (TryFindOverlappingInGameFontCellReference(buckets, target, BucketSize, out owner))
                            {
                                overlapping++;
                                if (examples.Count < 12)
                                {
                                    examples.Add(
                                        "U+" + target.Codepoint.ToString("X4") + "->" +
                                        Path.GetFileName(owner.FontPath) + "/U+" + owner.Codepoint.ToString("X4"));
                                }
                            }
                            else
                            {
                                exclusive++;
                            }

                            FdtGlyphEntry patched;
                            if (!patchedGlyphs.TryGetValue(target.Codepoint, out patched) ||
                                (patched.ImageIndex == target.ImageIndex &&
                                 patched.X == target.X &&
                                 patched.Y == target.Y &&
                                 patched.Width == target.Width &&
                                 patched.Height == target.Height))
                            {
                                continue;
                            }

                            transformed++;
                            if (patched.Width <= target.Width && patched.Height <= target.Height)
                            {
                                fitOriginal++;
                            }

                            if (patched.Width + 2 <= target.Width && patched.Height + 2 <= target.Height)
                            {
                                fitWithPadding++;
                            }

                            maxWidthRatio = Math.Max(maxWidthRatio, SafeRatio(patched.Width, target.Width));
                            maxHeightRatio = Math.Max(maxHeightRatio, SafeRatio(patched.Height, target.Height));
                        }

                        WriteTsvRow(
                            writer,
                            fontPath,
                            pair.Value.Count.ToString(),
                            exclusive.ToString(),
                            overlapping.ToString(),
                            transformed.ToString(),
                            fitOriginal.ToString(),
                            fitWithPadding.ToString(),
                            FormatInGameFontSurveyDouble(maxWidthRatio),
                            FormatInGameFontSurveyDouble(maxHeightRatio),
                            string.Join(",", examples.ToArray()));
                    }
                }
            }

            private static void AddInGameFontCellReferenceToBuckets(
                Dictionary<string, List<InGameFontCellReference>> buckets,
                InGameFontCellReference reference,
                int bucketSize)
            {
                int firstX = reference.X / bucketSize;
                int lastX = (reference.X + reference.Width - 1) / bucketSize;
                int firstY = reference.Y / bucketSize;
                int lastY = (reference.Y + reference.Height - 1) / bucketSize;
                for (int bucketY = firstY; bucketY <= lastY; bucketY++)
                {
                    for (int bucketX = firstX; bucketX <= lastX; bucketX++)
                    {
                        string key = CreateInGameFontCellBucketKey(reference, bucketX, bucketY);
                        List<InGameFontCellReference> values;
                        if (!buckets.TryGetValue(key, out values))
                        {
                            values = new List<InGameFontCellReference>();
                            buckets.Add(key, values);
                        }

                        values.Add(reference);
                    }
                }
            }

            private static bool TryFindOverlappingInGameFontCellReference(
                Dictionary<string, List<InGameFontCellReference>> buckets,
                InGameFontCellReference target,
                int bucketSize,
                out InGameFontCellReference owner)
            {
                owner = null;
                HashSet<int> visited = new HashSet<int>();
                int firstX = target.X / bucketSize;
                int lastX = (target.X + target.Width - 1) / bucketSize;
                int firstY = target.Y / bucketSize;
                int lastY = (target.Y + target.Height - 1) / bucketSize;
                for (int bucketY = firstY; bucketY <= lastY; bucketY++)
                {
                    for (int bucketX = firstX; bucketX <= lastX; bucketX++)
                    {
                        List<InGameFontCellReference> values;
                        if (!buckets.TryGetValue(CreateInGameFontCellBucketKey(target, bucketX, bucketY), out values))
                        {
                            continue;
                        }

                        for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
                        {
                            InGameFontCellReference candidate = values[valueIndex];
                            if (candidate.Id == target.Id || !visited.Add(candidate.Id))
                            {
                                continue;
                            }

                            if (RectanglesOverlap(
                                target.X,
                                target.Y,
                                target.Width,
                                target.Height,
                                candidate.X,
                                candidate.Y,
                                candidate.Width,
                                candidate.Height))
                            {
                                owner = candidate;
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            private static string CreateInGameFontCellBucketKey(
                InGameFontCellReference reference,
                int bucketX,
                int bucketY)
            {
                return reference.TexturePath + "|" + reference.Channel.ToString() + "|" +
                       bucketX.ToString() + "|" + bucketY.ToString();
            }

            private bool TrySurveyInGameFontScale(
                string fontPath,
                out InGameFontScaleSurveyRow row,
                out string error)
            {
                row = null;
                error = null;
                if (!_ttmpFont.ContainsPath(fontPath))
                {
                    error = "TTMP source font is missing";
                    return false;
                }

                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(fontPath);
                    targetFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }

                Dictionary<uint, FdtGlyphEntry> sourceGlyphs = ReadHangulGlyphEntries(sourceFdt);
                Dictionary<uint, FdtGlyphEntry> targetGlyphs = ReadHangulGlyphEntries(targetFdt);
                FdtFontMetrics sourceFontMetrics;
                FdtFontMetrics targetFontMetrics;
                if (!TryReadFdtFontMetrics(sourceFdt, out sourceFontMetrics) ||
                    !TryReadFdtFontMetrics(targetFdt, out targetFontMetrics))
                {
                    error = "invalid FDT font metrics header";
                    return false;
                }

                List<uint> compared = new List<uint>();
                List<uint> metricChanged = new List<uint>();
                List<uint> metricUnchanged = new List<uint>();
                int missingTarget = 0;
                int cellChanged = 0;
                foreach (KeyValuePair<uint, FdtGlyphEntry> pair in sourceGlyphs)
                {
                    uint codepoint = pair.Key;
                    FdtGlyphEntry sourceGlyph = pair.Value;
                    if (!IsModernHangulSyllable(codepoint) || sourceGlyph.Width == 0 || sourceGlyph.Height == 0)
                    {
                        continue;
                    }

                    FdtGlyphEntry targetGlyph;
                    if (!targetGlyphs.TryGetValue(codepoint, out targetGlyph) ||
                        targetGlyph.Width == 0 ||
                        targetGlyph.Height == 0)
                    {
                        missingTarget++;
                        continue;
                    }

                    compared.Add(codepoint);
                    if (FontScaleSurveyCellChanged(sourceGlyph, targetGlyph))
                    {
                        cellChanged++;
                    }

                    if (FontScaleSurveyMetricsChanged(sourceGlyph, targetGlyph))
                    {
                        metricChanged.Add(codepoint);
                    }
                    else
                    {
                        metricUnchanged.Add(codepoint);
                    }
                }

                if (compared.Count == 0)
                {
                    error = "no modern Hangul syllables to compare";
                    return false;
                }

                compared.Sort();
                metricChanged.Sort();
                metricUnchanged.Sort();

                PhraseVisualBounds sourceDigits;
                PhraseVisualBounds targetDigits;
                string measurementError;
                if (!TryMeasurePhraseVisualBounds(_ttmpFont, fontPath, ActionDetailNumericBaselinePhrase, out sourceDigits, out measurementError) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailNumericBaselinePhrase, false, out targetDigits, out measurementError))
                {
                    error = "numeric baseline failed: " + measurementError;
                    return false;
                }

                row = new InGameFontScaleSurveyRow(fontPath);
                row.ComparedHangul = compared.Count;
                row.MissingTarget = missingTarget;
                row.CellChanged = cellChanged;
                row.MetricChanged = metricChanged.Count;
                row.MetricChangedCodepoints = FormatCodepoints(new HashSet<uint>(metricChanged));
                row.SourceFontSize = sourceFontMetrics.Size;
                row.SourceLineHeight = sourceFontMetrics.LineHeight;
                row.SourceAscent = sourceFontMetrics.Ascent;
                row.TargetFontSize = targetFontMetrics.Size;
                row.TargetLineHeight = targetFontMetrics.LineHeight;
                row.TargetAscent = targetFontMetrics.Ascent;
                row.SourceDigitHeight = sourceDigits.MeanDigitHeight;
                row.TargetDigitHeight = targetDigits.MeanDigitHeight;
                row.SourceHangulHeight = MeasureTtmpFontHangulSample(fontPath, compared, out row.SourceSample);
                row.TargetHangulHeight = MeasurePatchedFontHangulSample(fontPath, compared, out row.TargetSample);
                row.ChangedHangulHeight = MeasurePatchedFontHangulSample(fontPath, metricChanged, out row.ChangedSample);
                row.UnchangedHangulHeight = MeasurePatchedFontHangulSample(fontPath, metricUnchanged, out row.UnchangedSample);
                row.SourceHangulDigitRatio = SafeRatio(row.SourceHangulHeight, row.SourceDigitHeight);
                row.TargetHangulDigitRatio = SafeRatio(row.TargetHangulHeight, row.TargetDigitHeight);
                row.TargetHangulLineHeightRatio = SafeRatio(row.TargetHangulHeight, row.TargetLineHeight);
                row.TargetHangulFontSizeRatio = SafeRatio(row.TargetHangulHeight, row.TargetFontSize);
                row.TargetSourceHeightRatio = SafeRatio(row.TargetHangulHeight, row.SourceHangulHeight);
                row.ChangedHangulDigitRatio = SafeRatio(row.ChangedHangulHeight, row.TargetDigitHeight);
                row.UnchangedHangulDigitRatio = SafeRatio(row.UnchangedHangulHeight, row.TargetDigitHeight);
                row.ChangedUnchangedSpread = row.ChangedHangulHeight > 0d && row.UnchangedHangulHeight > 0d
                    ? Math.Max(row.ChangedHangulHeight, row.UnchangedHangulHeight) /
                      Math.Min(row.ChangedHangulHeight, row.UnchangedHangulHeight)
                    : 0d;
                row.IsPartialScaleRisk = row.MetricChanged > 0 &&
                    row.MetricChanged < row.ComparedHangul &&
                    row.ChangedUnchangedSpread >= InGameFontPartialScaleRiskSpread;
                row.IsOversizeCandidate = row.TargetHangulFontSizeRatio > InGameFontOversizeCandidateRatio;
                row.Status = DescribeInGameFontScaleSurveyStatus(row);
                return true;
            }

            private static bool TryReadFdtFontMetrics(byte[] fdt, out FdtFontMetrics metrics)
            {
                metrics = new FdtFontMetrics();
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return false;
                }

                byte[] sizeBytes = new byte[4];
                Buffer.BlockCopy(fdt, fontTableOffset + 0x14, sizeBytes, 0, sizeBytes.Length);
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(sizeBytes);
                }

                metrics.Size = BitConverter.ToSingle(sizeBytes, 0);
                metrics.LineHeight = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x18);
                metrics.Ascent = Endian.ReadUInt32LE(fdt, fontTableOffset + 0x1C);
                return true;
            }

            private double MeasurePatchedFontHangulSample(string fontPath, List<uint> codepoints, out int measured)
            {
                measured = 0;
                if (codepoints == null || codepoints.Count == 0)
                {
                    return 0d;
                }

                int samples = Math.Min(InGameFontScaleSampleLimit, codepoints.Count);
                double heightTotal = 0d;
                for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
                {
                    uint codepoint = codepoints[GetInGameFontScaleSampleIndex(codepoints.Count, samples, sampleIndex)];
                    try
                    {
                        int height = GetVisibleGlyphHeight(RenderGlyph(_patchedFont, fontPath, codepoint));
                        if (height > 0)
                        {
                            heightTotal += height;
                            measured++;
                        }
                    }
                    catch
                    {
                    }
                }

                return measured > 0 ? heightTotal / measured : 0d;
            }

            private double MeasureTtmpFontHangulSample(string fontPath, List<uint> codepoints, out int measured)
            {
                measured = 0;
                if (codepoints == null || codepoints.Count == 0)
                {
                    return 0d;
                }

                int samples = Math.Min(InGameFontScaleSampleLimit, codepoints.Count);
                double heightTotal = 0d;
                for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
                {
                    uint codepoint = codepoints[GetInGameFontScaleSampleIndex(codepoints.Count, samples, sampleIndex)];
                    try
                    {
                        int height = GetVisibleGlyphHeight(RenderGlyph(_ttmpFont, fontPath, codepoint));
                        if (height > 0)
                        {
                            heightTotal += height;
                            measured++;
                        }
                    }
                    catch
                    {
                    }
                }

                return measured > 0 ? heightTotal / measured : 0d;
            }

            private static int GetInGameFontScaleSampleIndex(int count, int samples, int sampleIndex)
            {
                if (samples <= 1)
                {
                    return count / 2;
                }

                return (int)Math.Round((double)sampleIndex * (count - 1) / (samples - 1));
            }

            private static bool FontScaleSurveyCellChanged(FdtGlyphEntry source, FdtGlyphEntry target)
            {
                return source.ImageIndex != target.ImageIndex ||
                       source.X != target.X ||
                       source.Y != target.Y;
            }

            private static bool FontScaleSurveyMetricsChanged(FdtGlyphEntry source, FdtGlyphEntry target)
            {
                return source.Width != target.Width ||
                       source.Height != target.Height ||
                       source.OffsetX != target.OffsetX ||
                       source.OffsetY != target.OffsetY;
            }

            private static bool IsModernHangulSyllable(uint codepoint)
            {
                return codepoint >= 0xAC00u && codepoint <= 0xD7A3u;
            }

            private static string DescribeInGameFontScaleSurveyStatus(InGameFontScaleSurveyRow row)
            {
                List<string> statuses = new List<string>();
                if (row.IsPartialScaleRisk)
                {
                    statuses.Add("partial-scale-risk");
                }
                else if (row.MetricChanged > 0 && row.MetricChanged < row.ComparedHangul)
                {
                    statuses.Add("partial-metric-change");
                }
                else if (row.MetricChanged == row.ComparedHangul)
                {
                    statuses.Add("full-metric-route");
                }
                else
                {
                    statuses.Add("source-metrics");
                }

                if (row.IsOversizeCandidate)
                {
                    statuses.Add("oversize-vs-font-size");
                }

                return string.Join(",", statuses.ToArray());
            }

            private static void WriteInGameFontScaleSurveyRow(StreamWriter writer, InGameFontScaleSurveyRow row)
            {
                WriteTsvRow(
                    writer,
                    row.FontPath,
                    row.ComparedHangul.ToString(),
                    row.MissingTarget.ToString(),
                    row.CellChanged.ToString(),
                    row.MetricChanged.ToString(),
                    FormatInGameFontSurveyDouble(SafeRatio(row.MetricChanged, row.ComparedHangul)),
                    row.MetricChangedCodepoints,
                    FormatInGameFontSurveyDouble(row.SourceFontSize),
                    row.SourceLineHeight.ToString(),
                    row.SourceAscent.ToString(),
                    FormatInGameFontSurveyDouble(row.TargetFontSize),
                    row.TargetLineHeight.ToString(),
                    row.TargetAscent.ToString(),
                    FormatInGameFontSurveyDouble(row.TargetHangulLineHeightRatio),
                    FormatInGameFontSurveyDouble(row.TargetHangulFontSizeRatio),
                    FormatInGameFontSurveyDouble(row.SourceDigitHeight),
                    FormatInGameFontSurveyDouble(row.TargetDigitHeight),
                    row.SourceSample.ToString(),
                    FormatInGameFontSurveyDouble(row.SourceHangulHeight),
                    FormatInGameFontSurveyDouble(row.SourceHangulDigitRatio),
                    row.TargetSample.ToString(),
                    FormatInGameFontSurveyDouble(row.TargetHangulHeight),
                    FormatInGameFontSurveyDouble(row.TargetHangulDigitRatio),
                    FormatInGameFontSurveyDouble(row.TargetSourceHeightRatio),
                    row.ChangedSample.ToString(),
                    FormatInGameFontSurveyDouble(row.ChangedHangulHeight),
                    FormatInGameFontSurveyDouble(row.ChangedHangulDigitRatio),
                    row.UnchangedSample.ToString(),
                    FormatInGameFontSurveyDouble(row.UnchangedHangulHeight),
                    FormatInGameFontSurveyDouble(row.UnchangedHangulDigitRatio),
                    FormatInGameFontSurveyDouble(row.ChangedUnchangedSpread),
                    row.Status,
                    string.Empty);
            }

            private static string FormatInGameFontSurveyDouble(double value)
            {
                return value.ToString("0.000", CultureInfo.InvariantCulture);
            }

            private sealed class InGameFontScaleSurveyRow
            {
                public readonly string FontPath;
                public int ComparedHangul;
                public int MissingTarget;
                public int CellChanged;
                public int MetricChanged;
                public string MetricChangedCodepoints;
                public double SourceFontSize;
                public uint SourceLineHeight;
                public uint SourceAscent;
                public double TargetFontSize;
                public uint TargetLineHeight;
                public uint TargetAscent;
                public double TargetHangulLineHeightRatio;
                public double TargetHangulFontSizeRatio;
                public double SourceDigitHeight;
                public double TargetDigitHeight;
                public int SourceSample;
                public double SourceHangulHeight;
                public double SourceHangulDigitRatio;
                public int TargetSample;
                public double TargetHangulHeight;
                public double TargetHangulDigitRatio;
                public double TargetSourceHeightRatio;
                public int ChangedSample;
                public double ChangedHangulHeight;
                public double ChangedHangulDigitRatio;
                public int UnchangedSample;
                public double UnchangedHangulHeight;
                public double UnchangedHangulDigitRatio;
                public double ChangedUnchangedSpread;
                public bool IsPartialScaleRisk;
                public bool IsOversizeCandidate;
                public string Status;

                public InGameFontScaleSurveyRow(string fontPath)
                {
                    FontPath = fontPath;
                }
            }

            private sealed class InGameFontCellReference
            {
                public int Id;
                public string FontPath;
                public uint Codepoint;
                public string TexturePath;
                public int ImageIndex;
                public int Channel;
                public int X;
                public int Y;
                public int Width;
                public int Height;
            }

            private struct FdtFontMetrics
            {
                public double Size;
                public uint LineHeight;
                public uint Ascent;
            }
        }
    }
}
