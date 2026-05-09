using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed class TtmpFontPackage : IDisposable
        {
            private const string TtmpMpdFileName = "TTMPD.mpd";
            private const string TtmpMplFileName = "TTMPL.mpl";

            private readonly FileStream _mpdStream;
            private readonly Dictionary<string, FontPayload> _payloadsByPath;

            public string DirectoryPath { get; private set; }
            public string CacheKey { get { return "ttmp:" + DirectoryPath; } }

            private TtmpFontPackage(string directoryPath, FileStream mpdStream, Dictionary<string, FontPayload> payloadsByPath)
            {
                DirectoryPath = directoryPath;
                _mpdStream = mpdStream;
                _payloadsByPath = payloadsByPath;
            }

            public static TtmpFontPackage TryOpen(string explicitDirectory)
            {
                foreach (string directory in EnumerateCandidateDirs(explicitDirectory))
                {
                    string fullDirectory = Path.GetFullPath(directory);
                    string mpdPath = Path.Combine(fullDirectory, TtmpMpdFileName);
                    string mplPath = Path.Combine(fullDirectory, TtmpMplFileName);
                    if (!File.Exists(mpdPath) || !File.Exists(mplPath))
                    {
                        continue;
                    }

                    Dictionary<string, FontPayload> payloads = LoadPayloads(mplPath);
                    if (payloads.Count == 0)
                    {
                        continue;
                    }

                    FileStream mpdStream = new FileStream(mpdPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return new TtmpFontPackage(fullDirectory, mpdStream, payloads);
                }

                return null;
            }

            public bool ContainsPath(string gamePath)
            {
                return _payloadsByPath.ContainsKey(NormalizeGamePath(gamePath));
            }

            public byte[] ReadFile(string gamePath)
            {
                byte[] packed;
                if (!TryReadPackedFile(gamePath, out packed))
                {
                    throw new FileNotFoundException("TTMP payload was not found.", gamePath);
                }

                return SqPackArchive.UnpackStandardFile(packed);
            }

            public bool TryReadPackedFile(string gamePath, out byte[] packed)
            {
                FontPayload payload;
                if (!_payloadsByPath.TryGetValue(NormalizeGamePath(gamePath), out payload))
                {
                    packed = null;
                    return false;
                }

                if (payload.ModOffset < 0 ||
                    payload.ModSize <= 0 ||
                    payload.ModOffset + (long)payload.ModSize > _mpdStream.Length)
                {
                    throw new InvalidDataException("TTMP payload is outside TTMPD.mpd: " + gamePath);
                }

                packed = new byte[payload.ModSize];
                lock (_mpdStream)
                {
                    _mpdStream.Position = payload.ModOffset;
                    int totalRead = 0;
                    while (totalRead < packed.Length)
                    {
                        int read = _mpdStream.Read(packed, totalRead, packed.Length - totalRead);
                        if (read == 0)
                        {
                            break;
                        }

                        totalRead += read;
                    }

                    if (totalRead != packed.Length)
                    {
                        throw new EndOfStreamException("Could not read TTMP payload: " + gamePath);
                    }
                }

                return true;
            }

            public void Dispose()
            {
                _mpdStream.Dispose();
            }

            private static IEnumerable<string> EnumerateCandidateDirs(string explicitDirectory)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string current = Directory.GetCurrentDirectory();
                string[] candidates = new string[]
                {
                    explicitDirectory,
                    AppDomain.CurrentDomain.BaseDirectory,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FontPatchAssets"),
                    current,
                    Path.Combine(current, "FontPatchAssets"),
                    Path.Combine(current, "FFXIVPatchGenerator", "bin", "Release"),
                    Path.Combine(current, "FFXIVPatchGenerator", "FontPatchAssets")
                };

                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = candidates[i];
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    string fullPath = Path.GetFullPath(candidate);
                    if (seen.Add(fullPath))
                    {
                        yield return fullPath;
                    }
                }
            }

            private static Dictionary<string, FontPayload> LoadPayloads(string mplPath)
            {
                Dictionary<string, FontPayload> payloads = new Dictionary<string, FontPayload>(StringComparer.OrdinalIgnoreCase);
                foreach (string rawLine in File.ReadLines(mplPath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        continue;
                    }

                    FontPayload payload = new FontPayload();
                    payload.FullPath = ExtractJsonString(rawLine, "FullPath");
                    payload.ModOffset = ExtractJsonInt(rawLine, "ModOffset");
                    payload.ModSize = ExtractJsonInt(rawLine, "ModSize");
                    if (payload.ModSize <= 0)
                    {
                        continue;
                    }

                    string normalized = NormalizeGamePath(payload.FullPath);
                    if (!payloads.ContainsKey(normalized))
                    {
                        payloads.Add(normalized, payload);
                    }
                }

                return payloads;
            }

            private static string NormalizeGamePath(string path)
            {
                return (path ?? string.Empty).Replace('\\', '/').Trim();
            }

            private static string ExtractJsonString(string jsonLine, string fieldName)
            {
                Match match = Regex.Match(jsonLine, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"");
                if (!match.Success)
                {
                    throw new InvalidDataException("Missing string field " + fieldName + " in TTMPL line: " + jsonLine);
                }

                return Regex.Unescape(match.Groups["value"].Value);
            }

            private static int ExtractJsonInt(string jsonLine, string fieldName)
            {
                Match match = Regex.Match(jsonLine, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*(?<value>-?\\d+)");
                if (!match.Success)
                {
                    throw new InvalidDataException("Missing integer field " + fieldName + " in TTMPL line: " + jsonLine);
                }

                return int.Parse(match.Groups["value"].Value);
            }

            private struct FontPayload
            {
                public string FullPath;
                public int ModOffset;
                public int ModSize;
            }
        }
    }
}
