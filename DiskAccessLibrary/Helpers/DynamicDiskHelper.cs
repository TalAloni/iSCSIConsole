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
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DiskAccessLibrary
{
    public partial class DynamicDiskHelper
    {
        public static DynamicDisk FindDisk(List<DynamicDisk> disks, Guid diskGuid)
        {
            foreach (DynamicDisk dynamicDisk in disks)
            {
                if (dynamicDisk != null)
                {
                    if (dynamicDisk.DiskGuid == diskGuid)
                    {
                        return dynamicDisk;
                    }
                }
            }
            return null;
        }

        public static long FindUnusedRegionInPrivateRegion(DynamicDisk disk, int sectorCount)
        {
            bool[] bitmap = GetPrivateRegionUsageBitmap(disk);
            
            int startIndex = 0;
            int freeCount = 0;
            for (int index = 0; index < bitmap.Length; index++)
            {
                if (freeCount == 0)
                {
                    if (bitmap[index] == false) // free
                    {
                        startIndex = index;
                        freeCount++;
                    }
                }
                else
                {
                    if (bitmap[index] == false) // free
                    {
                        freeCount++;
                        if (freeCount == sectorCount)
                        {
                            return (long)disk.PrivateHeader.PrivateRegionStartLBA + startIndex;
                        }
                    }
                    else
                    {
                        freeCount = 0;
                    }
                }
            }

            return -1;
        }

        public static long FindUnusedSectorInPrivateRegion(DynamicDisk disk)
        {
            bool[] bitmap = GetPrivateRegionUsageBitmap(disk);
            
            for (int index = 0; index < bitmap.Length; index++)
            {
                if (bitmap[index] == false)
                {
                    return (long)disk.PrivateHeader.PrivateRegionStartLBA + index;
                }
            }
            return -1;
        }

        public static bool[] GetPrivateRegionUsageBitmap(DynamicDisk disk)
        {
            // usage bitmap:
            bool[] bitmap = new bool[disk.PrivateHeader.PrivateRegionSizeLBA];
            bitmap[disk.PrivateHeader.PrimaryPrivateHeaderLBA] = true;
            bitmap[disk.PrivateHeader.SecondaryPrivateHeaderLBA] = true;
            bitmap[disk.PrivateHeader.PrimaryTocLBA] = true;
            bitmap[disk.PrivateHeader.PreviousPrimaryTocLBA] = true;
            bitmap[disk.PrivateHeader.SecondaryTocLBA] = true;
            bitmap[disk.PrivateHeader.PreviousSecondaryTocLBA] = true;

            foreach (TOCRegion region in disk.TOCBlock.Regions)
            {
                for (int index = 0; index < (int)region.SizeLBA; index++)
                {
                    bitmap[(int)region.StartLBA + index] = true;
                }
            }
            return bitmap;
        }
    }
}
