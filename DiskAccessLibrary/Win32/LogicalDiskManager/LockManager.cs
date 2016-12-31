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
using DiskAccessLibrary.LogicalDiskManager;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class LockManager
    {
        private static List<DynamicDisk> m_lockedDisks = new List<DynamicDisk>();
        private static List<DynamicVolume> m_lockedVolumes = new List<DynamicVolume>();

        public static LockStatus LockDynamicDiskGroup(Guid diskGroupGuid, bool lockAllDynamicVolumes)
        {
            List<DynamicDisk> disksToLock = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(diskGroupGuid);
            List<DynamicVolume> volumesToLock = new List<DynamicVolume>();

            if (lockAllDynamicVolumes)
            {
                volumesToLock = WindowsDynamicVolumeHelper.GetLockableDynamicVolumes(disksToLock);
            }

            LockStatus status = LockHelper.LockAllOrNone(disksToLock, volumesToLock);
            if (status == LockStatus.Success)
            {
                m_lockedDisks.AddRange(disksToLock);
                m_lockedVolumes.AddRange(volumesToLock);
            }
            return status;
        }

        public static void UnlockAllDisksAndVolumes()
        {
            DiskLockHelper.ReleaseLock(m_lockedDisks);

            foreach (DynamicVolume volumeToUnlock in m_lockedVolumes)
            {
                WindowsVolumeManager.ReleaseLock(volumeToUnlock.VolumeGuid);
            }
            m_lockedDisks.Clear();
            m_lockedVolumes.Clear();
        }
    }
}
