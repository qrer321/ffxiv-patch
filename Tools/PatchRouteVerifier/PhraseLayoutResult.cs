namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private struct PhraseLayoutResult
        {
            public int Glyphs;
            public int Width;
            public int OverlapPixels;
            public int MinimumGapPixels;
            public int RequiredMinimumGapPixels;
            public uint MinimumGapLeftCodepoint;
            public uint MinimumGapRightCodepoint;
            public int MinimumRequiredGapActualPixels;
            public int MinimumRequiredGapPixels;
            public uint MinimumRequiredGapLeftCodepoint;
            public uint MinimumRequiredGapRightCodepoint;
        }
    }
}
