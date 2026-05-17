namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class PvpProfileVisualScaleGlyphs
    {
        public const double HangulToDigitRatio = 1.08d;
        public const double JupiterCanvasScaleCompensation = 0.76d;

        public static readonly string[] TargetFontPaths = new string[]
        {
            "common/font/Jupiter_16.fdt",
            "common/font/Jupiter_20.fdt",
            "common/font/Jupiter_23.fdt"
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
