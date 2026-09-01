using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameAudioSwitcher
{
    internal static class Program
    {
        private const string AppName = "游戏音频自动切换";
        private const string MutexName = "GameAudioSwitcher_SingleInstance_6F2A";
        private const string RunKeyValue = "GameAudioSwitcher";

        private static Mutex _mutex;
        private static NotifyIcon _trayIcon;
        private static GameMonitor _monitor;
        private static Config _config;
        private static ToolStripMenuItem _statusItem;
        private static ToolStripMenuItem _autoStartItem;
        private static string _logPath;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(AppName + " 已在运行，请查看系统托盘图标。", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _logPath = Path.Combine(baseDir, "GameAudioSwitcher.log");
            string configPath = Path.Combine(baseDir, "config.ini");

            try
            {
                _config = Config.Load(configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("配置文件加载失败：" + ex.Message + "\n将使用默认配置运行。", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _config = Config.CreateDefault();
            }

            Log("程序启动。耳机=" + _config.HeadphoneName +
                "，扬声器=" + _config.SpeakerName +
                "，轮询间隔=" + _config.PollIntervalMs + "ms，进程=" +
                string.Join(";", _config.GameProcessNames.ToArray()));

            InitTray();

            _monitor = new GameMonitor(_config);
            _monitor.StateChanged += OnMonitorStateChanged;
            _monitor.Start();

            Application.ApplicationExit += delegate { OnExit(); };
            Application.Run();
        }

        private static void InitTray()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = AppName;
            _trayIcon.Icon = CreateTrayIcon();
            _trayIcon.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem("状态：正在检测…");
            _statusItem.Enabled = false;

            _autoStartItem = new ToolStripMenuItem("开机自启");
            _autoStartItem.Checked = IsAutoStartEnabled();
            _autoStartItem.Click += delegate { ToggleAutoStart(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };

            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += delegate
            {
                string current = AudioCore.GetCurrentDefaultDeviceName();
                if (current == null) current = "未知";
                ShowBalloon("当前默认输出设备：" + current, 1500);
            };
        }

        private static void OnMonitorStateChanged(object sender, GameStateEventArgs e)
        {
            if (_statusItem != null) _statusItem.Text = "状态：" + e.Message;
            if (_trayIcon != null) _trayIcon.Text = AppName + "｜" + e.Message;
            Log(e.Message);
            if (e.ShowBalloon && _config.BalloonEnabled && _trayIcon != null)
            {
                _trayIcon.ShowBalloonTip(2000, AppName, e.Message, ToolTipIcon.Info);
            }
        }

        private static void ShowBalloon(string message, int timeoutMs)
        {
            if (_trayIcon != null && _config != null && _config.BalloonEnabled)
                _trayIcon.ShowBalloonTip(timeoutMs, AppName, message, ToolTipIcon.Info);
        }

        private static void ToggleAutoStart()
        {
            bool enable = !_autoStartItem.Checked;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (enable)
                        key.SetValue(RunKeyValue, "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue(RunKeyValue, false);
                }
                _autoStartItem.Checked = enable;
                ShowBalloon(enable ? "已开启开机自启" : "已关闭开机自启", 1500);
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机自启失败：" + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue(RunKeyValue) != null;
                }
            }
            catch { return false; }
        }

        private static void ExitApplication()
        {
            if (_monitor != null) _monitor.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            Log("程序退出。");
            Application.Exit();
        }

        private static void OnExit()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex = null;
            }
        }

        private static void Log(string message)
        {
            try
            {
                if (_logPath == null) return;
                File.AppendAllText(_logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n");
            }
            catch { }
        }

        private static Icon CreateTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                    {
                        // 左右耳罩
                        g.FillEllipse(brush, 3, 13, 10, 14);
                        g.FillEllipse(brush, 19, 13, 10, 14);
                        // 中间连接
                        g.FillRectangle(brush, 8, 15, 16, 6);
                    }
                    using (Pen pen = new Pen(Color.FromArgb(41, 128, 185), 4))
                    {
                        // 头梁弧线
                        g.DrawArc(pen, 3, 1, 26, 26, 200, 140);
                    }
                }
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    return Icon.FromHandle(hIcon);
                }
                catch
                {
                    return SystemIcons.Application;
                }
            }
        }
    }
}
