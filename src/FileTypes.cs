// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SevenZipThemeManager
{
    // One file type icon of the 7-Zip in use: its 7z.dll icon group number and the theme file names that fill it
    internal sealed class FileTypeSlot
    {
        public int Index;
        public string[] Names;

        public string Label { get { return Names[0]; } }
    }

    // How a theme's file type icons line up with the 7-Zip in use
    internal sealed class FileTypeCoverage
    {
        public int Total;
        public readonly List<string> Missing = new List<string>();
        public readonly List<string> Extra = new List<string>();
    }

    // The file type icons a particular 7z.dll has, read from the file itself rather than assumed
    internal sealed class FileTypeSupport
    {
        // 7z.dll string 100 maps extensions to icon groups, e.g. "7z:0 zip:1 ... apfs:25"
        private const int ExtensionMapStringId = 100;

        public readonly List<FileTypeSlot> Slots = new List<FileTypeSlot>();

        // Null when the folder has no readable 7z.dll
        public static FileTypeSupport Read(string sevenZipFolder)
        {
            try
            {
                string dll = Path.Combine(sevenZipFolder, SevenZipInstall.IconLibrary);
                return FromResources(ResourceReader.Read(dll, NativeMethods.RT_GROUP_ICON, NativeMethods.RT_STRING));
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        // Every numbered icon group is a file type slot. Slots this program knows keep their usual
        // theme file names; newer ones are named after the extensions 7-Zip maps to them.
        public static FileTypeSupport FromResources(List<ResourceEntry> entries)
        {
            Dictionary<int, List<string>> extensions = ReadExtensionMap(entries);
            SortedDictionary<int, bool> indexes = new SortedDictionary<int, bool>();
            foreach (ResourceEntry entry in entries)
            {
                if (entry.Type != NativeMethods.RT_GROUP_ICON)
                    continue;
                // 7z.dll names group 0 "0" as a string; the others have numeric ids
                int index = entry.Name.Id;
                if (entry.Name.IsString && !int.TryParse(entry.Name.Text, out index))
                    continue;
                if (index >= 0)
                    indexes[index] = true;
            }

            FileTypeSupport support = new FileTypeSupport();
            foreach (int index in indexes.Keys)
            {
                FileTypeSlot slot = new FileTypeSlot();
                slot.Index = index;
                if (index < ThemeLayout.FileTypeSlots.Length)
                    slot.Names = ThemeLayout.FileTypeSlots[index];
                else if (extensions.ContainsKey(index))
                    slot.Names = extensions[index].ToArray();
                else
                    continue; // an icon 7-Zip does not use for any file type
                support.Slots.Add(slot);
            }
            return support;
        }

        public FileTypeCoverage Compare(Theme theme)
        {
            FileTypeCoverage coverage = new FileTypeCoverage();
            coverage.Total = Slots.Count;
            Dictionary<string, bool> used = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (FileTypeSlot slot in Slots)
            {
                string path = theme.FindFileTypeIcon(slot);
                if (path == null) coverage.Missing.Add(slot.Label);
                else used[Path.GetFileName(path)] = true;
            }

            string[] icons;
            try { icons = Directory.GetFiles(theme.Folder, "*.ico"); }
            catch (IOException) { icons = new string[0]; }
            catch (UnauthorizedAccessException) { icons = new string[0]; }
            Array.Sort(icons, StringComparer.OrdinalIgnoreCase);
            foreach (string icon in icons)
            {
                if (!used.ContainsKey(Path.GetFileName(icon)))
                    coverage.Extra.Add(Path.GetFileNameWithoutExtension(icon));
            }
            return coverage;
        }

        // Extensions per icon group from string 100, in the order 7-Zip lists them
        private static Dictionary<int, List<string>> ReadExtensionMap(List<ResourceEntry> entries)
        {
            Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
            string text = FindString(entries, ExtensionMapStringId);
            if (text == null)
                return map;
            foreach (string pair in text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = pair.LastIndexOf(':');
                int index;
                if (colon <= 0 || !int.TryParse(pair.Substring(colon + 1), out index))
                    continue;
                if (!map.ContainsKey(index))
                    map[index] = new List<string>();
                map[index].Add(pair.Substring(0, colon));
            }
            return map;
        }

        // RT_STRING blocks hold 16 strings each (block n has ids 16(n-1) to 16n-1), as WORD length + UTF-16
        private static string FindString(List<ResourceEntry> entries, int id)
        {
            int block = id / 16 + 1;
            foreach (ResourceEntry entry in entries)
            {
                if (entry.Type != NativeMethods.RT_STRING || !entry.Name.Matches(block))
                    continue;
                byte[] data = entry.Data;
                int offset = 0;
                for (int i = 0; i < 16 && offset + 2 <= data.Length; i++)
                {
                    int length = BitConverter.ToUInt16(data, offset);
                    offset += 2;
                    if (offset + length * 2 > data.Length)
                        break;
                    if (i == id % 16)
                        return length == 0 ? null : Encoding.Unicode.GetString(data, offset, length * 2);
                    offset += length * 2;
                }
            }
            return null;
        }
    }
}
