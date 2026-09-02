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
        private static string _configPath;
        private static string _logPath;

        private static ToolStripMenuItem _statusItem;
        private static ToolStripMenuItem _autoStartItem;
        private static ToolStripMenuItem _autoSwitchItem;
        private static ToolStripMenuItem _hotkeyMenuItem;
        private static bool _autoSwitchOn = true;

        private static HotkeyWindow _hotkeyWindow;
        private static bool _hotkeyActive;   // 当前热键已成功注册
        private static bool _hotkeyDisabled; // 配置为空，主动未启用

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
            _configPath = Path.Combine(baseDir, "config.ini");

            try
            {
                _config = Config.Load(_configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("配置文件加载失败：" + ex.Message + "\n将使用默认配置运行。", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _config = Config.CreateDefault();
            }

            string hotkeyDesc = (_config.Hotkey == null || _config.Hotkey.Trim().Length == 0)
                ? "(未配置)"
                : _config.Hotkey.Trim();
            Log("程序启动。耳机=" + _config.HeadphoneName +
                "，扬声器=" + _config.SpeakerName +
                "，轮询间隔=" + _config.PollIntervalMs + "ms，进程=" +
                string.Join(";", _config.GameProcessNames.ToArray()) +
                "，快捷键=" + hotkeyDesc);

            // 热键消息窗口须在注册前建好句柄
            _hotkeyWindow = new HotkeyWindow();
            _hotkeyWindow.EnsureHandle();
            _hotkeyWindow.HotkeyPressed += delegate { ManualSwitchDevice(true); };

            InitTray();
            ApplyHotkeyRegistration();

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

            _statusItem = new ToolStripMenuItem("状态：正在检测游戏…");
            _statusItem.Enabled = false;

            ToolStripMenuItem manualItem = new ToolStripMenuItem("立即切换输出设备");
            manualItem.Click += delegate { ManualSwitchDevice(false); };

            _hotkeyMenuItem = new ToolStripMenuItem("设置切换快捷键…");
            _hotkeyMenuItem.Click += delegate { ShowHotkeyDialog(); };

            _autoStartItem = new ToolStripMenuItem("开机自启");
            _autoStartItem.Checked = IsAutoStartEnabled();
            _autoStartItem.Click += delegate { ToggleAutoStart(); };

            _autoSwitchItem = new ToolStripMenuItem("自动切换：已开启");
            _autoSwitchItem.Checked = true;
            _autoSwitchItem.Click += delegate { ToggleAutoSwitch(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };

            menu.Items.Add(_statusItem);
            menu.Items.Add(manualItem);
            menu.Items.Add(_hotkeyMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(_autoSwitchItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += delegate
            {
                string current = AudioCore.GetCurrentDefaultDeviceName();
                if (current == null) current = "未知";
                ShowBalloon("当前默认输出设备：" + current, 1500);
            };
        }

        // ================= 状态 / 日志 / 气泡 =================

        private static void OnMonitorStateChanged(object sender, GameStateEventArgs e)
        {
            SetStatus(e.Message);
            Log(e.Message);
            if (e.ShowBalloon && _config.BalloonEnabled && _trayIcon != null)
            {
                _trayIcon.ShowBalloonTip(2000, AppName, e.Message, ToolTipIcon.Info);
            }
        }

        private static void SetStatus(string text)
        {
            if (_statusItem != null) _statusItem.Text = text;
            if (_trayIcon != null)
            {
                string tip = text;
                if (tip.Length > 40) tip = tip.Substring(0, 40) + "…";
                _trayIcon.Text = AppName + "｜" + tip;
            }
        }

        private static void ShowBalloon(string message, int timeoutMs)
        {
            if (_trayIcon != null && _config != null && _config.BalloonEnabled)
                _trayIcon.ShowBalloonTip(timeoutMs, AppName, message, ToolTipIcon.Info);
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

        // ================= 开机自启 =================

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

        // ================= 自动切换总开关（打开/关闭） =================

        private static void ToggleAutoSwitch()
        {
            bool enable = !_autoSwitchOn;
            _autoSwitchOn = enable;

            try
            {
                if (enable)
                {
                    // 先重置边界并校正一次（暂停期间可能已错过游戏启停），再恢复轮询
                    _monitor.Pause();
                    _monitor.Reconcile();
                    _monitor.Resume();
                    SetStatus("自动切换：已开启");
                    ShowBalloon("已开启自动切换：检测到游戏将自动切到耳机，游戏关闭后恢复扬声器", 2000);
                    Log("自动切换已开启（用户操作）。");
                }
                else
                {
                    _monitor.Pause();
                    SetStatus("自动切换：已暂停");
                    ShowBalloon("已暂停自动切换：游戏启停将不再自动切换，手动切换与快捷键仍可用", 2000);
                    Log("自动切换已暂停（用户操作）。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("切换自动切换状态失败：" + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _autoSwitchItem.Checked = enable;
            _autoSwitchItem.Text = enable ? "自动切换：已开启" : "自动切换：已暂停";
        }

        // ================= 手动切换输出设备（菜单 / 快捷键共用） =================

        private static void ManualSwitchDevice(bool fromHotkey)
        {
            if (_config == null) return;

            string current = AudioCore.GetCurrentDefaultDeviceName();
            string target;
            if (current != null && current.Equals(_config.HeadphoneName, StringComparison.OrdinalIgnoreCase))
                target = _config.SpeakerName;
            else
                target = _config.HeadphoneName;

            bool ok = AudioCore.SetDefaultDevice(target);
            string msg = ok
                ? "已切换输出设备到「" + target + "」"
                : "切换失败：目标设备「" + target + "」不可用或未找到";
            Log((fromHotkey ? "快捷键" : "手动") + "切换输出设备：" + msg);
            SetStatus(msg);
            ShowBalloon(msg, 1800);
        }

        // ================= 全局快捷键 =================

        private static void ApplyHotkeyRegistration()
        {
            if (_hotkeyWindow == null) return;
            string spec = (_config == null || _config.Hotkey == null) ? "" : _config.Hotkey.Trim();

            GlobalHotkey.Unregister(_hotkeyWindow.Handle);
            _hotkeyActive = false;
            _hotkeyDisabled = (spec.Length == 0);

            if (!_hotkeyDisabled)
            {
                string error;
                if (GlobalHotkey.Register(_hotkeyWindow.Handle, spec, out error))
                {
                    _hotkeyActive = true;
                    Log("快捷键已注册：" + spec);
                }
                else
                {
                    Log("快捷键注册失败（" + spec + "）：" + error);
                    ShowBalloon("快捷键注册失败：" + error, 3000);
                }
            }
            RefreshHotkeyMenuText();
        }

        private static void RefreshHotkeyMenuText()
        {
            if (_hotkeyMenuItem == null) return;
            string spec = (_config == null || _config.Hotkey == null) ? "" : _config.Hotkey.Trim();
            if (_hotkeyDisabled)
                _hotkeyMenuItem.Text = "设置切换快捷键…（未启用）";
            else if (_hotkeyActive)
                _hotkeyMenuItem.Text = "设置切换快捷键…（" + spec + "）";
            else
                _hotkeyMenuItem.Text = "设置切换快捷键…（注册失败）";
        }

        private static void ShowHotkeyDialog()
        {
            if (_config == null) return;

            using (HotkeyCaptureForm form = new HotkeyCaptureForm())
            {
                string spec = _config.Hotkey == null ? "" : _config.Hotkey.Trim();
                HotkeyModifiers mods;
                Keys key;
                string parseError;
                if (spec.Length > 0 && GlobalHotkey.Parse(spec, out mods, out key, out parseError))
                    form.SetCurrent(mods, key);

                if (form.ShowDialog() == DialogResult.OK)
                {
                    string newSpec = GlobalHotkey.Format(form.Modifiers, form.Key);
                    _config.Hotkey = newSpec;
                    try
                    {
                        Config.SaveTo(_configPath, _config);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("保存 config.ini 失败：" + ex.Message + "\n新快捷键本机仍生效，重启后将丢失。", AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    ApplyHotkeyRegistration();
                    Log("用户修改快捷键为：" + newSpec);
                    ShowBalloon("切换快捷键已设置为 " + newSpec + "，已同步写入 config.ini", 2500);
                }
            }
        }

        // ================= 退出 =================

        private static void ExitApplication()
        {
            if (_monitor != null) _monitor.Stop();
            if (_hotkeyWindow != null) GlobalHotkey.Unregister(_hotkeyWindow.Handle);
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
            if (_hotkeyWindow != null)
            {
                GlobalHotkey.Unregister(_hotkeyWindow.Handle);
                try { _hotkeyWindow.DestroyHandle(); }
                catch { }
                _hotkeyWindow = null;
            }
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); }
                catch { }
                _mutex = null;
            }
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
