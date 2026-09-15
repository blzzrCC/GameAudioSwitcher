using System;
using System.Diagnostics;
using System.Threading;

namespace GameAudioSwitcher
{
    internal class GameStateEventArgs : EventArgs
    {
        public string Message;
        public bool ShowBalloon;

        public GameStateEventArgs(string message, bool showBalloon)
        {
            Message = message;
            ShowBalloon = showBalloon;
        }
    }

    /// <summary>
    /// 游戏进程监控 + 边界切换状态机：
    /// 仅当 游戏启动（无→有）或 游戏关闭（有→无）时切换设备；
    /// 游戏运行期间不干预用户手动切换；
    /// 耳机从不可用变为可用且游戏仍在运行时，补切一次耳机。
    /// </summary>
    internal class GameMonitor
    {
        private readonly Config _config;
        private Timer _timer;
        private volatile bool _busy;
        private bool _lastGameDetected;
        private bool _headphoneWasUnavailable;

        public event EventHandler<GameStateEventArgs> StateChanged;

        public GameMonitor(Config config)
        {
            _config = config;
        }

        public void Start()
        {
            _timer = new Timer(Tick, null, 0, _config.PollIntervalMs);
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        /// <summary>暂停自动切换：停表并重置边界状态（程序仍驻留，手动切换/快捷键不受影响）。</summary>
        public void Pause()
        {
            Stop();
            _lastGameDetected = false;
            _headphoneWasUnavailable = false;
        }

        /// <summary>恢复自动切换：重新开始轮询。</summary>
        public void Resume()
        {
            Start();
        }

        /// <summary>
        /// 一次性校正到应有状态（供"恢复自动切换"时调用，避免暂停期间错过边界）：
        /// 游戏运行中 → 确保耳机；无游戏 → 确保扬声器。之后边界状态同步，轮询不会重复切换。
        /// </summary>
        public void Reconcile()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                bool game = IsGameRunning();
                bool headphoneOk = AudioCore.IsDeviceAvailable(_config.HeadphoneName);

                if (game)
                {
                    if (headphoneOk)
                    {
                        _headphoneWasUnavailable = false;
                        if (!IsCurrentDevice(_config.HeadphoneName))
                        {
                            bool ok = AudioCore.SetDefaultDevice(_config.HeadphoneName);
                            if (ok)
                                Raise("自动切换已恢复：检测到游戏，已切换输出到「" + _config.HeadphoneName + "」", true);
                            else
                                Raise("自动切换已恢复：检测到游戏，但切换耳机失败，请检查设备", true);
                        }
                        else
                        {
                            Raise("自动切换已恢复：游戏运行中，当前输出已是「" + _config.HeadphoneName + "」", false);
                        }
                    }
                    else
                    {
                        _headphoneWasUnavailable = true;
                        Raise("自动切换已恢复：游戏运行中，但耳机当前不可用，未切换", true);
                    }
                }
                else
                {
                    if (!IsCurrentDevice(_config.SpeakerName))
                    {
                        bool ok = AudioCore.SetDefaultDevice(_config.SpeakerName);
                        if (ok)
                            Raise("自动切换已恢复：无游戏运行，已恢复输出到「" + _config.SpeakerName + "」", true);
                        else
                            Raise("自动切换已恢复：恢复扬声器失败，请检查设备", true);
                    }
                    else
                    {
                        Raise("自动切换已恢复：无游戏运行，当前输出已是「" + _config.SpeakerName + "」", false);
                    }
                }
                _lastGameDetected = game;
            }
            catch (Exception ex)
            {
                Raise("自动切换恢复出错：" + ex.Message, false);
            }
            finally
            {
                _busy = false;
            }
        }

        private void Tick(object state)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                bool game = IsGameRunning();
                bool headphoneOk = AudioCore.IsDeviceAvailable(_config.HeadphoneName);

                if (game && !_lastGameDetected)
                {
                    // 边界：游戏刚启动 → 切耳机（耳机不可用则跳过；已是耳机则无需操作）
                    if (headphoneOk)
                    {
                        if (IsCurrentDevice(_config.HeadphoneName))
                        {
                            _headphoneWasUnavailable = false;
                            Raise("检测到游戏启动，当前输出已是「" + _config.HeadphoneName + "」", false);
                        }
                        else
                        {
                            bool ok = AudioCore.SetDefaultDevice(_config.HeadphoneName);
                            _headphoneWasUnavailable = !ok;
                            if (ok)
                                Raise("检测到游戏启动，已切换输出到「" + _config.HeadphoneName + "」", true);
                            else
                                Raise("检测到游戏启动，但切换耳机失败，请检查设备", true);
                        }
                    }
                    else
                    {
                        _headphoneWasUnavailable = true;
                        Raise("检测到游戏启动，但耳机当前不可用，未切换", true);
                    }
                }
                else if (!game && _lastGameDetected)
                {
                    // 边界：游戏刚关闭 → 恢复扬声器（已是扬声器则无需操作）
                    if (IsCurrentDevice(_config.SpeakerName))
                    {
                        _headphoneWasUnavailable = false;
                        Raise("游戏已关闭，当前输出已是「" + _config.SpeakerName + "」", false);
                    }
                    else
                    {
                        bool ok = AudioCore.SetDefaultDevice(_config.SpeakerName);
                        _headphoneWasUnavailable = false;
                        if (ok)
                            Raise("游戏已关闭，已恢复输出到「" + _config.SpeakerName + "」", true);
                        else
                            Raise("游戏已关闭，但恢复扬声器失败，请检查设备", true);
                    }
                }
                else if (game && _headphoneWasUnavailable && headphoneOk)
                {
                    // 游戏运行中，耳机刚变为可用 → 补切耳机
                    bool ok = AudioCore.SetDefaultDevice(_config.HeadphoneName);
                    _headphoneWasUnavailable = !ok;
                    if (ok)
                        Raise("检测到耳机已连接，已切换输出到耳机", true);
                }
                else if (game && !headphoneOk && !_headphoneWasUnavailable)
                {
                    // 游戏运行中，耳机刚拔出 → 仅记录状态，不切回扬声器（尊重边界切换原则）
                    _headphoneWasUnavailable = true;
                }

                _lastGameDetected = game;
            }
            catch (Exception ex)
            {
                Raise("检测出错：" + ex.Message, false);
            }
            finally
            {
                _busy = false;
            }
        }

        private bool IsGameRunning()
        {
            string[] names = _config.GameProcessNames.ToArray();
            foreach (string full in names)
            {
                string name = full;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);
                if (name.Length == 0) continue;

                Process[] procs = null;
                try { procs = Process.GetProcessesByName(name); }
                catch { continue; }

                if (procs != null && procs.Length > 0)
                {
                    foreach (Process p in procs) p.Dispose();
                    return true;
                }
            }
            return false;
        }

        /// <summary>当前系统默认输出设备是否已是指定设备（比对时忽略 Windows 端点的「N- 」重名前缀）。</summary>
        private bool IsCurrentDevice(string deviceName)
        {
            string current = AudioCore.GetCurrentDefaultDeviceName();
            if (current == null || deviceName == null) return false;
            if (current.Equals(deviceName, StringComparison.OrdinalIgnoreCase)) return true;
            return AudioCore.NormalizeDeviceName(current).Equals(
                AudioCore.NormalizeDeviceName(deviceName), StringComparison.OrdinalIgnoreCase);
        }

        private void Raise(string message, bool showBalloon)
        {
            EventHandler<GameStateEventArgs> handler = StateChanged;
            if (handler != null) handler(this, new GameStateEventArgs(message, showBalloon));
        }
    }
}
