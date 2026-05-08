using System;

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
