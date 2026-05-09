using System.Collections.Generic;

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
                        Fail("{0} has extra ASCII kerning pair {1} not present in {2}", targetFontPath, targetKey, sourceFontPath);
                        ok = false;
                    }
                }

                return checkedPairs;
            }
        }
    }
}
