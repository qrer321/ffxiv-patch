using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] LobbyVerbatimTexturePaths = new string[]
            {
                FontLobby1TexturePath,
                FontLobby2TexturePath,
                FontLobby3TexturePath,
                FontLobby4TexturePath,
                FontLobby5TexturePath,
                FontLobby6TexturePath,
                FontLobby7TexturePath
            };

            private void VerifyLobbyTtmpPayloads()
            {
                Console.WriteLine("[FDT/TEX] lobby TTMP payload preservation");
                if (_ttmpFont == null)
                {
                    Fail("TTMP font package is required to verify lobby payload preservation");
                    return;
                }

                int checkedPayloads = 0;
                for (int i = 0; i < LobbyPhraseFontPaths.Length; i++)
                {
                    checkedPayloads += VerifyLobbyPayload(LobbyPhraseFontPaths[i]);
                }

                for (int i = 0; i < LobbyVerbatimTexturePaths.Length; i++)
                {
                    checkedPayloads += VerifyLobbyPayload(LobbyVerbatimTexturePaths[i]);
                }

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    string targetPath = Derived4kLobbyFontPairs[i, 0];
                    if (!_ttmpFont.ContainsPath(targetPath))
                    {
                        checkedPayloads += VerifyLobbyPayloadMatchesClean(targetPath);
                    }
                }

                if (checkedPayloads == 0)
                {
                    Fail("No lobby font payloads were checked against TTMP or clean source");
                }
                else
                {
                    Pass("lobby payload preservation checked: {0}", checkedPayloads);
                }
            }

            private int VerifyLobbyPayload(string path)
            {
                if (_ttmpFont.ContainsPath(path))
                {
                    return VerifyLobbyPayloadMatchesTtmp(path);
                }

                return VerifyLobbyPayloadMatchesClean(path);
            }

            private int VerifyLobbyPayloadMatchesTtmp(string path)
            {
                byte[] expected;
                byte[] actual;
                if (IsTexturePayload(path))
                {
                    byte[] expectedPacked;
                    byte[] actualPacked;
                    if (!_ttmpFont.TryReadPackedFile(path, out expectedPacked) ||
                        !_patchedFont.TryReadPackedFile(path, out actualPacked))
                    {
                        Fail("{0} texture payload was not readable for TTMP preservation check", path);
                        return 1;
                    }

                    expected = UnpackTextureFile(expectedPacked);
                    actual = UnpackTextureFile(actualPacked);
                }
                else
                {
                    expected = _ttmpFont.ReadFile(path);
                    actual = _patchedFont.ReadFile(path);
                }

                if (!BytesEqual(expected, actual))
                {
                    Fail("{0} differs from TTMP payload; lobby glyph/texture recomposition is still active: {1}", path, FormatByteDifference(expected, actual));
                    return 1;
                }

                Pass("{0} matches TTMP payload", path);
                return 1;
            }

            private int VerifyLobbyPayloadMatchesClean(string path)
            {
                if (!_cleanFont.ContainsPath(path))
                {
                    Warn("{0} clean lobby payload missing; skipped preservation check", path);
                    return 0;
                }

                if (!_patchedFont.ContainsPath(path))
                {
                    Warn("{0} patched lobby payload missing; skipped preservation check", path);
                    return 0;
                }

                byte[] expected;
                byte[] actual;
                if (IsTexturePayload(path))
                {
                    byte[] expectedPacked;
                    byte[] actualPacked;
                    if (!_cleanFont.TryReadPackedFile(path, out expectedPacked) ||
                        !_patchedFont.TryReadPackedFile(path, out actualPacked))
                    {
                        Fail("{0} texture payload was not readable for clean preservation check", path);
                        return 1;
                    }

                    expected = UnpackTextureFile(expectedPacked);
                    actual = UnpackTextureFile(actualPacked);
                }
                else
                {
                    expected = _cleanFont.ReadFile(path);
                    actual = _patchedFont.ReadFile(path);
                }

                if (!BytesEqual(expected, actual))
                {
                    Fail("{0} differs from clean global payload; derived lobby recomposition is still active: {1}", path, FormatByteDifference(expected, actual));
                    return 1;
                }

                Pass("{0} matches clean global payload", path);
                return 1;
            }

            private static bool IsTexturePayload(string path)
            {
                return (path ?? string.Empty).EndsWith(".tex", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
