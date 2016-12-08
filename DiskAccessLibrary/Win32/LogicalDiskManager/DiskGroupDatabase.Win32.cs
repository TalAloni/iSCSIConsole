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

namespace DiskAccessLibrary.LogicalDiskManager
{
    public partial class DiskGroupDatabase
    {
        public static DiskGroupDatabase ReadFromPhysicalDisks(Guid diskGroupGuid)
        {
            List<DynamicDisk> dynamicDisks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
            return ReadFromDisks(dynamicDisks, diskGroupGuid);
        }

        public static List<DiskGroupDatabase> ReadFromPhysicalDisks()
        {
            List<DynamicDisk> dynamicDisks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
            return ReadFromDisks(dynamicDisks);
        }
    }
}
