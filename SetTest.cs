using System;

namespace GameAudioSwitcher
{
    /// <summary>诊断工具：验证 AudioCore.SetDefaultDevice 能否实际切换系统默认设备。</summary>
    internal static class SetTest
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string target = args.Length > 0 ? args[0] : "耳机 (Realtek(R) Audio)";
            Console.WriteLine("Target: " + target);
            Console.WriteLine("IsDeviceAvailable: " + AudioCore.IsDeviceAvailable(target));

            bool ok = AudioCore.SetDefaultDevice(target);
            Console.WriteLine("SetDefaultDevice => " + ok);

            System.Threading.Thread.Sleep(500);
            string current = AudioCore.GetCurrentDefaultDeviceName();
            Console.WriteLine("Current default: " + (current == null ? "(null)" : current));

            Console.WriteLine("SETTEST_DONE");
            return ok ? 0 : 1;
        }
    }
}
