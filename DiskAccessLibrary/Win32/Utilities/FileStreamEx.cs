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
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace DiskAccessLibrary
{
    /// <summary>
    /// This class was designed to offer unencumbered access to files. (Block devices such as disks, volumes and virtual disk files were specifically in mind).
    /// Unlike FileStream, this class does not implement any internal read or write buffering.
    /// </summary>
    /// <remarks>
    /// Note that it is possible to curcumvent FileStream's buffering by setting the buffer size to the sector size.
    /// </remarks>
    public class FileStreamEx : Stream
    {
        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, uint numberOfBytesToRead, out uint numberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle handle, byte[] buffer, uint numberOfBytesToWrite, out uint numberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileSizeEx(SafeFileHandle handle, out long fileSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFilePointerEx(SafeFileHandle handle, long distanceToMove, out long newFilePointer, uint moveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetEndOfFile(SafeFileHandle handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(SafeFileHandle handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileValidData(SafeFileHandle handle, long validDataLength);

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct OVERLAPPED
        {
            public UIntPtr Internal;
            public UIntPtr InternalHigh;
            public uint OffsetLow;
            public uint OffsetHigh;
            public IntPtr hEvent;

            public long Offset
            {
                set
                {
                    OffsetLow = (uint)value;
                    OffsetHigh = (uint)(value >> 32);
                }
            }
        }

        private SafeFileHandle m_handle;
        private bool m_canRead;
        private bool m_canWrite;
        private long m_position;
        private bool m_releaseHandle = true;

        public FileStreamEx(SafeFileHandle handle, FileAccess access)
        {
            m_handle = handle;
            m_canRead = (access & FileAccess.Read) != 0;
            m_canWrite = (access & FileAccess.Write) != 0;
        }

        /// <param name="offset">The byte offset in array at which the read bytes will be placed</param>
        /// <param name="count">The maximum number of bytes to read</param>
        public override int Read(byte[] array, int offset, int count)
        {
            uint numberOfBytesRead;
            if (offset == 0)
            {
                ReadFile(m_handle, array, (uint)count, out numberOfBytesRead, IntPtr.Zero);
            }
            else
            {
                byte[] buffer = new byte[count];
                ReadFile(m_handle, buffer, (uint)count, out numberOfBytesRead, IntPtr.Zero);
                Array.Copy(buffer, 0, array, offset, buffer.Length);
            }

            if (numberOfBytesRead == count)
            {
                m_position += count;
                return (int)numberOfBytesRead;
            }
            else
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to read from position {0} the requested number of bytes ({1}).", this.Position, count);
                IOExceptionHelper.ThrowIOError(errorCode, message);
                return 0; // this line will not be reached
            }
        }

        public override void Write(byte[] array, int offset, int count)
        {
            uint numberOfBytesWritten;
            if (offset == 0)
            {
                WriteFile(m_handle, array, (uint)count, out numberOfBytesWritten, IntPtr.Zero);
            }
            else
            {
                byte[] buffer = new byte[count];
                Array.Copy(array, offset, buffer, 0, buffer.Length);
                WriteFile(m_handle, buffer, (uint)count, out numberOfBytesWritten, IntPtr.Zero);
            }

            if (numberOfBytesWritten == count)
            {
                m_position += count;
            }
            else
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to write to position {0} the requested number of bytes ({1}).", this.Position, count);
                IOExceptionHelper.ThrowIOError(errorCode, message);
            }
        }

        /// <remarks>
        /// The caller can use this method with both synchronous and asynchronous (FILE_FLAG_OVERLAPPED) file handles.
        /// </remarks>
        public int ReadOverlapped(byte[] array, int offset, int count, long position)
        {
            uint temp; // will not be updated on async operation
            ManualResetEvent completionEvent = new ManualResetEvent(false);
            OVERLAPPED overlapped = new OVERLAPPED();
            overlapped.Offset = position;
            overlapped.hEvent = completionEvent.SafeWaitHandle.DangerousGetHandle();
            IntPtr lpOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf(overlapped));
            Marshal.StructureToPtr(overlapped, lpOverlapped, false);
            bool success;
            if (offset == 0)
            {
                success = ReadFile(m_handle, array, (uint)count, out temp, lpOverlapped);
            }
            else
            {
                byte[] buffer = new byte[count];
                success = ReadFile(m_handle, buffer, (uint)buffer.Length, out temp, lpOverlapped);
                Array.Copy(buffer, 0, array, offset, buffer.Length);
            }

            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != (int)Win32Error.ERROR_IO_PENDING)
                {
                    string message = String.Format("Failed to read from position {0} the requested number of bytes ({1}).", position, count);
                    IOExceptionHelper.ThrowIOError(errorCode, message);
                }
                bool completed = completionEvent.WaitOne();
            }
            Marshal.FreeHGlobal(lpOverlapped);
            return count;
        }

        /// <remarks>
        /// The caller can use this method with both synchronous and asynchronous (FILE_FLAG_OVERLAPPED) file handles.
        /// </remarks>
        public void WriteOverlapped(byte[] array, int offset, int count, long position)
        {
            uint temp; // will not be updated on async operation
            ManualResetEvent completionEvent = new ManualResetEvent(false);
            OVERLAPPED overlapped = new OVERLAPPED();
            overlapped.Offset = position;
            overlapped.hEvent = completionEvent.SafeWaitHandle.DangerousGetHandle();
            IntPtr lpOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf(overlapped));
            Marshal.StructureToPtr(overlapped, lpOverlapped, false);
            bool success;
            if (offset == 0)
            {
                success = WriteFile(m_handle, array, (uint)count, out temp, lpOverlapped);
            }
            else
            {
                byte[] buffer = new byte[count];
                Array.Copy(array, offset, buffer, 0, buffer.Length);
                success = WriteFile(m_handle, buffer, (uint)buffer.Length, out temp, lpOverlapped);
            }

            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != (int)Win32Error.ERROR_IO_PENDING)
                {
                    string message = String.Format("Failed to write to position {0} the requested number of bytes ({1}).", position, count);
                    IOExceptionHelper.ThrowIOError(errorCode, message);
                }
                bool completed = completionEvent.WaitOne();
            }
            Marshal.FreeHGlobal(lpOverlapped);
        }

        public override void Flush()
        {
            Flush(false);
        }

        public void Flush(bool flushToDisk)
        {
            if (flushToDisk)
            {
                bool success = FlushFileBuffers(m_handle);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    string message = "Failed to flush file buffers.";
                    IOExceptionHelper.ThrowIOError(errorCode, message);
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            bool success = SetFilePointerEx(m_handle, offset, out m_position, (uint)origin);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to seek to offset {0}, origin: {1}.", offset, origin);
                IOExceptionHelper.ThrowIOError(errorCode, message);
            }
            return m_position;
        }

        /// <remarks>we are working with block devices, and we are only supposed to read sectors</remarks>
        public override int ReadByte()
        {
            throw new NotImplementedException("Cannot read a single byte from disk");
        }

        /// <remarks>we are working with block devices, and we are only supposed to write sectors</remarks>
        public override void WriteByte(byte value)
        {
            throw new NotImplementedException("Cannot write a single byte to disk");
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
                if (!m_handle.IsClosed)
                {
                    m_handle.Close();
                    m_canRead = false;
                    m_canWrite = false;
                }
                base.Dispose(disposing);
            }
        }

        public override void SetLength(long value)
        {
            long position = m_position;
            Seek(value, SeekOrigin.Begin);
            bool success = SetEndOfFile(m_handle);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to set file length to {0}.", value);
                IOExceptionHelper.ThrowIOError(errorCode, message);
            }
            Seek(position, SeekOrigin.Begin);
        }

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
        public void SetValidLength(long value)
        {
            bool success = SetFileValidData(m_handle, value);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to set valid file length to {0}.", value);
                IOExceptionHelper.ThrowIOError(errorCode, message);
            }
        }

        public override long Length
        {
            get
            {
                long fileSize;
                bool success = GetFileSizeEx(m_handle, out fileSize);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    string message = "Failed to get file size.";
                    IOExceptionHelper.ThrowIOError(errorCode, message);
                }
                return fileSize;
            }
        }

        public override long Position
        {
            get
            {
                return m_position;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override bool CanRead
        {
            get
            {
                return m_canRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return m_canWrite;
            }
        }
    }
}
