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
    public class PublicRegionHelper
    {
        /// <summary>
        /// LBA values in extent / volume / component records refer to logical 512-byte blocks within the public region of the disk, regardless of the actual block size of the disk.
        /// We have to translate those values in order to support disks with 4K sectors. 
        /// </summary>
        public const int BytesPerPublicRegionSector = 512;

        public static long TranslateFromPublicRegionLBA(long publicRegionOffsetLBA, long publicRegionStartLBA, int bytesPerDiskSector)
        {
            return publicRegionStartLBA + publicRegionOffsetLBA * BytesPerPublicRegionSector / bytesPerDiskSector;
        }

        public static long TranslateFromPublicRegionSizeLBA(long sectorCount, int bytesPerDiskSector)
        {
            return sectorCount * BytesPerPublicRegionSector / bytesPerDiskSector;
        }

        public static long TranslateToPublicRegionLBA(long sectorIndex, PrivateHeader privateHeader)
        {
            return TranslateToPublicRegionLBA(sectorIndex, (long)privateHeader.PublicRegionStartLBA, (int)privateHeader.BytesPerSector);
        }

        public static long TranslateToPublicRegionLBA(long sectorIndex, long publicRegionStartLBA, int bytesPerDiskSector)
        {
            return (sectorIndex - publicRegionStartLBA) * bytesPerDiskSector / BytesPerPublicRegionSector;
        }

        public static long TranslateToPublicRegionSizeLBA(long sectorCount, PrivateHeader privateHeader)
        {
            return TranslateToPublicRegionSizeLBA(sectorCount, (int)privateHeader.BytesPerSector);
        }

        public static long TranslateToPublicRegionSizeLBA(long sectorCount, int bytesPerDiskSector)
        {
            return sectorCount * bytesPerDiskSector / BytesPerPublicRegionSector;
        }
    }
}
