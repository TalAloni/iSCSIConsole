/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class DynamicDiskExtentHelper
    {
        public static int GetIndexOfExtentID(List<DynamicDiskExtent> extents, ulong extentID)
        {
            for (int index = 0; index < extents.Count; index++)
            {
                if (extents[index].ExtentID == extentID)
                {
                    return index;
                }
            }
            return -1;
        }

        public static DynamicDiskExtent GetByExtentID(List<DynamicDiskExtent> extents, ulong extentID)
        {
            int index = GetIndexOfExtentID(extents, extentID);
            if (index >= 0)
            {
                return extents[index];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Support null disks
        /// </summary>
        public static DynamicDiskExtent GetDiskExtent(DynamicDisk dynamicDisk, ExtentRecord extentRecord)
        {
            long extentStartSector = GetExtentStartSector(dynamicDisk, extentRecord);
            long extentSize = (long)extentRecord.SizeLBA * PublicRegionHelper.BytesPerPublicRegionSector;
            Disk disk = null;
            Guid diskGuid = Guid.Empty;
            if (dynamicDisk != null)
            {
                disk = dynamicDisk.Disk;
                diskGuid = dynamicDisk.DiskGuid;
            }
            DynamicDiskExtent extent = new DynamicDiskExtent(disk, extentStartSector, extentSize, extentRecord.ExtentId);
            extent.Name = extentRecord.Name;
            extent.DiskGuid = diskGuid;
            return extent;
        }

        /// <summary>
        /// Support null disks
        /// </summary>
        public static long GetExtentStartSector(DynamicDisk disk, ExtentRecord extentRecord)
        {
            long publicRegionStartLBA = 0;
            int bytesPerDiskSector = DynamicColumn.DefaultBytesPerSector; // default for missing disks
            if (disk != null)
            {
                bytesPerDiskSector = disk.BytesPerSector;
                PrivateHeader privateHeader = disk.PrivateHeader;
                publicRegionStartLBA = (long)privateHeader.PublicRegionStartLBA;
            }
            return PublicRegionHelper.TranslateFromPublicRegionLBA((long)extentRecord.DiskOffsetLBA, publicRegionStartLBA, bytesPerDiskSector);
        }
    }
}
