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

    /// <summary>音频设备核心操作：枚举、可用性检测、切换默认设备。</summary>
    internal static class AudioCore
    {
        private static readonly Guid PKEY_Device_FriendlyName = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");

        private static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
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

            IMMDevice result = null;
            uint count;
            collection.GetCount(out count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice device;
                if (collection.Item(i, out device) != 0 || device == null) continue;
                string name = GetFriendlyName(device);
                if (name != null && name.Equals(friendlyName, StringComparison.OrdinalIgnoreCase))
                {
                    result = device;
                    break;
                }
                Marshal.ReleaseComObject(device);
            }

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
