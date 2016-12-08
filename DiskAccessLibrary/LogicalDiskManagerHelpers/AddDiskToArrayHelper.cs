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
    public class AddDiskToArrayHelper
    {
        public static void AddDiskToRaid5Volume(List<DynamicDisk> disks, Raid5Volume volume, DiskExtent newExtent, ref long bytesCopied)
        {
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(disks, volume.DiskGroupGuid);
            if (database == null)
            {
                throw new DatabaseNotFoundException();
            }
            // If there will be a power failure during the conversion, our RAID volume will resync during boot,
            // To prevent destruction of the data, we temporarily convert the array to striped volume
            VolumeManagerDatabaseHelper.ConvertRaidToStripedVolume(database, volume.VolumeGuid);
            ulong newExtentID = VolumeManagerDatabaseHelper.AddNewExtentToVolume(database, volume, newExtent);

            // Backup the first sector of the first extent to the last sector of the new extent
            // (We replace the filesystem boot record with our own sector for recovery purposes)
            byte[] filesystemBootRecord = volume.Extents[0].ReadSector(0);
            newExtent.WriteSectors(newExtent.TotalSectors - 1, filesystemBootRecord);

            AddDiskOperationBootRecord resumeRecord = new AddDiskOperationBootRecord();
            resumeRecord.VolumeGuid = volume.VolumeGuid;
            PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(newExtent.Disk);
            // privateHeader cannot be null at this point
            resumeRecord.NumberOfCommittedSectors = 0;

            // we use volume.WriteSectors so that the parity information will be update
            // this way, we could recover the first sector of each extent if a disk will fail
            volume.WriteSectors(0, resumeRecord.GetBytes());

            ResumeAddDiskToRaid5Volume(disks, volume, new DynamicDiskExtent(newExtent, newExtentID), resumeRecord, ref bytesCopied);
        }

        public static void ResumeAddDiskToRaid5Volume(List<DynamicDisk> disks, StripedVolume stripedVolume, AddDiskOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            List<DynamicColumn> columns = stripedVolume.Columns;
            DynamicDiskExtent newExtent = columns[columns.Count - 1].Extents[0];
            columns.RemoveAt(columns.Count - 1);

            Raid5Volume volume = new Raid5Volume(columns, stripedVolume.SectorsPerStripe, stripedVolume.VolumeGuid, stripedVolume.DiskGroupGuid);
            volume.VolumeID = stripedVolume.VolumeID;
            volume.Name = stripedVolume.Name;
            volume.DiskGroupName = stripedVolume.DiskGroupName;
            ResumeAddDiskToRaid5Volume(disks, volume, newExtent, resumeRecord, ref bytesCopied);
        }

        private static void ResumeAddDiskToRaid5Volume(List<DynamicDisk> disks, Raid5Volume volume, DynamicDiskExtent newExtent, AddDiskOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            // When reading from the volume, we must use the old volume (without the new disk)
            // However, when writing the boot sector to the volume, we must use the new volume or otherwise parity information will be invalid
            List<DynamicColumn> newVolumeColumns = new List<DynamicColumn>();
            newVolumeColumns.AddRange(volume.Columns);
            newVolumeColumns.Add(new DynamicColumn(newExtent));
            Raid5Volume newVolume = new Raid5Volume(newVolumeColumns, volume.SectorsPerStripe, volume.VolumeGuid, volume.DiskGroupGuid);

            int oldColumnCount = volume.Columns.Count;
            int newColumnCount = oldColumnCount + 1;

            long resumeFromStripe = (long)resumeRecord.NumberOfCommittedSectors / volume.SectorsPerStripe;
            // it would be prudent to write the new extent before committing to the operation, however, it would take much longer.
            
            // The number of sectors in extent / column is always a multiple of SectorsPerStripe.

            // We read enough stripes to write a vertical stripe segment in the new array,
            // We will read MaximumTransferSizeLBA and make sure maximumStripesToTransfer is multiple of (NumberOfColumns - 1).
            int maximumStripesToTransfer = (Settings.MaximumTransferSizeLBA / volume.SectorsPerStripe) / (newColumnCount - 1) * (newColumnCount - 1);
            long totalStripesInVolume = volume.TotalStripes;

            long stripeIndexInVolume = resumeFromStripe;
            while (stripeIndexInVolume < totalStripesInVolume)
            {
                // When we add a column, the distance between the stripes we read (later in the column) to thes one we write (earlier),
                // Is growing constantly (because we can stack more stripes in each vertical stripe), so we increment the number of stripes we
                // can safely transfer as we go.
                // (We assume that the segment we write will be corrupted if there will be a power failure)

                long stripeToReadIndexInColumn = stripeIndexInVolume / (oldColumnCount - 1);
                long stripeToWriteIndexInColumn = stripeIndexInVolume / (newColumnCount - 1);
                long numberOfStripesSafeToTransfer = (stripeToReadIndexInColumn - stripeToWriteIndexInColumn) * (newColumnCount - 1);
                bool verticalStripeAtRisk = (numberOfStripesSafeToTransfer == 0);
                if (numberOfStripesSafeToTransfer == 0)
                {
                    // The first few stripes in each column are 'at rist', meaning that we may overwrite crucial data (that is only stored in memory) 
                    // when writing the segment that will be lost forever if a power failure will occur during the write operation.
                    // Note: The number of 'at risk' vertical stripes is equal to the number of columns in the old array - 1
                    numberOfStripesSafeToTransfer = (newColumnCount - 1);
                }
                int numberOfStripesToTransfer = (int)Math.Min(numberOfStripesSafeToTransfer, maximumStripesToTransfer);

                long stripesLeft = totalStripesInVolume - stripeIndexInVolume;
                numberOfStripesToTransfer = (int)Math.Min(numberOfStripesToTransfer, stripesLeft);
                byte[] segmentData = volume.ReadStripes(stripeIndexInVolume, numberOfStripesToTransfer);

                if (numberOfStripesToTransfer % (newColumnCount - 1) > 0)
                {
                    // this is the last segment and we need to zero-fill it for the write:
                    int numberOfStripesToWrite = (int)Math.Ceiling((double)numberOfStripesToTransfer / (newColumnCount - 1)) * (newColumnCount - 1);
                    byte[] temp = new byte[numberOfStripesToWrite * volume.BytesPerStripe];
                    Array.Copy(segmentData, temp, segmentData.Length);
                    segmentData = temp;
                }

                long firstStripeIndexInColumn = stripeIndexInVolume / (newColumnCount - 1);
                if (verticalStripeAtRisk)
                {
                    // we write 'at risk' stripes one at a time to the new volume, this will make sure they will not overwrite crucial data
                    // (because they will be written in an orderly fashion, and not in bulk from the first column to the last)
                    newVolume.WriteStripes(stripeIndexInVolume, segmentData);
                }
                else
                {
                    WriteSegment(volume, newExtent, firstStripeIndexInColumn, segmentData);
                }

                // update resume record
                resumeRecord.NumberOfCommittedSectors += (ulong)(numberOfStripesToTransfer * volume.SectorsPerStripe);
                bytesCopied = (long)resumeRecord.NumberOfCommittedSectors * volume.BytesPerSector;
                newVolume.WriteSectors(0, resumeRecord.GetBytes());

                stripeIndexInVolume += numberOfStripesToTransfer;
            }

            // we're done, let's restore the filesystem boot record
            byte[] filesystemBootRecord = newExtent.ReadSector(newExtent.TotalSectors - 1);
            newVolume.WriteSectors(0, filesystemBootRecord);
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(disks, volume.DiskGroupGuid);
            VolumeManagerDatabaseHelper.ConvertStripedVolumeToRaid(database, volume.VolumeGuid);
        }

        /// <summary>
        /// Segment - sequence of stripes that is a multiple of (NumberOfColumns - 1),
        /// and every (NumberOfColumns - 1) stripes in the sequence have the same stripeIndexInColumn
        /// (Such sequence can be written to disk without reading the parity information first)
        /// </summary>
        public static void WriteSegment(Raid5Volume volume, DynamicDiskExtent newExtent, long firstStripeIndexInColumn, byte[] data)
        {
            List<DynamicColumn> newArray = new List<DynamicColumn>();
            newArray.AddRange(volume.Columns);
            newArray.Add(new DynamicColumn(newExtent));

            int bytesPerStripe = volume.BytesPerStripe;

            int stripesToWritePerColumn = (data.Length / bytesPerStripe) / (newArray.Count - 1);
            
            int dataLengthPerColumn = stripesToWritePerColumn * bytesPerStripe;
            byte[][] columnData = new byte[newArray.Count][];
            for(int index = 0; index < columnData.Length; index++)
            {
                columnData[index] = new byte[dataLengthPerColumn];
            }

            Parallel.For(0, stripesToWritePerColumn, delegate(int stripeOffsetInColumn)
            {
                long stripeIndexInColumn = firstStripeIndexInColumn + stripeOffsetInColumn;
                int parityColumnIndex = (newArray.Count - 1) - (int)(stripeIndexInColumn % newArray.Count);

                byte[] parityData = new byte[bytesPerStripe];
                for (int stripeVerticalIndex = 0; stripeVerticalIndex < newArray.Count - 1; stripeVerticalIndex++)
                {
                    int columnIndex = (parityColumnIndex + 1 + stripeVerticalIndex) % newArray.Count;

                    long stripeOffsetInData = (stripeOffsetInColumn * (newArray.Count - 1) + stripeVerticalIndex) * bytesPerStripe;
                    Array.Copy(data, stripeOffsetInData, columnData[columnIndex], stripeOffsetInColumn * bytesPerStripe, bytesPerStripe);

                    parityData = ByteUtils.XOR(parityData, 0, columnData[columnIndex], stripeOffsetInColumn * bytesPerStripe, bytesPerStripe);
                }
                Array.Copy(parityData, 0, columnData[parityColumnIndex], stripeOffsetInColumn * bytesPerStripe, bytesPerStripe);
            });

            // write the data
            long firstSectorIndexInColumn = firstStripeIndexInColumn * volume.SectorsPerStripe;
            for (int columnIndex = 0; columnIndex < newArray.Count; columnIndex++)
            {
                newArray[columnIndex].WriteSectors(firstSectorIndexInColumn, columnData[columnIndex]);
            }
        }
    }
}
