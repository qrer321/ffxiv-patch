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

                    string[] groupChecks = ResolveCheckGroup(check);
                    if (groupChecks != null)
                    {
                        AddCheckGroup(steps, known, selected, check, groupChecks);
                        continue;
                    }

                    if (!known.Contains(check))
                    {
                        throw new ArgumentException(
                            "unknown check: " + check + Environment.NewLine +
                            "available checks: " + FormatAvailableChecks(steps) + Environment.NewLine +
                            "available groups: lobby-critical,ingame-critical,font-critical");
                    }

                    selected.Add(check);
                }

                return selected;
            }

            private static void AddCheckGroup(
                VerificationStep[] steps,
                HashSet<string> known,
                HashSet<string> selected,
                string groupName,
                string[] groupChecks)
            {
                for (int groupIndex = 0; groupIndex < groupChecks.Length; groupIndex++)
                {
                    string check = groupChecks[groupIndex];
                    string[] nested = ResolveCheckGroup(check);
                    if (nested != null)
                    {
                        AddCheckGroup(steps, known, selected, check, nested);
                        continue;
                    }

                    if (!known.Contains(check))
                    {
                        throw new ArgumentException(
                            "unknown check in group " + groupName + ": " + check + Environment.NewLine +
                            "available checks: " + FormatAvailableChecks(steps));
                    }

                    selected.Add(check);
                }
            }

            private static string[] ResolveCheckGroup(string check)
            {
                if (string.Equals(check, "lobby-critical", StringComparison.OrdinalIgnoreCase))
                {
                    return new string[]
                    {
                        "start-system-settings-uld",
                        "system-settings-mixed-scale-layouts",
                        "system-settings-scaled-phrase-layouts",
                        "high-scale-ascii-phrase-layouts",
                        "lobby-scale-font-sources",
                        "lobby-render-snapshots",
                        "lobby-large-label-scale-layouts",
                        "lobby-coverage-glyphs",
                        "lobby-runtime-font-safety",
                        "lobby-multitexture-font-set",
                        "lobby-texture-cell-margin"
                    };
                }

                if (string.Equals(check, "ingame-critical", StringComparison.OrdinalIgnoreCase))
                {
                    return new string[]
                    {
                        "font-runtime-glyph-bounds",
                        "ingame-clean-ascii-glyphs",
                        "numeric-glyphs",
                        "party-list-self-marker",
                        "combat-flytext-damage-glyphs",
                        "third-party-game-font-safety",
                        "action-detail-scale-layouts",
                        "pvp-profile-font-routes"
                    };
                }

                if (string.Equals(check, "font-critical", StringComparison.OrdinalIgnoreCase))
                {
                    return new string[]
                    {
                        "lobby-critical",
                        "ingame-critical"
                    };
                }

                return null;
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
