/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public partial class NTFSVolume : IExtendableFileSystem
    {
        public long GetMaximumSizeToExtend()
        {
            // The sector following the NTFS volume is a backup to the boot sector, we want to leave room for a new backup boot sector
            return m_volume.Size - (this.Size + m_volume.BytesPerSector);
        }

        public void Extend(long numberOfAdditionalSectors)
        {
            Extend((ulong)numberOfAdditionalSectors);
        }

        public void Extend(ulong numberOfAdditionalSectors)
        {
            ulong originalNumberOfSectors = m_bootRecord.TotalSectors;
            ulong currentNumberOfClusters = m_bootRecord.TotalSectors / m_bootRecord.SectorsPerCluster;
            ulong numberOfAdditionalClusters = numberOfAdditionalSectors / m_bootRecord.SectorsPerCluster;

            Extend(currentNumberOfClusters, numberOfAdditionalClusters);

            // We set TotalSectors only after extending the File system, or otherwise the $bitmap size will mismatch
            m_bootRecord.TotalSectors += numberOfAdditionalClusters * m_bootRecord.SectorsPerCluster; // we only add usable sectors

            // update boot sector
            byte[] bootRecordBytes = m_bootRecord.GetBytes();
            WriteSectors(0, bootRecordBytes);

            // recreate the backup boot sector at the new end of the raw volume
            // Note: The backup boot sector does not count as part of the NTFS volume
            long backupBootSectorIndex = (long)(originalNumberOfSectors + numberOfAdditionalSectors);
            WriteSectors(backupBootSectorIndex, bootRecordBytes);
        }

        // Note: there could be up to 2^64 clusters ( http://technet.microsoft.com/en-us/library/cc938432.aspx ) 
        private void Extend(ulong currentNumberOfClusters, ulong numberOfAdditionalClusters)
        {
            // Each bit in the $Bitmap file represents a cluster.
            // The size of the $Bitmap file is always a multiple of 8 bytes, extra bits are always set to 1.
            //
            // Note:
            // 1TB of additional allocation will result in a bitmap of 32 MB (assuming 4KB clusters)
            // 128TB of additional allocation will result in a bitmap of 512 MB (assuming 8KB clusters)
            byte[] bitmap;
            ulong nextClusterIndexInBitmap = 0; // the next cluster that will be allocated
            ulong writeOffset = m_bitmap.Length;

            if (currentNumberOfClusters % 64 > 0)
            {
                ulong numberOfClustersToAllocate = numberOfAdditionalClusters - (64 - (currentNumberOfClusters % 64));
                ulong numberOfBytesToAllocate = (ulong)Math.Ceiling((double)numberOfClustersToAllocate / 8);
                numberOfBytesToAllocate = (ulong)Math.Ceiling((double)numberOfBytesToAllocate / 8) * 8;

                // The last 8 bytes may contain extra bits that were previously set as used, and now have to be free. 
                // We extend the file before reading the last 8 bytes of the bitmap, because it's possible that during the extension,
                // clusters will be allocated from the last 8 bytes of the $bitmap file. for this reason, we must first extend the file, and then read the bitmap
                m_bitmap.ExtendFile(numberOfBytesToAllocate);

                bitmap = new byte[8 + numberOfBytesToAllocate];
                // We have to modify the last 8 bytes in the current bitmap (which will be the first in 'bitmap')
                writeOffset = writeOffset - 8;
                byte[] temp = m_bitmap.ReadFromFile(writeOffset, 8);
                Array.Copy(temp, bitmap, 8);

                nextClusterIndexInBitmap = currentNumberOfClusters % 64;
                while (nextClusterIndexInBitmap < 64)
                {
                    ClusterUsageBitmap.UpdateClusterStatus(bitmap, nextClusterIndexInBitmap, false);
                    nextClusterIndexInBitmap++;
                }

                nextClusterIndexInBitmap += numberOfClustersToAllocate;
            }
            else
            {
                ulong numberOfAdditionalBytes = (ulong)Math.Ceiling((double)numberOfAdditionalClusters / 8);
                numberOfAdditionalBytes = (ulong)Math.Ceiling((double)numberOfAdditionalBytes / 8) * 8;

                bitmap = new byte[numberOfAdditionalBytes];
                nextClusterIndexInBitmap = numberOfAdditionalClusters;
            }

            // mark extra bits as used:
            while (nextClusterIndexInBitmap < (ulong)bitmap.Length * 8)
            {
                ClusterUsageBitmap.UpdateClusterStatus(bitmap, nextClusterIndexInBitmap, true);
                nextClusterIndexInBitmap++;
            }

            m_bitmap.WriteToFile(writeOffset, bitmap);
        }
    }
}
