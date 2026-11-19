// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;

namespace SevenZipThemeManager
{
    // One image inside a .ico file. Header holds the first 12 bytes of its directory entry
    // (width, height, colours, reserved, planes, bit count, byte count), which is also the
    // first 12 bytes of the matching RT_GROUP_ICON entry.
    internal sealed class IconImage
    {
        public byte[] Header;
        public byte[] Data;
    }

    internal static class IconFile
    {
        public static List<IconImage> Load(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < 6 || BitConverter.ToUInt16(file, 0) != 0 || BitConverter.ToUInt16(file, 2) != 1)
                throw new InvalidDataException(Path.GetFileName(path) + " is not an icon file");

            int count = BitConverter.ToUInt16(file, 4);
            if (count == 0 || file.Length < 6 + 16 * count)
                throw new InvalidDataException(Path.GetFileName(path) + " has no usable images");

            List<IconImage> images = new List<IconImage>();
            for (int i = 0; i < count; i++)
            {
                int entry = 6 + 16 * i;
                int length = BitConverter.ToInt32(file, entry + 8);
                int offset = BitConverter.ToInt32(file, entry + 12);
                if (length <= 0 || offset < 0 || offset + length > file.Length)
                    throw new InvalidDataException(Path.GetFileName(path) + " is damaged (image " + (i + 1) + " points outside the file)");

                IconImage image = new IconImage();
                image.Header = new byte[12];
                Buffer.BlockCopy(file, entry, image.Header, 0, 12);
                image.Data = new byte[length];
                Buffer.BlockCopy(file, offset, image.Data, 0, length);
                images.Add(image);
            }
            return images;
        }

        // Builds RT_GROUP_ICON data: GRPICONDIR followed by 14-byte entries pointing at RT_ICON ids
        public static byte[] BuildGroup(List<IconImage> images, List<int> iconIds)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                writer.Write(images[i].Header, 0, 8);
                writer.Write((uint)images[i].Data.Length);
                writer.Write((ushort)iconIds[i]);
            }
            writer.Flush();
            return stream.ToArray();
        }

        // RT_ICON ids referenced by existing RT_GROUP_ICON data
        public static List<int> GroupIconIds(byte[] group)
        {
            List<int> ids = new List<int>();
            if (group.Length < 6)
                return ids;
            int count = BitConverter.ToUInt16(group, 4);
            for (int i = 0; i < count && 6 + 14 * i + 14 <= group.Length; i++)
                ids.Add(BitConverter.ToUInt16(group, 6 + 14 * i + 12));
            return ids;
        }
    }
}
