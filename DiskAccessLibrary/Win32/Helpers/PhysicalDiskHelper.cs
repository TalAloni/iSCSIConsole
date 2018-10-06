/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace DiskAccessLibrary
{
    public class PhysicalDiskHelper
    {
        public static List<PhysicalDisk> GetPhysicalDisks()
        {
            List<PhysicalDisk> result = new List<PhysicalDisk>();
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
                catch (InvalidDataException)
                {
                    // e.g. When using Dataram RAMDisk v4.4.0 RC36
                    continue;
                }
                result.Add(disk);
            }

            return result;
        }

        public static bool LockAllOrNone(List<PhysicalDisk> disks)
        { 
            bool success = true;
            int lockIndex;
            for(lockIndex = 0; lockIndex < disks.Count; lockIndex++)
            {
                success = disks[lockIndex].ExclusiveLock();
                if (!success)
                {
                    break;
                }
            }

            // release the disks that were locked
            if (!success)
            {
                for (int index = 0; index < lockIndex; index++)
                {
                    disks[index].ReleaseLock();
                }
            }

            return success;
        }

        /// <summary>
        /// Will not persist across reboots
        /// </summary>
        public static bool OfflineAllOrNone(List<PhysicalDisk> disks)
        {
            bool success = true;
            int offlineIndex;
            for (offlineIndex = 0; offlineIndex < disks.Count; offlineIndex++)
            {
                success = disks[offlineIndex].SetOnlineStatus(false, false);
                if (!success)
                {
                    break;
                }
            }

            // online the disks that were offlined
            if (!success)
            {
                for (int index = 0; index < offlineIndex; index++)
                {
                    disks[index].SetOnlineStatus(true, false);
                }
            }

            return success;
        }
    }
}
