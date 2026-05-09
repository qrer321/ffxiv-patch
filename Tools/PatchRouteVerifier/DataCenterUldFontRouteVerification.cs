using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private bool TryCollectPreservedUldFontRoutes(string uldPath, string label, out HashSet<string> routedFontPaths)
            {
                return TryCollectPreservedUldFontRoutes(uldPath, label, true, out routedFontPaths);
            }

            private bool TryCollectPreservedUldFontRoutes(string uldPath, string label, bool lobby, out HashSet<string> routedFontPaths)
            {
                byte[] cleanUld = _cleanUi.ReadFile(uldPath);
                byte[] patchedUld = _patchedUi.ReadFile(uldPath);
                return TryCollectPreservedUldFontRoutes(uldPath, label, lobby, cleanUld, patchedUld, out routedFontPaths);
            }

            private bool TryCollectOptionalPreservedUldFontRoutes(string uldPath, string label, bool lobby, out HashSet<string> routedFontPaths)
            {
                routedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                byte[] cleanPacked;
                byte[] patchedPacked;
                if (!_cleanUi.TryReadPackedFile(uldPath, out cleanPacked) ||
                    !_patchedUi.TryReadPackedFile(uldPath, out patchedPacked))
                {
                    return false;
                }

                byte[] cleanUld = SqPackArchive.UnpackStandardFile(cleanPacked);
                byte[] patchedUld = SqPackArchive.UnpackStandardFile(patchedPacked);
                return TryCollectPreservedUldFontRoutes(uldPath, label, lobby, cleanUld, patchedUld, out routedFontPaths);
            }

            private bool TryCollectPreservedUldFontRoutes(
                string uldPath,
                string label,
                bool lobby,
                byte[] cleanUld,
                byte[] patchedUld,
                out HashSet<string> routedFontPaths)
            {
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
                CollectPreservedUldFontRoutes(uldPath, lobby, cleanFonts, patchedByOffset, routedFontPaths);
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
                bool lobby,
                List<UldTextNodeFont> cleanFonts,
                Dictionary<int, UldTextNodeFont> patchedByOffset,
                HashSet<string> routedFontPaths)
            {
                for (int i = 0; i < cleanFonts.Count; i++)
                {
                    AddPreservedUldFontRoute(uldPath, lobby, cleanFonts[i], patchedByOffset, routedFontPaths);
                }
            }

            private void AddPreservedUldFontRoute(
                string uldPath,
                bool lobby,
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

                string resolvedFont = ResolveUldFontPath(patchedNode.FontId, patchedNode.FontSize, lobby);
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
