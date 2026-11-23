// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace SevenZipThemeManager
{
    internal sealed class MainForm : Form
    {
        private readonly PictureBox detectedIcon = new PictureBox();
        private readonly Label detectedLabel = new Label();
        private readonly Button chooseFolder = new Button();
        private readonly ToolTip tips = new ToolTip();
        private readonly TabControl tabs = new TabControl();
        private readonly ThemePanel fileTypePanel;
        private readonly ThemePanel toolbarPanel;
        private readonly ThemePanel iconsPanel;
        private readonly Button restoreButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label adminLabel = new Label();
        private readonly Image foundImage = StatusImage(true);
        private readonly Image notFoundImage = StatusImage(false);
        private string sevenZipFolder;

        public MainForm()
        {
            Text = Program.Title + " " + Program.Version;
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadAppIcon();

            fileTypePanel = new ThemePanel(ThemeKind.FileType, "Apply file type icons");
            toolbarPanel = new ThemePanel(ThemeKind.Toolbar, "Apply toolbar buttons");
            iconsPanel = new ThemePanel(ThemeKind.Icons, "Apply program icons");
            foreach (ThemePanel panel in new ThemePanel[] { fileTypePanel, toolbarPanel, iconsPanel })
            {
                ThemePanel current = panel;
                current.ApplyClicked += delegate { Apply(current.Kind, current.Selected); };
            }

            // 7-Zip status on top, then one tab per theme kind, then the restore button and status
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8, 6, 8, 6);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(BuildDetectionRow(), 0, 0);

            tabs.Dock = DockStyle.Fill;
            AddTab("File types", fileTypePanel);
            AddTab("Toolbar", toolbarPanel);
            AddTab("Icons", iconsPanel);
            tabs.SelectedIndexChanged += delegate { CurrentPanel.FocusList(); };
            root.Controls.Add(tabs, 0, 1);

            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.ColumnCount = 3;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            restoreButton.Text = "Restore saved 7-Zip files";
            restoreButton.AutoSize = true;
            restoreButton.Click += delegate { Restore(); };
            statusLabel.AutoSize = true;
            statusLabel.Anchor = AnchorStyles.Left;
            adminLabel.AutoSize = true;
            adminLabel.Anchor = AnchorStyles.Right;
            adminLabel.ForeColor = SystemColors.GrayText;
            bottom.Controls.Add(restoreButton, 0, 0);
            bottom.Controls.Add(statusLabel, 1, 0);
            bottom.Controls.Add(adminLabel, 2, 0);
            root.Controls.Add(bottom, 0, 2);

            ClientSize = new Size(760, 420);
            Load += delegate { FitToPreview(); };

            string folder = Settings.SevenZipFolder;
            if (!SevenZipInstall.IsSevenZipFolder(folder))
                folder = SevenZipInstall.Find();
            sevenZipFolder = folder;
            RefreshSevenZip();
            ReloadThemes();
            Shown += delegate { CurrentPanel.FocusList(); };
        }

        private ThemePanel CurrentPanel
        {
            get { return (ThemePanel)tabs.SelectedTab.Controls[0]; }
        }

        private void AddTab(string caption, ThemePanel panel)
        {
            TabPage page = new TabPage(caption);
            page.Padding = new Padding(6);
            page.UseVisualStyleBackColor = true;
            page.Controls.Add(panel);
            tabs.TabPages.Add(page);
        }

        // Grows the window until a tab page holds its preview at full size; extra width goes to the list
        private void FitToPreview()
        {
            TabPage page = tabs.TabPages[0];
            Size wanted = fileTypePanel.PreferredContentSize;
            int growWidth = Math.Max(0, wanted.Width + page.Padding.Horizontal - page.ClientSize.Width);
            int growHeight = wanted.Height + page.Padding.Vertical - page.ClientSize.Height;
            ClientSize = new Size(ClientSize.Width + growWidth, ClientSize.Height + growHeight);
            MinimumSize = Size;
            MaximumSize = new Size(int.MaxValue, Height);
            CenterToScreen();
        }

        // The palette icon of the original 7-Zip Theme Manager, embedded so the title bar gets a sharp 16x16
        private static Icon LoadAppIcon()
        {
            using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("SevenZipThemeManager.app.ico"))
                return stream == null ? null : new Icon(stream);
        }

        // "[green tick or red cross] 7-Zip 26.02 detected [folder button]"
        private Control BuildDetectionRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.AutoSize = true;
            row.WrapContents = false;
            row.Margin = new Padding(0, 0, 0, 4);

            detectedIcon.Size = new Size(20, 20);
            detectedIcon.Margin = new Padding(3, 5, 4, 3);
            detectedLabel.AutoSize = true;
            detectedLabel.Margin = new Padding(0, 8, 6, 3);
            detectedLabel.Font = new Font(Font.FontFamily, Font.Size + 1f);

            chooseFolder.Size = new Size(30, 28);
            chooseFolder.Image = FolderImage();
            if (chooseFolder.Image == null)
                chooseFolder.Text = "...";
            chooseFolder.Click += delegate { ChooseFolder(); };
            tips.SetToolTip(chooseFolder, "Choose the 7-Zip folder (installed or portable)");

            row.Controls.Add(detectedIcon);
            row.Controls.Add(detectedLabel);
            row.Controls.Add(chooseFolder);
            return row;
        }

        private void ChooseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that contains 7z.dll and 7zFM.exe (installed or portable 7-Zip).";
                dialog.ShowNewFolderButton = false;
                if (sevenZipFolder != null && Directory.Exists(sevenZipFolder))
                    dialog.SelectedPath = sevenZipFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string folder = dialog.SelectedPath.TrimEnd('\\');
                if (!SevenZipInstall.IsSevenZipFolder(folder))
                {
                    MessageBox.Show(this, "There is no 7-Zip in " + folder + ".\r\nThe folder needs 7z.dll and 7zFM.exe.",
                        Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                sevenZipFolder = folder;
                RefreshSevenZip();
            }
        }

        private bool SevenZipFound
        {
            get { return SevenZipInstall.IsSevenZipFolder(sevenZipFolder); }
        }

        private void RefreshSevenZip()
        {
            bool found = SevenZipFound;
            detectedIcon.Image = found ? foundImage : notFoundImage;
            if (found)
            {
                string version = SevenZipInstall.Version(sevenZipFolder);
                detectedLabel.Text = "7-Zip " + (version ?? "") + (version == null ? "" : " ") + "detected";
                tips.SetToolTip(detectedLabel, sevenZipFolder);
                tips.SetToolTip(detectedIcon, sevenZipFolder);
                adminLabel.Text = SevenZipInstall.CanWrite(sevenZipFolder) ? "" : "Changes will ask for administrator permission.";
                Settings.SevenZipFolder = sevenZipFolder;
                Settings.Save();
            }
            else
            {
                detectedLabel.Text = "7-Zip NOT detected";
                tips.SetToolTip(detectedLabel, "Click the folder button to choose where 7-Zip is.");
                tips.SetToolTip(detectedIcon, null);
                adminLabel.Text = "";
            }
            fileTypePanel.SetFileTypeSupport(found ? FileTypeSupport.Read(sevenZipFolder) : null,
                found ? SevenZipInstall.Version(sevenZipFolder) : null);
            restoreButton.Enabled = found && Backup.Exists(sevenZipFolder);
            foreach (ThemePanel panel in new ThemePanel[] { fileTypePanel, toolbarPanel, iconsPanel })
                panel.CanApply = found;
        }

        private void ReloadThemes()
        {
            string root = ThemeStore.Root;
            fileTypePanel.SetThemes(ThemeStore.Load(root, ThemeKind.FileType));
            toolbarPanel.SetThemes(ThemeStore.Load(root, ThemeKind.Toolbar));
            iconsPanel.SetThemes(ThemeStore.Load(root, ThemeKind.Icons));
            statusLabel.Text = ThemeStore.LooksLikeThemesRoot(root) ? "" : "No FileType, ToolBar or Icons folder next to this program.";
        }

        private void Apply(ThemeKind kind, Theme theme)
        {
            if (theme == null || !SevenZipFound)
                return;
            Options options = new Options();
            options.SevenZipFolder = sevenZipFolder;
            if (kind == ThemeKind.FileType) options.FileType = theme.FolderName;
            else if (kind == ThemeKind.Toolbar) options.Toolbar = theme.FolderName;
            else options.Icons = theme.FolderName;

            string report = RunOptions(options);
            if (report == null)
                return;
            if (kind == ThemeKind.Toolbar)
                report += "\r\nRestart 7-Zip File Manager to see the new buttons.";
            else if (kind == ThemeKind.Icons)
                report += "\r\nWindows may keep showing the old program icons until its icon cache is refreshed.";
            statusLabel.Text = "Applied \"" + theme.Name + "\".";
            MessageBox.Show(this, report.Trim(), Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshSevenZip();
        }

        private void Restore()
        {
            if (MessageBox.Show(this, "Put back the 7-Zip files saved before 7-Zip Theme Manager first changed them?",
                    Program.Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            Options options = new Options();
            options.SevenZipFolder = sevenZipFolder;
            options.Restore = true;
            string report = RunOptions(options);
            if (report != null)
                MessageBox.Show(this, report.Trim(), Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshSevenZip();
        }

        // Runs here when the 7-Zip folder is writable, otherwise in an elevated copy of this program
        private string RunOptions(Options options)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                if (!SevenZipInstall.CanWrite(options.SevenZipFolder) && Program.CanElevate)
                    return Program.RunElevated(options);
                return Program.Run(options);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Program.DescribeError(ex), Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // A filled circle with a white tick (found) or cross (not found)
        private static Image StatusImage(bool found)
        {
            Bitmap image = new Bitmap(20, 20);
            using (Graphics g = Graphics.FromImage(image))
            using (Brush fill = new SolidBrush(found ? Color.FromArgb(16, 124, 16) : Color.FromArgb(196, 43, 28)))
            using (Pen mark = new Pen(Color.White, 2.2f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(fill, 0.5f, 0.5f, 18.5f, 18.5f);
                mark.StartCap = LineCap.Round;
                mark.EndCap = LineCap.Round;
                if (found)
                {
                    mark.LineJoin = LineJoin.Round;
                    g.DrawLines(mark, new PointF[] { new PointF(5.5f, 10.2f), new PointF(8.6f, 13.2f), new PointF(14.4f, 6.8f) });
                }
                else
                {
                    g.DrawLine(mark, 6.5f, 6.5f, 13.3f, 13.3f);
                    g.DrawLine(mark, 13.3f, 6.5f, 6.5f, 13.3f);
                }
            }
            return image;
        }

        // The standard closed-folder icon from shell32.dll (same index from Windows XP to 11)
        private static Image FolderImage()
        {
            IntPtr[] small = new IntPtr[1];
            if (NativeMethods.ExtractIconEx("shell32.dll", 3, null, small, 1) == 0 || small[0] == IntPtr.Zero)
                return null;
            try
            {
                using (Icon icon = Icon.FromHandle(small[0]))
                    return icon.ToBitmap();
            }
            finally
            {
                NativeMethods.DestroyIcon(small[0]);
            }
        }
    }

    // Theme list with a preview, details and an Apply button
    internal sealed class ThemePanel : Panel
    {
        public readonly ThemeKind Kind;
        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private readonly ListBox list = new ListBox();
        private readonly PictureBox preview = new PictureBox();
        private readonly Label details = new Label();
        private readonly LinkLabel website = new LinkLabel();
        private readonly Button apply = new Button();
        private readonly GroupBox coverageBox = new GroupBox();
        private readonly Label coverage = new Label();
        private FileTypeSupport fileTypeSupport;
        private string sevenZipVersion;
        private bool canApply;

        public event EventHandler ApplyClicked;

        // The size of preview.jpg in 7zTM themes; 7zTM shows it unscaled, and so does this
        public static readonly Size PreviewSize = new Size(430, 280);

        public ThemePanel(ThemeKind kind, string applyText)
        {
            Kind = kind;
            Dock = DockStyle.Fill;

            // Left: theme list, 7-Zip comparison (file types only), details, Apply. Right: the preview.
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 5;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            list.Dock = DockStyle.Fill;
            list.IntegralHeight = false;
            list.MinimumSize = new Size(200, 80);
            list.SelectedIndexChanged += delegate { ShowSelected(); };
            list.DoubleClick += delegate { if (apply.Enabled) OnApply(); };

            // The border sits outside the picture, so the picture area is exactly PreviewSize
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.BackColor = SystemColors.Window;
            preview.ClientSize = PreviewSize;
            preview.Margin = new Padding(8, 3, 0, 0);
            preview.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            details.AutoSize = true;
            details.Margin = new Padding(3, 6, 3, 0);
            website.AutoSize = true;
            website.LinkClicked += delegate
            {
                string url = website.Text;
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    Process.Start(url);
            };

            apply.Text = applyText;
            apply.AutoSize = true;
            apply.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            apply.Margin = new Padding(3, 6, 3, 0);
            apply.Click += delegate { OnApply(); };

            // Which of the installed 7-Zip's file types the selected theme covers, worked out from
            // 7z.dll itself so theme.ini never needs to list them
            coverageBox.AutoSize = true;
            coverageBox.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            coverageBox.Dock = DockStyle.Fill;
            coverageBox.Margin = new Padding(3, 6, 3, 0);
            coverageBox.Padding = new Padding(6, 3, 6, 6);
            coverage.AutoSize = true;
            coverage.Dock = DockStyle.Top;
            coverageBox.Controls.Add(coverage);
            coverageBox.Visible = kind == ThemeKind.FileType;

            layout.Controls.Add(list, 0, 0);
            layout.Controls.Add(coverageBox, 0, 1);
            layout.Controls.Add(details, 0, 2);
            layout.Controls.Add(website, 0, 3);
            layout.Controls.Add(apply, 0, 4);
            layout.Controls.Add(preview, 1, 0);
            layout.SetRowSpan(preview, 5);
            list.SizeChanged += delegate
            {
                details.MaximumSize = new Size(Math.Max(100, list.Width), 0);
                website.MaximumSize = details.MaximumSize;
                coverage.MaximumSize = new Size(Math.Max(80, list.Width - coverageBox.Padding.Horizontal - 2), 0);
            };
        }

        public Theme Selected { get { return list.SelectedItem as Theme; } }

        // Room needed to show the preview at full size next to a usable list
        public Size PreferredContentSize
        {
            get { return layout.GetPreferredSize(Size.Empty); }
        }

        public void FocusList()
        {
            list.Focus();
        }

        public bool CanApply
        {
            set { canApply = value; apply.Enabled = canApply && Selected != null; }
        }

        public void SetThemes(List<Theme> themes)
        {
            string previous = Selected == null ? null : Selected.Folder;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (Theme theme in themes)
                list.Items.Add(theme);
            list.EndUpdate();

            for (int i = 0; i < themes.Count; i++)
            {
                if (themes[i].Folder == previous)
                    list.SelectedIndex = i;
            }
            if (list.SelectedIndex < 0 && themes.Count > 0)
                list.SelectedIndex = 0;
            ShowSelected();
        }

        // The installed 7-Zip's file types, or null when there is no 7-Zip to compare with
        public void SetFileTypeSupport(FileTypeSupport support, string version)
        {
            fileTypeSupport = support;
            sevenZipVersion = version;
            ShowCoverage(Selected);
        }

        private void ShowCoverage(Theme theme)
        {
            if (Kind != ThemeKind.FileType)
                return;
            string sevenZip = "7-Zip" + (string.IsNullOrEmpty(sevenZipVersion) ? "" : " " + sevenZipVersion);
            coverageBox.Text = fileTypeSupport == null ? "File types" : "Compared with " + sevenZip;
            coverage.ForeColor = SystemColors.ControlText;
            if (fileTypeSupport == null)
            {
                coverage.ForeColor = SystemColors.GrayText;
                coverage.Text = "Choose the 7-Zip folder to see which of its file types this theme covers.";
                return;
            }
            if (theme == null)
            {
                coverage.Text = "";
                return;
            }

            FileTypeCoverage result = fileTypeSupport.Compare(theme);
            List<string> lines = new List<string>();
            int has = result.Total - result.Missing.Count;
            if (result.Missing.Count == 0)
                lines.Add("Has icons for all " + result.Total + " file types.");
            else
            {
                lines.Add("Has icons for " + has + " of " + result.Total + " file types.");
                lines.Add("Missing (7-Zip keeps its current icon): " + string.Join(", ", result.Missing.ToArray()));
            }
            if (result.Extra.Count > 0)
                lines.Add("Extra (not used by this 7-Zip): " + string.Join(", ", result.Extra.ToArray()));
            coverage.Text = string.Join(Environment.NewLine, lines.ToArray());
        }

        private void OnApply()
        {
            if (ApplyClicked != null)
                ApplyClicked(this, EventArgs.Empty);
        }

        private void ShowSelected()
        {
            Theme theme = Selected;
            SetPreview(theme == null ? null : LoadPreview(theme));
            ShowCoverage(theme);
            apply.Enabled = canApply && theme != null;
            if (theme == null)
            {
                details.Text = "";
                website.Text = "";
                return;
            }

            List<string> lines = new List<string>();
            if (!string.IsNullOrEmpty(theme.Author)) lines.Add("Author: " + theme.Author);
            if (!string.IsNullOrEmpty(theme.Licence)) lines.Add("Licence: " + theme.Licence);
            if (theme.Missing.Count > 0)
                lines.Add("Not in this theme (7-Zip keeps its current picture): " + string.Join(", ", theme.Missing.ToArray()));
            details.Text = string.Join("\r\n", lines.ToArray());
            website.Text = theme.Website ?? "";
        }

        private void SetPreview(Image image)
        {
            Image old = preview.Image;
            bool fits = image == null || (image.Width <= PreviewSize.Width && image.Height <= PreviewSize.Height);
            preview.SizeMode = fits ? PictureBoxSizeMode.CenterImage : PictureBoxSizeMode.Zoom;
            preview.Image = image;
            if (old != null)
                old.Dispose();
        }

        // preview.png or preview.jpg when the theme has one, otherwise a sheet of its pictures
        private static Image LoadPreview(Theme theme)
        {
            try
            {
                if (theme.PreviewPath != null)
                {
                    using (FileStream stream = File.OpenRead(theme.PreviewPath))
                    using (Image image = Image.FromStream(stream))
                        return new Bitmap(image);
                }

                if (theme.Kind == ThemeKind.FileType) return FileTypeSheet(theme);
                if (theme.Kind == ThemeKind.Toolbar) return ToolbarSheet(theme);
                return ProgramIconSheet(theme);
            }
            catch (Exception)
            {
                // A broken preview should never stop the theme from being listed
                return null;
            }
        }

        // All file type icons at 48x48, seven to a row, labelled with their extension
        private static Image FileTypeSheet(Theme theme)
        {
            Bitmap sheet = new Bitmap(PreviewSize.Width, PreviewSize.Height);
            int shown = 0;
            using (Graphics g = Graphics.FromImage(sheet))
            using (Font font = new Font(SystemFonts.MessageBoxFont.FontFamily, 7.5f))
            using (StringFormat centre = new StringFormat())
            {
                g.Clear(SystemColors.Window);
                centre.Alignment = StringAlignment.Center;
                for (int slot = 0; slot < ThemeLayout.FileTypeSlots.Length; slot++)
                {
                    string path = theme.FindFileTypeIcon(slot);
                    if (path == null)
                        continue;
                    int x = 5 + (slot % 7) * 60, y = 5 + (slot / 7) * 68;
                    using (Icon icon = new Icon(path, 48, 48))
                        g.DrawIcon(icon, new Rectangle(x + 6, y, 48, 48));
                    g.DrawString(Path.GetFileNameWithoutExtension(path), font, SystemBrushes.GrayText, new RectangleF(x, y + 50, 60, 14), centre);
                    shown++;
                }
            }
            if (shown > 0)
                return sheet;
            sheet.Dispose();
            return null;
        }

        // GDI+ ignores the alpha channel of 32-bit BMP files, which 7-Zip uses for toolbar buttons,
        // so those are read by hand. Other formats go through GDI+ as usual.
        private static Image LoadToolbarBitmap(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            int bitCount = file.Length >= 54 ? BitConverter.ToUInt16(file, 28) : 0;
            int compression = file.Length >= 54 ? BitConverter.ToInt32(file, 30) : -1;
            if (bitCount != 32 || (compression != 0 && compression != 3))
            {
                using (MemoryStream stream = new MemoryStream(file))
                using (Image image = Image.FromStream(stream))
                    return new Bitmap(image);
            }

            int pixels = BitConverter.ToInt32(file, 10);
            int width = BitConverter.ToInt32(file, 18);
            int height = BitConverter.ToInt32(file, 22);
            bool bottomUp = height > 0;
            height = Math.Abs(height);
            bool anyAlpha = false;
            for (int i = pixels + 3; i < pixels + width * height * 4 && i < file.Length; i += 4)
            {
                if (file[i] != 0) { anyAlpha = true; break; }
            }

            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++)
            {
                int row = pixels + (bottomUp ? height - 1 - y : y) * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int i = row + x * 4;
                    int alpha = anyAlpha ? file[i + 3] : 255;
                    bitmap.SetPixel(x, y, Color.FromArgb(alpha, file[i + 2], file[i + 1], file[i]));
                }
            }
            return bitmap;
        }

        // Each program icon at 48x48 with the 7-Zip file it belongs to
        private static Image ProgramIconSheet(Theme theme)
        {
            Bitmap sheet = new Bitmap(PreviewSize.Width, PreviewSize.Height);
            int shown = 0;
            using (Graphics g = Graphics.FromImage(sheet))
            using (Font font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8f))
            using (StringFormat centre = new StringFormat())
            {
                g.Clear(SystemColors.Window);
                centre.Alignment = StringAlignment.Center;
                for (int i = 0; i < ThemeLayout.ProgramIcons.Length; i++)
                {
                    string path = theme.FindProgramIcon(i);
                    if (path == null)
                        continue;
                    int x = 20 + (i % 3) * 135, y = 40 + (i / 3) * 110;
                    using (Icon icon = new Icon(path, 48, 48))
                        g.DrawIcon(icon, new Rectangle(x + 31, y, 48, 48));
                    g.DrawString(ThemeLayout.ProgramIcons[i][1], font, SystemBrushes.GrayText, new RectangleF(x, y + 54, 110, 16), centre);
                    shown++;
                }
            }
            if (shown > 0)
                return sheet;
            sheet.Dispose();
            return null;
        }

        // The toolbar as 7-Zip File Manager draws it: large buttons, then small ones
        private static Image ToolbarSheet(Theme theme)
        {
            Bitmap sheet = new Bitmap(PreviewSize.Width, PreviewSize.Height);
            int shown = 0;
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.Clear(SystemColors.Control);
                string[] folders = { ThemeLayout.LargeToolbarFolder, ThemeLayout.SmallToolbarFolder };
                int[] tops = { 70, 170 };
                for (int row = 0; row < folders.Length; row++)
                {
                    for (int b = 0; b < ThemeLayout.ToolbarButtons.Length; b++)
                    {
                        string path = theme.FindToolbarBitmap(folders[row], ThemeLayout.ToolbarButtons[b]);
                        if (path == null)
                            continue;
                        using (Image image = LoadToolbarBitmap(path))
                            g.DrawImage(image, 15 + b * 58 + (48 - image.Width) / 2, tops[row], image.Width, image.Height);
                        shown++;
                    }
                }
            }
            if (shown > 0)
                return sheet;
            sheet.Dispose();
            return null;
        }
    }
}
