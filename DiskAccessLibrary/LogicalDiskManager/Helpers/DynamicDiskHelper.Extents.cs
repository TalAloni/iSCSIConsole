/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace DiskAccessLibrary.LogicalDiskManager
{
    public partial class DynamicDiskHelper
    {
        public static List<DiskExtent> GetUnallocatedExtents(DynamicDisk disk)
        {
            List<DynamicDiskExtent> extents = GetDiskExtents(disk);
            // extents are sorted by first sector
            if (extents == null)
            {
                return null;
            }

            List<DiskExtent> result = new List<DiskExtent>();

            PrivateHeader privateHeader = disk.PrivateHeader;
            long publicRegionStartSector = (long)privateHeader.PublicRegionStartLBA;
            long startSector = publicRegionStartSector;
            long publicRegionSize = (long)privateHeader.PublicRegionSizeLBA * disk.Disk.BytesPerSector;

            // see if there is room before each extent
            foreach (DynamicDiskExtent extent in extents)
            {
                long extentStartSector = extent.FirstSector;
                long nextStartSector = extent.FirstSector + extent.Size / disk.BytesPerSector;
                long freeSpaceInBytes = (extentStartSector - startSector) * disk.BytesPerSector;
                if (freeSpaceInBytes > 0)
                {
                    result.Add(new DiskExtent(disk.Disk, startSector, freeSpaceInBytes));
                }

                startSector = nextStartSector;
            }

            // see if there is room after the last extent
            long spaceInBytes = publicRegionSize - (startSector - publicRegionStartSector) * disk.Disk.BytesPerSector;
            if (spaceInBytes > 0)
            {
                result.Add(new DiskExtent(disk.Disk, startSector, spaceInBytes));
            }

            return result;
        }

        /// <summary>
        /// Sorted by first sector
        /// </summary>
        /// <returns>null if there was a problem reading extent information from disk</returns>
        public static List<DynamicDiskExtent> GetDiskExtents(DynamicDisk disk)
        {
            List<DynamicDiskExtent> result = new List<DynamicDiskExtent>();
            PrivateHeader privateHeader = disk.PrivateHeader;
            if (privateHeader != null)
            {
                VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(disk);
                if (database != null)
                {
                    DiskRecord diskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
                    List<ExtentRecord> extentRecords = database.FindExtentsByDiskID(diskRecord.DiskId);
                    foreach (ExtentRecord extentRecord in extentRecords)
                    {
                        DynamicDiskExtent extent = DynamicDiskExtentHelper.GetDiskExtent(disk, extentRecord);
                        result.Add(extent);
                    }
                    DynamicDiskExtentsHelper.SortExtentsByFirstSector(result);
                    return result;
                }
            }
            return null;
        }

        public static long GetMaxNewExtentLength(DynamicDisk disk)
        {
            return GetMaxNewExtentLength(disk, 1);
        }

        /// <returns>In bytes</returns>
        public static long GetMaxNewExtentLength(DynamicDisk disk, long alignInSectors)
        {
            List<DiskExtent> unallocatedExtents = GetUnallocatedExtents(disk);
            if (unallocatedExtents == null)
            {
                return -1;
            }

            long result = 0;
            for (int index = 0; index < unallocatedExtents.Count; index++)
            {
                DiskExtent extent = unallocatedExtents[index];
                if (alignInSectors > 1)
                {
                    extent = DiskExtentHelper.GetAlignedDiskExtent(extent, alignInSectors);
                }
                if (extent.Size > result)
                {
                    result = extent.Size;
                }
            }
            return result;
        }

        public static DiskExtent FindExtentAllocation(DynamicDisk disk, long allocationLength)
        {
            return FindExtentAllocation(disk, allocationLength, 0);
        }

        /// <param name="allocationLength">In bytes</param>
        /// <param name="alignInSectors">0 or 1 for no alignment</param>
        /// <returns>Allocated DiskExtent or null if there is not enough free disk space</returns>
        public static DiskExtent FindExtentAllocation(DynamicDisk disk, long allocationLength, long alignInSectors)
        {
            List<DiskExtent> unallocatedExtents = DynamicDiskHelper.GetUnallocatedExtents(disk);
            if (unallocatedExtents == null)
            {
                return null;
            }

            for (int index = 0; index < unallocatedExtents.Count; index++)
            {
                DiskExtent extent = unallocatedExtents[index];
                if (alignInSectors > 1)
                {
                    extent = DiskExtentHelper.GetAlignedDiskExtent(extent, alignInSectors);
                }
                if (extent.Size >= allocationLength)
                {
                    return new DiskExtent(extent.Disk, extent.FirstSector, allocationLength);
                }
            }
            return null;
        }

        /// <param name="targetOffset">in bytes</param>
        public static bool IsMoveLocationValid(DynamicDiskExtent sourceExtent, DynamicDisk targetDisk, long targetOffset)
        {
            bool isSameDisk = (sourceExtent.Disk == targetDisk.Disk);
            List<DynamicDiskExtent> extents = GetDiskExtents(targetDisk);
            // extents are sorted by first sector
            if (extents == null)
            {
                return false;
            }

            PrivateHeader privateHeader = targetDisk.PrivateHeader;
            if (sourceExtent.BytesPerSector != targetDisk.BytesPerSector)
            {
                // We must not move an extent to another disk that has different sector size
                return false;
            }
            if (targetOffset % privateHeader.BytesPerSector > 0)
            {
                return false;
            }
            long targetSector = targetOffset / targetDisk.BytesPerSector;
            DiskExtent targetExtent = new DiskExtent(targetDisk.Disk, targetSector, sourceExtent.Size);

            List<DiskExtent> usedExtents = new List<DiskExtent>();
            foreach (DynamicDiskExtent extent in extents)
            {
                if (!isSameDisk || extent.FirstSector != sourceExtent.FirstSector)
                {
                    usedExtents.Add(extent);
                }
            }

            long publicRegionStartSector = (long)privateHeader.PublicRegionStartLBA;
            long publicRegionSize = (long)privateHeader.PublicRegionSizeLBA * targetDisk.BytesPerSector;
            List<DiskExtent> unallocatedExtents = DiskExtentsHelper.GetUnallocatedExtents(targetDisk.Disk, publicRegionStartSector, publicRegionSize, usedExtents);
            foreach (DiskExtent extent in unallocatedExtents)
            {
                if (extent.FirstSector <= targetExtent.FirstSector && targetExtent.LastSector <= extent.LastSector)
                {
                    return true;
                }
            }
            return false;
        }
    }
}