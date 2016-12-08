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
    // FileStream reads will try to fill the internal buffer,
    // this may cause issues when reading the last few sectors of the disk,
    // (FileStream will try to read sectors that do not exist).
    // This can be solved by setting the FileStream buffer size to the sector size.
    // An alternative is to use FileStreamEx which does not use an internal read buffer at all.
    public class FileStreamEx : FileStream
    {
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, uint numberOfBytesToRead, out uint numberOfBytesRead, IntPtr lpOverlapped);

        private bool m_releaseHandle = true;

        public FileStreamEx(SafeFileHandle handle, FileAccess access) : base(handle, access)
        {

        }

        /// <param name="offset">The byte offset in array at which the read bytes will be placed</param>
        /// <param name="count">The maximum number of bytes to read</param>
        public override int Read(byte[] array, int offset, int count)
        {
            uint result;
            if (offset == 0)
            {
                ReadFile(this.SafeFileHandle, array, (uint)count, out result, IntPtr.Zero);
            }
            else
            {
                byte[] buffer = new byte[count];
                ReadFile(this.SafeFileHandle, buffer, (uint)buffer.Length, out result, IntPtr.Zero);
                Array.Copy(buffer, 0, array, offset, buffer.Length);
            }
            if (count == result)
            {
                return (int)result;
            }
            else
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Could not read from position {0} the requested number of bytes ({1}).", this.Position, count);
                ThrowIOError(errorCode, message);
                return 0; // this line will not be reached
            }
        }

        internal static void ThrowIOError(int errorCode, string defaultMessage)
        {
            if (errorCode == (int)Win32Error.ERROR_ACCESS_DENIED)
            {
                // UnauthorizedAccessException will be thrown if stream was opened only for writing or if a user is not an administrator
                throw new UnauthorizedAccessException(defaultMessage);
            }
            else if (errorCode == (int)Win32Error.ERROR_SHARING_VIOLATION)
            {
                throw new SharingViolationException(defaultMessage);
            }
            else if (errorCode == (int)Win32Error.ERROR_SECTOR_NOT_FOUND)
            {
                string message = defaultMessage + " The sector does not exist.";
                throw new IOException(message, (int)Win32Error.ERROR_SECTOR_NOT_FOUND);
            }
            else if (errorCode == (int)Win32Error.ERROR_CRC)
            {
                string message = defaultMessage + " Data Error (Cyclic Redundancy Check).";
                throw new CyclicRedundancyCheckException(message);
            }
            else if (errorCode == (int)Win32Error.ERROR_NO_SYSTEM_RESOURCES)
            {
                throw new OutOfMemoryException();
            }
            else
            {
                string message = defaultMessage + String.Format(" Win32 Error: {0}", errorCode);
                throw new IOException(message, errorCode);
            }
        }

        // we are working with disks, and we are only supposed to read sectors
        public override int ReadByte()
        {
            throw new NotImplementedException("Cannot read a single byte from disk");
        }

        public void Close(bool releaseHandle)
        {
            m_releaseHandle = releaseHandle;
            this.Close();
        }

        /// <summary>
        /// This will prevent the handle from being disposed
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (m_releaseHandle)
            {
                base.Dispose(disposing);
            }
            else
            {
                try
                {
                    this.Flush();
                }
                catch
                { 
                }
            }
        }
    }
}
