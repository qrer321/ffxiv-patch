using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static long Diff(byte[] left, byte[] right)
        {
            long score = 0;
            for (int i = 0; i < left.Length; i++)
            {
                score += Math.Abs(left[i] - right[i]);
            }

            return score;
        }
    }
}
