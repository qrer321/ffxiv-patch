using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FFXIVKoreanPatch.Updater
{
    // Worker process for FFXIVKoreanPatch that updates the main executable.
    internal class Program
    {
        // args[0] -> path to the main executable that need to be updated.
        // args[1] -> path to the newly downloaded main executable.
        static void Main(string[] args)
        {
            // Check arguments.
            if (args.Length != 2) return;
            if (!File.Exists(args[0])) return;
            if (!File.Exists(args[1])) return;

            // See if main process is still running, and wait for it to close.
            Console.WriteLine("FFXIVKoreanPatch 가 종료되기를 기다리는 중...");
            Process[] processes = Process.GetProcessesByName("FFXIVKoreanPatch");

            while (processes.Length > 0)
            {
                // Wait a bit, then refresh process list.
                Thread.Sleep(1000);
                processes = Process.GetProcessesByName("FFXIVKoreanPatch");
            }

            Console.WriteLine();
            Console.WriteLine("FFXIVKoreanPatch 업데이트 중...");

            // Copy over the downloaded file.
            File.Copy(args[1], args[0], true);

            // Remove the downloaded file from the cache.
            File.Delete(args[1]);

            // Start the main executable after update is done.
            Process.Start(args[0]);
        }
    }
}
