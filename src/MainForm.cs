// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SevenZipThemeManager
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox sevenZipBox = new TextBox();
        private readonly TextBox themesBox = new TextBox();
        private readonly Label versionLabel = new Label();
        private readonly ThemePanel fileTypePanel;
        private readonly ThemePanel toolbarPanel;
        private readonly Button restoreButton = new Button();
        private readonly Label statusLabel = new Label();

        public MainForm()
        {
            Text = Program.Title;
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(760, 520);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (ArgumentException) { }

            fileTypePanel = new ThemePanel("File type icons", "Apply file type icons");
            toolbarPanel = new ThemePanel("Toolbar buttons", "Apply toolbar buttons");
            fileTypePanel.ApplyClicked += delegate { Apply(ThemeKind.FileType, fileTypePanel.Selected); };
            toolbarPanel.ApplyClicked += delegate { Apply(ThemeKind.Toolbar, toolbarPanel.Selected); };

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(BuildFolderRows(), 0, 0);

            TableLayoutPanel themes = new TableLayoutPanel();
            themes.Dock = DockStyle.Fill;
            themes.ColumnCount = 2;
            themes.RowCount = 1;
            themes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            themes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            themes.Controls.Add(fileTypePanel, 0, 0);
            themes.Controls.Add(toolbarPanel, 1, 0);
            root.Controls.Add(themes, 0, 1);

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.WrapContents = false;
            restoreButton.Text = "Restore saved 7-Zip files";
            restoreButton.AutoSize = true;
            restoreButton.Click += delegate { Restore(); };
            statusLabel.AutoSize = true;
            statusLabel.Margin = new Padding(10, 8, 0, 0);
            bottom.Controls.Add(restoreButton);
            bottom.Controls.Add(statusLabel);
            root.Controls.Add(bottom, 0, 2);

            string sevenZip = Settings.SevenZipFolder;
            if (!SevenZipInstall.IsSevenZipFolder(sevenZip))
                sevenZip = SevenZipInstall.Find();
            string themesRoot = Settings.ThemesFolder;
            if (string.IsNullOrEmpty(themesRoot) || !ThemeStore.LooksLikeThemesRoot(themesRoot))
                themesRoot = ThemeStore.FindThemesRoot(Application.StartupPath);

            sevenZipBox.Text = sevenZip ?? "";
            themesBox.Text = themesRoot ?? "";
            sevenZipBox.TextChanged += delegate { RefreshSevenZip(); };
            themesBox.TextChanged += delegate { ReloadThemes(); };
            RefreshSevenZip();
            ReloadThemes();
            Shown += delegate { fileTypePanel.FocusList(); };
        }

        private Control BuildFolderRows()
        {
            TableLayoutPanel rows = new TableLayoutPanel();
            rows.Dock = DockStyle.Fill;
            rows.AutoSize = true;
            rows.ColumnCount = 3;
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            AddFolderRow(rows, 0, "7-Zip folder:", sevenZipBox, "Choose the folder that contains 7z.dll and 7zFM.exe (installed or portable 7-Zip).");
            AddFolderRow(rows, 1, "Themes folder:", themesBox, "Choose the folder that contains the FileType and ToolBar theme folders.");

            versionLabel.AutoSize = true;
            versionLabel.ForeColor = SystemColors.GrayText;
            versionLabel.Margin = new Padding(3, 0, 3, 6);
            rows.Controls.Add(versionLabel, 1, 2);
            return rows;
        }

        private void AddFolderRow(TableLayoutPanel rows, int row, string caption, TextBox box, string browseDescription)
        {
            Label label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            box.Dock = DockStyle.Fill;

            Button browse = new Button();
            browse.Text = "Browse...";
            browse.AutoSize = true;
            browse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = browseDescription;
                    if (Directory.Exists(box.Text))
                        dialog.SelectedPath = box.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        box.Text = dialog.SelectedPath;
                }
            };

            rows.Controls.Add(label, 0, row);
            rows.Controls.Add(box, 1, row);
            rows.Controls.Add(browse, 2, row);
        }

        private string SevenZipFolder { get { return sevenZipBox.Text.Trim().TrimEnd('\\'); } }
        private string ThemesFolder { get { return themesBox.Text.Trim().TrimEnd('\\'); } }

        private void RefreshSevenZip()
        {
            bool found = SevenZipInstall.IsSevenZipFolder(SevenZipFolder);
            if (found)
            {
                string version = SevenZipInstall.Version(SevenZipFolder);
                versionLabel.Text = "7-Zip " + (version ?? "(unknown version)") + (SevenZipInstall.CanWrite(SevenZipFolder) ? "" : " - changes will ask for administrator permission");
                Settings.SevenZipFolder = SevenZipFolder;
                Settings.Save();
            }
            else
            {
                versionLabel.Text = "No 7-Zip found here (needs 7z.dll and 7zFM.exe).";
            }
            restoreButton.Enabled = found && Backup.Exists(SevenZipFolder);
            UpdateApplyButtons();
        }

        private void ReloadThemes()
        {
            string root = ThemesFolder;
            if (root.Length > 0 && ThemeStore.LooksLikeThemesRoot(root))
            {
                fileTypePanel.SetThemes(ThemeStore.Load(root, ThemeKind.FileType));
                toolbarPanel.SetThemes(ThemeStore.Load(root, ThemeKind.Toolbar));
                Settings.ThemesFolder = root;
                Settings.Save();
                statusLabel.Text = "";
            }
            else
            {
                fileTypePanel.SetThemes(new List<Theme>());
                toolbarPanel.SetThemes(new List<Theme>());
                statusLabel.Text = "No FileType or ToolBar folder found in the themes folder.";
            }
            UpdateApplyButtons();
        }

        private void UpdateApplyButtons()
        {
            bool found = SevenZipInstall.IsSevenZipFolder(SevenZipFolder);
            fileTypePanel.CanApply = found;
            toolbarPanel.CanApply = found;
        }

        private void Apply(ThemeKind kind, Theme theme)
        {
            if (theme == null)
                return;
            Options options = new Options();
            options.SevenZipFolder = SevenZipFolder;
            options.ThemesFolder = ThemesFolder;
            if (kind == ThemeKind.FileType) options.FileType = theme.FolderName;
            else options.Toolbar = theme.FolderName;

            string report = RunOptions(options);
            if (report == null)
                return;
            if (kind == ThemeKind.Toolbar)
                report += "\r\nRestart 7-Zip File Manager to see the new buttons.";
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
            options.SevenZipFolder = SevenZipFolder;
            options.Restore = true;
            string report = RunOptions(options);
            if (report != null)
                MessageBox.Show(this, report.Trim(), Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    }

    // Theme list with a preview, details and an Apply button
    internal sealed class ThemePanel : GroupBox
    {
        private readonly ListBox list = new ListBox();
        private readonly PictureBox preview = new PictureBox();
        private readonly Label details = new Label();
        private readonly LinkLabel website = new LinkLabel();
        private readonly Button apply = new Button();
        private bool canApply;

        public event EventHandler ApplyClicked;

        public ThemePanel(string caption, string applyText)
        {
            Text = caption;
            Dock = DockStyle.Fill;
            Padding = new Padding(8);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            list.Dock = DockStyle.Fill;
            list.IntegralHeight = false;
            list.SelectedIndexChanged += delegate { ShowSelected(); };
            list.DoubleClick += delegate { if (apply.Enabled) OnApply(); };

            preview.Dock = DockStyle.Fill;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.BackColor = SystemColors.Window;

            details.AutoSize = true;
            details.MaximumSize = new Size(1000, 0);
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
            apply.Anchor = AnchorStyles.Right;
            apply.Click += delegate { OnApply(); };

            layout.Controls.Add(list, 0, 0);
            layout.Controls.Add(preview, 0, 1);
            layout.Controls.Add(details, 0, 2);
            layout.Controls.Add(website, 0, 3);
            layout.Controls.Add(apply, 0, 4);
            Resize += delegate { details.MaximumSize = new Size(Math.Max(100, ClientSize.Width - 30), 0); };
        }

        public Theme Selected { get { return list.SelectedItem as Theme; } }

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

        private void OnApply()
        {
            if (ApplyClicked != null)
                ApplyClicked(this, EventArgs.Empty);
        }

        private void ShowSelected()
        {
            Theme theme = Selected;
            SetPreview(theme == null ? null : LoadPreview(theme));
            apply.Enabled = canApply && theme != null;
            if (theme == null)
            {
                details.Text = "";
                website.Text = "";
                return;
            }

            List<string> lines = new List<string>();
            if (!string.IsNullOrEmpty(theme.Author)) lines.Add("By " + theme.Author);
            if (!string.IsNullOrEmpty(theme.Licence)) lines.Add("Licence: " + theme.Licence);
            if (theme.Missing.Count > 0)
                lines.Add("Not in this theme (7-Zip keeps its current picture): " + string.Join(", ", theme.Missing.ToArray()));
            details.Text = string.Join("\r\n", lines.ToArray());
            website.Text = theme.Website ?? "";
        }

        private void SetPreview(Image image)
        {
            Image old = preview.Image;
            preview.Image = image;
            if (old != null)
                old.Dispose();
        }

        // preview.jpg when the theme has one, otherwise a sheet of its pictures
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

                List<string> files = new List<string>();
                if (theme.Kind == ThemeKind.FileType)
                {
                    for (int slot = 0; slot < ThemeLayout.FileTypeSlots.Length && files.Count < 12; slot++)
                    {
                        string path = theme.FindFileTypeIcon(slot);
                        if (path != null) files.Add(path);
                    }
                }
                else
                {
                    foreach (string button in ThemeLayout.ToolbarButtons)
                    {
                        string path = theme.FindToolbarBitmap(ThemeLayout.LargeToolbarFolder, button);
                        if (path != null) files.Add(path);
                    }
                }
                if (files.Count == 0)
                    return null;

                Bitmap sheet = new Bitmap(files.Count * 56, 56);
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        if (theme.Kind == ThemeKind.FileType)
                        {
                            using (Icon icon = new Icon(files[i], 48, 48))
                                g.DrawIcon(icon, new Rectangle(i * 56 + 4, 4, 48, 48));
                        }
                        else
                        {
                            using (Image image = Image.FromFile(files[i]))
                                g.DrawImage(image, i * 56 + 4, 10, image.Width, image.Height);
                        }
                    }
                }
                return sheet;
            }
            catch (Exception)
            {
                // A broken preview should never stop the theme from being listed
                return null;
            }
        }
    }
}
