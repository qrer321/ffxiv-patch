using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyAppliedOutputFiles()
            {
                Console.WriteLine("[APPLIED] Generated output file match");
                if (!_compareAppliedOutput)
                {
                    Pass("applied output file check skipped; pass --applied-game to compare an installed game folder");
                    return;
                }

                VerifyAppliedArchiveFiles("font", _generatedFont, _patchedFont, CollectAppliedFontPaths(), false);
                VerifyAppliedArchiveFiles("font", _generatedFont, _patchedFont, CollectAppliedFontTexturePaths(), true);
                VerifyAppliedArchiveFiles("ui", _generatedUi, _patchedUi, CollectAppliedRequiredUiPaths(), false);
                VerifyAppliedArchiveFiles("ui", _generatedUi, _patchedUi, CollectAppliedOptionalUiPaths(), true);
            }

            private void VerifyAppliedArchiveFiles(
                string archiveName,
                CompositeArchive generatedArchive,
                CompositeArchive appliedArchive,
                string[] paths,
                bool optional)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    VerifyAppliedArchiveFile(archiveName, generatedArchive, appliedArchive, paths[i], optional);
                }
            }

            private void VerifyAppliedArchiveFile(
                string archiveName,
                CompositeArchive generatedArchive,
                CompositeArchive appliedArchive,
                string path,
                bool optional)
            {
                byte[] generated;
                if (!generatedArchive.TryReadPackedFile(path, out generated))
                {
                    if (optional)
                    {
                        return;
                    }

                    Fail("{0} generated output is missing {1}", archiveName, path);
                    return;
                }

                byte[] applied;
                if (!appliedArchive.TryReadPackedFile(path, out applied))
                {
                    Fail("{0} applied game is missing {1}", archiveName, path);
                    return;
                }

                if (BytesEqual(generated, applied))
                {
                    Pass("{0} applied file matches generated output: {1}, bytes={2}", archiveName, path, generated.Length);
                    return;
                }

                Fail(
                    "{0} applied file differs from generated output: {1}, {2}",
                    archiveName,
                    path,
                    FormatGeneratedAppliedByteDifference(generated, applied));
            }

            private static string[] CollectAppliedFontPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddAppliedValues(paths, LobbyPhraseFontPaths);
                AddAppliedValues(paths, DialoguePhraseFontPaths);
                AddAppliedValues(paths, SystemSettingsScaledFonts);
                AddAppliedValues(paths, DataCenterTitleSameFontChecks);
                AddAppliedValues(paths, NumericGlyphSameFontChecks);
                AddAppliedValues(paths, ProtectedHangulFonts);
                AddAppliedValues(paths, PartyListSelfMarkerSameFontChecks);
                AddAppliedValues(paths, LobbyHangulVisibilityFonts);
                for (int i = 0; i < DataCenterTitleKoreanFontChecks.GetLength(0); i++)
                {
                    paths.Add(DataCenterTitleKoreanFontChecks[i, 0]);
                    paths.Add(DataCenterTitleKoreanFontChecks[i, 1]);
                }

                for (int i = 0; i < NumericGlyphKoreanFontChecks.GetLength(0); i++)
                {
                    paths.Add(NumericGlyphKoreanFontChecks[i, 0]);
                    paths.Add(NumericGlyphKoreanFontChecks[i, 1]);
                }

                for (int i = 0; i < PartyListSelfMarkerKoreanFontChecks.GetLength(0); i++)
                {
                    paths.Add(PartyListSelfMarkerKoreanFontChecks[i, 0]);
                    paths.Add(PartyListSelfMarkerKoreanFontChecks[i, 1]);
                }

                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    paths.Add(Derived4kLobbyFontPairs[i, 0]);
                    paths.Add(Derived4kLobbyFontPairs[i, 1]);
                }

                return ToSortedArray(paths);
            }

            private static string[] CollectAppliedFontTexturePaths()
            {
                return new string[]
                {
                    Font1TexturePath,
                    Font2TexturePath,
                    Font3TexturePath,
                    Font4TexturePath,
                    Font5TexturePath,
                    Font6TexturePath,
                    Font7TexturePath,
                    FontLobby1TexturePath,
                    FontLobby2TexturePath,
                    FontLobby3TexturePath,
                    FontLobby4TexturePath,
                    FontLobby5TexturePath,
                    FontLobby6TexturePath,
                    FontLobby7TexturePath,
                    FontKrnTexturePath
                };
            }

            private static string[] CollectAppliedRequiredUiPaths()
            {
                return new string[]
                {
                    DataCenterTitleUldPath,
                    DataCenterWorldmapUldPath
                };
            }

            private static string[] CollectAppliedOptionalUiPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < StartScreenSystemSettingsUldCandidates.Length; i++)
                {
                    paths.Add(StartScreenSystemSettingsUldCandidates[i].Path);
                }

                return ToSortedArray(paths);
            }

            private static void AddAppliedValues(HashSet<string> paths, string[] values)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    paths.Add(values[i]);
                }
            }

            private static string[] ToSortedArray(HashSet<string> values)
            {
                string[] result = new string[values.Count];
                values.CopyTo(result);
                Array.Sort(result, StringComparer.OrdinalIgnoreCase);
                return result;
            }

            private static string FormatGeneratedAppliedByteDifference(byte[] generated, byte[] applied)
            {
                int index = FindFirstByteDifference(generated, applied);
                if (index < 0)
                {
                    return "no byte difference";
                }

                string generatedValue = index < generated.Length ? "0x" + generated[index].ToString("X2") : "<missing>";
                string appliedValue = index < applied.Length ? "0x" + applied[index].ToString("X2") : "<missing>";
                return "relative=0x" + index.ToString("X") + ", generated=" + generatedValue + ", applied=" + appliedValue;
            }
        }
    }
}
