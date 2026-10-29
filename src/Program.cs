using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Open Taskbar")]
[assembly: AssemblyProduct("Open Taskbar")]
[assembly: AssemblyDescription("Opens every app pinned to the taskbar, maximized.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace OpenTaskbar
{
    internal static class Program
    {
        public const string TrayMutexName = @"Local\OpenTaskbar.Tray";

        /// <summary>Starts the tray icon; a second copy exits at once if one is already running.</summary>
        public static void StartTray()
        {
            System.Diagnostics.Process.Start(Application.ExecutablePath, "--tray");
        }

        // Usage:
        //   OpenTaskbar.exe              open all pinned apps (what clicking the pinned icon does)
        //   OpenTaskbar.exe --tray       sit silently in the notification area (started at sign-in)
        //   OpenTaskbar.exe --settings   show the settings window (also: Shift+click)
        //   OpenTaskbar.exe --list       print the detected pins to the console
        [STAThread]
        private static int Main(string[] args)
        {
            bool tray = HasArg(args, "--tray");

            if (HasArg(args, "--list"))
                return ListPins();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (tray)
            {
                bool first;
                using (var trayMutex = new Mutex(true, TrayMutexName, out first))
                {
                    if (first)
                        Application.Run(new TrayContext());
                }
                return 0;
            }

            if (HasArg(args, "--settings") || (Control.ModifierKeys & Keys.Shift) != 0)
            {
                Application.Run(new SettingsForm());
                return 0;
            }

            // A second click while a run is still in progress is ignored instead of opening duplicates.
            bool created;
            using (var mutex = new Mutex(true, @"Local\OpenTaskbar.Launch", out created))
            {
                if (!created)
                    return 0;
                try
                {
                    Log.Start();
                    Application.Run(new LaunchSession(AppSettings.Load()));
                }
                catch (Exception ex)
                {
                    Log.Write("Error: " + ex);
                    return 1;
                }
            }
            return 0;
        }

        private static bool HasArg(string[] args, string name)
        {
            return Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        private static int ListPins()
        {
            Native.AttachConsole(-1);
            List<PinnedApp> apps = PinnedApps.Load();
            Console.WriteLine("Pinned apps:");
            foreach (PinnedApp app in apps)
            {
                Console.WriteLine("  " + app.Name);
                Console.WriteLine("      key:    " + app.Key);
                Console.WriteLine("      target: " + (app.TargetPath ?? "-"));
                Console.WriteLine("      app id: " + (app.AppId ?? "-"));
            }
            Console.WriteLine();
            Console.WriteLine("Open app windows:");
            foreach (IntPtr h in WindowInfo.TopLevelWindows())
            {
                if (!WindowInfo.IsAppWindow(h, true))
                    continue;
                WindowInfo w = WindowInfo.Describe(h);
                PinnedApp owner = apps.Find(a => a.Matches(w));
                Console.WriteLine("  " + (w.ExePath ?? "?") + "  [" + (w.AppId ?? "-") + "]  -> " +
                    (owner != null ? owner.Name : "(not pinned)"));
            }
            return 0;
        }
    }
}
