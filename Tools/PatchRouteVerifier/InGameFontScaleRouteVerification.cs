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
            private const string PartyMemberListUldPath = PartyBonusRoleFontPatch.UldPath;
            private const uint PartyBonusComponentId = PartyBonusRoleFontPatch.ComponentId;
            private const uint PartyBonusTextNodeId = PartyBonusRoleFontPatch.TextNodeId;
            private const int PartyBonusComponentInstances = PartyBonusRoleFontPatch.ComponentInstanceCount;
            private const uint DutyFinderWidgetId = DutyFinderRoleFontPatch.WidgetId;
            private const uint DutyFinderRoleHeadingNodeId = DutyFinderRoleFontPatch.RoleHeadingNodeId;
            private const uint DutyFinderRoleValueNodeId = DutyFinderRoleFontPatch.RoleValueNodeId;
            private const uint DutyFinderJobNameNodeId = DutyFinderRoleFontPatch.JobNameNodeId;
            private const int ScaleRouteFailureLimit = 16;

            private static readonly int[] InGameUiScalePercents = new int[] { 100, 150, 200, 300 };
            private static readonly string[] DutyFinderJobNamePhrases = new string[]
            {
                "\uAC74\uBE0C\uB808\uC774\uCEE4",
                "\uC554\uD751\uAE30\uC0AC",
                "\uD53D\uD1A0\uB9E8\uC11C"
            };

            private void VerifyInGameFontScaleRoutes()
            {
                Console.WriteLine("[ULD/FDT] In-game UI-scale font routes");
                if (_ttmpFont == null)
                {
                    Fail("In-game UI-scale font routes require the TTMP source font package");
                    return;
                }

                string reportDir = ResolveInGameFontRiskReportDir();
                Directory.CreateDirectory(reportDir);
                string reportPath = Path.Combine(reportDir, "ingame-uld-font-scale-routes.tsv");
                int presentUlds = 0;
                int textNodes = 0;
                int unresolvedRoutes = 0;
                int visualScaleCoverageGaps = 0;

                using (StreamWriter writer = CreateUtf8Writer(reportPath))
                {
                    writer.WriteLine("area\tuld\tnode_offset\tcontainer_type\tcontainer_id\tnode_id\tfont_id\tfont_size\tfont_100\tfont_150\tfont_200\tfont_300\tstatus");
                    for (int candidateIndex = 0; candidateIndex < InGameFontRiskUldCandidates.Length; candidateIndex++)
                    {
                        InGameUldCandidate candidate = InGameFontRiskUldCandidates[candidateIndex];
                        byte[] patchedPacked;
                        if (!_patchedUi.TryReadPackedFile(candidate.Path, out patchedPacked))
                        {
                            if (!candidate.Optional)
                            {
                                Fail("{0} is missing from the patched UI archive", candidate.Path);
                            }

                            continue;
                        }

                        byte[] patchedUld = SqPackArchive.UnpackStandardFile(patchedPacked);
                        List<UldTextNodeFont> nodes = GetUldTextNodeFonts(patchedUld);
                        presentUlds++;
                        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                        {
                            UldTextNodeFont node = nodes[nodeIndex];
                            string[] routes = new string[InGameUiScalePercents.Length];
                            bool unresolved = false;
                            bool coverageGap = false;
                            for (int scaleIndex = 0; scaleIndex < InGameUiScalePercents.Length; scaleIndex++)
                            {
                                routes[scaleIndex] = ResolveUldFontPathAtScale(
                                    node.FontId,
                                    node.FontSize,
                                    InGameUiScalePercents[scaleIndex],
                                    false);
                                if (routes[scaleIndex] == null)
                                {
                                    routes[scaleIndex] = "unmapped";
                                    unresolved = true;
                                }
                                else if (node.FontId == UldTrumpGothicFontId &&
                                         !ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(routes[scaleIndex]))
                                {
                                    coverageGap = true;
                                }
                            }

                            if (unresolved)
                            {
                                unresolvedRoutes++;
                            }

                            if (coverageGap)
                            {
                                visualScaleCoverageGaps++;
                            }

                            WriteTsvRow(
                                writer,
                                candidate.Area,
                                candidate.Path,
                                "0x" + node.NodeOffset.ToString("X"),
                                node.ContainerType,
                                node.ContainerId.ToString(),
                                node.NodeId.ToString(),
                                node.FontId.ToString(),
                                node.FontSize.ToString(),
                                routes[0],
                                routes[1],
                                routes[2],
                                routes[3],
                                unresolved ? "unmapped" : coverageGap ? "visual-scale-coverage-gap" : "ok");
                            textNodes++;
                        }
                    }
                }

                HashSet<uint> visualScaleCodepoints = CollectActionDetailHighScaleHangulCodepointSet();
                int coveredGlyphs = VerifyLargeUiVisualScaleTierCoverage(visualScaleCodepoints);
                VerifyDutyFinderRoleFontScale();
                if (unresolvedRoutes > 0)
                {
                    Fail("In-game UI-scale route model has {0} unmapped text nodes; see {1}", unresolvedRoutes, reportPath);
                }

                if (visualScaleCoverageGaps > 0)
                {
                    Fail("TrumpGothic UI-scale routing has {0} uncorrected tier selections; see {1}", visualScaleCoverageGaps, reportPath);
                }

                if (unresolvedRoutes == 0 && visualScaleCoverageGaps == 0)
                {
                    Pass(
                        "in-game UI-scale routes resolved without coverage gaps: ulds={0}, nodes={1}, scaled_glyphs={2}, report={3}",
                        presentUlds,
                        textNodes,
                        coveredGlyphs,
                        reportPath);
                }
            }

            private int VerifyLargeUiVisualScaleTierCoverage(HashSet<uint> codepoints)
            {
                if (codepoints == null || codepoints.Count == 0)
                {
                    Fail("No large UI visual-scale Hangul codepoints were collected");
                    return 0;
                }

                int covered = 0;
                for (int fontIndex = 0; fontIndex < ActionDetailHighScaleHangulGlyphs.VisualScaleTargetFontPaths.Length; fontIndex++)
                {
                    string fontPath = ActionDetailHighScaleHangulGlyphs.VisualScaleTargetFontPaths[fontIndex];
                    byte[] sourceFdt;
                    byte[] targetFdt;
                    try
                    {
                        sourceFdt = _ttmpFont.ReadFile(fontPath);
                        targetFdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} visual-scale tier read error: {1}", fontPath, ex.Message);
                        continue;
                    }

                    int fontCovered = 0;
                    int failures = 0;
                    foreach (uint codepoint in codepoints)
                    {
                        FdtGlyphEntry sourceGlyph;
                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph) ||
                            !TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            if (failures < ScaleRouteFailureLimit)
                            {
                                Fail("{0} visual-scale tier is missing U+{1:X4}", fontPath, codepoint);
                            }

                            failures++;
                            continue;
                        }

                        bool routeChanged = sourceGlyph.ImageIndex != targetGlyph.ImageIndex ||
                                            sourceGlyph.X != targetGlyph.X ||
                                            sourceGlyph.Y != targetGlyph.Y;
                        bool metricsChanged = !GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph);
                        if (!routeChanged || !metricsChanged)
                        {
                            if (failures < ScaleRouteFailureLimit)
                            {
                                Fail(
                                    "{0} U+{1:X4} visual-scale tier is only partially transformed: route_changed={2}, metrics_changed={3}, source={4}, target={5}",
                                    fontPath,
                                    codepoint,
                                    routeChanged,
                                    metricsChanged,
                                    FormatGlyphRoute(sourceGlyph),
                                    FormatGlyphRoute(targetGlyph));
                            }

                            failures++;
                            continue;
                        }

                        fontCovered++;
                    }

                    if (fontCovered != codepoints.Count)
                    {
                        Fail(
                            "{0} visual-scale tier coverage incomplete: covered={1}/{2}, failures={3}",
                            fontPath,
                            fontCovered,
                            codepoints.Count,
                            failures);
                    }
                    else
                    {
                        Pass("{0} visual-scale tier is fully transformed: glyphs={1}", fontPath, fontCovered);
                    }

                    covered += fontCovered;
                }

                return covered;
            }

            private void VerifyDutyFinderRoleFontScale()
            {
                Console.WriteLine("[ULD/FDT] Duty Finder role tabs");
                ExpectText("Addon", 2503, ActionDetailHighScaleHangulGlyphs.DutyFinderRoleHeadingPhrase);
                ExpectText("Addon", 2784, ActionDetailHighScaleHangulGlyphs.DutyFinderDefenseRolePhrase);
                ExpectText("Addon", 2785, ActionDetailHighScaleHangulGlyphs.DutyFinderHealingRolePhrase);
                ExpectText("Addon", 2786, ActionDetailHighScaleHangulGlyphs.DutyFinderAttackRolePhrase);
                ExpectText("ClassJob", 32, DutyFinderJobNamePhrases[1]);
                ExpectText("ClassJob", 37, DutyFinderJobNamePhrases[0]);
                ExpectText("ClassJob", 42, DutyFinderJobNamePhrases[2]);

                for (int pathIndex = 0; pathIndex < DutyFinderRoleFontPatch.UldPaths.Length; pathIndex++)
                {
                    VerifyDutyFinderRoleFontScale(DutyFinderRoleFontPatch.UldPaths[pathIndex]);
                }
            }

            private void VerifyDutyFinderRoleFontScale(string uldPath)
            {
                byte[] cleanUld;
                byte[] patchedUld;
                try
                {
                    cleanUld = _cleanUi.ReadFile(uldPath);
                    patchedUld = _patchedUi.ReadFile(uldPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} Duty Finder role-tab read error: {1}", uldPath, ex.Message);
                    return;
                }

                DutyFinderRoleFontPatch.DutyFinderRoleTextNodes cleanLocator;
                DutyFinderRoleFontPatch.DutyFinderRoleTextNodes patchedLocator;
                string locatorError;
                if (!DutyFinderRoleFontPatch.TryFindRoleTextNodes(cleanUld, out cleanLocator, out locatorError))
                {
                    Fail("{0} clean role-tab lookup failed: {1}", uldPath, locatorError);
                    return;
                }

                if (!DutyFinderRoleFontPatch.TryFindRoleTextNodes(patchedUld, out patchedLocator, out locatorError))
                {
                    Fail("{0} patched role-tab lookup failed: {1}", uldPath, locatorError);
                    return;
                }

                VerifyDutyFinderUldByteDelta(uldPath, cleanUld, patchedUld, cleanLocator, patchedLocator);

                List<UldTextNodeFont> cleanNodes = GetUldTextNodeFonts(cleanUld);
                List<UldTextNodeFont> patchedNodes = GetUldTextNodeFonts(patchedUld);
                UldTextNodeFont cleanHeading;
                UldTextNodeFont cleanRoleValue;
                UldTextNodeFont cleanJobName;
                UldTextNodeFont patchedHeading;
                UldTextNodeFont patchedRoleValue;
                UldTextNodeFont patchedJobName;
                if (!TryFindDutyFinderRoleNode(uldPath, cleanNodes, DutyFinderRoleHeadingNodeId, "clean heading", out cleanHeading) ||
                    !TryFindDutyFinderRoleNode(uldPath, cleanNodes, DutyFinderRoleValueNodeId, "clean role value", out cleanRoleValue) ||
                    !TryFindDutyFinderRoleNode(uldPath, cleanNodes, DutyFinderJobNameNodeId, "clean job name", out cleanJobName) ||
                    !TryFindDutyFinderRoleNode(uldPath, patchedNodes, DutyFinderRoleHeadingNodeId, "patched heading", out patchedHeading) ||
                    !TryFindDutyFinderRoleNode(uldPath, patchedNodes, DutyFinderRoleValueNodeId, "patched role value", out patchedRoleValue) ||
                    !TryFindDutyFinderRoleNode(uldPath, patchedNodes, DutyFinderJobNameNodeId, "patched job name", out patchedJobName))
                {
                    return;
                }

                bool headingContract = VerifyDutyFinderRoleNodeContract(
                    uldPath,
                    cleanHeading,
                    DutyFinderRoleHeadingNodeId,
                    DutyFinderRoleFontPatch.RoleHeadingX,
                    DutyFinderRoleFontPatch.RoleHeadingWidth,
                    DutyFinderRoleFontPatch.RoleHeadingTextId,
                    DutyFinderRoleFontPatch.SourceFontId,
                    DutyFinderRoleFontPatch.SourceFontSize,
                    "clean heading");
                bool roleValueContract = VerifyDutyFinderRoleNodeContract(
                    uldPath,
                    cleanRoleValue,
                    DutyFinderRoleValueNodeId,
                    DutyFinderRoleFontPatch.RoleValueX,
                    DutyFinderRoleFontPatch.RoleValueWidth,
                    DutyFinderRoleFontPatch.RoleValueTextId,
                    DutyFinderRoleFontPatch.SourceFontId,
                    DutyFinderRoleFontPatch.SourceFontSize,
                    "clean role value");
                bool patchedHeadingContract = VerifyDutyFinderRoleNodeContract(
                    uldPath,
                    patchedHeading,
                    DutyFinderRoleHeadingNodeId,
                    DutyFinderRoleFontPatch.RoleHeadingX,
                    DutyFinderRoleFontPatch.RoleHeadingWidth,
                    DutyFinderRoleFontPatch.RoleHeadingTextId,
                    DutyFinderRoleFontPatch.TargetFontId,
                    DutyFinderRoleFontPatch.TargetFontSize,
                    "patched heading");
                bool patchedRoleValueContract = VerifyDutyFinderRoleNodeContract(
                    uldPath,
                    patchedRoleValue,
                    DutyFinderRoleValueNodeId,
                    DutyFinderRoleFontPatch.RoleValueX,
                    DutyFinderRoleFontPatch.RoleValueWidth,
                    DutyFinderRoleFontPatch.RoleValueTextId,
                    DutyFinderRoleFontPatch.TargetFontId,
                    DutyFinderRoleFontPatch.TargetFontSize,
                    "patched role value");
                bool cleanJobContract = VerifyDutyFinderJobNameNodeContract(uldPath, cleanJobName, "clean job name");
                bool patchedJobContract = VerifyDutyFinderJobNameNodeContract(uldPath, patchedJobName, "patched job name");
                if (!headingContract || !roleValueContract ||
                    !patchedHeadingContract || !patchedRoleValueContract ||
                    !cleanJobContract || !patchedJobContract)
                {
                    return;
                }

                string[] expectedRoleRoutes = new string[]
                {
                    "common/font/AXIS_12.fdt",
                    "common/font/AXIS_18.fdt",
                    "common/font/AXIS_18.fdt",
                    "common/font/AXIS_36.fdt"
                };
                string[] expectedJobRoutes = new string[]
                {
                    "common/font/Jupiter_23.fdt",
                    "common/font/Jupiter_46.fdt",
                    "common/font/Jupiter_46.fdt",
                    "common/font/Jupiter_46.fdt"
                };
                string[] headingPhrases = new string[]
                {
                    ActionDetailHighScaleHangulGlyphs.DutyFinderRoleHeadingPhrase
                };

                for (int scaleIndex = 0; scaleIndex < InGameUiScalePercents.Length; scaleIndex++)
                {
                    int scale = InGameUiScalePercents[scaleIndex];
                    string headingFontPath = ResolveUldFontPathAtScale(
                        patchedHeading.FontId,
                        patchedHeading.FontSize,
                        scale,
                        false);
                    string roleValueFontPath = ResolveUldFontPathAtScale(
                        patchedRoleValue.FontId,
                        patchedRoleValue.FontSize,
                        scale,
                        false);
                    string jobNameFontPath = ResolveUldFontPathAtScale(
                        patchedJobName.FontId,
                        patchedJobName.FontSize,
                        scale,
                        false);
                    if (!string.Equals(headingFontPath, expectedRoleRoutes[scaleIndex], StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(roleValueFontPath, expectedRoleRoutes[scaleIndex], StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(jobNameFontPath, expectedJobRoutes[scaleIndex], StringComparison.OrdinalIgnoreCase))
                    {
                        Fail(
                            "{0} Duty Finder tabs {1}% expected role={2}, job={3}; heading={4}, value={5}, jobName={6}",
                            uldPath,
                            scale,
                            expectedRoleRoutes[scaleIndex],
                            expectedJobRoutes[scaleIndex],
                            headingFontPath ?? "unmapped",
                            roleValueFontPath ?? "unmapped",
                            jobNameFontPath ?? "unmapped");
                        continue;
                    }

                    VerifyDutyFinderRolePhraseMetrics(
                        headingFontPath,
                        scale,
                        patchedHeading,
                        headingPhrases,
                        uldPath + " heading");
                    VerifyDutyFinderRolePhraseMetrics(
                        roleValueFontPath,
                        scale,
                        patchedRoleValue,
                        ActionDetailHighScaleHangulGlyphs.DutyFinderRolePhrases,
                        uldPath + " role value");
                    VerifyDutyFinderRolePhraseMetrics(
                        jobNameFontPath,
                        scale,
                        patchedJobName,
                        DutyFinderJobNamePhrases,
                        uldPath + " job name");
                    Pass(
                        "{0} Duty Finder tabs {1}% route role={2}/{3} to {4}, job={5}/{6} to {7}",
                        uldPath,
                        scale,
                        patchedHeading.FontId,
                        patchedHeading.FontSize,
                        headingFontPath,
                        patchedJobName.FontId,
                        patchedJobName.FontSize,
                        jobNameFontPath);
                }
            }

            private void VerifyDutyFinderUldByteDelta(
                string uldPath,
                byte[] cleanUld,
                byte[] patchedUld,
                DutyFinderRoleFontPatch.DutyFinderRoleTextNodes cleanLocator,
                DutyFinderRoleFontPatch.DutyFinderRoleTextNodes patchedLocator)
            {
                if (cleanUld.Length != patchedUld.Length ||
                    cleanLocator.RoleHeading.NodeOffset != patchedLocator.RoleHeading.NodeOffset ||
                    cleanLocator.RoleValue.NodeOffset != patchedLocator.RoleValue.NodeOffset ||
                    cleanLocator.JobName.NodeOffset != patchedLocator.JobName.NodeOffset)
                {
                    Fail(
                        "{0} Duty Finder role-tab ULD structure changed: cleanLength={1}, patchedLength={2}, heading=0x{3:X}/0x{4:X}, value=0x{5:X}/0x{6:X}",
                        uldPath,
                        cleanUld.Length,
                        patchedUld.Length,
                        cleanLocator.RoleHeading.NodeOffset,
                        patchedLocator.RoleHeading.NodeOffset,
                        cleanLocator.RoleValue.NodeOffset,
                        patchedLocator.RoleValue.NodeOffset);
                    return;
                }

                int differences = 0;
                int unexpectedDifferences = 0;
                for (int offset = 0; offset < cleanUld.Length; offset++)
                {
                    if (cleanUld[offset] == patchedUld[offset])
                    {
                        continue;
                    }

                    differences++;
                    if (offset != cleanLocator.RoleHeading.FontOffset &&
                        offset != cleanLocator.RoleHeading.FontSizeOffset &&
                        offset != cleanLocator.RoleValue.FontOffset &&
                        offset != cleanLocator.RoleValue.FontSizeOffset)
                    {
                        unexpectedDifferences++;
                    }
                }

                if (differences != 4 ||
                    unexpectedDifferences != 0 ||
                    patchedUld[cleanLocator.RoleHeading.FontOffset] != DutyFinderRoleFontPatch.TargetFontId ||
                    patchedUld[cleanLocator.RoleHeading.FontSizeOffset] != DutyFinderRoleFontPatch.TargetFontSize ||
                    patchedUld[cleanLocator.RoleValue.FontOffset] != DutyFinderRoleFontPatch.TargetFontId ||
                    patchedUld[cleanLocator.RoleValue.FontSizeOffset] != DutyFinderRoleFontPatch.TargetFontSize)
                {
                    Fail(
                        "{0} Duty Finder role-tab ULD expected only four font bytes to change: differences={1}, unexpected={2}, heading={3}/{4}, value={5}/{6}",
                        uldPath,
                        differences,
                        unexpectedDifferences,
                        patchedUld[cleanLocator.RoleHeading.FontOffset],
                        patchedUld[cleanLocator.RoleHeading.FontSizeOffset],
                        patchedUld[cleanLocator.RoleValue.FontOffset],
                        patchedUld[cleanLocator.RoleValue.FontSizeOffset]);
                    return;
                }

                Pass(
                    "{0} Duty Finder role-tab ULD changed only font bytes at 0x{1:X}/0x{2:X} and 0x{3:X}/0x{4:X}",
                    uldPath,
                    cleanLocator.RoleHeading.FontOffset,
                    cleanLocator.RoleHeading.FontSizeOffset,
                    cleanLocator.RoleValue.FontOffset,
                    cleanLocator.RoleValue.FontSizeOffset);
            }

            private bool TryFindDutyFinderRoleNode(
                string uldPath,
                List<UldTextNodeFont> nodes,
                uint nodeId,
                string label,
                out UldTextNodeFont result)
            {
                result = new UldTextNodeFont();
                int matches = 0;
                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                {
                    UldTextNodeFont node = nodes[nodeIndex];
                    if (string.Equals(node.ContainerType, "widget", StringComparison.Ordinal) &&
                        node.ContainerId == DutyFinderWidgetId &&
                        node.NodeId == nodeId)
                    {
                        result = node;
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    Fail(
                        "{0} expected one Duty Finder {1} node (widget={2}, node={3}), found={4}",
                        uldPath,
                        label,
                        DutyFinderWidgetId,
                        nodeId,
                        matches);
                    return false;
                }

                return true;
            }

            private bool VerifyDutyFinderRoleNodeContract(
                string uldPath,
                UldTextNodeFont node,
                uint expectedNodeId,
                short expectedX,
                ushort expectedWidth,
                uint expectedTextId,
                byte expectedFontId,
                byte expectedFontSize,
                string label)
            {
                if (node.NodeId != expectedNodeId ||
                    node.X != expectedX ||
                    node.Y != DutyFinderRoleFontPatch.TextY ||
                    node.Width != expectedWidth ||
                    node.Height != DutyFinderRoleFontPatch.TextHeight ||
                    node.TextId != expectedTextId ||
                    node.FontId != expectedFontId ||
                    node.FontSize != expectedFontSize ||
                    node.Alignment != DutyFinderRoleFontPatch.AlignmentLeft ||
                    node.TextFlags != 128 ||
                    node.SheetType != 0 ||
                    node.CharSpacing != 0 ||
                    node.LineSpacing != 0 ||
                    node.TextFlags2 != 6)
                {
                    Fail(
                        "{0} Duty Finder {1} contract changed: node={2}, rect={3},{4},{5},{6}, text={7}, font={8}/{9}, align={10}, flags={11}/{12}, sheet={13}, spacing={14}/{15}",
                        uldPath,
                        label,
                        node.NodeId,
                        node.X,
                        node.Y,
                        node.Width,
                        node.Height,
                        node.TextId,
                        node.FontId,
                        node.FontSize,
                        node.Alignment,
                        node.TextFlags,
                        node.TextFlags2,
                        node.SheetType,
                        node.CharSpacing,
                        node.LineSpacing);
                    return false;
                }

                Pass(
                    "{0} Duty Finder {1} node contract is stable: rect={2},{3},{4},{5}, font={6}/{7}",
                    uldPath,
                    label,
                    node.X,
                    node.Y,
                    node.Width,
                    node.Height,
                    node.FontId,
                    node.FontSize);
                return true;
            }

            private bool VerifyDutyFinderJobNameNodeContract(
                string uldPath,
                UldTextNodeFont node,
                string label)
            {
                if (node.NodeId != DutyFinderJobNameNodeId ||
                    node.X != DutyFinderRoleFontPatch.JobNameX ||
                    node.Y != DutyFinderRoleFontPatch.JobNameY ||
                    node.Width != DutyFinderRoleFontPatch.JobNameWidth ||
                    node.Height != DutyFinderRoleFontPatch.JobNameHeight ||
                    node.TextId != DutyFinderRoleFontPatch.JobNameTextId ||
                    node.FontId != DutyFinderRoleFontPatch.JobNameFontId ||
                    node.FontSize != DutyFinderRoleFontPatch.JobNameFontSize ||
                    node.Alignment != DutyFinderRoleFontPatch.AlignmentLeft ||
                    node.TextFlags != 128 ||
                    node.SheetType != 0 ||
                    node.CharSpacing != 0 ||
                    node.LineSpacing != 0 ||
                    node.TextFlags2 != 6)
                {
                    Fail(
                        "{0} Duty Finder {1} contract changed: node={2}, rect={3},{4},{5},{6}, text={7}, font={8}/{9}, align={10}, flags={11}/{12}, sheet={13}, spacing={14}/{15}",
                        uldPath,
                        label,
                        node.NodeId,
                        node.X,
                        node.Y,
                        node.Width,
                        node.Height,
                        node.TextId,
                        node.FontId,
                        node.FontSize,
                        node.Alignment,
                        node.TextFlags,
                        node.TextFlags2,
                        node.SheetType,
                        node.CharSpacing,
                        node.LineSpacing);
                    return false;
                }

                Pass(
                    "{0} Duty Finder {1} node contract is stable: rect={2},{3},{4},{5}, font={6}/{7}",
                    uldPath,
                    label,
                    node.X,
                    node.Y,
                    node.Width,
                    node.Height,
                    node.FontId,
                    node.FontSize);
                return true;
            }

            private void VerifyDutyFinderRolePhraseMetrics(
                string fontPath,
                int uiScalePercent,
                UldTextNodeFont node,
                string[] phrases,
                string slotLabel)
            {
                byte[] fdt;
                FdtFontMetrics fontMetrics;
                try
                {
                    fdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} Duty Finder role-tab font read error: {1}", fontPath, ex.Message);
                    return;
                }

                if (!TryReadFdtFontMetrics(fdt, out fontMetrics) || fontMetrics.Size <= 0d)
                {
                    Fail("{0} Duty Finder role-tab nominal font size could not be read", fontPath);
                    return;
                }

                double uiScale = uiScalePercent / 100d;
                double rasterScale = (node.FontSize * uiScale) / fontMetrics.Size;
                double availableWidth = node.Width * uiScale;
                double availableHeight = node.Height * uiScale;
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex];
                    PhraseVisualBounds bounds;
                    string error;
                    if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, phrase, true, out bounds, out error))
                    {
                        Fail(
                            "{0} Duty Finder {1} phrase [{2}] failed: {3}",
                            fontPath,
                            slotLabel,
                            Escape(phrase),
                            error);
                        continue;
                    }

                    double renderedWidth = Math.Max(bounds.Advance, bounds.Width) * rasterScale;
                    double renderedHeight = bounds.Height * rasterScale;
                    if (renderedWidth > availableWidth + 0.01d || renderedHeight > availableHeight + 0.01d)
                    {
                        Fail(
                            "{0} Duty Finder {1} {2}% phrase [{3}] exceeds its slot: rendered={4}x{5}, available={6}x{7}",
                            fontPath,
                            slotLabel,
                            uiScalePercent,
                            Escape(phrase),
                            FormatDouble(renderedWidth),
                            FormatDouble(renderedHeight),
                            FormatDouble(availableWidth),
                            FormatDouble(availableHeight));
                        continue;
                    }

                    Pass(
                        "{0} Duty Finder {1} {2}% phrase [{3}] rendered={4}x{5}, available={6}x{7}",
                        fontPath,
                        slotLabel,
                        uiScalePercent,
                        Escape(phrase),
                        FormatDouble(renderedWidth),
                        FormatDouble(renderedHeight),
                        FormatDouble(availableWidth),
                        FormatDouble(availableHeight));
                }
            }

            private void VerifyPartyBonusFontScale()
            {
                Console.WriteLine("[ULD/FDT] Party Bonus font scale");
                ExpectText("Addon", 1075, ActionDetailHighScaleHangulGlyphs.PartyBonusPhrase);
                ExpectText("Addon", 1619, ActionDetailHighScaleHangulGlyphs.DefensePhrase);
                ExpectText("Addon", 1620, ActionDetailHighScaleHangulGlyphs.HealingPhrase);
                ExpectText("Addon", 1621, ActionDetailHighScaleHangulGlyphs.MeleeAttackPhrase);
                ExpectText("Addon", 1622, ActionDetailHighScaleHangulGlyphs.RangedPhysicalAttackPhrase);
                ExpectText("Addon", 1623, ActionDetailHighScaleHangulGlyphs.RangedMagicAttackPhrase);

                byte[] cleanUld;
                byte[] patchedUld;
                try
                {
                    cleanUld = _cleanUi.ReadFile(PartyMemberListUldPath);
                    patchedUld = _patchedUi.ReadFile(PartyMemberListUldPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} Party Bonus route read error: {1}", PartyMemberListUldPath, ex.Message);
                    return;
                }

                PartyBonusRoleFontPatch.PartyBonusRoleTextNode cleanLocator;
                PartyBonusRoleFontPatch.PartyBonusRoleTextNode patchedLocator;
                string locatorError;
                if (!PartyBonusRoleFontPatch.TryFindRoleTextNode(cleanUld, out cleanLocator, out locatorError))
                {
                    Fail("{0} clean role-label lookup failed: {1}", PartyMemberListUldPath, locatorError);
                    return;
                }

                if (!PartyBonusRoleFontPatch.TryFindRoleTextNode(patchedUld, out patchedLocator, out locatorError))
                {
                    Fail("{0} patched role-label lookup failed: {1}", PartyMemberListUldPath, locatorError);
                    return;
                }

                List<UldTextNodeFont> cleanNodes = GetUldTextNodeFonts(cleanUld);
                List<UldTextNodeFont> patchedNodes = GetUldTextNodeFonts(patchedUld);
                UldTextNodeFont cleanRoleNode = new UldTextNodeFont();
                UldTextNodeFont patchedRoleNode = new UldTextNodeFont();
                int cleanRoleMatches = 0;
                int patchedRoleMatches = 0;
                for (int nodeIndex = 0; nodeIndex < cleanNodes.Count; nodeIndex++)
                {
                    UldTextNodeFont node = cleanNodes[nodeIndex];
                    if (string.Equals(node.ContainerType, "component", StringComparison.Ordinal) &&
                        node.ContainerId == PartyBonusComponentId &&
                        node.NodeId == PartyBonusTextNodeId)
                    {
                        cleanRoleNode = node;
                        cleanRoleMatches++;
                    }
                }

                for (int nodeIndex = 0; nodeIndex < patchedNodes.Count; nodeIndex++)
                {
                    UldTextNodeFont node = patchedNodes[nodeIndex];
                    if (string.Equals(node.ContainerType, "component", StringComparison.Ordinal) &&
                        node.ContainerId == PartyBonusComponentId &&
                        node.NodeId == PartyBonusTextNodeId)
                    {
                        patchedRoleNode = node;
                        patchedRoleMatches++;
                    }
                }

                if (cleanRoleMatches != 1 || patchedRoleMatches != 1)
                {
                    Fail(
                        "{0} expected one Party Bonus role text node in component {1}, clean={2}, patched={3}",
                        PartyMemberListUldPath,
                        PartyBonusComponentId,
                        cleanRoleMatches,
                        patchedRoleMatches);
                    return;
                }

                if (cleanRoleNode.FontId != PartyBonusRoleFontPatch.SourceFontId ||
                    cleanRoleNode.FontSize != PartyBonusRoleFontPatch.SourceFontSize ||
                    cleanRoleNode.X != PartyBonusRoleFontPatch.TextX ||
                    cleanRoleNode.Y != PartyBonusRoleFontPatch.TextY ||
                    cleanRoleNode.Width != PartyBonusRoleFontPatch.TextWidth ||
                    cleanRoleNode.Height != PartyBonusRoleFontPatch.TextHeight ||
                    cleanRoleNode.Alignment != PartyBonusRoleFontPatch.AlignmentLeft ||
                    cleanRoleNode.TextFlags != 0 ||
                    cleanRoleNode.CharSpacing != 0 ||
                    cleanRoleNode.LineSpacing != 0)
                {
                    Fail(
                        "{0} clean Party Bonus role contract changed: font={1}/{2}, rect={3},{4},{5},{6}, align={7}, flags={8}, spacing={9}/{10}",
                        PartyMemberListUldPath,
                        cleanRoleNode.FontId,
                        cleanRoleNode.FontSize,
                        cleanRoleNode.X,
                        cleanRoleNode.Y,
                        cleanRoleNode.Width,
                        cleanRoleNode.Height,
                        cleanRoleNode.Alignment,
                        cleanRoleNode.TextFlags,
                        cleanRoleNode.CharSpacing,
                        cleanRoleNode.LineSpacing);
                }

                if (patchedRoleNode.FontId != PartyBonusRoleFontPatch.TargetFontId ||
                    patchedRoleNode.FontSize != PartyBonusRoleFontPatch.TargetFontSize ||
                    patchedRoleNode.X != cleanRoleNode.X ||
                    patchedRoleNode.Y != cleanRoleNode.Y ||
                    patchedRoleNode.Width != cleanRoleNode.Width ||
                    patchedRoleNode.Height != cleanRoleNode.Height ||
                    patchedRoleNode.Alignment != cleanRoleNode.Alignment ||
                    patchedRoleNode.TextFlags != cleanRoleNode.TextFlags ||
                    patchedRoleNode.SheetType != cleanRoleNode.SheetType ||
                    patchedRoleNode.CharSpacing != cleanRoleNode.CharSpacing ||
                    patchedRoleNode.LineSpacing != cleanRoleNode.LineSpacing ||
                    patchedRoleNode.TextFlags2 != cleanRoleNode.TextFlags2)
                {
                    Fail(
                        "{0} patched Party Bonus role contract is invalid: font={1}/{2}, rect={3},{4},{5},{6}, align={7}, flags={8}/{9}, spacing={10}/{11}",
                        PartyMemberListUldPath,
                        patchedRoleNode.FontId,
                        patchedRoleNode.FontSize,
                        patchedRoleNode.X,
                        patchedRoleNode.Y,
                        patchedRoleNode.Width,
                        patchedRoleNode.Height,
                        patchedRoleNode.Alignment,
                        patchedRoleNode.TextFlags,
                        patchedRoleNode.TextFlags2,
                        patchedRoleNode.CharSpacing,
                        patchedRoleNode.LineSpacing);
                }

                VerifyPartyBonusUldByteDelta(cleanUld, patchedUld, cleanLocator, patchedLocator);

                int cleanInstances = CountUldNodesByType(cleanUld, (int)PartyBonusComponentId);
                int patchedInstances = CountUldNodesByType(patchedUld, (int)PartyBonusComponentId);
                if (cleanInstances != PartyBonusComponentInstances || patchedInstances != PartyBonusComponentInstances)
                {
                    Fail(
                        "{0} Party Bonus component instance count changed: clean={1}, patched={2}, expected={3}",
                        PartyMemberListUldPath,
                        cleanInstances,
                        patchedInstances,
                        PartyBonusComponentInstances);
                }

                string[] expectedRoutes = new string[]
                {
                    "common/font/AXIS_12.fdt",
                    "common/font/AXIS_18.fdt",
                    "common/font/AXIS_18.fdt",
                    "common/font/AXIS_36.fdt"
                };

                for (int scaleIndex = 0; scaleIndex < InGameUiScalePercents.Length; scaleIndex++)
                {
                    int scale = InGameUiScalePercents[scaleIndex];
                    string fontPath = ResolveUldFontPathAtScale(
                        patchedRoleNode.FontId,
                        patchedRoleNode.FontSize,
                        scale,
                        false);
                    if (!string.Equals(fontPath, expectedRoutes[scaleIndex], StringComparison.OrdinalIgnoreCase))
                    {
                        Fail(
                            "Party Bonus {0}% route expected {1}, actual {2}",
                            scale,
                            expectedRoutes[scaleIndex],
                            fontPath ?? "unmapped");
                        continue;
                    }

                    VerifyPartyBonusRolePhraseMetrics(fontPath, scale, patchedRoleNode);

                    Pass(
                        "Party Bonus {0}% routes FontType={1}/Size={2} to {3}",
                        scale,
                        patchedRoleNode.FontId,
                        patchedRoleNode.FontSize,
                        fontPath);
                }
            }

            private void VerifyPartyBonusUldByteDelta(
                byte[] cleanUld,
                byte[] patchedUld,
                PartyBonusRoleFontPatch.PartyBonusRoleTextNode cleanLocator,
                PartyBonusRoleFontPatch.PartyBonusRoleTextNode patchedLocator)
            {
                if (cleanUld.Length != patchedUld.Length || cleanLocator.NodeOffset != patchedLocator.NodeOffset)
                {
                    Fail(
                        "{0} Party Bonus ULD structure changed: cleanLength={1}, patchedLength={2}, cleanNode=0x{3:X}, patchedNode=0x{4:X}",
                        PartyMemberListUldPath,
                        cleanUld.Length,
                        patchedUld.Length,
                        cleanLocator.NodeOffset,
                        patchedLocator.NodeOffset);
                    return;
                }

                int differences = 0;
                int unexpectedDifferences = 0;
                for (int offset = 0; offset < cleanUld.Length; offset++)
                {
                    if (cleanUld[offset] == patchedUld[offset])
                    {
                        continue;
                    }

                    differences++;
                    if (offset != cleanLocator.FontOffset && offset != cleanLocator.FontSizeOffset)
                    {
                        unexpectedDifferences++;
                    }
                }

                if (differences != 2 || unexpectedDifferences != 0 ||
                    patchedUld[cleanLocator.FontOffset] != PartyBonusRoleFontPatch.TargetFontId ||
                    patchedUld[cleanLocator.FontSizeOffset] != PartyBonusRoleFontPatch.TargetFontSize)
                {
                    Fail(
                        "{0} Party Bonus ULD expected only font bytes to change: differences={1}, unexpected={2}, font={3}/{4}",
                        PartyMemberListUldPath,
                        differences,
                        unexpectedDifferences,
                        patchedUld[cleanLocator.FontOffset],
                        patchedUld[cleanLocator.FontSizeOffset]);
                    return;
                }

                Pass(
                    "{0} Party Bonus ULD changed only role font bytes at 0x{1:X}/0x{2:X}",
                    PartyMemberListUldPath,
                    cleanLocator.FontOffset,
                    cleanLocator.FontSizeOffset);
            }

            private void VerifyPartyBonusRolePhraseMetrics(
                string fontPath,
                int uiScalePercent,
                UldTextNodeFont roleNode)
            {
                byte[] fdt;
                FdtFontMetrics fontMetrics;
                try
                {
                    fdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} Party Bonus font read error: {1}", fontPath, ex.Message);
                    return;
                }

                if (!TryReadFdtFontMetrics(fdt, out fontMetrics) || fontMetrics.Size <= 0d)
                {
                    Fail("{0} Party Bonus nominal font size could not be read", fontPath);
                    return;
                }

                double uiScale = uiScalePercent / 100d;
                double rasterScale = (roleNode.FontSize * uiScale) / fontMetrics.Size;
                double availableWidth = (PartyBonusRoleFontPatch.ComponentStride - roleNode.X) * uiScale;
                double availableHeight = roleNode.Height * uiScale;
                for (int phraseIndex = 0; phraseIndex < ActionDetailHighScaleHangulGlyphs.PartyBonusRolePhrases.Length; phraseIndex++)
                {
                    string phrase = ActionDetailHighScaleHangulGlyphs.PartyBonusRolePhrases[phraseIndex];
                    PhraseVisualBounds bounds;
                    string error;
                    if (!TryMeasurePhraseVisualBounds(_patchedFont, fontPath, phrase, true, out bounds, out error))
                    {
                        Fail(
                            "{0} Party Bonus phrase [{1}] failed: {2}",
                            fontPath,
                            Escape(phrase),
                            error);
                        continue;
                    }

                    double renderedAdvance = bounds.Advance * rasterScale;
                    double renderedHeight = bounds.Height * rasterScale;
                    if (renderedAdvance > availableWidth + 0.01d || renderedHeight > availableHeight + 0.01d)
                    {
                        Fail(
                            "{0} Party Bonus {1}% phrase [{2}] exceeds its slot: rendered={3}x{4}, available={5}x{6}",
                            fontPath,
                            uiScalePercent,
                            Escape(phrase),
                            FormatDouble(renderedAdvance),
                            FormatDouble(renderedHeight),
                            FormatDouble(availableWidth),
                            FormatDouble(availableHeight));
                        continue;
                    }

                    Pass(
                        "{0} Party Bonus {1}% phrase [{2}] rendered={3}x{4}, available={5}x{6}",
                        fontPath,
                        uiScalePercent,
                        Escape(phrase),
                        FormatDouble(renderedAdvance),
                        FormatDouble(renderedHeight),
                        FormatDouble(availableWidth),
                        FormatDouble(availableHeight));
                }
            }
        }
    }
}
