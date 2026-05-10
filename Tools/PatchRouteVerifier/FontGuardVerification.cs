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
            private void Verify4kLobbyFontDerivations()
            {
                Console.WriteLine("[FDT] 4K lobby font derivations");
                uint[] requiredHangulCodepoints = Collect4kLobbyRequiredHangulCodepoints();
                HashSet<uint> actionDetailHighScaleCodepoints = CollectActionDetailHighScaleHangulCodepointSet();

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string targetFontPath = Derived4kLobbyFontPairs[i, 0];
                    string sourceFontPath = Derived4kLobbyFontPairs[i, 1];

                    for (int latinIndex = 0; latinIndex < FourKLobbyLatinCodepoints.Length; latinIndex++)
                    {
                        ExpectGlyphEqualIfSourceExists(_patchedFont, sourceFontPath, FourKLobbyLatinCodepoints[latinIndex], _patchedFont, targetFontPath, FourKLobbyLatinCodepoints[latinIndex]);
                    }

                    byte[] sourceFdt;
                    try
                    {
                        sourceFdt = _patchedFont.ReadFile(sourceFontPath);
                    }
                    catch (Exception ex)
                    {
                        Warn("{0} could not be read for 4K lobby derivation check: {1}", sourceFontPath, ex.Message);
                        continue;
                    }

                    bool checkedHangul = false;
                    int checkedHangulCount = 0;
                    for (int codepointIndex = 0; codepointIndex < requiredHangulCodepoints.Length; codepointIndex++)
                    {
                        uint codepoint = requiredHangulCodepoints[codepointIndex];
                        FdtGlyphEntry sourceGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            Fail("{0} is missing required 4K lobby source glyph U+{1:X4}", sourceFontPath, codepoint);
                            continue;
                        }

                        checkedHangul = true;
                        ExpectGlyphVisibleAtLeast(_patchedFont, targetFontPath, codepoint, 10);
                        ExpectGlyphNotEqualToFallback(targetFontPath, codepoint, '-');
                        ExpectGlyphNotEqualToFallback(targetFontPath, codepoint, '=');
                        VerifyDerived4kGlyphMetrics(
                            targetFontPath,
                            sourceFontPath,
                            codepoint,
                            sourceGlyph,
                            actionDetailHighScaleCodepoints);
                        checkedHangulCount++;
                    }

                    if (!checkedHangul)
                    {
                        Fail("{0} has no required Hangul glyphs for 4K lobby target {1}", sourceFontPath, targetFontPath);
                    }
                    else
                    {
                        Pass("{0} required 4K lobby Hangul glyphs checked: {1}", targetFontPath, checkedHangulCount);
                    }
                }
            }

            private uint[] Collect4kLobbyRequiredHangulCodepoints()
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                AddDynamicHangulCodepoints(codepoints, Derived4kLobbyRequiredHangulPhrases);
                int staticCount = codepoints.Count;
                int addonDerived = AddPatchedAddonRangeHangulCodepoints(
                    codepoints,
                    LobbyScaledHangulPhrases.StartScreenSystemSettingsAddonRowRanges,
                    "dynamic 4K lobby glyph verification");
                uint[] values = new uint[codepoints.Count];
                codepoints.CopyTo(values);
                Array.Sort(values);
                Pass("4K lobby required Hangul codepoints collected: static={0}, addon-derived={1}, total={2}", staticCount, addonDerived, values.Length);
                return values;
            }

            private int AddPatchedAddonRangeHangulCodepoints(HashSet<uint> codepoints, AddonRowRange[] ranges, string label)
            {
                int before = codepoints.Count;
                try
                {
                    ExcelHeader header = ExcelHeader.Parse(_patchedText.ReadFile("exd/Addon.exh"));
                    if (header.Variant != ExcelVariant.Default)
                    {
                        Warn("Addon header variant is not supported for {0}: {1}", label, header.Variant);
                        return 0;
                    }

                    byte languageId = LanguageToId(_language);
                    bool hasLanguageSuffix = header.HasLanguage(languageId);
                    List<int> stringColumns = header.GetStringColumnIndexes();
                    for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                    {
                        ExcelPageDefinition page = header.Pages[pageIndex];
                        if (!AddonPageOverlaps(page, ranges))
                        {
                            continue;
                        }

                        string exdPath = BuildExdPath("Addon", page.StartId, _language, hasLanguageSuffix);
                        ExcelDataFile file = ExcelDataFile.Parse(_patchedText.ReadFile(exdPath));
                        for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                        {
                            ExcelDataRow row = file.Rows[rowIndex];
                            if (!RowInRanges(row.RowId, ranges))
                            {
                                continue;
                            }

                            for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                            {
                                byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]);
                                AddDynamicHangulCodepoints(codepoints, bytes);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Warn("Could not collect Addon glyph coverage for {0}: {1}", label, ex.Message);
                    return 0;
                }

                return codepoints.Count - before;
            }

            private static bool AddonPageOverlaps(ExcelPageDefinition page, AddonRowRange[] ranges)
            {
                uint pageEnd = page.RowCount == 0 ? page.StartId : page.StartId + page.RowCount - 1;
                for (int i = 0; i < ranges.Length; i++)
                {
                    if (ranges[i].StartId <= pageEnd && ranges[i].EndId >= page.StartId)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool RowInRanges(uint rowId, AddonRowRange[] ranges)
            {
                for (int i = 0; i < ranges.Length; i++)
                {
                    if (ranges[i].Contains(rowId))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void AddDynamicHangulCodepoints(HashSet<uint> codepoints, string[] phrases)
            {
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    AddDynamicHangulCodepoints(codepoints, phrases[phraseIndex]);
                }
            }

            private static void AddDynamicHangulCodepoints(HashSet<uint> codepoints, byte[] bytes)
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return;
                }

                AddDynamicHangulCodepoints(codepoints, Encoding.UTF8.GetString(bytes));
            }

            private static void AddDynamicHangulCodepoints(HashSet<uint> codepoints, string value)
            {
                value = value ?? string.Empty;
                for (int index = 0; index < value.Length; index++)
                {
                    uint codepoint = ReadCodepoint(value, ref index);
                    if (IsHangulCodepoint(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }
            }

            private void VerifyDerived4kGlyphMetrics(
                string fontPath,
                string sourceFontPath,
                uint codepoint,
                FdtGlyphEntry sourceGlyph,
                HashSet<uint> actionDetailHighScaleCodepoints)
            {
                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fontPath);
                    FdtGlyphEntry targetGlyph;
                    if (!TryFindGlyph(fdt, codepoint, out targetGlyph))
                    {
                        Fail("{0} U+{1:X4} is missing", fontPath, codepoint);
                        return;
                    }

                    if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                    {
                        if (IsActionDetailHighScaleRepairedCodepoint(actionDetailHighScaleCodepoints, sourceFontPath, codepoint))
                        {
                            Pass(
                                "{0} U+{1:X4} keeps pre-repair lobby metrics while {2} is action-detail high-scale repaired: target={3}, source={4}",
                                fontPath,
                                codepoint,
                                sourceFontPath,
                                FormatGlyphSpacing(targetGlyph),
                                FormatGlyphSpacing(sourceGlyph));
                            return;
                        }

                        Fail(
                            "{0} U+{1:X4} metrics differ from derived lobby source: target={2}, source={3}",
                            fontPath,
                            codepoint,
                            FormatGlyphSpacing(targetGlyph),
                            FormatGlyphSpacing(sourceGlyph));
                        return;
                    }

                    Pass("{0} U+{1:X4} metrics match derived lobby source: {2}", fontPath, codepoint, FormatGlyphSpacing(targetGlyph));
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} metric check error: {2}", fontPath, codepoint, ex.Message);
                }
            }

        }
    }
}
