using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenTaskbar
{
    /// <summary>
    /// Launches every enabled pinned app, then watches for their windows and maximizes them.
    /// Many apps ignore the "start maximized" hint and restore their last size, so the windows
    /// are maximized after they appear rather than relying on the launch flag alone.
    /// </summary>
    internal sealed class LaunchSession : ApplicationContext
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan Settle = TimeSpan.FromSeconds(4);

        private readonly List<PinnedApp> launched = new List<PinnedApp>();
        private readonly HashSet<PinnedApp> satisfied = new HashSet<PinnedApp>();
        private readonly HashSet<IntPtr> preexisting = new HashSet<IntPtr>();
        private readonly HashSet<IntPtr> ignored = new HashSet<IntPtr>();
        private readonly Dictionary<IntPtr, DateTime> maximized = new Dictionary<IntPtr, DateTime>();
        private readonly uint selfPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        private readonly Timer timer = new Timer();
        private DateTime startUtc;
        private DateTime lastMaximizeUtc;

        public LaunchSession(AppSettings settings)
        {
            List<PinnedApp> apps = PinnedApps.Load();
            Log.Write("Found " + apps.Count + " pinned app(s).");

            var running = new List<WindowInfo>();
            foreach (IntPtr h in WindowInfo.TopLevelWindows())
            {
                if (Native.IsWindowVisible(h))
                    preexisting.Add(h);
                if (settings.SkipRunning && WindowInfo.IsAppWindow(h, true))
                    running.Add(WindowInfo.Describe(h));
            }

            startUtc = DateTime.UtcNow;
            lastMaximizeUtc = startUtc;
            foreach (PinnedApp app in apps)
            {
                if (settings.Excluded.Contains(app.Key))
                {
                    Log.Write("Skipped (turned off in settings): " + app.Name);
                    continue;
                }
                if (running.Exists(w => app.Matches(w)))
                {
                    Log.Write("Skipped (already running): " + app.Name);
                    continue;
                }
                if (Launch(app))
                    launched.Add(app);
            }

            if (!settings.Maximize || launched.Count == 0)
            {
                // Exit from the message loop rather than the constructor.
                timer.Interval = 1;
                timer.Tick += (s, e) => ExitThread();
            }
            else
            {
                timer.Interval = 250;
                timer.Tick += (s, e) => Poll();
            }
            timer.Start();
        }

        private static bool Launch(PinnedApp app)
        {
            IntPtr pidl = Native.AllocIdList(app.IdList);
            try
            {
                var info = new SHELLEXECUTEINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO));
                info.fMask = Native.SEE_MASK_INVOKEIDLIST | Native.SEE_MASK_NOASYNC | Native.SEE_MASK_FLAG_NO_UI;
                info.lpIDList = pidl;
                info.nShow = Native.SW_SHOWMAXIMIZED;
                bool ok = Native.ShellExecuteEx(ref info);
                Log.Write((ok ? "Launched: " : "Failed to launch (error " + Marshal.GetLastWin32Error() + "): ") + app.Name);
                return ok;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        private void Poll()
        {
            DateTime now = DateTime.UtcNow;

            foreach (IntPtr h in WindowInfo.TopLevelWindows())
            {
                if (preexisting.Contains(h) || ignored.Contains(h) || maximized.ContainsKey(h))
                    continue;
                if (!WindowInfo.IsAppWindow(h, false))
                    continue; // may still be loading or cloaked; check again next tick

                WindowInfo w = WindowInfo.Describe(h);
                PinnedApp owner = launched.Find(a => a.Matches(w));
                // Apps started through a launcher stub show their window from a different
                // executable, so also accept any window from a process started after we launched.
                bool startedByUs = w.ProcessStartUtc >= startUtc.AddSeconds(-1);
                if (w.ProcessId == selfPid || (owner == null && !startedByUs))
                {
                    ignored.Add(h);
                    continue;
                }

                Native.ShowWindowAsync(h, Native.SW_MAXIMIZE);
                maximized[h] = now;
                lastMaximizeUtc = now;
                if (owner != null)
                    satisfied.Add(owner);
                Log.Write("Maximized window of " + (owner != null ? owner.Name : w.ExePath));
            }

            // Some apps restore their saved size just after first showing; maximize those again.
            foreach (KeyValuePair<IntPtr, DateTime> entry in maximized)
            {
                IntPtr h = entry.Key;
                if (now - entry.Value < Settle && Native.IsWindow(h) && !Native.IsZoomed(h) && !Native.IsIconic(h))
                    Native.ShowWindowAsync(h, Native.SW_MAXIMIZE);
            }

            bool allDone = satisfied.Count >= launched.Count && now - lastMaximizeUtc > Settle;
            if (allDone || now - startUtc > Timeout)
            {
                Log.Write(allDone ? "All launched apps maximized." : "Stopped watching after timeout.");
                ExitThread();
            }
        }

        protected override void ExitThreadCore()
        {
            timer.Stop();
            timer.Dispose();
            base.ExitThreadCore();
        }
    }
}
