/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        List<DynamicDisk> m_disks = new List<DynamicDisk>(); // when updating the database, all dynamic disks in the system should be added

        public DiskGroupDatabase(List<DynamicDisk> disks, VolumeManagerDatabaseHeader databaseHeader, List<DatabaseRecord> databaseRecords, KernelUpdateLog kernelUpdateLog)
            : base(disks[0], databaseHeader, databaseRecords, kernelUpdateLog)
        {
            m_disks = disks;
        }

        public override void VerifyDatabaseConsistency()
        {
            base.VerifyDatabaseConsistency();
            
            // Make sure database is identical across disks
            for (int index = 1; index < m_disks.Count; index++)
            {
                VolumeManagerDatabase seconary = VolumeManagerDatabase.ReadFromDisk(m_disks[index]);
                seconary.VerifyDatabaseConsistency();

                if (this.DatabaseHeader.DiskGroupGuidString != seconary.DatabaseHeader.DiskGroupGuidString ||
                    this.DatabaseHeader.DiskGroupName != seconary.DatabaseHeader.DiskGroupName)
                {
                    throw new NotImplementedException("More than one disk group detected");
                }

                if (this.DatabaseHeader.CommitTransactionID != seconary.DatabaseHeader.CommitTransactionID)
                {
                    throw new NotImplementedException("Inconsistent disk group state");
                }
            }
        }

        protected override void WriteDatabaseHeader()
        {
            foreach (DynamicDisk disk in m_disks)
            {
                VolumeManagerDatabaseHeader.WriteToDisk(disk, this.DatabaseHeader);
            }
        }

        protected override void WriteDatabaseRecordFragment(DatabaseRecordFragment fragment)
        {
            foreach (DynamicDisk disk in m_disks)
            {
                VolumeManagerDatabase.WriteDatabaseRecordFragment(disk, fragment, (int)this.DatabaseHeader.BlockSize);
            }
        }

        protected override void SetKernelUpdateLogLastEntry(ulong committedTransactionID, ulong pendingTransactionID)
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
                if (database != null) // if there is issue with one disk (such as unsupported version) we skip the group entirely
                {
                    DiskGroupDatabase groupDatabase = new DiskGroupDatabase(groupDisks, database.DatabaseHeader, database.DatabaseRecords, database.KernelUpdateLog);
                    result.Add(groupDatabase);
                }
            }

            return result;
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
