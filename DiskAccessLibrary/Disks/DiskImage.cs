/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary
{
    public abstract partial class DiskImage : Disk
    {
        private string m_path;
        private bool m_isReadOnly;

        public DiskImage(string diskImagePath) : this(diskImagePath, false)
        {
        }

        public DiskImage(string diskImagePath, bool isReadOnly)
        {
            m_path = diskImagePath;
            m_isReadOnly = isReadOnly;
        }

        public void CheckBoundaries(long sectorIndex, int sectorCount)
        {
            if (sectorIndex < 0 || sectorIndex + (sectorCount - 1) >= this.TotalSectors)
            {
                throw new ArgumentOutOfRangeException("Attempted to access data outside of disk");
            }
        }

        public abstract void Extend(long numberOfAdditionalBytes);

        public abstract bool ExclusiveLock();

#if Win32
        public abstract bool ExclusiveLock(bool useOverlappedIO);
#endif

        public abstract bool ReleaseLock();

        public string Path
        {
            get
            {
                return m_path;
            }
        }

        public override bool IsReadOnly
        {
            get
            {
                return m_isReadOnly;
            }
        }

        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public static DiskImage GetDiskImage(string path)
        {
            return GetDiskImage(path, false);
        }

        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public static DiskImage GetDiskImage(string path, bool isReadOnly)
        {
            if (path.EndsWith(".vhd", StringComparison.InvariantCultureIgnoreCase))
            {
                return new VirtualHardDisk(path, isReadOnly);
            }
            else if (path.EndsWith(".vmdk", StringComparison.InvariantCultureIgnoreCase))
            {
                return new VirtualMachineDisk(path, isReadOnly);
            }
            else
            {
                return new RawDiskImage(path, isReadOnly);
            }
        }
    }
}
