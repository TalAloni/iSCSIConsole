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

        public static string GetVolumeTypeString(Volume volume)
        {
            if (volume is SimpleVolume)
            {
                return "Simple";
            }
            else if (volume is SpannedVolume)
            {
                return "Spanned";
            }
            else if (volume is StripedVolume)
            {
                return "Striped";
            }
            else if (volume is MirroredVolume)
            {
                return "Mirrored";
            }
            else if (volume is Raid5Volume)
            {
                return "RAID-5";
            }
            else if (volume is Partition)
            {
                return "Partition";
            }
            else
            {
                return "Unknown";
            }
        }

        public static string GetVolumeStatusString(Volume volume)
        {
            if (volume is DynamicVolume)
            {
                if (volume is MirroredVolume)
                {
                    if (!((MirroredVolume)volume).IsHealthy && ((MirroredVolume)volume).IsOperational)
                    {
                        return "Failed Rd";
                    }
                }
                else if (volume is Raid5Volume)
                {
                    if (!((Raid5Volume)volume).IsHealthy && ((Raid5Volume)volume).IsOperational)
                    {
                        return "Failed Rd";
                    }
                }

                if (((DynamicVolume)volume).IsHealthy)
                {
                    return "Healthy";
                }
                else
                {
                    return "Failed";
                }
            }
            else if (volume is Partition)
            {
                return "Healthy";
            }
            else
            {
                return String.Empty;
            }
        }
    }
}
