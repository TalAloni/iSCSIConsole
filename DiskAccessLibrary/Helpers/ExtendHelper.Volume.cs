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
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DiskAccessLibrary
{
    public class ExtendHelper
    {
        /// <summary>
        /// This refers to the raw volume and not to the filesystem
        /// </summary>
        /// <returns>Number of bytes (per disk extent)</returns>
        public static long GetMaximumSizeToExtendVolume(Volume volume)
        {
            if (volume is Partition)
            {
                return GetMaximumSizeToExtendPartition((Partition)volume);
            }
            else if (volume is DynamicVolume)
            {
                return GetMaximumSizeToExtendDynamicVolume((DynamicVolume)volume);
            }
            else
            {
                return 0;
            }
        }

        public static long GetMaximumSizeToExtendPartition(Partition partition)
        {
            if (partition is MBRPartition)
            {
                return GetMaximumSizeToExtendMBRPartition((MBRPartition)partition);
            }
            else if (partition is GPTPartition)
            {
                return GetMaximumSizeToExtendGPTPartition((GPTPartition)partition);
            }
            else
            {
                return 0;
            }
        }

        public static long GetMaximumSizeToExtendDynamicVolume(DynamicVolume volume)
        {
            if (volume is SimpleVolume)
            {
                SimpleVolume simpleVolume = (SimpleVolume)volume;
                return GetMaximumSizeToExtendDynamicDiskExtent(simpleVolume.DiskExtent);
            }
            else if (volume is StripedVolume)
            {
                StripedVolume stripedVolume = (StripedVolume)volume;
                long max = Int64.MaxValue;
                foreach (DynamicDiskExtent extent in stripedVolume.Extents)
                {
                    long extentMax = GetMaximumSizeToExtendDynamicDiskExtent(extent);
                    max = Math.Min(max, extentMax);
                }
                return max;
            }
            else if (volume is Raid5Volume)
            {
                Raid5Volume raid5Volume = (Raid5Volume)volume;
                long max = Int64.MaxValue;
                foreach (DynamicDiskExtent extent in raid5Volume.Extents)
                {
                    long extentMax = GetMaximumSizeToExtendDynamicDiskExtent(extent);
                    max = Math.Min(max, extentMax);
                }
                return max;
            }
            else
            {
                return 0;
            }
        }

        /// <returns>Number of bytes</returns>
        public static long GetMaximumSizeToExtendMBRPartition(MBRPartition partition)
        {
            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(partition.Disk);
            long partitonEndSector = partition.FirstSector + partition.Size / partition.BytesPerSector;

            long max = partition.Disk.Size - (partition.FirstSector * partition.BytesPerSector + partition.Size);
            foreach (PartitionTableEntry entry in mbr.PartitionTable)
            {
                if (entry.FirstSectorLBA > partition.FirstSector)
                {
                    long available = (entry.FirstSectorLBA - partition.FirstSector) * partition.BytesPerSector - partition.Size;
                    max = Math.Min(max, available);
                }
            }

            // MBR partition cannot be larger than 2^32 sectors
            max = Math.Min(max, UInt32.MaxValue * partition.BytesPerSector);
            return max;
        }

        /// <returns>Number of bytes</returns>
        public static long GetMaximumSizeToExtendGPTPartition(GPTPartition partition)
        {
            GuidPartitionTableHeader header = GuidPartitionTableHeader.ReadFromDisk(partition.Disk);
            long partitonEndSector = partition.FirstSector + partition.Size / partition.BytesPerSector;
            // Prevent from extending beyond the secondary GPT header / partition array
            long max = ((long)header.LastUsableLBA + 1) * partition.BytesPerSector - (partition.FirstSector * partition.BytesPerSector + partition.Size);

            List<GuidPartitionEntry> entries = GuidPartitionTable.ReadEntriesFromDisk(partition.Disk);
            foreach (GuidPartitionEntry entry in entries)
            {
                if ((long)entry.FirstLBA > partition.FirstSector)
                {
                    long available = ((long)entry.FirstLBA - partition.FirstSector) * partition.BytesPerSector - partition.Size;
                    max = Math.Min(max, available);
                }
            }

            return max;
        }

        /// <returns>Number of bytes</returns>
        public static long GetMaximumSizeToExtendDynamicDiskExtent(DynamicDiskExtent targetExtent)
        {
            DynamicDisk disk = DynamicDisk.ReadFromDisk(targetExtent.Disk);
            PrivateHeader privateHeader = disk.PrivateHeader;
            List<DynamicDiskExtent> extents = DynamicDiskHelper.GetDiskExtents(disk);
            if (extents == null)
            {
                throw new InvalidDataException("Cannot read extents information from disk");
            }

            long endOfData = (long)((privateHeader.PublicRegionStartLBA + privateHeader.PublicRegionSizeLBA) * (ulong)disk.BytesPerSector);
            long max = endOfData - (targetExtent.FirstSector * targetExtent.BytesPerSector + targetExtent.Size); // space from the extent end to the end of the disk
            foreach (DynamicDiskExtent extent in extents)
            {
                if (extent.FirstSector > targetExtent.FirstSector)
                {
                    long spaceBetweenExtents = (extent.FirstSector - targetExtent.FirstSector) * disk.BytesPerSector - targetExtent.Size;
                    max = Math.Min(max, spaceBetweenExtents);
                }
            }

            return max;
        }

        public static void ExtendVolume(Volume volume, long numberOfAdditionalExtentSectors, DiskGroupDatabase database)
        {
            if (volume is Partition)
            {
                ExtendPartition((Partition)volume, numberOfAdditionalExtentSectors);
            }
            else if (volume is DynamicVolume)
            {
                ExtendDynamicVolume((DynamicVolume)volume, numberOfAdditionalExtentSectors, database);
            }
        }

        public static void ExtendPartition(Partition volume, long numberOfAdditionalExtentSectors)
        {
            if (volume is MBRPartition)
            {
                MBRPartition partition = (MBRPartition)volume;
                ExtendMBRPartition(partition, numberOfAdditionalExtentSectors);
            }
            else if (volume is GPTPartition)
            {
                GPTPartition partition = (GPTPartition)volume;
                ExtendGPTPartition(partition, numberOfAdditionalExtentSectors);
            }
        }

        public static void ExtendDynamicVolume(DynamicVolume volume, long numberOfAdditionalExtentSectors, DiskGroupDatabase database)
        {
            if (volume is SimpleVolume)
            {
                SimpleVolume simpleVolume = (SimpleVolume)volume;
                VolumeManagerDatabaseHelper.ExtendSimpleVolume(database, simpleVolume, numberOfAdditionalExtentSectors);
            }
            else if (volume is StripedVolume)
            {
                StripedVolume stripedVolume = (StripedVolume)volume;
                VolumeManagerDatabaseHelper.ExtendStripedVolume(database, stripedVolume, numberOfAdditionalExtentSectors);
            }
            else if (volume is Raid5Volume)
            {
                Raid5Volume raid5Volume = (Raid5Volume)volume;
                VolumeManagerDatabaseHelper.ExtendRAID5Volume(database, raid5Volume, numberOfAdditionalExtentSectors);
            }
        }

        public static void ExtendMBRPartition(MBRPartition partition, long numberOfAdditionalExtentSectors)
        {
            Disk disk = partition.Disk;
            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            for (int index = 0; index < mbr.PartitionTable.Length; index++)
            {
                if (mbr.PartitionTable[index].FirstSectorLBA == partition.FirstSector)
                {
                    mbr.PartitionTable[index].SectorCountLBA += (uint)numberOfAdditionalExtentSectors;
                    ulong lastSectorLBA = mbr.PartitionTable[index].LastSectorLBA;
                    mbr.PartitionTable[index].LastSectorCHS = CHSAddress.FromLBA(lastSectorLBA, disk);
                    break;
                }
            }
            MasterBootRecord.WriteToDisk(disk, mbr);
        }

        public static void ExtendGPTPartition(GPTPartition partition, long numberOfAdditionalExtentSectors)
        {
            Disk disk = partition.Disk;
            GuidPartitionTableHeader primaryHeader = GuidPartitionTableHeader.ReadPrimaryFromDisk(disk);
            GuidPartitionTableHeader secondaryHeader = GuidPartitionTableHeader.ReadSecondaryFromDisk(disk, primaryHeader);
            if (primaryHeader == null || secondaryHeader == null)
            {
                throw new NotImplementedException("Cannot extend GPT disk with corrupted header");
            }

            if (primaryHeader.PartitionArrayCRC32 != secondaryHeader.PartitionArrayCRC32)
            {
                throw new NotImplementedException("Cannot extend GPT disk with mismatched partition arrays");
            }

            List<GuidPartitionEntry> entries = GuidPartitionTable.ReadEntriesFromDisk(disk);

            foreach(GuidPartitionEntry entry in entries)
            {
                if ((long)entry.FirstLBA == partition.FirstSector)
                {
                    entry.LastLBA += (ulong)numberOfAdditionalExtentSectors;
                    GuidPartitionEntry.WriteToDisk(disk, primaryHeader, entry);
                    GuidPartitionEntry.WriteToDisk(disk, secondaryHeader, entry);
                    break;
                }
            }

            primaryHeader.PartitionArrayCRC32 = GuidPartitionTable.ComputePartitionArrayCRC32(disk, primaryHeader);
            GuidPartitionTableHeader.WriteToDisk(disk, primaryHeader);
            secondaryHeader.PartitionArrayCRC32 = GuidPartitionTable.ComputePartitionArrayCRC32(disk, secondaryHeader);
            GuidPartitionTableHeader.WriteToDisk(disk, secondaryHeader);
        }
    }
}
