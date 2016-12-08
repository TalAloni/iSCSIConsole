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
using Utilities;
using DiskAccessLibrary;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class MoveExtentHelper
    {
        public const int BackupBufferSizeLBA = 128; // there are about 180 contiguous free sectors in a private region

        /// <summary>
        /// Move extent to another disk
        /// </summary>
        public static void MoveExtentToAnotherDisk(List<DynamicDisk> disks, DynamicVolume volume, DynamicDiskExtent sourceExtent, DiskExtent relocatedExtent, ref long bytesCopied)
        {
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(disks, volume.DiskGroupGuid);
            if (database == null)
            {
                throw new DatabaseNotFoundException();
            }

            // copy the data
            long transferSizeLBA = Settings.MaximumTransferSizeLBA;
            for (long sectorIndex = 0; sectorIndex < relocatedExtent.TotalSectors; sectorIndex += transferSizeLBA)
            {
                long sectorsLeft = relocatedExtent.TotalSectors - sectorIndex;
                int sectorsToRead = (int)Math.Min(transferSizeLBA, sectorsLeft);

                byte[] data = sourceExtent.ReadSectors(sectorIndex, sectorsToRead);
                
                relocatedExtent.WriteSectors(sectorIndex, data);

                bytesCopied += sectorsToRead * sourceExtent.BytesPerSector;
            }

            // Update the database to point to the relocated extent
            DynamicDisk targetDisk = DynamicDisk.ReadFromDisk(relocatedExtent.Disk);
            DynamicDiskExtent dynamicRelocatedExtent = new DynamicDiskExtent(relocatedExtent, sourceExtent.ExtentID);
            dynamicRelocatedExtent.Name = sourceExtent.Name;
            dynamicRelocatedExtent.DiskGuid = targetDisk.DiskGuid;
            VolumeManagerDatabaseHelper.UpdateExtentLocation(database, volume, dynamicRelocatedExtent);
        }

        /// <summary>
        /// Move extent to a new location on the same disk
        /// </summary>
        public static void MoveExtentWithinSameDisk(List<DynamicDisk> disks, DynamicVolume volume, DynamicDiskExtent sourceExtent, DiskExtent relocatedExtent, ref long bytesCopied)
        {
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(disks, volume.DiskGroupGuid);
            if (database == null)
            {
                throw new DatabaseNotFoundException();
            }

            MoveExtentOperationBootRecord resumeRecord = new MoveExtentOperationBootRecord();
            // If there will be a power failure during the move, a RAID volume will resync during boot,
            // To prevent destruction of the data, we temporarily convert the array to striped volume
            if (volume is Raid5Volume)
            {
                VolumeManagerDatabaseHelper.ConvertRaidToStripedVolume(database, volume.VolumeGuid);
                resumeRecord.RestoreRAID5 = true;
            }

            // We want to write our own volume boot sector for recovery purposes, so we must find where to backup the old boot sector.
            // We don't want to store the backup in the range of the existing or relocated extent, because then we would have to move
            // the backup around during the move operation, other options include:
            // 1. Store it between sectors 1-62 (cons: Could be in use, Windows occasionally start a volume from sector 1)
            // 2. Find an easily compressible sector (e.g. zero-filled) within the existing extent, overwrite it with the backup, and restore it when the operation is done.
            // 3. use the LDM private region to store the sector.

            DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(relocatedExtent.Disk);
            // Note: backupSectorIndex will be from the beginning of the private region while backupBufferStartSector will be from the end
            // so there is no need to allocate them
            long backupSectorIndex = DynamicDiskHelper.FindUnusedSectorInPrivateRegion(dynamicDisk);

            resumeRecord.VolumeGuid = volume.VolumeGuid;
            resumeRecord.NumberOfCommittedSectors = 0;
            resumeRecord.ExtentID = sourceExtent.ExtentID;
            resumeRecord.OldStartSector = (ulong)sourceExtent.FirstSector;
            resumeRecord.NewStartSector = (ulong)relocatedExtent.FirstSector;
            resumeRecord.BootRecordBackupSector = (ulong)backupSectorIndex;

            long distanceLBA = (long)Math.Abs((double)resumeRecord.NewStartSector - resumeRecord.OldStartSector);
            if (distanceLBA < MoveHelper.BufferedModeThresholdLBA)
            {
                long backupBufferStartSector = DynamicDiskHelper.FindUnusedRegionInPrivateRegion(dynamicDisk, BackupBufferSizeLBA);
                if (backupBufferStartSector == -1)
                {
                    throw new Exception("Private region is full");
                }

                if (backupBufferStartSector <= backupSectorIndex)
                {
                    throw new Exception("Private region structure is unknown");
                }
                resumeRecord.BackupBufferStartSector = (ulong)backupBufferStartSector;
                resumeRecord.BackupBufferSizeLBA = BackupBufferSizeLBA;
            }

            // Backup the first sector of the first extent
            // (We replace the filesystem boot record with our own sector for recovery purposes)
            byte[] filesystemBootRecord = volume.ReadSector(0);
            relocatedExtent.Disk.WriteSectors(backupSectorIndex, filesystemBootRecord);

            // we write the resume record instead of the boot record
            volume.WriteSectors(0, resumeRecord.GetBytes());

            if (sourceExtent.FirstSector < relocatedExtent.FirstSector)
            {
                // move right
                MoveExtentRight(disks, volume, resumeRecord, ref bytesCopied);
            }
            else
            { 
                // move left

                // we write the resume record at the new location as well (to be able to resume if a power failure will occur immediately after updating the database)
                relocatedExtent.WriteSectors(0, resumeRecord.GetBytes());
                DynamicDiskExtent dynamicRelocatedExtent = new DynamicDiskExtent(relocatedExtent, sourceExtent.ExtentID);
                dynamicRelocatedExtent.Name = sourceExtent.Name;
                dynamicRelocatedExtent.DiskGuid = sourceExtent.DiskGuid;
                VolumeManagerDatabaseHelper.UpdateExtentLocation(database, volume, dynamicRelocatedExtent);
                int extentIndex = DynamicDiskExtentHelper.GetIndexOfExtentID(volume.DynamicExtents, sourceExtent.ExtentID);
                // get the updated volume (we just moved an extent)
                volume = DynamicVolumeHelper.GetVolumeByGuid(disks, volume.VolumeGuid);
                MoveExtentLeft(disks, volume, resumeRecord, ref bytesCopied);
            }
        }

        public static void ResumeMoveExtent(List<DynamicDisk> disks, DynamicVolume volume, MoveExtentOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            if (resumeRecord.OldStartSector == resumeRecord.NewStartSector)
            {
                throw new InvalidDataException("Invalid move record");
            }

            if (resumeRecord.RestoreFromBuffer)
            {
                // we need to use the backup buffer to restore the data that may have been overwritten
                int extentIndex = DynamicDiskExtentHelper.GetIndexOfExtentID(volume.DynamicExtents, resumeRecord.ExtentID);
                DynamicDiskExtent sourceExtent = volume.DynamicExtents[extentIndex];

                byte[] backupBuffer = sourceExtent.Disk.ReadSectors((long)resumeRecord.BackupBufferStartSector, BackupBufferSizeLBA);
                if (resumeRecord.OldStartSector < resumeRecord.NewStartSector)
                {
                    // move right
                    long readCount = (long)resumeRecord.NumberOfCommittedSectors;
                    int sectorsToRead = BackupBufferSizeLBA;
                    long sectorIndex = sourceExtent.TotalSectors - readCount - sectorsToRead;
                    sourceExtent.WriteSectors(sectorIndex, backupBuffer);

                    System.Diagnostics.Debug.WriteLine("Restored to " + sectorIndex);
                }
                else
                {
                    // move left
                    long sectorIndex = (long)resumeRecord.NumberOfCommittedSectors;
                    sourceExtent.WriteSectors(sectorIndex, backupBuffer);

                    System.Diagnostics.Debug.WriteLine("Restored to " + sectorIndex);
                }
            }

            if (resumeRecord.OldStartSector < resumeRecord.NewStartSector)
            {
                MoveExtentRight(disks, volume, resumeRecord, ref bytesCopied);
            }
            else
            {
                MoveExtentLeft(disks, volume, resumeRecord, ref bytesCopied);
            }
        }

        private static void MoveExtentRight(List<DynamicDisk> disks, DynamicVolume volume, MoveExtentOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(disks, volume.DiskGroupGuid);
            if (database == null)
            {
                throw new DatabaseNotFoundException();
            }

            int extentIndex = DynamicDiskExtentHelper.GetIndexOfExtentID(volume.DynamicExtents, resumeRecord.ExtentID);
            DynamicDiskExtent sourceExtent = volume.DynamicExtents[extentIndex];
            DiskExtent relocatedExtent = new DiskExtent(sourceExtent.Disk, (long)resumeRecord.NewStartSector, sourceExtent.Size);

            MoveHelper.MoveExtentDataRight(volume, sourceExtent, relocatedExtent, resumeRecord, ref bytesCopied);

            // even if the database update won't complete, the resume record was copied 

            // update the database
            DynamicDiskExtent dynamicRelocatedExtent = new DynamicDiskExtent(relocatedExtent, sourceExtent.ExtentID);
            dynamicRelocatedExtent.Name = sourceExtent.Name;
            dynamicRelocatedExtent.DiskGuid = sourceExtent.DiskGuid;
            VolumeManagerDatabaseHelper.UpdateExtentLocation(database, volume, dynamicRelocatedExtent);

            // if this is a resume, then volume is StripedVolume, otherwise it is a Raid5Volume
            if (resumeRecord.RestoreRAID5)
            {
                VolumeManagerDatabaseHelper.ConvertStripedVolumeToRaid(database, volume.VolumeGuid);
            }
            // get the updated volume (we moved an extent and possibly reconverted to RAID-5)
            volume = DynamicVolumeHelper.GetVolumeByGuid(disks, volume.VolumeGuid);

            // restore the filesystem boot sector
            byte[] filesystemBootRecord = relocatedExtent.Disk.ReadSector((long)resumeRecord.BootRecordBackupSector);
            volume.WriteSectors(0, filesystemBootRecord);

            ClearBackupData(relocatedExtent.Disk, resumeRecord);
        }

        private static void MoveExtentLeft(List<DynamicDisk> disks, DynamicVolume volume, MoveExtentOperationBootRecord resumeRecord, ref long bytesCopied)
        {
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(disks, volume.DiskGroupGuid);
            if (database == null)
            {
                throw new DatabaseNotFoundException();
            }

            DynamicDiskExtent relocatedExtent = DynamicDiskExtentHelper.GetByExtentID(volume.DynamicExtents, resumeRecord.ExtentID);
            if (resumeRecord.OldStartSector == (ulong)relocatedExtent.FirstSector)
            { 
                // the database update was not completed (this must be a resume operation)
                relocatedExtent = new DynamicDiskExtent(relocatedExtent.Disk, (long)resumeRecord.NewStartSector, relocatedExtent.Size, resumeRecord.ExtentID);
                VolumeManagerDatabaseHelper.UpdateExtentLocation(database, volume, relocatedExtent);
            }

            DiskExtent sourceExtent = new DiskExtent(relocatedExtent.Disk, (long)resumeRecord.OldStartSector, relocatedExtent.Size);

            MoveHelper.MoveExtentDataLeft(volume, sourceExtent, relocatedExtent, resumeRecord, ref bytesCopied);
            
            // if this is a resume, then volume is StripedVolume, otherwise it is a Raid5Volume
            if (resumeRecord.RestoreRAID5)
            {
                VolumeManagerDatabaseHelper.ConvertStripedVolumeToRaid(database, volume.VolumeGuid);
                // get the updated volume (we just reconverted to RAID-5)
                volume = DynamicVolumeHelper.GetVolumeByGuid(disks, volume.VolumeGuid);
            }
            
            // restore the filesystem boot sector
            byte[] filesystemBootRecord = relocatedExtent.Disk.ReadSector((long)resumeRecord.BootRecordBackupSector);
            volume.WriteSectors(0, filesystemBootRecord);

            ClearBackupData(relocatedExtent.Disk, resumeRecord);
        }

        private static void ClearBackupData(Disk relocatedExtentDisk, MoveExtentOperationBootRecord resumeRecord)
        {
            byte[] emptySector = new byte[relocatedExtentDisk.BytesPerSector];
            relocatedExtentDisk.WriteSectors((long)resumeRecord.BootRecordBackupSector, emptySector);
            if (resumeRecord.BackupBufferStartSector > 0)
            {
                byte[] emptyRegion = new byte[resumeRecord.BackupBufferSizeLBA * relocatedExtentDisk.BytesPerSector];
                relocatedExtentDisk.WriteSectors((long)resumeRecord.BackupBufferStartSector, emptyRegion);
            }
        }
    }
}
