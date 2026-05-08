using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static string FormatBounds(GlyphStats stats)
            {
                if (stats.ComponentCount == 0)
                {
                    return "-";
                }

                return stats.MinX.ToString() + "," +
                       stats.MinY.ToString() + "-" +
                       stats.MaxX.ToString() + "," +
                       stats.MaxY.ToString();
            }

            private static string SanitizeFileName(string value)
            {
                StringBuilder builder = new StringBuilder(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char ch = value[i];
                    if ((ch >= 'a' && ch <= 'z') ||
                        (ch >= 'A' && ch <= 'Z') ||
                        (ch >= '0' && ch <= '9'))
                    {
                        builder.Append(ch);
                    }
                    else
                    {
                        builder.Append('_');
                    }
                }

                return builder.ToString().Trim('_');
            }

            private static string EscapeTsv(string value)
            {
                return (value ?? string.Empty).Replace("\t", " ").Replace("\r", "\\r").Replace("\n", "\\n");
            }
        }
    }
}
