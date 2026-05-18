using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] PvpProfileFontRouteUlds = new string[]
            {
                "ui/uld/PvPProfile.uld",
                "ui/uld/PvPCharacter.uld",
                "ui/uld/PvPAction.uld",
                "ui/uld/PvPActions.uld",
                "ui/uld/PvPTeam.uld",
                "ui/uld/PvPTeamBoard.uld",
                "ui/uld/PvPSchedule.uld"
            };

            private const double PvpReferenceMinRatio = 0.86d;
            private const double PvpReferenceMaxRatio = 1.18d;
            private const double PvpNumericMinRatio = 0.86d;
            private const double PvpNumericMaxRatio = 1.18d;

            private static readonly string[] PvpVisualScaleCandidateFonts = new string[]
            {
                "common/font/Jupiter_16.fdt",
                "common/font/Jupiter_20.fdt"
            };

            private static readonly PvpProfileRoutePhrase[] PvpProfileRoutePhrases = new PvpProfileRoutePhrase[]
            {
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.PvpProfilePhrase, "PvP\u30D7\u30ED\u30D5\u30A3\u30FC\u30EB"),
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.BattleRecordPhrase, "\u6226\u7E3E"),
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.CrystallineConflictPhrase, "\u30AF\u30EA\u30B9\u30BF\u30EB\u30B3\u30F3\u30D5\u30EA\u30AF\u30C8"),
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.FrontlinePhrase, "\u30D5\u30ED\u30F3\u30C8\u30E9\u30A4\u30F3"),
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.RivalWingsPhrase, "\u30E9\u30A4\u30D0\u30EB\u30A6\u30A3\u30F3\u30B0\u30BA"),
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.PvpActionsPhrase, "PvP\u30A2\u30AF\u30B7\u30E7\u30F3"),
                new PvpProfileRoutePhrase(ActionDetailHighScaleHangulGlyphs.TacticalCommunicationPhrase, "\u30AF\u30A4\u30C3\u30AF\u30C1\u30E3\u30C3\u30C8")
            };

            private void VerifyPvpProfileFontRoutes()
            {
                Console.WriteLine("[ULD/FDT] PvP profile font routes");

                HashSet<string> routedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int foundUlds = 0;
                for (int i = 0; i < PvpProfileFontRouteUlds.Length; i++)
                {
                    HashSet<string> uldFonts;
                    if (!TryCollectOptionalPreservedUldFontRoutes(
                        PvpProfileFontRouteUlds[i],
                        "PvP profile",
                        false,
                        out uldFonts))
                    {
                        continue;
                    }

                    foundUlds++;
                    foreach (string fontPath in uldFonts)
                    {
                        routedFonts.Add(fontPath);
                    }
                }

                if (foundUlds == 0)
                {
                    Fail("No PvP profile ULD candidate was found; verifier is not covering the reported PvP profile route");
                    return;
                }

                int measured = 0;
                foreach (string fontPath in routedFonts)
                {
                    measured += VerifyPvpProfileFont(fontPath);
                }

                if (measured == 0)
                {
                    Fail("No PvP profile routed font could render the reported PvP phrases");
                }
                else
                {
                    Pass("PvP profile routed phrases checked: ulds={0}, fonts={1}, phrases={2}", foundUlds, routedFonts.Count, measured);
                }
            }

            private int VerifyPvpProfileFont(string fontPath)
            {
                PhraseVisualBounds numeric;
                string error;
                if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, ActionDetailNumericBaselinePhrase, false, out numeric, out error))
                {
                    Warn("{0} PvP numeric baseline skipped: {1}", fontPath, error);
                    return 0;
                }

                int measured = 0;
                for (int i = 0; i < PvpProfileRoutePhrases.Length; i++)
                {
                    PvpProfileRoutePhrase phrase = PvpProfileRoutePhrases[i];
                    PhraseVisualBounds phraseBounds;
                    if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, phrase.Korean, true, out phraseBounds, out error))
                    {
                        Warn("{0} PvP phrase [{1}] skipped: {2}", fontPath, Escape(phrase.Korean), error);
                        continue;
                    }

                    if (ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(fontPath))
                    {
                        VerifyActionDetailValueHeight(fontPath, phrase.Korean, phraseBounds, numeric);
                    }

                    if (IsPvpVisualScaleCandidateFontPath(fontPath))
                    {
                        VerifyPvpNumericScale(fontPath, phrase, phraseBounds, numeric);
                    }
                    else
                    {
                        ReportPvpReferenceScale(fontPath, phrase, phraseBounds);
                    }

                    VerifyNoPhraseOverlap(fontPath, phrase.Korean);
                    measured++;
                }

                return measured;
            }

            private void VerifyPvpNumericScale(string fontPath, PvpProfileRoutePhrase phrase, PhraseVisualBounds korean, PhraseVisualBounds numeric)
            {
                double ratio = SafeRatio(korean.MeanHangulHeight, numeric.MeanDigitHeight);
                if (ratio < PvpNumericMinRatio || ratio > PvpNumericMaxRatio)
                {
                    Fail(
                        "{0} PvP phrase [{1}] Hangul/digit visual height ratio {2} outside {3}..{4}: hangul={5}, digit={6}, bounds={7}",
                        fontPath,
                        Escape(phrase.Korean),
                        FormatRatio(ratio),
                        FormatRatio(PvpNumericMinRatio),
                        FormatRatio(PvpNumericMaxRatio),
                        FormatDouble(korean.MeanHangulHeight),
                        FormatDouble(numeric.MeanDigitHeight),
                        FormatPhraseBounds(korean));
                    return;
                }

                Pass(
                    "{0} PvP phrase [{1}] matches numeric visual scale: ratio={2}, hangul={3}, digit={4}",
                    fontPath,
                    Escape(phrase.Korean),
                    FormatRatio(ratio),
                    FormatDouble(korean.MeanHangulHeight),
                    FormatDouble(numeric.MeanDigitHeight));
            }

            private void ReportPvpReferenceScale(string fontPath, PvpProfileRoutePhrase phrase, PhraseVisualBounds korean)
            {
                PhraseVisualBounds reference;
                string error;
                if (!TryMeasurePhraseVisualBounds(_cleanFont, fontPath, phrase.Reference, false, out reference, out error))
                {
                    Warn("{0} PvP reference phrase [{1}] skipped: {2}", fontPath, Escape(phrase.Reference), error);
                    return;
                }

                if (korean.MeanHangulHeight <= 0d || reference.MeanReferenceHeight <= 0d)
                {
                    Warn(
                        "{0} PvP reference ratio skipped for [{1}]/[{2}]: korean={3}, reference={4}",
                        fontPath,
                        Escape(phrase.Korean),
                        Escape(phrase.Reference),
                        FormatDouble(korean.MeanHangulHeight),
                        FormatDouble(reference.MeanReferenceHeight));
                    return;
                }

                double ratio = SafeRatio(korean.MeanHangulHeight, reference.MeanReferenceHeight);
                if (ratio < PvpReferenceMinRatio || ratio > PvpReferenceMaxRatio)
                {
                    Warn(
                        "{0} PvP phrase [{1}] Hangul/reference CJK height ratio {2} outside advisory {3}..{4}: hangul={5}, reference=[{6}] {7}, koreanBounds={8}, referenceBounds={9}",
                        fontPath,
                        Escape(phrase.Korean),
                        FormatRatio(ratio),
                        FormatRatio(PvpReferenceMinRatio),
                        FormatRatio(PvpReferenceMaxRatio),
                        FormatDouble(korean.MeanHangulHeight),
                        Escape(phrase.Reference),
                        FormatDouble(reference.MeanReferenceHeight),
                        FormatPhraseBounds(korean),
                        FormatPhraseBounds(reference));
                    return;
                }

                Pass(
                    "{0} PvP phrase [{1}] matches reference CJK scale: ratio={2}, hangul={3}, reference=[{4}] {5}",
                    fontPath,
                    Escape(phrase.Korean),
                    FormatRatio(ratio),
                    FormatDouble(korean.MeanHangulHeight),
                    Escape(phrase.Reference),
                    FormatDouble(reference.MeanReferenceHeight));
            }

            private static bool IsPvpVisualScaleCandidateFontPath(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                for (int i = 0; i < PvpVisualScaleCandidateFonts.Length; i++)
                {
                    if (string.Equals(normalized, PvpVisualScaleCandidateFonts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private struct PvpProfileRoutePhrase
            {
                public readonly string Korean;
                public readonly string Reference;

                public PvpProfileRoutePhrase(string korean, string reference)
                {
                    Korean = korean;
                    Reference = reference;
                }
            }
        }
    }
}
