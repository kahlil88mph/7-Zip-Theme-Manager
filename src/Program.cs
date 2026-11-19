// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SevenZipThemeManager
{
    internal static class Program
    {
        public const string Title = "7-Zip Theme Manager";

        private const string Usage =
            "Usage: 7zThemeManager.exe [--7zip <folder>] [--themes <folder>]\r\n" +
            "                          [--filetype <theme>] [--toolbar <theme>] [--restore]\r\n" +
            "\r\n" +
            "With no options the window opens. <theme> is a theme's folder name or its name in theme.ini.\r\n" +
            "--restore puts back the 7-Zip files saved before the first change.";

        [STAThread]
        private static int Main(string[] args)
        {
            Settings.Load();
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
            return RunCommandLine(args);
        }

        private static int RunCommandLine(string[] args)
        {
            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);

            Options options;
            string error = Options.TryParse(args, out options);
            if (error != null)
            {
                Report(options, error + "\r\n\r\n" + Usage);
                return 2;
            }
            if (options.Help)
            {
                Report(options, Usage);
                return 0;
            }

            try
            {
                string output = Run(options);
                Report(options, output);
                return 0;
            }
            catch (Exception ex)
            {
                Report(options, "Error: " + DescribeError(ex));
                return 1;
            }
        }

        // Does the work for both the command line and the elevated helper started by the window
        public static string Run(Options options)
        {
            string sevenZip = options.SevenZipFolder ?? Settings.SevenZipFolder ?? SevenZipInstall.Find();
            if (!SevenZipInstall.IsSevenZipFolder(sevenZip))
                throw new InvalidOperationException("7-Zip was not found. Pass its folder with --7zip.");
            Patcher.CleanUp(sevenZip);

            StringBuilder output = new StringBuilder();
            if (options.Restore)
            {
                int restored = Backup.Restore(sevenZip);
                output.AppendLine(restored == 0 ? "No saved 7-Zip files to restore." : "Restored " + restored + " saved 7-Zip file(s).");
                return output.ToString();
            }

            string themesRoot = options.ThemesFolder ?? Settings.ThemesFolder ?? ThemeStore.FindThemesRoot(Application.StartupPath);
            if (options.FileType != null)
            {
                Theme theme = FindTheme(themesRoot, ThemeKind.FileType, options.FileType);
                output.AppendLine(Patcher.ApplyFileType(sevenZip, theme).Summary("File type icons \"" + theme.Name + "\""));
            }
            if (options.Toolbar != null)
            {
                Theme theme = FindTheme(themesRoot, ThemeKind.Toolbar, options.Toolbar);
                output.AppendLine(Patcher.ApplyToolbar(sevenZip, theme).Summary("Toolbar \"" + theme.Name + "\""));
            }
            if (output.Length == 0)
                output.AppendLine("Nothing to do. Pass --filetype, --toolbar or --restore.");
            return output.ToString();
        }

        private static Theme FindTheme(string themesRoot, ThemeKind kind, string wanted)
        {
            if (string.IsNullOrEmpty(themesRoot) || !ThemeStore.LooksLikeThemesRoot(themesRoot))
                throw new InvalidOperationException("The themes folder was not found. Pass the folder that holds FileType and ToolBar with --themes.");
            foreach (Theme theme in ThemeStore.Load(themesRoot, kind))
            {
                if (string.Equals(theme.FolderName, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(theme.Name, wanted, StringComparison.OrdinalIgnoreCase))
                    return theme;
            }
            throw new InvalidOperationException("No " + (kind == ThemeKind.FileType ? "file type" : "toolbar") + " theme called \"" + wanted + "\".");
        }

        public static string DescribeError(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
                return ex.Message + " Run 7-Zip Theme Manager as administrator to change an installed 7-Zip.";
            if (ex is Win32Exception && ((Win32Exception)ex).NativeErrorCode == 5)
                return ex.Message + " (access denied). Run 7-Zip Theme Manager as administrator to change an installed 7-Zip.";
            return ex.Message;
        }

        private static void Report(Options options, string text)
        {
            if (options != null && options.ResultFile != null)
            {
                File.WriteAllText(options.ResultFile, text, Encoding.UTF8);
                return;
            }
            Console.WriteLine();
            Console.WriteLine(text);
        }

        // Windows Vista and later: runs the same work as administrator and returns its report
        public static string RunElevated(Options options)
        {
            string resultFile = Path.Combine(Path.GetTempPath(), "7zTM-result-" + Guid.NewGuid().ToString("N") + ".txt");
            options.ResultFile = resultFile;

            ProcessStartInfo start = new ProcessStartInfo(Application.ExecutablePath, options.ToArguments());
            start.Verb = "runas";
            start.UseShellExecute = true;
            try
            {
                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    return "Cancelled: administrator permission was not given.";
                throw;
            }

            if (!File.Exists(resultFile))
                return "The administrator helper finished without a report.";
            string report = File.ReadAllText(resultFile, Encoding.UTF8);
            File.Delete(resultFile);
            return report;
        }

        public static bool CanElevate
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6; }
        }
    }

    internal sealed class Options
    {
        public string SevenZipFolder;
        public string ThemesFolder;
        public string FileType;
        public string Toolbar;
        public string ResultFile;
        public bool Restore;
        public bool Help;

        public static string TryParse(string[] args, out Options options)
        {
            options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "--help" || arg == "-h" || arg == "/?") { options.Help = true; continue; }
                if (arg == "--restore") { options.Restore = true; continue; }

                if (i + 1 >= args.Length)
                    return "Missing value after " + args[i];
                string value = args[++i];
                switch (arg)
                {
                    case "--7zip": options.SevenZipFolder = value.TrimEnd('\\'); break;
                    case "--themes": options.ThemesFolder = value.TrimEnd('\\'); break;
                    case "--filetype": options.FileType = value; break;
                    case "--toolbar": options.Toolbar = value; break;
                    case "--result": options.ResultFile = value; break;
                    default: return "Unknown option " + args[i];
                }
            }
            return null;
        }

        public string ToArguments()
        {
            List<string> parts = new List<string>();
            AddPair(parts, "--7zip", SevenZipFolder);
            AddPair(parts, "--themes", ThemesFolder);
            AddPair(parts, "--filetype", FileType);
            AddPair(parts, "--toolbar", Toolbar);
            AddPair(parts, "--result", ResultFile);
            if (Restore) parts.Add("--restore");
            return string.Join(" ", parts.ToArray());
        }

        private static void AddPair(List<string> parts, string name, string value)
        {
            if (value == null)
                return;
            parts.Add(name);
            parts.Add("\"" + value.TrimEnd('\\').Replace("\"", "") + "\"");
        }
    }
}
