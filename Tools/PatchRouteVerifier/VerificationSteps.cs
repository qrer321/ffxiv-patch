using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void RunVerificationSteps()
            {
                VerificationStep[] steps = CreateVerificationSteps();
                HashSet<string> selected = CreateSelectedCheckSet(steps, _selectedChecks);
                if (selected != null)
                {
                    Console.WriteLine("  checks: {0}", string.Join(",", _selectedChecks));
                }

                for (int i = 0; i < steps.Length; i++)
                {
                    if (selected == null || selected.Contains(steps[i].Name))
                    {
                        steps[i].Run();
                    }
                }
            }
        }
    }
}
