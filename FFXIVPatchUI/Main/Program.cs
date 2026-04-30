// 이 프로젝트는 FFXIV 한글 패치 원작자 https://github.com/korean-patch 의 작업을 참고했습니다.
// 한글 패치의 기반과 구현 흐름을 만들어주신 원작자에게 감사드립니다.

using System;
using System.Windows.Forms;

namespace FFXIVKoreanPatch.Main
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FFXIVKoreanPatch());
        }
    }
}
