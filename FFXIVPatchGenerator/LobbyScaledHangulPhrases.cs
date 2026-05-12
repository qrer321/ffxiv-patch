using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class LobbyScaledHangulPhrases
    {
        public static readonly AddonRowRange[] StartScreenSystemSettingsAddonRowRanges = new AddonRowRange[]
        {
            // System Config header/options and the high-resolution UI option block.
            new AddonRowRange(4000, 4200),
            new AddonRowRange(8683, 8722)
        };

        public static readonly string[] Core = new string[]
        {
            "\uCE90\uB9AD\uD130 \uC815\uBCF4\uB97C \uBCC0\uACBD\uD558\uAE30 \uC704\uD574",
            "\uC2DC\uC2A4\uD15C \uC124\uC815",
            "\uC2DC\uC2A4\uD15C \uC124\uC815 150%",
            "\uC2DC\uC2A4\uD15C \uC124\uC815 200%",
            "\uC2DC\uC2A4\uD15C \uC124\uC815 300%",
            "\uAE00\uAF34 \uD06C\uAE30",
            "\uD30C\uD2F0 \uBAA9\uB85D",
            "\uB370\uC774\uD130 \uC13C\uD130",
            "\uB370\uC774\uD130 \uC13C\uD130 Mana\uC5D0 \uC811\uC18D \uC911\uC785\uB2C8\uB2E4.",
            "\uD604\uC7AC \uC811\uC18D \uC911\uC778 \uB370\uC774\uD130 \uC13C\uD130",
            "\uB2E4\uB978 \uB370\uC774\uD130 \uC13C\uD130",
            "\uC811\uC18D\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?",
            "\uC885\uB8CC",
            "\uB098\uAC00\uAE30",
            "\uB4A4\uB85C",
            "\uC774\uC804 \uB2E8\uACC4\uB85C \uB418\uB3CC\uC544\uAC00\uAE30",
            "\uB3CC\uC544\uAC00\uAE30",
            "\uCDE8\uC18C",
            "\uD655\uC778",
            "\uC989\uC2DC \uBC1C\uB3D9",
            "\uCD08\uC2B9\uB2EC \uB808\uBCA8",
            "\uD0D0\uC0AC\uB300 \uD638\uC704\uB300\uC6D0"
        };

        public static readonly string[] StartScreenSystemSettings = new string[]
        {
            "\uC2DC\uC2A4\uD15C \uC124\uC815",
            "\uC2DC\uC2A4\uD15C \uC124\uC815 150%",
            "\uC2DC\uC2A4\uD15C \uC124\uC815 200%",
            "\uC2DC\uC2A4\uD15C \uC124\uC815 300%",
            "\uADF8\uB798\uD53D",
            "\uADF8\uB798\uD53D \uC124\uC815",
            "\uADF8\uB798\uD53D \uAC04\uB2E8 \uC124\uC815",
            "3D \uADF8\uB798\uD53D \uD574\uC0C1\uB3C4 \uC2A4\uCF00\uC77C\uB9C1",
            "\uADF8\uB798\uD53D \uC5C5\uC2A4\uCF00\uC77C\uB9C1 \uC720\uD615",
            "\uD654\uBA74",
            "\uD654\uBA74 \uC124\uC815",
            "\uD654\uBA74 \uBAA8\uB4DC \uC124\uC815",
            "\uAC00\uC0C1 \uC804\uCCB4 \uD654\uBA74 \uBAA8\uB4DC",
            "\uC804\uCCB4 \uD654\uBA74 \uBAA8\uB4DC",
            "\uD574\uC0C1\uB3C4 \uC124\uC815",
            "\uD574\uC0C1\uB3C4 \uC120\uD0DD",
            "\uD574\uC0C1\uB3C4 \uC0AC\uC6A9\uC790 \uC815\uC758",
            "UI \uD574\uC0C1\uB3C4 \uC124\uC815",
            "UI \uD574\uC0C1\uB3C4",
            "\uACE0\uD574\uC0C1\uB3C4 UI \uD06C\uAE30 \uC124\uC815",
            "\uB514\uC2A4\uD50C\uB808\uC774",
            "\uC8FC \uB514\uC2A4\uD50C\uB808\uC774",
            "\uD504\uB808\uC784 \uC18D\uB3C4 \uC81C\uD55C",
            "\uD14D\uC2A4\uCC98 \uD574\uC0C1\uB3C4",
            "\uB3D9\uC801 \uD574\uC0C1\uB3C4 \uD65C\uC131\uD654",
            "\uB9C8\uC6B0\uC2A4",
            "\uD0A4\uBCF4\uB4DC",
            "\uC0AC\uC6B4\uB4DC",
            "\uCE74\uBA54\uB77C",
            "\uCEE8\uD2B8\uB864\uB7EC",
            "\uCE90\uB9AD\uD130 \uC124\uC815",
            "\uC870\uC791 \uC124\uC815"
        };

        public static readonly string[] HighResolutionUiScaleOptions = new string[]
        {
            "\uB192\uC740 \uD574\uC0C1\uB3C4\uB85C \uD50C\uB808\uC774\uD558\uB294 \uBD84\uC744 \uC704\uD55C \uC124\uC815\uC785\uB2C8\uB2E4.",
            "100%(\uD45C\uC900): \uAE30\uBCF8 UI \uD06C\uAE30",
            "150%(FHD): 1728x972 \uC774\uC0C1 \uAD8C\uC7A5",
            "200%(WQHD): 2304x1296 \uC774\uC0C1 \uAD8C\uC7A5",
            "300%(4K): 3456x1944 \uC774\uC0C1 \uAD8C\uC7A5"
        };

        public static readonly string[] StartScreenSystemSettingsResultMessages = new string[]
        {
            "\uC124\uC815\uC744 \uBCC0\uACBD\uD588\uC2B5\uB2C8\uB2E4.",
            "\uC77C\uBD80 \uC124\uC815\uC740 \uC801\uC6A9\uC744 \uB20C\uB7EC\uC57C \uBC18\uC601\uB429\uB2C8\uB2E4."
        };

        public static readonly string[] StartScreenMainMenu = new string[]
        {
            "\uAC8C\uC784 \uC2DC\uC791",
            "\uB370\uC774\uD130 \uC13C\uD130",
            "\uB3D9\uC601\uC0C1 \uBC0F \uD0C0\uC774\uD2C0",
            "\uC124\uC815",
            "\uB77C\uC774\uC120\uC2A4",
            "\uC885\uB8CC"
        };

        public static readonly string[] CharacterSelect = new string[]
        {
            "\uB85C\uC2A4\uAC00\uB974",
            "\uC885\uC871",
            "\uB2CC\uC790",
            "\uC9C0\uACE0\uCC9C \uAC70\uB9AC"
        };

        public static readonly string[] All = Combine(
            Core,
            StartScreenSystemSettings,
            HighResolutionUiScaleOptions,
            StartScreenSystemSettingsResultMessages,
            StartScreenMainMenu,
            CharacterSelect);

        private static string[] Combine(params string[][] groups)
        {
            List<string> values = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                string[] group = groups[groupIndex];
                for (int i = 0; i < group.Length; i++)
                {
                    if (seen.Add(group[i]))
                    {
                        values.Add(group[i]);
                    }
                }
            }

            return values.ToArray();
        }
    }

    internal struct AddonRowRange
    {
        public readonly uint StartId;
        public readonly uint EndId;

        public AddonRowRange(uint startId, uint endId)
        {
            StartId = startId;
            EndId = endId;
        }

        public bool Contains(uint rowId)
        {
            return rowId >= StartId && rowId <= EndId;
        }
    }
}
