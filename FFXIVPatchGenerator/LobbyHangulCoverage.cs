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
            "common/font/MiedingerMid_12_lobby.fdt",
            "common/font/MiedingerMid_14_lobby.fdt",
            "common/font/MiedingerMid_18_lobby.fdt"
        };

        public static readonly LobbyHangulCoverageRowSpec[] Rows = new LobbyHangulCoverageRowSpec[]
        {
            new LobbyHangulCoverageRowSpec("Lobby", 2003, 2009),
            new LobbyHangulCoverageRowSpec("Lobby", 2052, 2059),
            new LobbyHangulCoverageRowSpec("Addon", 2744, 2744),
            new LobbyHangulCoverageRowSpec("Addon", 4000, 4000),
            new LobbyHangulCoverageRowSpec("Addon", 4000, 4200),
            new LobbyHangulCoverageRowSpec("Addon", 8683, 8722),
            new LobbyHangulCoverageRowSpec("Lobby", 23, 24),
            new LobbyHangulCoverageRowSpec("Lobby", 41, 53),
            new LobbyHangulCoverageRowSpec("Lobby", 101, 101),
            new LobbyHangulCoverageRowSpec("Lobby", 507, 507),
            new LobbyHangulCoverageRowSpec("Lobby", 841, 842),
            new LobbyHangulCoverageRowSpec("Lobby", 849, 850),
            new LobbyHangulCoverageRowSpec("Lobby", 921, 921),
            new LobbyHangulCoverageRowSpec("Lobby", 975, 975),
            new LobbyHangulCoverageRowSpec("Lobby", 1100, 1233),
            new LobbyHangulCoverageRowSpec("Lobby", 2019, 2019),
            new LobbyHangulCoverageRowSpec("Lobby", 2066, 2066),
            new LobbyHangulCoverageRowSpec("Error", 13206, 13220),
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

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
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
