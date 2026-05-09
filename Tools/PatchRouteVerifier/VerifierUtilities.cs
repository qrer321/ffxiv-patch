using System;
using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static bool LooksLikeMissingGlyphFallback(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int visible = 0;
            int fallback = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                {
                    continue;
                }

                visible++;
                if (ch == '-' || ch == '=' || ch == '\uFF0D' || ch == '\u30FC')
                {
                    fallback++;
                }
            }

            return visible > 0 && visible == fallback;
        }

        private static bool ContainsHangul(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (IsHangulCodepoint(value[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static uint[] CreateHangulCodepoints(string[] phrases)
        {
            System.Collections.Generic.HashSet<uint> codepoints = new System.Collections.Generic.HashSet<uint>();
            for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                string phrase = phrases[phraseIndex] ?? string.Empty;
                for (int charIndex = 0; charIndex < phrase.Length; charIndex++)
                {
                    uint codepoint = ReadCodepoint(phrase, ref charIndex);
                    if (IsHangulCodepoint(codepoint))
                    {
                        codepoints.Add(codepoint);
                    }
                }
            }

            uint[] values = new uint[codepoints.Count];
            codepoints.CopyTo(values);
            System.Array.Sort(values);
            return values;
        }

        private static bool IsHangulCodepoint(uint codepoint)
        {
            return (codepoint >= 0xAC00 && codepoint <= 0xD7A3) ||
                   (codepoint >= 0x1100 && codepoint <= 0x11FF) ||
                   (codepoint >= 0x3130 && codepoint <= 0x318F) ||
                   (codepoint >= 0xA960 && codepoint <= 0xA97F) ||
                   (codepoint >= 0xD7B0 && codepoint <= 0xD7FF);
        }

        private static bool HasRange(byte[] data, int offset, int length)
        {
            return data != null &&
                   offset >= 0 &&
                   length >= 0 &&
                   offset <= data.Length - length;
        }

        private static bool HasAsciiMagic(byte[] data, int offset, string magic)
        {
            if (string.IsNullOrEmpty(magic) || !HasRange(data, offset, magic.Length))
            {
                return false;
            }

            for (int i = 0; i < magic.Length; i++)
            {
                if (data[offset + i] != (byte)magic[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static uint ReadCodepoint(string value, ref int index)
        {
            if (char.IsHighSurrogate(value[index]) &&
                index + 1 < value.Length &&
                char.IsLowSurrogate(value[index + 1]))
            {
                uint codepoint = (uint)char.ConvertToUtf32(value[index], value[index + 1]);
                index++;
                return codepoint;
            }

            return value[index];
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("X2"));
            }

            return builder.ToString();
        }
    }
}
