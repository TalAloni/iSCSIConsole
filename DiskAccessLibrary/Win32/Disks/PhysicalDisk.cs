/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Utilities;

namespace DiskAccessLibrary
{
    public class PhysicalDisk : Disk, IDiskGeometry
    {
        // ReadFile failed with ERROR_INVALID_FUNCTION when transfer size was > 4880 sectors when working with an iSCSI drive
        // Note: The size of the internal buffer has no meaningful impact on performance, instead you should look at MaximumTransferSizeLBA.
        public const int MaximumDirectTransferSizeLBA = 2048; // 1 MB (assuming 512-byte sectors)

        private int m_physicalDiskIndex;
        private int m_bytesPerSector;
        private long m_size;
        private bool m_isReadOnly;
        private string m_description;
        private string m_serialNumber;

        // CHS:
        private long m_cylinders;
        private int m_tracksPerCylinder; // a.k.a. heads
        private int m_sectorsPerTrack;

        public PhysicalDisk(int physicalDiskIndex) : this(physicalDiskIndex, false)
        {
        }

        public PhysicalDisk(int physicalDiskIndex, bool isReadOnly)
        {
            m_physicalDiskIndex = physicalDiskIndex;
            m_isReadOnly = isReadOnly;
            PopulateDiskInfo(); // We must do it before any read request use the disk handle
            PopulateDescription();
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            if (sectorCount > MaximumDirectTransferSizeLBA)
            {
                // we must read one segment at the time, and copy the segments to a big bufffer
                byte[] buffer = new byte[sectorCount * m_bytesPerSector];
                for (int sectorOffset = 0; sectorOffset < sectorCount; sectorOffset += MaximumDirectTransferSizeLBA)
                {
                    int leftToRead = sectorCount - sectorOffset;
                    int sectorsToRead = (int)Math.Min(leftToRead, MaximumDirectTransferSizeLBA);
                    byte[] segment = ReadSectorsUnbuffered(sectorIndex + sectorOffset, sectorsToRead);
                    Array.Copy(segment, 0, buffer, sectorOffset * m_bytesPerSector, segment.Length);
                }
                return buffer;
            }
            else
            {
                return ReadSectorsUnbuffered(sectorIndex, sectorCount);
            }
        }

