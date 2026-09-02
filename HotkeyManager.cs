using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameAudioSwitcher
{
    /// <summary>热键修饰键组合（WinForms 无现成枚举，自定）。</summary>
    [Flags]
    internal enum HotkeyModifiers
    {
        None = 0,
        Ctrl = 1,
        Alt = 2,
        Shift = 4,
        Win = 8
    }

    /// <summary>
    /// 全局热键注册与组合键字符串解析/格式化。
    /// 组合键格式示例：Ctrl+Alt+H、Shift+F8、Ctrl+Shift+7、F9（单独功能键）。
    /// 支持修饰键 Ctrl / Alt / Shift / Win 与主键（字母、数字、F1-F24 及其它 Keys 枚举名）。
    /// </summary>
    internal static class GlobalHotkey
    {
        public const int WM_HOTKEY = 0x0312;
        public const int HOTKEY_ID = 0x8001; // 本程序唯一热键 ID

        private const uint MOD_ALT = 0x1;
        private const uint MOD_CONTROL = 0x2;
        private const uint MOD_SHIFT = 0x4;
        private const uint MOD_WIN = 0x8;
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>以"修饰键+主键"解析配置字符串。成功返回 true；spec 为空返回 false 且 error 为空（表示禁用）。</summary>
        public static bool Parse(string spec, out HotkeyModifiers modifiers, out Keys key, out string error)
        {
            modifiers = HotkeyModifiers.None;
            key = Keys.None;
            error = null;

            if (spec == null) spec = "";
            spec = spec.Trim();
            if (spec.Length == 0)
            {
                error = null; // 空串 = 不注册
                return false;
            }

            string[] tokens = spec.Split('+');
            HotkeyModifiers mods = HotkeyModifiers.None;
            Keys mainKey = Keys.None;

            foreach (string raw in tokens)
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;

                if (t.Equals("CTRL", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("CONTROL", StringComparison.OrdinalIgnoreCase)) { mods |= HotkeyModifiers.Ctrl; continue; }
                if (t.Equals("ALT", StringComparison.OrdinalIgnoreCase)) { mods |= HotkeyModifiers.Alt; continue; }
                if (t.Equals("SHIFT", StringComparison.OrdinalIgnoreCase)) { mods |= HotkeyModifiers.Shift; continue; }
                if (t.Equals("WIN", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase)) { mods |= HotkeyModifiers.Win; continue; }

                if (mainKey != Keys.None)
                {
                    error = "组合键包含多个主键：" + spec;
                    return false;
                }
                Keys k = ParseKeyToken(t);
                if (k == Keys.None)
                {
                    error = "无法识别的按键：" + t;
                    return false;
                }
                mainKey = k;
            }

            if (mainKey == Keys.None)
            {
                error = "缺少主键（如字母、数字、F1-F24）";
                return false;
            }

            bool hasModifier = mods != HotkeyModifiers.None;
            bool isFunctionKey = (mainKey >= Keys.F1 && mainKey <= Keys.F24);
            if (!hasModifier && !isFunctionKey)
            {
                error = "组合键需包含 Ctrl/Alt/Shift/Win 之一，或单独使用 F1-F24 功能键";
                return false;
            }

            modifiers = mods;
            key = mainKey;
            return true;
        }

        /// <summary>注册全局热键。成功返回 true；spec 为空返回 true（视为禁用，无操作）。失败时 error 给出原因。</summary>
        public static bool Register(IntPtr hWnd, string spec, out string error)
        {
            error = null;
            if (hWnd == IntPtr.Zero)
            {
                error = "消息窗口未就绪";
                return false;
            }

            if (spec == null) spec = "";
            spec = spec.Trim();
            if (spec.Length == 0) return true; // 未配置热键 = 禁用

            HotkeyModifiers mods;
            Keys key;
            if (!Parse(spec, out mods, out key, out error)) return false;

            uint flags = MOD_NOREPEAT; // 长按不重复触发
            if ((mods & HotkeyModifiers.Ctrl) != HotkeyModifiers.None) flags |= MOD_CONTROL;
            if ((mods & HotkeyModifiers.Alt) != HotkeyModifiers.None) flags |= MOD_ALT;
            if ((mods & HotkeyModifiers.Shift) != HotkeyModifiers.None) flags |= MOD_SHIFT;
            if ((mods & HotkeyModifiers.Win) != HotkeyModifiers.None) flags |= MOD_WIN;

            if (!RegisterHotKey(hWnd, HOTKEY_ID, flags, (uint)key))
            {
                int err = Marshal.GetLastWin32Error();
                error = "注册失败（错误码 " + err + "），该组合可能已被其他程序占用";
                return false;
            }
            return true;
        }

        public static void Unregister(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero)
            {
                try { UnregisterHotKey(hWnd, HOTKEY_ID); }
                catch { }
            }
        }

        /// <summary>将组合键格式化为可读字符串（如 Ctrl+Alt+H）。</summary>
        public static string Format(HotkeyModifiers modifiers, Keys key)
        {
            List<string> parts = new List<string>();
            if ((modifiers & HotkeyModifiers.Ctrl) != HotkeyModifiers.None) parts.Add("Ctrl");
            if ((modifiers & HotkeyModifiers.Alt) != HotkeyModifiers.None) parts.Add("Alt");
            if ((modifiers & HotkeyModifiers.Shift) != HotkeyModifiers.None) parts.Add("Shift");
            if ((modifiers & HotkeyModifiers.Win) != HotkeyModifiers.None) parts.Add("Win");
            parts.Add(KeyTokenName(key));
            return string.Join("+", parts.ToArray());
        }

        private static string KeyTokenName(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            if (code >= Keys.F1 && code <= Keys.F24)
                return "F" + (int)(code - Keys.F1 + 1);
            if (code >= Keys.D0 && code <= Keys.D9)
                return ((char)('0' + (int)(code - Keys.D0))).ToString();
            if (code >= Keys.A && code <= Keys.Z)
                return ((char)('A' + (int)(code - Keys.A))).ToString();
            return code.ToString();
        }

        private static Keys ParseKeyToken(string token)
        {
            if (token.Length == 1)
            {
                char c = char.ToUpperInvariant(token[0]);
                if (c >= 'A' && c <= 'Z') return (Keys)c;
                if (c >= '0' && c <= '9') return (Keys)c; // 数字键，Keys.D0..D9 与 ASCII 一致
                return Keys.None;
            }

            if (token[0] == 'F' || token[0] == 'f')
            {
                string num = token.Substring(1);
                int n;
                if (int.TryParse(num, out n) && n >= 1 && n <= 24)
                    return Keys.F1 + (n - 1);
                return Keys.None;
            }

            Keys parsed;
            if (Enum.TryParse<Keys>(token, true, out parsed))
            {
                Keys code = parsed & Keys.KeyCode;
                // 排除修饰键本身与鼠标键等不适合全局注册的键
                switch (code)
                {
                    case Keys.ShiftKey:
                    case Keys.ControlKey:
                    case Keys.Menu:
                    case Keys.LWin:
                    case Keys.RWin:
                    case Keys.LButton:
                    case Keys.MButton:
                    case Keys.RButton:
                        return Keys.None;
                    default:
                        return code;
                }
            }
            return Keys.None;
        }
    }

    /// <summary>用于接收 WM_HOTKEY 消息的隐藏窗口。托盘程序没有主窗体，用 NativeWindow 承载消息循环。</summary>
    internal class HotkeyWindow : NativeWindow
    {
        public event Action HotkeyPressed;

        public void EnsureHandle()
        {
            if (this.Handle == IntPtr.Zero)
            {
                CreateParams cp = new CreateParams();
                cp.Caption = "GameAudioSwitcherHotkeyWindow";
                this.CreateHandle(cp);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == GlobalHotkey.WM_HOTKEY)
            {
                Action handler = HotkeyPressed;
                if (handler != null) handler();
            }
            base.WndProc(ref m);
        }
    }
}
