/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary
{
    public class DiskExtentsHelper
    {
        /// <param name="dataRegionSize">In bytes</param>
        internal static List<DiskExtent> GetUnallocatedExtents(Disk disk, long dataRegionStartSector, long dataRegionSize, List<DiskExtent> usedExtents)
        {
            List<DiskExtent> result = new List<DiskExtent>();
            long startSector = dataRegionStartSector;
            SortExtentsByFirstSector(usedExtents);
            // see if there is room before each extent
            foreach (DiskExtent extent in usedExtents)
            {
                long extentStartSector = extent.FirstSector;
                long nextStartSector = extent.FirstSector + extent.Size / disk.BytesPerSector;
                long freeSpaceInBytes = (extentStartSector - startSector) * disk.BytesPerSector;
                if (freeSpaceInBytes > 0)
                {
                    result.Add(new DiskExtent(disk, startSector, freeSpaceInBytes));
                }

                startSector = nextStartSector;
            }

            // see if there is room after the last extent
            long spaceInBytes = dataRegionSize - (startSector - dataRegionStartSector) * disk.BytesPerSector;
            if (spaceInBytes > 0)
            {
                result.Add(new DiskExtent(disk, startSector, spaceInBytes));
            }
            return result;
        }

        /// <summary>
        /// Sort (in-place) extents by first sector
        /// </summary>
        public static void SortExtentsByFirstSector(List<DiskExtent> extents)
        {
            SortedList<long, DiskExtent> list = new SortedList<long, DiskExtent>();
            foreach (DiskExtent extent in extents)
            {
                list.Add(extent.FirstSector, extent);
            }

            extents.Clear();
            extents.AddRange(list.Values);
        }
    }
}
