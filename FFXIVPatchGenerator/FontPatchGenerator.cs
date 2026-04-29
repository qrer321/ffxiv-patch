using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class FontPatchGenerator
    {
        private const string RepositoryDir = "sqpack\\ffxiv";
        private const string IndexFileName = "000000.win32.index";
        private const string Dat0FileName = "000000.win32.dat0";
        private const string Dat1FileName = "000000.win32.dat1";
        private const string OrigIndexFileName = "orig.000000.win32.index";

        private static readonly string[] FontPaths = new string[]
        {
            "common/font/Jupiter_45.fdt",
            "common/font/Jupiter_45_lobby.fdt",
            "common/font/Jupiter_90.fdt",
            "common/font/Jupiter_20_lobby.fdt",
            "common/font/Jupiter_23_lobby.fdt",
            "common/font/Jupiter_23.fdt",
            "common/font/Jupiter_46.fdt",
            "common/font/Jupiter_16_lobby.fdt",
            "common/font/Meidinger_16_lobby.fdt",
            "common/font/Meidinger_20_lobby.fdt",
            "common/font/Meidinger_40.fdt",
            "common/font/MiedingerMid_10_lobby.fdt",
            "common/font/MiedingerMid_12_lobby.fdt",
            "common/font/MiedingerMid_14_lobby.fdt",
            "common/font/MiedingerMid_18_lobby.fdt",
            "common/font/MiedingerMid_36.fdt",
            "common/font/TrumpGothic_23.fdt",
            "common/font/TrumpGothic_23_lobby.fdt",
            "common/font/TrumpGothic_34.fdt",
            "common/font/TrumpGothic_34_lobby.fdt",
            "common/font/TrumpGothic_68.fdt",
            "common/font/TrumpGothic_184.fdt",
            "common/font/TrumpGothic_184_lobby.fdt",
            "common/font/AXIS_12.fdt",
            "common/font/AXIS_12_lobby.fdt",
            "common/font/AXIS_14.fdt",
            "common/font/AXIS_14_lobby.fdt",
            "common/font/AXIS_18.fdt",
            "common/font/AXIS_18_lobby.fdt",
            "common/font/AXIS_36.fdt",
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
            "common/font/font_krn_1.tex",
            "common/font/font_krn_2.tex",
            "common/font/font_krn_3.tex",
            "common/font/font_krn_4.tex",
            "common/font/font_krn_5.tex",
            "common/font/font_krn_6.tex",
            "common/font/font_krn_7.tex",
            "common/font/font_krn_8.tex",
            "common/font/font_krn_9.tex"
        };

        private readonly BuildOptions _options;
        private readonly BuildReport _report;

        public FontPatchGenerator(BuildOptions options, BuildReport report)
        {
            _options = options;
            _report = report;
        }

        public void Build()
        {
            string globalGame = Path.GetFullPath(_options.GlobalGamePath);
            string koreaGame = Path.GetFullPath(_options.KoreaGamePath);
            string outputDir = Path.GetFullPath(_options.OutputPath);
            string globalSqpack = Path.Combine(globalGame, RepositoryDir);
            string koreaSqpack = Path.Combine(koreaGame, RepositoryDir);

            RequireFile(Path.Combine(globalSqpack, IndexFileName));
            RequireFile(Path.Combine(globalSqpack, Dat0FileName));
            RequireFile(Path.Combine(koreaSqpack, IndexFileName));
            RequireFile(Path.Combine(koreaSqpack, Dat0FileName));

            string currentGlobalIndex = Path.Combine(globalSqpack, IndexFileName);
            string originalGlobalIndex = Path.Combine(globalSqpack, OrigIndexFileName);
            string baseIndex = ResolveBaseIndex(currentGlobalIndex, originalGlobalIndex);

            string outputIndex = Path.Combine(outputDir, IndexFileName);
            string outputOrigIndex = Path.Combine(outputDir, OrigIndexFileName);
            string outputDat1 = Path.Combine(outputDir, Dat1FileName);

            File.Copy(baseIndex, outputOrigIndex, true);
            File.Copy(baseIndex, outputIndex, true);

            Console.WriteLine("Using base global font index: {0}", baseIndex);

            using (SqPackArchive koreaArchive = new SqPackArchive(Path.Combine(koreaSqpack, IndexFileName), koreaSqpack, "000000.win32"))
            using (SqPackIndexFile mutableIndex = new SqPackIndexFile(outputIndex))
            using (SqPackDatWriter datWriter = new SqPackDatWriter(outputDat1, Path.Combine(globalSqpack, Dat0FileName)))
            {
                mutableIndex.EnsureDataFileCount(2);

                for (int i = 0; i < FontPaths.Length; i++)
                {
                    string path = FontPaths[i];
                    ProgressReporter.Report(90 + i * 8 / FontPaths.Length, "폰트 처리 중: " + (i + 1).ToString() + "/" + FontPaths.Length.ToString());
                    if (!mutableIndex.ContainsPath(path))
                    {
                        AddLimitedWarning("Missing global font target: " + path);
                        continue;
                    }

                    byte[] packedFile;
                    if (!koreaArchive.TryReadPackedFile(path, out packedFile))
                    {
                        AddLimitedWarning("Missing Korean font source: " + path);
                        continue;
                    }

                    long datOffset = datWriter.WritePackedFile(packedFile);
                    mutableIndex.SetFileOffset(path, 1, datOffset);
                    _report.FontFilesPatched++;
                }

                mutableIndex.Save();
            }

            ProgressReporter.Report(98, "폰트 패치 저장 완료");
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required file is missing.", path);
            }
        }

        private string ResolveBaseIndex(string currentGlobalIndex, string originalGlobalIndex)
        {
            string baseIndex;
            bool explicitBaseIndex = !string.IsNullOrEmpty(_options.BaseFontIndexPath);
            bool foundOrigIndex = File.Exists(originalGlobalIndex);

            if (explicitBaseIndex)
            {
                baseIndex = Path.GetFullPath(_options.BaseFontIndexPath);
                RequireFile(baseIndex);
            }
            else if (foundOrigIndex)
            {
                baseIndex = originalGlobalIndex;
            }
            else
            {
                baseIndex = currentGlobalIndex;
            }

            if (!explicitBaseIndex && !foundOrigIndex)
            {
                using (SqPackIndexFile probe = new SqPackIndexFile(baseIndex))
                {
                    Dictionary<byte, int> counts = probe.CountEntriesByDataFile();
                    int dat1Count = counts.ContainsKey(1) ? counts[1] : 0;
                    if (dat1Count > 0 && !_options.AllowPatchedGlobal)
                    {
                        throw new InvalidOperationException(
                            "The installed global 000000.win32.index already contains " + dat1Count +
                            " dat1 entries, but orig.000000.win32.index was not found. Use a clean client, restore the original index, or pass --base-font-index <clean index>. " +
                            "Use --allow-patched-global only for experiments.");
                    }
                }
            }

            return baseIndex;
        }

        private void AddLimitedWarning(string message)
        {
            if (_report.Warnings.Count < 30)
            {
                _report.Warnings.Add(message);
            }
        }
    }
}
