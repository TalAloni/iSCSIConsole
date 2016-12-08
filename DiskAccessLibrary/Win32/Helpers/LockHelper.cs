/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public enum LockStatus
    { 
        Success,
        CannotLockDisk,
        CannotLockVolume,
    }

    public class LockHelper
    {
        private static List<DynamicDisk> m_lockedDisks = new List<DynamicDisk>();
        private static List<DynamicVolume> m_lockedVolumes = new List<DynamicVolume>();

        public static LockStatus LockAllOrNone(List<DynamicDisk> disksToLock, List<DynamicVolume> volumesToLock)
        {
            bool success = DiskLockHelper.LockAllOrNone(disksToLock);
            if (!success)
            {
                return LockStatus.CannotLockDisk;
            }

            success = WindowsDynamicVolumeHelper.LockAllMountedOrNone(volumesToLock);
            if (!success)
            {
                DiskLockHelper.ReleaseLock(disksToLock);
                return LockStatus.CannotLockVolume;
            }

            return LockStatus.Success;
        }

        public static LockStatus LockAllDynamicDisks(bool lockAllDynamicVolumes)
        {
            List<DynamicDisk> disksToLock = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
            List<DynamicVolume> volumesToLock = new List<DynamicVolume>();

            if (lockAllDynamicVolumes)
            {
                volumesToLock = WindowsDynamicVolumeHelper.GetLockableDynamicVolumes(disksToLock);
            }

            LockStatus status = LockAllOrNone(disksToLock, volumesToLock);
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
