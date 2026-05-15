using System;
using System.Collections.Generic;
using System.IO;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyAppliedOutputFiles()
            {
                Console.WriteLine("[APPLIED] Generated output file match");
                if (!_compareAppliedOutput)
                {
                    Pass("applied output file check skipped; pass --applied-game to compare an installed game folder");
                    return;
                }

                VerifyAppliedArchiveFiles("font", _generatedFont, _patchedFont, CollectAppliedFontPaths(), false);
                VerifyAppliedArchiveFiles("font", _generatedFont, _patchedFont, CollectAppliedFontTexturePaths(), true);
                VerifyAppliedArchiveFiles("ui", _generatedUi, _patchedUi, CollectAppliedRequiredUiPaths(), false);
                VerifyAppliedArchiveFiles("ui", _generatedUi, _patchedUi, CollectAppliedOptionalUiPaths(), true);
            }

            private void VerifyAppliedLobbyRoutes()
            {
                Console.WriteLine("[APPLIED] Lobby route match");
                if (!_compareAppliedOutput)
                {
                    Pass("applied lobby route check skipped; pass --applied-game to compare an installed game folder");
                    return;
                }

                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);
                string routeReportPath = Path.Combine(reportDir, "applied-lobby-route-comparison.tsv");
                string payloadReportPath = Path.Combine(reportDir, "applied-lobby-payload-comparison.tsv");
                AppliedLobbyRouteStats stats = new AppliedLobbyRouteStats();
                HashSet<string> routedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (StreamWriter routeWriter = CreateUtf8Writer(routeReportPath))
                {
                    routeWriter.WriteLine("screen\tuld\tnode_offset\tgenerated_present\tapplied_present\tuld_payload_match\tgenerated_font_id\tgenerated_font_size\tgenerated_font_path\tapplied_font_id\tapplied_font_size\tapplied_font_path\troute_match\trender_state_match\tfont_payload_match\tfont_payload_difference");
                    WriteAppliedLobbyRouteRows(
                        routeWriter,
                        stats,
                        routedFonts,
                        "data-center-select",
                        new UldRouteCandidate[]
                        {
                            new UldRouteCandidate(DataCenterTitleUldPath, true),
                            new UldRouteCandidate(DataCenterWorldmapUldPath, true)
                        });
                    WriteAppliedLobbyRouteRows(routeWriter, stats, routedFonts, "start-system-settings", StartScreenSystemSettingsUldCandidates);
                    WriteAppliedLobbyRouteRows(routeWriter, stats, routedFonts, "start-main-menu", StartScreenMainMenuUldCandidates);
                    WriteAppliedLobbyRouteRows(routeWriter, stats, routedFonts, "character-select", CharacterSelectLobbyUldCandidates);
                }

                using (StreamWriter payloadWriter = CreateUtf8Writer(payloadReportPath))
                {
                    payloadWriter.WriteLine("category\tpath\tgenerated_present\tapplied_present\tmatch\tgenerated_bytes\tapplied_bytes\tdifference");
                    stats.PayloadMismatches += WriteAppliedPayloadRows(
                        payloadWriter,
                        "routed-font",
                        _generatedFont,
                        _patchedFont,
                        ToSortedArray(routedFonts));
                    stats.PayloadMismatches += WriteAppliedPayloadRows(
                        payloadWriter,
                        "required-font",
                        _generatedFont,
                        _patchedFont,
                        CollectAppliedFontPaths());
                    stats.PayloadMismatches += WriteAppliedPayloadRows(
                        payloadWriter,
                        "font-texture",
                        _generatedFont,
                        _patchedFont,
                        CollectAppliedFontTexturePaths());
                    stats.PayloadMismatches += WriteAppliedPayloadRows(
                        payloadWriter,
                        "required-ui",
                        _generatedUi,
                        _patchedUi,
                        CollectAppliedRequiredUiPaths());
                    stats.PayloadMismatches += WriteAppliedPayloadRows(
                        payloadWriter,
                        "optional-ui",
                        _generatedUi,
                        _patchedUi,
                        CollectAppliedOptionalUiPaths());
                }

                int mismatches = stats.UldPayloadMismatches +
                    stats.NodeRouteMismatches +
                    stats.NodeRenderStateMismatches +
                    stats.PayloadMismatches;
                if (mismatches == 0)
                {
                    Pass(
                        "applied lobby routes match generated output: ulds={0}, nodes={1}, routed_fonts={2}",
                        stats.UldCandidates,
                        stats.NodeRows,
                        routedFonts.Count);
                    return;
                }

                Fail(
                    "applied lobby route mismatch: uld_payload={0}, node_routes={1}, node_state={2}, payloads={3}; see {4} and {5}",
                    stats.UldPayloadMismatches,
                    stats.NodeRouteMismatches,
                    stats.NodeRenderStateMismatches,
                    stats.PayloadMismatches,
                    routeReportPath,
                    payloadReportPath);
            }

            private void WriteAppliedLobbyRouteRows(
                StreamWriter writer,
                AppliedLobbyRouteStats stats,
                HashSet<string> routedFonts,
                string screen,
                UldRouteCandidate[] candidates)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    stats.UldCandidates++;
                    UldRouteCandidate candidate = candidates[i];
                    byte[] generatedPacked;
                    byte[] appliedPacked;
                    bool generatedPresent = _generatedUi.TryReadPackedFile(candidate.Path, out generatedPacked);
                    bool appliedPresent = _patchedUi.TryReadPackedFile(candidate.Path, out appliedPacked);
                    bool uldPayloadMatch = generatedPresent && appliedPresent && BytesEqual(generatedPacked, appliedPacked);
                    if (generatedPresent != appliedPresent || (generatedPresent && appliedPresent && !uldPayloadMatch))
                    {
                        stats.UldPayloadMismatches++;
                    }

                    Dictionary<int, UldTextNodeFont> generatedNodes = generatedPresent
                        ? TryGetAppliedLobbyUldNodes(generatedPacked)
                        : new Dictionary<int, UldTextNodeFont>();
                    Dictionary<int, UldTextNodeFont> appliedNodes = appliedPresent
                        ? TryGetAppliedLobbyUldNodes(appliedPacked)
                        : new Dictionary<int, UldTextNodeFont>();
                    HashSet<int> nodeOffsets = new HashSet<int>();
                    foreach (int offset in generatedNodes.Keys)
                    {
                        nodeOffsets.Add(offset);
                    }

                    foreach (int offset in appliedNodes.Keys)
                    {
                        nodeOffsets.Add(offset);
                    }

                    List<int> sortedOffsets = new List<int>(nodeOffsets);
                    sortedOffsets.Sort();
                    if (sortedOffsets.Count == 0)
                    {
                        WriteAppliedLobbyRouteRow(
                            writer,
                            screen,
                            candidate.Path,
                            string.Empty,
                            generatedPresent,
                            appliedPresent,
                            uldPayloadMatch,
                            null,
                            null,
                            candidate.UsesLobbyFonts,
                            routedFonts,
                            stats);
                        continue;
                    }

                    for (int offsetIndex = 0; offsetIndex < sortedOffsets.Count; offsetIndex++)
                    {
                        int offset = sortedOffsets[offsetIndex];
                        UldTextNodeFont generatedNode;
                        UldTextNodeFont appliedNode;
                        bool hasGeneratedNode = generatedNodes.TryGetValue(offset, out generatedNode);
                        bool hasAppliedNode = appliedNodes.TryGetValue(offset, out appliedNode);
                        WriteAppliedLobbyRouteRow(
                            writer,
                            screen,
                            candidate.Path,
                            "0x" + offset.ToString("X"),
                            generatedPresent,
                            appliedPresent,
                            uldPayloadMatch,
                            hasGeneratedNode ? (UldTextNodeFont?)generatedNode : null,
                            hasAppliedNode ? (UldTextNodeFont?)appliedNode : null,
                            candidate.UsesLobbyFonts,
                            routedFonts,
                            stats);
                    }
                }
            }

            private Dictionary<int, UldTextNodeFont> TryGetAppliedLobbyUldNodes(byte[] packed)
            {
                try
                {
                    return GetUldTextNodeFontsByOffset(SqPackArchive.UnpackStandardFile(packed));
                }
                catch
                {
                    return new Dictionary<int, UldTextNodeFont>();
                }
            }

            private void WriteAppliedLobbyRouteRow(
                StreamWriter writer,
                string screen,
                string uldPath,
                string nodeOffset,
                bool generatedPresent,
                bool appliedPresent,
                bool uldPayloadMatch,
                UldTextNodeFont? generatedNode,
                UldTextNodeFont? appliedNode,
                bool usesLobbyFonts,
                HashSet<string> routedFonts,
                AppliedLobbyRouteStats stats)
            {
                stats.NodeRows++;
                string generatedFontPath = generatedNode.HasValue
                    ? ResolveUldFontPath(generatedNode.Value.FontId, generatedNode.Value.FontSize, usesLobbyFonts) ?? "unmapped"
                    : string.Empty;
                string appliedFontPath = appliedNode.HasValue
                    ? ResolveUldFontPath(appliedNode.Value.FontId, appliedNode.Value.FontSize, usesLobbyFonts) ?? "unmapped"
                    : string.Empty;

                if (!string.IsNullOrEmpty(generatedFontPath) && !string.Equals(generatedFontPath, "unmapped", StringComparison.OrdinalIgnoreCase))
                {
                    routedFonts.Add(generatedFontPath);
                }

                if (!string.IsNullOrEmpty(appliedFontPath) && !string.Equals(appliedFontPath, "unmapped", StringComparison.OrdinalIgnoreCase))
                {
                    routedFonts.Add(appliedFontPath);
                }

                bool routeMatch = generatedNode.HasValue &&
                    appliedNode.HasValue &&
                    generatedNode.Value.FontId == appliedNode.Value.FontId &&
                    generatedNode.Value.FontSize == appliedNode.Value.FontSize &&
                    string.Equals(generatedFontPath, appliedFontPath, StringComparison.OrdinalIgnoreCase);
                bool renderStateMatch = generatedNode.HasValue &&
                    appliedNode.HasValue &&
                    generatedNode.Value.NodeSize == appliedNode.Value.NodeSize &&
                    BytesEqual(generatedNode.Value.HeaderBytes, appliedNode.Value.HeaderBytes) &&
                    BytesEqual(generatedNode.Value.TextExtraBytes, appliedNode.Value.TextExtraBytes);
                bool bothNodesAbsent = !generatedNode.HasValue && !appliedNode.HasValue;

                if (!bothNodesAbsent && !routeMatch)
                {
                    stats.NodeRouteMismatches++;
                }

                if (!bothNodesAbsent && !renderStateMatch)
                {
                    stats.NodeRenderStateMismatches++;
                }

                string fontPayloadMatch = "n/a";
                string fontPayloadDifference = string.Empty;
                if (routeMatch && !string.IsNullOrEmpty(generatedFontPath) && !string.Equals(generatedFontPath, "unmapped", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] generatedFont;
                    byte[] appliedFont;
                    bool generatedFontPresent = _generatedFont.TryReadPackedFile(generatedFontPath, out generatedFont);
                    bool appliedFontPresent = _patchedFont.TryReadPackedFile(generatedFontPath, out appliedFont);
                    bool match = generatedFontPresent && appliedFontPresent && BytesEqual(generatedFont, appliedFont);
                    fontPayloadMatch = match ? "yes" : "no";
                    fontPayloadDifference = FormatAppliedPayloadDifference(generatedFontPresent, generatedFont, appliedFontPresent, appliedFont);
                }

                WriteTsvRow(
                    writer,
                    screen,
                    uldPath,
                    nodeOffset,
                    generatedPresent ? "yes" : "no",
                    appliedPresent ? "yes" : "no",
                    uldPayloadMatch ? "yes" : "no",
                    generatedNode.HasValue ? generatedNode.Value.FontId.ToString() : string.Empty,
                    generatedNode.HasValue ? generatedNode.Value.FontSize.ToString() : string.Empty,
                    generatedFontPath,
                    appliedNode.HasValue ? appliedNode.Value.FontId.ToString() : string.Empty,
                    appliedNode.HasValue ? appliedNode.Value.FontSize.ToString() : string.Empty,
                    appliedFontPath,
                    bothNodesAbsent ? "n/a" : routeMatch ? "yes" : "no",
                    bothNodesAbsent ? "n/a" : renderStateMatch ? "yes" : "no",
                    fontPayloadMatch,
                    fontPayloadDifference);
            }

            private int WriteAppliedPayloadRows(
                StreamWriter writer,
                string category,
                CompositeArchive generatedArchive,
                CompositeArchive appliedArchive,
                string[] paths)
            {
                int mismatches = 0;
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    if (string.IsNullOrEmpty(path) || !seen.Add(path))
                    {
                        continue;
                    }

                    byte[] generated;
                    byte[] applied;
                    bool generatedPresent = generatedArchive.TryReadPackedFile(path, out generated);
                    bool appliedPresent = appliedArchive.TryReadPackedFile(path, out applied);
                    bool match = generatedPresent && appliedPresent && BytesEqual(generated, applied);
                    if (generatedPresent != appliedPresent || (generatedPresent && appliedPresent && !match))
                    {
                        mismatches++;
                    }

                    WriteTsvRow(
                        writer,
                        category,
                        path,
                        generatedPresent ? "yes" : "no",
                        appliedPresent ? "yes" : "no",
                        match ? "yes" : "no",
                        generatedPresent ? generated.Length.ToString() : string.Empty,
                        appliedPresent ? applied.Length.ToString() : string.Empty,
                        FormatAppliedPayloadDifference(generatedPresent, generated, appliedPresent, applied));
                }

                return mismatches;
            }

            private static string FormatAppliedPayloadDifference(
                bool generatedPresent,
                byte[] generated,
                bool appliedPresent,
                byte[] applied)
            {
                if (!generatedPresent && !appliedPresent)
                {
                    return "both-missing";
                }

                if (!generatedPresent)
                {
                    return "generated-missing";
                }

                if (!appliedPresent)
                {
                    return "applied-missing";
                }

                if (BytesEqual(generated, applied))
                {
                    return "none";
                }

                return FormatGeneratedAppliedByteDifference(generated, applied);
            }

            private void VerifyAppliedArchiveFiles(
                string archiveName,
                CompositeArchive generatedArchive,
                CompositeArchive appliedArchive,
                string[] paths,
                bool optional)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    VerifyAppliedArchiveFile(archiveName, generatedArchive, appliedArchive, paths[i], optional);
                }
            }

            private void VerifyAppliedArchiveFile(
                string archiveName,
                CompositeArchive generatedArchive,
                CompositeArchive appliedArchive,
                string path,
                bool optional)
            {
                byte[] generated;
                if (!generatedArchive.TryReadPackedFile(path, out generated))
                {
                    if (optional)
                    {
                        return;
                    }

                    Fail("{0} generated output is missing {1}", archiveName, path);
                    return;
                }

                byte[] applied;
                if (!appliedArchive.TryReadPackedFile(path, out applied))
                {
                    Fail("{0} applied game is missing {1}", archiveName, path);
                    return;
                }

                if (BytesEqual(generated, applied))
                {
                    Pass("{0} applied file matches generated output: {1}, bytes={2}", archiveName, path, generated.Length);
                    return;
                }

                Fail(
                    "{0} applied file differs from generated output: {1}, {2}",
                    archiveName,
                    path,
                    FormatGeneratedAppliedByteDifference(generated, applied));
            }

            private static string[] CollectAppliedFontPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddAppliedValues(paths, LobbyPhraseFontPaths);
                AddAppliedValues(paths, DialoguePhraseFontPaths);
                AddAppliedValues(paths, SystemSettingsScaledFonts);
                AddAppliedValues(paths, DataCenterTitleSameFontChecks);
                AddAppliedValues(paths, NumericGlyphSameFontChecks);
                AddAppliedValues(paths, ProtectedHangulFonts);
                AddAppliedValues(paths, PartyListSelfMarkerSameFontChecks);
                AddAppliedValues(paths, LobbyHangulVisibilityFonts);
                for (int i = 0; i < DataCenterTitleKoreanFontChecks.GetLength(0); i++)
                {
                    paths.Add(DataCenterTitleKoreanFontChecks[i, 0]);
                    paths.Add(DataCenterTitleKoreanFontChecks[i, 1]);
                }

                for (int i = 0; i < NumericGlyphKoreanFontChecks.GetLength(0); i++)
                {
                    paths.Add(NumericGlyphKoreanFontChecks[i, 0]);
                    paths.Add(NumericGlyphKoreanFontChecks[i, 1]);
                }

                for (int i = 0; i < PartyListSelfMarkerKoreanFontChecks.GetLength(0); i++)
                {
                    paths.Add(PartyListSelfMarkerKoreanFontChecks[i, 0]);
                    paths.Add(PartyListSelfMarkerKoreanFontChecks[i, 1]);
                }

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    paths.Add(Derived4kLobbyFontPairs[i, 0]);
                    paths.Add(Derived4kLobbyFontPairs[i, 1]);
                }

                return ToSortedArray(paths);
            }

            private static string[] CollectAppliedFontTexturePaths()
            {
                return new string[]
                {
                    Font1TexturePath,
                    Font2TexturePath,
                    Font3TexturePath,
                    Font4TexturePath,
                    Font5TexturePath,
                    Font6TexturePath,
                    Font7TexturePath,
                    FontLobby1TexturePath,
                    FontLobby2TexturePath,
                    FontLobby3TexturePath,
                    FontLobby4TexturePath,
                    FontLobby5TexturePath,
                    FontLobby6TexturePath,
                    FontLobby7TexturePath,
                    FontKrnTexturePath
                };
            }

            private static string[] CollectAppliedRequiredUiPaths()
            {
                return new string[]
                {
                    DataCenterTitleUldPath,
                    DataCenterWorldmapUldPath
                };
            }

            private static string[] CollectAppliedOptionalUiPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < StartScreenSystemSettingsUldCandidates.Length; i++)
                {
                    paths.Add(StartScreenSystemSettingsUldCandidates[i].Path);
                }

                for (int i = 0; i < StartScreenMainMenuUldCandidates.Length; i++)
                {
                    paths.Add(StartScreenMainMenuUldCandidates[i].Path);
                }

                for (int i = 0; i < CharacterSelectLobbyUldCandidates.Length; i++)
                {
                    paths.Add(CharacterSelectLobbyUldCandidates[i].Path);
                }

                return ToSortedArray(paths);
            }

            private static void AddAppliedValues(HashSet<string> paths, string[] values)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    paths.Add(values[i]);
                }
            }

            private static string[] ToSortedArray(HashSet<string> values)
            {
                string[] result = new string[values.Count];
                values.CopyTo(result);
                Array.Sort(result, StringComparer.OrdinalIgnoreCase);
                return result;
            }

            private static string FormatGeneratedAppliedByteDifference(byte[] generated, byte[] applied)
            {
                int index = FindFirstByteDifference(generated, applied);
                if (index < 0)
                {
                    return "no byte difference";
                }

                string generatedValue = index < generated.Length ? "0x" + generated[index].ToString("X2") : "<missing>";
                string appliedValue = index < applied.Length ? "0x" + applied[index].ToString("X2") : "<missing>";
                return "relative=0x" + index.ToString("X") + ", generated=" + generatedValue + ", applied=" + appliedValue;
            }

            private sealed class AppliedLobbyRouteStats
            {
                public int UldCandidates;
                public int NodeRows;
                public int UldPayloadMismatches;
                public int NodeRouteMismatches;
                public int NodeRenderStateMismatches;
                public int PayloadMismatches;
            }
        }
    }
}
