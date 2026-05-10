using System;
using System.Collections.Generic;
using System.Globalization;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const string ActionDetailUldPath = "ui/uld/ActionDetail.uld";

            private static readonly string[] ActionDetailScaleProbeFonts = new string[]
            {
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/TrumpGothic_23.fdt",
                "common/font/TrumpGothic_34.fdt",
                "common/font/TrumpGothic_68.fdt"
            };

            private static readonly FontPair[] ActionDetailScalePairs = new FontPair[]
            {
                new FontPair("common/font/AXIS_12.fdt", "common/font/AXIS_18.fdt"),
                new FontPair("common/font/AXIS_18.fdt", "common/font/AXIS_36.fdt"),
                new FontPair("common/font/TrumpGothic_23.fdt", "common/font/TrumpGothic_34.fdt"),
                new FontPair("common/font/TrumpGothic_34.fdt", "common/font/TrumpGothic_68.fdt")
            };

            private static readonly string ActionDetailInstantPhrase = ActionDetailHighScaleHangulGlyphs.InstantCastPhrase;
            private static readonly string ActionDetailSecondUnitPhrase = ActionDetailHighScaleHangulGlyphs.SecondUnitPhrase;
            private static readonly string ActionDetailLongTimerPhrase = "120.00\uCD08";
            private static readonly string ActionDetailShortTimerPhrase = "1.50\uCD08";
            private const string ActionDetailNumericBaselinePhrase = "120.00";

            private void VerifyActionDetailScaleLayouts()
            {
                Console.WriteLine("[FDT] Action detail scale layout");

                HashSet<string> fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> routedFonts;
                if (TryCollectOptionalPreservedUldFontRoutes(ActionDetailUldPath, "action-detail", false, out routedFonts))
                {
                    foreach (string routedFont in routedFonts)
                    {
                        fonts.Add(routedFont);
                    }
                }
                else
                {
                    Fail("{0} was not available for action-detail font route verification", ActionDetailUldPath);
                }

                AddValues(fonts, ActionDetailScaleProbeFonts);

                int measured = 0;
                foreach (string fontPath in fonts)
                {
                    measured += VerifyActionDetailFont(fontPath);
                }

                for (int i = 0; i < ActionDetailScalePairs.Length; i++)
                {
                    VerifyActionDetailScalePair(ActionDetailScalePairs[i]);
                }

                if (measured == 0)
                {
                    Fail("No action-detail font route could render the reported phrases");
                }
            }

            private int VerifyActionDetailFont(string fontPath)
            {
                PhraseVisualBounds numeric;
                PhraseVisualBounds instant;
                PhraseVisualBounds unit;
                PhraseVisualBounds longTimer;
                PhraseVisualBounds shortTimer;
                string error;

                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailNumericBaselinePhrase, false, out numeric, out error))
                {
                    Warn("{0} action-detail numeric baseline skipped: {1}", fontPath, error);
                    return 0;
                }

                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailInstantPhrase, true, out instant, out error))
                {
                    Warn("{0} action-detail instant phrase skipped: {1}", fontPath, error);
                    return 0;
                }

                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailSecondUnitPhrase, true, out unit, out error))
                {
                    Warn("{0} action-detail second unit skipped: {1}", fontPath, error);
                    return 0;
                }

                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailLongTimerPhrase, true, out longTimer, out error))
                {
                    Warn("{0} action-detail long timer skipped: {1}", fontPath, error);
                    return 0;
                }

                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailShortTimerPhrase, true, out shortTimer, out error))
                {
                    Warn("{0} action-detail short timer skipped: {1}", fontPath, error);
                    return 0;
                }

                VerifyActionDetailValueHeight(fontPath, ActionDetailInstantPhrase, instant, numeric);
                VerifyActionDetailValueHeight(fontPath, ActionDetailSecondUnitPhrase, unit, numeric);
                VerifyActionDetailTimerMix(fontPath, ActionDetailLongTimerPhrase, longTimer);
                VerifyActionDetailTimerMix(fontPath, ActionDetailShortTimerPhrase, shortTimer);
                return 1;
            }

            private void VerifyActionDetailValueHeight(string fontPath, string phrase, PhraseVisualBounds target, PhraseVisualBounds numeric)
            {
                double ratio = SafeRatio(target.MeanHangulHeight, numeric.MeanDigitHeight);
                if (ratio < 0.72d || ratio > 1.28d)
                {
                    Fail(
                        "{0} action-detail [{1}] Hangul/digit visual height ratio {2} outside 0.72..1.28: hangul={3}, digit={4}, bounds={5}",
                        fontPath,
                        Escape(phrase),
                        FormatRatio(ratio),
                        FormatDouble(target.MeanHangulHeight),
                        FormatDouble(numeric.MeanDigitHeight),
                        FormatPhraseBounds(target));
                    return;
                }

                Pass(
                    "{0} action-detail [{1}] Hangul/digit visual height ratio={2}, hangul={3}, digit={4}, bounds={5}",
                    fontPath,
                    Escape(phrase),
                    FormatRatio(ratio),
                    FormatDouble(target.MeanHangulHeight),
                    FormatDouble(numeric.MeanDigitHeight),
                    FormatPhraseBounds(target));
            }

            private void VerifyActionDetailTimerMix(string fontPath, string phrase, PhraseVisualBounds timer)
            {
                double ratio = SafeRatio(timer.MeanHangulHeight, timer.MeanDigitHeight);
                if (ratio < 0.72d || ratio > 1.28d)
                {
                    Fail(
                        "{0} action-detail timer [{1}] Hangul/digit visual height ratio {2} outside 0.72..1.28: hangul={3}, digit={4}, bounds={5}",
                        fontPath,
                        Escape(phrase),
                        FormatRatio(ratio),
                        FormatDouble(timer.MeanHangulHeight),
                        FormatDouble(timer.MeanDigitHeight),
                        FormatPhraseBounds(timer));
                    return;
                }

                Pass(
                    "{0} action-detail timer [{1}] Hangul/digit visual height ratio={2}, hangul={3}, digit={4}, bounds={5}",
                    fontPath,
                    Escape(phrase),
                    FormatRatio(ratio),
                    FormatDouble(timer.MeanHangulHeight),
                    FormatDouble(timer.MeanDigitHeight),
                    FormatPhraseBounds(timer));
            }

            private void VerifyActionDetailScalePair(FontPair pair)
            {
                PhraseVisualBounds lowNumeric;
                PhraseVisualBounds highNumeric;
                PhraseVisualBounds lowInstant;
                PhraseVisualBounds highInstant;
                PhraseVisualBounds lowUnit;
                PhraseVisualBounds highUnit;
                string error;

                if (!TryMeasurePhraseVisualBounds(_patchedFont, pair.SourceFontPath, ActionDetailNumericBaselinePhrase, false, out lowNumeric, out error) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, pair.TargetFontPath, ActionDetailNumericBaselinePhrase, false, out highNumeric, out error) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, pair.SourceFontPath, ActionDetailInstantPhrase, true, out lowInstant, out error) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, pair.TargetFontPath, ActionDetailInstantPhrase, true, out highInstant, out error) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, pair.SourceFontPath, ActionDetailSecondUnitPhrase, true, out lowUnit, out error) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, pair.TargetFontPath, ActionDetailSecondUnitPhrase, true, out highUnit, out error))
                {
                    Warn(
                        "{0}->{1} action-detail scale pair skipped: {2}",
                        pair.SourceFontPath,
                        pair.TargetFontPath,
                        error);
                    return;
                }

                double numericScale = SafeRatio(highNumeric.MeanDigitHeight, lowNumeric.MeanDigitHeight);
                VerifyActionDetailScaleRatio(pair, ActionDetailInstantPhrase, numericScale, lowInstant.MeanHangulHeight, highInstant.MeanHangulHeight);
                VerifyActionDetailScaleRatio(pair, ActionDetailSecondUnitPhrase, numericScale, lowUnit.MeanHangulHeight, highUnit.MeanHangulHeight);
            }

            private void VerifyActionDetailScaleRatio(FontPair pair, string phrase, double numericScale, double lowHangulHeight, double highHangulHeight)
            {
                double hangulScale = SafeRatio(highHangulHeight, lowHangulHeight);
                double relative = SafeRatio(hangulScale, numericScale);
                if (relative < 0.80d || relative > 1.20d)
                {
                    Fail(
                        "{0}->{1} action-detail [{2}] scale ratio {3} differs from numeric scale {4}: hangulScale={5}, lowHangul={6}, highHangul={7}",
                        pair.SourceFontPath,
                        pair.TargetFontPath,
                        Escape(phrase),
                        FormatRatio(relative),
                        FormatRatio(numericScale),
                        FormatRatio(hangulScale),
                        FormatDouble(lowHangulHeight),
                        FormatDouble(highHangulHeight));
                    return;
                }

                Pass(
                    "{0}->{1} action-detail [{2}] scale follows numeric: relative={3}, numeric={4}, hangul={5}",
                    pair.SourceFontPath,
                    pair.TargetFontPath,
                    Escape(phrase),
                    FormatRatio(relative),
                    FormatRatio(numericScale),
                    FormatRatio(hangulScale));
            }

            private bool TryMeasurePhraseVisualBounds(
                CompositeArchive archive,
                string fontPath,
                string phrase,
                bool validateGlyphShape,
                out PhraseVisualBounds bounds,
                out string error)
            {
                bounds = new PhraseVisualBounds();
                error = null;

                try
                {
                    byte[] fdt = archive.ReadFile(fontPath);
                    Dictionary<string, int> kerningAdjustments = ReadKerningAdjustments(fdt);
                    int cursor = 0;
                    bool hasPreviousCodepoint = false;
                    uint previousCodepoint = 0;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPreviousCodepoint)
                        {
                            cursor += GetKerningAdjustment(kerningAdjustments, previousCodepoint, codepoint);
                        }

                        if (IsPhraseLayoutSpace(codepoint))
                        {
                            cursor += PhraseLayoutSpaceAdvance;
                            previousCodepoint = codepoint;
                            hasPreviousCodepoint = true;
                            continue;
                        }

                        FdtGlyphEntry glyph;
                        if (!TryFindGlyph(fdt, codepoint, out glyph))
                        {
                            error = "missing U+" + codepoint.ToString("X4");
                            return false;
                        }

                        GlyphCanvas canvas = RenderGlyph(archive, fontPath, codepoint);
                        if (validateGlyphShape && !TryValidatePhraseGlyphShape(fontPath, codepoint, canvas, out error))
                        {
                            return false;
                        }

                        GlyphStats stats = AnalyzeGlyph(canvas);
                        bounds.AddGlyph(codepoint, cursor, glyph, stats, canvas.VisiblePixels);
                        cursor += GetGlyphAdvance(glyph);
                        previousCodepoint = codepoint;
                        hasPreviousCodepoint = true;
                    }

                    bounds.Advance = cursor;
                    return bounds.Glyphs > 0;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static string FormatPhraseBounds(PhraseVisualBounds bounds)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "advance={0}, bbox={1}x{2}, fdtH={3}/{4}, offY={5}/{6}, visible={7}, glyphs={8}",
                    bounds.Advance,
                    bounds.Width,
                    bounds.Height,
                    FormatDouble(bounds.MeanHangulFdtHeight),
                    FormatDouble(bounds.MeanDigitFdtHeight),
                    FormatDouble(bounds.MeanHangulOffsetY),
                    FormatDouble(bounds.MeanDigitOffsetY),
                    bounds.VisiblePixels,
                    bounds.Glyphs);
            }

            private static double SafeRatio(double numerator, double denominator)
            {
                if (denominator <= 0d)
                {
                    return 0d;
                }

                return numerator / denominator;
            }

            private static string FormatRatio(double value)
            {
                return value.ToString("0.000", CultureInfo.InvariantCulture);
            }

            private static string FormatDouble(double value)
            {
                return value.ToString("0.0", CultureInfo.InvariantCulture);
            }

            private HashSet<uint> CollectActionDetailHighScaleHangulCodepointSet()
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                AddDynamicHangulCodepoints(codepoints, ActionDetailHighScaleHangulGlyphs.FallbackPhrases);
                AddPatchedAddonRangeHangulCodepoints(
                    codepoints,
                    ActionDetailHighScaleHangulGlyphs.AddonRowRanges,
                    "action-detail high-scale glyph verification");
                return codepoints;
            }

            private bool IsActionDetailHighScaleRepairedCodepoint(HashSet<uint> repairedCodepoints, string fontPath, uint codepoint)
            {
                return repairedCodepoints != null &&
                       repairedCodepoints.Contains(codepoint) &&
                       ActionDetailHighScaleHangulGlyphs.IsTargetFontPath(fontPath);
            }

            private bool PhraseUsesActionDetailHighScaleRepairedHangul(
                string fontPath,
                string phrase,
                HashSet<uint> repairedCodepoints)
            {
                if (!ActionDetailHighScaleHangulGlyphs.IsTargetFontPath(fontPath) ||
                    repairedCodepoints == null)
                {
                    return false;
                }

                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (!IsHangulCodepoint(codepoint))
                    {
                        continue;
                    }

                    if (repairedCodepoints.Contains(codepoint))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private struct PhraseVisualBounds
        {
            private int _minX;
            private int _minY;
            private int _maxX;
            private int _maxY;
            private int _hangulHeightTotal;
            private int _hangulCount;
            private int _digitHeightTotal;
            private int _digitCount;
            private int _hangulFdtHeightTotal;
            private int _digitFdtHeightTotal;
            private int _hangulOffsetYTotal;
            private int _digitOffsetYTotal;

            public int Advance;
            public int Glyphs;
            public int VisiblePixels;

            public int Width
            {
                get { return Glyphs == 0 || _maxX < _minX ? 0 : _maxX - _minX + 1; }
            }

            public int Height
            {
                get { return Glyphs == 0 || _maxY < _minY ? 0 : _maxY - _minY + 1; }
            }

            public double MeanHangulHeight
            {
                get { return _hangulCount == 0 ? 0d : (double)_hangulHeightTotal / _hangulCount; }
            }

            public double MeanDigitHeight
            {
                get { return _digitCount == 0 ? 0d : (double)_digitHeightTotal / _digitCount; }
            }

            public double MeanHangulFdtHeight
            {
                get { return _hangulCount == 0 ? 0d : (double)_hangulFdtHeightTotal / _hangulCount; }
            }

            public double MeanDigitFdtHeight
            {
                get { return _digitCount == 0 ? 0d : (double)_digitFdtHeightTotal / _digitCount; }
            }

            public double MeanHangulOffsetY
            {
                get { return _hangulCount == 0 ? 0d : (double)_hangulOffsetYTotal / _hangulCount; }
            }

            public double MeanDigitOffsetY
            {
                get { return _digitCount == 0 ? 0d : (double)_digitOffsetYTotal / _digitCount; }
            }

            public void AddGlyph(uint codepoint, int cursor, FdtGlyphEntry glyph, GlyphStats stats, int visiblePixels)
            {
                if (Glyphs == 0)
                {
                    _minX = int.MaxValue;
                    _minY = int.MaxValue;
                    _maxX = int.MinValue;
                    _maxY = int.MinValue;
                }

                if (stats.MinX <= stats.MaxX && stats.MinY <= stats.MaxY)
                {
                    int minX = cursor + stats.MinX - 32;
                    int maxX = cursor + stats.MaxX - 32;
                    int minY = stats.MinY - 32;
                    int maxY = stats.MaxY - 32;
                    if (minX < _minX) _minX = minX;
                    if (minY < _minY) _minY = minY;
                    if (maxX > _maxX) _maxX = maxX;
                    if (maxY > _maxY) _maxY = maxY;

                    int height = maxY - minY + 1;
                    if (IsHangulCodepoint(codepoint))
                    {
                        _hangulHeightTotal += height;
                        _hangulFdtHeightTotal += glyph.Height;
                        _hangulOffsetYTotal += glyph.OffsetY;
                        _hangulCount++;
                    }
                    else if (codepoint >= '0' && codepoint <= '9')
                    {
                        _digitHeightTotal += height;
                        _digitFdtHeightTotal += glyph.Height;
                        _digitOffsetYTotal += glyph.OffsetY;
                        _digitCount++;
                    }
                }

                VisiblePixels += visiblePixels;
                Glyphs++;
            }
        }
    }
}
