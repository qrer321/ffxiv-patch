using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class HangulText
    {
        // True when UTF-8 text has a Hangul syllable, Jamo or compatibility Jamo.
        public static bool Contains(byte[] bytes)
        {
            return Contains(bytes, 0, bytes.Length);
        }

        public static bool Contains(byte[] bytes, int offset, int length)
        {
            string text = new UTF8Encoding(false, false).GetString(bytes, offset, length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if ((ch >= '\uac00' && ch <= '\ud7a3') ||
                    (ch >= '\u1100' && ch <= '\u11ff') ||
                    (ch >= '\u3130' && ch <= '\u318f'))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
