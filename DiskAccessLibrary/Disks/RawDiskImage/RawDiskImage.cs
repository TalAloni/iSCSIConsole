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
        public RawDiskImage(string rawDiskImagePath) : base(rawDiskImagePath)
        {
        }

        /// <summary>
        /// Sector refers to physical disk sector, we can only read complete sectors
        /// </summary>
        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            // We should use noncached I/O operations in case KB981166 is not installed on the host.
            FileStream stream = new FileStream(this.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.WriteThrough);
            long offset = sectorIndex * BytesPerSector;
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] result = new byte[BytesPerSector * sectorCount];
            stream.Read(result, 0, BytesPerSector * sectorCount);
            stream.Close();
            return result;
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a readonly disk");
            }

            CheckBoundaries(sectorIndex, data.Length / this.BytesPerSector);
            // We must avoid using the file system cache for writing, using it will negatively affect the performance and reliability.
            // Note: once the file system cache is filled, Windows may delay any (cache-dependent) pending write operations, which will create a deadlock.
            FileStream stream = new FileStream(this.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x1000, FileOptions.WriteThrough);
            long offset = sectorIndex * BytesPerSector;
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(data, 0, data.Length);
            stream.Close();
        }

        public override void Extend(long additionalNumberOfBytes)
        {
            if (additionalNumberOfBytes % this.BytesPerSector > 0)
            {
                throw new ArgumentException("additionalNumberOfBytes must be a multiple of BytesPerSector");
            }

            long length = this.Size;
            FileStream stream = new FileStream(this.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x1000, FileOptions.WriteThrough);
            stream.SetLength(length + additionalNumberOfBytes);
            stream.Close();
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
