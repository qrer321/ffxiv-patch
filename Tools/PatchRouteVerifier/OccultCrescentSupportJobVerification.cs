using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const uint MkdSupportJobFirstRow = 0;
        private const uint MkdSupportJobLastPlayableRow = 15;
        private const ushort MkdSupportJobFullNameColumnOffset = 0;
        private const ushort MkdSupportJobShortNameColumnOffset = 4;
        private const ushort MkdSupportJobSupportTextColumnOffset = 12;
        private const ushort MkdSupportJobEnglishFullNameColumnOffset = 16;

        private sealed partial class Verifier
        {
            private void VerifyOccultCrescentSupportJobRows()
            {
                Console.WriteLine("[EXD] Occult Crescent phantom/support job labels");
                for (uint rowId = MkdSupportJobFirstRow; rowId <= MkdSupportJobLastPlayableRow; rowId++)
                {
                    string fullName = GetStringColumnByOffset(_patchedText, "MkdSupportJob", rowId, _language, MkdSupportJobFullNameColumnOffset);
                    string shortName = GetStringColumnByOffset(_patchedText, "MkdSupportJob", rowId, _language, MkdSupportJobShortNameColumnOffset);
                    string supportText = GetStringColumnByOffset(_patchedText, "MkdSupportJob", rowId, _language, MkdSupportJobSupportTextColumnOffset);
                    string englishFullName = GetStringColumnByOffset(_patchedText, "MkdSupportJob", rowId, _language, MkdSupportJobEnglishFullNameColumnOffset);

                    ExpectEqual("MkdSupportJob#" + rowId.ToString() + "@0 mirrors English full name column", fullName, englishFullName);
                    ExpectStartsWith("MkdSupportJob#" + rowId.ToString() + "@0", fullName, "Phantom ");
                    ExpectStartsWith("MkdSupportJob#" + rowId.ToString() + "@4", shortName, "Ph.");
                    ExpectNotContains("MkdSupportJob#" + rowId.ToString() + "@0", fullName, "\uC11C\uD3EC\uD2B8");
                    ExpectNotContains("MkdSupportJob#" + rowId.ToString() + "@4", shortName, "\uC11C\uD3EC\uD2B8");
                    ExpectContains("MkdSupportJob#" + rowId.ToString() + "@12", supportText, "\uC11C\uD3EC\uD2B8");
                }
            }
        }
    }
}
