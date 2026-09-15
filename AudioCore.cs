using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GameAudioSwitcher
{
    internal enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }

    internal enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    // 端点状态掩码
    internal static class DevState
    {
        public const uint ACTIVE = 0x1;
        public const uint DISABLED = 0x2;
        public const uint NOTPRESENT = 0x4;
        public const uint UNPLUGGED = 0x8;
        public const uint ALL = 0xF;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice endpoint);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        int GetCount(out uint cDevices);
        int Item(uint nDevice, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        int GetState(out int pdwState);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr data1;
        public IntPtr data2;
    }

    // ================= PolicyConfig（未文档化 COM，设置系统默认音频设备） =================
    // 参照 SoundSwitch / AudioSwitcher 的权威实现：
    //   CoClass CLSID = 870AF99C-171D-4F9E-AF0D-E63DF40C2BC9
    //   同一 COM 对象支持多个版本接口（Redstone / Win7 / Vista），按优先级 QueryInterface。
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class CPolicyConfigClient { }

    // Redstone 版接口（Windows 10 1607+），IID = CA286FC3-91FD-42C3-8E9B-CAAFA66242E3
    [ComImport, Guid("CA286FC3-91FD-42C3-8E9B-CAAFA66242E3"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfigX
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.U4)] ERole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
    }

    // Win7 版接口，IID = F8679F50-850A-41CF-9C72-430F290290C8（vtable 与 X 相同）
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.U4)] ERole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
    }

    // Vista 版接口，IID = 568B9108-44BF-40B4-9006-86AFE5B5A620（11 个方法，无 ResetDeviceFormat）
    [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfigVista
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr ppFormat);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.U4)] ERole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
    }

    /// <summary>渲染端点的扫描结果（供「扫描音频输出设备」界面使用）。</summary>
    internal class AudioDeviceInfo
    {
        private const int STATE_ACTIVE = 0x1;

        /// <summary>FriendlyName 原样，可能带 Windows 消歧序号，如「耳机 (2- Realtek(R) Audio)」。</summary>
        public string Name = "";
        /// <summary>剥离消歧序号后的名称，用于跨驱动重枚举的稳定匹配。</summary>
        public string BaseName = "";
        /// <summary>端点 ID（如 {0.0.0.00000000}.{...}）。</summary>
        public string Id = "";
        /// <summary>端点状态原始掩码。</summary>
        public int State = -1;
        /// <summary>是否为当前系统默认输出设备（eConsole 角色）。</summary>
        public bool IsDefault;

        /// <summary>是否处于 ACTIVE（可用）状态。</summary>
        public bool IsActive { get { return (State & STATE_ACTIVE) != 0; } }

        /// <summary>状态中文描述。</summary>
        public string StateText
        {
            get
            {
                if (IsActive) return "可用";
                if ((State & (int)DevState.UNPLUGGED) != 0) return "未插入";
                if ((State & (int)DevState.DISABLED) != 0) return "已禁用";
                if ((State & (int)DevState.NOTPRESENT) != 0) return "不存在";
                return "未知(" + State + ")";
            }
        }
    }

    /// <summary>音频设备核心操作：枚举、可用性检测、切换默认设备。</summary>
    internal static class AudioCore
    {
        private static readonly Guid PKEY_Device_FriendlyName = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");

        // 耳机类设备主关键字（小写比对，覆盖中英文常见命名）
        private static readonly string[] HeadphoneKeywords = {
            "耳机", "耳麦", "headphone", "headset", "earphone", "earbud", "airpod", "buds", "tws"
        };
        // 扬声器类设备主关键字
        private static readonly string[] SpeakerKeywords = {
            "扬声器", "喇叭", "speaker", "speakers"
        };
        // 扬声器类设备次关键字（显示器/数字输出等非常规命名，兜底用）
        private static readonly string[] SpeakerSecondaryKeywords = {
            "display audio", "hdmi", "digital output", "line out", "spdif"
        };

        private static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        }

        /// <summary>
        /// 扫描全部渲染端点，返回含名称 / 归一化名称 / ID / 状态 / 是否默认 的完整信息。
        /// 排序：可用优先 → 当前默认优先 → 名称升序，便于界面首屏直接落在可用设备上。
        /// </summary>
        public static List<AudioDeviceInfo> ScanRenderDevices()
        {
            List<AudioDeviceInfo> result = new List<AudioDeviceInfo>();
            string defaultId = GetDefaultDeviceId();

            try
            {
                IMMDeviceEnumerator enumerator = CreateEnumerator();
                IMMDeviceCollection collection;
                int hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DevState.ALL, out collection);
                if (hr != 0 || collection == null)
                {
                    Marshal.ReleaseComObject(enumerator);
                    return result;
                }

                uint count;
                collection.GetCount(out count);
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device;
                    if (collection.Item(i, out device) != 0 || device == null) continue;

                    string name = GetFriendlyName(device);
                    if (name != null)
                    {
                        AudioDeviceInfo info = new AudioDeviceInfo();
                        info.Name = name;
                        info.BaseName = NormalizeDeviceName(name);

                        string id;
                        if (device.GetId(out id) == 0) info.Id = id;
                        info.IsDefault = id != null && defaultId != null &&
                            id.Equals(defaultId, StringComparison.OrdinalIgnoreCase);

                        int state = -1;
                        device.GetState(out state);
                        info.State = state;

                        result.Add(info);
                    }
                    Marshal.ReleaseComObject(device);
                }

                Marshal.ReleaseComObject(collection);
                Marshal.ReleaseComObject(enumerator);
            }
            catch { }

            result.Sort(delegate(AudioDeviceInfo a, AudioDeviceInfo b)
            {
                if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
                if (a.IsDefault != b.IsDefault) return a.IsDefault ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        /// <summary>
        /// 从扫描结果中自动推定「耳机 / 扬声器」配对，供新用户一键完成配置。
        /// 推定顺序（先只看可用端点，再退回全量，以覆盖「耳机未插入但系统中有记录」的常见情形）：
        ///   扬声器：主关键字 → 次关键字 → 当前默认设备 → 可用末端点 → 全量末端点
        ///   耳机  ：可用端点主关键字 → 全量端点主关键字 → 可用剩余端点 → 全量剩余端点
        /// </summary>
        public static void AutoDetectPair(List<AudioDeviceInfo> devices, out string headphone, out string speaker)
        {
            headphone = null;
            speaker = null;
            if (devices == null || devices.Count == 0) return;

            List<AudioDeviceInfo> all = devices;
            List<AudioDeviceInfo> active = new List<AudioDeviceInfo>();
            foreach (AudioDeviceInfo d in all) if (d.IsActive) active.Add(d);

            // ---- 扬声器 ----
            AudioDeviceInfo sp = FirstMatch(active, SpeakerKeywords);
            if (sp == null) sp = FirstMatch(active, SpeakerSecondaryKeywords);
            if (sp == null)
            {
                foreach (AudioDeviceInfo d in active) if (d.IsDefault) { sp = d; break; }
            }
            if (sp == null && active.Count > 0) sp = active[active.Count - 1];
            if (sp == null) sp = all[all.Count - 1];

            // ---- 耳机 ----
            AudioDeviceInfo hp = FirstMatch(active, HeadphoneKeywords, sp);
            if (hp == null) hp = FirstMatch(all, HeadphoneKeywords, sp);
            if (hp == null) hp = FirstOther(active, sp);
            if (hp == null) hp = FirstOther(all, sp);

            if (hp != null) headphone = hp.Name;
            if (sp != null) speaker = sp.Name;

            // 两者仍指向同一端点时，说明本机只有一个输出设备：保留扬声器，耳机留空由用户决定
            if (headphone != null && speaker != null &&
                headphone.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                headphone = null;
        }

        /// <summary>名称是否命中耳机类关键字。</summary>
        public static bool LooksLikeHeadphone(string name)
        {
            return ContainsAny(name, HeadphoneKeywords);
        }

        /// <summary>名称是否命中扬声器类关键字。</summary>
        public static bool LooksLikeSpeaker(string name)
        {
            return ContainsAny(name, SpeakerKeywords) || ContainsAny(name, SpeakerSecondaryKeywords);
        }

        private static AudioDeviceInfo FirstMatch(List<AudioDeviceInfo> pool, string[] keywords)
        {
            return FirstMatch(pool, keywords, null);
        }

        private static AudioDeviceInfo FirstMatch(List<AudioDeviceInfo> pool, string[] keywords, AudioDeviceInfo exclude)
        {
            foreach (AudioDeviceInfo d in pool)
            {
                if (exclude != null && d.Name.Equals(exclude.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (ContainsAny(d.Name, keywords)) return d;
            }
            return null;
        }

        private static AudioDeviceInfo FirstOther(List<AudioDeviceInfo> pool, AudioDeviceInfo exclude)
        {
            foreach (AudioDeviceInfo d in pool)
            {
                if (exclude != null && d.Name.Equals(exclude.Name, StringComparison.OrdinalIgnoreCase)) continue;
                return d;
            }
            return null;
        }

        private static bool ContainsAny(string name, string[] keywords)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            foreach (string k in keywords)
                if (lower.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>取当前默认输出端点的 ID（eConsole 角色），失败返回 null。</summary>
        private static string GetDefaultDeviceId()
        {
            try
            {
                IMMDeviceEnumerator enumerator = CreateEnumerator();
                IMMDevice endpoint;
                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out endpoint) != 0)
                {
                    Marshal.ReleaseComObject(enumerator);
                    return null;
                }
                string id;
                endpoint.GetId(out id);
                Marshal.ReleaseComObject(endpoint);
                Marshal.ReleaseComObject(enumerator);
                return id;
            }
            catch { return null; }
        }

        /// <summary>枚举所有渲染端点：返回 (名称, 状态) 列表。</summary>
        public static List<KeyValuePair<string, int>> ListRenderDevices()
        {
            List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>();
            try
            {
                IMMDeviceEnumerator enumerator = CreateEnumerator();
                IMMDeviceCollection collection;
                int hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DevState.ALL, out collection);
                if (hr != 0 || collection == null) return result;

                uint count;
                collection.GetCount(out count);
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device;
                    if (collection.Item(i, out device) != 0 || device == null) continue;
                    string name = GetFriendlyName(device);
                    int state = -1;
                    device.GetState(out state);
                    if (name != null) result.Add(new KeyValuePair<string, int>(name, state));
                    Marshal.ReleaseComObject(device);
                }
                Marshal.ReleaseComObject(collection);
                Marshal.ReleaseComObject(enumerator);
            }
            catch { }
            return result;
        }

        /// <summary>设备是否处于可用（ACTIVE）状态。</summary>
        public static bool IsDeviceAvailable(string friendlyName)
        {
            IMMDevice device = FindDevice(friendlyName);
            if (device == null) return false;
            try
            {
                int state;
                device.GetState(out state);
                return (state & (int)DevState.ACTIVE) != 0;
            }
            catch { return false; }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }

        /// <summary>将默认输出设备 + 默认通信设备都切换为目标设备。设备不可用或未找到时返回 false。</summary>
        public static bool SetDefaultDevice(string friendlyName)
        {
            IMMDevice device = FindDevice(friendlyName);
            if (device == null) return false;

            try
            {
                int state;
                device.GetState(out state);
                if ((state & (int)DevState.ACTIVE) == 0) return false; // 设备不可用，不切换

                string deviceId;
                device.GetId(out deviceId);

                return SetDefaultEndpoint(deviceId);
            }
            catch { return false; }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }

        /// <summary>读取当前默认输出设备（eConsole 角色）的名称，未找到返回 null。</summary>
        public static string GetCurrentDefaultDeviceName()
        {
            try
            {
                IMMDeviceEnumerator enumerator = CreateEnumerator();
                IMMDevice endpoint;
                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out endpoint) != 0)
                {
                    Marshal.ReleaseComObject(enumerator);
                    return null;
                }
                string name = GetFriendlyName(endpoint);
                Marshal.ReleaseComObject(endpoint);
                Marshal.ReleaseComObject(enumerator);
                return name;
            }
            catch { return null; }
        }

        /// <summary>设置指定设备为系统默认（eConsole / eMultimedia / eCommunications 三个角色）。</summary>
        private static bool SetDefaultEndpoint(string deviceId)
        {
            object co;
            try { co = new CPolicyConfigClient(); }
            catch { return false; }
            if (co == null) return false;

            try
            {
                // 按版本优先级尝试：Redstone → Win7 → Vista
                IPolicyConfigX px = co as IPolicyConfigX;
                if (px != null)
                {
                    bool ok = SetAllRoles(delegate(ERole role) { return px.SetDefaultEndpoint(deviceId, role); });
                    Marshal.FinalReleaseComObject(px);
                    return ok;
                }
                IPolicyConfig p7 = co as IPolicyConfig;
                if (p7 != null)
                {
                    bool ok = SetAllRoles(delegate(ERole role) { return p7.SetDefaultEndpoint(deviceId, role); });
                    Marshal.FinalReleaseComObject(p7);
                    return ok;
                }
                IPolicyConfigVista pv = co as IPolicyConfigVista;
                if (pv != null)
                {
                    bool ok = SetAllRoles(delegate(ERole role) { return pv.SetDefaultEndpoint(deviceId, role); });
                    Marshal.FinalReleaseComObject(pv);
                    return ok;
                }
            }
            catch { }
            finally
            {
                if (Marshal.IsComObject(co)) Marshal.FinalReleaseComObject(co);
            }
            return false;
        }

        private static bool SetAllRoles(Func<ERole, int> setter)
        {
            int r1 = setter(ERole.eConsole);
            int r2 = setter(ERole.eMultimedia);
            int r3 = setter(ERole.eCommunications);
            return r1 == 0 && r2 == 0 && r3 == 0;
        }

        /// <summary>
        /// 归一化设备名：剥离 Windows 端点重名消歧序号，便于跨界面/驱动变化稳定匹配。
        /// 实测两种形式：
        ///   ① 序号在名称开头，如 "2- 耳机 (Realtek(R) Audio)"
        ///   ② 序号在首个括号内，如 "耳机 (2- Realtek(R) Audio)"（本机实际形态）
        /// 严格匹配「数字 + '-' + 空格」结构，形如 "5-1 音箱" 不会被误剥离。
        /// </summary>
        public static string NormalizeDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // 形态①：名称开头的 "N- "
            int i = 0;
            while (i < name.Length && name[i] >= '0' && name[i] <= '9') i++;
            if (i > 0 && i + 1 < name.Length && name[i] == '-' && name[i + 1] == ' ')
                return name.Substring(i + 2);

            // 形态②：首个 '(' 之后的 "N- "
            int open = name.IndexOf('(');
            if (open >= 0)
            {
                int k = open + 1;
                int digitsStart = k;
                while (k < name.Length && name[k] >= '0' && name[k] <= '9') k++;
                if (k > digitsStart && k + 1 < name.Length && name[k] == '-' && name[k + 1] == ' ')
                    return name.Substring(0, open + 1) + name.Substring(k + 2);
            }

            return name;
        }

        /// <summary>
        /// 按名称查找渲染端点。名称比对前做归一化（忽略 Windows 的「N- 」重名前缀），
        /// 并优先返回 ACTIVE 端点，避免命中同名但已失效（NOTPRESENT）的历史端点。
        /// </summary>
        private static IMMDevice FindDevice(string friendlyName)
        {
            IMMDeviceEnumerator enumerator = CreateEnumerator();
            IMMDeviceCollection collection;
            int hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DevState.ALL, out collection);
            if (hr != 0 || collection == null)
            {
                Marshal.ReleaseComObject(enumerator);
                return null;
            }

            string target = NormalizeDeviceName(friendlyName);
            IMMDevice active = null;    // 可用端点（优先采用）
            IMMDevice inactive = null;  // 匹配但不可用（兜底，由调用方再做状态校验）

            uint count;
            collection.GetCount(out count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice device;
                if (collection.Item(i, out device) != 0 || device == null) continue;

                string name = GetFriendlyName(device);
                bool match = name != null &&
                    (name.Equals(friendlyName, StringComparison.OrdinalIgnoreCase) ||
                     NormalizeDeviceName(name).Equals(target, StringComparison.OrdinalIgnoreCase));
                if (!match)
                {
                    Marshal.ReleaseComObject(device);
                    continue;
                }

                int state = -1;
                device.GetState(out state);
                if ((state & (int)DevState.ACTIVE) != 0)
                {
                    active = device;   // 命中可用端点，直接采用
                    break;
                }
                if (inactive == null) inactive = device;
                else Marshal.ReleaseComObject(device);
            }

            if (active != null && inactive != null) Marshal.ReleaseComObject(inactive);
            IMMDevice result = active != null ? active : inactive;

            Marshal.ReleaseComObject(collection);
            Marshal.ReleaseComObject(enumerator);
            return result;
        }

        private static string GetFriendlyName(IMMDevice device)
        {
            try
            {
                IPropertyStore store;
                if (device.OpenPropertyStore(0, out store) != 0 || store == null) return null;
                PROPERTYKEY key = new PROPERTYKEY();
                key.fmtid = PKEY_Device_FriendlyName;
                key.pid = 14; // PKEY_Device_FriendlyName

                PROPVARIANT pv;
                if (store.GetValue(ref key, out pv) != 0)
                {
                    Marshal.ReleaseComObject(store);
                    return null;
                }
                string name = null;
                if (pv.vt == 31 /* VT_LPWSTR */ && pv.data1 != IntPtr.Zero)
                    name = Marshal.PtrToStringUni(pv.data1);
                Marshal.ReleaseComObject(store);
                return name;
            }
            catch { return null; }
        }
    }
}
