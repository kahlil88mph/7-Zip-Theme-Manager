// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SevenZipThemeManager
{
    // A resource name is either a 16-bit id or a string. 7z.dll names its first icon group "0" as a string.
    internal sealed class ResourceName
    {
        public readonly int Id;
        public readonly string Text;

        public ResourceName(int id) { Id = id; }
        public ResourceName(string text) { Text = text; Id = -1; }

        public bool IsString { get { return Text != null; } }

        // Matches "5" against both the id 5 and the string name "5"
        public bool Matches(int number)
        {
            return IsString ? Text == number.ToString() : Id == number;
        }

        public override string ToString()
        {
            return IsString ? "\"" + Text + "\"" : Id.ToString();
        }
    }

    internal sealed class ResourceEntry
    {
        public int Type;
        public ResourceName Name;
        public ushort Language;
        public byte[] Data;
    }

    // Reads the resource table straight from the PE file. EnumResourceNames is not used because the
    // string names it hands back are not reliably null-terminated, which garbles names such as "0".
    internal static class ResourceReader
    {
        private const int ResourceDirectoryIndex = 2;

        public static List<ResourceEntry> Read(string fileName, params int[] types)
        {
            byte[] file = File.ReadAllBytes(fileName);
            try
            {
                return Parse(file, types);
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidDataException(Path.GetFileName(fileName) + " has a damaged resource table");
            }
            catch (ArgumentException)
            {
                throw new InvalidDataException(Path.GetFileName(fileName) + " has a damaged resource table");
            }
        }

        private static List<ResourceEntry> Parse(byte[] file, int[] types)
        {
            List<ResourceEntry> result = new List<ResourceEntry>();
            if (file.Length < 0x40 || file[0] != 'M' || file[1] != 'Z')
                throw new InvalidDataException("Not a Windows program file");
            int pe = BitConverter.ToInt32(file, 0x3C);
            if (BitConverter.ToUInt32(file, pe) != 0x00004550)
                throw new InvalidDataException("Not a Windows program file");

            int sectionCount = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            int magic = BitConverter.ToUInt16(file, optional);
            int directories = optional + (magic == 0x20B ? 112 : 96);
            int resourceRva = BitConverter.ToInt32(file, directories + 8 * ResourceDirectoryIndex);
            if (resourceRva == 0)
                return result;

            int[] sectionRva = new int[sectionCount];
            int[] sectionSize = new int[sectionCount];
            int[] sectionRaw = new int[sectionCount];
            int sections = optional + optionalSize;
            for (int i = 0; i < sectionCount; i++)
            {
                int s = sections + 40 * i;
                sectionSize[i] = Math.Max(BitConverter.ToInt32(file, s + 8), BitConverter.ToInt32(file, s + 16));
                sectionRva[i] = BitConverter.ToInt32(file, s + 12);
                sectionRaw[i] = BitConverter.ToInt32(file, s + 20);
            }
            Converter<int, int> toOffset = delegate(int rva)
            {
                for (int i = 0; i < sectionCount; i++)
                {
                    if (rva >= sectionRva[i] && rva < sectionRva[i] + sectionSize[i])
                        return rva - sectionRva[i] + sectionRaw[i];
                }
                throw new InvalidDataException("Resource address outside every section");
            };

            int root = toOffset(resourceRva);
            List<int> wanted = new List<int>(types);
            foreach (KeyValuePair<ResourceName, int> type in ReadDirectory(file, root, root))
            {
                if (type.Key.IsString || !wanted.Contains(type.Key.Id))
                    continue;
                foreach (KeyValuePair<ResourceName, int> name in ReadDirectory(file, root, type.Value))
                {
                    foreach (KeyValuePair<ResourceName, int> lang in ReadDirectory(file, root, name.Value))
                    {
                        // Leaf: IMAGE_RESOURCE_DATA_ENTRY { OffsetToData (RVA), Size, CodePage, Reserved }
                        int dataOffset = toOffset(BitConverter.ToInt32(file, lang.Value));
                        int size = BitConverter.ToInt32(file, lang.Value + 4);
                        byte[] data = new byte[size];
                        Buffer.BlockCopy(file, dataOffset, data, 0, size);

                        ResourceEntry entry = new ResourceEntry();
                        entry.Type = type.Key.Id;
                        entry.Name = name.Key;
                        entry.Language = (ushort)lang.Key.Id;
                        entry.Data = data;
                        result.Add(entry);
                    }
                }
            }
            return result;
        }

        // Entries of one IMAGE_RESOURCE_DIRECTORY: name and file offset of the child (directory or data entry)
        private static List<KeyValuePair<ResourceName, int>> ReadDirectory(byte[] file, int root, int directory)
        {
            List<KeyValuePair<ResourceName, int>> entries = new List<KeyValuePair<ResourceName, int>>();
            int count = BitConverter.ToUInt16(file, directory + 12) + BitConverter.ToUInt16(file, directory + 14);
            for (int i = 0; i < count; i++)
            {
                int entry = directory + 16 + 8 * i;
                uint name = BitConverter.ToUInt32(file, entry);
                uint child = BitConverter.ToUInt32(file, entry + 4);

                ResourceName resourceName;
                if ((name & 0x80000000) != 0)
                {
                    // IMAGE_RESOURCE_DIR_STRING_U: WORD length, then that many UTF-16 characters
                    int text = root + (int)(name & 0x7FFFFFFF);
                    int length = BitConverter.ToUInt16(file, text);
                    resourceName = new ResourceName(Encoding.Unicode.GetString(file, text + 2, length * 2));
                }
                else
                {
                    resourceName = new ResourceName((int)(name & 0xFFFF));
                }
                entries.Add(new KeyValuePair<ResourceName, int>(resourceName, root + (int)(child & 0x7FFFFFFF)));
            }
            return entries;
        }
    }

    // Collects resource changes and writes them in one BeginUpdateResource/EndUpdateResource pass
    internal sealed class ResourceWriter
    {
        private readonly List<ResourceEntry> changes = new List<ResourceEntry>();

        public void Set(int type, ResourceName name, ushort language, byte[] data)
        {
            ResourceEntry entry = new ResourceEntry();
            entry.Type = type;
            entry.Name = name;
            entry.Language = language;
            entry.Data = data;
            changes.Add(entry);
        }

        public void Delete(int type, ResourceName name, ushort language)
        {
            Set(type, name, language, null);
        }

        public int Count { get { return changes.Count; } }

        public void Commit(string fileName)
        {
            IntPtr update = NativeMethods.BeginUpdateResource(fileName, false);
            if (update == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open " + fileName + " for writing");

            // UpdateResource keeps string names by pointer until EndUpdateResource, so they must outlive it
            List<NamePointer> names = new List<NamePointer>();
            try
            {
                bool ok = false;
                try
                {
                    foreach (ResourceEntry change in changes)
                    {
                        NamePointer namePtr = new NamePointer(change.Name);
                        names.Add(namePtr);
                        uint size = change.Data == null ? 0u : (uint)change.Data.Length;
                        if (!NativeMethods.UpdateResource(update, new IntPtr(change.Type), namePtr.Value, change.Language, change.Data, size))
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update resource " + change.Name + " (type " + change.Type + ")");
                    }
                    ok = true;
                }
                finally
                {
                    if (!ok)
                        NativeMethods.EndUpdateResource(update, true);
                }

                if (!NativeMethods.EndUpdateResource(update, false))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not save " + fileName);
            }
            finally
            {
                foreach (NamePointer name in names)
                    name.Dispose();
            }
        }
    }

    // Turns a ResourceName into the pointer form the Win32 resource functions expect
    internal sealed class NamePointer : IDisposable
    {
        public readonly IntPtr Value;
        private readonly bool allocated;

        public NamePointer(ResourceName name)
        {
            if (name.IsString)
            {
                Value = Marshal.StringToHGlobalUni(name.Text);
                allocated = true;
            }
            else
            {
                Value = new IntPtr(name.Id);
            }
        }

        public void Dispose()
        {
            if (allocated)
                Marshal.FreeHGlobal(Value);
        }
    }
}
