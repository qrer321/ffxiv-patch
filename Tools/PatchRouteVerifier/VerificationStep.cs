using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private sealed class VerificationStep
            {
                public readonly string Name;
                public readonly Action Run;

                public VerificationStep(string name, Action run)
                {
                    Name = name;
                    Run = run;
                }
            }
        }
    }
}
