using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private int VerifyCleanAsciiKerningRoute(
                byte[] sourceFdt,
                byte[] targetFdt,
                string sourceFontPath,
                string targetFontPath,
                ref bool ok)
            {
                Dictionary<string, byte[]> sourceKerning = ReadAsciiKerningEntries(sourceFdt);
                Dictionary<string, byte[]> targetKerning = ReadAsciiKerningEntries(targetFdt);
                int checkedPairs = 0;

                foreach (KeyValuePair<string, byte[]> sourcePair in sourceKerning)
                {
                    byte[] targetEntry;
                    if (!targetKerning.TryGetValue(sourcePair.Key, out targetEntry))
                    {
                        Fail("{0} missing ASCII kerning pair {1} from {2}", targetFontPath, sourcePair.Key, sourceFontPath);
                        ok = false;
                        continue;
                    }

                    if (!KerningEntryMatchesOrLobbySafe(targetFontPath, sourcePair.Value, targetEntry))
                    {
                        Fail(
                            "{0} ASCII kerning pair {1} differs from {2}: target={3}, clean={4}",
                            targetFontPath,
                            sourcePair.Key,
                            sourceFontPath,
                            ToHex(targetEntry),
                            ToHex(sourcePair.Value));
                        ok = false;
                        continue;
                    }

                    checkedPairs++;
                }

                foreach (string targetKey in targetKerning.Keys)
                {
                    if (!sourceKerning.ContainsKey(targetKey))
                    {
                        if (IsAllowedStartScreenAsciiKerningPair(targetFontPath, targetKey, targetKerning[targetKey]))
                        {
                            checkedPairs++;
                            continue;
                        }

                        Fail("{0} has extra ASCII kerning pair {1} not present in {2}", targetFontPath, targetKey, sourceFontPath);
                        ok = false;
                    }
                }

                return checkedPairs;
            }

            private static readonly System.Lazy<HashSet<string>> AllowedStartScreenAsciiKerningPairs =
                new System.Lazy<HashSet<string>>(CreateAllowedStartScreenAsciiKerningPairs);

            private static readonly System.Lazy<HashSet<string>> AllowedLobbySystemSettingsKerningPairs =
                new System.Lazy<HashSet<string>>(CreateAllowedLobbySystemSettingsKerningPairs);

            private static bool IsAllowedStartScreenAsciiKerningPair(string targetFontPath, string key, byte[] targetEntry)
            {
                uint left;
                uint right;
                if (!TryDecodeKerningKey(key, out left, out right))
                {
                    return false;
                }

                int targetAdjustment = targetEntry != null && targetEntry.Length >= FdtKerningEntrySize
                    ? unchecked((int)Endian.ReadUInt32LE(targetEntry, 12))
                    : 0;
                return IsAllowedStartScreenAsciiKerningAdjustment(targetFontPath, left, right, 0, targetAdjustment);
            }

            private static bool IsAllowedStartScreenAsciiKerningAdjustment(
                string targetFontPath,
                uint leftPackedUtf8,
                uint rightPackedUtf8,
                int sourceAdjustment,
                int targetAdjustment)
            {
                string normalized = (targetFontPath ?? string.Empty).Replace('\\', '/');
                if (!string.Equals(normalized, "common/font/AXIS_14.fdt", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "common/font/KrnAXIS_140.fdt", System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (sourceAdjustment != 0 || targetAdjustment != 1)
                {
                    return false;
                }

                string key = leftPackedUtf8.ToString("X2") + ":" + rightPackedUtf8.ToString("X2");
                return AllowedStartScreenAsciiKerningPairs.Value.Contains(key);
            }

            private static bool IsAllowedLobbySystemSettingsKerningAdjustment(
                string targetFontPath,
                uint leftPackedUtf8,
                uint rightPackedUtf8,
                int sourceAdjustment,
                int targetAdjustment)
            {
                string normalized = (targetFontPath ?? string.Empty).Replace('\\', '/');
                bool axis12 = string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", System.StringComparison.OrdinalIgnoreCase);
                bool axis14Or18 =
                    string.Equals(normalized, "common/font/AXIS_14_lobby.fdt", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "common/font/AXIS_18_lobby.fdt", System.StringComparison.OrdinalIgnoreCase);
                if (!axis12 && !axis14Or18)
                {
                    return false;
                }

                int expectedTargetAdjustment = axis12 ? 2 : 1;
                if (targetAdjustment != expectedTargetAdjustment || sourceAdjustment > targetAdjustment)
                {
                    return false;
                }

                string key = leftPackedUtf8.ToString("X2") + ":" + rightPackedUtf8.ToString("X2");
                return AllowedLobbySystemSettingsKerningPairs.Value.Contains(key);
            }

            private static bool TryDecodeKerningKey(string key, out uint leftPackedUtf8, out uint rightPackedUtf8)
            {
                leftPackedUtf8 = 0;
                rightPackedUtf8 = 0;
                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }

                string[] parts = key.Split(':');
                return parts.Length == 2 &&
                       uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out leftPackedUtf8) &&
                       uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out rightPackedUtf8);
            }

            private static HashSet<string> CreateAllowedStartScreenAsciiKerningPairs()
            {
                HashSet<string> pairs = new HashSet<string>(System.StringComparer.Ordinal);
                string[] phrases = LobbyScaledHangulPhrases.HighResolutionUiScaleOptions;
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    uint previous = 0;
                    bool hasPrevious = false;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPrevious && previous >= '0' && previous <= '9' && codepoint == '%')
                        {
                            pairs.Add(PackUtf8(previous).ToString("X2") + ":" + PackUtf8(codepoint).ToString("X2"));
                        }

                        previous = codepoint;
                        hasPrevious = !IsPhraseLayoutSpace(codepoint);
                    }
                }

                return pairs;
            }

            private static HashSet<string> CreateAllowedLobbySystemSettingsKerningPairs()
            {
                HashSet<string> pairs = new HashSet<string>(System.StringComparer.Ordinal);
                AddAdjacentHangulKerningPairs(pairs, LobbyScaledHangulPhrases.StartScreenSystemSettings);
                AddAdjacentHangulKerningPairs(pairs, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions);
                AddAdjacentHangulKerningPairs(pairs, LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);
                AddTerminalPunctuationKerningPairs(pairs, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions);
                AddTerminalPunctuationKerningPairs(pairs, LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);
                return pairs;
            }

            private static void AddAdjacentHangulKerningPairs(HashSet<string> pairs, string[] phrases)
            {
                AddKerningPairs(
                    pairs,
                    phrases,
                    delegate(uint left, uint right)
                    {
                        return IsHangulCodepoint(left) && IsHangulCodepoint(right);
                    });
            }

            private static void AddTerminalPunctuationKerningPairs(HashSet<string> pairs, string[] phrases)
            {
                AddKerningPairs(
                    pairs,
                    phrases,
                    delegate(uint left, uint right)
                    {
                        return IsHangulCodepoint(left) && IsVerifierTerminalPunctuationCodepoint(right);
                    });
            }

            private static void AddKerningPairs(HashSet<string> pairs, string[] phrases, KerningPairPredicate predicate)
            {
                if (pairs == null || phrases == null)
                {
                    return;
                }

                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    uint previous = 0;
                    bool hasPrevious = false;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (hasPrevious && predicate(previous, codepoint))
                        {
                            pairs.Add(PackUtf8(previous).ToString("X2") + ":" + PackUtf8(codepoint).ToString("X2"));
                        }

                        previous = codepoint;
                        hasPrevious = !IsPhraseLayoutSpace(codepoint);
                    }
                }
            }

            private delegate bool KerningPairPredicate(uint left, uint right);

            private static bool IsVerifierTerminalPunctuationCodepoint(uint codepoint)
            {
                return codepoint == 0x002Eu ||
                       codepoint == 0x3002u ||
                       codepoint == 0xFF0Eu;
            }
        }
    }
}
