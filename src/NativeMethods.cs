// 7-Zip Theme Manager
// Copyright (C) 2026 7-Zip Theme Manager contributors
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Runtime.InteropServices;

namespace SevenZipThemeManager
{
    // Win32 calls used for rewriting PE resources and swapping files. All of them exist on Windows XP.
    internal static class NativeMethods
    {
        public const int RT_BITMAP = 2;
        public const int RT_ICON = 3;
        public const int RT_GROUP_ICON = 14;

        public const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;
        public const int SHCNE_ASSOCCHANGED = 0x08000000;
        public const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr BeginUpdateResource(string fileName, bool deleteExistingResources);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool EndUpdateResource(IntPtr update, bool discard);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool MoveFileEx(string existingFileName, string newFileName, uint flags);

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
