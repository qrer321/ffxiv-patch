using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
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
        }
    }
}
