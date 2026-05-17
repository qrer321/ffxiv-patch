using System;
using System.Collections.Generic;
using System.IO;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly string[] FontOnlyRequiredFiles = new string[]
            {
                "000000.win32.dat1",
                "000000.win32.index",
                "000000.win32.index2",
                "orig.000000.win32.index",
                "orig.000000.win32.index2"
            };

            private static readonly string[] FontOnlyForbiddenFiles = new string[]
            {
                "0a0000.win32.dat1",
                "0a0000.win32.index",
                "0a0000.win32.index2",
                "orig.0a0000.win32.index",
                "orig.0a0000.win32.index2",
                "patch-diagnostics.tsv",
                "060000.win32.dat4",
                "060000.win32.index",
                "060000.win32.index2",
                "orig.060000.win32.index",
                "orig.060000.win32.index2",
                "manifest.json"
            };

            private static readonly string[] FontOnlyAllowedPatchedFontEntries = new string[]
            {
                "common/font/KrnAXIS_120.fdt",
                "common/font/KrnAXIS_140.fdt",
                "common/font/KrnAXIS_180.fdt",
                "common/font/KrnAXIS_360.fdt",
                "common/font/font_krn_1.tex"
            };

            private static readonly string[] FontOnlyRequiredPatchedFontEntries = new string[]
            {
                "common/font/KrnAXIS_120.fdt",
                "common/font/KrnAXIS_140.fdt",
                "common/font/KrnAXIS_180.fdt",
                "common/font/KrnAXIS_360.fdt"
            };

            private static readonly string[] FontOnlyGuardedFontEntries = new string[]
            {
                "common/font/Jupiter_45.fdt",
                "common/font/Jupiter_45_lobby.fdt",
                "common/font/Jupiter_90.fdt",
                "common/font/Jupiter_90_lobby.fdt",
                "common/font/Jupiter_20_lobby.fdt",
                "common/font/Jupiter_23_lobby.fdt",
                "common/font/Jupiter_23.fdt",
                "common/font/Jupiter_46.fdt",
                "common/font/Jupiter_46_lobby.fdt",
                "common/font/Jupiter_16_lobby.fdt",
                "common/font/Meidinger_16_lobby.fdt",
                "common/font/Meidinger_20_lobby.fdt",
                "common/font/Meidinger_40.fdt",
                "common/font/Meidinger_40_lobby.fdt",
                "common/font/MiedingerMid_10_lobby.fdt",
                "common/font/MiedingerMid_12_lobby.fdt",
                "common/font/MiedingerMid_14_lobby.fdt",
                "common/font/MiedingerMid_18_lobby.fdt",
                "common/font/MiedingerMid_36.fdt",
                "common/font/MiedingerMid_36_lobby.fdt",
                "common/font/TrumpGothic_23.fdt",
                "common/font/TrumpGothic_23_lobby.fdt",
                "common/font/TrumpGothic_34.fdt",
                "common/font/TrumpGothic_34_lobby.fdt",
                "common/font/TrumpGothic_68.fdt",
                "common/font/TrumpGothic_68_lobby.fdt",
                "common/font/TrumpGothic_184.fdt",
                "common/font/TrumpGothic_184_lobby.fdt",
                "common/font/AXIS_12.fdt",
                "common/font/AXIS_12_lobby.fdt",
                "common/font/AXIS_14.fdt",
                "common/font/AXIS_14_lobby.fdt",
                "common/font/AXIS_18.fdt",
                "common/font/AXIS_18_lobby.fdt",
                "common/font/AXIS_36.fdt",
                "common/font/AXIS_36_lobby.fdt",
                "common/font/AXIS_96.fdt",
                "common/font/KrnAXIS_120.fdt",
                "common/font/KrnAXIS_140.fdt",
                "common/font/KrnAXIS_180.fdt",
                "common/font/KrnAXIS_360.fdt",
                "common/font/Jupiter_16.fdt",
                "common/font/Jupiter_20.fdt",
                "common/font/Meidinger_16.fdt",
                "common/font/Meidinger_20.fdt",
                "common/font/MiedingerMid_10.fdt",
                "common/font/MiedingerMid_12.fdt",
                "common/font/MiedingerMid_14.fdt",
                "common/font/MiedingerMid_18.fdt",
                "common/font/font1.tex",
                "common/font/font_lobby1.tex",
                "common/font/font2.tex",
                "common/font/font_lobby2.tex",
                "common/font/font3.tex",
                "common/font/font4.tex",
                "common/font/font5.tex",
                "common/font/font6.tex",
                "common/font/font7.tex",
                "common/font/font_lobby3.tex",
                "common/font/font_lobby4.tex",
                "common/font/font_lobby5.tex",
                "common/font/font_lobby6.tex",
                "common/font/font_lobby7.tex",
                "common/font/font_krn_1.tex"
            };

            private static readonly string[] FontOnlyKoreanReadableFonts = new string[]
            {
                "common/font/KrnAXIS_120.fdt",
                "common/font/KrnAXIS_140.fdt",
                "common/font/KrnAXIS_180.fdt",
                "common/font/KrnAXIS_360.fdt"
            };

            private static readonly uint[] FontOnlyKoreanSmokeCodepoints = new uint[]
            {
                0xD55Cu, // 한
                0xAE00u, // 글
                0xD14Cu, // 테
                0xC2A4u, // 스
                0xD2B8u  // 트
            };

            private void VerifyFontOnlyOutputScope()
            {
                Console.WriteLine("[Output] Font-only output scope");
                if (!IsVerificationStepExplicitlySelected("font-only-output-scope"))
                {
                    Warn("font-only output scope is skipped unless explicitly selected with --checks font-only-output-scope");
                    return;
                }

                for (int i = 0; i < FontOnlyRequiredFiles.Length; i++)
                {
                    string fileName = FontOnlyRequiredFiles[i];
                    string path = Path.Combine(_output, fileName);
                    if (File.Exists(path))
                    {
                        Pass("font-only required file exists: {0}", fileName);
                    }
                    else
                    {
                        Fail("font-only required file is missing: {0}", fileName);
                    }
                }

                for (int i = 0; i < FontOnlyForbiddenFiles.Length; i++)
                {
                    string fileName = FontOnlyForbiddenFiles[i];
                    string path = Path.Combine(_output, fileName);
                    if (File.Exists(path))
                    {
                        Fail("font-only output must not include {0}", fileName);
                    }
                    else
                    {
                        Pass("font-only forbidden file absent: {0}", fileName);
                    }
                }

                VerifyFontOnlyPatchedFontEntryScope();
                VerifyFontOnlyKoreanReadableGlyphs();
            }

            private void VerifyFontOnlyPatchedFontEntryScope()
            {
                string outputIndexPath = Path.Combine(_output, "000000.win32.index");
                string originalIndexPath = Path.Combine(_output, "orig.000000.win32.index");
                if (!File.Exists(outputIndexPath) || !File.Exists(originalIndexPath))
                {
                    return;
                }

                using (SqPackIndexFile outputIndex = new SqPackIndexFile(outputIndexPath))
                using (SqPackIndexFile originalIndex = new SqPackIndexFile(originalIndexPath))
                {
                    HashSet<string> allowed = new HashSet<string>(FontOnlyAllowedPatchedFontEntries, StringComparer.OrdinalIgnoreCase);
                    HashSet<string> changedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int allowedChanges = 0;
                    int blockedChanges = 0;
                    for (int i = 0; i < FontOnlyGuardedFontEntries.Length; i++)
                    {
                        string gamePath = FontOnlyGuardedFontEntries[i];
                        SqPackIndexEntry outputEntry;
                        SqPackIndexEntry originalEntry;
                        if (!outputIndex.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out outputEntry) ||
                            !originalIndex.TryGetEntry(SqPackHash.GetIndexHash(gamePath), out originalEntry) ||
                            outputEntry.Data == originalEntry.Data)
                        {
                            continue;
                        }

                        if (allowed.Contains(gamePath))
                        {
                            changedEntries.Add(gamePath);
                            allowedChanges++;
                        }
                        else
                        {
                            blockedChanges++;
                            Fail("font-only must not modify shared/special font entry: {0}", gamePath);
                        }
                    }

                    if (allowedChanges > 0)
                    {
                        Pass("font-only patched only allowed font entries: allowed={0}, blocked={1}", allowedChanges, blockedChanges);
                    }
                    else
                    {
                        Fail("font-only did not modify any allowed font entries");
                    }

                    if (blockedChanges == 0)
                    {
                        Pass("font-only left guarded shared/special font entries untouched");
                    }

                    for (int i = 0; i < FontOnlyRequiredPatchedFontEntries.Length; i++)
                    {
                        string gamePath = FontOnlyRequiredPatchedFontEntries[i];
                        if (changedEntries.Contains(gamePath))
                        {
                            Pass("font-only required game-font entry patched: {0}", gamePath);
                        }
                        else
                        {
                            Fail("font-only required game-font entry was not patched: {0}", gamePath);
                        }
                    }
                }
            }

            private void VerifyFontOnlyKoreanReadableGlyphs()
            {
                for (int fontIndex = 0; fontIndex < FontOnlyKoreanReadableFonts.Length; fontIndex++)
                {
                    for (int codepointIndex = 0; codepointIndex < FontOnlyKoreanSmokeCodepoints.Length; codepointIndex++)
                    {
                        ExpectGlyphVisibleAtLeast(
                            _patchedFont,
                            FontOnlyKoreanReadableFonts[fontIndex],
                            FontOnlyKoreanSmokeCodepoints[codepointIndex],
                            5);
                    }
                }
            }

            private bool IsVerificationStepExplicitlySelected(string checkName)
            {
                if (_selectedChecks == null || _selectedChecks.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < _selectedChecks.Length; i++)
                {
                    if (string.Equals(_selectedChecks[i], checkName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
