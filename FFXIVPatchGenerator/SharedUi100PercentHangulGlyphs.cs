using System;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class SharedUi100PercentHangulGlyphs
    {
        public const string Jupiter16TargetFontPath = "common/font/Jupiter_16.fdt";
        public const string Jupiter20TargetFontPath = "common/font/Jupiter_20.fdt";
        public const string Jupiter16SourceFontPath = "common/font/AXIS_14.fdt";
        public const string Jupiter20SourceFontPath = "common/font/Jupiter_16.fdt";

        public static readonly string[] TargetFontPaths = new string[]
        {
            Jupiter16TargetFontPath,
            Jupiter20TargetFontPath
        };

        public static bool IsTargetFontPath(string path)
        {
            string ignored;
            return TryGetSourceFontPath(path, out ignored);
        }

        public static bool IsTargetCodepoint(uint codepoint)
        {
            // These two invisible composition fillers use font-specific cells.
            if (codepoint == 0x115Fu || codepoint == 0x1160u)
            {
                return false;
            }

            return (codepoint >= 0xAC00u && codepoint <= 0xD7A3u) ||
                   (codepoint >= 0x1100u && codepoint <= 0x11FFu) ||
                   (codepoint >= 0x3130u && codepoint <= 0x318Fu) ||
                   (codepoint >= 0xA960u && codepoint <= 0xA97Fu) ||
                   (codepoint >= 0xD7B0u && codepoint <= 0xD7FFu);
        }

        public static bool TryGetSourceFontPath(string path, out string sourceFontPath)
        {
            string normalized = Normalize(path);
            if (string.Equals(normalized, Jupiter16TargetFontPath, StringComparison.OrdinalIgnoreCase))
            {
                sourceFontPath = Jupiter16SourceFontPath;
                return true;
            }

            if (string.Equals(normalized, Jupiter20TargetFontPath, StringComparison.OrdinalIgnoreCase))
            {
                sourceFontPath = Jupiter20SourceFontPath;
                return true;
            }

            sourceFontPath = null;
            return false;
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
