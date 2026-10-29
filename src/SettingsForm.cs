using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenTaskbar
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings settings = AppSettings.Load();
        private readonly ListView list = new ListView();
        private readonly ImageList icons = new ImageList();
        private readonly CheckBox maximize = new CheckBox();
        private readonly CheckBox skipRunning = new CheckBox();
        private readonly CheckBox startup = new CheckBox();

        public SettingsForm()
        {
            Text = "Open Taskbar Settings";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(440, 520);
            MinimumSize = new Size(380, 440);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(MakeLabel("These are the apps pinned to your taskbar. Clicking Open Taskbar opens all checked apps."));

            icons.ColorDepth = ColorDepth.Depth32Bit;
            icons.ImageSize = SystemInformation.SmallIconSize;
            list.View = View.Details;
            list.CheckBoxes = true;
            list.FullRowSelect = true;
            list.HeaderStyle = ColumnHeaderStyle.None;
            list.SmallImageList = icons;
            list.Dock = DockStyle.Fill;
            list.Margin = new Padding(0, 6, 0, 10);
            list.Columns.Add("App");
            list.Resize += (s, e) => list.Columns[0].Width = list.ClientSize.Width - 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(list);

            maximize.Text = "Open apps maximized";
            maximize.Checked = settings.Maximize;
            skipRunning.Text = "Don't reopen apps that are already open";
            skipRunning.Checked = settings.SkipRunning;
            startup.Text = "Start in the system tray when I sign in to Windows";
            startup.Checked = StartupRegistration.IsEnabled();

            foreach (CheckBox box in new[] { maximize, skipRunning, startup })
            {
                box.AutoSize = true;
                box.Margin = new Padding(0, 2, 0, 2);
                root.Controls.Add(box);
            }

            var hint = MakeLabel("Tip: right-click the tray icon, or hold Shift while clicking Open Taskbar, to come back to these settings.");
            hint.ForeColor = SystemColors.GrayText;
            hint.Margin = new Padding(0, 10, 0, 10);
            root.Controls.Add(hint);

            var buttons = new TableLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.ColumnCount = 2;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.Margin = Padding.Empty;

            var left = new FlowLayoutPanel();
            left.AutoSize = true;
            left.WrapContents = false;
            left.Margin = Padding.Empty;
            Button openNow = MakeButton("Open all now");
            openNow.Click += (s, e) => { Save(); TrayContext.OpenAll(); Close(); };
            Button refresh = MakeButton("Refresh");
            refresh.Click += (s, e) => LoadPins();
            left.Controls.Add(openNow);
            left.Controls.Add(refresh);

            var right = new FlowLayoutPanel();
            right.AutoSize = true;
            right.WrapContents = false;
            right.Margin = Padding.Empty;
            Button save = MakeButton("Save");
            save.Click += (s, e) => { Save(); Close(); };
            Button cancel = MakeButton("Cancel");
            cancel.Click += (s, e) => Close();
            right.Controls.Add(save);
            right.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;

            buttons.Controls.Add(left, 0, 0);
            buttons.Controls.Add(right, 1, 0);
            root.Controls.Add(buttons);

            LoadPins();
        }

        private static Label MakeLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.MaximumSize = new Size(400, 0);
            label.Margin = Padding.Empty;
            return label;
        }

        private static Button MakeButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(80, 0);
            button.Padding = new Padding(6, 2, 6, 2);
            button.Margin = new Padding(0, 0, 6, 0);
            return button;
        }

        private void LoadPins()
        {
            list.BeginUpdate();
            list.Items.Clear();
            icons.Images.Clear();
            foreach (PinnedApp app in PinnedApps.Load())
            {
                var item = new ListViewItem(app.Name);
                item.Tag = app;
                item.Checked = !settings.Excluded.Contains(app.Key);
                Bitmap icon = LoadIcon(app);
                if (icon != null)
                {
                    icons.Images.Add(icon);
                    item.ImageIndex = icons.Images.Count - 1;
                }
                list.Items.Add(item);
            }
            if (list.Items.Count == 0)
                list.Items.Add(new ListViewItem("(No pinned apps found)"));
            list.EndUpdate();
        }

        private static Bitmap LoadIcon(PinnedApp app)
        {
            IntPtr pidl = Native.AllocIdList(app.IdList);
            try
            {
                return LoadShellImage(pidl) ?? LoadFileIcon(pidl);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        /// <summary>The icon the taskbar itself shows; also covers Store app logos.</summary>
        private static Bitmap LoadShellImage(IntPtr pidl)
        {
            IShellItemImageFactory factory = null;
            IntPtr hbitmap = IntPtr.Zero;
            try
            {
                Guid iid = Native.IID_IShellItemImageFactory;
                if (Native.SHCreateImageFactoryFromIDList(pidl, ref iid, out factory) != 0 || factory == null)
                    return null;
                var size = new SIZE();
                size.cx = size.cy = SystemInformation.SmallIconSize.Width;
                if (factory.GetImage(size, Native.SIIGBF_ICONONLY, out hbitmap) != 0 || hbitmap == IntPtr.Zero)
                    return null;

                // FromHbitmap drops the alpha channel; re-read the same pixels as premultiplied ARGB.
                using (Bitmap opaque = Image.FromHbitmap(hbitmap))
                {
                    var rect = new Rectangle(0, 0, opaque.Width, opaque.Height);
                    var data = opaque.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, opaque.PixelFormat);
                    try
                    {
                        using (var alpha = new Bitmap(data.Width, data.Height, data.Stride,
                            System.Drawing.Imaging.PixelFormat.Format32bppPArgb, data.Scan0))
                        {
                            return new Bitmap(alpha);
                        }
                    }
                    finally
                    {
                        opaque.UnlockBits(data);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (hbitmap != IntPtr.Zero)
                    Native.DeleteObject(hbitmap);
                if (factory != null)
                    Marshal.ReleaseComObject(factory);
            }
        }

        private static Bitmap LoadFileIcon(IntPtr pidl)
        {
            var info = new SHFILEINFO();
            Native.SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf(info),
                Native.SHGFI_PIDL | Native.SHGFI_ICON | Native.SHGFI_SMALLICON);
            if (info.hIcon == IntPtr.Zero)
                return null;
            using (Icon icon = Icon.FromHandle(info.hIcon))
            {
                Bitmap bitmap = icon.ToBitmap();
                Native.DestroyIcon(info.hIcon);
                return bitmap;
            }
        }

        private void Save()
        {
            settings.Maximize = maximize.Checked;
            settings.SkipRunning = skipRunning.Checked;
            settings.Excluded.Clear();
            foreach (ListViewItem item in list.Items)
            {
                var app = item.Tag as PinnedApp;
                if (app != null && !item.Checked)
                    settings.Excluded.Add(app.Key);
            }
            try
            {
                settings.Save();
                StartupRegistration.SetEnabled(startup.Checked, Application.ExecutablePath);
                if (startup.Checked)
                    Program.StartTray(); // show it now rather than at the next sign-in
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save settings: " + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
