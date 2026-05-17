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

            private static readonly string[] PvpProfileRoutePhrases = new string[]
            {
                ActionDetailHighScaleHangulGlyphs.PvpProfilePhrase,
                ActionDetailHighScaleHangulGlyphs.BattleRecordPhrase,
                ActionDetailHighScaleHangulGlyphs.CrystallineConflictPhrase,
                ActionDetailHighScaleHangulGlyphs.FrontlinePhrase,
                ActionDetailHighScaleHangulGlyphs.RivalWingsPhrase,
                ActionDetailHighScaleHangulGlyphs.PvpActionsPhrase,
                ActionDetailHighScaleHangulGlyphs.TacticalCommunicationPhrase
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
                    string phrase = PvpProfileRoutePhrases[i];
                    PhraseVisualBounds phraseBounds;
                    if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, phrase, true, out phraseBounds, out error))
                    {
                        Warn("{0} PvP phrase [{1}] skipped: {2}", fontPath, Escape(phrase), error);
                        continue;
                    }

                    if (ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(fontPath))
                    {
                        VerifyActionDetailValueHeight(fontPath, phrase, phraseBounds, numeric);
                    }

                    VerifyNoPhraseOverlap(fontPath, phrase);
                    measured++;
                }

                return measured;
            }
        }
    }
}
