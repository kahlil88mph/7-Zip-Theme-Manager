// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SevenZipThemeManager
{
    internal sealed class PatchResult
    {
        public int Replaced;
        public readonly List<string> NotInTheme = new List<string>();
        public readonly List<string> NotInSevenZip = new List<string>();
        public bool UsedRenameFallback;

        public string Summary(string what)
        {
            StringBuilder text = new StringBuilder();
            text.AppendFormat("{0}: replaced {1} picture{2}.", what, Replaced, Replaced == 1 ? "" : "s");
            if (NotInTheme.Count > 0)
                text.Append("\r\nKept 7-Zip's current picture (theme has none): " + string.Join(", ", NotInTheme.ToArray()));
            if (NotInSevenZip.Count > 0)
                text.Append("\r\nNot used by this 7-Zip version: " + string.Join(", ", NotInSevenZip.ToArray()));
            if (UsedRenameFallback)
                text.Append("\r\nThe old file was in use; it will be removed after the next restart.");
            return text.ToString();
        }
    }

    internal static class Patcher
    {
        private delegate void ChangeBuilder(List<ResourceEntry> current, ResourceWriter writer, PatchResult result);

        public static PatchResult ApplyFileType(string sevenZipFolder, Theme theme)
        {
            PatchResult result = new PatchResult();
            string target = Path.Combine(sevenZipFolder, SevenZipInstall.IconLibrary);
            PatchFile(target, result, delegate(List<ResourceEntry> current, ResourceWriter writer, PatchResult r)
            {
                BuildFileTypeChanges(current, theme, writer, r);
            }, NativeMethods.RT_ICON, NativeMethods.RT_GROUP_ICON, NativeMethods.RT_STRING);
            NotifyShell();
            return result;
        }

        public static PatchResult ApplyToolbar(string sevenZipFolder, Theme theme)
        {
            PatchResult result = new PatchResult();
            string target = Path.Combine(sevenZipFolder, SevenZipInstall.FileManager);
            PatchFile(target, result, delegate(List<ResourceEntry> current, ResourceWriter writer, PatchResult r)
            {
                BuildToolbarChanges(current, theme, writer, r);
            }, NativeMethods.RT_BITMAP);
            return result;
        }

        // Replaces the main icon of 7zFM.exe, 7zG.exe and the SFX modules that are present
        public static PatchResult ApplyIcons(string sevenZipFolder, Theme theme)
        {
            PatchResult result = new PatchResult();
            for (int i = 0; i < ThemeLayout.ProgramIcons.Length; i++)
            {
                string label = ThemeLayout.ProgramIcons[i][1];
                string target = Path.Combine(sevenZipFolder, label);
                string iconPath = theme.FindProgramIcon(i);
                if (!File.Exists(target))
                {
                    if (iconPath != null) result.NotInSevenZip.Add(label);
                    continue;
                }
                if (iconPath == null)
                {
                    result.NotInTheme.Add(label);
                    continue;
                }
                PatchFile(target, result, delegate(List<ResourceEntry> current, ResourceWriter writer, PatchResult r)
                {
                    // The first icon group is the one Windows shows for the program
                    ResourceEntry main = null;
                    foreach (ResourceEntry entry in current)
                    {
                        if (entry.Type == NativeMethods.RT_GROUP_ICON) { main = entry; break; }
                    }
                    if (main == null)
                    {
                        r.NotInSevenZip.Add(label);
                        return;
                    }
                    Dictionary<ResourceEntry, List<IconImage>> replacements = new Dictionary<ResourceEntry, List<IconImage>>();
                    replacements[main] = IconFile.Load(iconPath);
                    ReplaceIconGroups(current, replacements, writer, r, label);
                }, NativeMethods.RT_ICON, NativeMethods.RT_GROUP_ICON);
            }
            NotifyShell();
            return result;
        }

        private static void BuildFileTypeChanges(List<ResourceEntry> current, Theme theme, ResourceWriter writer, PatchResult result)
        {
            List<ResourceEntry> groups = new List<ResourceEntry>();
            foreach (ResourceEntry entry in current)
            {
                if (entry.Type == NativeMethods.RT_GROUP_ICON) groups.Add(entry);
            }

            // The slots come from this 7z.dll, so the report matches what the window showed for the theme
            FileTypeSupport support = FileTypeSupport.FromResources(current);
            FileTypeCoverage coverage = support.Compare(theme);
            result.NotInTheme.AddRange(coverage.Missing);
            result.NotInSevenZip.AddRange(coverage.Extra);

            // Work out which groups get replaced before touching any RT_ICON ids
            Dictionary<ResourceEntry, List<IconImage>> replacements = new Dictionary<ResourceEntry, List<IconImage>>();
            foreach (FileTypeSlot slot in support.Slots)
            {
                string iconPath = theme.FindFileTypeIcon(slot);
                if (iconPath == null)
                    continue;
                foreach (ResourceEntry candidate in groups)
                {
                    if (candidate.Name.Matches(slot.Index)) { replacements[candidate] = IconFile.Load(iconPath); break; }
                }
            }
            ReplaceIconGroups(current, replacements, writer, result, SevenZipInstall.IconLibrary);
        }

        // Writes new RT_ICON images and group data for each replaced group, reusing the ids of icons
        // that no remaining group refers to and deleting the ones left over
        private static void ReplaceIconGroups(List<ResourceEntry> current, Dictionary<ResourceEntry, List<IconImage>> replacements,
            ResourceWriter writer, PatchResult result, string fileName)
        {
            List<ResourceEntry> groups = new List<ResourceEntry>();
            Dictionary<int, ushort> iconLanguages = new Dictionary<int, ushort>();
            foreach (ResourceEntry entry in current)
            {
                if (entry.Type == NativeMethods.RT_GROUP_ICON) groups.Add(entry);
                else if (entry.Type == NativeMethods.RT_ICON && !entry.Name.IsString) iconLanguages[entry.Name.Id] = entry.Language;
            }

            // RT_ICON ids still referenced after the change must stay; the rest can be freed and reused
            Dictionary<int, bool> inUse = new Dictionary<int, bool>();
            foreach (ResourceEntry group in groups)
            {
                if (replacements.ContainsKey(group))
                    continue;
                foreach (int id in IconFile.GroupIconIds(group.Data))
                    inUse[id] = true;
            }
            Dictionary<int, bool> freed = new Dictionary<int, bool>();
            foreach (ResourceEntry group in replacements.Keys)
            {
                foreach (int id in IconFile.GroupIconIds(group.Data))
                {
                    if (!inUse.ContainsKey(id) && iconLanguages.ContainsKey(id))
                        freed[id] = true;
                }
            }
            foreach (int id in iconLanguages.Keys)
            {
                if (!freed.ContainsKey(id))
                    inUse[id] = true;
            }

            int nextId = 1;
            foreach (KeyValuePair<ResourceEntry, List<IconImage>> pair in replacements)
            {
                ResourceEntry group = pair.Key;
                List<int> newIds = new List<int>();
                foreach (IconImage image in pair.Value)
                {
                    while (inUse.ContainsKey(nextId)) nextId++;
                    if (nextId > 0xFFFF)
                        throw new InvalidOperationException(fileName + " has no free icon ids left");
                    inUse[nextId] = true;
                    newIds.Add(nextId);
                    freed.Remove(nextId);
                    writer.Set(NativeMethods.RT_ICON, new ResourceName(nextId), group.Language, image.Data);
                }
                writer.Set(NativeMethods.RT_GROUP_ICON, group.Name, group.Language, IconFile.BuildGroup(pair.Value, newIds));
                result.Replaced++;
            }

            foreach (int id in freed.Keys)
                writer.Delete(NativeMethods.RT_ICON, new ResourceName(id), iconLanguages[id]);
        }

        private static void BuildToolbarChanges(List<ResourceEntry> current, Theme theme, ResourceWriter writer, PatchResult result)
        {
            AddToolbarSize(current, theme, writer, result, ThemeLayout.LargeToolbarFolder, ThemeLayout.LargeToolbarFirstId);
            AddToolbarSize(current, theme, writer, result, ThemeLayout.SmallToolbarFolder, ThemeLayout.SmallToolbarFirstId);
        }

        private static void AddToolbarSize(List<ResourceEntry> current, Theme theme, ResourceWriter writer, PatchResult result, string sizeFolder, int firstId)
        {
            for (int i = 0; i < ThemeLayout.ToolbarButtons.Length; i++)
            {
                string label = sizeFolder + "\\" + ThemeLayout.ToolbarButtons[i];
                ResourceEntry existing = null;
                foreach (ResourceEntry entry in current)
                {
                    if (entry.Type == NativeMethods.RT_BITMAP && entry.Name.Matches(firstId + i)) { existing = entry; break; }
                }
                string path = theme.FindToolbarBitmap(sizeFolder, ThemeLayout.ToolbarButtons[i]);
                if (existing == null)
                {
                    if (path != null) result.NotInSevenZip.Add(label);
                    continue;
                }
                if (path == null)
                {
                    result.NotInTheme.Add(label);
                    continue;
                }

                // RT_BITMAP data is the .bmp file without its 14-byte BITMAPFILEHEADER
                byte[] file = File.ReadAllBytes(path);
                if (file.Length <= 14 + 40 || file[0] != 'B' || file[1] != 'M')
                    throw new InvalidDataException(path + " is not a bitmap file");
                byte[] data = new byte[file.Length - 14];
                Buffer.BlockCopy(file, 14, data, 0, data.Length);
                writer.Set(NativeMethods.RT_BITMAP, existing.Name, existing.Language, data);
                result.Replaced++;
            }
        }

        // Patches a copy next to the target, checks it still loads, keeps a backup, then swaps it in
        private static void PatchFile(string target, PatchResult result, ChangeBuilder build, params int[] types)
        {
            List<ResourceEntry> current = ResourceReader.Read(target, types);
            ResourceWriter writer = new ResourceWriter();
            build(current, writer, result);
            if (writer.Count == 0)
                return;

            string temp = target + ".7zTM-new";
            File.Copy(target, temp, true);
            try
            {
                writer.Commit(temp);
                ResourceReader.Read(temp, types);
                Backup.SaveOnce(target);
                result.UsedRenameFallback = ReplaceFile(temp, target);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        // Returns true when the target was in use and had to be renamed out of the way
        public static bool ReplaceFile(string source, string target)
        {
            try
            {
                File.Copy(source, target, true);
                return false;
            }
            catch (IOException)
            {
                if (!File.Exists(target))
                    throw;
            }

            // A loaded DLL or running exe cannot be overwritten, but it can be renamed
            string old = target + ".7zTM-old";
            for (int n = 1; File.Exists(old); n++)
            {
                try { File.Delete(old); }
                catch (IOException) { old = target + ".7zTM-old" + n; }
                catch (UnauthorizedAccessException) { old = target + ".7zTM-old" + n; }
            }
            File.Move(target, old);
            File.Copy(source, target, true);
            NativeMethods.MoveFileEx(old, null, NativeMethods.MOVEFILE_DELAY_UNTIL_REBOOT);
            return true;
        }

        // Deletes leftovers from earlier swaps once nothing is using them any more
        public static void CleanUp(string sevenZipFolder)
        {
            try
            {
                foreach (string file in Directory.GetFiles(sevenZipFolder, "*.7zTM-old*"))
                {
                    try { File.Delete(file); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void NotifyShell()
        {
            NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    // Keeps the first copy of each 7-Zip file seen before this program changed it
    internal static class Backup
    {
        public static string Folder(string sevenZipFolder)
        {
            string key = sevenZipFolder.TrimEnd('\\').Replace(":", "").Replace('\\', '_');
            return Path.Combine(Path.Combine(Settings.DataFolder, "Backup"), key);
        }

        public static void SaveOnce(string target)
        {
            string folder = Folder(Path.GetDirectoryName(target));
            string copy = Path.Combine(folder, Path.GetFileName(target));
            if (File.Exists(copy))
                return;
            Directory.CreateDirectory(folder);
            File.Copy(target, copy, false);
        }

        public static bool Exists(string sevenZipFolder)
        {
            string folder = Folder(sevenZipFolder);
            return Directory.Exists(folder) && Directory.GetFiles(folder).Length > 0;
        }

        // Puts the saved files back; returns how many were restored
        public static int Restore(string sevenZipFolder)
        {
            int restored = 0;
            string folder = Folder(sevenZipFolder);
            if (!Directory.Exists(folder))
                return 0;
            foreach (string copy in Directory.GetFiles(folder))
            {
                Patcher.ReplaceFile(copy, Path.Combine(sevenZipFolder, Path.GetFileName(copy)));
                restored++;
            }
            NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
            return restored;
        }
    }
}
