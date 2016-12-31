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
using DiskAccessLibrary;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public partial class DiskGroupDatabase : VolumeManagerDatabase
    {
        List<DynamicDisk> m_disks = new List<DynamicDisk>(); // when updating the database, all dynamic disks in the group should be added

        public DiskGroupDatabase(List<DynamicDisk> disks, VolumeManagerDatabaseHeader databaseHeader, List<DatabaseRecord> databaseRecords, KernelUpdateLog kernelUpdateLog)
            : base(databaseHeader, databaseRecords, kernelUpdateLog)
        {
            m_disks = disks;
        }

        public override void WriteDatabaseHeader()
        {
            foreach (DynamicDisk disk in m_disks)
            {
                VolumeManagerDatabaseHeader.WriteToDisk(disk, this.DatabaseHeader);
            }
        }

        public override void WriteDatabaseRecordFragment(DatabaseRecordFragment fragment)
        {
            foreach (DynamicDisk disk in m_disks)
            {
                VolumeManagerDatabase.WriteDatabaseRecordFragment(disk, fragment, (int)this.DatabaseHeader.BlockSize);
            }
        }

        public override void SetKernelUpdateLogLastEntry(ulong committedTransactionID, ulong pendingTransactionID)
        {
            foreach (DynamicDisk disk in m_disks)
            {
                this.KernelUpdateLog.SetLastEntry(disk, committedTransactionID, pendingTransactionID);
            }
        }

        public static DiskGroupDatabase ReadFromDisks(List<DynamicDisk> disks, Guid diskGroupGuid)
        {
            List<DiskGroupDatabase> diskGroupDatabaseList = ReadFromDisks(disks);
            foreach (DiskGroupDatabase database in diskGroupDatabaseList)
            {
                if (database.DiskGroupGuid == diskGroupGuid)
                {
                    return database;
                }
            }
            return null;
        }

        public static List<DiskGroupDatabase> ReadFromDisks(List<DynamicDisk> disks)
        {
            Dictionary<Guid, List<DynamicDisk>> groups = new Dictionary<Guid, List<DynamicDisk>>();
            foreach (DynamicDisk disk in disks)
            {
                Guid diskGroupGuid = disk.PrivateHeader.DiskGroupGuid;
                if (groups.ContainsKey(diskGroupGuid))
                {
                    groups[diskGroupGuid].Add(disk);
                }
                else
                {
                    List<DynamicDisk> list = new List<DynamicDisk>();
                    list.Add(disk);
                    groups.Add(diskGroupGuid, list);
                }
            }

            List<DiskGroupDatabase> result = new List<DiskGroupDatabase>();
            foreach (List<DynamicDisk> groupDisks in groups.Values)
            {
                VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(groupDisks[0]);
                if (database != null && !database.IsDirty)
                {
                    DiskGroupDatabase groupDatabase = new DiskGroupDatabase(groupDisks, database.DatabaseHeader, database.DatabaseRecords, database.KernelUpdateLog);
                    // if there is issue with one disk we skip the group entirely
                    if (!groupDatabase.IsDirty)
                    {
                        result.Add(groupDatabase);
                    }
                }
            }

            return result;
        }

        public override bool IsDirty
        {
            get
            {
                return !this.IsIdenticalAcrossAllDisks || base.IsDirty;
            }
        }

        public bool IsIdenticalAcrossAllDisks
        {
            get
            {
                // Make sure database is identical across disks
                for (int index = 1; index < m_disks.Count; index++)
                {
                    VolumeManagerDatabase seconary = VolumeManagerDatabase.ReadFromDisk(m_disks[index]);
                    if (seconary.IsDirty)
                    {
                        return false;
                    }

                    if (this.DatabaseHeader.DiskGroupGuidString != seconary.DatabaseHeader.DiskGroupGuidString ||
                        this.DatabaseHeader.DiskGroupName != seconary.DatabaseHeader.DiskGroupName)
                    {
                        return false;
                    }

                    if (this.DatabaseHeader.CommitTransactionID != seconary.DatabaseHeader.CommitTransactionID)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public bool AreDisksMissing
        {
            get
            {
                List<DiskRecord> diskRecords = this.DiskRecords;
                foreach (DiskRecord diskRecord in diskRecords)
                {
                    if (DynamicDiskHelper.FindDisk(m_disks, diskRecord.DiskGuid) == null)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public List<DynamicDisk> Disks
        {
            get
            {
                return m_disks;
            }
        }
    }
}
