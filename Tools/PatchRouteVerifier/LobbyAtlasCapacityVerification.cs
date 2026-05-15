using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int LobbyAtlasPadding = 4;
            private const int MaxLobbyCoverageGlyphFailures = 80;

            private static readonly string[] LobbyAtlasTexturePaths = new string[]
            {
                FontLobby1TexturePath,
                FontLobby2TexturePath,
                FontLobby3TexturePath,
                FontLobby4TexturePath,
                FontLobby5TexturePath,
                FontLobby6TexturePath,
                FontLobby7TexturePath
            };

            private void VerifyLobbyAtlasCapacity()
            {
                Console.WriteLine("[REPORT] lobby atlas capacity");
                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);

                Dictionary<string, HashSet<string>> fontsByScreen = CollectLobbyFontsByScreen();
                Dictionary<string, LobbyFontGlyphRequirement> requirements =
                    CollectLobbyFontGlyphRequirements(fontsByScreen, reportDir);
                LobbyAtlasCapacityStats stats = WriteLobbyAtlasCapacityReports(requirements, fontsByScreen, reportDir);

                Pass(
                    "lobby atlas capacity wrote {0} target fonts, {1} required codepoints, {2} missing target glyphs, {3} source-covered glyphs, {4} aggregate allocation failures, {5} actionable candidate ranges",
                    stats.TargetFonts,
                    stats.RequiredCodepoints,
                    stats.MissingTargetGlyphs,
                    stats.SourceCoveredGlyphs,
                    stats.AggregateAllocationFailures,
                    stats.ActionableCandidateRanges);
            }

            private void VerifyLobbyCoverageGlyphs()
            {
                Console.WriteLine("[FDT] Lobby coverage glyphs");
                string reportDir = ResolveLobbyReportDir();
                Directory.CreateDirectory(reportDir);

                Dictionary<string, HashSet<string>> fontsByScreen = CollectLobbyFontsByScreen();
                Dictionary<string, LobbyFontGlyphRequirement> requirements =
                    CollectLobbyFontGlyphRequirements(fontsByScreen, reportDir);

                int checkedFonts = 0;
                int checkedGlyphs = 0;
                int failures = 0;
                List<string> fonts = new List<string>(requirements.Keys);
                fonts.Sort(StringComparer.OrdinalIgnoreCase);
                for (int fontIndex = 0; fontIndex < fonts.Count; fontIndex++)
                {
                    string fontPath = fonts[fontIndex];
                    LobbyFontGlyphRequirement requirement = requirements[fontPath];
                    byte[] targetFdt;
                    try
                    {
                        targetFdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        failures = FailLobbyCoverageGlyphOnce(failures, "{0} required by {1} is missing or unreadable: {2}", fontPath, JoinSorted(requirement.Screens), ex.Message);
                        continue;
                    }

                    checkedFonts++;
                    List<uint> codepoints = new List<uint>(requirement.Codepoints);
                    codepoints.Sort();
                    for (int codepointIndex = 0; codepointIndex < codepoints.Count; codepointIndex++)
                    {
                        uint codepoint = codepoints[codepointIndex];
                        FdtGlyphEntry glyph;
                        if (!TryFindGlyph(targetFdt, codepoint, out glyph))
                        {
                            failures = FailLobbyCoverageGlyphOnce(
                                failures,
                                "{0} missing lobby coverage glyph U+{1:X4} [{2}] for screens={3}, groups={4}",
                                fontPath,
                                codepoint,
                                char.ConvertFromUtf32(checked((int)codepoint)),
                                JoinSorted(requirement.Screens),
                                JoinSorted(requirement.Groups));
                            continue;
                        }

                        try
                        {
                            GlyphCanvas canvas = RenderGlyph(_patchedFont, fontPath, codepoint);
                            if (canvas.VisiblePixels <= 0)
                            {
                                failures = FailLobbyCoverageGlyphOnce(
                                    failures,
                                    "{0} invisible lobby coverage glyph U+{1:X4} [{2}] for screens={3}, groups={4}",
                                    fontPath,
                                    codepoint,
                                    char.ConvertFromUtf32(checked((int)codepoint)),
                                    JoinSorted(requirement.Screens),
                                    JoinSorted(requirement.Groups));
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            failures = FailLobbyCoverageGlyphOnce(
                                failures,
                                "{0} lobby coverage glyph U+{1:X4} [{2}] render error: {3}",
                                fontPath,
                                codepoint,
                                char.ConvertFromUtf32(checked((int)codepoint)),
                                ex.Message);
                            continue;
                        }

                        checkedGlyphs++;
                    }
                }

                if (failures >= MaxLobbyCoverageGlyphFailures)
                {
                    Warn("lobby coverage glyph check stopped after {0} failures", MaxLobbyCoverageGlyphFailures);
                }

                if (failures == 0)
                {
                    Pass(
                        "lobby coverage glyphs are present and visible: fonts={0}, glyphs={1}",
                        checkedFonts,
                        checkedGlyphs);
                }
            }

            private int FailLobbyCoverageGlyphOnce(int failures, string format, params object[] args)
            {
                if (failures < MaxLobbyCoverageGlyphFailures)
                {
                    Fail(format, args);
                }

                return failures + 1;
            }

            private Dictionary<string, HashSet<string>> CollectLobbyFontsByScreen()
            {
                Dictionary<string, HashSet<string>> fontsByScreen =
                    new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                CollectLobbyFontsByScreen(fontsByScreen, "data-center-select", new UldRouteCandidate[]
                {
                    new UldRouteCandidate(DataCenterTitleUldPath, true),
                    new UldRouteCandidate(DataCenterWorldmapUldPath, true)
                });
                CollectLobbyFontsByScreen(fontsByScreen, "start-system-settings", StartScreenSystemSettingsUldCandidates);
                CollectLobbyFontsByScreen(fontsByScreen, "start-main-menu", StartScreenMainMenuUldCandidates);
                CollectLobbyFontsByScreen(fontsByScreen, "character-select", CharacterSelectLobbyUldCandidates);

                return fontsByScreen;
            }

            private void CollectLobbyFontsByScreen(
                Dictionary<string, HashSet<string>> fontsByScreen,
                string screen,
                UldRouteCandidate[] candidates)
            {
                HashSet<string> fonts = GetOrAddStringSet(fontsByScreen, screen);
                for (int i = 0; i < candidates.Length; i++)
                {
                    UldRouteCandidate candidate = candidates[i];
                    byte[] packed;
                    if (!_patchedUi.TryReadPackedFile(candidate.Path, out packed))
                    {
                        continue;
                    }

                    byte[] uld;
                    try
                    {
                        uld = SqPackArchive.UnpackStandardFile(packed);
                    }
                    catch
                    {
                        continue;
                    }

                    List<UldTextNodeFont> nodes = GetUldTextNodeFonts(uld);
                    for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                    {
                        string fontPath = ResolveUldFontPath(nodes[nodeIndex].FontId, nodes[nodeIndex].FontSize, candidate.UsesLobbyFonts);
                        if (!string.IsNullOrEmpty(fontPath))
                        {
                            fonts.Add(fontPath);
                        }
                    }
                }
            }

            private Dictionary<string, LobbyFontGlyphRequirement> CollectLobbyFontGlyphRequirements(
                Dictionary<string, HashSet<string>> fontsByScreen,
                string reportDir)
            {
                Dictionary<string, LobbyFontGlyphRequirement> requirements =
                    new Dictionary<string, LobbyFontGlyphRequirement>(StringComparer.OrdinalIgnoreCase);
                string coverageReportPath = Path.Combine(reportDir, "lobby-coverage-glyphs.tsv");

                using (StreamWriter writer = CreateUtf8Writer(coverageReportPath))
                {
                    writer.WriteLine("group\tscreens\tfont_count\tsheet\trow_id\tcolumn_offset\thangul_chars\tunique_hangul\ttext");
                    LobbyCoverageGroup[] groups = CreateLobbyCoverageGroups();
                    for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                    {
                        LobbyCoverageGroup group = groups[groupIndex];
                        HashSet<string> groupFonts = ResolveCoverageGroupFonts(fontsByScreen, group.Screens);
                        List<LobbyCoverageTextRow> rows = ReadCoverageTextRows(group);
                        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                        {
                            LobbyCoverageTextRow row = rows[rowIndex];
                            WriteTsvRow(
                                writer,
                                group.Name,
                                string.Join(",", group.Screens),
                                groupFonts.Count.ToString(),
                                row.Sheet,
                                row.RowId.ToString(),
                                row.ColumnOffset.ToString(),
                                CountHangulChars(row.Text).ToString(),
                                FormatCodepoints(row.Codepoints),
                                row.Text);

                            foreach (string fontPath in groupFonts)
                            {
                                LobbyFontGlyphRequirement requirement = GetOrAddRequirement(requirements, fontPath);
                                requirement.Groups.Add(group.Name);
                                requirement.Screens.UnionWith(group.Screens);
                                AddRange(requirement.Codepoints, row.Codepoints);
                                AddRange(GetOrAddCodepointSet(requirement.CodepointsByGroup, group.Name), row.Codepoints);
                            }
                        }
                    }
                }

                return requirements;
            }

            private static LobbyCoverageGroup[] CreateLobbyCoverageGroups()
            {
                return new LobbyCoverageGroup[]
                {
                    new LobbyCoverageGroup(
                        "start-main-menu",
                        new string[] { "start-main-menu" },
                        new LobbyCoverageRowSpec[]
                        {
                            new LobbyCoverageRowSpec("Lobby", 2003, 2009),
                            new LobbyCoverageRowSpec("Lobby", 2052, 2059),
                            new LobbyCoverageRowSpec("Addon", 2744, 2744),
                            new LobbyCoverageRowSpec("Addon", 4000, 4000)
                        }),
                    new LobbyCoverageGroup(
                        "start-system-settings",
                        new string[] { "start-system-settings" },
                        CreateStartSystemSettingsCoverageRows()),
                    new LobbyCoverageGroup(
                        "character-select-core",
                        new string[] { "character-select" },
                        new LobbyCoverageRowSpec[]
                        {
                            new LobbyCoverageRowSpec("Lobby", 23, 24),
                            new LobbyCoverageRowSpec("Lobby", 41, 53),
                            new LobbyCoverageRowSpec("Lobby", 101, 101),
                            new LobbyCoverageRowSpec("Lobby", 507, 507),
                            new LobbyCoverageRowSpec("Lobby", 841, 842),
                            new LobbyCoverageRowSpec("Lobby", 849, 850),
                            new LobbyCoverageRowSpec("Lobby", 921, 921),
                            new LobbyCoverageRowSpec("Lobby", 975, 975),
                            new LobbyCoverageRowSpec("Lobby", 1100, 1104),
                            new LobbyCoverageRowSpec("Lobby", 1150, 1150),
                            new LobbyCoverageRowSpec("Lobby", 1223, 1223),
                            new LobbyCoverageRowSpec("Lobby", 2019, 2019),
                            new LobbyCoverageRowSpec("Lobby", 2066, 2066)
                        }),
                    new LobbyCoverageGroup(
                        "character-select-world-transfer",
                        new string[] { "character-select" },
                        new LobbyCoverageRowSpec[]
                        {
                            new LobbyCoverageRowSpec("Lobby", 1100, 1233),
                            new LobbyCoverageRowSpec("Error", 13206, 13220)
                        }),
                    new LobbyCoverageGroup(
                        "character-select-class-race-tribe",
                        new string[] { "character-select" },
                        new LobbyCoverageRowSpec[]
                        {
                            new LobbyCoverageRowSpec("ClassJob", 0, 43),
                            new LobbyCoverageRowSpec("Race", 1, 8),
                            new LobbyCoverageRowSpec("Tribe", 1, 16)
                        }),
                    new LobbyCoverageGroup(
                        "character-select-guardian-deity-names",
                        new string[] { "character-select" },
                        new LobbyCoverageRowSpec[]
                        {
                            new LobbyCoverageRowSpec("GuardianDeity", 1, 12, 0)
                        })
                };
            }

            private static LobbyCoverageRowSpec[] CreateStartSystemSettingsCoverageRows()
            {
                List<LobbyCoverageRowSpec> rows = new List<LobbyCoverageRowSpec>();
                for (int i = 0; i < LobbyScaledHangulPhrases.StartScreenSystemSettingsAddonRowRanges.Length; i++)
                {
                    AddonRowRange range = LobbyScaledHangulPhrases.StartScreenSystemSettingsAddonRowRanges[i];
                    rows.Add(new LobbyCoverageRowSpec("Addon", range.StartId, range.EndId));
                }

                return rows.ToArray();
            }

            private HashSet<string> ResolveCoverageGroupFonts(
                Dictionary<string, HashSet<string>> fontsByScreen,
                string[] screens)
            {
                HashSet<string> fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < screens.Length; i++)
                {
                    HashSet<string> routedFonts;
                    if (fontsByScreen.TryGetValue(screens[i], out routedFonts))
                    {
                        foreach (string font in routedFonts)
                        {
                            fonts.Add(font);
                        }
                    }
                }

                return fonts;
            }

            private List<LobbyCoverageTextRow> ReadCoverageTextRows(LobbyCoverageGroup group)
            {
                List<LobbyCoverageTextRow> rows = new List<LobbyCoverageTextRow>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int specIndex = 0; specIndex < group.Rows.Length; specIndex++)
                {
                    LobbyCoverageRowSpec spec = group.Rows[specIndex];
                    ExcelHeader header;
                    try
                    {
                        header = ExcelHeader.Parse(_patchedText.ReadFile("exd/" + spec.Sheet + ".exh"));
                    }
                    catch
                    {
                        continue;
                    }

                    List<int> stringColumns = header.GetStringColumnIndexes();
                    bool hasLanguageSuffix = header.HasLanguage(LanguageToId(_language));
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        if (!PageOverlaps(page, spec.StartRowId, spec.EndRowId))
                        {
                            continue;
                        }

                        ExcelDataFile file;
                        try
                        {
                            file = ExcelDataFile.Parse(_patchedText.ReadFile(BuildExdPath(spec.Sheet, page.StartId, _language, hasLanguageSuffix)));
                        }
                        catch
                        {
                            continue;
                        }

                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            if (row.RowId < spec.StartRowId || row.RowId > spec.EndRowId)
                            {
                                continue;
                            }

                            for (int columnListIndex = 0; columnListIndex < stringColumns.Count; columnListIndex++)
                            {
                                int columnIndex = stringColumns[columnListIndex];
                                ExcelColumnDefinition column = header.Columns[columnIndex];
                                if (spec.ColumnOffset.HasValue && spec.ColumnOffset.Value != column.Offset)
                                {
                                    continue;
                                }

                                byte[] bytes = file.GetStringBytes(row, header, columnIndex);
                                string text = bytes == null ? string.Empty : Encoding.UTF8.GetString(bytes);
                                if (!ContainsHangul(text))
                                {
                                    continue;
                                }

                                string key = spec.Sheet + "|" + row.RowId.ToString() + "|" + column.Offset.ToString();
                                if (!seen.Add(key))
                                {
                                    continue;
                                }

                                LobbyCoverageTextRow textRow = new LobbyCoverageTextRow();
                                textRow.Sheet = spec.Sheet;
                                textRow.RowId = row.RowId;
                                textRow.ColumnOffset = column.Offset;
                                textRow.Text = text;
                                textRow.Codepoints = CollectHangulCodepoints(text);
                                rows.Add(textRow);
                            }
                        }
                    }
                }

                return rows;
            }

            private static bool PageOverlaps(ExcelPageDefinition page, uint startRowId, uint endRowId)
            {
                uint pageEnd = page.RowCount == 0 ? page.StartId : page.StartId + page.RowCount - 1;
                return startRowId <= pageEnd && endRowId >= page.StartId;
            }

            private LobbyAtlasCapacityStats WriteLobbyAtlasCapacityReports(
                Dictionary<string, LobbyFontGlyphRequirement> requirements,
                Dictionary<string, HashSet<string>> fontsByScreen,
                string reportDir)
            {
                string fontReportPath = Path.Combine(reportDir, "lobby-font-required-glyphs.tsv");
                string detailReportPath = Path.Combine(reportDir, "lobby-atlas-allocation-details.tsv");
                string aggregateReportPath = Path.Combine(reportDir, "lobby-atlas-aggregate-capacity.tsv");
                string groupReportPath = Path.Combine(reportDir, "lobby-atlas-group-capacity.tsv");
                string actionableRangeReportPath = Path.Combine(reportDir, "lobby-actionable-coverage-ranges.tsv");
                string actionableCapacityReportPath = Path.Combine(reportDir, "lobby-actionable-group-capacity.tsv");
                string sourceTextureReportPath = Path.Combine(reportDir, "lobby-source-texture-summary.tsv");

                LobbyAtlasCapacityStats stats = new LobbyAtlasCapacityStats();
                Dictionary<string, byte[]> fdtCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> sourceTextureUseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                LobbyAtlasAllocator baseAllocator = TryCreateLobbyAtlasAllocator();
                LobbyAtlasAllocator aggregateAllocator = baseAllocator == null ? null : baseAllocator.Clone();
                Dictionary<string, int> aggregateAllocatedByFont = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> aggregateFailuresByFont = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                List<string> fonts = new List<string>(requirements.Keys);
                fonts.Sort(StringComparer.OrdinalIgnoreCase);

                using (StreamWriter fontWriter = CreateUtf8Writer(fontReportPath))
                using (StreamWriter detailWriter = CreateUtf8Writer(detailReportPath))
                {
                    fontWriter.WriteLine("font_path\tdiagnostic_only\tscreens\tgroups\trequired\talready_present\tmissing_target\tsource_covered\tsource_missing\tindependent_allocated\tindependent_failures\tsource_paths");
                    detailWriter.WriteLine("font_path\tcodepoint\tchar\tstatus\tsource_path\tsource_texture\timage_index\tchannel\tx\ty\twidth\theight\toffset_x\toffset_y");

                    for (int fontIndex = 0; fontIndex < fonts.Count; fontIndex++)
                    {
                        string fontPath = fonts[fontIndex];
                        LobbyFontGlyphRequirement requirement = requirements[fontPath];
                        stats.TargetFonts++;
                        stats.RequiredCodepoints += requirement.Codepoints.Count;

                        byte[] targetFdt = TryReadFdt(_patchedFont, fontPath, fdtCache);
                        HashSet<uint> missingTarget = new HashSet<uint>();
                        int alreadyPresent = 0;
                        foreach (uint codepoint in requirement.Codepoints)
                        {
                            FdtGlyphEntry ignored;
                            if (targetFdt != null && TryFindGlyph(targetFdt, codepoint, out ignored))
                            {
                                alreadyPresent++;
                            }
                            else
                            {
                                missingTarget.Add(codepoint);
                            }
                        }

                        stats.MissingTargetGlyphs += missingTarget.Count;

                        LobbyAtlasAllocator independentAllocator = baseAllocator == null ? null : baseAllocator.Clone();
                        int sourceCovered = 0;
                        int sourceMissing = 0;
                        int independentAllocated = 0;
                        int independentFailures = 0;
                        HashSet<string> sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        bool diagnosticOnly = !IsLobbyFontPath(fontPath);

                        List<uint> missing = new List<uint>(missingTarget);
                        missing.Sort();
                        for (int i = 0; i < missing.Count; i++)
                        {
                            uint codepoint = missing[i];
                            LobbySourceGlyph sourceGlyph;
                            if (!TryResolveLobbySourceGlyph(fontPath, codepoint, fdtCache, out sourceGlyph))
                            {
                                sourceMissing++;
                                WriteAllocationDetail(detailWriter, fontPath, codepoint, "missing-source", new LobbySourceGlyph());
                                continue;
                            }

                            sourceCovered++;
                            stats.SourceCoveredGlyphs++;
                            sourcePaths.Add(sourceGlyph.SourcePath);
                            IncrementCounter(sourceTextureUseCounts, sourceGlyph.TexturePath);

                            if (diagnosticOnly)
                            {
                                WriteAllocationDetail(detailWriter, fontPath, codepoint, "diagnostic-non-lobby", sourceGlyph);
                                continue;
                            }

                            bool independentOk = independentAllocator != null &&
                                independentAllocator.TryAllocate(sourceGlyph.Width, sourceGlyph.Height);
                            if (independentOk)
                            {
                                independentAllocated++;
                            }
                            else
                            {
                                independentFailures++;
                            }

                            bool aggregateOk = aggregateAllocator != null &&
                                aggregateAllocator.TryAllocate(sourceGlyph.Width, sourceGlyph.Height);
                            if (aggregateOk)
                            {
                                IncrementCounter(aggregateAllocatedByFont, fontPath);
                            }
                            else
                            {
                                IncrementCounter(aggregateFailuresByFont, fontPath);
                                stats.AggregateAllocationFailures++;
                            }

                            WriteAllocationDetail(
                                detailWriter,
                                fontPath,
                                codepoint,
                                independentOk ? "allocated" : "allocation-failed",
                                sourceGlyph);
                        }

                        int aggregateAllocated = GetCounter(aggregateAllocatedByFont, fontPath);
                        int aggregateFailures = GetCounter(aggregateFailuresByFont, fontPath);
                        WriteTsvRow(
                            fontWriter,
                            fontPath,
                            diagnosticOnly ? "yes" : "no",
                            JoinSorted(requirement.Screens),
                            JoinSorted(requirement.Groups),
                            requirement.Codepoints.Count.ToString(),
                            alreadyPresent.ToString(),
                            missingTarget.Count.ToString(),
                            sourceCovered.ToString(),
                            sourceMissing.ToString(),
                            independentAllocated.ToString(),
                            independentFailures.ToString(),
                            JoinSorted(sourcePaths));
                    }
                }

                WriteAggregateCapacityReport(aggregateReportPath, aggregateAllocatedByFont, aggregateFailuresByFont);
                WriteLobbyAtlasGroupCapacityReport(groupReportPath, requirements, fdtCache, baseAllocator);
                LobbyActionableCapacityStats actionableStats = WriteLobbyActionableCandidateCapacityReports(
                    actionableRangeReportPath,
                    actionableCapacityReportPath,
                    fontsByScreen,
                    fdtCache,
                    baseAllocator);
                stats.ActionableCandidateRanges = actionableStats.Ranges;
                stats.ActionableCandidateMissingTargetGlyphs = actionableStats.MissingTargetGlyphs;
                stats.ActionableCandidateAggregateAllocationFailures = actionableStats.AggregateAllocationFailures;
                WriteLobbySourceTextureSummaryReport(sourceTextureReportPath, sourceTextureUseCounts);
                return stats;
            }

            private void WriteLobbySourceTextureSummaryReport(
                string reportPath,
                Dictionary<string, int> sourceTextureUseCounts)
            {
                List<string> texturePaths = new List<string>(sourceTextureUseCounts.Keys);
                texturePaths.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(reportPath))
                {
                    writer.WriteLine("source_texture\tglyph_references\tttmp_present\tpatched_present\tclean_present\tpatched_matches_ttmp\tpatched_matches_clean\tclean_matches_ttmp");
                    for (int i = 0; i < texturePaths.Count; i++)
                    {
                        string texturePath = texturePaths[i];
                        byte[] ttmp = TryReadUnpackedTexture(_ttmpFont, texturePath);
                        byte[] patched = TryReadUnpackedTexture(_patchedFont, texturePath);
                        byte[] clean = TryReadUnpackedTexture(_cleanFont, texturePath);
                        WriteTsvRow(
                            writer,
                            texturePath,
                            GetCounter(sourceTextureUseCounts, texturePath).ToString(),
                            ttmp == null ? "no" : "yes",
                            patched == null ? "no" : "yes",
                            clean == null ? "no" : "yes",
                            FormatByteEquality(patched, ttmp),
                            FormatByteEquality(patched, clean),
                            FormatByteEquality(clean, ttmp));
                    }
                }
            }

            private void WriteLobbyAtlasGroupCapacityReport(
                string groupReportPath,
                Dictionary<string, LobbyFontGlyphRequirement> requirements,
                Dictionary<string, byte[]> fdtCache,
                LobbyAtlasAllocator baseAllocator)
            {
                LobbyAtlasAllocator aggregateAllocator = baseAllocator == null ? null : baseAllocator.Clone();
                HashSet<string> aggregateSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<string> fonts = new List<string>(requirements.Keys);
                fonts.Sort(StringComparer.OrdinalIgnoreCase);
                LobbyCoverageGroup[] groups = CreateLobbyCoverageGroups();

                using (StreamWriter writer = CreateUtf8Writer(groupReportPath))
                {
                    writer.WriteLine("group\tfont_path\tdiagnostic_only\trequired\talready_present\tmissing_target\tsource_covered\tsource_missing\tindependent_allocated\tindependent_failures\taggregate_new_missing\taggregate_reused_from_earlier_groups\taggregate_allocated\taggregate_failures");
                    for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                    {
                        string groupName = groups[groupIndex].Name;
                        for (int fontIndex = 0; fontIndex < fonts.Count; fontIndex++)
                        {
                            string fontPath = fonts[fontIndex];
                            LobbyFontGlyphRequirement requirement = requirements[fontPath];
                            HashSet<uint> groupCodepoints;
                            if (!requirement.CodepointsByGroup.TryGetValue(groupName, out groupCodepoints) ||
                                groupCodepoints.Count == 0)
                            {
                                continue;
                            }

                            byte[] targetFdt = TryReadFdt(_patchedFont, fontPath, fdtCache);
                            bool diagnosticOnly = !IsLobbyFontPath(fontPath);
                            LobbyAtlasAllocator independentAllocator = baseAllocator == null ? null : baseAllocator.Clone();
                            int alreadyPresent = 0;
                            int missingTarget = 0;
                            int sourceCovered = 0;
                            int sourceMissing = 0;
                            int independentAllocated = 0;
                            int independentFailures = 0;
                            int aggregateNewMissing = 0;
                            int aggregateReused = 0;
                            int aggregateAllocated = 0;
                            int aggregateFailures = 0;

                            List<uint> codepoints = new List<uint>(groupCodepoints);
                            codepoints.Sort();
                            for (int i = 0; i < codepoints.Count; i++)
                            {
                                uint codepoint = codepoints[i];
                                FdtGlyphEntry ignored;
                                if (targetFdt != null && TryFindGlyph(targetFdt, codepoint, out ignored))
                                {
                                    alreadyPresent++;
                                    continue;
                                }

                                missingTarget++;
                                string aggregateKey = fontPath + "|U+" + codepoint.ToString("X4");
                                bool firstAggregateRequirement = aggregateSeen.Add(aggregateKey);
                                if (firstAggregateRequirement)
                                {
                                    aggregateNewMissing++;
                                }
                                else
                                {
                                    aggregateReused++;
                                }

                                LobbySourceGlyph sourceGlyph;
                                if (!TryResolveLobbySourceGlyph(fontPath, codepoint, fdtCache, out sourceGlyph))
                                {
                                    sourceMissing++;
                                    continue;
                                }

                                sourceCovered++;
                                if (diagnosticOnly)
                                {
                                    continue;
                                }

                                bool independentOk = independentAllocator != null &&
                                    independentAllocator.TryAllocate(sourceGlyph.Width, sourceGlyph.Height);
                                if (independentOk)
                                {
                                    independentAllocated++;
                                }
                                else
                                {
                                    independentFailures++;
                                }

                                if (!firstAggregateRequirement)
                                {
                                    continue;
                                }

                                bool aggregateOk = aggregateAllocator != null &&
                                    aggregateAllocator.TryAllocate(sourceGlyph.Width, sourceGlyph.Height);
                                if (aggregateOk)
                                {
                                    aggregateAllocated++;
                                }
                                else
                                {
                                    aggregateFailures++;
                                }
                            }

                            WriteTsvRow(
                                writer,
                                groupName,
                                fontPath,
                                diagnosticOnly ? "yes" : "no",
                                groupCodepoints.Count.ToString(),
                                alreadyPresent.ToString(),
                                missingTarget.ToString(),
                                sourceCovered.ToString(),
                                sourceMissing.ToString(),
                                independentAllocated.ToString(),
                                independentFailures.ToString(),
                                aggregateNewMissing.ToString(),
                                aggregateReused.ToString(),
                                aggregateAllocated.ToString(),
                                aggregateFailures.ToString());
                        }
                    }
                }
            }

            private LobbyActionableCapacityStats WriteLobbyActionableCandidateCapacityReports(
                string rangeReportPath,
                string capacityReportPath,
                Dictionary<string, HashSet<string>> fontsByScreen,
                Dictionary<string, byte[]> fdtCache,
                LobbyAtlasAllocator baseAllocator)
            {
                LobbyActionableCapacityStats stats = new LobbyActionableCapacityStats();
                List<LobbyActionableCoverageRange> ranges = CreateActionableCoverageRanges();
                stats.Ranges = ranges.Count;

                using (StreamWriter rangeWriter = CreateUtf8Writer(rangeReportPath))
                {
                    rangeWriter.WriteLine("candidate_group\tnearest_coverage_group\tscreens\tsheet\tstart_row\tend_row\tcolumn_offset\tcandidate_columns\thangul_chars\tunique_hangul\treasons");
                    for (int i = 0; i < ranges.Count; i++)
                    {
                        LobbyActionableCoverageRange range = ranges[i];
                        WriteTsvRow(
                            rangeWriter,
                            range.Name,
                            range.NearestCoverageGroup,
                            string.Join(",", range.Screens),
                            range.Sheet,
                            range.StartRowId.ToString(),
                            range.EndRowId.ToString(),
                            range.ColumnOffset.ToString(),
                            range.CandidateColumns.ToString(),
                            range.HangulChars.ToString(),
                            FormatCodepoints(range.Codepoints),
                            JoinSorted(range.Reasons));
                    }
                }

                LobbyAtlasAllocator aggregateAllocator = baseAllocator == null ? null : baseAllocator.Clone();
                HashSet<string> aggregateSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(capacityReportPath))
                {
                    writer.WriteLine("candidate_group\tnearest_coverage_group\tscreens\tfont_path\tdiagnostic_only\trequired\talready_present\tmissing_target\tsource_covered\tsource_missing\tindependent_allocated\tindependent_failures\taggregate_new_missing\taggregate_reused_from_earlier_groups\taggregate_allocated\taggregate_failures");
                    for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                    {
                        LobbyActionableCoverageRange range = ranges[rangeIndex];
                        HashSet<string> fonts = ResolveCoverageGroupFonts(fontsByScreen, range.Screens);
                        List<string> sortedFonts = new List<string>(fonts);
                        sortedFonts.Sort(StringComparer.OrdinalIgnoreCase);

                        if (sortedFonts.Count == 0)
                        {
                            WriteTsvRow(
                                writer,
                                range.Name,
                                range.NearestCoverageGroup,
                                string.Join(",", range.Screens),
                                string.Empty,
                                string.Empty,
                                range.Codepoints.Count.ToString(),
                                "0",
                                range.Codepoints.Count.ToString(),
                                "0",
                                range.Codepoints.Count.ToString(),
                                "0",
                                "0",
                                "0",
                                "0",
                                "0",
                                "0");
                            continue;
                        }

                        for (int fontIndex = 0; fontIndex < sortedFonts.Count; fontIndex++)
                        {
                            string fontPath = sortedFonts[fontIndex];
                            LobbyActionableFontCapacity fontStats = MeasureActionableFontCapacity(
                                range,
                                fontPath,
                                fdtCache,
                                baseAllocator,
                                aggregateAllocator,
                                aggregateSeen);

                            stats.FontRows++;
                            stats.RequiredCodepoints += fontStats.Required;
                            stats.MissingTargetGlyphs += fontStats.MissingTarget;
                            stats.SourceCoveredGlyphs += fontStats.SourceCovered;
                            stats.AggregateAllocationFailures += fontStats.AggregateFailures;

                            WriteTsvRow(
                                writer,
                                range.Name,
                                range.NearestCoverageGroup,
                                string.Join(",", range.Screens),
                                fontPath,
                                fontStats.DiagnosticOnly ? "yes" : "no",
                                fontStats.Required.ToString(),
                                fontStats.AlreadyPresent.ToString(),
                                fontStats.MissingTarget.ToString(),
                                fontStats.SourceCovered.ToString(),
                                fontStats.SourceMissing.ToString(),
                                fontStats.IndependentAllocated.ToString(),
                                fontStats.IndependentFailures.ToString(),
                                fontStats.AggregateNewMissing.ToString(),
                                fontStats.AggregateReused.ToString(),
                                fontStats.AggregateAllocated.ToString(),
                                fontStats.AggregateFailures.ToString());
                        }
                    }
                }

                return stats;
            }

            private LobbyActionableFontCapacity MeasureActionableFontCapacity(
                LobbyActionableCoverageRange range,
                string fontPath,
                Dictionary<string, byte[]> fdtCache,
                LobbyAtlasAllocator baseAllocator,
                LobbyAtlasAllocator aggregateAllocator,
                HashSet<string> aggregateSeen)
            {
                LobbyActionableFontCapacity stats = new LobbyActionableFontCapacity();
                stats.Required = range.Codepoints.Count;
                stats.DiagnosticOnly = !IsLobbyFontPath(fontPath);
                byte[] targetFdt = TryReadFdt(_patchedFont, fontPath, fdtCache);
                LobbyAtlasAllocator independentAllocator = null;

                List<uint> codepoints = new List<uint>(range.Codepoints);
                codepoints.Sort();
                for (int i = 0; i < codepoints.Count; i++)
                {
                    uint codepoint = codepoints[i];
                    FdtGlyphEntry ignored;
                    if (targetFdt != null && TryFindGlyph(targetFdt, codepoint, out ignored))
                    {
                        stats.AlreadyPresent++;
                        continue;
                    }

                    stats.MissingTarget++;
                    string aggregateKey = fontPath + "|U+" + codepoint.ToString("X4");
                    bool firstAggregateRequirement = aggregateSeen.Add(aggregateKey);
                    if (firstAggregateRequirement)
                    {
                        stats.AggregateNewMissing++;
                    }
                    else
                    {
                        stats.AggregateReused++;
                    }

                    LobbySourceGlyph sourceGlyph;
                    if (!TryResolveLobbySourceGlyph(fontPath, codepoint, fdtCache, out sourceGlyph))
                    {
                        stats.SourceMissing++;
                        continue;
                    }

                    stats.SourceCovered++;
                    if (stats.DiagnosticOnly)
                    {
                        continue;
                    }

                    if (independentAllocator == null && baseAllocator != null)
                    {
                        independentAllocator = baseAllocator.Clone();
                    }

                    bool independentOk = independentAllocator != null &&
                        independentAllocator.TryAllocate(sourceGlyph.Width, sourceGlyph.Height);
                    if (independentOk)
                    {
                        stats.IndependentAllocated++;
                    }
                    else
                    {
                        stats.IndependentFailures++;
                    }

                    if (!firstAggregateRequirement)
                    {
                        continue;
                    }

                    bool aggregateOk = aggregateAllocator != null &&
                        aggregateAllocator.TryAllocate(sourceGlyph.Width, sourceGlyph.Height);
                    if (aggregateOk)
                    {
                        stats.AggregateAllocated++;
                    }
                    else
                    {
                        stats.AggregateFailures++;
                    }
                }

                return stats;
            }

            private List<LobbyActionableCoverageRange> CreateActionableCoverageRanges()
            {
                List<LobbyActionableCoverageCandidate> candidates = CollectActionableCoverageCandidates();
                candidates.Sort(CompareActionableCoverageCandidates);

                List<LobbyActionableCoverageRange> ranges = new List<LobbyActionableCoverageRange>();
                LobbyActionableCoverageRange current = null;
                for (int i = 0; i < candidates.Count; i++)
                {
                    LobbyActionableCoverageCandidate candidate = candidates[i];
                    if (current == null || !current.CanAppend(candidate))
                    {
                        current = new LobbyActionableCoverageRange(candidate);
                        ranges.Add(current);
                    }
                    else
                    {
                        current.Append(candidate);
                    }
                }

                for (int i = 0; i < ranges.Count; i++)
                {
                    ranges[i].SetName(i + 1);
                }

                return ranges;
            }

            private List<LobbyActionableCoverageCandidate> CollectActionableCoverageCandidates()
            {
                List<LobbyActionableCoverageCandidate> candidates = new List<LobbyActionableCoverageCandidate>();
                LobbyCoverageGroup[] coverageGroups = CreateLobbyCoverageGroups();
                for (int sheetIndex = 0; sheetIndex < LobbySurveySheets.Length; sheetIndex++)
                {
                    string sheet = LobbySurveySheets[sheetIndex];
                    ExcelHeader header;
                    try
                    {
                        header = ExcelHeader.Parse(_patchedText.ReadFile("exd/" + sheet + ".exh"));
                    }
                    catch
                    {
                        continue;
                    }

                    List<int> stringColumns = header.GetStringColumnIndexes();
                    bool hasLanguageSuffix = header.HasLanguage(LanguageToId(_language));
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        ExcelDataFile file;
                        try
                        {
                            file = ExcelDataFile.Parse(_patchedText.ReadFile(BuildExdPath(sheet, page.StartId, _language, hasLanguageSuffix)));
                        }
                        catch
                        {
                            continue;
                        }

                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            for (int columnListIndex = 0; columnListIndex < stringColumns.Count; columnListIndex++)
                            {
                                int columnIndex = stringColumns[columnListIndex];
                                ExcelColumnDefinition column = header.Columns[columnIndex];
                                byte[] bytes = file.GetStringBytes(row, header, columnIndex);
                                string text = bytes == null ? string.Empty : Encoding.UTF8.GetString(bytes);
                                if (!ContainsHangul(text) || IsLobbyCoverageRowCovered(sheet, row.RowId, column.Offset))
                                {
                                    continue;
                                }

                                string priority = GetLobbyCoverageGapPriority(sheet, row.RowId);
                                string reasons = GetLobbyCoverageGapActionReasons(sheet, row.RowId, text, priority);
                                if (reasons.Length == 0)
                                {
                                    continue;
                                }

                                LobbyCoverageGroup nearestGroup = ResolveActionableCoverageGroup(sheet, row.RowId, text, coverageGroups);
                                LobbyActionableCoverageCandidate candidate = new LobbyActionableCoverageCandidate();
                                candidate.Sheet = sheet;
                                candidate.RowId = row.RowId;
                                candidate.ColumnOffset = column.Offset;
                                candidate.Priority = priority;
                                candidate.Reasons = reasons;
                                candidate.Text = text;
                                candidate.Codepoints = CollectHangulCodepoints(text);
                                candidate.NearestCoverageGroup = nearestGroup == null ? "unknown" : nearestGroup.Name;
                                candidate.Screens = nearestGroup == null ? new string[0] : nearestGroup.Screens;
                                candidates.Add(candidate);
                            }
                        }
                    }
                }

                return candidates;
            }

            private static int CompareActionableCoverageCandidates(
                LobbyActionableCoverageCandidate left,
                LobbyActionableCoverageCandidate right)
            {
                int cmp = string.Compare(left.NearestCoverageGroup, right.NearestCoverageGroup, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = string.Compare(left.Sheet, right.Sheet, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = left.ColumnOffset.CompareTo(right.ColumnOffset);
                if (cmp != 0)
                {
                    return cmp;
                }

                return left.RowId.CompareTo(right.RowId);
            }

            private static LobbyCoverageGroup ResolveActionableCoverageGroup(
                string sheet,
                uint rowId,
                string text,
                LobbyCoverageGroup[] coverageGroups)
            {
                if (string.Equals(sheet, "Lobby", StringComparison.OrdinalIgnoreCase) &&
                    IsTitleMenuLobbyText(text))
                {
                    LobbyCoverageGroup titleGroup = FindCoverageGroupByName(coverageGroups, "start-main-menu");
                    if (titleGroup != null)
                    {
                        return titleGroup;
                    }
                }

                return FindNearestCoverageGroup(sheet, rowId, coverageGroups);
            }

            private static bool IsTitleMenuLobbyText(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                string trimmed = text.Trim();
                return string.Equals(trimmed, "게임 시작", StringComparison.Ordinal) ||
                       string.Equals(trimmed, "데이터 센터", StringComparison.Ordinal) ||
                       string.Equals(trimmed, "동영상 및 타이틀", StringComparison.Ordinal) ||
                       string.Equals(trimmed, "설정", StringComparison.Ordinal) ||
                       string.Equals(trimmed, "라이선스", StringComparison.Ordinal) ||
                       string.Equals(trimmed, "종료", StringComparison.Ordinal) ||
                       string.Equals(trimmed, "환경 설정", StringComparison.Ordinal);
            }

            private static LobbyCoverageGroup FindCoverageGroupByName(
                LobbyCoverageGroup[] coverageGroups,
                string name)
            {
                for (int i = 0; i < coverageGroups.Length; i++)
                {
                    if (string.Equals(coverageGroups[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return coverageGroups[i];
                    }
                }

                return null;
            }

            private static LobbyCoverageGroup FindNearestCoverageGroup(
                string sheet,
                uint rowId,
                LobbyCoverageGroup[] coverageGroups)
            {
                LobbyCoverageGroup bestGroup = null;
                uint bestDistance = uint.MaxValue;
                for (int groupIndex = 0; groupIndex < coverageGroups.Length; groupIndex++)
                {
                    LobbyCoverageGroup group = coverageGroups[groupIndex];
                    for (int rowIndex = 0; rowIndex < group.Rows.Length; rowIndex++)
                    {
                        LobbyCoverageRowSpec row = group.Rows[rowIndex];
                        if (!string.Equals(row.Sheet, sheet, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        uint distance = rowId < row.StartRowId
                            ? row.StartRowId - rowId
                            : rowId > row.EndRowId
                                ? rowId - row.EndRowId
                                : 0;
                        if (bestGroup == null || distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestGroup = group;
                        }
                    }
                }

                return bestGroup;
            }

            private LobbyAtlasAllocator TryCreateLobbyAtlasAllocator()
            {
                LobbyAtlasAllocator allocator = new LobbyAtlasAllocator();
                for (int i = 0; i < LobbyAtlasTexturePaths.Length; i++)
                {
                    string texturePath = LobbyAtlasTexturePaths[i];
                    try
                    {
                        Texture texture = ReadFontTexture(_patchedFont, texturePath);
                        allocator.AddTexture(texturePath, texture);
                    }
                    catch
                    {
                    }
                }

                for (int i = 0; i < LobbyPhraseFontPaths.Length; i++)
                {
                    string fdtPath = LobbyPhraseFontPaths[i];
                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fdtPath);
                    }
                    catch
                    {
                        continue;
                    }

                    MarkLobbyFdtGlyphOccupancy(allocator, fdtPath, fdt);
                }

                return allocator.TextureCount == 0 ? null : allocator;
            }

            private static void MarkLobbyFdtGlyphOccupancy(LobbyAtlasAllocator allocator, string fdtPath, byte[] fdt)
            {
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return;
                }

                for (int i = 0; i < glyphCount; i++)
                {
                    int offset = glyphStart + i * FdtGlyphEntrySize;
                    FdtGlyphEntry glyph = ReadGlyphEntry(fdt, offset);
                    string texturePath = ResolveFontTexturePath(fdtPath, glyph.ImageIndex);
                    if (texturePath == null || !IsLobbyTexturePath(texturePath))
                    {
                        continue;
                    }

                    allocator.MarkOccupied(
                        texturePath,
                        glyph.ImageIndex % 4,
                        glyph.X - LobbyAtlasPadding,
                        glyph.Y - LobbyAtlasPadding,
                        glyph.Width + LobbyAtlasPadding * 2,
                        glyph.Height + LobbyAtlasPadding * 2);
                }
            }

            private static bool IsLobbyTexturePath(string texturePath)
            {
                return !string.IsNullOrEmpty(texturePath) &&
                       texturePath.Replace('\\', '/').IndexOf("common/font/font_lobby", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private bool TryResolveLobbySourceGlyph(
                string targetFontPath,
                uint codepoint,
                Dictionary<string, byte[]> fdtCache,
                out LobbySourceGlyph sourceGlyph)
            {
                List<LobbySourceCandidate> candidates = CreateLobbySourceCandidates(targetFontPath);
                for (int i = 0; i < candidates.Count; i++)
                {
                    LobbySourceCandidate candidate = candidates[i];
                    byte[] fdt = candidate.FromTtmp
                        ? TryReadFdt(_ttmpFont, candidate.FontPath, fdtCache)
                        : TryReadFdt(_patchedFont, candidate.FontPath, fdtCache);
                    if (fdt == null)
                    {
                        continue;
                    }

                    FdtGlyphEntry glyph;
                    if (!TryFindGlyph(fdt, codepoint, out glyph) || glyph.Width == 0 || glyph.Height == 0)
                    {
                        continue;
                    }

                    sourceGlyph = new LobbySourceGlyph();
                    sourceGlyph.SourcePath = (candidate.FromTtmp ? "ttmp:" : "patched:") + candidate.FontPath;
                    sourceGlyph.TexturePath = ResolveFontTexturePath(candidate.FontPath, glyph.ImageIndex) ?? string.Empty;
                    sourceGlyph.ImageIndex = glyph.ImageIndex;
                    sourceGlyph.Channel = glyph.ImageIndex % 4;
                    sourceGlyph.X = glyph.X;
                    sourceGlyph.Y = glyph.Y;
                    sourceGlyph.Width = glyph.Width;
                    sourceGlyph.Height = glyph.Height;
                    sourceGlyph.OffsetX = glyph.OffsetX;
                    sourceGlyph.OffsetY = glyph.OffsetY;
                    return true;
                }

                sourceGlyph = new LobbySourceGlyph();
                return false;
            }

            private static List<LobbySourceCandidate> CreateLobbySourceCandidates(string targetFontPath)
            {
                List<LobbySourceCandidate> candidates = new List<LobbySourceCandidate>();
                AddSourceCandidate(candidates, targetFontPath, true);

                string normalized = targetFontPath.Replace('\\', '/');
                if (normalized.EndsWith("_lobby.fdt", StringComparison.OrdinalIgnoreCase))
                {
                    AddSourceCandidate(candidates, normalized.Substring(0, normalized.Length - "_lobby.fdt".Length) + ".fdt", true);
                }

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    if (string.Equals(normalized, Derived4kLobbyFontPairs[i, 0], StringComparison.OrdinalIgnoreCase))
                    {
                        AddSourceCandidate(candidates, Derived4kLobbyFontPairs[i, 1], true);
                    }
                }

                AddSourceCandidate(candidates, targetFontPath, false);
                return candidates;
            }

            private static void AddSourceCandidate(List<LobbySourceCandidate> candidates, string fontPath, bool fromTtmp)
            {
                if (string.IsNullOrEmpty(fontPath))
                {
                    return;
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].FromTtmp == fromTtmp &&
                        string.Equals(candidates[i].FontPath, fontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                candidates.Add(new LobbySourceCandidate(fontPath, fromTtmp));
            }

            private byte[] TryReadFdt(CompositeArchive archive, string fontPath, Dictionary<string, byte[]> cache)
            {
                if (archive == null)
                {
                    return null;
                }

                string key = archive.CacheKey + "|" + fontPath;
                byte[] value;
                if (cache.TryGetValue(key, out value))
                {
                    return value;
                }

                try
                {
                    value = archive.ReadFile(fontPath);
                    cache[key] = value;
                    return value;
                }
                catch
                {
                    cache[key] = null;
                    return null;
                }
            }

            private byte[] TryReadFdt(TtmpFontPackage package, string fontPath, Dictionary<string, byte[]> cache)
            {
                if (package == null || !package.ContainsPath(fontPath))
                {
                    return null;
                }

                string key = package.CacheKey + "|" + fontPath;
                byte[] value;
                if (cache.TryGetValue(key, out value))
                {
                    return value;
                }

                try
                {
                    value = package.ReadFile(fontPath);
                    cache[key] = value;
                    return value;
                }
                catch
                {
                    cache[key] = null;
                    return null;
                }
            }

            private byte[] TryReadUnpackedTexture(CompositeArchive archive, string texturePath)
            {
                if (archive == null || string.IsNullOrEmpty(texturePath))
                {
                    return null;
                }

                byte[] packed;
                if (!archive.TryReadPackedFile(texturePath, out packed))
                {
                    return null;
                }

                return UnpackTextureFile(packed);
            }

            private byte[] TryReadUnpackedTexture(TtmpFontPackage package, string texturePath)
            {
                if (package == null || string.IsNullOrEmpty(texturePath))
                {
                    return null;
                }

                byte[] packed;
                if (!package.TryReadPackedFile(texturePath, out packed))
                {
                    return null;
                }

                return UnpackTextureFile(packed);
            }

            private static string FormatByteEquality(byte[] left, byte[] right)
            {
                if (left == null || right == null)
                {
                    return "n/a";
                }

                return BytesEqual(left, right) ? "yes" : "no";
            }

            private static void WriteAllocationDetail(
                StreamWriter writer,
                string fontPath,
                uint codepoint,
                string status,
                LobbySourceGlyph sourceGlyph)
            {
                WriteTsvRow(
                    writer,
                    fontPath,
                    "U+" + codepoint.ToString("X4"),
                    char.ConvertFromUtf32(checked((int)codepoint)),
                    status,
                    sourceGlyph.SourcePath ?? string.Empty,
                    sourceGlyph.TexturePath ?? string.Empty,
                    sourceGlyph.ImageIndex.ToString(),
                    sourceGlyph.Channel.ToString(),
                    sourceGlyph.X.ToString(),
                    sourceGlyph.Y.ToString(),
                    sourceGlyph.Width.ToString(),
                    sourceGlyph.Height.ToString(),
                    sourceGlyph.OffsetX.ToString(),
                    sourceGlyph.OffsetY.ToString());
            }

            private static void WriteAggregateCapacityReport(
                string aggregateReportPath,
                Dictionary<string, int> allocatedByFont,
                Dictionary<string, int> failuresByFont)
            {
                HashSet<string> fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string font in allocatedByFont.Keys)
                {
                    fonts.Add(font);
                }

                foreach (string font in failuresByFont.Keys)
                {
                    fonts.Add(font);
                }

                List<string> sorted = new List<string>(fonts);
                sorted.Sort(StringComparer.OrdinalIgnoreCase);
                using (StreamWriter writer = CreateUtf8Writer(aggregateReportPath))
                {
                    writer.WriteLine("font_path\taggregate_allocated\taggregate_failures");
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        string font = sorted[i];
                        WriteTsvRow(
                            writer,
                            font,
                            GetCounter(allocatedByFont, font).ToString(),
                            GetCounter(failuresByFont, font).ToString());
                    }
                }
            }

            private static LobbyFontGlyphRequirement GetOrAddRequirement(
                Dictionary<string, LobbyFontGlyphRequirement> requirements,
                string fontPath)
            {
                LobbyFontGlyphRequirement requirement;
                if (!requirements.TryGetValue(fontPath, out requirement))
                {
                    requirement = new LobbyFontGlyphRequirement(fontPath);
                    requirements.Add(fontPath, requirement);
                }

                return requirement;
            }

            private static HashSet<string> GetOrAddStringSet(Dictionary<string, HashSet<string>> map, string key)
            {
                HashSet<string> value;
                if (!map.TryGetValue(key, out value))
                {
                    value = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map.Add(key, value);
                }

                return value;
            }

            private static HashSet<uint> GetOrAddCodepointSet(Dictionary<string, HashSet<uint>> map, string key)
            {
                HashSet<uint> value;
                if (!map.TryGetValue(key, out value))
                {
                    value = new HashSet<uint>();
                    map.Add(key, value);
                }

                return value;
            }

            private static void IncrementCounter(Dictionary<string, int> counters, string key)
            {
                int value;
                counters.TryGetValue(key, out value);
                counters[key] = value + 1;
            }

            private static int GetCounter(Dictionary<string, int> counters, string key)
            {
                int value;
                return counters.TryGetValue(key, out value) ? value : 0;
            }

            private sealed class LobbyAtlasAllocator
            {
                private readonly Dictionary<string, LobbyTextureAllocator> _textures =
                    new Dictionary<string, LobbyTextureAllocator>(StringComparer.OrdinalIgnoreCase);

                public int TextureCount
                {
                    get { return _textures.Count; }
                }

                public void AddTexture(string path, Texture texture)
                {
                    _textures[path] = new LobbyTextureAllocator(texture);
                }

                public void MarkOccupied(string texturePath, int channel, int x, int y, int width, int height)
                {
                    LobbyTextureAllocator texture;
                    if (_textures.TryGetValue(texturePath, out texture))
                    {
                        texture.MarkOccupied(channel, x, y, width, height);
                    }
                }

                public bool TryAllocate(int width, int height)
                {
                    for (int i = 0; i < LobbyAtlasTexturePaths.Length; i++)
                    {
                        LobbyTextureAllocator texture;
                        if (_textures.TryGetValue(LobbyAtlasTexturePaths[i], out texture) &&
                            texture.TryAllocate(width, height))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                public LobbyAtlasAllocator Clone()
                {
                    LobbyAtlasAllocator clone = new LobbyAtlasAllocator();
                    foreach (KeyValuePair<string, LobbyTextureAllocator> pair in _textures)
                    {
                        clone._textures.Add(pair.Key, pair.Value.Clone());
                    }

                    return clone;
                }
            }

            private sealed class LobbyTextureAllocator
            {
                private readonly int _width;
                private readonly int _height;
                private readonly bool[][] _occupied;

                public LobbyTextureAllocator(Texture texture)
                {
                    _width = texture.Width;
                    _height = texture.Height;
                    _occupied = new bool[4][];
                    int pixels = checked(_width * _height);
                    for (int channel = 0; channel < _occupied.Length; channel++)
                    {
                        _occupied[channel] = new bool[pixels];
                    }

                    int pixel = 0;
                    for (int y = 0; y < _height; y++)
                    {
                        for (int x = 0; x < _width; x++)
                        {
                            int offset = pixel * 2;
                            byte lo = texture.Data[offset];
                            byte hi = texture.Data[offset + 1];
                            if ((hi & 0x0F) != 0)
                            {
                                _occupied[0][pixel] = true;
                            }

                            if ((lo & 0xF0) != 0)
                            {
                                _occupied[1][pixel] = true;
                            }

                            if ((lo & 0x0F) != 0)
                            {
                                _occupied[2][pixel] = true;
                            }

                            if ((hi & 0xF0) != 0)
                            {
                                _occupied[3][pixel] = true;
                            }

                            pixel++;
                        }
                    }
                }

                private LobbyTextureAllocator(int width, int height, bool[][] occupied)
                {
                    _width = width;
                    _height = height;
                    _occupied = occupied;
                }

                public LobbyTextureAllocator Clone()
                {
                    bool[][] occupied = new bool[_occupied.Length][];
                    for (int i = 0; i < _occupied.Length; i++)
                    {
                        occupied[i] = new bool[_occupied[i].Length];
                        Array.Copy(_occupied[i], occupied[i], _occupied[i].Length);
                    }

                    return new LobbyTextureAllocator(_width, _height, occupied);
                }

                public void MarkOccupied(int channel, int x, int y, int width, int height)
                {
                    if (channel < 0 || channel >= _occupied.Length)
                    {
                        return;
                    }

                    int left = Math.Max(0, x);
                    int top = Math.Max(0, y);
                    int right = Math.Min(_width, x + Math.Max(0, width));
                    int bottom = Math.Min(_height, y + Math.Max(0, height));
                    for (int yy = top; yy < bottom; yy++)
                    {
                        int row = yy * _width;
                        for (int xx = left; xx < right; xx++)
                        {
                            _occupied[channel][row + xx] = true;
                        }
                    }
                }

                public bool TryAllocate(int width, int height)
                {
                    int w = Math.Max(1, width);
                    int h = Math.Max(1, height);
                    int stepX = Math.Max(1, w + 2);
                    int stepY = Math.Max(1, h + 2);
                    for (int channel = 0; channel < _occupied.Length; channel++)
                    {
                        for (int y = _height - h - 1; y >= 0; y -= stepY)
                        {
                            for (int x = 0; x <= _width - w; x += stepX)
                            {
                                if (!IsFree(channel, x, y, w, h))
                                {
                                    continue;
                                }

                                MarkOccupied(channel, x, y, w, h);
                                return true;
                            }
                        }
                    }

                    return false;
                }

                private bool IsFree(int channel, int x, int y, int width, int height)
                {
                    for (int yy = 0; yy < height; yy++)
                    {
                        int row = (y + yy) * _width;
                        for (int xx = 0; xx < width; xx++)
                        {
                            if (_occupied[channel][row + x + xx])
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            private sealed class LobbyCoverageGroup
            {
                public readonly string Name;
                public readonly string[] Screens;
                public readonly LobbyCoverageRowSpec[] Rows;

                public LobbyCoverageGroup(string name, string[] screens, LobbyCoverageRowSpec[] rows)
                {
                    Name = name;
                    Screens = screens;
                    Rows = rows;
                }
            }

            private sealed class LobbyCoverageRowSpec
            {
                public readonly string Sheet;
                public readonly uint StartRowId;
                public readonly uint EndRowId;
                public readonly int? ColumnOffset;

                public LobbyCoverageRowSpec(string sheet, uint startRowId, uint endRowId)
                    : this(sheet, startRowId, endRowId, null)
                {
                }

                public LobbyCoverageRowSpec(string sheet, uint startRowId, uint endRowId, int? columnOffset)
                {
                    Sheet = sheet;
                    StartRowId = startRowId;
                    EndRowId = endRowId;
                    ColumnOffset = columnOffset;
                }
            }

            private sealed class LobbyCoverageTextRow
            {
                public string Sheet;
                public uint RowId;
                public int ColumnOffset;
                public string Text;
                public HashSet<uint> Codepoints;
            }

            private sealed class LobbyFontGlyphRequirement
            {
                public readonly string FontPath;
                public readonly HashSet<string> Screens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                public readonly HashSet<string> Groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                public readonly HashSet<uint> Codepoints = new HashSet<uint>();
                public readonly Dictionary<string, HashSet<uint>> CodepointsByGroup =
                    new Dictionary<string, HashSet<uint>>(StringComparer.OrdinalIgnoreCase);

                public LobbyFontGlyphRequirement(string fontPath)
                {
                    FontPath = fontPath;
                }
            }

            private sealed class LobbyAtlasCapacityStats
            {
                public int TargetFonts;
                public int RequiredCodepoints;
                public int MissingTargetGlyphs;
                public int SourceCoveredGlyphs;
                public int AggregateAllocationFailures;
                public int ActionableCandidateRanges;
                public int ActionableCandidateMissingTargetGlyphs;
                public int ActionableCandidateAggregateAllocationFailures;
            }

            private sealed class LobbyActionableCapacityStats
            {
                public int Ranges;
                public int FontRows;
                public int RequiredCodepoints;
                public int MissingTargetGlyphs;
                public int SourceCoveredGlyphs;
                public int AggregateAllocationFailures;
            }

            private sealed class LobbyActionableFontCapacity
            {
                public bool DiagnosticOnly;
                public int Required;
                public int AlreadyPresent;
                public int MissingTarget;
                public int SourceCovered;
                public int SourceMissing;
                public int IndependentAllocated;
                public int IndependentFailures;
                public int AggregateNewMissing;
                public int AggregateReused;
                public int AggregateAllocated;
                public int AggregateFailures;
            }

            private sealed class LobbyActionableCoverageCandidate
            {
                public string Sheet;
                public uint RowId;
                public int ColumnOffset;
                public string Priority;
                public string Reasons;
                public string Text;
                public HashSet<uint> Codepoints;
                public string NearestCoverageGroup;
                public string[] Screens;
            }

            private sealed class LobbyActionableCoverageRange
            {
                public string Name;
                public readonly string Sheet;
                public readonly int ColumnOffset;
                public readonly string NearestCoverageGroup;
                public readonly string[] Screens;
                public uint StartRowId;
                public uint EndRowId;
                public int CandidateColumns;
                public int HangulChars;
                public readonly HashSet<uint> Codepoints = new HashSet<uint>();
                public readonly HashSet<string> Reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                public LobbyActionableCoverageRange(LobbyActionableCoverageCandidate candidate)
                {
                    Sheet = candidate.Sheet;
                    ColumnOffset = candidate.ColumnOffset;
                    NearestCoverageGroup = candidate.NearestCoverageGroup;
                    Screens = candidate.Screens;
                    StartRowId = candidate.RowId;
                    EndRowId = candidate.RowId;
                    Append(candidate);
                }

                public bool CanAppend(LobbyActionableCoverageCandidate candidate)
                {
                    return string.Equals(Sheet, candidate.Sheet, StringComparison.OrdinalIgnoreCase) &&
                           ColumnOffset == candidate.ColumnOffset &&
                           string.Equals(NearestCoverageGroup, candidate.NearestCoverageGroup, StringComparison.OrdinalIgnoreCase) &&
                           SameScreens(Screens, candidate.Screens) &&
                           candidate.RowId == EndRowId + 1;
                }

                public void Append(LobbyActionableCoverageCandidate candidate)
                {
                    if (candidate.RowId < StartRowId)
                    {
                        StartRowId = candidate.RowId;
                    }

                    if (candidate.RowId > EndRowId)
                    {
                        EndRowId = candidate.RowId;
                    }

                    CandidateColumns++;
                    HangulChars += CountHangulChars(candidate.Text);
                    AddRange(Codepoints, candidate.Codepoints);
                    AddActionableReasons(candidate.Reasons);
                }

                public void SetName(int ordinal)
                {
                    Name = "actionable-" + ordinal.ToString("000") + "-" +
                        NearestCoverageGroup + "-" +
                        Sheet + "-" +
                        StartRowId.ToString() + "-" +
                        EndRowId.ToString() + "-col" +
                        ColumnOffset.ToString();
                }

                private void AddActionableReasons(string reasons)
                {
                    if (string.IsNullOrEmpty(reasons))
                    {
                        return;
                    }

                    string[] parts = reasons.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        Reasons.Add(parts[i]);
                    }
                }

                private static bool SameScreens(string[] left, string[] right)
                {
                    if (left == null || right == null)
                    {
                        return left == right;
                    }

                    if (left.Length != right.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            private sealed class LobbySourceGlyph
            {
                public string SourcePath;
                public string TexturePath;
                public int ImageIndex;
                public int Channel;
                public int X;
                public int Y;
                public int Width;
                public int Height;
                public int OffsetX;
                public int OffsetY;
            }

            private struct LobbySourceCandidate
            {
                public readonly string FontPath;
                public readonly bool FromTtmp;

                public LobbySourceCandidate(string fontPath, bool fromTtmp)
                {
                    FontPath = fontPath;
                    FromTtmp = fromTtmp;
                }
            }
        }
    }
}
