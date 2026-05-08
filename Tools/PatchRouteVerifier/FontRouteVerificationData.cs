namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] DataCenterTitleSameFontChecks = new string[]
            {
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/AXIS_96.fdt",
                "common/font/AXIS_12_lobby.fdt",
                "common/font/AXIS_14_lobby.fdt",
                "common/font/AXIS_18_lobby.fdt",
                "common/font/AXIS_36_lobby.fdt"
            };

            private static readonly string[,] DataCenterTitleKoreanFontChecks = new string[,]
            {
                { "common/font/AXIS_12.fdt", "common/font/KrnAXIS_120.fdt" },
                { "common/font/AXIS_14.fdt", "common/font/KrnAXIS_140.fdt" },
                { "common/font/AXIS_18.fdt", "common/font/KrnAXIS_180.fdt" },
                { "common/font/AXIS_36.fdt", "common/font/KrnAXIS_360.fdt" }
            };

            private static readonly CleanAsciiReferenceRoute[] CleanAsciiReferenceRoutes = new CleanAsciiReferenceRoute[]
            {
                new CleanAsciiReferenceRoute("/KrnAXIS_120.fdt", "common/font/AXIS_12.fdt"),
                new CleanAsciiReferenceRoute("/KrnAXIS_140.fdt", "common/font/AXIS_14.fdt"),
                new CleanAsciiReferenceRoute("/KrnAXIS_180.fdt", "common/font/AXIS_18.fdt"),
                new CleanAsciiReferenceRoute("/KrnAXIS_360.fdt", "common/font/AXIS_36.fdt")
            };

            private struct CleanAsciiReferenceRoute
            {
                public readonly string TargetSuffix;
                public readonly string SourceFontPath;

                public CleanAsciiReferenceRoute(string targetSuffix, string sourceFontPath)
                {
                    TargetSuffix = targetSuffix;
                    SourceFontPath = sourceFontPath;
                }
            }
        }
    }
}
