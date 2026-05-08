using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyDataCenterTitleUldRoute()
            {
                VerifyUldFontPreservation(DataCenterTitleUldPath, "data-center title", "data-center-title");
            }

            private void VerifyDataCenterWorldmapUldRoute()
            {
                VerifyUldFontPreservation(DataCenterWorldmapUldPath, "data-center world map", "data-center-worldmap");
            }

            private void VerifyUldFontPreservation(string uldPath, string label, string dumpGroup)
            {
                Console.WriteLine("[ULD/FDT] {0} font preservation", label);
                byte[] cleanUld = _cleanUi.ReadFile(uldPath);
                byte[] patchedUld = _patchedUi.ReadFile(uldPath);
                List<UldTextNodeFont> cleanFonts = GetUldTextNodeFonts(cleanUld);
                List<UldTextNodeFont> patchedFonts = GetUldTextNodeFonts(patchedUld);
                Dictionary<int, UldTextNodeFont> patchedByOffset = GetUldTextNodeFontsByOffset(patchedUld);

                if (cleanFonts.Count == 0)
                {
                    Fail("{0} clean ULD did not expose text-node fonts", uldPath);
                    return;
                }

                if (cleanFonts.Count != patchedFonts.Count)
                {
                    Fail(
                        "{0} text-node count changed: clean={1}, patched={2}",
                        uldPath,
                        cleanFonts.Count,
                        patchedFonts.Count);
                }

                HashSet<string> routedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cleanFonts.Count; i++)
                {
                    UldTextNodeFont cleanNode = cleanFonts[i];
                    UldTextNodeFont patchedNode;
                    if (!patchedByOffset.TryGetValue(cleanNode.NodeOffset, out patchedNode))
                    {
                        Fail("{0} missing patched text node at 0x{1:X}", uldPath, cleanNode.NodeOffset);
                        continue;
                    }

                    if (patchedNode.FontId != cleanNode.FontId || patchedNode.FontSize != cleanNode.FontSize)
                    {
                        Fail(
                            "{0} node 0x{1:X} font changed: clean={2}/{3}, patched={4}/{5}",
                            uldPath,
                            cleanNode.NodeOffset,
                            cleanNode.FontId,
                            cleanNode.FontSize,
                            patchedNode.FontId,
                            patchedNode.FontSize);
                        continue;
                    }

                    string resolvedFont = ResolveUldFontPath(patchedNode.FontId, patchedNode.FontSize, true);
                    if (resolvedFont == null)
                    {
                        Fail(
                            "{0} node 0x{1:X} font {2}/{3} has no verifier font mapping",
                            uldPath,
                            cleanNode.NodeOffset,
                            patchedNode.FontId,
                            patchedNode.FontSize);
                        continue;
                    }

                    routedFontPaths.Add(resolvedFont);
                    Pass(
                        "{0} node 0x{1:X} preserves font {2}/{3} routes to {4}",
                        uldPath,
                        cleanNode.NodeOffset,
                        patchedNode.FontId,
                        patchedNode.FontSize,
                        resolvedFont);
                }

                if (routedFontPaths.Count == 0)
                {
                    Fail("{0} did not route any {1} node to a verifiable font", uldPath, label);
                    return;
                }

                foreach (string fontPath in routedFontPaths)
                {
                    VerifyLabelGlyphsEqualClean(fontPath, DataCenterWorldmapLabels);
                    DumpLabelPreview(dumpGroup, fontPath, DataCenterWorldmapLabels);
                }
            }
        }
    }
}
