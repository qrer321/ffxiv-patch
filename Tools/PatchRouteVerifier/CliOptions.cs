using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed class Options
        {
            public string OutputPath;
            public string AppliedGamePath;
            public string GlobalGamePath;
            public string GlobalTextGamePath;
            public string GlobalFontGamePath;
            public string GlobalUiGamePath;
            public string CleanFontIndexPath;
            public string CleanUiIndexPath;
            public string KoreaGamePath;
            public string FontPackDir;
            public string TargetLanguage = "ja";
            public string GlyphDumpDir;
            public string[] Checks;
            public bool NoGlyphDump;
            public bool ShowHelp;

            public static Options Parse(string[] args)
            {
                Options options = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ShowHelp = true;
                    }
                    else if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
                    {
                        options.OutputPath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--global", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlobalGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--global-text", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlobalTextGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--global-font", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlobalFontGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--global-ui", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlobalUiGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--clean-font-index", StringComparison.OrdinalIgnoreCase))
                    {
                        options.CleanFontIndexPath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--clean-ui-index", StringComparison.OrdinalIgnoreCase))
                    {
                        options.CleanUiIndexPath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--korea", StringComparison.OrdinalIgnoreCase))
                    {
                        options.KoreaGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--font-pack-dir", StringComparison.OrdinalIgnoreCase))
                    {
                        options.FontPackDir = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--applied-game", StringComparison.OrdinalIgnoreCase))
                    {
                        options.AppliedGamePath = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--target-language", StringComparison.OrdinalIgnoreCase))
                    {
                        options.TargetLanguage = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--glyph-dump-dir", StringComparison.OrdinalIgnoreCase))
                    {
                        options.GlyphDumpDir = RequireValue(args, ref i, arg);
                    }
                    else if (string.Equals(arg, "--checks", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Checks = ParseChecks(RequireValue(args, ref i, arg));
                    }
                    else if (string.Equals(arg, "--no-glyph-dump", StringComparison.OrdinalIgnoreCase))
                    {
                        options.NoGlyphDump = true;
                    }
                    else
                    {
                        throw new ArgumentException("unknown argument: " + arg);
                    }
                }

                return options;
            }

            private static string RequireValue(string[] args, ref int index, string name)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException(name + " requires a value");
                }

                index++;
                return args[index];
            }

            private static string[] ParseChecks(string value)
            {
                string[] parts = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = parts[i].Trim();
                }

                return parts;
            }
        }
    }
}
