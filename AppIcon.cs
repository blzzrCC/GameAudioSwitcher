using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GameAudioSwitcher
{
    /// <summary>
    /// 应用图标统一入口（图标来源优先级）：
    ///   ① 编译期嵌入的 app.ico 资源（build.bat 中 /resource:app.ico,AppIcon.ico）
    ///   ② EXE 自身图标（/win32icon:app.ico）
    ///   ③ 内置矢量兜底图标（蓝色耳机）
    /// 每次调用都返回**新实例**，由调用方负责释放（Form.Dispose 会释放其 Icon，故不可共用）。
    /// </summary>
    internal static class AppIcon
    {
        private const string ResourceName = "AppIcon.ico";

        private static string _lastSource = "未加载";

        /// <summary>最近一次图标加载的实际来源，写入日志便于自查图标是否生效。</summary>
        public static string LastSource { get { return _lastSource; } }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>加载指定尺寸的应用图标（新实例，调用方负责释放）。</summary>
        public static Icon Load(int size)
        {
            if (size <= 0) size = 32;

            try
            {
                Stream stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
                if (stream != null)
                {
                    using (stream)
                    {
                        Icon ico = new Icon(stream, new Size(size, size));
                        _lastSource = "嵌入资源 " + ResourceName + "（" + size + "px）";
                        return ico;
                    }
                }
            }
            catch { }

            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    Icon scaled = null;
                    try { scaled = new Icon(extracted, new Size(size, size)); }
                    catch { }
                    if (scaled != null)
                    {
                        extracted.Dispose();
                        _lastSource = "EXE 关联图标（" + size + "px）";
                        return scaled;
                    }
                    _lastSource = "EXE 关联图标（原始尺寸）";
                    return extracted; // 无匹配尺寸时按原样使用
                }
            }
            catch { }

            _lastSource = "内置矢量兜底图标（未找到 " + ResourceName + "）";
            return CreateFallback(size);
        }

        /// <summary>托盘图标：按系统小图标尺寸挑选最合适的帧（高 DPI 下会自动取 20/24）。</summary>
        public static Icon LoadForTray()
        {
            int w = SystemInformation.SmallIconSize.Width;
            return Load(w > 0 ? w : 16);
        }

        /// <summary>窗体图标（标题栏 / Alt-Tab）。</summary>
        public static Icon LoadForForm()
        {
            return Load(32);
        }

        /// <summary>内置矢量兜底图标（蓝色耳机），尺寸自适应。</summary>
        public static Icon CreateFallback(int size)
        {
            if (size <= 0) size = 32;
            try
            {
                using (Bitmap bmp = new Bitmap(size, size))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);

                        float k = size / 32f;
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                        {
                            g.FillEllipse(brush, 3 * k, 13 * k, 10 * k, 14 * k);
                            g.FillEllipse(brush, 19 * k, 13 * k, 10 * k, 14 * k);
                            g.FillRectangle(brush, 8 * k, 15 * k, 16 * k, 6 * k);
                        }
                        using (Pen pen = new Pen(Color.FromArgb(41, 128, 185), Math.Max(1f, 4 * k)))
                        {
                            g.DrawArc(pen, 3 * k, 1 * k, 26 * k, 26 * k, 200, 140);
                        }
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        using (Icon temp = Icon.FromHandle(hIcon))
                            return (Icon)temp.Clone();
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch { return (Icon)SystemIcons.Application.Clone(); }
        }
    }
}
