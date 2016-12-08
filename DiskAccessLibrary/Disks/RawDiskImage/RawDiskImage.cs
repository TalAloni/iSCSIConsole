/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace DiskAccessLibrary
{
    public partial class RawDiskImage : DiskImage
    {
        const FileOptions FILE_FLAG_NO_BUFFERING = (FileOptions)0x20000000;
        private bool m_isExclusiveLock;
        private FileStream m_stream;

        public RawDiskImage(string rawDiskImagePath) : base(rawDiskImagePath)
        {
        }

        public override bool ExclusiveLock()
        {
            if (!m_isExclusiveLock)
            {
                m_isExclusiveLock = true;
                FileAccess fileAccess = IsReadOnly ? FileAccess.Read : FileAccess.ReadWrite;
                // We should use noncached I/O operations to avoid excessive RAM usage.
                // Note: KB99794 provides information about FILE_FLAG_WRITE_THROUGH and FILE_FLAG_NO_BUFFERING.
                m_stream = new FileStream(this.Path, FileMode.Open, fileAccess, FileShare.Read, 0x1000, FILE_FLAG_NO_BUFFERING | FileOptions.WriteThrough);
                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool ReleaseLock()
        {
            if (m_isExclusiveLock)
            {
                m_isExclusiveLock = false;
                m_stream.Close();
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Sector refers to physical disk sector, we can only read complete sectors
        /// </summary>
        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            if (!m_isExclusiveLock)
            {
                // We should use noncached I/O operations to avoid excessive RAM usage.
                // Note: KB99794 provides information about FILE_FLAG_WRITE_THROUGH and FILE_FLAG_NO_BUFFERING.
                m_stream = new FileStream(this.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FILE_FLAG_NO_BUFFERING | FileOptions.WriteThrough);
            }
            long offset = sectorIndex * BytesPerSector;
            m_stream.Seek(offset, SeekOrigin.Begin);
            byte[] result = new byte[BytesPerSector * sectorCount];
            m_stream.Read(result, 0, BytesPerSector * sectorCount);
            if (!m_isExclusiveLock)
            {
                m_stream.Close();
            }
            return result;
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a readonly disk");
            }

            CheckBoundaries(sectorIndex, data.Length / this.BytesPerSector);
            if (!m_isExclusiveLock)
            {
                // We should use noncached I/O operations to avoid excessive RAM usage.
                // We must avoid using buffered writes, using it will negatively affect the performance and reliability.
                // Note: once the file system write buffer is filled, Windows may delay any (buffer-dependent) pending write operations, which will create a deadlock.
                m_stream = new FileStream(this.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x1000, FILE_FLAG_NO_BUFFERING | FileOptions.WriteThrough);
            }
            long offset = sectorIndex * BytesPerSector;
            m_stream.Seek(offset, SeekOrigin.Begin);
            m_stream.Write(data, 0, data.Length);
            if (!m_isExclusiveLock)
            {
                m_stream.Close();
            }
        }

        public override void Extend(long additionalNumberOfBytes)
        {
            if (additionalNumberOfBytes % this.BytesPerSector > 0)
            {
                throw new ArgumentException("additionalNumberOfBytes must be a multiple of BytesPerSector");
            }

            long length = this.Size;
            if (!m_isExclusiveLock)
            {
                m_stream = new FileStream(this.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x1000, FILE_FLAG_NO_BUFFERING | FileOptions.WriteThrough);
            }
            m_stream.SetLength(length + additionalNumberOfBytes);
            if (m_isExclusiveLock)
            {
                m_stream.Close();
            }
        }

        public override int BytesPerSector
        {
            get
            {
                FileInfo info = new FileInfo(this.Path);
                string[] components = info.Name.Split('.');
                if (components.Length >= 3) // file.512.img
                {
                    string bytesPerSectorString = components[components.Length - 2];
                    int bytesPerSector = Conversion.ToInt32(bytesPerSectorString, BytesPerDiskImageSector);
                    return bytesPerSector;
                }
                else
                {
                    return BytesPerDiskImageSector;
                }
            }
        }

        public override long Size
        {
            get
            {
                FileInfo info = new FileInfo(this.Path);
                return info.Length;
            }
        }
    }
}
