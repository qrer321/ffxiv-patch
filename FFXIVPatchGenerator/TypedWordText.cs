using System.Text.RegularExpressions;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Typing-game word sheets: the player types the word shown on screen, so its Korean text has to be
    // typeable on a Korean keyboard. A Korean keyboard types the tilde as U+007E, the form the Korean
    // client's words use, so words keep it on every target.
    internal static class TypedWordText
    {
        private static readonly Regex[] Sheets = new Regex[]
        {
            new Regex("^KTGTypeWordTextData$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        // Symbols a Korean keyboard types directly besides Hangul syllables, ASCII letters and digits.
        private const string TypeableSymbols = " ,.!?-'&~";

        public static bool IsTypedWordSheet(string sheetName)
        {
            for (int i = 0; i < Sheets.Length; i++)
            {
                if (Sheets[i].IsMatch(sheetName))
                {
                    return true;
                }
            }

            return false;
        }

        // Supplement rule of typed-word sheets: the Korean word has to be typeable, and a tilde is kept
        // only where the Korean client's own wording has it. Returns a problem, or null.
        public static string CheckWord(SupplementEntry entry)
        {
            string word = entry.Korean;
            for (int i = 0; i < word.Length; i++)
            {
                char ch = word[i];
                bool typeable = (ch >= '\uAC00' && ch <= '\uD7A3') ||
                                (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') ||
                                TypeableSymbols.IndexOf(ch) >= 0;
                if (!typeable)
                {
                    return "character U+" + ((int)ch).ToString("X4") + " cannot be typed: " + word;
                }
            }

            if (word.IndexOf('~') >= 0 && !SupplementSources.IsKoreanClientWording(entry.Source))
            {
                return "'~' is only kept from the Korean client's wording: " + word;
            }

            return null;
        }
    }
}
