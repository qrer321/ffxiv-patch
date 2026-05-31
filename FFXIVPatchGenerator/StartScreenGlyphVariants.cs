using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal struct StartScreenGlyphVariant
    {
        public readonly uint SourceCodepoint;
        public readonly uint AliasCodepoint;

        public StartScreenGlyphVariant(uint sourceCodepoint, uint aliasCodepoint)
        {
            SourceCodepoint = sourceCodepoint;
            AliasCodepoint = aliasCodepoint;
        }
    }

    internal static class StartScreenGlyphVariants
    {
        private static readonly bool Enabled = IsEnabled();
        private const uint AliasBaseCodepoint = 0xF700u;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static readonly string[] KnownPhrases = Combine(
            LobbyScaledHangulPhrases.HighResolutionUiScaleOptions,
            LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);

        public static readonly StartScreenGlyphVariant[] Aliases = Enabled
            ? CreateAliases()
            : new StartScreenGlyphVariant[0];
        private static readonly string[] VariantPhraseReplacements = CreateVariantPhraseReplacements();

        public static byte[] ApplyToAddonBytes(byte[] bytes)
        {
            if (!Enabled || bytes == null || bytes.Length == 0 || Aliases.Length == 0)
            {
                return bytes;
            }

            byte[] result = bytes;
            for (int i = 0; i < KnownPhrases.Length; i++)
            {
                string phrase = KnownPhrases[i] ?? string.Empty;
                string replacement = VariantPhraseReplacements[i] ?? string.Empty;
                if (phrase.Length == 0 || string.Equals(phrase, replacement, StringComparison.Ordinal))
                {
                    continue;
                }

                result = ReplaceAll(result, Utf8.GetBytes(phrase), Utf8.GetBytes(replacement));
            }

            return result;
        }

        public static string ApplyToKnownPhrases(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text) || Aliases.Length == 0)
            {
                return text;
            }

            string result = text;
            for (int i = 0; i < KnownPhrases.Length; i++)
            {
                string phrase = KnownPhrases[i] ?? string.Empty;
                string replacement = VariantPhraseReplacements[i] ?? string.Empty;
                if (phrase.Length == 0 || string.Equals(phrase, replacement, StringComparison.Ordinal))
                {
                    continue;
                }

                result = result.Replace(phrase, replacement);
            }

            return result;
        }

        public static string NormalizeAliases(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text) || Aliases.Length == 0)
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                uint codepoint = ReadCodepoint(text, ref i);
                uint source;
                if (TryGetSourceCodepoint(codepoint, out source))
                {
                    AppendCodepoint(builder, source);
                }
                else
                {
                    AppendCodepoint(builder, codepoint);
                }
            }

            return builder.ToString();
        }

        public static bool ShouldApplyToAddonRow(uint rowId)
        {
            if (!Enabled)
            {
                return false;
            }

            AddonRowRange[] ranges = LobbyScaledHangulPhrases.StartScreenSystemSettingsAddonRowRanges;
            for (int i = 0; i < ranges.Length; i++)
            {
                if (ranges[i].Contains(rowId))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAlias(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text) || Aliases.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                uint codepoint = ReadCodepoint(text, ref i);
                uint ignored;
                if (TryGetSourceCodepoint(codepoint, out ignored))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsAliasSourceCodepoint(uint codepoint)
        {
            if (!Enabled)
            {
                return false;
            }

            uint ignored;
            return TryGetAliasCodepoint(codepoint, out ignored);
        }

        public static bool TryGetAliasCodepoint(uint sourceCodepoint, out uint aliasCodepoint)
        {
            if (!Enabled)
            {
                aliasCodepoint = 0;
                return false;
            }

            for (int i = 0; i < Aliases.Length; i++)
            {
                if (Aliases[i].SourceCodepoint == sourceCodepoint)
                {
                    aliasCodepoint = Aliases[i].AliasCodepoint;
                    return true;
                }
            }

            aliasCodepoint = 0;
            return false;
        }

        public static bool TryGetSourceCodepoint(uint aliasCodepoint, out uint sourceCodepoint)
        {
            if (!Enabled)
            {
                sourceCodepoint = 0;
                return false;
            }

            for (int i = 0; i < Aliases.Length; i++)
            {
                if (Aliases[i].AliasCodepoint == aliasCodepoint)
                {
                    sourceCodepoint = Aliases[i].SourceCodepoint;
                    return true;
                }
            }

            sourceCodepoint = 0;
            return false;
        }

        private static StartScreenGlyphVariant[] CreateAliases()
        {
            SortedSet<uint> sourceCodepoints = new SortedSet<uint>();
            for (int phraseIndex = 0; phraseIndex < KnownPhrases.Length; phraseIndex++)
            {
                string phrase = KnownPhrases[phraseIndex] ?? string.Empty;
                List<uint> codepoints = ReadCodepoints(phrase);
                for (int i = 0; i + 1 < codepoints.Count; i++)
                {
                    if (ShouldAliasLeftCodepoint(codepoints[i], codepoints[i + 1]))
                    {
                        sourceCodepoints.Add(codepoints[i]);
                    }
                }
            }

            StartScreenGlyphVariant[] aliases = new StartScreenGlyphVariant[sourceCodepoints.Count];
            int index = 0;
            foreach (uint sourceCodepoint in sourceCodepoints)
            {
                aliases[index] = new StartScreenGlyphVariant(sourceCodepoint, AliasBaseCodepoint + (uint)index);
                index++;
            }

            return aliases;
        }

        private static bool IsEnabled()
        {
            return false;
        }

        private static string[] CreateVariantPhraseReplacements()
        {
            string[] replacements = new string[KnownPhrases.Length];
            for (int i = 0; i < KnownPhrases.Length; i++)
            {
                replacements[i] = ApplyToSingleKnownPhrase(KnownPhrases[i] ?? string.Empty);
            }

            return replacements;
        }

        private static string ApplyToSingleKnownPhrase(string phrase)
        {
            if (string.IsNullOrEmpty(phrase) || Aliases.Length == 0)
            {
                return phrase;
            }

            List<uint> codepoints = ReadCodepoints(phrase);
            StringBuilder builder = new StringBuilder(phrase.Length);
            for (int i = 0; i < codepoints.Count; i++)
            {
                uint codepoint = codepoints[i];
                uint alias;
                if (i + 1 < codepoints.Count &&
                    ShouldAliasLeftCodepoint(codepoint, codepoints[i + 1]) &&
                    TryGetAliasCodepoint(codepoint, out alias))
                {
                    AppendCodepoint(builder, alias);
                }
                else
                {
                    AppendCodepoint(builder, codepoint);
                }
            }

            return builder.ToString();
        }

        private static bool ShouldAliasLeftCodepoint(uint left, uint right)
        {
            return IsHangulCodepoint(left) &&
                   (IsHangulCodepoint(right) || IsTerminalPunctuationCodepoint(right));
        }

        private static string[] Combine(params string[][] groups)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < groups.Length; i++)
            {
                string[] group = groups[i];
                if (group == null)
                {
                    continue;
                }

                for (int j = 0; j < group.Length; j++)
                {
                    if (!string.IsNullOrEmpty(group[j]))
                    {
                        values.Add(group[j]);
                    }
                }
            }

            return values.ToArray();
        }

        private static byte[] ReplaceAll(byte[] source, byte[] search, byte[] replacement)
        {
            if (source == null || source.Length == 0 || search == null || search.Length == 0 || replacement == null)
            {
                return source;
            }

            MemoryStream output = null;
            int cursor = 0;
            int lastCopy = 0;
            while (cursor <= source.Length - search.Length)
            {
                if (!BytesMatch(source, cursor, search))
                {
                    cursor++;
                    continue;
                }

                if (output == null)
                {
                    output = new MemoryStream(source.Length);
                }

                int unchangedLength = cursor - lastCopy;
                if (unchangedLength > 0)
                {
                    output.Write(source, lastCopy, unchangedLength);
                }

                output.Write(replacement, 0, replacement.Length);
                cursor += search.Length;
                lastCopy = cursor;
            }

            if (output == null)
            {
                return source;
            }

            if (lastCopy < source.Length)
            {
                output.Write(source, lastCopy, source.Length - lastCopy);
            }

            return output.ToArray();
        }

        private static bool BytesMatch(byte[] source, int offset, byte[] search)
        {
            if (offset < 0 || source == null || search == null || offset > source.Length - search.Length)
            {
                return false;
            }

            for (int i = 0; i < search.Length; i++)
            {
                if (source[offset + i] != search[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static List<uint> ReadCodepoints(string text)
        {
            List<uint> codepoints = new List<uint>();
            if (text == null)
            {
                return codepoints;
            }

            for (int i = 0; i < text.Length; i++)
            {
                codepoints.Add(ReadCodepoint(text, ref i));
            }

            return codepoints;
        }

        private static uint ReadCodepoint(string text, ref int index)
        {
            char ch = text[index];
            if (char.IsHighSurrogate(ch) &&
                index + 1 < text.Length &&
                char.IsLowSurrogate(text[index + 1]))
            {
                int codepoint = char.ConvertToUtf32(ch, text[index + 1]);
                index++;
                return checked((uint)codepoint);
            }

            return ch;
        }

        private static void AppendCodepoint(StringBuilder builder, uint codepoint)
        {
            if (codepoint <= 0xFFFFu)
            {
                builder.Append((char)codepoint);
                return;
            }

            builder.Append(char.ConvertFromUtf32(checked((int)codepoint)));
        }

        private static bool IsHangulCodepoint(uint codepoint)
        {
            return codepoint >= 0xAC00u && codepoint <= 0xD7A3u;
        }

        private static bool IsTerminalPunctuationCodepoint(uint codepoint)
        {
            return codepoint == 0x002Eu ||
                   codepoint == 0x3002u ||
                   codepoint == 0xFF0Eu;
        }
    }
}
