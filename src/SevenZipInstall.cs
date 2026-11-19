// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SevenZipThemeManager
{
    internal static class SevenZipInstall
    {
        public const string IconLibrary = "7z.dll";
        public const string FileManager = "7zFM.exe";

        public static bool IsSevenZipFolder(string folder)
        {
            return !string.IsNullOrEmpty(folder)
                && File.Exists(Path.Combine(folder, IconLibrary))
                && File.Exists(Path.Combine(folder, FileManager));
        }

        // Installed 7-Zip from the registry, then the usual Program Files folders
        public static string Find()
        {
            List<string> candidates = new List<string>();
            foreach (RegistryKey hive in new RegistryKey[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using (RegistryKey key = hive.OpenSubKey(@"Software\7-Zip"))
                {
                    if (key == null)
                        continue;
                    foreach (string value in new string[] { "Path64", "Path" })
                    {
                        string path = key.GetValue(value) as string;
                        if (!string.IsNullOrEmpty(path))
                            candidates.Add(path);
                    }
                }
            }
            foreach (string variable in new string[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" })
            {
                string programFiles = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(programFiles))
                    candidates.Add(Path.Combine(programFiles, "7-Zip"));
            }

            foreach (string candidate in candidates)
            {
                if (IsSevenZipFolder(candidate))
                    return candidate.TrimEnd('\\');
            }
            return null;
        }

        public static string Version(string folder)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(Path.Combine(folder, FileManager));
                return string.IsNullOrEmpty(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
            }
            catch (IOException)
            {
                return null;
            }
        }

        // True when files in the folder can be replaced without administrator rights
        public static bool CanWrite(string folder)
        {
            string probe = Path.Combine(folder, "7zTM-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (File.Create(probe)) { }
                File.Delete(probe);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
