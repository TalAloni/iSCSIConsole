/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class PrivateRegionHelper
    {
        public static long FindUnusedSector(PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            return FindUnusedRegion(privateHeader, tocBlock, 1);
        }

        public static long FindUnusedRegion(PrivateHeader privateHeader, TOCBlock tocBlock, int sectorCount)
        {
            bool[] bitmap = GetPrivateRegionUsageBitmap(privateHeader, tocBlock);
            // Reserve the first, second, third, third-last and second-last sectors for TOCBlocks
            bitmap[0] = true;
            bitmap[1] = true;
            bitmap[2] = true;
            bitmap[privateHeader.PrivateRegionSizeLBA - 3] = true;
            bitmap[privateHeader.PrivateRegionSizeLBA - 2] = true;

            int startIndex = 0;
            int freeCount = 0;
            for (int index = 0; index < bitmap.Length; index++)
            {
                if (bitmap[index] == false) // free
                {
                    if (freeCount == 0)
                    {
                        startIndex = index;
                    }
                    freeCount++;
                    if (freeCount == sectorCount)
                    {
                        return (long)privateHeader.PrivateRegionStartLBA + startIndex;
                    }
                }
                else
                {
                    freeCount = 0;
                }
            }

            return -1;
        }

        // On disks with 512-byte sectors Windows will reserve sector 0 and alternate between sectors 1 and 2 of the private region.
        // On disks with 4KB sectors Windows will alternate between sectors 0 and 1.
        public static long FindUnusedLBAForPrimaryToc(PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            bool[] bitmap = GetPrivateRegionUsageBitmap(privateHeader, tocBlock);
            for (int index = 0; index < bitmap.Length; index++)
            {
                if (bitmap[index] == false)
                {
                    return (long)privateHeader.PrivateRegionStartLBA + index;
                }
            }
            return -1;
        }

        // The secondary TOC is usually alternated between the third-last and the second-last sectors of the private region.
        public static long FindUnusedLBAForSecondaryToc(PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            bool[] bitmap = GetPrivateRegionUsageBitmap(privateHeader, tocBlock);
            for (int index = bitmap.Length - 1; index >= 0; index--)
            {
                if (bitmap[index] == false)
                {
                    return (long)privateHeader.PrivateRegionStartLBA + index;
                }
            }
            return -1;
        }

        private static bool[] GetPrivateRegionUsageBitmap(PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            // usage bitmap:
            bool[] bitmap = new bool[privateHeader.PrivateRegionSizeLBA];
            bitmap[privateHeader.PrimaryPrivateHeaderLBA] = true;
            bitmap[privateHeader.SecondaryPrivateHeaderLBA] = true;
            bitmap[privateHeader.PrimaryTocLBA] = true;
            bitmap[privateHeader.SecondaryTocLBA] = true;

            foreach (TOCRegion region in tocBlock.Regions)
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
