/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class VolumeManagerDatabaseHelper
    {
        public static void ConvertRaidToStripedVolume(DiskGroupDatabase database, Guid volumeGuid)
        {
            List<DatabaseRecord> records = new List<DatabaseRecord>();

            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volumeGuid);
            if (volumeRecord == null)
            {
                throw new MissingDatabaseRecordException("Volume record is missing");
            }
            volumeRecord = (VolumeRecord)volumeRecord.Clone();
            volumeRecord.VolumeTypeString = "gen";
            volumeRecord.ReadPolicy = ReadPolicyName.Select;
            volumeRecord.VolumeFlags = VolumeFlags.DefaultUnknown | VolumeFlags.Writeback;
            records.Add(volumeRecord);

            ComponentRecord componentRecord = database.FindComponentsByVolumeID(volumeRecord.VolumeId)[0];
            if (componentRecord == null)
            {
                throw new MissingDatabaseRecordException("Component record is missing");
            }
            componentRecord = (ComponentRecord)componentRecord.Clone();
            componentRecord.ExtentLayout = ExtentLayoutName.Stripe;
            records.Add(componentRecord);

            database.UpdateDatabase(records);
        }

        public static void ConvertStripedVolumeToRaid(DiskGroupDatabase database, Guid volumeGuid)
        {
            List<DatabaseRecord> records = new List<DatabaseRecord>();

            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volumeGuid);
            if (volumeRecord == null)
            {
                throw new MissingDatabaseRecordException("Volume record is missing");
            }
            volumeRecord = (VolumeRecord)volumeRecord.Clone();
            volumeRecord.VolumeTypeString = "raid5";
            volumeRecord.ReadPolicy = ReadPolicyName.RAID;
            volumeRecord.VolumeFlags = VolumeFlags.DefaultUnknown | VolumeFlags.Writeback | VolumeFlags.Writecopy;
            records.Add(volumeRecord);

            ComponentRecord componentRecord = database.FindComponentsByVolumeID(volumeRecord.VolumeId)[0];
            if (componentRecord == null)
            {
                throw new MissingDatabaseRecordException("Component record is missing");
            }
            componentRecord = (ComponentRecord)componentRecord.Clone();
            componentRecord.ExtentLayout = ExtentLayoutName.RAID5;
            records.Add(componentRecord);

            database.UpdateDatabase(records);
        }

        /// <summary>
        /// Update the database (add the new extent)
        /// </summary>
        /// <param name="volume">RAID-5 or striped volume</param>
        /// <returns>new extent ID</returns>
        public static ulong AddNewExtentToVolume(DiskGroupDatabase database, DynamicVolume volume, DiskExtent newExtent)
        {
            PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(newExtent.Disk);

            List<DatabaseRecord> records = new List<DatabaseRecord>();

            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volume.VolumeGuid);
            if (volumeRecord == null)
            {
                throw new MissingDatabaseRecordException("Volume record is missing");
            }
            volumeRecord = (VolumeRecord)volumeRecord.Clone();
            volumeRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(newExtent.TotalSectors, privateHeader);
            records.Add(volumeRecord);

            ComponentRecord componentRecord = database.FindComponentsByVolumeID(volumeRecord.VolumeId)[0];
            if (componentRecord == null)
            {
                throw new MissingDatabaseRecordException("Component record is missing");
            }
            componentRecord = (ComponentRecord)componentRecord.Clone();
            componentRecord.NumberOfExtents++;
            componentRecord.NumberOfColumns++;
            records.Add(componentRecord);

            DiskRecord diskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
            if (diskRecord == null)
            {
                throw new MissingDatabaseRecordException("Disk record is missing");
            }
            diskRecord = (DiskRecord)diskRecord.Clone();
            records.Add(diskRecord);

            ExtentRecord newExtentRecord = new ExtentRecord();
            newExtentRecord.Name = GetNextExtentName(database.ExtentRecords, diskRecord.Name);
            newExtentRecord.ComponentId = componentRecord.ComponentId;
            newExtentRecord.DiskId = diskRecord.DiskId;
            newExtentRecord.DiskOffsetLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionLBA(newExtent.FirstSector, privateHeader);
            newExtentRecord.SizeLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(newExtent.TotalSectors, privateHeader);
            newExtentRecord.HasColumnIndexFlag = true;
            newExtentRecord.ColumnIndex = (uint)volume.Columns.Count; // zero based
                        
            records.Add(newExtentRecord);

            // we should update the disk records and extent records
            foreach (DynamicDiskExtent extent in volume.Extents)
            {
                ExtentRecord extentRecord = database.FindExtentByExtentID(extent.ExtentID);
                if (extentRecord == null)
                {
                    throw new MissingDatabaseRecordException("Extent record is missing");
                }
                extentRecord = (ExtentRecord)extentRecord.Clone();
                records.Add(extentRecord);

                diskRecord = database.FindDiskByDiskID(extentRecord.DiskId);
                if (diskRecord == null)
                {
                    throw new MissingDatabaseRecordException("Disk record is missing");
                }
                // there could be multiple extents on the same disk, make sure we only add each disk once
                if (!records.Contains(diskRecord))
                {
                    diskRecord = (DiskRecord)diskRecord.Clone();
                    records.Add(diskRecord);
                }
            }

            database.UpdateDatabase(records);

            return newExtentRecord.ExtentId;
        }

        /// <summary>
        /// Update the database to point to the new extent location (same or different disk)
        /// </summary>
        public static void UpdateExtentLocation(DiskGroupDatabase database, DynamicVolume volume, DynamicDiskExtent relocatedExtent)
        {
            PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(relocatedExtent.Disk);

            DiskRecord targetDiskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volume.VolumeGuid);

            List<DatabaseRecord> records = new List<DatabaseRecord>();
            ExtentRecord sourceExtentRecord = database.FindExtentByExtentID(relocatedExtent.ExtentID);
            ExtentRecord relocatedExtentRecord = (ExtentRecord)sourceExtentRecord.Clone();
            relocatedExtentRecord.DiskId = targetDiskRecord.DiskId;
            relocatedExtentRecord.DiskOffsetLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionLBA(relocatedExtent.FirstSector, privateHeader);
            records.Add(relocatedExtentRecord);

            // we should update the disk records
            foreach (DynamicDiskExtent extent in volume.Extents)
            {
                DiskRecord diskRecord = database.FindDiskByDiskID(relocatedExtentRecord.DiskId);
                // there could be multiple extents on the same disk, make sure we only add each disk once
                if (!records.Contains(diskRecord))
                {
                    diskRecord = (DiskRecord)diskRecord.Clone();
                    records.Add(diskRecord);
                }
            }

            // when moving to a new disk, we should update the new disk record as well
            if (!records.Contains(targetDiskRecord))
            {
                records.Add(targetDiskRecord.Clone());
            }

            database.UpdateDatabase(records);
        }

        public static void ExtendSimpleVolume(DiskGroupDatabase database, SimpleVolume volume, long numberOfAdditionalSectors)
        {
            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volume.VolumeGuid);
            volumeRecord = (VolumeRecord)volumeRecord.Clone();
            volumeRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(numberOfAdditionalSectors, volume.BytesPerSector);
            ExtentRecord extentRecord = database.FindExtentByExtentID(volume.DiskExtent.ExtentID);
            extentRecord = (ExtentRecord)extentRecord.Clone();
            extentRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(numberOfAdditionalSectors, volume.BytesPerSector);
            DiskRecord diskRecord = database.FindDiskByDiskID(extentRecord.DiskId); // we should update the disk, see Database.cs
            diskRecord = (DiskRecord)diskRecord.Clone();

            List<DatabaseRecord> records = new List<DatabaseRecord>();
            records.Add(volumeRecord);
            records.Add(extentRecord);
            records.Add(diskRecord);

            database.UpdateDatabase(records);
        }

        public static void ExtendStripedVolume(DiskGroupDatabase database, StripedVolume volume, long numberOfAdditionalExtentSectors)
        {
            if (numberOfAdditionalExtentSectors % volume.SectorsPerStripe > 0)
            {
                throw new ArgumentException("Number of additional sectors must be multiple of stripes per sector");
            }

            List<DatabaseRecord> records = new List<DatabaseRecord>();

            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volume.VolumeGuid);
            volumeRecord = (VolumeRecord)volumeRecord.Clone();
            volumeRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(numberOfAdditionalExtentSectors * volume.NumberOfColumns, volume.BytesPerSector);
            records.Add(volumeRecord);

            // we only want to extend the last extent in each column
            foreach (DynamicColumn column in volume.Columns)
            {
                DynamicDiskExtent lastExtent = column.Extents[column.Extents.Count - 1];
                ExtentRecord extentRecord = database.FindExtentByExtentID(lastExtent.ExtentID);
                extentRecord = (ExtentRecord)extentRecord.Clone();
                extentRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(numberOfAdditionalExtentSectors, volume.BytesPerSector);
                records.Add(extentRecord);

                DiskRecord diskRecord = database.FindDiskByDiskID(extentRecord.DiskId); // we should update the disk, see Database.cs
                diskRecord = (DiskRecord)diskRecord.Clone();
                records.Add(diskRecord);
            }

            database.UpdateDatabase(records);
        }

        public static void ExtendRAID5Volume(DiskGroupDatabase database, Raid5Volume volume, long numberOfAdditionalExtentSectors)
        {
            if (numberOfAdditionalExtentSectors % volume.SectorsPerStripe > 0)
            {
                throw new ArgumentException("Number of additional sectors must be multiple of stripes per sector");
            }

            List<DatabaseRecord> records = new List<DatabaseRecord>();

            VolumeRecord volumeRecord = database.FindVolumeByVolumeGuid(volume.VolumeGuid);
            volumeRecord = (VolumeRecord)volumeRecord.Clone();
            volumeRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(numberOfAdditionalExtentSectors * (volume.NumberOfColumns - 1), volume.BytesPerSector);
            records.Add(volumeRecord);

            foreach (DynamicColumn column in volume.Columns)
            {
                DynamicDiskExtent lastExtent = column.Extents[column.Extents.Count - 1];
                ExtentRecord extentRecord = database.FindExtentByExtentID(lastExtent.ExtentID);
                extentRecord = (ExtentRecord)extentRecord.Clone();
                extentRecord.SizeLBA += (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(numberOfAdditionalExtentSectors, volume.BytesPerSector);
                records.Add(extentRecord);

                DiskRecord diskRecord = database.FindDiskByDiskID(extentRecord.DiskId); // we should update the disk, see Database.cs
                diskRecord = (DiskRecord)diskRecord.Clone();
                records.Add(diskRecord);
            }

            database.UpdateDatabase(records);
        }
        
        public static ulong CreateSimpleVolume(DiskGroupDatabase database, DiskExtent extent)
        {
            List<DatabaseRecord> records = new List<DatabaseRecord>();

            VolumeRecord volumeRecord = new VolumeRecord();
            volumeRecord.Id = database.AllocateNewRecordID();
            volumeRecord.Name = GetNextSimpleVolumeName(database.VolumeRecords);
            volumeRecord.VolumeTypeString = "gen";
            volumeRecord.StateString = "ACTIVE";
            volumeRecord.ReadPolicy = ReadPolicyName.Select;
            volumeRecord.VolumeNumber = GetNextVolumeNumber(database.VolumeRecords);
            volumeRecord.VolumeFlags = VolumeFlags.Writeback | VolumeFlags.DefaultUnknown;
            volumeRecord.NumberOfComponents = 1;
            volumeRecord.SizeLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(extent.TotalSectors, extent.BytesPerSector);
            volumeRecord.PartitionType = PartitionType.RAW;
            volumeRecord.VolumeGuid = Guid.NewGuid();
            records.Add(volumeRecord);

            ComponentRecord componentRecord = new ComponentRecord();
            componentRecord.Id = database.AllocateNewRecordID();
            componentRecord.Name = volumeRecord.Name + "-01";
            componentRecord.StateString = "ACTIVE";
            componentRecord.ExtentLayout = ExtentLayoutName.Concatenated;
            componentRecord.NumberOfExtents = 1;
            componentRecord.VolumeId = volumeRecord.VolumeId;
            componentRecord.HasStripedExtentsFlag = false;
            componentRecord.NumberOfColumns = 0;
            records.Add(componentRecord);

            // we should update the disk record
            PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(extent.Disk);
            DiskRecord diskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
            diskRecord = (DiskRecord)diskRecord.Clone();
            records.Add(diskRecord);

            ExtentRecord extentRecord = new ExtentRecord();
            extentRecord.Name = GetNextExtentName(database.ExtentRecords, diskRecord.Name);
            extentRecord.DiskOffsetLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionLBA(extent.FirstSector, privateHeader);
            extentRecord.SizeLBA = volumeRecord.SizeLBA;
            extentRecord.ComponentId = componentRecord.ComponentId;
            extentRecord.DiskId = diskRecord.DiskId;

            extentRecord.HasColumnIndexFlag = false;

            records.Add(extentRecord);

            database.UpdateDatabase(records);

            return volumeRecord.VolumeId;
        }

        public static ulong CreateRAID5Volume(DiskGroupDatabase database, List<DiskExtent> extents, bool isDegraded)
        {
            int numberOfColumns;
            if (isDegraded)
            {
                numberOfColumns = extents.Count + 1;
            }
            else
            {
                numberOfColumns = extents.Count;
            }

            List<DatabaseRecord> records = new List<DatabaseRecord>();
           
            VolumeRecord volumeRecord = new VolumeRecord();
            volumeRecord.Id = database.AllocateNewRecordID();
            volumeRecord.Name = GetNextRAIDVolumeName(database.VolumeRecords);
            volumeRecord.VolumeTypeString = "raid5";
            volumeRecord.StateString = "ACTIVE";
            volumeRecord.ReadPolicy = ReadPolicyName.RAID;
            volumeRecord.VolumeNumber = GetNextVolumeNumber(database.VolumeRecords);
            volumeRecord.VolumeFlags = VolumeFlags.Writeback | VolumeFlags.Writecopy | VolumeFlags.DefaultUnknown;
            volumeRecord.NumberOfComponents = 1;
            volumeRecord.SizeLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(extents[0].TotalSectors * (numberOfColumns - 1), extents[0].BytesPerSector);
            volumeRecord.PartitionType = PartitionType.RAW;
            volumeRecord.VolumeGuid = Guid.NewGuid();
            records.Add(volumeRecord);

            ComponentRecord componentRecord = new ComponentRecord();
            componentRecord.Id = database.AllocateNewRecordID();
            componentRecord.Name = volumeRecord.Name + "-01";
            componentRecord.StateString = "ACTIVE";
            componentRecord.ExtentLayout = ExtentLayoutName.RAID5;
            componentRecord.NumberOfExtents = (uint)numberOfColumns;
            componentRecord.VolumeId = volumeRecord.VolumeId;
            componentRecord.HasStripedExtentsFlag = true;
            componentRecord.StripeSizeLBA = 128; // 64KB - the default
            componentRecord.NumberOfColumns = (uint)numberOfColumns;
            records.Add(componentRecord);

            for(int index = 0; index < extents.Count; index++)
            {
                DiskExtent extent = extents[index];

                // we should update the disk records
                PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(extent.Disk);
                DiskRecord diskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
                diskRecord = (DiskRecord)diskRecord.Clone();
                records.Add(diskRecord);

                ExtentRecord extentRecord = new ExtentRecord();
                extentRecord.Name = GetNextExtentName(database.ExtentRecords, diskRecord.Name);
                extentRecord.DiskOffsetLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionLBA(extent.FirstSector, privateHeader);
                extentRecord.SizeLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(extent.TotalSectors, extent.BytesPerSector);
                extentRecord.ComponentId = componentRecord.ComponentId;
                extentRecord.DiskId = diskRecord.DiskId;
                
                extentRecord.HasColumnIndexFlag = (index > 0);
                extentRecord.ColumnIndex = (uint)index; // zero based

                records.Add(extentRecord);
            }

            if (isDegraded)
            { 
                // we have to make-up a disk
                // The DiskFlags and ExtentFlags are not necessary (they will be added later anyway when the disk group is reimported)
                DiskRecord diskRecord = new DiskRecord();
                diskRecord.Id = database.AllocateNewRecordID();
                diskRecord.Name = "Miss" + new Random().Next(100);
                diskRecord.DiskGuid = Guid.NewGuid();
                diskRecord.DiskFlags = DiskFlags.Detached;
                records.Add(diskRecord);

                ExtentRecord extentRecord = new ExtentRecord();
                extentRecord.Name = diskRecord.Name + "-01";
                extentRecord.ExtentFlags = ExtentFlags.Recover;
                extentRecord.SizeLBA = (ulong)PublicRegionHelper.TranslateToPublicRegionSizeLBA(extents[0].TotalSectors, extents[0].BytesPerSector);
                extentRecord.ComponentId = componentRecord.ComponentId;
                extentRecord.DiskId = diskRecord.DiskId;
                extentRecord.HasColumnIndexFlag = true;
                extentRecord.ColumnIndex = (uint)extents.Count; // zero based

                records.Add(extentRecord);
            }

            database.UpdateDatabase(records);

            return volumeRecord.VolumeId;
        }

        private static string GetNextSimpleVolumeName(List<VolumeRecord> volumeRecords)
        {
            return GetNextVolumeName(volumeRecords, "Volume");
        }

        private static string GetNextSpannedVolumeName(List<VolumeRecord> volumeRecords)
        {
            return GetNextVolumeName(volumeRecords, "Volume");
        }

        private static string GetNextStripedVolumeName(List<VolumeRecord> volumeRecords)
        {
            return GetNextVolumeName(volumeRecords, "Stripe");
        }

        private static string GetNextRAIDVolumeName(List<VolumeRecord> volumeRecords)
        {
            return GetNextVolumeName(volumeRecords, "Raid");
        }

        private static string GetNextVolumeName(List<VolumeRecord> volumeRecords, string prefix)
        {
            int index = 1;
            while (true)
            {
                string name = prefix + index.ToString();
                bool isNameAvailable = true;
                foreach (VolumeRecord volumeRecord in volumeRecords)
                {
                    if (volumeRecord.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        isNameAvailable = false;
                        break;
                    }
                }
                if (isNameAvailable)
                {
                    return name;
                }
                index++;
            }
        }

        /// <param name="extentRecords">Could be all of the database records or just the relevant records</param>
        public static string GetNextExtentName(List<ExtentRecord> extentRecords, string diskName)
        {
            int index = 1;
            while (true)
            {
                string name = String.Format("{0}-{1}", diskName, index.ToString("00"));
                bool isNameAvailable = true;
                foreach (ExtentRecord extentRecord in extentRecords)
                {
                    if (extentRecord.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        isNameAvailable = false;
                        break;
                    }
                }
                if (isNameAvailable)
                {
                    return name;
                }
                index++;
            }
        }

        public static uint GetNextVolumeNumber(List<VolumeRecord> volumeRecords)
        {
            // volume number starts with 5 (probably because 1-4 are reserved for partitions)
            uint volumeNumber = 5;
            foreach (VolumeRecord record in volumeRecords)
            {
                if (record.VolumeNumber >= volumeNumber)
                {
                    volumeNumber = record.VolumeNumber + 1;
                }
            }
            return volumeNumber;
        }
    }
}
