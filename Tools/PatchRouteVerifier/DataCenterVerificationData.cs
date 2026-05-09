using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static readonly string[] DataCenterGlobalLanguages = new string[] { "ja", "en", "de", "fr" };
        private static readonly DataCenterRegionLabel[] DataCenterRegionLabels = new DataCenterRegionLabel[]
        {
            new DataCenterRegionLabel("Japan", "Japanese Data Center", "Japanese Data Centers"),
            new DataCenterRegionLabel("North America", "North American Data Center", "North American Data Centers"),
            new DataCenterRegionLabel("Europe", "European Data Center", "European Data Centers"),
            new DataCenterRegionLabel("Oceania", "Oceanian Data Center", "Oceanian Data Center")
        };
        private static readonly string[] DataCenterRegions = CreateDataCenterRegions();
        private static readonly string[] WorldRegionGroupLabels = new string[]
        {
            "Japan",
            "North America",
            "Europe",
            "Oceania",
            "China",
            "Korea",
            "NA Cloud",
            "Traditional Chinese regions"
        };
        private static readonly string[] WorldPhysicalDcLabels = new string[]
        {
            "Japan",
            "North America",
            "Europe",
            "Oceania",
            "NA Cloud Data Center (Beta)"
        };
        private static readonly string[] WorldDcGroupTypeLabels = new string[]
        {
            "Elemental",
            "Gaia",
            "Mana",
            "Aether",
            "Primal",
            "Chaos",
            "Light",
            "Crystal",
            "Materia",
            "Meteor",
            "Dynamis",
            "Shadow",
            "NA Cloud DC (Beta)"
        };
        private static readonly string[] DataCenterWorldLabels = new string[]
        {
            "Aegis",
            "Atomos",
            "Carbuncle",
            "Garuda",
            "Gungnir",
            "Kujata",
            "Tonberry",
            "Typhon",
            "Alexander",
            "Bahamut",
            "Durandal",
            "Fenrir",
            "Ifrit",
            "Ridill",
            "Tiamat",
            "Ultima",
            "Anima",
            "Asura",
            "Chocobo",
            "Hades",
            "Ixion",
            "Masamune",
            "Pandaemonium",
            "Titan",
            "Belias",
            "Mandragora",
            "Ramuh",
            "Shinryu",
            "Unicorn",
            "Valefor",
            "Yojimbo",
            "Zeromus"
        };
        private static readonly string[] DataCenterKoreanRoutePhrases = new string[]
        {
            "\uB370\uC774\uD130 \uC13C\uD130 Mana\uC5D0 \uC811\uC18D \uC911\uC785\uB2C8\uB2E4.",
            "\uD604\uC7AC \uC811\uC18D \uC911\uC778 \uB370\uC774\uD130 \uC13C\uD130",
            "\uB2E4\uB978 \uB370\uC774\uD130 \uC13C\uD130",
            "\uC811\uC18D\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?",
            "\uB098\uAC00\uAE30",
            "\uC885\uB8CC",
            "\uCDE8\uC18C",
            "\uD655\uC778"
        };
        private static readonly string[] DataCenterActionLabels = new string[]
        {
            "OK",
            "Proceed",
            "Cancel",
            "Exit",
            "Data Center"
        };
        private static readonly string[] DataCenterWorldmapLabels = CreateDataCenterGlyphLabels();

        private static string[] CreateDataCenterGlyphLabels()
        {
            List<string> labels = new List<string>();
            labels.Add("DATA CENTER SELECT");
            labels.Add("INFORMATION");
            labels.Add("Data Center Selection");
            labels.Add("Information");
            for (int i = 0; i < DataCenterRegions.Length; i++)
            {
                labels.Add(DataCenterRegions[i].ToUpperInvariant() + " DATA CENTER");
                labels.Add(DataCenterRegions[i] + " Data Center");
            }

            for (int i = 0; i < DataCenterRegionLabels.Length; i++)
            {
                labels.Add(DataCenterRegionLabels[i].Singular);
            }

            for (int i = 0; i < DataCenterRegionLabels.Length; i++)
            {
                if (!string.Equals(DataCenterRegionLabels[i].Plural, DataCenterRegionLabels[i].Singular, System.StringComparison.Ordinal))
                {
                    labels.Add(DataCenterRegionLabels[i].Plural);
                }
            }

            labels.Add("NA Cloud Data Center (Beta)");
            AddLabels(labels, DataCenterActionLabels);
            AddLabels(labels, WorldRegionGroupLabels);
            AddLabels(labels, WorldPhysicalDcLabels);
            AddLabels(labels, WorldDcGroupTypeLabels);
            AddLabels(labels, DataCenterWorldLabels);
            return labels.ToArray();
        }

        private static string[] CreateDataCenterRegions()
        {
            string[] regions = new string[DataCenterRegionLabels.Length];
            for (int i = 0; i < DataCenterRegionLabels.Length; i++)
            {
                regions[i] = DataCenterRegionLabels[i].Region;
            }

            return regions;
        }

        private static void AddLabels(List<string> target, string[] labels)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                target.Add(labels[i]);
            }
        }

        private sealed class DataCenterLabelExpectation
        {
            public readonly string Sheet;
            public readonly uint RowId;
            public readonly string Expected;
            public readonly bool AllowSubstring;

            public DataCenterLabelExpectation(string sheet, uint rowId, string expected, bool allowSubstring = false)
            {
                Sheet = sheet;
                RowId = rowId;
                Expected = expected;
                AllowSubstring = allowSubstring;
            }
        }

        private sealed class DataCenterRegionLabel
        {
            public readonly string Region;
            public readonly string Singular;
            public readonly string Plural;

            public DataCenterRegionLabel(string region, string singular, string plural)
            {
                Region = region;
                Singular = singular;
                Plural = plural;
            }
        }
    }
}
