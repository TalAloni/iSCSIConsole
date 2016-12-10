/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;

namespace DiskAccessLibrary
{
    public enum LockStatus
    {
        Success,
        CannotLockDisk,
        CannotLockVolume,
    }

    public partial class LockHelper
    {
        /// <summary>
        /// Will lock physical basic disk and all volumes on it.
        /// If the operation is not completed successfully, all locks will be releases.
        /// </summary>
        public static LockStatus LockBasicDiskAndVolumesOrNone(PhysicalDisk disk)
        {
            bool success = disk.ExclusiveLock();
            if (!success)
            {
                return LockStatus.CannotLockDisk;
            }
            List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
            List<Guid> volumeGuids = new List<Guid>();
            foreach (Partition partition in partitions)
            {
                Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(partition);
                if (windowsVolumeGuid.HasValue)
                {
                    volumeGuids.Add(windowsVolumeGuid.Value);
                }
                else
                {
                    return LockStatus.CannotLockVolume;
                }
            }

            success = LockAllVolumesOrNone(volumeGuids);
            if (!success)
            {
                disk.ReleaseLock();
                return LockStatus.CannotLockVolume;
            }
            return LockStatus.Success;
        }

        public static void UnlockBasicDiskAndVolumes(PhysicalDisk disk)
        {
            List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
            foreach (Partition partition in partitions)
            {
                Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(partition);
                if (windowsVolumeGuid.HasValue)
                {
                    WindowsVolumeManager.ReleaseLock(windowsVolumeGuid.Value);
                }
            }

            disk.ReleaseLock();
        }

        public static bool LockAllVolumesOrNone(List<Guid> volumeGuids)
        {
            bool success = true;
            int lockIndex;
            for (lockIndex = 0; lockIndex < volumeGuids.Count; lockIndex++)
            {
                success = WindowsVolumeManager.ExclusiveLock(volumeGuids[lockIndex]);
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
                    WindowsVolumeManager.ReleaseLock(volumeGuids[lockIndex]);
                }
            }

            return success;
        }
    }
}