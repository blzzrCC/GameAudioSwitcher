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
        private static ToolStripMenuItem _startupMenu;
        private static ToolStripMenuItem _startupHeadphoneItem;
        private static ToolStripMenuItem _startupSpeakerItem;
        private static ToolStripMenuItem _startupNoneItem;
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
                "，快捷键=" + hotkeyDesc +
                "，开机切换=" + Config.NormalizeStartupSwitch(_config.StartupSwitch));

            // 热键消息窗口须在注册前建好句柄
            _hotkeyWindow = new HotkeyWindow();
            _hotkeyWindow.EnsureHandle();
            _hotkeyWindow.HotkeyPressed += delegate { ManualSwitchDevice(true); };

            InitTray();
            ApplyHotkeyRegistration();

            _monitor = new GameMonitor(_config);
            _monitor.StateChanged += OnMonitorStateChanged;
            _monitor.Start();

            // 新用户引导：等消息循环跑起来再做设备配置自检，
            // 首次运行 / 换机导致配置对不上时自动弹出「扫描音频输出设备」向导。
            System.Windows.Forms.Timer startupCheck = new System.Windows.Forms.Timer();
            startupCheck.Interval = 1200;
            startupCheck.Tick += delegate
            {
                startupCheck.Stop();
                startupCheck.Dispose();
                CheckDeviceConfigOnStartup();
            };
            startupCheck.Start();

            Application.ApplicationExit += delegate { OnExit(); };
            Application.Run();
        }

        private static void InitTray()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = AppName;
            _trayIcon.Icon = AppIcon.LoadForTray();
            _trayIcon.Visible = true;
            Log("托盘图标已加载，来源：" + AppIcon.LastSource);

            ContextMenuStrip menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem("状态：正在检测游戏…");
            _statusItem.Enabled = false;

            ToolStripMenuItem scanItem = new ToolStripMenuItem("扫描音频输出设备…");
            scanItem.Click += delegate { ShowScanDialog(false); };

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

            // 开机（程序启动）时把系统默认输出切到所选设备
            _startupMenu = new ToolStripMenuItem("开机时切换至");
            _startupSpeakerItem = new ToolStripMenuItem("扬声器");
            _startupHeadphoneItem = new ToolStripMenuItem("耳机");
            _startupNoneItem = new ToolStripMenuItem("不切换（跟随系统）");
            _startupSpeakerItem.Click += delegate { SetStartupSwitch("speaker"); };
            _startupHeadphoneItem.Click += delegate { SetStartupSwitch("headphone"); };
            _startupNoneItem.Click += delegate { SetStartupSwitch("none"); };
            _startupMenu.DropDownItems.Add(_startupSpeakerItem);
            _startupMenu.DropDownItems.Add(_startupHeadphoneItem);
            _startupMenu.DropDownItems.Add(new ToolStripSeparator());
            _startupMenu.DropDownItems.Add(_startupNoneItem);
            RefreshStartupMenuChecks();

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };

            menu.Items.Add(_statusItem);
            menu.Items.Add(scanItem);
            menu.Items.Add(manualItem);
            menu.Items.Add(_hotkeyMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(_startupMenu);
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

        // ================= 开机切换目标设备 =================

        /// <summary>刷新「开机时切换至」子菜单的标题与勾选状态。</summary>
        private static void RefreshStartupMenuChecks()
        {
            if (_startupMenu == null) return;

            string mode = (_config == null) ? "none" : Config.NormalizeStartupSwitch(_config.StartupSwitch);
            bool hp = (mode == "headphone");
            bool sp = (mode == "speaker");

            _startupHeadphoneItem.Checked = hp;
            _startupSpeakerItem.Checked = sp;
            _startupNoneItem.Checked = (!hp && !sp);

            if (hp) _startupMenu.Text = "开机时切换至：耳机";
            else if (sp) _startupMenu.Text = "开机时切换至：扬声器";
            else _startupMenu.Text = "开机时切换至：不切换";
        }

        /// <summary>设置开机切换目标并写入 config.ini（下次程序启动时执行）。</summary>
        private static void SetStartupSwitch(string mode)
        {
            if (_config == null) return;

            string normalized = Config.NormalizeStartupSwitch(mode);
            _config.StartupSwitch = normalized;

            try
            {
                Config.SaveTo(_configPath, _config);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存 config.ini 失败：" + ex.Message + "\n本机仍生效，重启后将丢失。", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            RefreshStartupMenuChecks();

            string msg;
            if (normalized == "none")
            {
                msg = "已设置：开机启动时不改变系统默认输出设备";
            }
            else
            {
                string label = (normalized == "headphone") ? "耳机" : "扬声器";
                msg = "已设置：开机启动时自动切换到「" + label + "」";
                if (!IsAutoStartEnabled())
                    msg += "（需同时在菜单勾选「开机自启」才能随开机生效）";
            }

            Log("开机切换设置：startup_switch=" + normalized);
            SetStatus(msg);
            ShowBalloon(msg, 2800);
        }

        /// <summary>
        /// 程序启动后执行一次「开机切换」：把系统默认输出切到用户所选设备。
        /// 仅在无游戏运行、且目标设备可用时执行 —— 避免开机瞬间打断运行中的游戏，或误切到不可用端点。
        /// </summary>
        private static void ApplyStartupSwitch()
        {
            if (_config == null) return;

            string mode = Config.NormalizeStartupSwitch(_config.StartupSwitch);
            if (mode == "none")
            {
                Log("开机切换：未启用，保持系统当前默认输出设备。");
                return;
            }

            if (_monitor != null && _monitor.IsGameRunningNow())
            {
                Log("开机切换：跳过 —— 检测到游戏正在运行，交由自动切换状态机处理。");
                return;
            }

            string target = (mode == "headphone") ? _config.HeadphoneName : _config.SpeakerName;
            string label = (mode == "headphone") ? "耳机" : "扬声器";

            bool available;
            try { available = AudioCore.IsDeviceAvailable(target); }
            catch { available = false; }

            if (!available)
            {
                string skipMsg = "开机切换已跳过：" + label + "「" + target + "」当前不可用";
                Log(skipMsg);
                SetStatus(skipMsg);
                ShowBalloon(skipMsg, 2600);
                return;
            }

            if (IsCurrentDevice(target))
            {
                Log("开机切换：当前输出已是「" + target + "」，无需切换。");
                return;
            }

            bool ok = AudioCore.SetDefaultDevice(target);
            string doneMsg = ok
                ? "开机切换：已切换输出到「" + target + "」"
                : "开机切换失败：目标设备「" + target + "」不可用或未找到";
            Log(doneMsg);
            SetStatus(doneMsg);
            ShowBalloon(doneMsg, ok ? 2200 : 2800);
        }

        /// <summary>当前系统默认输出设备是否已是指定设备（比对时忽略 Windows 端点的「N- 」重名前缀）。</summary>
        private static bool IsCurrentDevice(string deviceName)
        {
            string current = AudioCore.GetCurrentDefaultDeviceName();
            if (current == null || deviceName == null) return false;
            if (current.Equals(deviceName, StringComparison.OrdinalIgnoreCase)) return true;
            return AudioCore.NormalizeDeviceName(current).Equals(
                AudioCore.NormalizeDeviceName(deviceName), StringComparison.OrdinalIgnoreCase);
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
            // 与耳机比对时忽略 Windows 端点的「N- 」重名前缀，避免误判导致反复"切到耳机"
            if (current != null &&
                AudioCore.NormalizeDeviceName(current).Equals(
                    AudioCore.NormalizeDeviceName(_config.HeadphoneName), StringComparison.OrdinalIgnoreCase))
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

        // ================= 扫描音频输出设备 =================

        /// <summary>
        /// 打开扫描窗口。autoTriggered=true 表示由首次运行引导自动弹出（用于文案区分）。
        /// 用户点「保存并应用」后立即写入 config.ini 并刷新设备名引用，无需重启程序。
        /// </summary>
        private static void ShowScanDialog(bool autoTriggered)
        {
            if (_config == null) return;

            using (DeviceScanForm form = new DeviceScanForm(_config.HeadphoneName, _config.SpeakerName))
            {
                if (form.ShowDialog() != DialogResult.OK) return;

                string hp = form.HeadphoneName;
                string sp = form.SpeakerName;
                string notes = form.SaveNotes;
                if (notes.Length > 0) Log("扫描音频输出设备提示：" + notes);

                bool unchanged =
                    string.Equals(hp, _config.HeadphoneName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sp, _config.SpeakerName, StringComparison.OrdinalIgnoreCase);
                if (unchanged)
                {
                    Log("扫描音频输出设备：设备名无变化，未改动配置。");
                    ShowBalloon("设备名未变化，配置保持不变。", 1500);
                    return;
                }

                string oldHp = _config.HeadphoneName;
                string oldSp = _config.SpeakerName;
                _config.HeadphoneName = hp;
                _config.SpeakerName = sp;

                try
                {
                    Config.SaveTo(_configPath, _config);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("保存 config.ini 失败：" + ex.Message
                        + "\n新设备名在本机仍生效，重启后将丢失。", AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                Log("扫描音频输出设备：耳机 " + oldHp + " → " + hp + "；扬声器 " + oldSp + " → " + sp);
                SetStatus("设备配置已更新：耳机「" + hp + "」/ 扬声器「" + sp + "」");

                string tip = "设备配置已保存并生效：游戏启动切「" + hp + "」，游戏关闭回「" + sp + "」";
                if (notes.Length > 0) tip += "。" + notes + "。";
                ShowBalloon(tip, 2500);
            }
        }

        /// <summary>
        /// 新用户引导：启动时若配置中的耳机/扬声器在本机无法匹配到可用端点，
        /// 说明配置沿用了默认值或设备已变动 —— 直接弹出扫描窗口，一步完成配置。
        /// </summary>
        private static void CheckDeviceConfigOnStartup()
        {
            if (_config == null || _trayIcon == null) return;

            bool hpOk;
            bool spOk;
            try
            {
                hpOk = AudioCore.IsDeviceAvailable(_config.HeadphoneName);
                spOk = AudioCore.IsDeviceAvailable(_config.SpeakerName);
            }
            catch { return; }

            if (hpOk && spOk)
            {
                Log("设备配置自检通过：耳机「" + _config.HeadphoneName + "」/ 扬声器「" + _config.SpeakerName + "」均可用。");
                ApplyStartupSwitch();
                return;
            }

            if (!hpOk && !spOk)
            {
                // 两个都对不上：典型的首次运行 / 换机场景，直接引导
                Log("设备配置自检未通过：耳机与扬声器均未匹配到可用设备，自动打开扫描窗口引导配置。");
                ShowBalloon("未找到配置中的音频设备，正在打开「扫描音频输出设备」向导…", 3000);
                ShowScanDialog(true);
            }
            else
            {
                string bad = hpOk ? _config.SpeakerName : _config.HeadphoneName;
                string role = hpOk ? "扬声器" : "耳机";
                Log("设备配置自检警告：" + role + "「" + bad + "」当前不可用。");
                ShowBalloon("提示：" + role + "「" + bad + "」当前未找到，可右键托盘图标选择「扫描音频输出设备…」重新指定。", 3000);
            }
        }
    }
}
