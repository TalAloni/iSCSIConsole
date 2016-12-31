/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class DiskOfflineHelper
    {
        public static bool IsDiskGroupOnlineAndWritable(Guid diskGroupGuid)
        {
            List<DynamicDisk> diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(diskGroupGuid);
            return IsDiskGroupOnlineAndWritable(diskGroup);
        }

        public static bool IsDiskGroupOnlineAndWritable(List<DynamicDisk> diskGroup)
        {
            List<PhysicalDisk> physicalDisks = new List<PhysicalDisk>();
            foreach (DynamicDisk dynamicDisk in diskGroup)
            {
                if (dynamicDisk.Disk is PhysicalDisk)
                {
                    physicalDisks.Add((PhysicalDisk)dynamicDisk.Disk);
                }
            }

            foreach (PhysicalDisk disk in physicalDisks)
            {
                bool isReadOnly;
                bool isOnline = disk.GetOnlineStatus(out isReadOnly);
                if (!isOnline || isReadOnly)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Will not persist across reboots
        /// </summary>
        public static bool OfflineDiskGroup(Guid diskGroupGuid)
        {
            List<DynamicDisk> disksToOffline = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(diskGroupGuid);
            return OfflineAllOrNone(disksToOffline);
        }

        public static void OnlineDiskGroup(Guid diskGroupGuid)
        {
            List<DynamicDisk> disksToOnline = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(diskGroupGuid);
            OnlineAll(disksToOnline);
        }

        /// <summary>
        /// Will not persist across reboots
        /// </summary>
        public static bool OfflineAllOrNone(List<DynamicDisk> disksToOffline)
        {
            List<PhysicalDisk> physicalDisks = new List<PhysicalDisk>();
            foreach (DynamicDisk dynamicDisk in disksToOffline)
            {
                if (dynamicDisk.Disk is PhysicalDisk)
                {
                    physicalDisks.Add((PhysicalDisk)dynamicDisk.Disk);
                }
            }
            return PhysicalDiskHelper.OfflineAllOrNone(physicalDisks);
        }

        public static void OnlineAll(List<DynamicDisk> disksToOnline)
        {
            foreach (DynamicDisk disk in disksToOnline)
            {
                ((PhysicalDisk)disk.Disk).SetOnlineStatus(true);
            }
        }
    }
}
