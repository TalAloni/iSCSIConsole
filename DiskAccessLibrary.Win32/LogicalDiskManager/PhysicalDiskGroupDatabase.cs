/* Copyright (C) 2014-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager.Win32
{
    public partial class PhysicalDiskGroupDatabase
    {
        public static DiskGroupDatabase ReadFromPhysicalDisks(Guid diskGroupGuid)
        {
            List<DynamicDisk> dynamicDisks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(diskGroupGuid);
            return DiskGroupDatabase.ReadFromDisks(dynamicDisks, diskGroupGuid);
        }

        public static List<DiskGroupDatabase> ReadFromPhysicalDisks()
        {
            List<DynamicDisk> dynamicDisks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
            return DiskGroupDatabase.ReadFromDisks(dynamicDisks);
        }
    }
}
