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
            AddLabels(labels, WorldRegionGroupLabels);
            AddLabels(labels, WorldPhysicalDcLabels);
            AddLabels(labels, WorldDcGroupTypeLabels);
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
