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

namespace ISCSIConsole
{
    public class LockUtils
    {
        public static void ReleaseDisks(List<Disk> disks)
        {
            foreach (Disk disk in disks)
            {
                ReleaseDisk(disk);
            }
        }

        public static void ReleaseDisk(Disk disk)
        {
            if (disk is DiskImage)
            {
                ((DiskImage)disk).ReleaseLock();
            }
#if Win32
            else if (disk is PhysicalDisk)
            {
                if (!DiskAccessLibrary.LogicalDiskManager.DynamicDisk.IsDynamicDisk(disk))
                {
                    LockHelper.UnlockBasicDiskAndVolumes((PhysicalDisk)disk);
                    try
                    {
                        ((PhysicalDisk)disk).UpdateProperties();
                    }
                    catch (System.IO.IOException)
                    {
                    }
                }
            }
            else if (disk is VolumeDisk)
            {
                Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(((VolumeDisk)disk).Volume);
                if (windowsVolumeGuid.HasValue)
                {
                    WindowsVolumeManager.ReleaseLock(windowsVolumeGuid.Value);
                }
            }
#endif
        }
    }
}
