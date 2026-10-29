using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OpenTaskbar
{
    /// <summary>One item pinned to the taskbar: a desktop app shortcut or a Store app.</summary>
    internal sealed class PinnedApp
    {
        public string Name;
        public string Key;          // stable identifier, used to remember exclusions
        public string TargetPath;   // executable behind a shortcut, if any
        public string AppId;        // AppUserModelID, if any
        public byte[] IdList;       // absolute shell ITEMIDLIST, used to launch it

        /// <summary>True if the window belongs to this app.</summary>
        public bool Matches(WindowInfo window)
        {
            if (AppId != null && window.AppId != null &&
                string.Equals(AppId, window.AppId, StringComparison.OrdinalIgnoreCase))
                return true;
            return TargetPath != null && window.ExePath != null &&
                PathsEqual(TargetPath, window.ExePath);
        }

        public static bool PathsEqual(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    internal static class PinnedApps
    {
        private const string TaskbandKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";

        private static string PinnedFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            }
        }

        /// <summary>
        /// Returns the current taskbar pins in taskbar order, excluding this app itself.
        /// Read fresh on every call, so newly pinned apps are picked up automatically.
        /// </summary>
        public static List<PinnedApp> Load()
        {
            var apps = new List<PinnedApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Explorer keeps every pin (including Store apps, which have no .lnk file) in this blob.
            foreach (byte[] idList in ReadFavorites())
                Add(apps, seen, FromIdList(idList));

            // Shortcut files cover anything the blob is missing, e.g. if its format ever changes.
            if (Directory.Exists(PinnedFolder))
            {
                foreach (string lnk in Directory.GetFiles(PinnedFolder, "*.lnk"))
                {
                    byte[] idList = IdListFromPath(lnk);
                    if (idList != null)
                        Add(apps, seen, FromIdList(idList));
                }
            }

            string self = Application.ExecutablePath;
            apps.RemoveAll(a => a.TargetPath != null && PinnedApp.PathsEqual(a.TargetPath, self));
            return apps;
        }

        private static void Add(List<PinnedApp> apps, HashSet<string> seen, PinnedApp app)
        {
            if (app != null && seen.Add(app.Key))
                apps.Add(app);
        }

        private static List<byte[]> ReadFavorites()
        {
            var result = new List<byte[]>();
            byte[] blob = null;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TaskbandKey))
            {
                if (key != null)
                    blob = key.GetValue("Favorites") as byte[];
            }
            if (blob == null || blob.Length < 6)
                return result;

            // Layout: one version byte, then per pin a 4-byte length, an absolute ITEMIDLIST
            // of that length and one separator byte. 0xFF terminates the list.
            int p = 1;
            while (p + 4 <= blob.Length)
            {
                int len = BitConverter.ToInt32(blob, p);
                if (len < 4 || p + 4 + len > blob.Length)
                    break;
                if (blob[p + 4 + len - 1] != 0 || blob[p + 4 + len - 2] != 0)
                    break; // not a terminated ITEMIDLIST: unknown format, stop here
                var idList = new byte[len];
                Buffer.BlockCopy(blob, p + 4, idList, 0, len);
                result.Add(idList);
                p += 4 + len;
                if (p >= blob.Length || blob[p] == 0xFF)
                    break;
                p++;
            }
            return result;
        }

        private static byte[] IdListFromPath(string path)
        {
            IntPtr pidl;
            uint attrs;
            if (Native.SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out attrs) != 0 || pidl == IntPtr.Zero)
                return null;
            try
            {
                var bytes = new byte[Native.ILGetSize(pidl)];
                Marshal.Copy(pidl, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        private static PinnedApp FromIdList(byte[] idList)
        {
            IntPtr pidl = Native.AllocIdList(idList);
            IShellItem2 item = null;
            try
            {
                Guid iid = Native.IID_IShellItem2;
                if (Native.SHCreateItemFromIDList(pidl, ref iid, out item) != 0 || item == null)
                    return null;

                var app = new PinnedApp();
                app.IdList = idList;
                app.Key = GetDisplayName(item, SIGDN.DESKTOPABSOLUTEPARSING);
                app.Name = GetDisplayName(item, SIGDN.NORMALDISPLAY) ?? app.Key;
                app.TargetPath = GetString(item, Native.PKEY_Link_TargetParsingPath);
                app.AppId = GetString(item, Native.PKEY_AppUserModel_ID);
                if (app.Key == null)
                    return null;
                if (app.TargetPath != null && !Path.IsPathRooted(app.TargetPath))
                    app.TargetPath = null; // e.g. File Explorer's target is a shell folder, not a file
                if (app.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    app.Name = app.Name.Substring(0, app.Name.Length - 4);

                // A Store app pin caches its package version, which goes stale when the app updates.
                // Re-resolve it from its AppUserModelID so launching and icons use the current version.
                if (app.TargetPath == null && app.AppId != null &&
                    string.Equals(app.Key, app.AppId, StringComparison.OrdinalIgnoreCase))
                {
                    byte[] fresh = IdListFromPath(@"shell:AppsFolder\" + app.AppId);
                    if (fresh != null)
                        app.IdList = fresh;
                }
                return app;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (item != null)
                    Marshal.ReleaseComObject(item);
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        private static string GetDisplayName(IShellItem2 item, SIGDN form)
        {
            IntPtr p;
            return item.GetDisplayName(form, out p) == 0 ? Native.TakeCoTaskMemString(p) : null;
        }

        private static string GetString(IShellItem2 item, PROPERTYKEY key)
        {
            IntPtr p;
            string s = item.GetString(ref key, out p) == 0 ? Native.TakeCoTaskMemString(p) : null;
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
