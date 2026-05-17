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

            private static readonly string[] LargeUiSourcePreservationFonts = new string[]
            {
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/KrnAXIS_120.fdt",
                "common/font/KrnAXIS_140.fdt",
                "common/font/KrnAXIS_180.fdt",
                "common/font/KrnAXIS_360.fdt",
                "common/font/Jupiter_23.fdt",
                "common/font/Jupiter_46.fdt",
                "common/font/MiedingerMid_18.fdt",
                "common/font/MiedingerMid_36.fdt",
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

            private static readonly string ActionDetailLongTimerPhrase = "120.00\uCD08";
            private static readonly string ActionDetailShortTimerPhrase = "1.50\uCD08";
            private const string ActionDetailNumericBaselinePhrase = "120.00";
            private const double VisualScalePhraseMinRatio = 0.94d;
            private const double VisualScalePhraseMaxRatio = 1.19d;
            private const double VisualScalePairMinRelativeRatio = 0.88d;
            private const double VisualScalePairMaxRelativeRatio = 1.12d;
            private const double HighScaleGlyphMinHeightRatio = 0.80d;
            private const double HighScaleGlyphMaxHeightRatio = 1.20d;
            private static readonly string[] LargeUiScalePhrases = ActionDetailHighScaleHangulGlyphs.FallbackPhrases;
            private static readonly string[] LargeUiSourcePreservationPhrases = new string[]
            {
                ActionDetailHighScaleHangulGlyphs.InstantCastPhrase,
                ActionDetailHighScaleHangulGlyphs.SecondUnitPhrase,
                ActionDetailHighScaleHangulGlyphs.DutyFinderPhrase,
                ActionDetailHighScaleHangulGlyphs.QuestPhrase,
                ActionDetailHighScaleHangulGlyphs.SystemConfigurationPhrase,
                ActionDetailHighScaleHangulGlyphs.CharacterPhrase,
                "\uC778\uBCA4\uD1A0\uB9AC",
                "\uB85C\uC2A4\uAC00\uB974",
                "\uC5EC",
                "\uB808\uBCA8",
                "\uC554\uD751\uAE30\uC0AC",
                ActionDetailHighScaleHangulGlyphs.PvpProfilePhrase,
                ActionDetailHighScaleHangulGlyphs.BattleRecordPhrase,
                ActionDetailHighScaleHangulGlyphs.CrystallineConflictPhrase,
                ActionDetailHighScaleHangulGlyphs.FrontlinePhrase,
                ActionDetailHighScaleHangulGlyphs.RivalWingsPhrase,
                ActionDetailHighScaleHangulGlyphs.PvpActionsPhrase,
                ActionDetailHighScaleHangulGlyphs.TacticalCommunicationPhrase
            };

            private void VerifyActionDetailScaleLayouts()
            {
                Console.WriteLine("[FDT] Large UI label scale layout");

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
                    Warn("{0} was not available; action-detail verification is using static font probes", ActionDetailUldPath);
                }

                AddValues(fonts, ActionDetailScaleProbeFonts);

                int measured = 0;
                foreach (string fontPath in fonts)
                {
                    measured += VerifyActionDetailFont(fontPath);
                }

                VerifyLargeUiLabelSourcePreservation();

                for (int i = 0; i < ActionDetailScalePairs.Length; i++)
                {
                    VerifyActionDetailScalePair(ActionDetailScalePairs[i]);
                }

                HashSet<uint> highScaleCodepoints = CollectActionDetailHighScaleHangulCodepointSet();
                if (ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(ActionDetailHighScaleHangulGlyphs.TargetFontPath))
                {
                    VerifyActionDetailHighScaleTargetGlyphCoverage(highScaleCodepoints);
                }
                else
                {
                    VerifyActionDetailHighScaleTargetGlyphSourcePreservation(highScaleCodepoints);
                }

                if (measured == 0)
                {
                    Fail("No large UI font route could render the reported phrases");
                }
            }

            private void VerifyLargeUiLabelSourcePreservation()
            {
                if (_ttmpFont == null)
                {
                    Fail("TTMP font package is required to verify large UI source preservation");
                    return;
                }

                int compared = 0;
                for (int fontIndex = 0; fontIndex < LargeUiSourcePreservationFonts.Length; fontIndex++)
                {
                    string fontPath = LargeUiSourcePreservationFonts[fontIndex];
                    if (ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(fontPath))
                    {
                        continue;
                    }

                    if (!_ttmpFont.ContainsPath(fontPath))
                    {
                        continue;
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
                        Fail("{0} large UI source-preservation read error: {1}", fontPath, ex.Message);
                        continue;
                    }

                    for (int phraseIndex = 0; phraseIndex < LargeUiSourcePreservationPhrases.Length; phraseIndex++)
                    {
                        string phrase = LargeUiSourcePreservationPhrases[phraseIndex];
                        if (IsKnownLargeUiSourcePreservationException(fontPath, phrase))
                        {
                            continue;
                        }

                        if (!PhraseGlyphsExist(sourceFdt, phrase))
                        {
                            continue;
                        }

                        if (!PhraseGlyphsExist(targetFdt, phrase))
                        {
                            Fail("{0} large UI phrase [{1}] exists in TTMP source but is missing patched glyphs", fontPath, Escape(phrase));
                            continue;
                        }

                        PhraseRenderSnapshot sourcePixels;
                        PhraseRenderSnapshot targetPixels;
                        PhraseLayoutResult sourceLayout;
                        PhraseLayoutResult targetLayout;
                        string error;
                        if (!TryRenderPhrasePixels(_ttmpFont, fontPath, phrase, out sourcePixels, out error) ||
                            !TryMeasurePhraseLayout(_ttmpFont, fontPath, phrase, out sourceLayout, out error))
                        {
                            Fail("{0} large UI phrase [{1}] TTMP source render/layout error: {2}", fontPath, Escape(phrase), error);
                            continue;
                        }

                        if (!TryRenderPhrasePixels(_patchedFont, fontPath, phrase, true, out targetPixels, out error) ||
                            !TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out targetLayout, out error))
                        {
                            Fail("{0} large UI phrase [{1}] patched render/layout error: {2}", fontPath, Escape(phrase), error);
                            continue;
                        }

                        if (sourcePixels.Width != targetPixels.Width ||
                            sourcePixels.Glyphs != targetPixels.Glyphs ||
                            sourceLayout.Width != targetLayout.Width ||
                            sourceLayout.OverlapPixels != targetLayout.OverlapPixels ||
                            !PhrasePixelsEqual(sourcePixels.Pixels, targetPixels.Pixels))
                        {
                            Fail(
                                "{0} large UI phrase [{1}] differs from TTMP source: layoutWidth={2}/{3}, renderWidth={4}/{5}, overlap={6}/{7}, pixels={8}/{9}",
                                fontPath,
                                Escape(phrase),
                                targetLayout.Width,
                                sourceLayout.Width,
                                targetPixels.Width,
                                sourcePixels.Width,
                                targetLayout.OverlapPixels,
                                sourceLayout.OverlapPixels,
                                targetPixels.Pixels.Count,
                                sourcePixels.Pixels.Count);
                            continue;
                        }

                        compared++;
                    }
                }

                if (compared == 0)
                {
                    Fail("No large UI source-preservation phrases were compared");
                }
                else
                {
                    Pass("large UI source-preservation phrases match TTMP source: comparisons={0}", compared);
                }
            }

            private int VerifyActionDetailFont(string fontPath)
            {
                PhraseVisualBounds numeric;
                PhraseVisualBounds longTimer;
                PhraseVisualBounds shortTimer;
                string error;

                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailNumericBaselinePhrase, false, out numeric, out error))
                {
                    Warn("{0} action-detail numeric baseline skipped: {1}", fontPath, error);
                    return 0;
                }

                int measuredPhrases = 0;
                for (int phraseIndex = 0; phraseIndex < LargeUiScalePhrases.Length; phraseIndex++)
                {
                    string phrase = LargeUiScalePhrases[phraseIndex];
                    PhraseVisualBounds phraseBounds;
                    if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, phrase, true, out phraseBounds, out error))
                    {
                        if (ActionDetailHighScaleHangulGlyphs.IsTargetFontPath(fontPath))
                        {
                            Fail("{0} large UI phrase [{1}] skipped: {2}", fontPath, Escape(phrase), error);
                        }
                        else
                        {
                            Warn("{0} large UI phrase [{1}] skipped: {2}", fontPath, Escape(phrase), error);
                        }

                        continue;
                    }

                    VerifyActionDetailValueHeight(fontPath, phrase, phraseBounds, numeric);
                    measuredPhrases++;
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

                VerifyActionDetailTimerMix(fontPath, ActionDetailLongTimerPhrase, longTimer);
                VerifyActionDetailTimerMix(fontPath, ActionDetailShortTimerPhrase, shortTimer);
                return measuredPhrases > 0 ? 1 : 0;
            }

            private static bool IsKnownLargeUiSourcePreservationException(string fontPath, string phrase)
            {
                if (!string.Equals(phrase, ActionDetailHighScaleHangulGlyphs.SystemConfigurationPhrase, StringComparison.Ordinal))
                {
                    return false;
                }

                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                return string.Equals(normalized, "common/font/AXIS_12.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_14.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/KrnAXIS_120.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/KrnAXIS_140.fdt", StringComparison.OrdinalIgnoreCase);
            }

            private void VerifyActionDetailValueHeight(string fontPath, string phrase, PhraseVisualBounds target, PhraseVisualBounds numeric)
            {
                double ratio = SafeRatio(target.MeanHangulHeight, numeric.MeanDigitHeight);
                bool strict = ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(fontPath);
                double minimum = strict ? VisualScalePhraseMinRatio : 0.72d;
                double maximum = strict ? VisualScalePhraseMaxRatio : 1.28d;
                if (ratio < minimum || ratio > maximum)
                {
                    Fail(
                        "{0} action-detail [{1}] Hangul/digit visual height ratio {2} outside {3}..{4}: hangul={5}, digit={6}, bounds={7}",
                        fontPath,
                        Escape(phrase),
                        FormatRatio(ratio),
                        FormatRatio(minimum),
                        FormatRatio(maximum),
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
                bool strict = ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(fontPath);
                double minimum = strict ? VisualScalePhraseMinRatio : 0.72d;
                double maximum = strict ? VisualScalePhraseMaxRatio : 1.28d;
                if (ratio < minimum || ratio > maximum)
                {
                    Fail(
                        "{0} action-detail timer [{1}] Hangul/digit visual height ratio {2} outside {3}..{4}: hangul={5}, digit={6}, bounds={7}",
                        fontPath,
                        Escape(phrase),
                        FormatRatio(ratio),
                        FormatRatio(minimum),
                        FormatRatio(maximum),
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
                string error;

                if (!TryMeasurePhraseVisualBounds(_patchedFont, pair.SourceFontPath, ActionDetailNumericBaselinePhrase, false, out lowNumeric, out error) ||
                    !TryMeasurePhraseVisualBounds(_patchedFont, pair.TargetFontPath, ActionDetailNumericBaselinePhrase, false, out highNumeric, out error))
                {
                    Warn(
                        "{0}->{1} large UI scale pair skipped: {2}",
                        pair.SourceFontPath,
                        pair.TargetFontPath,
                        error);
                    return;
                }

                double numericScale = SafeRatio(highNumeric.MeanDigitHeight, lowNumeric.MeanDigitHeight);
                for (int phraseIndex = 0; phraseIndex < LargeUiScalePhrases.Length; phraseIndex++)
                {
                    string phrase = LargeUiScalePhrases[phraseIndex];
                    PhraseVisualBounds lowPhrase;
                    PhraseVisualBounds highPhrase;
                    if (!TryMeasurePhraseVisualBounds(_patchedFont, pair.SourceFontPath, phrase, true, out lowPhrase, out error) ||
                        !TryMeasurePhraseVisualBounds(_patchedFont, pair.TargetFontPath, phrase, true, out highPhrase, out error))
                    {
                        Warn(
                            "{0}->{1} large UI [{2}] scale pair skipped: {3}",
                            pair.SourceFontPath,
                            pair.TargetFontPath,
                            Escape(phrase),
                            error);
                        continue;
                    }

                    VerifyActionDetailScaleRatio(
                        pair,
                        phrase,
                        "height",
                        numericScale,
                        lowPhrase.MeanHangulHeight,
                        highPhrase.MeanHangulHeight);
                    VerifyActionDetailScaleRatio(
                        pair,
                        phrase,
                        "width",
                        numericScale,
                        lowPhrase.MeanHangulWidth,
                        highPhrase.MeanHangulWidth);
                    VerifyActionDetailScaleRatio(
                        pair,
                        phrase,
                        "advance",
                        numericScale,
                        lowPhrase.MeanHangulAdvance,
                        highPhrase.MeanHangulAdvance);
                }
            }

            private void VerifyActionDetailScaleRatio(FontPair pair, string phrase, string axis, double numericScale, double lowValue, double highValue)
            {
                double hangulScale = SafeRatio(highValue, lowValue);
                double relative = SafeRatio(hangulScale, numericScale);
                bool strict = ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(pair.SourceFontPath) ||
                              ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(pair.TargetFontPath);
                double minimum = strict ? VisualScalePairMinRelativeRatio : 0.80d;
                double maximum = strict ? VisualScalePairMaxRelativeRatio : 1.20d;
                if (relative < minimum || relative > maximum)
                {
                    Fail(
                        "{0}->{1} action-detail [{2}] {3} scale ratio {4} outside {5}..{6}: numericScale={7}, hangulScale={8}, low={9}, high={10}",
                        pair.SourceFontPath,
                        pair.TargetFontPath,
                        Escape(phrase),
                        axis,
                        FormatRatio(relative),
                        FormatRatio(minimum),
                        FormatRatio(maximum),
                        FormatRatio(numericScale),
                        FormatRatio(hangulScale),
                        FormatDouble(lowValue),
                        FormatDouble(highValue));
                    return;
                }

                Pass(
                    "{0}->{1} action-detail [{2}] {3} scale follows numeric: relative={4}, numeric={5}, hangul={6}",
                    pair.SourceFontPath,
                    pair.TargetFontPath,
                    Escape(phrase),
                    axis,
                    FormatRatio(relative),
                    FormatRatio(numericScale),
                    FormatRatio(hangulScale));
            }

            private void VerifyActionDetailHighScaleTargetGlyphCoverage(HashSet<uint> codepoints)
            {
                if (codepoints == null || codepoints.Count == 0)
                {
                    Fail("No large UI high-scale Hangul codepoints were collected");
                    return;
                }

                byte[] sourceFdt;
                byte[] targetFdt;
                byte[] targetSourceFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(ActionDetailHighScaleHangulGlyphs.SourceFontPath);
                    targetFdt = _patchedFont.ReadFile(ActionDetailHighScaleHangulGlyphs.TargetFontPath);
                    targetSourceFdt = _ttmpFont.ReadFile(ActionDetailHighScaleHangulGlyphs.TargetFontPath);
                }
                catch (Exception ex)
                {
                    Fail("Large UI high-scale target/source font read error: {0}", ex.Message);
                    return;
                }

                PhraseVisualBounds numeric;
                string error;
                if (!TryMeasurePhraseVisualBounds(_patchedFont, ActionDetailHighScaleHangulGlyphs.TargetFontPath, ActionDetailNumericBaselinePhrase, false, out numeric, out error))
                {
                    Fail("{0} large UI numeric baseline failed: {1}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, error);
                    return;
                }

                int checkedGlyphs = 0;
                foreach (uint codepoint in codepoints)
                {
                    FdtGlyphEntry sourceGlyph;
                    FdtGlyphEntry targetGlyph;
                    FdtGlyphEntry targetSourceGlyph;
                    if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                    {
                        Fail("{0} large UI source is missing U+{1:X4}", ActionDetailHighScaleHangulGlyphs.SourceFontPath, codepoint);
                        continue;
                    }

                    if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                    {
                        Fail("{0} large UI target is missing U+{1:X4}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                        continue;
                    }

                    if (!TryFindGlyph(targetSourceFdt, codepoint, out targetSourceGlyph))
                    {
                        Fail("{0} large UI TTMP target source is missing U+{1:X4}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                        continue;
                    }

                    GlyphCanvas targetCanvas;
                    try
                    {
                        targetCanvas = RenderGlyph(_patchedFont, ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} U+{1:X4} large UI render failed: {2}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint, ex.Message);
                        continue;
                    }

                    if (targetGlyph.OffsetY == targetSourceGlyph.OffsetY)
                    {
                        Fail(
                            "{0} U+{1:X4} large UI target kept the TTMP high-scale offset and will render too small/low: target={2}, source={3}",
                            ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                            codepoint,
                            FormatGlyphSpacing(targetGlyph),
                            FormatGlyphSpacing(targetSourceGlyph));
                        continue;
                    }

                    if (!TryValidatePhraseGlyphShape(ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint, targetCanvas, out error))
                    {
                        Fail("{0} U+{1:X4} large UI glyph shape failed: {2}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint, error);
                        continue;
                    }

                    GlyphStats stats = AnalyzeGlyph(targetCanvas);
                    int height = stats.MinY <= stats.MaxY ? stats.MaxY - stats.MinY + 1 : 0;
                    double heightRatio = SafeRatio(height, numeric.MeanDigitHeight);
                    if (heightRatio < HighScaleGlyphMinHeightRatio || heightRatio > HighScaleGlyphMaxHeightRatio)
                    {
                        Fail(
                            "{0} U+{1:X4} large UI glyph height ratio {2} outside {3}..{4}: glyph={5}, digit={6}, target={7}, source={8}",
                            ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                            codepoint,
                            FormatRatio(heightRatio),
                            FormatRatio(HighScaleGlyphMinHeightRatio),
                            FormatRatio(HighScaleGlyphMaxHeightRatio),
                            height.ToString(CultureInfo.InvariantCulture),
                            FormatDouble(numeric.MeanDigitHeight),
                            FormatGlyphRoute(targetGlyph),
                            FormatGlyphRoute(sourceGlyph));
                        continue;
                    }

                    checkedGlyphs++;
                }

                if (checkedGlyphs != codepoints.Count)
                {
                    Fail(
                        "{0} large UI high-scale Hangul checked only {1}/{2} glyphs",
                        ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                        checkedGlyphs,
                        codepoints.Count);
                    return;
                }

                Pass(
                    "{0} large UI high-scale Hangul glyphs checked: glyphs={1}, collected={2}",
                    ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                    checkedGlyphs,
                    codepoints.Count);
            }

            private void VerifyActionDetailHighScaleTargetGlyphSourcePreservation(HashSet<uint> codepoints)
            {
                if (codepoints == null || codepoints.Count == 0)
                {
                    Fail("No large UI high-scale Hangul codepoints were collected");
                    return;
                }

                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(ActionDetailHighScaleHangulGlyphs.TargetFontPath);
                    targetFdt = _patchedFont.ReadFile(ActionDetailHighScaleHangulGlyphs.TargetFontPath);
                }
                catch (Exception ex)
                {
                    Fail("Large UI source-preservation target font read error: {0}", ex.Message);
                    return;
                }

                int checkedGlyphs = 0;
                foreach (uint codepoint in codepoints)
                {
                    FdtGlyphEntry sourceGlyph;
                    FdtGlyphEntry targetGlyph;
                    if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                    {
                        Fail("{0} large UI TTMP target source is missing U+{1:X4}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                        continue;
                    }

                    if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                    {
                        Fail("{0} large UI target is missing U+{1:X4}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                        continue;
                    }

                    if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph) ||
                        sourceGlyph.ImageIndex != targetGlyph.ImageIndex ||
                        sourceGlyph.X != targetGlyph.X ||
                        sourceGlyph.Y != targetGlyph.Y)
                    {
                        Fail(
                            "{0} U+{1:X4} large UI target glyph route differs from TTMP source: target={2}, source={3}",
                            ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                            codepoint,
                            FormatGlyphRoute(targetGlyph),
                            FormatGlyphRoute(sourceGlyph));
                        continue;
                    }

                    GlyphCanvas sourceCanvas;
                    GlyphCanvas targetCanvas;
                    try
                    {
                        sourceCanvas = RenderGlyph(_ttmpFont, ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                        targetCanvas = RenderGlyph(_patchedFont, ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} U+{1:X4} large UI source-preservation render failed: {2}", ActionDetailHighScaleHangulGlyphs.TargetFontPath, codepoint, ex.Message);
                        continue;
                    }

                    long diff = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                    if (diff != 0)
                    {
                        Fail(
                            "{0} U+{1:X4} large UI target pixels differ from TTMP source: score={2}, visible={3}/{4}",
                            ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                            codepoint,
                            diff,
                            targetCanvas.VisiblePixels,
                            sourceCanvas.VisiblePixels);
                        continue;
                    }

                    checkedGlyphs++;
                }

                if (checkedGlyphs != codepoints.Count)
                {
                    Fail(
                        "{0} large UI high-scale Hangul source preservation checked only {1}/{2} glyphs",
                        ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                        checkedGlyphs,
                        codepoints.Count);
                    return;
                }

                Pass(
                    "{0} large UI high-scale Hangul glyphs source-preserved: glyphs={1}, collected={2}",
                    ActionDetailHighScaleHangulGlyphs.TargetFontPath,
                    checkedGlyphs,
                    codepoints.Count);
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
                AddPatchedSheetHangulCodepoints(
                    codepoints,
                    ActionDetailHighScaleHangulGlyphs.LargeUiLabelSheetNames,
                    "large UI high-scale glyph verification");
                AddPatchedAddonRangeHangulCodepoints(
                    codepoints,
                    ActionDetailHighScaleHangulGlyphs.AddonRowRanges,
                    "action-detail high-scale glyph verification");
                RemoveDynamicHangulCodepoints(codepoints, ActionDetailHighScaleHangulGlyphs.CombatFlyTextPreservePhrases);
                return codepoints;
            }

            private int AddPatchedSheetHangulCodepoints(HashSet<uint> codepoints, string[] sheets, string label)
            {
                if (codepoints == null || sheets == null || sheets.Length == 0)
                {
                    return 0;
                }

                int before = codepoints.Count;
                for (int sheetIndex = 0; sheetIndex < sheets.Length; sheetIndex++)
                {
                    AddPatchedSheetHangulCodepoints(codepoints, sheets[sheetIndex], label);
                }

                return codepoints.Count - before;
            }

            private void AddPatchedSheetHangulCodepoints(HashSet<uint> codepoints, string sheet, string label)
            {
                if (string.IsNullOrWhiteSpace(sheet))
                {
                    return;
                }

                try
                {
                    ExcelHeader header = ExcelHeader.Parse(_patchedText.ReadFile("exd/" + sheet + ".exh"));
                    if (header.Variant != ExcelVariant.Default)
                    {
                        Warn("{0} header variant is not supported for {1}: {2}", sheet, label, header.Variant);
                        return;
                    }

                    byte languageId = LanguageToId(_language);
                    bool hasLanguageSuffix = header.HasLanguage(languageId);
                    List<int> stringColumns = header.GetStringColumnIndexes();
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        string exdPath = BuildExdPath(sheet, page.StartId, _language, hasLanguageSuffix);
                        ExcelDataFile file = ExcelDataFile.Parse(_patchedText.ReadFile(exdPath));
                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                            {
                                byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]);
                                AddDynamicHangulCodepoints(codepoints, bytes);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Warn("Could not collect sheet glyph coverage for {0} ({1}): {2}", sheet, label, ex.Message);
                }
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

            private static void RemoveDynamicHangulCodepoints(HashSet<uint> codepoints, string[] phrases)
            {
                if (codepoints == null || phrases == null)
                {
                    return;
                }

                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    RemoveDynamicHangulCodepoints(codepoints, phrases[phraseIndex]);
                }
            }

            private static void RemoveDynamicHangulCodepoints(HashSet<uint> codepoints, string phrase)
            {
                phrase = phrase ?? string.Empty;
                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (IsHangulCodepoint(codepoint))
                    {
                        codepoints.Remove(codepoint);
                    }
                }
            }
        }

        private struct PhraseVisualBounds
        {
            private int _minX;
            private int _minY;
            private int _maxX;
            private int _maxY;
            private int _hangulHeightTotal;
            private int _hangulWidthTotal;
            private int _hangulAdvanceTotal;
            private int _hangulCount;
            private int _digitHeightTotal;
            private int _digitWidthTotal;
            private int _digitAdvanceTotal;
            private int _digitCount;
            private int _referenceHeightTotal;
            private int _referenceWidthTotal;
            private int _referenceAdvanceTotal;
            private int _referenceCount;
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

            public double MeanHangulWidth
            {
                get { return _hangulCount == 0 ? 0d : (double)_hangulWidthTotal / _hangulCount; }
            }

            public double MeanHangulAdvance
            {
                get { return _hangulCount == 0 ? 0d : (double)_hangulAdvanceTotal / _hangulCount; }
            }

            public double MeanDigitHeight
            {
                get { return _digitCount == 0 ? 0d : (double)_digitHeightTotal / _digitCount; }
            }

            public double MeanDigitWidth
            {
                get { return _digitCount == 0 ? 0d : (double)_digitWidthTotal / _digitCount; }
            }

            public double MeanDigitAdvance
            {
                get { return _digitCount == 0 ? 0d : (double)_digitAdvanceTotal / _digitCount; }
            }

            public double MeanReferenceHeight
            {
                get { return _referenceCount == 0 ? 0d : (double)_referenceHeightTotal / _referenceCount; }
            }

            public double MeanReferenceWidth
            {
                get { return _referenceCount == 0 ? 0d : (double)_referenceWidthTotal / _referenceCount; }
            }

            public double MeanReferenceAdvance
            {
                get { return _referenceCount == 0 ? 0d : (double)_referenceAdvanceTotal / _referenceCount; }
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
                    int width = maxX - minX + 1;
                    int advance = GetGlyphAdvance(glyph);
                    if (IsHangulCodepoint(codepoint))
                    {
                        _hangulHeightTotal += height;
                        _hangulWidthTotal += width;
                        _hangulAdvanceTotal += advance;
                        _hangulFdtHeightTotal += glyph.Height;
                        _hangulOffsetYTotal += glyph.OffsetY;
                        _hangulCount++;
                    }
                    else if (codepoint >= '0' && codepoint <= '9')
                    {
                        _digitHeightTotal += height;
                        _digitWidthTotal += width;
                        _digitAdvanceTotal += advance;
                        _digitFdtHeightTotal += glyph.Height;
                        _digitOffsetYTotal += glyph.OffsetY;
                        _digitCount++;
                    }
                    else if (IsReferenceCjkCodepoint(codepoint))
                    {
                        _referenceHeightTotal += height;
                        _referenceWidthTotal += width;
                        _referenceAdvanceTotal += advance;
                        _referenceCount++;
                    }
                }

                VisiblePixels += visiblePixels;
                Glyphs++;
            }

            private static bool IsReferenceCjkCodepoint(uint codepoint)
            {
                return (codepoint >= 0x3040u && codepoint <= 0x309Fu) ||
                       (codepoint >= 0x30A0u && codepoint <= 0x30FFu) ||
                       (codepoint >= 0x4E00u && codepoint <= 0x9FFFu) ||
                       codepoint == 0x3005u ||
                       codepoint == 0x30FCu;
            }
        }
    }
}
