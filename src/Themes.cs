// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SevenZipThemeManager
{
    internal enum ThemeKind { FileType, Toolbar, Icons }

    // Where each themed picture goes inside 7-Zip's files
    internal static class ThemeLayout
    {
        // 7z.dll icon group N belongs to the format at index N. The first name is the theme file name;
        // the others are accepted alternatives used by some themes.
        public static readonly string[][] FileTypeSlots = new string[][]
        {
            new string[] { "7z" }, new string[] { "zip" }, new string[] { "bz2" }, new string[] { "rar" },
            new string[] { "arj" }, new string[] { "z" }, new string[] { "lha" }, new string[] { "cab" },
            new string[] { "iso" }, new string[] { "001" }, new string[] { "rpm" }, new string[] { "deb" },
            new string[] { "cpio" }, new string[] { "tar" }, new string[] { "gz" }, new string[] { "wim" },
            new string[] { "lzh" }, new string[] { "dmg" }, new string[] { "hfs" }, new string[] { "xar" },
            new string[] { "vhd" }, new string[] { "fat" }, new string[] { "ntfs" }, new string[] { "xz" },
            new string[] { "sqfs", "squashfs", "sq" }, new string[] { "apfs" }, new string[] { "zst", "zstd" },
        };

        // 7zFM.exe bitmaps: large (48x36) buttons are 100-106, small (24x24) are 150-156, in this order
        public static readonly string[] ToolbarButtons = { "Add", "Extract", "Test", "Copy", "Move", "Delete", "Info" };
        public const int LargeToolbarFirstId = 100;
        public const int SmallToolbarFirstId = 150;
        public const string LargeToolbarFolder = "48x36";
        public const string SmallToolbarFolder = "24x24";

        // Program icons (7zTM 3.5 "Icons" themes): icon file name, and the 7-Zip file whose main icon
        // it replaces. 7zS.sfx and 7zS2.sfx come with the separate "7-Zip Extra" download.
        public static readonly string[][] ProgramIcons = new string[][]
        {
            new string[] { "7zFM", "7zFM.exe" }, new string[] { "7zG", "7zG.exe" },
            new string[] { "7zSFX", "7z.sfx" }, new string[] { "7zConSFX", "7zCon.sfx" },
            new string[] { "7zSsfx", "7zS.sfx" }, new string[] { "7zS2sfx", "7zS2.sfx" },
        };

        public static string Describe(ThemeKind kind)
        {
            switch (kind)
            {
                case ThemeKind.FileType: return "file type";
                case ThemeKind.Toolbar: return "toolbar";
                default: return "program icon";
            }
        }
    }

    internal sealed class Theme
    {
        public ThemeKind Kind;
        public string Folder;
        public string Name;
        public string Author;
        public string Licence;
        public string Website;
        public string PreviewPath;
        public readonly List<string> Missing = new List<string>();
        public int Found;

        public string FolderName { get { return Path.GetFileName(Folder); } }

        public override string ToString()
        {
            return Name;
        }

        // Path of the icon for a 7z.dll slot, or null when the theme has none
        public string FindFileTypeIcon(int slot)
        {
            return FindIcon(ThemeLayout.FileTypeSlots[slot]);
        }

        public string FindFileTypeIcon(FileTypeSlot slot)
        {
            return FindIcon(slot.Names);
        }

        private string FindIcon(string[] names)
        {
            foreach (string name in names)
            {
                string path = Path.Combine(Folder, name + ".ico");
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        public string FindProgramIcon(int index)
        {
            string path = Path.Combine(Folder, ThemeLayout.ProgramIcons[index][0] + ".ico");
            return File.Exists(path) ? path : null;
        }

        public string FindToolbarBitmap(string sizeFolder, string button)
        {
            string path = Path.Combine(Path.Combine(Folder, sizeFolder), button + ".bmp");
            return File.Exists(path) ? path : null;
        }
    }

    internal static class ThemeStore
    {
        public const string FileTypeFolder = "FileType";
        public const string ToolbarFolder = "ToolBar";
        public const string IconsFolder = "Icons";

        public static string FolderFor(ThemeKind kind)
        {
            switch (kind)
            {
                case ThemeKind.FileType: return FileTypeFolder;
                case ThemeKind.Toolbar: return ToolbarFolder;
                default: return IconsFolder;
            }
        }

        public static bool LooksLikeThemesRoot(string folder)
        {
            return Directory.Exists(Path.Combine(folder, FileTypeFolder)) || Directory.Exists(Path.Combine(folder, ToolbarFolder))
                || Directory.Exists(Path.Combine(folder, IconsFolder));
        }

        // Portable: themes always live next to the program
        public static string Root
        {
            get { return System.Windows.Forms.Application.StartupPath; }
        }

        public static List<Theme> Load(string root, ThemeKind kind)
        {
            List<Theme> themes = new List<Theme>();
            string folder = Path.Combine(root, FolderFor(kind));
            if (!Directory.Exists(folder))
                return themes;

            foreach (string dir in Directory.GetDirectories(folder))
            {
                Theme theme = LoadTheme(dir, kind);
                if (theme.Found > 0)
                    themes.Add(theme);
            }
            themes.Sort(delegate(Theme a, Theme b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return themes;
        }

        public static Theme LoadTheme(string dir, ThemeKind kind)
        {
            Theme theme = new Theme();
            theme.Kind = kind;
            theme.Folder = dir;
            theme.Name = Path.GetFileName(dir);

            Dictionary<string, string> ini = ReadIni(Path.Combine(dir, "theme.ini"));
            string value;
            if (ini.TryGetValue("name", out value) && value.Length > 0) theme.Name = value;
            if (ini.TryGetValue("author", out value)) theme.Author = value;
            if (ini.TryGetValue("licence", out value) || ini.TryGetValue("license", out value)) theme.Licence = value;
            if (ini.TryGetValue("website", out value)) theme.Website = value;

            // preview.png is lossless and wins; preview.jpg is what 7zTM reads, so older themes keep working
            // and a theme can ship both to stay usable in 7zTM
            foreach (string preview in new string[] { "preview.png", "preview.jpg" })
            {
                if (File.Exists(Path.Combine(dir, preview)))
                {
                    theme.PreviewPath = Path.Combine(dir, preview);
                    break;
                }
            }

            if (kind == ThemeKind.FileType)
            {
                // Missing and extra icons depend on the 7-Zip in use; FileTypeSupport.Compare works them out
                theme.Found = Directory.GetFiles(dir, "*.ico").Length;
            }
            else if (kind == ThemeKind.Icons)
            {
                for (int i = 0; i < ThemeLayout.ProgramIcons.Length; i++)
                {
                    if (theme.FindProgramIcon(i) != null) theme.Found++;
                    else theme.Missing.Add(ThemeLayout.ProgramIcons[i][0] + ".ico");
                }
            }
            else
            {
                foreach (string size in new string[] { ThemeLayout.LargeToolbarFolder, ThemeLayout.SmallToolbarFolder })
                {
                    foreach (string button in ThemeLayout.ToolbarButtons)
                    {
                        if (theme.FindToolbarBitmap(size, button) != null) theme.Found++;
                        else theme.Missing.Add(size + "\\" + button + ".bmp");
                    }
                }
            }
            return theme;
        }

        // Reads key=value lines from the [Theme] section. Older themes are ANSI, newer ones UTF-8.
        private static Dictionary<string, string> ReadIni(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return values;

            byte[] bytes = File.ReadAllBytes(path);
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                text = Encoding.Default.GetString(bytes);
            }
            text = text.TrimStart('﻿');

            bool inTheme = false;
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("["))
                {
                    inTheme = string.Equals(line, "[Theme]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (inTheme && eq > 0)
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return values;
        }
    }
}
