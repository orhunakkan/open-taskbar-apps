using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTaskbar
{
    /// <summary>A top-level window and the app it belongs to.</summary>
    internal sealed class WindowInfo
    {
        public IntPtr Handle;
        public uint ProcessId;
        public string ExePath;
        public string AppId;
        public DateTime ProcessStartUtc;

        public static List<IntPtr> TopLevelWindows()
        {
            var list = new List<IntPtr>();
            Native.EnumWindowsProc proc = (h, l) => { list.Add(h); return true; };
            Native.EnumWindows(proc, IntPtr.Zero);
            GC.KeepAlive(proc);
            return list;
        }

        /// <summary>
        /// True for a visible, resizable, top-level app window: the kind that can be maximized.
        /// Windows cloaked by the shell are on another virtual desktop; they only count when
        /// <paramref name="allowOtherDesktops"/> is set.
        /// </summary>
        public static bool IsAppWindow(IntPtr hwnd, bool allowOtherDesktops)
        {
            if (!Native.IsWindowVisible(hwnd) || Native.GetWindow(hwnd, Native.GW_OWNER) != IntPtr.Zero)
                return false;

            long style = Native.GetWindowLong(hwnd, Native.GWL_STYLE);
            long exStyle = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
            if ((exStyle & (Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE)) != 0)
                return false;
            if ((style & Native.WS_MAXIMIZEBOX) == 0 || (style & Native.WS_THICKFRAME) == 0)
                return false;

            int cloaked;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0)
            {
                if (!(allowOtherDesktops && cloaked == Native.DWM_CLOAKED_SHELL))
                    return false;
            }

            if (!Native.IsIconic(hwnd))
            {
                RECT r;
                if (Native.GetWindowRect(hwnd, out r) && (r.Right - r.Left < 150 || r.Bottom - r.Top < 100))
                    return false;
            }
            return true;
        }

        public static WindowInfo Describe(IntPtr hwnd)
        {
            var info = new WindowInfo();
            info.Handle = hwnd;
            Native.GetWindowThreadProcessId(hwnd, out info.ProcessId);

            string processAppId;
            ReadProcess(info.ProcessId, out info.ExePath, out info.ProcessStartUtc, out processAppId);

            info.AppId = WindowAppId(hwnd) ?? processAppId;
            string className = Native.GetClassName(hwnd);

            // File Explorer windows don't expose an AppUserModelID; this is the one their pin uses.
            if (info.AppId == null && className == "CabinetWClass")
                info.AppId = "Microsoft.Windows.Explorer";

            // UWP apps (e.g. Settings) live in a frame owned by ApplicationFrameHost.exe;
            // the real app is the process behind the CoreWindow child.
            if (info.AppId == null && className == "ApplicationFrameWindow")
            {
                uint childPid = 0;
                Native.EnumWindowsProc proc = (h, l) =>
                {
                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (pid != info.ProcessId && Native.GetClassName(h) == "Windows.UI.Core.CoreWindow")
                    {
                        childPid = pid;
                        return false;
                    }
                    return true;
                };
                Native.EnumChildWindows(hwnd, proc, IntPtr.Zero);
                GC.KeepAlive(proc);
                if (childPid != 0)
                {
                    string exe;
                    DateTime started;
                    ReadProcess(childPid, out exe, out started, out info.AppId);
                }
            }
            return info;
        }

        private static string WindowAppId(IntPtr hwnd)
        {
            IPropertyStore store;
            Guid iid = Native.IID_IPropertyStore;
            if (Native.SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store == null)
                return null;
            try
            {
                PROPERTYKEY key = Native.PKEY_AppUserModel_ID;
                PROPVARIANT pv;
                if (store.GetValue(ref key, out pv) != 0)
                    return null;
                try
                {
                    if (pv.vt != Native.VT_LPWSTR || pv.value == IntPtr.Zero)
                        return null;
                    string s = System.Runtime.InteropServices.Marshal.PtrToStringUni(pv.value);
                    return string.IsNullOrEmpty(s) ? null : s;
                }
                finally
                {
                    Native.PropVariantClear(ref pv);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(store);
            }
        }

        private static void ReadProcess(uint pid, out string exePath, out DateTime startUtc, out string appId)
        {
            exePath = null;
            appId = null;
            startUtc = DateTime.MinValue;

            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero)
                return;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (Native.QueryFullProcessImageName(h, 0, sb, ref size))
                    exePath = sb.ToString();

                long creation, exit, kernel, user;
                if (Native.GetProcessTimes(h, out creation, out exit, out kernel, out user))
                    startUtc = DateTime.FromFileTimeUtc(creation);

                // Only packaged (MSIX / Store) processes have an AppUserModelID here.
                var id = new StringBuilder(256);
                int len = id.Capacity;
                if (Native.GetApplicationUserModelId(h, ref len, id) == 0 && id.Length > 0)
                    appId = id.ToString();
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }
    }
}
