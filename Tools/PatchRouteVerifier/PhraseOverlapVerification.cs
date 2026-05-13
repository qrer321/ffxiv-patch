using System;
using System.Collections.Generic;
using System.Text;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifySystemSettingsScaledPhraseLayouts()
            {
                Console.WriteLine("[FDT] System settings scaled phrase layout");
                for (int fontIndex = 0; fontIndex < SystemSettingsScaledFonts.Length; fontIndex++)
                {
                    for (int phraseIndex = 0; phraseIndex < SystemSettingsScaledPhrases.Length; phraseIndex++)
                    {
                        VerifyNoPhraseOverlap(SystemSettingsScaledFonts[fontIndex], SystemSettingsScaledPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifySystemSettingsMixedScalePhraseLayouts()
            {
                Console.WriteLine("[FDT] System settings mixed high-scale phrase layout");
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string fontPath = Derived4kLobbyFontPairs[i, 0];
                    string asciiSourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPath);
                    string asciiFallbackSourceFontPath = ResolveDerived4kLobbyFallbackAsciiReferenceFontPath(fontPath);
                    for (int phraseIndex = 0; phraseIndex < SystemSettingsMixedScalePhrases.Length; phraseIndex++)
                    {
                        VerifyMixedScalePhraseLayout(fontPath, asciiSourceFontPath, asciiFallbackSourceFontPath, SystemSettingsMixedScalePhrases[phraseIndex]);
                    }
                }
            }

            private void Verify4kLobbyPhraseLayouts()
            {
                Console.WriteLine("[FDT] 4K lobby phrase layout");
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string fontPath = Derived4kLobbyFontPairs[i, 0];
                    string sourceFontPath = Derived4kLobbyFontPairs[i, 1];
                    for (int phraseIndex = 0; phraseIndex < FourKLobbyPhrases.Length; phraseIndex++)
                    {
                        VerifyNoDerived4kLobbyPhraseOverlap(fontPath, sourceFontPath, FourKLobbyPhrases[phraseIndex]);
                    }
                }
            }

            private void VerifyStartScreenMainMenuPhraseLayouts()
            {
                Console.WriteLine("[FDT] Start-screen main menu phrase layout");
                if (_ttmpFont == null)
                {
                    Fail("TTMP font package is required to verify start-screen main menu phrase layouts");
                    return;
                }

                for (int fontIndex = 0; fontIndex < StartScreenMainMenuFontPairs.Length; fontIndex++)
                {
                    FontPair pair = StartScreenMainMenuFontPairs[fontIndex];
                    for (int phraseIndex = 0; phraseIndex < LobbyScaledHangulPhrases.StartScreenMainMenu.Length; phraseIndex++)
                    {
                        VerifyStartScreenMainMenuPhraseLayout(pair, LobbyScaledHangulPhrases.StartScreenMainMenu[phraseIndex]);
                    }
                }

                VerifyStartScreenMainMenuUldRoutes();
            }

            private void VerifyStartScreenMainMenuUldRoutes()
            {
                Console.WriteLine("[ULD/FDT] Start-screen main menu font routes");
                int found = 0;
                HashSet<string> checkedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int candidateIndex = 0; candidateIndex < StartScreenMainMenuUldCandidates.Length; candidateIndex++)
                {
                    UldRouteCandidate candidate = StartScreenMainMenuUldCandidates[candidateIndex];
                    HashSet<string> routedFontPaths;
                    if (!TryCollectOptionalPreservedUldFontRoutes(
                        candidate.Path,
                        "start-screen main menu",
                        candidate.UsesLobbyFonts,
                        out routedFontPaths))
                    {
                        continue;
                    }

                    found++;
                    foreach (string fontPath in routedFontPaths)
                    {
                        if (!checkedRoutes.Add(fontPath))
                        {
                            continue;
                        }

                        for (int phraseIndex = 0; phraseIndex < LobbyScaledHangulPhrases.StartScreenMainMenu.Length; phraseIndex++)
                        {
                            VerifyStartScreenMainMenuRoutedPhraseLayout(fontPath, LobbyScaledHangulPhrases.StartScreenMainMenu[phraseIndex]);
                        }
                    }
                }

                if (found == 0)
                {
                    Fail("No start-screen main menu ULD candidate was found; verifier is not covering the live title menu route");
                }
                else
                {
                    Pass("start-screen main menu ULD candidates found: {0}", found);
                }
            }

            private void VerifyStartScreenMainMenuRoutedPhraseLayout(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} start main-menu routed phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (!VerifyPhraseMinimumVisualGap(fontPath, phrase, layout, ResolveCleanAsciiReferenceFontPath(fontPath)))
                {
                    return;
                }

                Pass(
                    "{0} start main-menu routed phrase [{1}] layout width={2}, maxGap={3}, minGap={4}",
                    fontPath,
                    Escape(phrase),
                    layout.Width,
                    layout.MaximumGapPixels,
                    layout.MinimumGapPixels);
            }

            private void VerifyStartScreenMainMenuPhraseLayout(FontPair pair, string phrase)
            {
                const int widthTolerancePixels = 8;
                const int maxGapTolerancePixels = 4;

                PhraseLayoutResult targetLayout;
                string targetError;
                if (!TryMeasurePhraseLayout(_patchedFont, pair.TargetFontPath, phrase, true, out targetLayout, out targetError))
                {
                    Fail("{0} start main-menu phrase [{1}] layout error: {2}", pair.TargetFontPath, Escape(phrase), targetError);
                    return;
                }

                PhraseLayoutResult sourceLayout;
                string sourceError = "source font is missing";
                if (!_ttmpFont.ContainsPath(pair.SourceFontPath) ||
                    !TryMeasurePhraseLayout(_ttmpFont, pair.SourceFontPath, phrase, out sourceLayout, out sourceError))
                {
                    Fail("{0} start main-menu phrase [{1}] source layout error from {2}: {3}", pair.TargetFontPath, Escape(phrase), pair.SourceFontPath, sourceError);
                    return;
                }

                if (targetLayout.OverlapPixels > sourceLayout.OverlapPixels)
                {
                    Fail(
                        "{0} start main-menu phrase [{1}] overlap {2} exceeds source {3} from {4}",
                        pair.TargetFontPath,
                        Escape(phrase),
                        targetLayout.OverlapPixels,
                        sourceLayout.OverlapPixels,
                        pair.SourceFontPath);
                    return;
                }

                int normalizedSourceWidth = sourceLayout.Width;
                bool hasAdvanceNormalizedWidth =
                    IsLobbyAxisHangulAdvanceNormalizedFont(pair.TargetFontPath) &&
                    TryComputeLobbyAxisAdvanceNormalizedWidth(
                        pair.TargetFontPath,
                        pair.SourceFontPath,
                        phrase,
                        sourceLayout,
                        out normalizedSourceWidth,
                        out sourceError);
                int widthTolerance = hasAdvanceNormalizedWidth ? 9 : widthTolerancePixels;
                if (IsLobbyFontPath(pair.TargetFontPath))
                {
                    widthTolerance += CountHangulCodepointsInPhrase(phrase) * 2;
                }

                int allowedWidth = normalizedSourceWidth + widthTolerance;
                if (targetLayout.Width > allowedWidth)
                {
                    Fail(
                        "{0} start main-menu phrase [{1}] width {2} exceeds source {3}+{4} from {5}",
                        pair.TargetFontPath,
                        Escape(phrase),
                        targetLayout.Width,
                        normalizedSourceWidth,
                        widthTolerance,
                        pair.SourceFontPath);
                    return;
                }

                int allowedMaxGap = sourceLayout.MaximumGapPixels + maxGapTolerancePixels;
                if (targetLayout.MaximumGapPixels > allowedMaxGap)
                {
                    Fail(
                        "{0} start main-menu phrase [{1}] maxGap {2} exceeds source {3}+{4} from {5}, pair=U+{6:X4}/U+{7:X4}",
                        pair.TargetFontPath,
                        Escape(phrase),
                        targetLayout.MaximumGapPixels,
                        sourceLayout.MaximumGapPixels,
                        maxGapTolerancePixels,
                        pair.SourceFontPath,
                        targetLayout.MaximumGapLeftCodepoint,
                        targetLayout.MaximumGapRightCodepoint);
                    return;
                }

                Pass(
                    "{0} start main-menu phrase [{1}] layout width={2}/{3}, maxGap={4}/{5}, minGap={6}",
                    pair.TargetFontPath,
                    Escape(phrase),
                    targetLayout.Width,
                    normalizedSourceWidth,
                    targetLayout.MaximumGapPixels,
                    sourceLayout.MaximumGapPixels,
                    targetLayout.MinimumGapPixels);
            }

            private static int CountHangulCodepointsInPhrase(string phrase)
            {
                int count = 0;
                string value = phrase ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    uint codepoint = ReadCodepoint(value, ref i);
                    if (IsHangulCodepoint(codepoint))
                    {
                        count++;
                    }
                }

                return count;
            }

            private void VerifyCharacterSelectLobbyPhraseLayouts()
            {
                Console.WriteLine("[FDT] Character-select lobby phrase layout");
                for (int fontIndex = 0; fontIndex < CharacterSelectLobbyFontPaths.Length; fontIndex++)
                {
                    string fontPath = CharacterSelectLobbyFontPaths[fontIndex];
                    for (int phraseIndex = 0; phraseIndex < LobbyScaledHangulPhrases.CharacterSelect.Length; phraseIndex++)
                    {
                        VerifyCharacterSelectLobbyPhraseLayout(fontPath, LobbyScaledHangulPhrases.CharacterSelect[phraseIndex]);
                    }
                }

                HashSet<string> routedFontPaths = VerifyCharacterSelectLobbyUldRoutes();
                if (routedFontPaths.Count > 0)
                {
                    VerifyLobbySheetGlyphCoverage(
                        "character-select lobby",
                        ToStringArray(routedFontPaths),
                        LobbyScaledHangulPhrases.CharacterSelectSheetRowRanges);
                }
            }

            private HashSet<string> VerifyCharacterSelectLobbyUldRoutes()
            {
                Console.WriteLine("[ULD/FDT] Character-select lobby font routes");
                int found = 0;
                HashSet<string> checkedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> routedFontPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int candidateIndex = 0; candidateIndex < CharacterSelectLobbyUldCandidates.Length; candidateIndex++)
                {
                    UldRouteCandidate candidate = CharacterSelectLobbyUldCandidates[candidateIndex];
                    HashSet<string> candidateFontPaths;
                    if (!TryCollectOptionalPreservedUldFontRoutes(
                        candidate.Path,
                        "character-select lobby",
                        candidate.UsesLobbyFonts,
                        out candidateFontPaths))
                    {
                        continue;
                    }

                    found++;
                    foreach (string fontPath in candidateFontPaths)
                    {
                        routedFontPaths.Add(fontPath);
                        if (!checkedRoutes.Add(fontPath))
                        {
                            continue;
                        }

                        for (int phraseIndex = 0; phraseIndex < LobbyScaledHangulPhrases.CharacterSelect.Length; phraseIndex++)
                        {
                            VerifyCharacterSelectLobbyPhraseLayout(fontPath, LobbyScaledHangulPhrases.CharacterSelect[phraseIndex]);
                        }
                    }
                }

                if (found == 0)
                {
                    Fail("No character-select lobby ULD candidate was found; verifier is not covering the live character-select route");
                }
                else
                {
                    Pass("character-select lobby ULD candidates found: {0}", found);
                }

                return routedFontPaths;
            }

            private static string[] ToStringArray(HashSet<string> values)
            {
                string[] result = new string[values.Count];
                values.CopyTo(result);
                Array.Sort(result, StringComparer.OrdinalIgnoreCase);
                return result;
            }

            private void VerifyCharacterSelectLobbyPhraseLayout(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} character-select phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (!VerifyPhraseMinimumVisualGap(fontPath, phrase, layout, ResolveCleanAsciiReferenceFontPath(fontPath)))
                {
                    return;
                }

                Pass(
                    "{0} character-select phrase [{1}] layout width={2}, maxGap={3}, minGap={4}",
                    fontPath,
                    Escape(phrase),
                    layout.Width,
                    layout.MaximumGapPixels,
                    layout.MinimumGapPixels);
            }

            private void VerifyLobbySheetGlyphCoverage(string label, string[] fontPaths, SheetRowRange[] ranges)
            {
                HashSet<uint> codepoints = CollectPatchedSheetHangulCodepoints(ranges, label);
                if (codepoints.Count == 0)
                {
                    Fail("{0} sheet glyph coverage collected no Hangul codepoints", label);
                    return;
                }

                for (int fontIndex = 0; fontIndex < fontPaths.Length; fontIndex++)
                {
                    string fontPath = fontPaths[fontIndex];
                    byte[] fdt;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} sheet glyph coverage cannot read {1}: {2}", label, fontPath, ex.Message);
                        continue;
                    }

                    int failures = 0;
                    List<string> samples = new List<string>();
                    foreach (uint codepoint in codepoints)
                    {
                        FdtGlyphEntry ignored;
                        if (!TryFindGlyph(fdt, codepoint, out ignored))
                        {
                            AddGlyphCoverageFailure(samples, codepoint, "missing");
                            failures++;
                            continue;
                        }

                        try
                        {
                            GlyphCanvas glyph = RenderGlyph(_patchedFont, fontPath, codepoint);
                            if (glyph.VisiblePixels <= 0)
                            {
                                AddGlyphCoverageFailure(samples, codepoint, "invisible");
                                failures++;
                                continue;
                            }

                            if (GlyphMatchesFallback(fontPath, glyph, codepoint, '-') ||
                                GlyphMatchesFallback(fontPath, glyph, codepoint, '='))
                            {
                                AddGlyphCoverageFailure(samples, codepoint, "fallback");
                                failures++;
                            }
                        }
                        catch (Exception ex)
                        {
                            AddGlyphCoverageFailure(samples, codepoint, "error:" + ex.Message);
                            failures++;
                        }
                    }

                    if (failures > 0)
                    {
                        Fail(
                            "{0} sheet glyph coverage failed for {1}: failures={2}/{3}, samples={4}",
                            label,
                            fontPath,
                            failures,
                            codepoints.Count,
                            string.Join(", ", samples.ToArray()));
                    }
                    else
                    {
                        Pass("{0} sheet glyph coverage OK for {1}: codepoints={2}", label, fontPath, codepoints.Count);
                    }
                }
            }

            private static void AddGlyphCoverageFailure(List<string> samples, uint codepoint, string reason)
            {
                if (samples.Count >= 24)
                {
                    return;
                }

                samples.Add("U+" + codepoint.ToString("X4") + "(" + CodepointToString(codepoint) + "):" + reason);
            }

            private HashSet<uint> CollectPatchedSheetHangulCodepoints(SheetRowRange[] ranges, string label)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                if (ranges == null || ranges.Length == 0)
                {
                    return codepoints;
                }

                List<string> sheetNames = CollectSheetNames(ranges);
                for (int sheetIndex = 0; sheetIndex < sheetNames.Count; sheetIndex++)
                {
                    string sheetName = sheetNames[sheetIndex];
                    ExcelHeader header;
                    try
                    {
                        header = ExcelHeader.Parse(_patchedText.ReadFile("exd/" + sheetName + ".exh"));
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} sheet glyph coverage cannot read {1}.exh: {2}", label, sheetName, ex.Message);
                        continue;
                    }

                    if (header.Variant != ExcelVariant.Default)
                    {
                        Fail("{0} sheet glyph coverage does not support {1} variant {2}", label, sheetName, header.Variant);
                        continue;
                    }

                    byte languageId = LanguageToId(_language);
                    bool hasLanguageSuffix = header.HasLanguage(languageId);
                    List<int> stringColumns = header.GetStringColumnIndexes();
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        if (!SheetPageOverlaps(sheetName, page, ranges))
                        {
                            continue;
                        }

                        string exdPath = BuildExdPath(sheetName, page.StartId, _language, hasLanguageSuffix);
                        ExcelDataFile file;
                        try
                        {
                            file = ExcelDataFile.Parse(_patchedText.ReadFile(exdPath));
                        }
                        catch (Exception ex)
                        {
                            Fail("{0} sheet glyph coverage cannot read {1}: {2}", label, exdPath, ex.Message);
                            continue;
                        }

                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            if (!RowInSheetRanges(sheetName, row.RowId, ranges))
                            {
                                continue;
                            }

                            for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                            {
                                byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]);
                                if (bytes == null || bytes.Length == 0)
                                {
                                    continue;
                                }

                                AddHangulCodepoints(codepoints, Encoding.UTF8.GetString(bytes));
                            }
                        }
                    }
                }

                return codepoints;
            }

            private static List<string> CollectSheetNames(SheetRowRange[] ranges)
            {
                List<string> sheetNames = new List<string>();
                for (int rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
                {
                    string sheetName = ranges[rangeIndex].SheetName ?? string.Empty;
                    if (sheetName.Length == 0 || ContainsOrdinalIgnoreCase(sheetNames, sheetName))
                    {
                        continue;
                    }

                    sheetNames.Add(sheetName);
                }

                return sheetNames;
            }

            private static bool ContainsOrdinalIgnoreCase(List<string> values, string value)
            {
                for (int index = 0; index < values.Count; index++)
                {
                    if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool SheetPageOverlaps(string sheetName, ExcelPageDefinition page, SheetRowRange[] ranges)
            {
                uint pageEnd = page.RowCount == 0 ? page.StartId : page.StartId + page.RowCount - 1;
                for (int rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
                {
                    if (string.Equals(ranges[rangeIndex].SheetName, sheetName, StringComparison.OrdinalIgnoreCase) &&
                        ranges[rangeIndex].StartId <= pageEnd &&
                        ranges[rangeIndex].EndId >= page.StartId)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool RowInSheetRanges(string sheetName, uint rowId, SheetRowRange[] ranges)
            {
                for (int rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
                {
                    if (ranges[rangeIndex].Contains(sheetName, rowId))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void AddHangulCodepoints(HashSet<uint> codepoints, string value)
            {
                value = value ?? string.Empty;
                for (int charIndex = 0; charIndex < value.Length; charIndex++)
                {
                    uint codepoint = ReadCodepoint(value, ref charIndex);
                    if (IsHangulCodepoint(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }
            }

            private static string CodepointToString(uint codepoint)
            {
                try
                {
                    return char.ConvertFromUtf32(checked((int)codepoint));
                }
                catch
                {
                    return "?";
                }
            }

            private void VerifyMixedScalePhraseLayout(string fontPath, string asciiSourceFontPath, string asciiFallbackSourceFontPath, string phrase)
            {
                const int antiAliasOverlapTolerance = 2;
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} high-scale mixed phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                byte[] cleanTargetFdt;
                byte[] cleanAsciiSourceFdt;
                byte[] cleanAsciiFallbackSourceFdt = null;
                byte[] patchedFdt;
                try
                {
                    cleanTargetFdt = _cleanFont.ReadFile(fontPath);
                    cleanAsciiSourceFdt = _cleanFont.ReadFile(asciiSourceFontPath);
                    if (!string.IsNullOrEmpty(asciiFallbackSourceFontPath) &&
                        !string.Equals(asciiFallbackSourceFontPath, asciiSourceFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        cleanAsciiFallbackSourceFdt = _cleanFont.ReadFile(asciiFallbackSourceFontPath);
                    }

                    patchedFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} high-scale mixed phrase [{1}] FDT read error: {2}", fontPath, Escape(phrase), ex.Message);
                    return;
                }

                int cjkMedianAdvance;
                int cjkSamples;
                bool hasCjkBaseline = TryComputeMedianCjkAdvance(cleanTargetFdt, out cjkMedianAdvance, out cjkSamples);

                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (IsPhraseLayoutSpace(codepoint))
                    {
                        continue;
                    }

                    FdtGlyphEntry patchedGlyph;
                    if (!TryFindGlyph(patchedFdt, codepoint, out patchedGlyph))
                    {
                        Fail("{0} high-scale mixed phrase [{1}] missing patched U+{2:X4}", fontPath, Escape(phrase), codepoint);
                        return;
                    }

                    if (codepoint <= 0x7Eu)
                    {
                        FdtGlyphEntry cleanGlyph;
                        string cleanGlyphFontPath = asciiSourceFontPath;
                        if (!TryFindGlyph(cleanAsciiSourceFdt, codepoint, out cleanGlyph))
                        {
                            cleanGlyphFontPath = asciiFallbackSourceFontPath;
                            if (cleanAsciiFallbackSourceFdt == null ||
                                !TryFindGlyph(cleanAsciiFallbackSourceFdt, codepoint, out cleanGlyph))
                            {
                                cleanGlyphFontPath = fontPath;
                                if (!TryFindGlyph(cleanTargetFdt, codepoint, out cleanGlyph))
                                {
                                    Fail("{0} high-scale mixed phrase [{1}] missing clean ASCII U+{2:X4} in {3} and fallback {4}", fontPath, Escape(phrase), codepoint, fontPath, asciiSourceFontPath);
                                    return;
                                }
                            }
                        }

                        if (!GlyphSpacingMetricsMatchOrLobbySafe(fontPath, codepoint, cleanGlyph, patchedGlyph))
                        {
                            Fail(
                                "{0} high-scale mixed phrase [{1}] ASCII U+{2:X4} metrics differ from clean {3}: target={4}, clean={5}",
                                fontPath,
                                Escape(phrase),
                                codepoint,
                                cleanGlyphFontPath,
                                FormatGlyphSpacing(patchedGlyph),
                                FormatGlyphSpacing(cleanGlyph));
                            return;
                        }

                        if (!VerifyGlyphTextureNeighborhoodMatchesClean(cleanGlyphFontPath, fontPath, codepoint, DataCenterGlyphTexturePadding, out error))
                        {
                            Fail(
                                "{0} high-scale mixed phrase [{1}] ASCII U+{2:X4} texture padding differs from clean {3}: {4}",
                                fontPath,
                                Escape(phrase),
                                codepoint,
                                cleanGlyphFontPath,
                                error);
                            return;
                        }

                        continue;
                    }

                    if (hasCjkBaseline && IsHangulCodepoint(codepoint))
                    {
                        continue;
                    }
                }

                if (layout.OverlapPixels > 0)
                {
                    int cleanAsciiOverlap;
                    if (TryMeasureCleanAsciiPhraseOverlap(asciiSourceFontPath, asciiFallbackSourceFontPath, phrase, out cleanAsciiOverlap, out error))
                    {
                        if (layout.OverlapPixels > cleanAsciiOverlap + antiAliasOverlapTolerance)
                        {
                            Fail("{0} high-scale mixed phrase [{1}] has overlap pixels={2}, clean ASCII baseline={3}", fontPath, Escape(phrase), layout.OverlapPixels, cleanAsciiOverlap);
                            return;
                        }
                    }
                    else if (layout.OverlapPixels > antiAliasOverlapTolerance)
                    {
                        Fail("{0} high-scale mixed phrase [{1}] has overlap pixels={2}, clean ASCII baseline={3}", fontPath, Escape(phrase), layout.OverlapPixels, error);
                        return;
                    }
                }

                if (!VerifyMixedScalePhraseMinimumVisualGap(fontPath, phrase, layout, asciiSourceFontPath, asciiFallbackSourceFontPath))
                {
                    return;
                }

                Pass(
                    "{0} high-scale mixed phrase [{1}] layout glyphs={2}, width={3}, minGap={4}, cleanCjkMedian={5}",
                    fontPath,
                    Escape(phrase),
                    layout.Glyphs,
                    layout.Width,
                    layout.MinimumGapPixels,
                    hasCjkBaseline ? cjkMedianAdvance.ToString() : "n/a");
            }

            private bool TryMeasureCleanAsciiPhraseOverlap(string fontPath, string fallbackFontPath, string phrase, out int overlapPixels, out string error)
            {
                overlapPixels = 0;
                PhraseLayoutResult cleanLayout;
                if (!TryMeasurePhraseLayout(_cleanFont, fontPath, ToAsciiOnlyPhrase(phrase), false, out cleanLayout, out error))
                {
                    if (string.IsNullOrEmpty(fallbackFontPath) ||
                        string.Equals(fallbackFontPath, fontPath, StringComparison.OrdinalIgnoreCase) ||
                        !TryMeasurePhraseLayout(_cleanFont, fallbackFontPath, ToAsciiOnlyPhrase(phrase), false, out cleanLayout, out error))
                    {
                        return false;
                    }
                }

                overlapPixels = cleanLayout.OverlapPixels;
                return true;
            }

            private static string ToAsciiOnlyPhrase(string phrase)
            {
                char[] chars = (phrase ?? string.Empty).ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (chars[i] > 0x7E)
                    {
                        chars[i] = ' ';
                    }
                }

                return new string(chars);
            }

            private static string ResolveDerived4kLobbyFallbackAsciiReferenceFontPath(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    if (string.Equals(normalized, Derived4kLobbyFontPairs[i, 0], StringComparison.OrdinalIgnoreCase))
                    {
                        return Derived4kLobbyFontPairs[i, 1];
                    }
                }

                return null;
            }

            private void VerifyNoDerived4kLobbyPhraseOverlap(string fontPath, string sourceFontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (layout.OverlapPixels == 0)
                {
                    if (ShouldVerifyLobbyAsciiVisualGap(fontPath, phrase) &&
                        !VerifyPhraseMinimumVisualGap(fontPath, phrase, layout, ResolveCleanAsciiReferenceFontPath(fontPath)))
                    {
                        return;
                    }

                    Pass("{0} phrase [{1}] layout glyphs={2}, width={3}, minGap={4}", fontPath, Escape(phrase), layout.Glyphs, layout.Width, layout.MinimumGapPixels);
                    return;
                }

                if (layout.OverlapPixels <= 1)
                {
                    if (ShouldVerifyLobbyAsciiVisualGap(fontPath, phrase) &&
                        !VerifyPhraseMinimumVisualGap(fontPath, phrase, layout, ResolveCleanAsciiReferenceFontPath(fontPath)))
                    {
                        return;
                    }

                    Pass("{0} phrase [{1}] layout glyphs={2}, width={3}, minGap={4}, overlap={5} within anti-alias tolerance", fontPath, Escape(phrase), layout.Glyphs, layout.Width, layout.MinimumGapPixels, layout.OverlapPixels);
                    return;
                }

                PhraseLayoutResult sourceLayout;
                string sourceError = "source font is missing";
                if (TryMeasurePhraseLayout(_patchedFont, sourceFontPath, phrase, true, out sourceLayout, out sourceError) &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels)
                {
                    Pass(
                        "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches source {5} baseline={6}",
                        fontPath,
                        Escape(phrase),
                        layout.Glyphs,
                        layout.Width,
                        layout.OverlapPixels,
                        sourceFontPath,
                        sourceLayout.OverlapPixels);
                    return;
                }

                Fail("{0} phrase [{1}] has overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
            }

            private void VerifyNoPhraseOverlap(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (layout.OverlapPixels > 0)
                {
                    if (IsAsciiPhrase(phrase))
                    {
                        PhraseLayoutResult cleanLayout;
                        string cleanError;
                        if (TryMeasurePhraseLayout(_cleanFont, fontPath, phrase, false, out cleanLayout, out cleanError) &&
                            layout.OverlapPixels <= cleanLayout.OverlapPixels)
                        {
                            Pass(
                                "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches clean baseline={5}",
                                fontPath,
                                Escape(phrase),
                                layout.Glyphs,
                                layout.Width,
                                layout.OverlapPixels,
                                cleanLayout.OverlapPixels);
                            return;
                        }

                        Fail("{0} phrase [{1}] has ASCII overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
                        return;
                    }

                    PhraseLayoutResult sourceLayout;
                    string sourceError;
                    if (_ttmpFont != null &&
                        _ttmpFont.ContainsPath(fontPath) &&
                        TryMeasurePhraseLayout(_ttmpFont, fontPath, phrase, out sourceLayout, out sourceError) &&
                        layout.OverlapPixels <= sourceLayout.OverlapPixels)
                    {
                        Pass(
                            "{0} phrase [{1}] layout glyphs={2}, width={3}, overlap={4} matches TTMP baseline={5}",
                            fontPath,
                            Escape(phrase),
                            layout.Glyphs,
                            layout.Width,
                            layout.OverlapPixels,
                            sourceLayout.OverlapPixels);
                        return;
                    }

                    Fail("{0} phrase [{1}] has overlap pixels={2}", fontPath, Escape(phrase), layout.OverlapPixels);
                    return;
                }

                if (ShouldVerifyLobbyAsciiVisualGap(fontPath, phrase) &&
                    !VerifyPhraseMinimumVisualGap(fontPath, phrase, layout, ResolveCleanAsciiReferenceFontPath(fontPath)))
                {
                    return;
                }

                Pass("{0} phrase [{1}] layout glyphs={2}, width={3}, minGap={4}", fontPath, Escape(phrase), layout.Glyphs, layout.Width, layout.MinimumGapPixels);
            }

            private void VerifySystemSettingsScaledRoutePhraseLayout(string fontPath, string phrase, bool strictVisualGap)
            {
                if (!IsScaledLobbySystemSettingsFont(fontPath))
                {
                    if ((strictVisualGap || IsStartScreenHighRiskSystemSettingsPhrase(phrase)) &&
                        IsSystemSettingsStrictVisualFont(fontPath) &&
                        !VerifySystemSettingsStrictScaledRoutePhraseLayout(fontPath, phrase))
                    {
                        return;
                    }

                    VerifyNoPhraseOverlap(fontPath, phrase);
                    return;
                }

                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} system-settings phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                PhraseLayoutResult sourceLayout;
                string sourceError = "source font is missing";
                string sourceFontPath = ResolveLobbyHangulSourceFontPath(fontPath);
                if (_ttmpFont == null ||
                    !_ttmpFont.ContainsPath(sourceFontPath) ||
                    !TryMeasurePhraseLayout(_ttmpFont, sourceFontPath, phrase, out sourceLayout, out sourceError))
                {
                    Fail(
                        "{0} system-settings phrase [{1}] source layout error from {2}: {3}",
                        fontPath,
                        Escape(phrase),
                        sourceFontPath,
                        sourceError);
                    return;
                }

                if (layout.OverlapPixels > sourceLayout.OverlapPixels)
                {
                    Fail(
                        "{0} system-settings phrase [{1}] overlap {2} exceeds source {3} from {4}",
                        fontPath,
                        Escape(phrase),
                        layout.OverlapPixels,
                        sourceLayout.OverlapPixels,
                        sourceFontPath);
                    return;
                }

                int normalizedSourceWidth = sourceLayout.Width;
                bool hasAdvanceNormalizedWidth =
                    IsLobbyAxisHangulAdvanceNormalizedFont(fontPath) &&
                    TryComputeLobbyAxisAdvanceNormalizedWidth(
                        fontPath,
                        sourceFontPath,
                        phrase,
                        sourceLayout,
                        out normalizedSourceWidth,
                        out sourceError);
                int allowedSourceWidth = hasAdvanceNormalizedWidth ? normalizedSourceWidth : sourceLayout.Width;
                int widthTolerance = hasAdvanceNormalizedWidth ? 9 : 8;
                if (layout.Width > allowedSourceWidth + widthTolerance)
                {
                    Fail(
                        "{0} system-settings phrase [{1}] width {2} exceeds source {3}+{4} from {5}",
                        fontPath,
                        Escape(phrase),
                        layout.Width,
                        allowedSourceWidth,
                        widthTolerance,
                        sourceFontPath);
                    return;
                }

                if (layout.MaximumGapPixels > sourceLayout.MaximumGapPixels + 4)
                {
                    Fail(
                        "{0} system-settings phrase [{1}] maxGap {2} exceeds source {3}+4 from {4}, pair=U+{5:X4}/U+{6:X4}",
                        fontPath,
                        Escape(phrase),
                        layout.MaximumGapPixels,
                        sourceLayout.MaximumGapPixels,
                        sourceFontPath,
                        layout.MaximumGapLeftCodepoint,
                        layout.MaximumGapRightCodepoint);
                    return;
                }

                if (strictVisualGap &&
                    !VerifySystemSettingsStrictVisualGap(fontPath, phrase, layout))
                {
                    return;
                }

                Pass(
                    "{0} system-settings phrase [{1}] scaled-lobby layout glyphs={2}, width={3}/{4}, maxGap={5}/{6}, minGap={7}",
                    fontPath,
                    Escape(phrase),
                    layout.Glyphs,
                    layout.Width,
                    allowedSourceWidth,
                    layout.MaximumGapPixels,
                    sourceLayout.MaximumGapPixels,
                    layout.MinimumGapPixels);
            }

            private bool TryComputeLobbyAxisAdvanceNormalizedWidth(
                string fontPath,
                string sourceFontPath,
                string phrase,
                PhraseLayoutResult sourceLayout,
                out int normalizedWidth,
                out string error)
            {
                normalizedWidth = sourceLayout.Width;
                error = null;
                if (_ttmpFont == null || !_ttmpFont.ContainsPath(sourceFontPath))
                {
                    error = "source font is missing";
                    return false;
                }

                byte[] sourceFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(sourceFontPath);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }

                byte[] targetFdt;
                try
                {
                    targetFdt = _patchedFont.ReadFile(fontPath);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }

                for (int i = 0; i < phrase.Length; i++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref i);
                    if (IsPhraseLayoutSpace(codepoint))
                    {
                        continue;
                    }

                    FdtGlyphEntry sourceGlyph;
                    if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                    {
                        error = "missing U+" + codepoint.ToString("X4");
                        return false;
                    }

                    FdtGlyphEntry targetGlyph;
                    if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                    {
                        continue;
                    }

                    int sourceAdvance = GetGlyphAdvance(sourceGlyph);
                    int targetAdvance = sourceAdvance;
                    if (IsHangulCodepoint(codepoint) &&
                        LobbyAxisHangulAdvanceEntryMatchesExpected(sourceGlyph, targetGlyph))
                    {
                        targetAdvance = GetGlyphAdvance(targetGlyph);
                    }
                    else if (codepoint > 0x20u &&
                             codepoint <= 0x7Eu &&
                             GlyphSpacingMetricsMatchOrLobbySafe(fontPath, codepoint, sourceGlyph, targetGlyph))
                    {
                        targetAdvance = GetGlyphAdvance(targetGlyph);
                    }

                    if (targetAdvance > sourceAdvance)
                    {
                        normalizedWidth += targetAdvance - sourceAdvance;
                    }
                }

                return true;
            }

            private bool VerifySystemSettingsStrictScaledRoutePhraseLayout(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} system-settings strict phrase [{1}] layout error: {2}", fontPath, Escape(phrase), error);
                    return false;
                }

                return VerifySystemSettingsStrictVisualGap(fontPath, phrase, layout);
            }

            private bool VerifySystemSettingsStrictVisualGap(string fontPath, string phrase, PhraseLayoutResult layout)
            {
                if (!IsSystemSettingsStrictVisualFont(fontPath) || layout.Glyphs <= 1)
                {
                    return true;
                }

                if (layout.MinimumGapPixels >= 0)
                {
                    return true;
                }

                string sourceFontPath = ResolveCleanAsciiReferenceFontPath(fontPath);
                PhraseLayoutResult cleanLayout;
                string cleanError;
                if (!string.IsNullOrEmpty(sourceFontPath) &&
                    TryMeasurePhraseLayout(_cleanFont, sourceFontPath, phrase, false, out cleanLayout, out cleanError) &&
                    layout.MinimumGapPixels >= cleanLayout.MinimumGapPixels &&
                    layout.OverlapPixels <= cleanLayout.OverlapPixels + 2)
                {
                    return true;
                }

                string pairDetail = DescribeScaledLobbyPairSpacing(
                    fontPath,
                    layout.MinimumGapLeftCodepoint,
                    layout.MinimumGapRightCodepoint);
                Fail(
                    "{0} system-settings strict phrase [{1}] minGap={2} is below 0, pair=U+{3:X4}/U+{4:X4}{5}",
                    fontPath,
                    Escape(phrase),
                    layout.MinimumGapPixels,
                    layout.MinimumGapLeftCodepoint,
                    layout.MinimumGapRightCodepoint,
                    pairDetail);
                return false;
            }

            private string DescribeScaledLobbyPairSpacing(string fontPath, uint leftCodepoint, uint rightCodepoint)
            {
                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fontPath);
                    FdtGlyphEntry leftGlyph;
                    FdtGlyphEntry rightGlyph;
                    if (!TryFindGlyph(fdt, leftCodepoint, out leftGlyph) ||
                        !TryFindGlyph(fdt, rightCodepoint, out rightGlyph))
                    {
                        return string.Empty;
                    }

                    PhraseGlyphMeasurement leftMeasurement;
                    PhraseGlyphMeasurement rightMeasurement;
                    string error;
                    if (!TryMeasurePhraseGlyph(_patchedFont, fontPath, fdt, leftCodepoint, false, out leftMeasurement, out error) ||
                        !TryMeasurePhraseGlyph(_patchedFont, fontPath, fdt, rightCodepoint, false, out rightMeasurement, out error))
                    {
                        return string.Empty;
                    }

                    int kerning = GetKerningAdjustment(ReadKerningAdjustments(fdt), leftCodepoint, rightCodepoint);
                    return string.Format(
                        " leftAdvance={0}, leftSize={1}x{2}, leftOffset={3}/{4}, leftBounds={5}-{6}, rightAdvance={7}, rightSize={8}x{9}, rightOffset={10}/{11}, rightBounds={12}-{13}, kerning={14}",
                        GetGlyphAdvance(leftGlyph),
                        leftGlyph.Width,
                        leftGlyph.Height,
                        leftGlyph.OffsetX,
                        leftGlyph.OffsetY,
                        leftMeasurement.MinX,
                        leftMeasurement.MaxX,
                        GetGlyphAdvance(rightGlyph),
                        rightGlyph.Width,
                        rightGlyph.Height,
                        rightGlyph.OffsetX,
                        rightGlyph.OffsetY,
                        rightMeasurement.MinX,
                        rightMeasurement.MaxX,
                        kerning);
                }
                catch
                {
                    return string.Empty;
                }
            }

            private void VerifyPhraseMinimumVisualGap(string fontPath, string phrase)
            {
                PhraseLayoutResult layout;
                string error;
                if (!TryMeasurePhraseLayout(_patchedFont, fontPath, phrase, true, out layout, out error))
                {
                    Fail("{0} phrase [{1}] visual-spacing layout error: {2}", fontPath, Escape(phrase), error);
                    return;
                }

                if (!VerifyPhraseMinimumVisualGap(fontPath, phrase, layout, ResolveCleanAsciiReferenceFontPath(fontPath)))
                {
                    return;
                }

                Pass(
                    "{0} phrase [{1}] visual spacing minGap={2}, required={3}",
                    fontPath,
                    Escape(phrase),
                    layout.MinimumGapPixels,
                    layout.MinimumRequiredGapPixels);
            }

            private bool VerifyPhraseMinimumVisualGap(string fontPath, string phrase, PhraseLayoutResult layout, string sourceFontPath)
            {
                if (!IsLobbyFontPath(fontPath) || layout.Glyphs <= 1 || layout.MinimumRequiredGapPixels <= 0)
                {
                    return true;
                }

                if (layout.MinimumGapPixels >= 1)
                {
                    return true;
                }

                PhraseLayoutResult sourceLayout;
                string sourceError;
                if (!string.IsNullOrEmpty(sourceFontPath) &&
                    TryMeasurePhraseLayout(_cleanFont, sourceFontPath, phrase, false, out sourceLayout, out sourceError) &&
                    layout.MinimumGapPixels >= sourceLayout.MinimumGapPixels &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels + 2)
                {
                    return true;
                }

                if (IsAsciiCodepoint(layout.MinimumGapLeftCodepoint) &&
                    IsAsciiCodepoint(layout.MinimumGapRightCodepoint) &&
                    !string.IsNullOrEmpty(sourceFontPath) &&
                    TryMeasurePhraseLayout(_cleanFont, sourceFontPath, ToAsciiOnlyPhrase(phrase), false, out sourceLayout, out sourceError) &&
                    layout.MinimumGapPixels >= sourceLayout.MinimumGapPixels &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels + 2)
                {
                    return true;
                }

                string pairDetail = DescribeScaledLobbyPairSpacing(
                    fontPath,
                    layout.MinimumGapLeftCodepoint,
                    layout.MinimumGapRightCodepoint);
                Fail(
                    "{0} phrase [{1}] min visual gap {2} is below lobby floor 1, pair=U+{3:X4}/U+{4:X4}{5}",
                    fontPath,
                    Escape(phrase),
                    layout.MinimumGapPixels,
                    layout.MinimumGapLeftCodepoint,
                    layout.MinimumGapRightCodepoint,
                    pairDetail);
                return false;
            }

            private bool VerifyMixedScalePhraseMinimumVisualGap(
                string fontPath,
                string phrase,
                PhraseLayoutResult layout,
                string asciiSourceFontPath,
                string asciiFallbackSourceFontPath)
            {
                if (!IsLobbyFontPath(fontPath) || layout.Glyphs <= 1 || layout.MinimumRequiredGapPixels <= 0)
                {
                    return true;
                }

                if (layout.MinimumGapPixels >= 1)
                {
                    return true;
                }

                PhraseLayoutResult sourceLayout;
                string error;
                string hangulSourceFontPath = ResolveLobbyHangulSourceFontPath(fontPath);
                if (_ttmpFont != null &&
                    _ttmpFont.ContainsPath(hangulSourceFontPath) &&
                    TryMeasurePhraseLayout(_ttmpFont, hangulSourceFontPath, phrase, out sourceLayout, out error) &&
                    layout.MinimumGapPixels >= sourceLayout.MinimumGapPixels &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels + 2)
                {
                    return true;
                }

                string asciiOnlyPhrase = ToAsciiOnlyPhrase(phrase);
                if (TryMeasurePhraseLayout(_cleanFont, asciiSourceFontPath, asciiOnlyPhrase, false, out sourceLayout, out error) &&
                    layout.MinimumGapPixels >= sourceLayout.MinimumGapPixels &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels + 2)
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(asciiFallbackSourceFontPath) &&
                    !string.Equals(asciiFallbackSourceFontPath, asciiSourceFontPath, StringComparison.OrdinalIgnoreCase) &&
                    TryMeasurePhraseLayout(_cleanFont, asciiFallbackSourceFontPath, asciiOnlyPhrase, false, out sourceLayout, out error) &&
                    layout.MinimumGapPixels >= sourceLayout.MinimumGapPixels &&
                    layout.OverlapPixels <= sourceLayout.OverlapPixels + 2)
                {
                    return true;
                }

                string pairDetail = DescribeScaledLobbyPairSpacing(
                    fontPath,
                    layout.MinimumGapLeftCodepoint,
                    layout.MinimumGapRightCodepoint);
                Fail(
                    "{0} phrase [{1}] min visual gap {2} is worse than clean/source baselines, pair=U+{3:X4}/U+{4:X4}{5}",
                    fontPath,
                    Escape(phrase),
                    layout.MinimumGapPixels,
                    layout.MinimumGapLeftCodepoint,
                    layout.MinimumGapRightCodepoint,
                    pairDetail);
                return false;
            }

            private static bool ShouldVerifyLobbyAsciiVisualGap(string fontPath, string phrase)
            {
                if (!IsLobbyFontPath(fontPath))
                {
                    return false;
                }

                return IsAsciiPhrase(phrase);
            }

            private static bool IsScaledLobbySystemSettingsFont(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                return string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_18_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_36_lobby.fdt", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsSystemSettingsStrictVisualFont(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                if (IsScaledLobbySystemSettingsFont(normalized))
                {
                    return true;
                }

                for (int i = 0; i < SystemSettingsScaledFonts.Length; i++)
                {
                    if (string.Equals(normalized, SystemSettingsScaledFonts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsStartScreenHighRiskSystemSettingsPhrase(string phrase)
            {
                return ContainsPhrase(LobbyScaledHangulPhrases.HighResolutionUiScaleOptions, phrase) ||
                       ContainsPhrase(LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages, phrase);
            }

            private static bool ContainsPhrase(string[] phrases, string phrase)
            {
                if (phrases == null)
                {
                    return false;
                }

                for (int i = 0; i < phrases.Length; i++)
                {
                    if (string.Equals(phrases[i], phrase, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string ResolveLobbyHangulSourceFontPath(string fontPath)
            {
                string normalized = (fontPath ?? string.Empty).Replace('\\', '/');
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    if (string.Equals(normalized, Derived4kLobbyFontPairs[i, 0], StringComparison.OrdinalIgnoreCase))
                    {
                        return Derived4kLobbyFontPairs[i, 1];
                    }
                }

                return normalized;
            }

            private static bool IsAsciiPhrase(string phrase)
            {
                for (int i = 0; i < phrase.Length; i++)
                {
                    if (phrase[i] > 0x7E)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool IsAsciiCodepoint(uint codepoint)
            {
                return codepoint >= 0x21u && codepoint <= 0x7Eu;
            }
        }
    }
}
