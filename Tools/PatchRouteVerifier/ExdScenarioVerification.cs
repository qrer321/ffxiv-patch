using System;
using System.Collections.Generic;
using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
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

            private void VerifyRsvAutoTranslateDelimiters()
            {
                Console.WriteLine("[EXD] RSV auto-translate colored brackets");

                byte[] open = new byte[]
                {
                    0x02, 0x13, 0x06, 0xFE, 0xFF, 0x7F, 0xBF, 0x5F, 0x03,
                    0xEE, 0x81, 0x80,
                    0x02, 0x13, 0x02, 0xEC, 0x03
                };
                byte[] close = new byte[]
                {
                    0x02, 0x13, 0x06, 0xFE, 0xFF, 0xC1, 0x58, 0x4F, 0x03,
                    0xEE, 0x81, 0x81,
                    0x02, 0x13, 0x02, 0xEC, 0x03
                };
                byte[] newLine = new byte[] { 0x02, 0x10, 0x01, 0x03 };

                List<byte> expected = new List<byte>(128);
                expected.AddRange(open);
                expected.AddRange(Encoding.UTF8.GetBytes("여기는 처음 옵니다."));
                expected.AddRange(close);
                expected.AddRange(newLine);
                expected.AddRange(open);
                expected.AddRange(Encoding.UTF8.GetBytes("잘 부탁합니다!"));
                expected.AddRange(close);

                ExpectBytes(
                    "InstanceContentTextData#45500/" + _language,
                    GetFirstStringBytes(_patchedText, "InstanceContentTextData", 45500, _language),
                    expected.ToArray());
            }


        }
    }
}
