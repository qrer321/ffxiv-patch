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
            public string TargetLanguage = "ja";
            public string GlyphDumpDir;
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
        }
    }
}
