using System.Collections.Generic;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private static readonly string[] DataCenterGlobalLanguages = new string[] { "ja", "en", "de", "fr" };
        private static readonly string[] DataCenterRegions = new string[] { "Japan", "North America", "Europe", "Oceania" };
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

            labels.Add("Japanese Data Center");
            labels.Add("North American Data Center");
            labels.Add("European Data Center");
            labels.Add("Oceanian Data Center");
            labels.Add("Japanese Data Centers");
            labels.Add("North American Data Centers");
            labels.Add("European Data Centers");
            labels.Add("NA Cloud Data Center (Beta)");
            AddLabels(labels, WorldRegionGroupLabels);
            AddLabels(labels, WorldPhysicalDcLabels);
            AddLabels(labels, WorldDcGroupTypeLabels);
            return labels.ToArray();
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
    }
}
