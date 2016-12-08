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
    public class WindowsDynamicVolumeHelper
    {
        public static List<DynamicVolume> GetDynamicVolumes()
        {
            List<DynamicDisk> disks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
            return DynamicVolumeHelper.GetDynamicVolumes(disks);
        }

        public static List<DynamicVolume> GetLockableDynamicVolumes(List<DynamicDisk> dynamicDisks)
        {
            List<DynamicVolume> result = new List<DynamicVolume>();

            List<DynamicDisk> disks = new List<DynamicDisk>();
            foreach (DynamicDisk dynamicDisk in dynamicDisks)
            {
                if (dynamicDisk.Disk is PhysicalDisk)
                {
                    disks.Add(dynamicDisk);
                }
            }

            return DynamicVolumeHelper.GetDynamicVolumes(disks);
        }

        public static bool LockAllMountedOrNone(List<DynamicVolume> volumes)
        {
            bool success = true;
            int lockIndex;
            for (lockIndex = 0; lockIndex < volumes.Count; lockIndex++)
            {
                // NOTE: The fact that a volume does not have mount points, does not mean it is not mounted and cannot be accessed by Windows
                success = WindowsVolumeManager.ExclusiveLockIfMounted(volumes[lockIndex].VolumeGuid);
                if (!success)
                {
                    break;
                }
            }

            if (!success)
            {
                // release the volumes that were locked
                for (int index = 0; index < lockIndex; index++)
                {
                    WindowsVolumeManager.ReleaseLock(volumes[index].VolumeGuid);
                }
            }

            return success;
        }
    }
}
