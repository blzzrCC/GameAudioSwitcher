using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GameAudioSwitcher
{
    /// <summary>命令行验证工具：输出所有渲染端点（名称+状态）以及当前默认输出设备名称。</summary>
    internal static class AuditAudio
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== 渲染端点列表（ALL） ===");
                List<KeyValuePair<string, int>> devices = AudioCore.ListRenderDevices();
                foreach (KeyValuePair<string, int> kv in devices)
                {
                    string stateText;
                    switch (kv.Value)
                    {
                        case (int)DevState.ACTIVE: stateText = "ACTIVE(可用)"; break;
                        case (int)DevState.DISABLED: stateText = "DISABLED(已禁用)"; break;
                        case (int)DevState.NOTPRESENT: stateText = "NOTPRESENT(不存在)"; break;
                        case (int)DevState.UNPLUGGED: stateText = "UNPLUGGED(未插入)"; break;
                        default: stateText = "STATE=" + kv.Value; break;
                    }
                    Console.WriteLine("  [" + stateText + "] " + kv.Key);
                }

                Console.WriteLine("=== 当前默认输出设备 ===");
                string def = AudioCore.GetCurrentDefaultDeviceName();
                Console.WriteLine("  " + (def == null ? "(获取失败)" : def));

                Console.WriteLine("=== 耳机可用性 ===");
                string headphone = "耳机 (Realtek(R) Audio)";
                Console.WriteLine("  " + headphone + " => " + (AudioCore.IsDeviceAvailable(headphone) ? "可用" : "不可用"));
                Console.WriteLine("AUDIT_DONE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }
    }
}
