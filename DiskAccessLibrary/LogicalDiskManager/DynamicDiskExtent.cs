/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class DynamicDiskExtent : DiskExtent
    {
        private ulong m_extentID;
        public string Name;
        public Guid DiskGuid;

        public DynamicDiskExtent(Disk disk, long firstSector, long size, ulong extentID) : base(disk, firstSector, size)
        {
            m_extentID = extentID;
        }

        public DynamicDiskExtent(DiskExtent diskExtent, ulong extentID) : base(diskExtent.Disk, diskExtent.FirstSector, diskExtent.Size)
        {
            m_extentID = extentID;
        }

        public ulong ExtentID
        {
            get
            {
                return m_extentID;
            }
        }
    }
}
