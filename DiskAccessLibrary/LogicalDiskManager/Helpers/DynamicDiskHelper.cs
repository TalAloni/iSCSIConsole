/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace DiskAccessLibrary.LogicalDiskManager
{
    public partial class DynamicDiskHelper
    {
        public static DynamicDisk FindDisk(List<DynamicDisk> disks, Guid diskGuid)
        {
            foreach (DynamicDisk dynamicDisk in disks)
            {
                if (dynamicDisk.DiskGuid == diskGuid)
                {
                    return dynamicDisk;
                }
            }
            return null;
        }

        public static List<DynamicDisk> FindDiskGroup(List<DynamicDisk> disks, Guid diskGroupGuid)
        {
            List<DynamicDisk> result = new List<DynamicDisk>();
            foreach (DynamicDisk dynamicDisk in disks)
            {
                if (dynamicDisk.DiskGroupGuid == diskGroupGuid)
                {
                    result.Add(dynamicDisk);
                }
            }
            return result;
        }
    }
}
