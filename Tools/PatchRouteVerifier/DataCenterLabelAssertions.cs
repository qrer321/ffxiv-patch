using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
        }
    }
}
