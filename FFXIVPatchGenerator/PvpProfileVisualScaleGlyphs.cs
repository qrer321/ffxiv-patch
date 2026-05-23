namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class PvpProfileVisualScaleGlyphs
    {
        public const double HangulToDigitRatio = 1.03d;
        public const double JupiterCanvasScaleCompensation = 1.0d;

        public static readonly string[] TargetFontPaths = new string[]
        {
            // Source-preserving these routes makes Korean PvP labels overflow
            // their ULD areas. The generator crops visible source pixels before
            // scaling, so this target renders around the verifier's 1.16..1.42
            // digit-height window rather than the raw source ratio.
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

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
