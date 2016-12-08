/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DiskAccessLibrary
{
    public class MoveHelper
    {
        /// <summary>
        /// When a user want to move an extent one sector to the left/right (e.g. for alignment purposes),
        /// the regular operation method will mandate reading and writing one sector at a time,
        /// this can be extremely slow, and to avoid this, we use free space on the disk to "buffer" the data read.
        /// (this will allow us to recover from a power failure)
        /// </summary>
        public const int BufferedModeThresholdLBA = 64;

        public static void MoveExtentDataRight(Volume volume, DiskExtent sourceExtent, DiskExtent relocatedExtent, MoveExtentOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            // we make sure no data will be overwritten too soon:
            long distanceLBA = (long)(resumeRecord.NewStartSector - resumeRecord.OldStartSector);
            bool bufferedMode = false;
            if (distanceLBA < BufferedModeThresholdLBA)
            {
                bufferedMode = true;
            }

            int transferSizeLBA;
            if (bufferedMode)
            {
                transferSizeLBA = (int)resumeRecord.BackupBufferSizeLBA;
            }
            else
            {
                transferSizeLBA = (int)Math.Min(Settings.MaximumTransferSizeLBA, distanceLBA);
            }

            // move the data
            for (long readCount = (long)resumeRecord.NumberOfCommittedSectors; readCount < relocatedExtent.TotalSectors; readCount += transferSizeLBA)
            {
                // we read (and write) from the end of the extent and progress to the left
                long sectorsLeft = relocatedExtent.TotalSectors - readCount;
                int sectorsToRead = (int)Math.Min(transferSizeLBA, sectorsLeft);

                long sectorIndex = relocatedExtent.TotalSectors - readCount - sectorsToRead;

                byte[] data = sourceExtent.ReadSectors(sectorIndex, sectorsToRead);

                if (bufferedMode)
                {
                    // we write the data to the buffer for recovery purposes
                    relocatedExtent.Disk.WriteSectors((long)resumeRecord.BackupBufferStartSector, data);
                    resumeRecord.RestoreFromBuffer = true;
                    // Note: if the extent we move is the first in the volume, we will write the resume record to
                    // the source extent, which is the one that the database is still referring to
                    volume.WriteSectors(0, resumeRecord.GetBytes());
                }
                relocatedExtent.WriteSectors(sectorIndex, data);

                // update the resume record
                resumeRecord.RestoreFromBuffer = false;
                resumeRecord.NumberOfCommittedSectors += (ulong)sectorsToRead;
                volume.WriteSectors(0, resumeRecord.GetBytes());
                bytesCopied = (long)resumeRecord.NumberOfCommittedSectors * sourceExtent.BytesPerSector;
            }
        }

        public static void MoveExtentDataLeft(Volume volume, DiskExtent sourceExtent, DiskExtent relocatedExtent, MoveExtentOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            // we make sure no data will be overwritten too soon:
            long distanceLBA = (long)(resumeRecord.OldStartSector - resumeRecord.NewStartSector);
            bool bufferedMode = false;
            if (distanceLBA < BufferedModeThresholdLBA)
            {
                bufferedMode = true;
            }

            int transferSizeLBA;
            if (bufferedMode)
            {
                transferSizeLBA = (int)resumeRecord.BackupBufferSizeLBA; ;
            }
            else
            {
                transferSizeLBA = (int)Math.Min(Settings.MaximumTransferSizeLBA, distanceLBA);
            }

            // move the data
            for (long sectorIndex = (long)resumeRecord.NumberOfCommittedSectors; sectorIndex < relocatedExtent.TotalSectors; sectorIndex += transferSizeLBA)
            {
                long sectorsLeft = relocatedExtent.TotalSectors - sectorIndex;
                int sectorsToRead = (int)Math.Min(transferSizeLBA, sectorsLeft);

                byte[] data = sourceExtent.ReadSectors(sectorIndex, sectorsToRead);
                if (bufferedMode)
                {
                    // we write the data to the buffer for recovery purposes
                    relocatedExtent.Disk.WriteSectors((long)resumeRecord.BackupBufferStartSector, data);
                    resumeRecord.RestoreFromBuffer = true;
                    relocatedExtent.WriteSectors(0, resumeRecord.GetBytes());
                }
                relocatedExtent.WriteSectors(sectorIndex, data);

                // update the resume record
                resumeRecord.RestoreFromBuffer = false;
                resumeRecord.NumberOfCommittedSectors += (ulong)sectorsToRead;
                volume.WriteSectors(0, resumeRecord.GetBytes());
                bytesCopied = (long)resumeRecord.NumberOfCommittedSectors * sourceExtent.BytesPerSector;
            }
        }
    }
}
