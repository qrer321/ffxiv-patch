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
            private void VerifyCompactTimeRows()
            {
                Console.WriteLine("[EXD] Compact time labels");
                ExpectTextContains("Addon", 44, "m");
                ExpectTextContains("Addon", 45, "h");
                ExpectTextContains("Addon", 49, "s");
                ExpectTextContains("Addon", 2338, "h");
                ExpectTextContains("Addon", 2338, "m");
                ExpectTextContains("Addon", 6166, "h");
                ExpectTextContains("Addon", 6166, "m");
                ExpectTextContains("Addon", 876, "\uE028");
                ExpectTextNotContains("Addon", 876, "분");
                ExpectText("Addon", 8291, "5m");
                ExpectText("Addon", 8292, "10m");
                ExpectText("Addon", 8293, "30m");
                ExpectText("Addon", 8294, "60m");
                ExpectTextNotContains("Addon", 8291, "분");
                ExpectTextNotContains("Addon", 8292, "분");
                ExpectTextNotContains("Addon", 8293, "분");
                ExpectTextNotContains("Addon", 8294, "분");
            }

            private void VerifyWorldVisitRows()
            {
                Console.WriteLine("[EXD] World visit labels");
                ExpectText("Addon", 12510, "서버 텔레포");
                ExpectText("Addon", 12511, "서버 텔레포");
                ExpectText("Addon", 12520, "서버 텔레포 예약 신청 중");
                ExpectText("Addon", 12524, "서버 텔레포");
                ExpectText("Addon", 12537, "서버 텔레포");
            }

            private void VerifyConfigurationSharingRows()
            {
                Console.WriteLine("[EXD] Configuration Sharing labels");
                ExpectText("Addon", 17300, "\uC124\uC815 \uACF5\uC720");
                ExpectText("Addon", 17301, "\uC124\uC815 \uACF5\uC720");
                ExpectAnyTextColumnNotContains("Addon", 17300, "\u30B3\u30F3\u30D5\u30A3\u30B0\u30B7\u30A7\u30A2");
                ExpectAnyTextColumnNotContains("Addon", 17300, "\u30B3\u30F3\u30C6\u30F3\u30C4\u30B7\u30A7\u30A2");
                ExpectAnyTextColumnNotContains("Addon", 17300, "Configuration Sharing");
                ExpectTextNotContains("Addon", 17301, "\u30B3\u30F3\u30D5\u30A3\u30B0\u30B7\u30A7\u30A2");
                ExpectTextNotContains("Addon", 17301, "\u30B3\u30F3\u30C6\u30F3\u30C4\u30B7\u30A7\u30A2");
                ExpectTextNotContains("Addon", 17301, "Configuration Sharing");
            }

            private void VerifyBozjaEntranceRows()
            {
                Console.WriteLine("[EXD] Bozja entrance custom talk");

                ExpectAnyTextColumnContains("custom/006/ctsmycentrance_00673", 1, "입장하기");
                ExpectAnyTextColumnContains("custom/006/ctsmycentrance_00673", 2, "남부 보즈야 전선");
                ExpectAnyTextColumnContains("custom/006/ctsmycentrance_00673", 3, "취소");
                ExpectAnyTextColumnNotContains("custom/006/ctsmycentrance_00673", 1, "突入する");
                ExpectAnyTextColumnNotContains("custom/006/ctsmycentrance_00673", 2, "南方ボズヤ戦線");

                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 3, "레지스탕스 랭크");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 14, "현재의");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 23, "미시야");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 25, "모험가님");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancenormal_00705", 3, "レジスタンスランク");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancenormal_00705", 23, "ミーシィヤ");

                ExpectAnyTextColumnContains("custom/007/ctsmycentrancehard_00706", 1, "입장하기");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancehard_00706", 3, "이야기 듣기");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancehard_00706", 5, "초고를 읽어");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancehard_00706", 1, "突入する");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancehard_00706", 3, "話を聞く");
            }

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
