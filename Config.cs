using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameAudioSwitcher
{
    /// <summary>配置：设备名、进程名、轮询间隔。从 exe 同目录 config.ini 加载，UTF-8 编码。</summary>
    internal class Config
    {
        public string HeadphoneName = "耳机 (Realtek(R) Audio)";
        public string SpeakerName = "扬声器 (Realtek(R) Audio)";
        public int PollIntervalMs = 2000;
        public bool BalloonEnabled = true;
        public List<string> GameProcessNames = new List<string>();

        public static Config CreateDefault()
        {
            Config cfg = new Config();
            cfg.GameProcessNames.Add("VALORANT-Win64-Shipping.exe");
            cfg.GameProcessNames.Add("VALORANT.exe");
            cfg.GameProcessNames.Add("ABInfinite-Win64-Shipping.exe");
            cfg.GameProcessNames.Add("ABInfinite.exe");
            return cfg;
        }

        public static Config Load(string path)
        {
            Config cfg = CreateDefault();
            if (!File.Exists(path))
            {
                SaveTo(path, cfg);
                return cfg;
            }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            string section = "";
            bool gamesSectionSeen = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();

                if (section == "devices")
                {
                    if (key == "headphone" && value.Length > 0) cfg.HeadphoneName = value;
                    else if (key == "speaker" && value.Length > 0) cfg.SpeakerName = value;
                }
                else if (section == "settings")
                {
                    if (key == "poll_interval_ms")
                    {
                        int v;
                        if (int.TryParse(value, out v) && v >= 500) cfg.PollIntervalMs = v;
                    }
                    else if (key == "balloon")
                    {
                        cfg.BalloonEnabled = value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
                else if (section == "games")
                {
                    // 一旦出现 [games] 段，进程名完全由配置文件决定
                    if (!gamesSectionSeen)
                    {
                        cfg.GameProcessNames.Clear();
                        gamesSectionSeen = true;
                    }
                    string[] parts = value.Split(';');
                    foreach (string part in parts)
                    {
                        string p = part.Trim();
                        if (p.Length > 0) cfg.GameProcessNames.Add(p);
                    }
                }
            }
            return cfg;
        }

        public static void SaveTo(string path, Config cfg)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; 游戏音频自动切换 - 配置文件");
            sb.AppendLine("; 修改后需重新启动程序生效（UTF-8 编码）");
            sb.AppendLine("");
            sb.AppendLine("[devices]");
            sb.AppendLine("; 游戏运行时切换到的设备名称（需与 Windows 声音设置中的名称完全一致）");
            sb.AppendLine("headphone=" + cfg.HeadphoneName);
            sb.AppendLine("; 无游戏运行时使用的设备名称");
            sb.AppendLine("speaker=" + cfg.SpeakerName);
            sb.AppendLine("");
            sb.AppendLine("[settings]");
            sb.AppendLine("; 游戏进程检测间隔（毫秒，最小 500）");
            sb.AppendLine("poll_interval_ms=" + cfg.PollIntervalMs);
            sb.AppendLine("; 切换时是否显示托盘气泡通知（1=显示，0=关闭）");
            sb.AppendLine("balloon=1");
            sb.AppendLine("");
            sb.AppendLine("[games]");
            sb.AppendLine("; 游戏本体进程名列表，多个用英文分号 ; 分隔，不含 .exe 亦可");
            sb.AppendLine("; 任一进程存在即视为游戏运行中（只认本体，不含启动器/客户端）");
            sb.AppendLine("processes=" + string.Join(";", cfg.GameProcessNames.ToArray()));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
