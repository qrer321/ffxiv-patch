using System;
namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void ExpectText(string sheet, uint rowId, string expected)
            {
                ExpectText(sheet, rowId, expected, _language);
            }

            private void ExpectText(string sheet, uint rowId, string expected, string language)
            {
                string actual = GetFirstString(_patchedText, sheet, rowId, language);
                if (string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Pass("{0}#{1}/{2} = {3}", sheet, rowId, language, expected);
                    return;
                }

                Fail("{0}#{1}/{2} expected [{3}], actual [{4}]", sheet, rowId, language, expected, Escape(actual));
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

        }
    }
}
