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
            private void VerifyStartScreenGlyphVariantRows()
            {
                Console.WriteLine("[EXD] start-screen glyph variant rows");

                ExcelHeader header = ExcelHeader.Parse(_patchedText.ReadFile("exd/Addon.exh"));
                if (header.Variant != ExcelVariant.Default)
                {
                    Fail("Addon header variant is not supported for start-screen glyph variant row verification: {0}", header.Variant);
                    return;
                }

                byte languageId = LanguageToId(_language);
                bool hasLanguageSuffix = header.HasLanguage(languageId);
                List<int> stringColumns = header.GetStringColumnIndexes();
                int aliasRows = 0;
                int aliasStrings = 0;
                int requiredRows = 0;
                int requiredStrings = 0;
                int failures = 0;

                for (int pageIndex = 0; pageIndex < header.Pages.Count; pageIndex++)
                {
                    ExcelPageDefinition page = header.Pages[pageIndex];
                    string exdPath = BuildExdPath("Addon", page.StartId, _language, hasLanguageSuffix);
                    ExcelDataFile file = ExcelDataFile.Parse(_patchedText.ReadFile(exdPath));

                    for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                    {
                        ExcelDataRow row = file.Rows[rowIndex];
                        bool allowedRow = StartScreenGlyphVariants.ShouldApplyToAddonRow(row.RowId);
                        bool countedAliasRow = false;
                        bool countedRequiredRow = false;

                        for (int columnIndex = 0; columnIndex < stringColumns.Count; columnIndex++)
                        {
                            byte[] bytes = file.GetStringBytes(row, header, stringColumns[columnIndex]) ?? new byte[0];
                            string text = Encoding.UTF8.GetString(bytes);
                            bool hasAlias = StartScreenGlyphVariants.ContainsAlias(text);
                            string normalized = StartScreenGlyphVariants.NormalizeAliases(text);
                            bool requiresAlias = RequiresStartScreenGlyphVariant(normalized);

                            if (hasAlias)
                            {
                                aliasStrings++;
                                if (!countedAliasRow)
                                {
                                    aliasRows++;
                                    countedAliasRow = true;
                                }

                                if (!allowedRow)
                                {
                                    Fail(
                                        "Addon#{0} column {1} has start-screen glyph variants outside approved row ranges: [{2}]",
                                        row.RowId,
                                        stringColumns[columnIndex],
                                        Escape(normalized));
                                    failures++;
                                }
                            }

                            if (allowedRow && requiresAlias)
                            {
                                requiredStrings++;
                                if (!countedRequiredRow)
                                {
                                    requiredRows++;
                                    countedRequiredRow = true;
                                }

                                if (!hasAlias)
                                {
                                    Fail(
                                        "Addon#{0} column {1} still has unaliased start-screen phrase: [{2}]",
                                        row.RowId,
                                        stringColumns[columnIndex],
                                        Escape(normalized));
                                    failures++;
                                }
                            }
                        }
                    }
                }

                if (requiredStrings == 0)
                {
                    Fail("No start-screen Addon strings requiring glyph variants were found; verifier is not covering the reported route");
                    return;
                }

                if (failures == 0)
                {
                    Pass(
                        "start-screen glyph variants scoped to Addon row ranges: alias_rows={0}, alias_strings={1}, required_rows={2}, required_strings={3}",
                        aliasRows,
                        aliasStrings,
                        requiredRows,
                        requiredStrings);
                }

                VerifyStartScreenGlyphVariantVisualScale();
            }

            private void VerifyStartScreenGlyphVariantVisualScale()
            {
                VerifyStartScreenGlyphVariantVisualScaleCase("axis12-to-axis18", "common/font/AXIS_12.fdt", "common/font/AXIS_18.fdt");
                VerifyStartScreenGlyphVariantVisualScaleCase("axis14-to-axis18", "common/font/AXIS_14.fdt", "common/font/AXIS_18.fdt");
                VerifyStartScreenGlyphVariantVisualScaleCase("axis18-to-axis36", "common/font/AXIS_18.fdt", "common/font/AXIS_36.fdt");
            }

            private void VerifyStartScreenGlyphVariantVisualScaleCase(string id, string targetFontPath, string referenceFontPath)
            {
                const double minHeightRatio = 0.86d;
                const double maxHeightRatio = 1.20d;

                PhraseVisualBounds target;
                string error;
                string targetPhrase = StartScreenGlyphVariants.ApplyToKnownPhrases(LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages[0]);
                if (!TryMeasurePhraseVisualBounds(_patchedFont, targetFontPath, targetPhrase, true, out target, out error))
                {
                    Fail("{0} start-screen variant target render failed in {1}: {2}", id, targetFontPath, error);
                    return;
                }

                PhraseVisualBounds reference;
                string referencePhrase = LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages[0];
                if (!TryMeasurePhraseVisualBounds(_patchedFont, referenceFontPath, referencePhrase, true, out reference, out error))
                {
                    Fail("{0} start-screen variant reference render failed in {1}: {2}", id, referenceFontPath, error);
                    return;
                }

                double ratio = SafeRatio(target.Height, reference.Height);
                if (ratio < minHeightRatio || ratio > maxHeightRatio)
                {
                    Fail(
                        "{0} start-screen variant height ratio {1} outside {2}..{3}: target={4} {5}, reference={6} {7}, targetBounds={8}, referenceBounds={9}",
                        id,
                        FormatRatio(ratio),
                        FormatRatio(minHeightRatio),
                        FormatRatio(maxHeightRatio),
                        targetFontPath,
                        target.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        referenceFontPath,
                        reference.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        FormatPhraseBounds(target),
                        FormatPhraseBounds(reference));
                    return;
                }

                Pass(
                    "{0} start-screen variant visual scale matches {1}: ratio={2}, target={3}, reference={4}",
                    id,
                    referenceFontPath,
                    FormatRatio(ratio),
                    target.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    reference.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            private static bool RequiresStartScreenGlyphVariant(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                for (int i = 0; i < StartScreenGlyphVariants.KnownPhrases.Length; i++)
                {
                    string phrase = StartScreenGlyphVariants.KnownPhrases[i] ?? string.Empty;
                    if (phrase.Length == 0)
                    {
                        continue;
                    }

                    if (text.IndexOf(phrase, StringComparison.Ordinal) >= 0 &&
                        !string.Equals(StartScreenGlyphVariants.ApplyToKnownPhrases(phrase), phrase, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
