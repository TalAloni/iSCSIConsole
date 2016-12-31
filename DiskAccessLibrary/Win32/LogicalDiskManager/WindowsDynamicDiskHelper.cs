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
    public class WindowsDynamicDiskHelper
    {
        public static List<DynamicDisk> GetPhysicalDynamicDisks()
        {
            List<PhysicalDisk> disks = PhysicalDiskHelper.GetPhysicalDisks();
            List<DynamicDisk> result = new List<DynamicDisk>();
            foreach (PhysicalDisk disk in disks)
            {
                DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);
                if (dynamicDisk != null)
                {
                    result.Add(dynamicDisk);
                }
            }
            return result;
        }

        public static List<DynamicDisk> GetPhysicalDynamicDisks(Guid diskGroupGuid)
        {
            List<DynamicDisk> dynamicDisks = GetPhysicalDynamicDisks();
            return DynamicDiskHelper.FindDiskGroup(dynamicDisks, diskGroupGuid);
        }

        public static PrivateHeader FindDiskPrivateHeader(Guid diskGuid)
        {
            DynamicDisk disk = FindDisk(diskGuid);
            if (disk != null)
            {
                return disk.PrivateHeader;
            }
            return null;
        }

        public static DynamicDisk FindDisk(Guid diskGuid)
        {
            List<int> diskIndexList = PhysicalDiskControl.GetPhysicalDiskIndexList();

            foreach (int diskIndex in diskIndexList)
            {
                PhysicalDisk disk;
                try
                {
                    disk = new PhysicalDisk(diskIndex); // will throw an exception if disk is not valid
                }
                catch (DriveNotFoundException)
                {
                    // The disk must have been removed from the system
                    continue;
                }
                catch (DeviceNotReadyException)
                {
                    continue;
                }
                catch (SharingViolationException) // skip this disk, it's probably being used
                {
                    continue;
                }

                DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);
                if (dynamicDisk != null)
                {
                    if (dynamicDisk.DiskGuid == diskGuid)
                    {
                        return dynamicDisk;
                    }
                }
            }
            return null;
        }
    }
}
