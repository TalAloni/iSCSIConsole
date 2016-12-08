/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DiskAccessLibrary
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
            ulong extentStartSector = GetExtentStartSector(dynamicDisk, extentRecord);
            int bytesPerSector = 512; // default for missing disk
            Disk disk = null;
            Guid diskGuid = Guid.Empty;
            if (dynamicDisk != null)
            {
                bytesPerSector = dynamicDisk.BytesPerSector;
                disk = dynamicDisk.Disk;
                diskGuid = dynamicDisk.DiskGuid;
            }
            DynamicDiskExtent extent = new DynamicDiskExtent(disk, (long)extentStartSector, (long)extentRecord.SizeLBA * bytesPerSector, extentRecord.ExtentId);
            extent.Name = extentRecord.Name;
            extent.DiskGuid = diskGuid;
            return extent;
        }

        /// <summary>
        /// Support null disks
        /// </summary>
        public static ulong GetExtentStartSector(DynamicDisk disk, ExtentRecord extentRecord)
        {
            ulong dataStartLBA = 0;
            if (disk != null)
            {
                PrivateHeader privateHeader = disk.PrivateHeader;
                dataStartLBA = privateHeader.PublicRegionStartLBA;
            }
            ulong extentStartSector = dataStartLBA + extentRecord.DiskOffsetLBA;
            return extentStartSector;
        }

        /// <param name="targetOffset">in bytes</param>
        public static bool IsMoveLocationValid(DynamicDisk disk, DynamicDiskExtent sourceExtent, long targetOffset)
        {
            List<DynamicDiskExtent> extents = GetDiskExtents(disk);
            // extents are sorted by first sector
            if (extents == null)
            {
                return false;
            }

            PrivateHeader privateHeader = disk.PrivateHeader;
            if (targetOffset % privateHeader.BytesPerSector > 0)
            {
                return false;
            }

            int index = GetIndexOfExtentID(extents, sourceExtent.ExtentID);
            extents.RemoveAt(index);

            long targetStartSector = targetOffset / disk.BytesPerSector;

            long publicRegionStartSector = (long)privateHeader.PublicRegionStartLBA;
            long startSector = publicRegionStartSector;
            long publicRegionSizeLBA = (long)privateHeader.PublicRegionSizeLBA;

            if (targetStartSector < publicRegionStartSector)
            {
                return false;
            }

            if (targetStartSector + sourceExtent.TotalSectors > publicRegionStartSector + publicRegionSizeLBA)
            {
                return false;
            }
            
            foreach (DynamicDiskExtent extent in extents)
            {
                long extentStartSector = extent.FirstSector;
                long extentEndSector = extent.FirstSector + extent.Size / disk.BytesPerSector - 1;
                if (extentStartSector >= targetStartSector &&
                    extentStartSector <= targetStartSector + sourceExtent.TotalSectors)
                {
                    // extent start within the requested region
                    return false;
                }

                if (extentEndSector >= targetStartSector &&
                    extentEndSector <= targetStartSector + sourceExtent.TotalSectors)
                {
                    // extent end within the requested region
                    return false;
                }
            }

            return true;
        }

        public static DiskExtent AllocateNewExtent(DynamicDisk disk, long allocationLength)
        {
            return AllocateNewExtent(disk, allocationLength, 0);
        }

        /// <param name="allocationLength">In bytes</param>
        /// <param name="alignInSectors">0 or 1 for no alignment</param>
        /// <returns>Allocated DiskExtent or null if there is not enough free disk space</returns>
        public static DiskExtent AllocateNewExtent(DynamicDisk disk, long allocationLength, long alignInSectors)
        {
            List<DiskExtent> unallocatedExtents = GetUnallocatedSpace(disk);
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

        public static long GetMaxNewExtentLength(DynamicDisk disk)
        {
            return GetMaxNewExtentLength(disk, 0);
        }

        /// <returns>In bytes</returns>
        public static long GetMaxNewExtentLength(DynamicDisk disk, long alignInSectors)
        {
            List<DiskExtent> unallocatedExtents = GetUnallocatedSpace(disk);
            if (unallocatedExtents == null)
            {
                return -1;
            }

            long result = 0;
            for(int index = 0; index < unallocatedExtents.Count; index++)
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

        private static List<DiskExtent> GetUnallocatedSpace(DynamicDisk disk)
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
                        DynamicDiskExtent extent = GetDiskExtent(disk, extentRecord);
                        result.Add(extent);
                    }
                    SortExtentsByFirstSector(result);
                    return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Sort (in-place) extents by first sector
        /// </summary>
        public static void SortExtentsByFirstSector(List<DynamicDiskExtent> extents)
        {
            SortedList<long, DynamicDiskExtent> list = new SortedList<long, DynamicDiskExtent>();
            foreach (DynamicDiskExtent extent in extents)
            {
                list.Add(extent.FirstSector, extent);
            }

            extents.Clear();
            foreach (DynamicDiskExtent extent in list.Values)
            {
                extents.Add(extent);
            }
        }
    }
}
