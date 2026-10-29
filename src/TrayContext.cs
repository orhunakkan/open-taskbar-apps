using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace OpenTaskbar
{
    /// <summary>
    /// Runs silently in the notification area. Left-click opens all pinned apps;
    /// the right-click menu also offers settings and exit.
    /// </summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon tray = new NotifyIcon();
        private SettingsForm settingsForm;

        public TrayContext()
        {
            var menu = new ContextMenuStrip();
            ToolStripItem openAll = menu.Items.Add("Open all apps", null, (s, e) => OpenAll());
            openAll.Font = new Font(openAll.Font, FontStyle.Bold);
            menu.Items.Add("Settings...", null, (s, e) => ShowSettings());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            tray.ContextMenuStrip = menu;
            tray.Icon = LoadSmallIcon();
            tray.Text = "Open Taskbar - click to open all pinned apps";
            tray.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    OpenAll();
            };
            tray.Visible = true;
        }

        public static void OpenAll()
        {
            // A separate process does the launching, so the tray stays idle and a click
            // while a run is in progress is ignored by that process's own single-instance check.
            Process.Start(Application.ExecutablePath);
        }

        private void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm();
                settingsForm.Show();
            }
            else
            {
                if (settingsForm.WindowState == FormWindowState.Minimized)
                    settingsForm.WindowState = FormWindowState.Normal;
                settingsForm.Activate();
            }
        }

        /// <summary>The exe's embedded icon at the notification area's size, rather than a scaled-down 32 px one.</summary>
        private static Icon LoadSmallIcon()
        {
            Size size = SystemInformation.SmallIconSize;
            try
            {
                IntPtr hicon;
                if (Native.LoadIconWithScaleDown(Native.GetModuleHandle(null), (IntPtr)Native.AppIconResourceId,
                        size.Width, size.Height, out hicon) == 0 && hicon != IntPtr.Zero)
                    return Icon.FromHandle(hicon);
            }
            catch (EntryPointNotFoundException)
            {
                // Common Controls v6 not active; fall through.
            }
            return new Icon(Icon.ExtractAssociatedIcon(Application.ExecutablePath), size);
        }

        protected override void ExitThreadCore()
        {
            tray.Visible = false;
            tray.Dispose();
            if (settingsForm != null && !settingsForm.IsDisposed)
                settingsForm.Close();
            base.ExitThreadCore();
        }
    }
}
