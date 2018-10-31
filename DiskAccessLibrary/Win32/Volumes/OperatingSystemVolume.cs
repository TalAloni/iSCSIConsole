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
using Utilities;

namespace DiskAccessLibrary
{
    public class OperatingSystemVolume : Volume
    {
        private Guid m_volumeGuid;
        private int m_bytesPerSector;
        private long m_size;
        private bool m_isReadOnly;

        public OperatingSystemVolume(Guid volumeGuid, int bytesPerSector, long size)  : this(volumeGuid, bytesPerSector, size, false)
        { 
        }

        public OperatingSystemVolume(Guid volumeGuid, int bytesPerSector, long size, bool isReadOnly)
        {
            m_volumeGuid = volumeGuid;
            m_bytesPerSector = bytesPerSector;
            m_size = size;
            m_isReadOnly = isReadOnly;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            if (sectorCount > PhysicalDisk.MaximumDirectTransferSizeLBA)
            {
                // we must read one segment at the time, and copy the segments to a big bufffer
                byte[] buffer = new byte[sectorCount * m_bytesPerSector];
                for (int sectorOffset = 0; sectorOffset < sectorCount; sectorOffset += PhysicalDisk.MaximumDirectTransferSizeLBA)
                {
                    int leftToRead = sectorCount - sectorOffset;
                    int sectorsToRead = (int)Math.Min(leftToRead, PhysicalDisk.MaximumDirectTransferSizeLBA);
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
            SafeFileHandle handle = VolumeHandlePool.ObtainHandle(m_volumeGuid, FileAccess.Read, ShareMode.ReadWrite, out releaseHandle);
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
                        VolumeHandlePool.ReleaseHandle(m_volumeGuid);
                    }
                }
                return buffer;
            }
            else
            {
                // we always release invalid handle
                VolumeHandlePool.ReleaseHandle(m_volumeGuid);
                // get error code and throw
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Can't read sector {0} from volume {1}, Win32 Error: {2}", sectorIndex, m_volumeGuid, errorCode);
                throw new IOException(message);
            }
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (data.Length % m_bytesPerSector > 0)
            {
                throw new IOException("Cannot write partial sectors");
            }
            int sectorCount = data.Length / m_bytesPerSector;
            if (sectorCount > PhysicalDisk.MaximumDirectTransferSizeLBA)
            {
                // we must write one segment at the time
                for (int sectorOffset = 0; sectorOffset < sectorCount; sectorOffset += PhysicalDisk.MaximumDirectTransferSizeLBA)
                {
                    int leftToWrite = sectorCount - sectorOffset;
                    int sectorsToWrite = (int)Math.Min(leftToWrite, PhysicalDisk.MaximumDirectTransferSizeLBA);
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

            if (!m_isReadOnly)
            {
                bool releaseHandle;
                SafeFileHandle handle = VolumeHandlePool.ObtainHandle(m_volumeGuid, FileAccess.ReadWrite, ShareMode.None, out releaseHandle);
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
                            VolumeHandlePool.ReleaseHandle(m_volumeGuid);
                        }
                    }
                }
                else
                {
                    // we always release invalid handle
                    VolumeHandlePool.ReleaseHandle(m_volumeGuid);
                    // get error code and throw
                    int errorCode = Marshal.GetLastWin32Error();
                    string message = String.Format("Can't write to sector {0} of volume {1}, Win32 errorCode: {2}", sectorIndex, m_volumeGuid, errorCode);
                    throw new IOException(message);
                }
            }
        }

        /// <summary>
        /// Volume should be locked at this point for this call to have any effect
        /// </summary>
        /// <returns></returns>
        public bool AllowExtendedIO()
        { 
            bool releaseHandle;
            SafeFileHandle handle = VolumeHandlePool.ObtainHandle(m_volumeGuid, FileAccess.ReadWrite, ShareMode.None, out releaseHandle);
            if (!handle.IsInvalid)
            {
                bool result = VolumeControl.AllowExtendedIO(handle);
                if (releaseHandle)
                {
                    VolumeHandlePool.ReleaseHandle(m_volumeGuid);
                }
                return result;
            }
            else
            {
                // we always release invalid handle
                VolumeHandlePool.ReleaseHandle(m_volumeGuid);
                return false;
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

        public Guid VolumeGuid
        {
            get 
            {
                return m_volumeGuid;
            }
        }

        public override List<DiskExtent> Extents
        {
            get 
            {
                return new List<DiskExtent>();
            }
        }
    }
}
