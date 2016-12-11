/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;

namespace ISCSIConsole
{
    public class VolumeInfo
    {
        public static bool IsOffline(Volume volume)
        {
            IList<PhysicalDisk> disks = GetVolumeDisks(volume);
            foreach (PhysicalDisk disk in disks)
            {
                bool isOnline = disk.GetOnlineStatus();
                if (isOnline)
                {
                    return false;
                }
            }
            return true;
        }

        private static IList<PhysicalDisk> GetVolumeDisks(Volume volume)
        {
            SortedList<int, PhysicalDisk> disks = new SortedList<int, PhysicalDisk>();
            if (volume is DynamicVolume)
            {
                foreach (DiskExtent extent in ((DynamicVolume)volume).Extents)
                {
                    if (extent.Disk is PhysicalDisk)
                    {
                        PhysicalDisk disk = (PhysicalDisk)extent.Disk;
                        if (!disks.ContainsKey(disk.PhysicalDiskIndex))
                        {
                            disks.Add(disk.PhysicalDiskIndex, disk);
                        }
                    }
                }
            }
            else if (volume is Partition)
            {
                Partition partition = (Partition)volume;
                if (partition.Disk is PhysicalDisk)
                {
                    PhysicalDisk disk = (PhysicalDisk)partition.Disk;
                    disks.Add(disk.PhysicalDiskIndex, (PhysicalDisk)disk);
                }
            }
            return disks.Values;
        }
    }
}
