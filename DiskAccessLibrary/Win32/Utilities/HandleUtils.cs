/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DiskAccessLibrary
{
    public enum ShareMode : uint
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        ReadWrite = 0x3,
        Delete = 0x4,
    }

    public class HandleUtils
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, SafeFileHandle hTemplateFile);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;

        //private const uint FILE_SHARE_READ = 0x00000001;
        //private const uint FILE_SHARE_WRITE = 0x00000002;

        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000; // needed for directories


        public static SafeFileHandle GetDiskHandle(int physicalDiskIndex, FileAccess access, ShareMode shareMode)
        {
            string fileName = String.Format(@"\\.\PhysicalDrive{0}", physicalDiskIndex);
            return GetFileHandle(fileName, access, shareMode);
        }

        public static SafeFileHandle GetVolumeHandle(char driveLetter, FileAccess access, ShareMode shareMode)
        {
            string fileName = String.Format(@"\\.\{0}:", driveLetter);
            return GetFileHandle(fileName, access, shareMode);
        }

        public static SafeFileHandle GetVolumeHandle(string path, FileAccess access, ShareMode shareMode)
        {
            path = path.TrimEnd('\\');
            string fileName = String.Format(@"\\.\{0}", path);
            return GetFileHandle(fileName, access, shareMode, true);
        }

        public static SafeFileHandle GetVolumeHandle(Guid volumeGuid, FileAccess access, ShareMode shareMode)
        {
            string fileName = String.Format(@"\\?\Volume{0}", volumeGuid.ToString("B"));
            return GetFileHandle(fileName, access, shareMode);
        }

        public static SafeFileHandle GetFileHandle(string fileName, FileAccess access, ShareMode shareMode)
        {
            return GetFileHandle(fileName, access, shareMode, false);
        }

        public static SafeFileHandle GetFileHandle(string fileName, FileAccess access, ShareMode shareMode, bool isDirectory)
        {
            uint desiredAccess = GetDesiredAccess(access);

            uint flags = FILE_ATTRIBUTE_NORMAL;
            if (isDirectory)
            {
                flags |= FILE_FLAG_BACKUP_SEMANTICS;
            }
            SafeFileHandle handle = CreateFile(fileName, desiredAccess, (uint)shareMode, IntPtr.Zero, OPEN_EXISTING, flags, new SafeFileHandle(IntPtr.Zero, true));
            return handle;
        }

        private static uint GetDesiredAccess(FileAccess access)
        {
            switch (access)
            {
                case FileAccess.Read:
                    return GENERIC_READ;
                case FileAccess.Write:
                    return GENERIC_WRITE;
                case FileAccess.ReadWrite:
                    return GENERIC_READ | GENERIC_WRITE;
                default:
                    return GENERIC_READ;
            }
        }
    }
}
