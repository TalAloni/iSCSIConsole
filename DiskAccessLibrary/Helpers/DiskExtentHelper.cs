/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

namespace DiskAccessLibrary
{
    public class DiskExtentHelper
    {
        public static DiskExtent GetAlignedDiskExtent(DiskExtent extent, long alignInSectors)
        {
            long alignedStartSector = (long)Math.Ceiling((double)extent.FirstSector / alignInSectors) * alignInSectors;
            long lossDueToAlignment = (alignedStartSector - extent.FirstSector) * extent.BytesPerSector;
            return new DiskExtent(extent.Disk, alignedStartSector, extent.Size - lossDueToAlignment);
        }
    }
}