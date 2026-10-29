using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTaskbar
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;

        public PROPERTYKEY(string fmtid, uint pid)
        {
            this.fmtid = new Guid(fmtid);
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPVARIANT
    {
        public ushort vt;
        public ushort reserved1, reserved2, reserved3;
        public IntPtr value;
        public IntPtr value2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string lpVerb;
        public string lpFile;
        public string lpParameters;
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    internal enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        DESKTOPABSOLUTEPARSING = 0x80028000,
    }

    [ComImport, Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem2
    {
        // IShellItem
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IntPtr ppsi);
        [PreserveSig] int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IntPtr psi, uint hint, out int piOrder);
        // IShellItem2
        void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyStoreWithCreateObject(int flags, IntPtr punkCreateObject, ref Guid riid, out IntPtr ppv);
        void GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, int flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(ref PROPERTYKEY keyType, ref Guid riid, out IntPtr ppv);
        void Update(IntPtr pbc);
        void GetProperty(ref PROPERTYKEY key, IntPtr ppropvar);
        void GetCLSID(ref PROPERTYKEY key, out Guid pclsid);
        void GetFileTime(ref PROPERTYKEY key, out long pft);
        void GetInt32(ref PROPERTYKEY key, out int pi);
        [PreserveSig] int GetString(ref PROPERTYKEY key, out IntPtr ppsz);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int cx, cy;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    internal static class Native
    {
        public static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3", 5);
        public static readonly PROPERTYKEY PKEY_Link_TargetParsingPath = new PROPERTYKEY("B9B4B3FC-2B51-4A42-B5D8-324146AFCF25", 2);
        public static readonly Guid IID_IShellItem2 = new Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93");
        public static readonly Guid IID_IPropertyStore = new Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
        public static readonly Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
        public const int SIIGBF_ICONONLY = 0x4;

        public const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
        public const uint SEE_MASK_NOASYNC = 0x00000100;
        public const uint SEE_MASK_FLAG_NO_UI = 0x00000400;

        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_PIDL = 0x8;

        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_MAXIMIZE = 3;

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const long WS_MAXIMIZEBOX = 0x00010000;
        public const long WS_THICKFRAME = 0x00040000;
        public const long WS_EX_TOOLWINDOW = 0x00000080;
        public const long WS_EX_NOACTIVATE = 0x08000000;
        public const uint GW_OWNER = 4;

        public const int DWMWA_CLOAKED = 14;
        public const int DWM_CLOAKED_SHELL = 2;

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const ushort VT_LPWSTR = 31;

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("shell32.dll")]
        public static extern int SHCreateItemFromIDList(IntPtr pidl, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem2 ppv);

        [DllImport("shell32.dll", EntryPoint = "SHCreateItemFromIDList")]
        public static extern int SHCreateImageFactoryFromIDList(IntPtr pidl, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        public static extern uint ILGetSize(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(IntPtr pidl, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll")]
        public static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PROPVARIANT pvar);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hwnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll")]
        public static extern bool GetProcessTimes(IntPtr hProcess, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetApplicationUserModelId(IntPtr hProcess, ref int applicationUserModelIdLength, StringBuilder applicationUserModelId);

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // csc /win32icon stores the app icon under this resource ID.
        public const int AppIconResourceId = 32512;

        [DllImport("comctl32.dll")]
        public static extern int LoadIconWithScaleDown(IntPtr hinst, IntPtr pszName, int cx, int cy, out IntPtr phico);

        public static long GetWindowLong(IntPtr hwnd, int index)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hwnd, index).ToInt64();
            return (uint)GetWindowLong32(hwnd, index);
        }

        public static string GetClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>Reads a string returned through a CoTaskMem pointer and frees it.</summary>
        public static string TakeCoTaskMemString(IntPtr p)
        {
            if (p == IntPtr.Zero)
                return null;
            try { return Marshal.PtrToStringUni(p); }
            finally { Marshal.FreeCoTaskMem(p); }
        }

        /// <summary>Copies a managed ITEMIDLIST into CoTaskMem; the caller frees it.</summary>
        public static IntPtr AllocIdList(byte[] idList)
        {
            IntPtr mem = Marshal.AllocCoTaskMem(idList.Length);
            Marshal.Copy(idList, 0, mem, idList.Length);
            return mem;
        }
    }
}
