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
    public partial class DynamicVolumeHelper
    {
        public static DynamicVolume GetVolumeByGuid(List<DynamicDisk> disks, Guid volumeGuid)
        {
            List<DynamicVolume> volumes = GetDynamicVolumes(disks);
            foreach (DynamicVolume volume in volumes)
            {
                if (volume.VolumeGuid == volumeGuid)
                {
                    return volume;
                }
            }
            return null;
        }

        public static List<DynamicVolume> GetDynamicVolumes(List<DynamicDisk> disks)
        {
            List<DynamicVolume> result = new List<DynamicVolume>();
            
            List<DiskGroupDatabase> diskGroupDatabases = DiskGroupDatabase.ReadFromDisks(disks);
            foreach (DiskGroupDatabase database in diskGroupDatabases)
            {
                foreach (VolumeRecord volumeRecord in database.VolumeRecords)
                {
                    DynamicVolume volume = GetVolume(disks, database, volumeRecord);
                    result.Add(volume);
                }
            }

            return result;
        }

        /// <summary>
        /// Return volumes that are stored (or partially stored) on the given disk
        /// </summary>
        [Obsolete]
        public static List<DynamicVolume> GetDynamicDiskVolumes(DynamicDisk disk)
        {
            VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(disk);
            List<DynamicDisk> disks = new List<DynamicDisk>();
            disks.Add(disk);

            List<DynamicVolume> result = new List<DynamicVolume>();
            if (database != null)
            {
                foreach (VolumeRecord volumeRecord in database.VolumeRecords)
                {
                    DynamicVolume volume = GetVolume(disks, database, volumeRecord);
                    if (volume != null)
                    {
                        foreach (DynamicDiskExtent extent in volume.Extents)
                        {
                            if (extent.DiskGuid == disk.DiskGuid)
                            {
                                result.Add(volume);
                                break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static DynamicVolume GetVolume(List<DynamicDisk> disks, VolumeManagerDatabase database, VolumeRecord volumeRecord)
        {
            List<ComponentRecord> componentRecords = database.FindComponentsByVolumeID(volumeRecord.VolumeId);
            if (volumeRecord.NumberOfComponents != (ulong)componentRecords.Count || componentRecords.Count == 0)
            { 
                // database record is invalid
                throw new InvalidDataException("Number of components in volume record does not match actual number of component records");
            }
            
            if (componentRecords.Count == 1)
            {
                ComponentRecord componentRecord = componentRecords[0];
                return GetVolume(disks, database, volumeRecord, componentRecord);
                
            }
            else // Mirrored volume
            {
                // Mirrored Simple Volume is the only kind of mirror suppored by Windows (only 2-way mirror is supported)
                // Veritas also supports Mirrored Stripe / Mirrored RAID-5 / Mirrored Spanned Volume (up to 32-way mirror is supported)
                List<DynamicVolume> volumes = new List<DynamicVolume>();
                foreach (ComponentRecord componentRecord in componentRecords)
                {
                    DynamicVolume volume = GetVolume(disks, database, volumeRecord, componentRecord);
                    volumes.Add(volume);
                }

                MirroredVolume mirroredVolume = new MirroredVolume(volumes, volumeRecord.VolumeGuid, database.DiskGroupGuid);
                mirroredVolume.VolumeID = volumeRecord.VolumeId;
                mirroredVolume.Name = volumeRecord.Name;
                return mirroredVolume;
            }
        }

        private static DynamicVolume GetVolume(List<DynamicDisk> disks, VolumeManagerDatabase database, VolumeRecord volumeRecord, ComponentRecord componentRecord)
        {
            if (componentRecord.ExtentLayout == ExtentLayoutName.Concatenated)
            {
                if (componentRecord.NumberOfExtents == 1)
                {
                    // Simple volume
                    return GetSimpleVolume(disks, database, componentRecord, volumeRecord); ;
                }
                else
                {
                    // spanned volume
                    SpannedVolume volume = GetSpannedVolume(disks, database, componentRecord, volumeRecord);
                    return volume;
                }
            }
            else if (componentRecord.ExtentLayout == ExtentLayoutName.Stripe)
            {
                // striped volume
                StripedVolume volume = GetStripedVolume(disks, database, componentRecord, volumeRecord);
                return volume;
            }
            else if (componentRecord.ExtentLayout == ExtentLayoutName.RAID5)
            {
                Raid5Volume volume = GetRAID5Volume(disks, database, componentRecord, volumeRecord);
                return volume;
            }
            else
            {
                return null;
            }
        }

        private static List<DynamicColumn> GetDynamicVolumeColumns(List<DynamicDisk> disks, VolumeManagerDatabase database, ComponentRecord componentRecord, VolumeRecord volumeRecord)
        {
            // extentRecords are sorted by offset in column
            List<ExtentRecord> extentRecords = database.FindExtentsByComponentID(componentRecord.ComponentId);
            if (componentRecord.NumberOfExtents != extentRecords.Count || extentRecords.Count == 0)
            {
                // database record is invalid
                throw new InvalidDataException("Number of extents in component record does not match actual number of extent records");
            }

            SortedList<uint, List<DynamicDiskExtent>> columns = new SortedList<uint,List<DynamicDiskExtent>>();

            foreach (ExtentRecord extentRecord in extentRecords)
            {
                DiskRecord diskRecord = database.FindDiskByDiskID(extentRecord.DiskId);
                DynamicDisk disk = DynamicDiskHelper.FindDisk(disks, diskRecord.DiskGuid); // we add nulls as well
                DynamicDiskExtent extent = DynamicDiskExtentHelper.GetDiskExtent(disk, extentRecord);

                if (columns.ContainsKey(extentRecord.ColumnIndex))
                {
                    columns[extentRecord.ColumnIndex].Add(extent);
                }
                else
                { 
                    List<DynamicDiskExtent> list = new List<DynamicDiskExtent>();
                    list.Add(extent);
                    columns.Add(extentRecord.ColumnIndex, list);
                }
            }

            List<DynamicColumn> result = new List<DynamicColumn>();
            foreach (List<DynamicDiskExtent> extents in columns.Values)
            {
                result.Add(new DynamicColumn(extents));
            }
            return result;
        }

        private static SimpleVolume GetSimpleVolume(List<DynamicDisk> disks, VolumeManagerDatabase database, ComponentRecord componentRecord, VolumeRecord volumeRecord)
        {
            List<ExtentRecord> extentRecords = database.FindExtentsByComponentID(componentRecord.ComponentId);
            if (extentRecords.Count == 1)
            {
                ExtentRecord extentRecord = extentRecords[0];

                DiskRecord diskRecord = database.FindDiskByDiskID(extentRecord.DiskId);
                DynamicDisk disk = DynamicDiskHelper.FindDisk(disks, diskRecord.DiskGuid); // we add nulls as well
                DynamicDiskExtent extent = DynamicDiskExtentHelper.GetDiskExtent(disk, extentRecord);

                SimpleVolume volume = new SimpleVolume(extent, volumeRecord.VolumeGuid, database.DiskGroupGuid);
                volume.VolumeID = volumeRecord.VolumeId;
                volume.Name = volumeRecord.Name;
                volume.DiskGroupName = database.DiskGroupName;
                return volume;
            }
            else
            {
                // component / extent records are invalid
                throw new InvalidDataException("Number of extents in component record does not match actual number of extent records");
            }
        }

        private static Raid5Volume GetRAID5Volume(List<DynamicDisk> disks, VolumeManagerDatabase database, ComponentRecord componentRecord, VolumeRecord volumeRecord)
        {
            List<DynamicColumn> columns = GetDynamicVolumeColumns(disks, database, componentRecord, volumeRecord);

            Raid5Volume volume = new Raid5Volume(columns, (int)componentRecord.StripeSizeLBA, volumeRecord.VolumeGuid, database.DiskGroupGuid);
            volume.VolumeID = volumeRecord.VolumeId;
            volume.Name = volumeRecord.Name;
            volume.DiskGroupName = database.DiskGroupName;
            return volume;
        }

        private static StripedVolume GetStripedVolume(List<DynamicDisk> disks, VolumeManagerDatabase database, ComponentRecord componentRecord, VolumeRecord volumeRecord)
        {
            List<DynamicColumn> columns = GetDynamicVolumeColumns(disks, database, componentRecord, volumeRecord);

            StripedVolume volume = new StripedVolume(columns, (int)componentRecord.StripeSizeLBA, volumeRecord.VolumeGuid, database.DiskGroupGuid);
            volume.VolumeID = volumeRecord.VolumeId;
            volume.Name = volumeRecord.Name;
            volume.DiskGroupName = database.DiskGroupName;
            return volume;
        }

        private static SpannedVolume GetSpannedVolume(List<DynamicDisk> disks, VolumeManagerDatabase database, ComponentRecord componentRecord, VolumeRecord volumeRecord)
        {
            List<DynamicColumn> columns = GetDynamicVolumeColumns(disks, database, componentRecord, volumeRecord);

            SpannedVolume volume = new SpannedVolume(columns[0], volumeRecord.VolumeGuid, database.DiskGroupGuid);
            volume.VolumeID = volumeRecord.VolumeId;
            volume.Name = volumeRecord.Name;
            volume.DiskGroupName = database.DiskGroupName;
            return volume;
        }
    }
}
