using System;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class ActionDetailHighScaleHangulGlyphs
    {
        public const string SourceFontPath = "common/font/TrumpGothic_34.fdt";
        public const string TargetFontPath = "common/font/TrumpGothic_68.fdt";

        public const string InstantCastPhrase = "\uC989\uC2DC \uBC1C\uB3D9";
        public const string CastTimePhrase = "\uC2DC\uC804 \uC2DC\uAC04";
        public const string RecastPhrase = "\uC7AC\uC0AC\uC6A9";
        public const string RecastTimePhrase = "\uC7AC\uC0AC\uC6A9 \uB300\uAE30 \uC2DC\uAC04";
        public const string ActivationConditionPhrase = "\uBC1C\uB3D9 \uC870\uAC74";
        public const string SecondUnitPhrase = "\uCD08";

        public static readonly string[] FallbackPhrases = new string[]
        {
            InstantCastPhrase,
            CastTimePhrase,
            RecastPhrase,
            RecastTimePhrase,
            ActivationConditionPhrase,
            SecondUnitPhrase
        };

        public static readonly AddonRowRange[] AddonRowRanges = new AddonRowRange[]
        {
            new AddonRowRange(699, 714)
        };

        public static bool IsTargetFontPath(string path)
        {
            return string.Equals(
                Normalize(path),
                TargetFontPath,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
