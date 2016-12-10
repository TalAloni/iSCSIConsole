/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
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

            List<DynamicVolume> dynamicVolumes = DynamicVolumeHelper.GetDynamicVolumes(disks);
            for (int index = 0; index < dynamicVolumes.Count; index++)
            {
                // non-operational volumes cannot be locked
                if (!dynamicVolumes[index].IsOperational)
                {
                    dynamicVolumes.RemoveAt(index);
                    index--;
                }
            }
            return dynamicVolumes;
        }
    }
}
