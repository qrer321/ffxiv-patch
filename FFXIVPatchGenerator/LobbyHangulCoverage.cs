using System;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class LobbyHangulCoverage
    {
        public static readonly string[] TargetFontPaths = new string[]
        {
            "common/font/AXIS_12_lobby.fdt",
            "common/font/AXIS_14_lobby.fdt",
            "common/font/AXIS_18_lobby.fdt",
            "common/font/AXIS_36_lobby.fdt",
            "common/font/Jupiter_46_lobby.fdt",
            "common/font/Jupiter_90_lobby.fdt",
            "common/font/Meidinger_40_lobby.fdt",
            "common/font/MiedingerMid_12_lobby.fdt",
            "common/font/MiedingerMid_14_lobby.fdt",
            "common/font/MiedingerMid_18_lobby.fdt",
            "common/font/MiedingerMid_36_lobby.fdt",
            "common/font/TrumpGothic_23_lobby.fdt",
            "common/font/TrumpGothic_34_lobby.fdt",
            "common/font/TrumpGothic_68_lobby.fdt"
        };

        public static readonly string[] HighScaleTargetFontPaths = new string[]
        {
            "common/font/AXIS_36_lobby.fdt",
            "common/font/Jupiter_46_lobby.fdt",
            "common/font/Jupiter_90_lobby.fdt",
            "common/font/Meidinger_40_lobby.fdt",
            "common/font/MiedingerMid_36_lobby.fdt",
            "common/font/TrumpGothic_68_lobby.fdt"
        };

        public static readonly LobbyHangulCoverageRowSpec[] Rows = CreateFullSheetRows(
            "Lobby",
            "Error",
            "Addon",
            "ClassJob",
            "Race",
            "Tribe",
            "GuardianDeity");

        public static readonly LobbyHangulCoverageRowSpec[] StartMainMenuRows = CreateFullSheetRows(
            "Lobby",
            "Error",
            "Addon",
            "ClassJob",
            "Race",
            "Tribe",
            "GuardianDeity");

        public static readonly LobbyHangulCoverageRowSpec[] SystemSettingsRows = CreateFullSheetRows(
            "Lobby",
            "Error",
            "Addon",
            "ClassJob",
            "Race",
            "Tribe",
            "GuardianDeity");

        public static readonly LobbyHangulCoverageRowSpec[] CharacterSelectRows = CreateFullSheetRows(
            "Lobby",
            "Error",
            "Addon",
            "ClassJob",
            "Race",
            "Tribe",
            "GuardianDeity");

        public static readonly LobbyHangulCoverageRowSpec[] LargeLabelRows = new LobbyHangulCoverageRowSpec[]
        {
            new LobbyHangulCoverageRowSpec("ClassJob", 0, 43),
            new LobbyHangulCoverageRowSpec("Race", 1, 8),
            new LobbyHangulCoverageRowSpec("Tribe", 1, 16),
            new LobbyHangulCoverageRowSpec("GuardianDeity", 1, 12, 0)
        };

        public static bool IsTargetFontPath(string path)
        {
            string normalized = Normalize(path);
            for (int i = 0; i < TargetFontPaths.Length; i++)
            {
                if (string.Equals(normalized, TargetFontPaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsMainMenuOnlyTargetFontPath(string path)
        {
            return string.Equals(
                Normalize(path),
                "common/font/MiedingerMid_18_lobby.fdt",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHighScaleTargetFontPath(string path)
        {
            string normalized = Normalize(path);
            for (int i = 0; i < HighScaleTargetFontPaths.Length; i++)
            {
                if (string.Equals(normalized, HighScaleTargetFontPaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static LobbyHangulCoverageRowSpec[] CreateFullSheetRows(params string[] sheets)
        {
            LobbyHangulCoverageRowSpec[] rows = new LobbyHangulCoverageRowSpec[sheets.Length];
            for (int i = 0; i < sheets.Length; i++)
            {
                rows[i] = new LobbyHangulCoverageRowSpec(sheets[i], 0, uint.MaxValue);
            }

            return rows;
        }
    }

    internal struct LobbyHangulCoverageRowSpec
    {
        public readonly string Sheet;
        public readonly uint StartId;
        public readonly uint EndId;
        public readonly ushort? ColumnOffset;

        public LobbyHangulCoverageRowSpec(string sheet, uint startId, uint endId)
            : this(sheet, startId, endId, null)
        {
        }

        public LobbyHangulCoverageRowSpec(string sheet, uint startId, uint endId, ushort? columnOffset)
        {
            Sheet = sheet;
            StartId = startId;
            EndId = endId;
            ColumnOffset = columnOffset;
        }

        public bool Contains(uint rowId)
        {
            return rowId >= StartId && rowId <= EndId;
        }
    }
}
