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

namespace DiskAccessLibrary
{
    public class BasicDiskHelper
    {
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

    }
}
