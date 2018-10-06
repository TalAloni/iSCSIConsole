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
    public class FileStreamUtils
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileValidData(SafeFileHandle handle, long validDataLength);

        /// <summary>
        /// On NTFS, extending a file reserves disk space but does not zero out the data.
        /// Instead, NTFS keeps track of the "last byte written", technically known as the valid data length, and only zeroes out up to that point.
        /// The data past the valid data length are logically zero but are not physically zero on disk.
        /// When you write to a point past the current valid data length, all the bytes between the valid data length and the start of your write need to be zeroed out before the new valid data length can be set to the end of your write operation.
        /// Extending the file and then calling SetValidLength() may save a considerable amount of time zeroing out the extended portion of the file.
        /// </summary>
        /// <remarks>
        /// Calling SetFileValidData requires SeManageVolumePrivilege privileges.
        /// </remarks>
        public static bool SetValidLength(FileStream fileStream, long validDataLength)
        {
            bool success = SetFileValidData(fileStream.SafeFileHandle, validDataLength);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Unable to set valid file length, Win32 Error: {0}", errorCode);
                throw new IOException(message);
            }
            return success;
        }
    }
}
