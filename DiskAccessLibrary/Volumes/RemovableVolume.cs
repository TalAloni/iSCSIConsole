/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;
using DiskAccessLibrary.FileSystems.NTFS;

namespace DiskAccessLibrary
{
    /// <summary>
    /// A single volume that takes up all of the disk (there is no MBR),
    /// The disk has no partition table and the first sector contains the filesystem boot record.
    /// 
    /// MSDN definition:
    /// A super floppy layout is one in which there is no MBR, so there is no partition table. The entire disk (from start to end) is one giant partition.
    /// </summary>
    public class RemovableVolume : Partition
    {
        public RemovableVolume(DiskExtent extent) : base(extent)
        {
        }

        public RemovableVolume(Disk disk) : base(disk, 0, disk.Size)
        {
        }
    }
}
