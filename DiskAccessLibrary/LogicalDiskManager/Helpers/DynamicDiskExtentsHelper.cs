/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class DynamicDiskExtentsHelper
    {
        /// <summary>
        /// Sort (in-place) extents by first sector
        /// </summary>
        public static void SortExtentsByFirstSector(List<DynamicDiskExtent> extents)
        {
            SortedList<long, DynamicDiskExtent> list = new SortedList<long, DynamicDiskExtent>();
            foreach (DynamicDiskExtent extent in extents)
            {
                list.Add(extent.FirstSector, extent);
            }

            extents.Clear();
            extents.AddRange(list.Values);
        }
    }
}
