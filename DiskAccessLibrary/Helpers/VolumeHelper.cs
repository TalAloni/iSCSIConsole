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
    public partial class VolumeHelper
    {
        /// <summary>
        /// Will return or generate a persistent volume unique ID
        /// </summary>
        [Obsolete]
        public static Guid? GetVolumeUniqueID(Volume volume)
        {
            if (volume is MBRPartition)
            {
                MBRPartition partition = (MBRPartition)volume;
                MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(partition.Disk);
                byte[] firstSectorBytes = BigEndianConverter.GetBytes(partition.FirstSector);
                return new Guid((int)mbr.DiskSignature, 0, 0, firstSectorBytes);
            }
            else if (volume is GPTPartition)
            {
                return ((GPTPartition)volume).VolumeGuid;
            }
            else if (volume is DynamicVolume)
            {
                return ((DynamicVolume)volume).VolumeGuid;
            }
            else
            {
                return null;
            }
        }

        [Obsolete]
        public static Volume GetVolumeByGuid(List<Disk> disks, Guid volumeGuid)
        {
            List<Volume> volumes = GetVolumes(disks);
            foreach (Volume volume in volumes)
            {
                Guid? guid = GetVolumeUniqueID(volume);
                if (guid == volumeGuid)
                {
                    {
                        return volume;
                    }
                }
            }
            return null;
        }

        public static List<Volume> GetVolumes(List<Disk> disks)
        {
            List<Volume> result = new List<Volume>();
            List<DynamicDisk> dynamicDisks = new List<DynamicDisk>();

            // Get partitions:
            foreach (Disk disk in disks)
            {
                if (!DynamicDisk.IsDynamicDisk(disk))
                {
                    List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
                    foreach (Partition partition in partitions)
                    {
                        result.Add(partition);
                    }
                }
                else
                {
                    dynamicDisks.Add(DynamicDisk.ReadFromDisk(disk));
                }
            }

            // Get dynamic volumes
            List<DynamicVolume> dynamicVolumes = DynamicVolumeHelper.GetDynamicVolumes(dynamicDisks);
            foreach (DynamicVolume volume in dynamicVolumes)
            {
                result.Add(volume);
            }

            return result;
        }

        /// <summary>
        /// Return volumes that are stored (or partially stored) on the given disk
        /// </summary>
        [Obsolete]
        public static List<Volume> GetDiskVolumes(Disk disk)
        {
            List<Volume> result = new List<Volume>();

            DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);

            if (dynamicDisk == null)
            {
                // basic disk
                List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
                foreach (MBRPartition partition in partitions)
                {
                    result.Add(partition);
                }
            }
            else
            {
                // dynamic disk
                List<DynamicVolume> dynamicVolumes = DynamicVolumeHelper.GetDynamicDiskVolumes(dynamicDisk);
                foreach (DynamicVolume volume in dynamicVolumes)
                {
                    result.Add(volume);
                }
            }
            return result;
        }

        [Obsolete]
        public static bool ContainsVolumeGuid(List<Volume> volumes, Guid volumeGuid)
        {
            foreach (Volume volume in volumes)
            {
                if (volume is DynamicVolume)
                {
                    DynamicVolume dynamicVolume = (DynamicVolume)volume;
                    if (dynamicVolume.VolumeGuid == volumeGuid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
