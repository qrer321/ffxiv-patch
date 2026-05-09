using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private bool TryCollectPreservedUldFontRoutes(string uldPath, string label, out HashSet<string> routedFontPaths)
            {
                byte[] cleanUld = _cleanUi.ReadFile(uldPath);
                byte[] patchedUld = _patchedUi.ReadFile(uldPath);
                List<UldTextNodeFont> cleanFonts = GetUldTextNodeFonts(cleanUld);
                List<UldTextNodeFont> patchedFonts = GetUldTextNodeFonts(patchedUld);
                Dictionary<int, UldTextNodeFont> patchedByOffset = GetUldTextNodeFontsByOffset(patchedUld);
                routedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (cleanFonts.Count == 0)
                {
                    Fail("{0} clean ULD did not expose text-node fonts", uldPath);
                    return false;
                }

                VerifyUldTextNodeCount(uldPath, cleanFonts, patchedFonts);
                CollectPreservedUldFontRoutes(uldPath, cleanFonts, patchedByOffset, routedFontPaths);
                if (routedFontPaths.Count == 0)
                {
                    Fail("{0} did not route any {1} node to a verifiable font", uldPath, label);
                    return false;
                }

                return true;
            }

            private void VerifyUldTextNodeCount(string uldPath, List<UldTextNodeFont> cleanFonts, List<UldTextNodeFont> patchedFonts)
            {
                if (cleanFonts.Count == patchedFonts.Count)
                {
                    return;
                }

                Fail(
                    "{0} text-node count changed: clean={1}, patched={2}",
                    uldPath,
                    cleanFonts.Count,
                    patchedFonts.Count);
            }

            private void CollectPreservedUldFontRoutes(
                string uldPath,
                List<UldTextNodeFont> cleanFonts,
                Dictionary<int, UldTextNodeFont> patchedByOffset,
                HashSet<string> routedFontPaths)
            {
                for (int i = 0; i < cleanFonts.Count; i++)
                {
                    AddPreservedUldFontRoute(uldPath, cleanFonts[i], patchedByOffset, routedFontPaths);
                }
            }

            private void AddPreservedUldFontRoute(
                string uldPath,
                UldTextNodeFont cleanNode,
                Dictionary<int, UldTextNodeFont> patchedByOffset,
                HashSet<string> routedFontPaths)
            {
                UldTextNodeFont patchedNode;
                if (!patchedByOffset.TryGetValue(cleanNode.NodeOffset, out patchedNode))
                {
                    Fail("{0} missing patched text node at 0x{1:X}", uldPath, cleanNode.NodeOffset);
                    return;
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
                    return;
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
                    return;
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
        }
    }
}
