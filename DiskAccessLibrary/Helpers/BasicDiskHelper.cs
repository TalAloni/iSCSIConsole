/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary
{
    public class BasicDiskHelper
    {
        /// <summary>
        /// While technically a partition on an MBR disk could start on sector 1, we use sector 64 for alignment purposes.
        /// Note: Windows 7 will start MBR partitions on sector 128 for disks with 512-byte sectors, and on sector 64 for disks with 1KB/2KB/4KB sectors.
        /// Windows will start the dynamic data partition of a dynamic disks on sector 63 (or 1 in some cases), but the volume (extent) itself may be aligned to native sector boundaries.
        /// </summary>
        public const int MBRDiskFirstUsableSector = 64;

        public static List<Partition> GetPartitions(Disk disk)
        {
            List<Partition> result = new List<Partition>();

            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            if (mbr != null)
            {
                if (!mbr.IsGPTBasedDisk)
                {
                    for (int index = 0; index < mbr.PartitionTable.Length; index++)
                    {
                        PartitionTableEntry entry = mbr.PartitionTable[index];
                        if (entry.SectorCountLBA > 0)
                        {
                            long size = entry.SectorCountLBA * disk.BytesPerSector;
                            MBRPartition partition = new MBRPartition(entry.PartitionType, disk, entry.FirstSectorLBA, size);
                            result.Add(partition);
                        }
                    }
                }
                else
                {
                    List<GuidPartitionEntry> entries = GuidPartitionTable.ReadEntriesFromDisk(disk);
                    if (entries != null)
                    {
                        foreach (GuidPartitionEntry entry in entries)
                        {
                            GPTPartition partition = new GPTPartition(entry.PartitionGuid, entry.PartitionTypeGuid, entry.PartitionName, disk, (long)entry.FirstLBA, (long)(entry.SizeLBA * (uint)disk.BytesPerSector));
                            result.Add(partition);
                        }
                    }
                }
            }
            return result;
        }

        public static Partition GetPartitionByStartOffset(Disk disk, long firstSector)
        {
            List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
            foreach (Partition partition in partitions)
            {
                if (partition.FirstSector == firstSector)
                {
                    return partition;
                }
            }
            return null;
        }

        public static List<DiskExtent> GetUnallocatedExtents(Disk disk)
        {
            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            List<DiskExtent> result = new List<DiskExtent>();
            if (mbr == null)
            {
                result.Add(new DiskExtent(disk, 0, disk.Size));
                return result;
            }
            else
            {
                long dataRegionStartSector;
                long dataRegionSize;
                if (!mbr.IsGPTBasedDisk)
                {
                    dataRegionStartSector = MBRDiskFirstUsableSector;
                    dataRegionSize = Math.Min(disk.Size, UInt32.MaxValue * disk.BytesPerSector) - dataRegionStartSector;
                }
                else
                {
                    GuidPartitionTableHeader gptHeader = GuidPartitionTableHeader.ReadFromDisk(disk);
                    dataRegionStartSector = (long)gptHeader.FirstUsableLBA;
                    dataRegionSize = (long)(gptHeader.LastUsableLBA - gptHeader.FirstUsableLBA + 1) * disk.BytesPerSector;
                }

                List<Partition> partitions = GetPartitions(disk);
                List<DiskExtent> usedExtents = new List<DiskExtent>();
                foreach (Partition partition in partitions)
                {
                    usedExtents.Add(partition.Extent);
                }
                return DiskExtentsHelper.GetUnallocatedExtents(disk, dataRegionStartSector, dataRegionSize, usedExtents);
            }
        }
    }
}
