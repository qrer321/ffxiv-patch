using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void ExpectEqual(string label, string actual, string expected)
            {
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0} = {1}", label, Escape(actual));
                    return;
                }

                Fail("{0} expected [{1}], actual [{2}]", label, Escape(expected), Escape(actual));
            }

            private void ExpectStartsWith(string label, string actual, string expectedPrefix)
            {
                if ((actual ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    Pass("{0} starts with {1}", label, expectedPrefix);
                    return;
                }

                Fail("{0} does not start with [{1}], actual [{2}]", label, expectedPrefix, Escape(actual));
            }

            private void ExpectContains(string label, string actual, string expected)
            {
                if ((actual ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    Pass("{0} contains {1}", label, expected);
                    return;
                }

                Fail("{0} does not contain [{1}], actual [{2}]", label, expected, Escape(actual));
            }

            private void ExpectNotContains(string label, string actual, string unexpected)
            {
                if ((actual ?? string.Empty).IndexOf(unexpected, StringComparison.Ordinal) < 0)
                {
                    Pass("{0} does not contain {1}", label, unexpected);
                    return;
                }

                Fail("{0} still contains [{1}], actual [{2}]", label, unexpected, Escape(actual));
            }

            private void ExpectText(string sheet, uint rowId, string expected)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, _language);
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0}#{1} = {2}", sheet, rowId, expected);
                    return;
                }

                Fail("{0}#{1} expected [{2}], actual [{3}]", sheet, rowId, expected, Escape(actual));
            }

            private void ExpectTextContains(string sheet, uint rowId, string expected)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, _language);
                if (actual.IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    Pass("{0}#{1} contains {2}", sheet, rowId, expected);
                    return;
                }

                Fail("{0}#{1} does not contain [{2}], actual [{3}]", sheet, rowId, expected, Escape(actual));
            }

            private void ExpectTextNotContains(string sheet, uint rowId, string unexpected)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, _language);
                if (actual.IndexOf(unexpected, StringComparison.Ordinal) < 0)
                {
                    Pass("{0}#{1} does not contain {2}", sheet, rowId, unexpected);
                    return;
                }

                Fail("{0}#{1} still contains [{2}], actual [{3}]", sheet, rowId, unexpected, Escape(actual));
            }

            private void ExpectTextColumn(string sheet, uint rowId, ushort columnOffset, string expected)
            {
                string actual = GetStringColumnByOffset(_patchedText, sheet, rowId, _language, columnOffset);
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0}#{1}@{2} = {3}", sheet, rowId, columnOffset, expected);
                    return;
                }

                Fail("{0}#{1}@{2} expected [{3}], actual [{4}]", sheet, rowId, columnOffset, expected, Escape(actual));
            }

            private void ExpectTextColumnContains(string sheet, uint rowId, ushort columnOffset, string expected)
            {
                string actual = GetStringColumnByOffset(_patchedText, sheet, rowId, _language, columnOffset);
                if (actual.IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    Pass("{0}#{1}@{2} contains {3}", sheet, rowId, columnOffset, expected);
                    return;
                }

                Fail("{0}#{1}@{2} does not contain [{3}], actual [{4}]", sheet, rowId, columnOffset, expected, Escape(actual));
            }

            private void ExpectTextColumnNotContains(string sheet, uint rowId, ushort columnOffset, string unexpected)
            {
                string actual = GetStringColumnByOffset(_patchedText, sheet, rowId, _language, columnOffset);
                if (actual.IndexOf(unexpected, StringComparison.Ordinal) < 0)
                {
                    Pass("{0}#{1}@{2} does not contain {3}", sheet, rowId, columnOffset, unexpected);
                    return;
                }

                Fail("{0}#{1}@{2} still contains [{3}], actual [{4}]", sheet, rowId, columnOffset, unexpected, Escape(actual));
            }

            private void ExpectAnyTextColumnContains(string sheet, uint rowId, string expected)
            {
                List<string> columns = GetStringColumns(_patchedText, sheet, rowId, _language);
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].IndexOf(expected, StringComparison.Ordinal) >= 0)
                    {
                        Pass("{0}#{1} column {2} contains {3}", sheet, rowId, i, expected);
                        return;
                    }
                }

                Fail("{0}#{1} does not contain [{2}], columns=[{3}]", sheet, rowId, expected, Escape(string.Join(" | ", columns.ToArray())));
            }

            private void ExpectAnyTextColumnNotContains(string sheet, uint rowId, string unexpected)
            {
                List<string> columns = GetStringColumns(_patchedText, sheet, rowId, _language);
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].IndexOf(unexpected, StringComparison.Ordinal) >= 0)
                    {
                        Fail("{0}#{1} column {2} still contains [{3}], value=[{4}]", sheet, rowId, i, unexpected, Escape(columns[i]));
                        return;
                    }
                }

                Pass("{0}#{1} does not contain {2}", sheet, rowId, unexpected);
            }

            private void ExpectDataCenterLabel(DataCenterLabelExpectation expectation, string language)
            {
                List<string> columns = GetStringColumns(_patchedText, expectation.Sheet, expectation.RowId, language);
                bool found = false;
                bool fallback = false;
                bool hangul = false;
                bool unexpectedNonEmpty = false;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (expectation.AllowSubstring
                        ? columns[i].IndexOf(expectation.Expected, StringComparison.Ordinal) >= 0
                        : string.Equals(columns[i], expectation.Expected, StringComparison.Ordinal))
                    {
                        found = true;
                    }

                    if (LooksLikeMissingGlyphFallback(columns[i]))
                    {
                        fallback = true;
                    }

                    if (ContainsHangul(columns[i]))
                    {
                        hangul = true;
                    }

                    if (!expectation.AllowSubstring && columns[i].Length > 0 && !string.Equals(columns[i], expectation.Expected, StringComparison.Ordinal))
                    {
                        unexpectedNonEmpty = true;
                    }
                }

                if (found && !fallback && !hangul && !unexpectedNonEmpty)
                {
                    Pass("{0}#{1}/{2} contains [{3}]", expectation.Sheet, expectation.RowId, language, expectation.Expected);
                    return;
                }

                Fail(
                    "{0}#{1}/{2} expected [{3}], fallback={4}, hangul={5}, unexpected={6}, columns [{7}]",
                    expectation.Sheet,
                    expectation.RowId,
                    language,
                    expectation.Expected,
                    fallback,
                    hangul,
                    unexpectedNonEmpty,
                    string.Join("] [", columns.ToArray()));
            }

            private void VerifyLabelGlyphsEqualClean(string fdtPath, string[] labels)
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
                {
                    string label = labels[labelIndex];
                    for (int charIndex = 0; charIndex < label.Length; charIndex++)
                    {
                        char ch = label[charIndex];
                        if (!char.IsWhiteSpace(ch))
                        {
                            codepoints.Add(ch);
                        }
                    }
                }

                foreach (uint codepoint in codepoints)
                {
                    ExpectGlyphEqual(_cleanFont, fdtPath, codepoint, _patchedFont, fdtPath, codepoint);
                    ExpectGlyphNotEqualToFallback(fdtPath, codepoint, '-');
                    ExpectGlyphNotEqualToFallback(fdtPath, codepoint, '=');
                }
            }

            private void ExpectGlyphNotEqualToFallback(string fdtPath, uint codepoint, uint fallbackCodepoint)
            {
                if (codepoint == fallbackCodepoint)
                {
                    return;
                }

                try
                {
                    byte[] fdt = _patchedFont.ReadFile(fdtPath);
                    FdtGlyphEntry ignored;
                    if (!TryFindGlyph(fdt, codepoint, out ignored))
                    {
                        Fail("{0} U+{1:X4} is missing", fdtPath, codepoint);
                        return;
                    }

                    if (!TryFindGlyph(fdt, fallbackCodepoint, out ignored))
                    {
                        Warn("{0} U+{1:X4} fallback comparison skipped; missing U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    GlyphCanvas glyph = RenderGlyph(_patchedFont, fdtPath, codepoint);
                    GlyphCanvas fallback = RenderGlyph(_patchedFont, fdtPath, fallbackCodepoint);
                    long score = Diff(glyph.Alpha, fallback.Alpha);
                    if (score != 0 && glyph.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} is not fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                        return;
                    }

                    Fail("{0} U+{1:X4} matches fallback U+{2:X4}", fdtPath, codepoint, fallbackCodepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} fallback comparison error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private void ExpectBytes(string label, byte[] actual, byte[] expected)
            {
                bool equal = actual.Length == expected.Length;
                for (int i = 0; equal && i < actual.Length; i++)
                {
                    equal = actual[i] == expected[i];
                }

                if (equal)
                {
                    Pass("{0} bytes = {1}", label, ToHex(expected));
                    return;
                }

                Fail("{0} bytes expected {1}, actual {2}", label, ToHex(expected), ToHex(actual));
            }

            private void ExpectGlyphEqual(
                CompositeArchive sourceArchive,
                string sourceFdtPath,
                uint sourceCodepoint,
                CompositeArchive targetArchive,
                string targetFdtPath,
                uint targetCodepoint)
            {
                try
                {
                    GlyphCanvas source = RenderGlyph(sourceArchive, sourceFdtPath, sourceCodepoint);
                    GlyphCanvas target = RenderGlyph(targetArchive, targetFdtPath, targetCodepoint);
                    long score = Diff(source.Alpha, target.Alpha);
                    bool spacingMatch = GlyphSpacingMetricsMatch(source.Glyph, target.Glyph);
                    if (score == 0 && spacingMatch && source.VisiblePixels > 0 && target.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} -> {2} U+{3:X4}", sourceFdtPath, sourceCodepoint, targetFdtPath, targetCodepoint);
                        return;
                    }

                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} mismatch score={4} visible={5}/{6}, target={7}, clean={8}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        score,
                        source.VisiblePixels,
                        target.VisiblePixels,
                        FormatGlyphSpacing(target.Glyph),
                        FormatGlyphSpacing(source.Glyph));
                }
                catch (Exception ex)
                {
                    Fail(
                        "{0} U+{1:X4} -> {2} U+{3:X4} error: {4}",
                        sourceFdtPath,
                        sourceCodepoint,
                        targetFdtPath,
                        targetCodepoint,
                        ex.Message);
                }
            }

            private void ExpectGlyphEqualIfSourceExists(
                CompositeArchive sourceArchive,
                string sourceFdtPath,
                uint sourceCodepoint,
                CompositeArchive targetArchive,
                string targetFdtPath,
                uint targetCodepoint)
            {
                byte[] sourceFdt = sourceArchive.ReadFile(sourceFdtPath);
                FdtGlyphEntry ignored;
                if (!TryFindGlyph(sourceFdt, sourceCodepoint, out ignored))
                {
                    return;
                }

                ExpectGlyphEqual(sourceArchive, sourceFdtPath, sourceCodepoint, targetArchive, targetFdtPath, targetCodepoint);
            }

            private void ExpectGlyphVisible(CompositeArchive archive, string fdtPath, uint codepoint)
            {
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fdtPath, codepoint);
                    if (canvas.VisiblePixels > 0)
                    {
                        Pass("{0} U+{1:X4} visible={2}", fdtPath, codepoint, canvas.VisiblePixels);
                        return;
                    }

                    Fail("{0} U+{1:X4} is invisible", fdtPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private void ExpectGlyphVisibleAtLeast(CompositeArchive archive, string fdtPath, uint codepoint, int minimumVisiblePixels)
            {
                try
                {
                    GlyphCanvas canvas = RenderGlyph(archive, fdtPath, codepoint);
                    if (canvas.VisiblePixels >= minimumVisiblePixels)
                    {
                        Pass("{0} U+{1:X4} visible={2}", fdtPath, codepoint, canvas.VisiblePixels);
                        return;
                    }

                    Fail("{0} U+{1:X4} visible={2}, expected at least {3}", fdtPath, codepoint, canvas.VisiblePixels, minimumVisiblePixels);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} error: {2}", fdtPath, codepoint, ex.Message);
                }
            }

            private static long Diff(byte[] left, byte[] right)
            {
                long score = 0;
                for (int i = 0; i < left.Length; i++)
                {
                    score += Math.Abs(left[i] - right[i]);
                }

                return score;
            }

            private static void Pass(string format, params object[] args)
            {
                Console.WriteLine("  OK   " + string.Format(format, args));
            }

            private static void Warn(string format, params object[] args)
            {
                Console.WriteLine("  WARN " + string.Format(format, args));
            }

            private void Fail(string format, params object[] args)
            {
                Failed = true;
                Console.WriteLine("  FAIL " + string.Format(format, args));
            }
        }
    }
}
