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
    public class VolumeControl
    {
        private const uint FSCTL_IS_VOLUME_MOUNTED = 0x90028;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x90020;
        private const uint FSCTL_LOCK_VOLUME = 0x90018;
        private const uint FSCTL_UNLOCK_VOLUME = 0x9001C;
        private const uint FSCTL_ALLOW_EXTENDED_DASD_IO = 0x90083;

        public const int MaxPath = 260;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetVolumePathNamesForVolumeNameW(
                [MarshalAs(UnmanagedType.LPWStr)]
        string lpszVolumeName,
                [MarshalAs(UnmanagedType.LPWStr)]
        string lpszVolumePathNames,
            uint cchBuferLength,
            ref uint lpcchReturnLength);

        /// <summary>
        /// e.g.
        /// D:\
        /// E:\MountPoint\
        /// </summary>
        public static List<string> GetVolumeMountPoints(Guid volumeGuid)
        { 
            string volumeGuidPath = String.Format(@"\\?\Volume{0}\", volumeGuid.ToString("B"));
            string lpszVolumeName = new string(new char[MaxPath]);
            uint returnLength = 0;
            bool success = GetVolumePathNamesForVolumeNameW(volumeGuidPath, lpszVolumeName, (uint)lpszVolumeName.Length, ref returnLength);

            if (success)
            {
                lpszVolumeName = lpszVolumeName.Substring(0, (int)returnLength);
            }
            else
            {
                if (Marshal.GetLastWin32Error() == (int)Win32Error.ERROR_MORE_DATA)
                {
                    lpszVolumeName = new string(new char[returnLength]);
                    success = GetVolumePathNamesForVolumeNameW(volumeGuidPath, lpszVolumeName, (uint)lpszVolumeName.Length, ref returnLength);
                }
            }

            if (success)
            {
                lpszVolumeName = lpszVolumeName.TrimEnd('\0');
                if (lpszVolumeName != String.Empty)
                {
                    List<string> result = new List<string>(lpszVolumeName.Split('\0'));
                    return result;
                }
            }
            return new List<string>();
        }

        public static bool IsVolumeMounted(char driveLetter)
        {
            SafeFileHandle handle = HandleUtils.GetVolumeHandle(driveLetter, FileAccess.Read, ShareMode.ReadWrite);
            return IsVolumeMounted(handle);
        }

        public static bool IsVolumeMounted(string path)
        {
            SafeFileHandle handle = HandleUtils.GetVolumeHandle(path, FileAccess.Read, ShareMode.ReadWrite);
            return IsVolumeMounted(handle);
        }

        public static bool IsVolumeMounted(Guid volumeGuid)
        {
            SafeFileHandle handle = HandleUtils.GetVolumeHandle(volumeGuid, FileAccess.Read, ShareMode.ReadWrite);
            return IsVolumeMounted(handle);
        }

        /// <param name="handle">When opening a volume, the dwShareMode parameter must have the FILE_SHARE_WRITE flag.</param>
        public static bool IsVolumeMounted(SafeFileHandle handle)
        {
            if (!handle.IsInvalid)
            {
                uint dummy;
                bool mounted = PhysicalDiskControl.DeviceIoControl(handle, FSCTL_IS_VOLUME_MOUNTED, IntPtr.Zero, 0, IntPtr.Zero, 0, out dummy, IntPtr.Zero);
                handle.Close();
                return mounted;
            }
            else
            {
                return false;
            }
        }

        // By locking the volume before you dismount it, you can ensure that the volume is dismounted cleanly
        // (because the system flushes all cached data to the volume before locking it)
        //
        // A locked volume remains locked until one of the following occurs:
        // * The application uses the FSCTL_UNLOCK_VOLUME control code to unlock the volume.
        // * The handle closes, either directly through CloseHandle, or indirectly when a process terminates.
        // http://msdn.microsoft.com/en-us/library/bb521494(v=winembedded.5).aspx
        // http://msdn.microsoft.com/en-us/library/Aa364575
        /// <param name="handle">
        /// The application must specify the FILE_SHARE_READ and FILE_SHARE_WRITE flags in the dwShareMode parameter of CreateFile.
        /// https://msdn.microsoft.com/en-us/library/Aa364575
        /// </param>
        public static bool LockVolume(SafeFileHandle handle)
        {
            if (!handle.IsInvalid)
            {
                uint dummy;
                bool success = PhysicalDiskControl.DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out dummy, IntPtr.Zero);
                return success;
            }
            else
            {
                return false;
            }
        }

        // Forced dismount only does FSCTL_DISMOUNT_VOLUME (without locking the volume first),
        // and then does the formatting write/verify operations _from this very handle_.
        // Then the handle is just closed, and any next attempt to access the volume will automount it.
        /// <param name="handle">
        /// The application must specify the FILE_SHARE_READ and FILE_SHARE_WRITE flags in the dwShareMode parameter of CreateFile.
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa364562%28v=vs.85%29.aspx
        /// </param>
        public static bool DismountVolume(SafeFileHandle handle)
        {
            if (!handle.IsInvalid)
            {
                uint dummy;
                bool success = PhysicalDiskControl.DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out dummy, IntPtr.Zero);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == (int)Win32Error.ERROR_ACCESS_DENIED)
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
                return success;
            }
            else
            {
                return false;
            }
        }

        public static bool AllowExtendedIO(SafeFileHandle handle)
        {
            if (!handle.IsInvalid)
            {
                uint dummy;
                bool success = PhysicalDiskControl.DeviceIoControl(handle, FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0, IntPtr.Zero, 0, out dummy, IntPtr.Zero);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == (int)Win32Error.ERROR_ACCESS_DENIED)
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
                return success;
            }
            else
            {
                return false;
            }
        }
    }
}
