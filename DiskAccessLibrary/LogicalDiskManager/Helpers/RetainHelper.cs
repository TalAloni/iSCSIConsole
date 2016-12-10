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
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class RetainHelper
    {
        public static bool IsVolumeRetained(DynamicVolume volume, out bool isBootVolume)
        {
            if (volume is SimpleVolume)
            {
                return IsVolumeRetained((SimpleVolume)volume, out isBootVolume);
            }
            else if (volume is MirroredVolume)
            {
                DynamicVolume component = ((MirroredVolume)volume).Components[0];
                if (component is SimpleVolume)
                {
                    return IsVolumeRetained((SimpleVolume)component, out isBootVolume);
                }
            }

            isBootVolume = false;
            return false;
        }

        public static bool IsVolumeRetained(SimpleVolume volume, out bool isBootVolume)
        {
            isBootVolume = false;
            DynamicDisk disk = DynamicDisk.ReadFromDisk(volume.Disk);
            long bootPartitionStartLBA;
            List<ExtentRecord> retained = GetRetainedExtentsOnDisk(disk, out bootPartitionStartLBA);
            foreach(ExtentRecord record in retained)
            {
                if (record.ExtentId == volume.DiskExtent.ExtentID)
                {
                    if ((long)(disk.PrivateHeader.PublicRegionStartLBA + record.DiskOffsetLBA) == bootPartitionStartLBA)
                    {
                        isBootVolume = true;
                    }
                    return true;
                }
            }
            return false;
        }

        private static List<ExtentRecord> GetRetainedExtentsOnDisk(DynamicDisk disk, out long bootPartitionStartLBA)
        {
            VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(disk);
            DiskRecord diskRecord = database.FindDiskByDiskGuid(disk.DiskGuid);
            bootPartitionStartLBA = -1;

            List<ExtentRecord> result = new List<ExtentRecord>();
            foreach (VolumeRecord volume in database.VolumeRecords)
            {
                if ((volume.VolumeFlags & VolumeFlags.RetainPartition) > 0)
                {
                    List<ComponentRecord> components = database.FindComponentsByVolumeID(volume.VolumeId);
                    foreach (ComponentRecord componentRecord in components)
                    {
                        if (componentRecord.ExtentLayout == ExtentLayoutName.Concatenated)
                        {
                            if (componentRecord.NumberOfExtents == 1)
                            {
                                List<ExtentRecord> extents = database.FindExtentsByComponentID(componentRecord.ComponentId);
                                if (extents.Count == 1)
                                {
                                    ExtentRecord extent = extents[0];
                                    if (extent.DiskId == diskRecord.DiskId)
                                    {
                                        result.Add(extent);
                                        if ((volume.VolumeFlags & VolumeFlags.BootVolume) > 0)
                                        {
                                            bootPartitionStartLBA = (long)(disk.PrivateHeader.PublicRegionStartLBA + extent.DiskOffsetLBA);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