        /// <summary>
        /// Sector refers to physical disk blocks, we can only read complete blocks
        /// </summary>
        public byte[] ReadSectorsUnbuffered(long sectorIndex, int sectorCount)
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.Read, ShareMode.ReadWrite, out releaseHandle);
            if (!handle.IsInvalid)
            {
                FileStreamEx stream = new FileStreamEx(handle, FileAccess.Read);
                byte[] buffer = new byte[m_bytesPerSector * sectorCount];
                try
                {
                    stream.Seek(sectorIndex * m_bytesPerSector, SeekOrigin.Begin);
                    stream.Read(buffer, 0, m_bytesPerSector * sectorCount);
                }
                finally
                {
                    stream.Close(releaseHandle);
                    if (releaseHandle)
                    {
                        PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                    }
                }
                return buffer;
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                // get error code and throw
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to read sector {0} from disk {1}.", sectorIndex, m_physicalDiskIndex);
                IOExceptionHelper.ThrowIOError(errorCode, message);
                return null; // this line will not be reached
            }
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (data.Length % m_bytesPerSector > 0)
            {
                throw new IOException("Cannot write partial sectors");
            }
            int sectorCount = data.Length / m_bytesPerSector;
            if (sectorCount > MaximumDirectTransferSizeLBA)
            {
                // we must write one segment at the time
                for (int sectorOffset = 0; sectorOffset < sectorCount; sectorOffset += MaximumDirectTransferSizeLBA)
                {
                    int leftToWrite = sectorCount - sectorOffset;
                    int sectorsToWrite = (int)Math.Min(leftToWrite, MaximumDirectTransferSizeLBA);
                    byte[] segment = new byte[sectorsToWrite * m_bytesPerSector];
                    Array.Copy(data, sectorOffset * m_bytesPerSector, segment, 0, sectorsToWrite * m_bytesPerSector);
                    WriteSectorsUnbuffered(sectorIndex + sectorOffset, segment);
                }
            }
            else
            {
                WriteSectorsUnbuffered(sectorIndex, data);
            }
        }

        public void WriteSectorsUnbuffered(long sectorIndex, byte[] data)
        {
            if (data.Length % m_bytesPerSector > 0)
            {
                throw new IOException("Cannot write partial sectors");
            }

            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a readonly disk");
            }

            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.ReadWrite, ShareMode.Read, out releaseHandle);
            if (!handle.IsInvalid)
            {
                FileStreamEx stream = new FileStreamEx(handle, FileAccess.Write);
                try
                {
                    stream.Seek(sectorIndex * m_bytesPerSector, SeekOrigin.Begin);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
                finally
                {
                    stream.Close(releaseHandle);
                    if (releaseHandle)
                    {
                        PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                    }
                }
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                // get error code and throw
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to write to sector {0} of disk {1}.", sectorIndex, m_physicalDiskIndex);
                IOExceptionHelper.ThrowIOError(errorCode, message);
            }
        }

        public bool ExclusiveLock()
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.ReadWrite, ShareMode.Read, out releaseHandle);
            if (releaseHandle) // new allocation
            {
                if (!handle.IsInvalid)
                {
                    return true;
                }
                else
                {
                    // we always release invalid handle
                    PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public bool ReleaseLock()
        {
            return PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
        }

        /// <summary>
        /// Invalidates the cached partition table and re-enumerates the device
        /// </summary>
        /// <exception cref="System.IO.IOException"></exception>
        public void UpdateProperties()
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.ReadWrite, ShareMode.Read, out releaseHandle);
            if (!handle.IsInvalid)
            {
                bool success = PhysicalDiskControl.UpdateDiskProperties(handle);
                if (!success)
                {
                    throw new IOException("Failed to update disk properties");
                }
                if (releaseHandle)
                {
                    PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                }
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
            }
        }

        private void PopulateDiskInfo()
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.Read, ShareMode.ReadWrite, out releaseHandle);
            if (!handle.IsInvalid)
            {
                if (!PhysicalDiskControl.IsMediaAccesible(handle))
                {
                    throw new DeviceNotReadyException();
                }
                DISK_GEOMETRY diskGeometry = PhysicalDiskControl.GetDiskGeometryAndSize(handle, out m_size);
                if (releaseHandle)
                {
                    PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                }
                m_bytesPerSector = (int)diskGeometry.BytesPerSector;

                // CHS:
                m_cylinders = diskGeometry.Cylinders;
                m_tracksPerCylinder = (int)diskGeometry.TracksPerCylinder;
                m_sectorsPerTrack = (int)diskGeometry.SectorsPerTrack;
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);

                // get error code and throw
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to access disk {0}.", m_physicalDiskIndex);
                if (errorCode == (int)Win32Error.ERROR_FILE_NOT_FOUND)
                {
                    throw new DriveNotFoundException(message);
                }
                else
                {
                    IOExceptionHelper.ThrowIOError(errorCode, message);
                }
            }
        }

        private void PopulateDescription()
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.Read, ShareMode.ReadWrite, out releaseHandle);
            if (!handle.IsInvalid)
            {
                m_description = PhysicalDiskControl.GetDeviceDescription(handle);
                m_serialNumber = PhysicalDiskControl.GetDeviceSerialNumber(handle);
                if (releaseHandle)
                {
                    PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                }
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);

                // get error code and throw
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to access disk {0}.", m_physicalDiskIndex);
                if (errorCode == (int)Win32Error.ERROR_FILE_NOT_FOUND)
                {
                    throw new DriveNotFoundException(message);
                }
                else
                {
                    IOExceptionHelper.ThrowIOError(errorCode, message);
                }
            }
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        public bool GetOnlineStatus()
        {
            bool isReadOnly;
            return GetOnlineStatus(out isReadOnly);
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        /// <exception cref="System.IO.IOException"></exception>
        public bool GetOnlineStatus(out bool isReadOnly)
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.ReadWrite, ShareMode.Read, out releaseHandle);
            if (!handle.IsInvalid)
            {
                bool isOnline = PhysicalDiskControl.GetOnlineStatus(handle, out isReadOnly);
                if (releaseHandle)
                {
                    PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                }
                return isOnline;
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);

                // get error code and throw
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Failed to get disk {0} online status, Win32 Error: {1}", m_physicalDiskIndex, errorCode);
                throw new IOException(message);
            }
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        public bool SetOnlineStatus(bool online)
        {
            return SetOnlineStatus(online, false);
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        /// <exception cref="System.IO.IOException"></exception>
        public bool SetOnlineStatus(bool online, bool persist)
        {
            bool releaseHandle;
            SafeFileHandle handle = PhysicalDiskHandlePool.ObtainHandle(m_physicalDiskIndex, FileAccess.ReadWrite, ShareMode.Read, out releaseHandle);
            if (!handle.IsInvalid)
            {
                bool success = PhysicalDiskControl.SetOnlineStatus(handle, online, persist);
                if (releaseHandle)
                {
                    PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);
                }
                return success;
            }
            else
            {
                // we always release invalid handle
                PhysicalDiskHandlePool.ReleaseHandle(m_physicalDiskIndex);

                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode == (int)Win32Error.ERROR_SHARING_VIOLATION)
                {
                    return false;
                }
                else
                {
                    string message = String.Format("Failed to take disk {0} offline, Win32 Error: {1}", m_physicalDiskIndex, errorCode);
                    throw new IOException(message);
                }
            }
        }

        public int PhysicalDiskIndex
        {
            get
            {
                return m_physicalDiskIndex;
            }
        }

        public override int BytesPerSector
        {
            get
            {
                return m_bytesPerSector;
            }
        }

        public override long Size
        {
            get
            {
                return m_size;
            }
        }

        public override bool IsReadOnly
        {
            get
            {
                return m_isReadOnly;
            }
        }

        public string Description
        {
            get
            {
                return m_description;
            }
        }

        public string SerialNumber
        {
            get
            {
                return m_serialNumber;
            }
        }

        public long Cylinders
        {
            get
            {
                return m_cylinders;
            }
        }

        /// <summary>
        /// a.k.a heads
        /// </summary>
        public int TracksPerCylinder
        {
            get
            {
                return m_tracksPerCylinder;
            }
        }

        public int SectorsPerTrack
        {
            get
            {
                return m_sectorsPerTrack;
            }
        }
    }
}
