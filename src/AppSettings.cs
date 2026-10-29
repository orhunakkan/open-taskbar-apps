using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace OpenTaskbar
{
    /// <summary>User preferences, stored as key=value lines in %APPDATA%\OpenTaskbar\settings.ini.</summary>
    internal sealed class AppSettings
    {
        public bool Maximize = true;
        public bool SkipRunning = true;
        public HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string DataFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenTaskbar"); }
        }

        private static string FilePath
        {
            get { return Path.Combine(DataFolder, "settings.ini"); }
        }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            if (!File.Exists(FilePath))
                return s;
            try
            {
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string name = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (name == "Maximize") s.Maximize = value != "false";
                    else if (name == "SkipRunning") s.SkipRunning = value != "false";
                    else if (name == "Exclude" && value.Length > 0) s.Excluded.Add(value);
                }
            }
            catch (Exception)
            {
                // Unreadable settings: fall back to defaults rather than failing to launch.
            }
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(DataFolder);
            var lines = new List<string>();
            lines.Add("Maximize=" + (Maximize ? "true" : "false"));
            lines.Add("SkipRunning=" + (SkipRunning ? "true" : "false"));
            foreach (string key in Excluded)
                lines.Add("Exclude=" + key);
            File.WriteAllLines(FilePath, lines);
        }
    }

    /// <summary>The "start in the tray when I sign in" switch: an entry under HKCU\...\Run.</summary>
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        public const string ValueName = "Open Taskbar";

        public static bool IsEnabled()
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                if (run == null || run.GetValue(ValueName) == null)
                    return false;
            }
            // Task Manager's "Disable" leaves the Run entry and marks it here (odd first byte = disabled).
            using (RegistryKey approved = Registry.CurrentUser.OpenSubKey(ApprovedKey))
            {
                byte[] state = approved == null ? null : approved.GetValue(ValueName) as byte[];
                return state == null || state.Length == 0 || (state[0] & 1) == 0;
            }
        }

        public static void SetEnabled(bool enabled, string exePath)
        {
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                    run.SetValue(ValueName, "\"" + exePath + "\" --tray");
                else
                    run.DeleteValue(ValueName, false);
            }
            using (RegistryKey approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, true))
            {
                if (approved != null)
                    approved.DeleteValue(ValueName, false);
            }
        }
    }
}
