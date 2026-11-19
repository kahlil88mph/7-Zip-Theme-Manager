// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SevenZipThemeManager
{
    // Remembers the chosen folders in %LOCALAPPDATA%\7-Zip Theme Manager\settings.ini
    internal static class Settings
    {
        public static string DataFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "7-Zip Theme Manager"); }
        }

        private static string FilePath
        {
            get { return Path.Combine(DataFolder, "settings.ini"); }
        }

        public static string SevenZipFolder;
        public static string ThemesFolder;

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (key == "SevenZipFolder") SevenZipFolder = value;
                    else if (key == "ThemesFolder") ThemesFolder = value;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(DataFolder);
                List<string> lines = new List<string>();
                lines.Add("SevenZipFolder=" + SevenZipFolder);
                lines.Add("ThemesFolder=" + ThemesFolder);
                File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
