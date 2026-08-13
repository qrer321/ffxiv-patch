using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class RsvStringResolver
    {
        public static readonly RsvStringResolver Empty = new RsvStringResolver(
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            -1,
            null);

        private static readonly byte[] AutoTranslateOpenGlyph = new byte[]
        {
            0x02, 0x13, 0x06, 0xFE, 0xFF, 0x7F, 0xBF, 0x5F, 0x03,
            0xEE, 0x81, 0x80,
            0x02, 0x13, 0x02, 0xEC, 0x03
        };

        private static readonly byte[] AutoTranslateCloseGlyph = new byte[]
        {
            0x02, 0x13, 0x06, 0xFE, 0xFF, 0xC1, 0x58, 0x4F, 0x03,
            0xEE, 0x81, 0x81,
            0x02, 0x13, 0x02, 0xEC, 0x03
        };

        private static readonly byte[] SeStringNewLine =
        {
            0x02, 0x10, 0x01, 0x03
        };

        private static readonly byte[] KefkaKoreanAutoTranslateGreeting =
            BuildKefkaKoreanAutoTranslateGreeting();

        private readonly Dictionary<string, byte[]> _values;
        private readonly int _sourceRsvLanguageId;

        private RsvStringResolver(Dictionary<string, byte[]> values, int sourceRsvLanguageId, string sourcePath)
        {
            _values = values ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
            _sourceRsvLanguageId = sourceRsvLanguageId;
            SourcePath = sourcePath;
        }

        public string SourcePath { get; private set; }

        public int Count
        {
            get { return _values.Count; }
        }

        public bool IsEnabled
        {
            get { return _values.Count > 0 && _sourceRsvLanguageId >= 0; }
        }

        public static RsvStringResolver Load(string path, string sourceLanguage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Empty;
            }

            string fullPath = Path.GetFullPath(path.Trim('"'));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("RSV map file is missing.", fullPath);
            }

            int sourceRsvLanguageId = RsvLanguageIdFromCode(sourceLanguage);
            if (sourceRsvLanguageId < 0)
            {
                throw new ArgumentException("Unsupported RSV source language: " + sourceLanguage);
            }

            string json = File.ReadAllText(fullPath, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, string> parsed = serializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed == null)
            {
                throw new InvalidDataException("RSV map JSON must be an object of string keys and string values: " + fullPath);
            }

            Dictionary<string, byte[]> values = new Dictionary<string, byte[]>(parsed.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in parsed)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value == null)
                {
                    continue;
                }

                values[entry.Key] = EncodeRsvValue(entry.Key, entry.Value);
            }

            return new RsvStringResolver(values, sourceRsvLanguageId, fullPath);
        }

        private static byte[] EncodeRsvValue(string token, string value)
        {
            if (!IsKefkaKoreanAutoTranslateGreetingToken(token))
            {
                return Encoding.UTF8.GetBytes(value);
            }

            const string extractedValue =
                "   7 여기는 처음 옵니다.   8 \n   7 잘 부탁합니다!   8 ";
            if (!string.Equals(value, extractedValue, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Known RSV auto-translate greeting changed: " + token);
            }

            return KefkaKoreanAutoTranslateGreeting;
        }

        private static bool IsKefkaKoreanAutoTranslateGreetingToken(string token)
        {
            const string keyPrefix = "_rsv_45500_-1_6_0_";
            const string keySuffix = "_S13095D61_E13095D61";
            return string.Equals(token, keyPrefix + "0" + keySuffix, StringComparison.Ordinal) ||
                   string.Equals(token, keyPrefix + "1" + keySuffix, StringComparison.Ordinal);
        }

        private static byte[] BuildKefkaKoreanAutoTranslateGreeting()
        {
            using (MemoryStream output = new MemoryStream(128))
            {
                WriteBytes(output, AutoTranslateOpenGlyph);
                WriteUtf8(output, "여기는 처음 옵니다.");
                WriteBytes(output, AutoTranslateCloseGlyph);
                WriteBytes(output, SeStringNewLine);
                WriteBytes(output, AutoTranslateOpenGlyph);
                WriteUtf8(output, "잘 부탁합니다!");
                WriteBytes(output, AutoTranslateCloseGlyph);
                return output.ToArray();
            }
        }

        private static void WriteUtf8(Stream output, string value)
        {
            WriteBytes(output, Encoding.UTF8.GetBytes(value));
        }

        private static void WriteBytes(Stream output, byte[] bytes)
        {
            output.Write(bytes, 0, bytes.Length);
        }


        public RsvResolutionResult Resolve(byte[] input)
        {
            if (!IsEnabled || input == null || input.Length < 5 || !ContainsRsvToken(input))
            {
                return new RsvResolutionResult(input, 0, 0);
            }

            MemoryStream output = new MemoryStream(input.Length);
            int offset = 0;
            int resolved = 0;
            int unresolved = 0;

            while (offset < input.Length)
            {
                int tokenStart = IndexOfRsvToken(input, offset);
                if (tokenStart < 0)
                {
                    output.Write(input, offset, input.Length - offset);
                    break;
                }

                output.Write(input, offset, tokenStart - offset);
                int tokenEnd = tokenStart + 5;
                while (tokenEnd < input.Length && IsRsvTokenByte(input[tokenEnd]))
                {
                    tokenEnd++;
                }

                string token = Encoding.ASCII.GetString(input, tokenStart, tokenEnd - tokenStart);
                byte[] valueBytes;
                if (TryGetValue(token, out valueBytes))
                {
                    output.Write(valueBytes, 0, valueBytes.Length);
                    resolved++;
                }
                else
                {
                    output.Write(input, tokenStart, tokenEnd - tokenStart);
                    unresolved++;
                }

                offset = tokenEnd;
            }

            if (resolved == 0)
            {
                return new RsvResolutionResult(input, 0, unresolved);
            }

            return new RsvResolutionResult(output.ToArray(), resolved, unresolved);
        }

        public bool TryGetValue(string token, out byte[] valueBytes)
        {
            if (string.IsNullOrEmpty(token))
            {
                valueBytes = null;
                return false;
            }

            string sourceToken = BuildLanguageVariantKey(token, _sourceRsvLanguageId);
            if (sourceToken != null && _values.TryGetValue(sourceToken, out valueBytes))
            {
                return true;
            }

            if (_values.TryGetValue(token, out valueBytes))
            {
                return true;
            }

            valueBytes = null;
            return false;
        }

        public static bool ContainsRsvToken(byte[] bytes)
        {
            return IndexOfRsvToken(bytes, 0) >= 0;
        }

        private static int IndexOfRsvToken(byte[] bytes, int start)
        {
            if (bytes == null || bytes.Length < 5)
            {
                return -1;
            }

            for (int i = Math.Max(0, start); i <= bytes.Length - 5; i++)
            {
                if (bytes[i] == (byte)'_' &&
                    (bytes[i + 1] == (byte)'r' || bytes[i + 1] == (byte)'R') &&
                    (bytes[i + 2] == (byte)'s' || bytes[i + 2] == (byte)'S') &&
                    (bytes[i + 3] == (byte)'v' || bytes[i + 3] == (byte)'V') &&
                    bytes[i + 4] == (byte)'_')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsRsvTokenByte(byte value)
        {
            return (value >= (byte)'A' && value <= (byte)'Z') ||
                   (value >= (byte)'a' && value <= (byte)'z') ||
                   (value >= (byte)'0' && value <= (byte)'9') ||
                   value == (byte)'_' ||
                   value == (byte)'-';
        }

        private static string BuildLanguageVariantKey(string token, int rsvLanguageId)
        {
            string marker = "_-1_";
            int start = token.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            int end = token.IndexOf('_', start);
            if (end <= start)
            {
                return null;
            }

            return token.Substring(0, start) +
                   rsvLanguageId.ToString() +
                   token.Substring(end);
        }

        private static int RsvLanguageIdFromCode(string code)
        {
            switch ((code ?? string.Empty).ToLowerInvariant())
            {
                case "ja":
                    return 0;
                case "en":
                    return 1;
                case "de":
                    return 2;
                case "fr":
                    return 3;
                case "chs":
                    return 4;
                case "cht":
                    return 5;
                case "ko":
                    return 6;
                default:
                    return -1;
            }
        }
    }

    internal sealed class RsvResolutionResult
    {
        public readonly byte[] Bytes;
        public readonly int ResolvedTokens;
        public readonly int UnresolvedTokens;

        public RsvResolutionResult(byte[] bytes, int resolvedTokens, int unresolvedTokens)
        {
            Bytes = bytes;
            ResolvedTokens = resolvedTokens;
            UnresolvedTokens = unresolvedTokens;
        }

        public bool Changed
        {
            get { return ResolvedTokens > 0; }
        }
    }
}
