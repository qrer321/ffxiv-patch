using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static HashSet<string> CreateSelectedCheckSet(VerificationStep[] steps, string[] selectedChecks)
            {
                if (selectedChecks == null || selectedChecks.Length == 0)
                {
                    return null;
                }

                HashSet<string> known = CreateKnownCheckSet(steps);
                HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < selectedChecks.Length; i++)
                {
                    string check = selectedChecks[i];
                    if (string.IsNullOrWhiteSpace(check))
                    {
                        continue;
                    }

                    if (!known.Contains(check))
                    {
                        throw new ArgumentException(
                            "unknown check: " + check + Environment.NewLine +
                            "available checks: " + FormatAvailableChecks(steps));
                    }

                    selected.Add(check);
                }

                return selected;
            }

            private static HashSet<string> CreateKnownCheckSet(VerificationStep[] steps)
            {
                HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < steps.Length; i++)
                {
                    known.Add(steps[i].Name);
                }

                return known;
            }

            private static string FormatAvailableChecks(VerificationStep[] steps)
            {
                string[] names = new string[steps.Length];
                for (int i = 0; i < steps.Length; i++)
                {
                    names[i] = steps[i].Name;
                }

                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                return string.Join(",", names);
            }
        }
    }
}
