namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class PvpProfileVisualScaleGlyphs
    {
        public const double HangulToDigitRatio = 1.03d;
        public const double JupiterCanvasScaleCompensation = 1.0d;

        // Jupiter_16/20 are shared by Action, PvP, and Free Company ULDs.
        // Route-local PvP sizing must not mutate their global Hangul glyphs.
        public static readonly string[] TargetFontPaths = new string[0];

        // Keep the retired transform's allocation footprint until all later
        // atlas writers have been migrated away from the shared allocator.
        // The generator reserves these cells without changing FDT/TEX payloads.
        public static readonly string[] LegacyAtlasReservationFontPaths = new string[]
        {
            "common/font/Jupiter_16.fdt",
            "common/font/Jupiter_20.fdt"
        };

        public static readonly string[] SheetNames = new string[]
        {
            "PvPRankTransient",
            "PvPSelectTrait",
            "PvPSelectTraitTransient",
            "XPVPGroupActivity"
        };

        public static readonly AddonRowRange[] AddonRowRanges = new AddonRowRange[]
        {
            new AddonRowRange(5530, 5556),
            new AddonRowRange(8081, 8085),
            new AddonRowRange(10231, 10232),
            new AddonRowRange(11720, 11722)
        };

        public static readonly string[] FallbackPhrases = new string[]
        {
            ActionDetailHighScaleHangulGlyphs.PvpProfilePhrase,
            ActionDetailHighScaleHangulGlyphs.BattleRecordPhrase,
            ActionDetailHighScaleHangulGlyphs.CrystallineConflictPhrase,
            ActionDetailHighScaleHangulGlyphs.FrontlinePhrase,
            ActionDetailHighScaleHangulGlyphs.RivalWingsPhrase,
            ActionDetailHighScaleHangulGlyphs.PvpActionsPhrase,
            ActionDetailHighScaleHangulGlyphs.TacticalCommunicationPhrase
        };

        public static bool IsTargetFontPath(string path)
        {
            string normalized = Normalize(path);
            for (int i = 0; i < TargetFontPaths.Length; i++)
            {
                if (string.Equals(normalized, TargetFontPaths[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLegacyAtlasReservationFontPath(string path)
        {
            string normalized = Normalize(path);
            for (int i = 0; i < LegacyAtlasReservationFontPaths.Length; i++)
            {
                if (string.Equals(normalized, LegacyAtlasReservationFontPaths[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
