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

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class VolumeManagerDatabase
    {
        public const uint FirstSequenceNumber = 4; // SequenceNumber starts from 4 (0-3 are taken by the VMDB)

        private DynamicDisk m_disk;
        private VolumeManagerDatabaseHeader m_databaseHeader;
        private List<DatabaseRecord> m_databaseRecords;
        private KernelUpdateLog m_kernelUpdateLog;
        
        private ulong m_nextRecordID;

        public VolumeManagerDatabase(DynamicDisk disk, VolumeManagerDatabaseHeader databaseHeader, List<DatabaseRecord> databaseRecords, KernelUpdateLog kernelUpdateLog)
        {
            m_disk = disk;
            m_databaseHeader = databaseHeader;
            m_databaseRecords = databaseRecords;
            m_kernelUpdateLog = kernelUpdateLog;

            m_nextRecordID = m_databaseHeader.CommitTransactionID + 1;
        }

        public ulong AllocateNewRecordID()
        {
            m_nextRecordID++;
            return m_nextRecordID - 1;
        }

        // Steps to update the database (as performed by Windows):
        // -------------------------------------------------------
        // 1. We create all the new records as 'pending activation', and mark old records as 'pending deletion'.
        // 2. We mark the database header update status as 'Change', and update its PendingTransactionID and the number of pending VBlks.
        // 3. We update the KLog to store the new PendingTransactionID.
        // 4. We mark the database header update status as 'Commit'.
        // 5. We delete all the 'pending deletion' records, and activate all the 'pending activation' records
        // 6. We mark the database header as 'Clean', and update CommitTransactionID (set it to PendingTransactionID) and the number of committed VBlks.
        
        // Notes:
        // ------
        // 1. The volume manager database and kernel update log ('config' and 'log' regions) are identical across all disks (not including the PRIVHEAD
        //    and TOCBLOCKs of course), and should be kept identical (each step should be performed across all disks before proceeding to the next step).
        // 2.  I've always encountered steps 1 and 2 within the same write operation, so the order may be the other way around.
        // 3. If an update operation has been terminated (power failure) before step 4 has been reach, Windows will roll back the changes made,
        //    Once step 4 has been performed, Windows will commit the changes made.
        // 4. When a disk is being modified (volume is being added / deleted etc.), Windows / Veritas Storage Foundation updates the disk record,
        //    and a new CommitTransactionID is applied.
        
        /// <param name="records">New or modified records (.e.g. new volume, volume with modified size etc.)</param>
        public void UpdateDatabase(List<DatabaseRecord> records)
        {
            foreach (DatabaseRecord newRecord in records)
            {
                foreach (DatabaseRecord record in m_databaseRecords)
                {
                    if (newRecord == record)
                    {
                        // We probably forgot to clone the record we want to modify
                        throw new ArgumentException("New record must not reference record already in the database");
                    }
                }
            }
            VerifyDatabaseConsistency();

            // step 1:
            MarkOldRecordsAsPendingDeletion(records);
            // pendingDeletion records are now marked as 'PendingDeletion'

            // New records should get ID between CommitTransactionID and pendingTransactionID
            ulong pendingTransactionID = m_databaseHeader.CommitTransactionID + (ulong)records.Count + 1;

            PrepareNewRecordsForWriting(records, pendingTransactionID);
            // records are now marked as 'PendingActivation'

            m_databaseRecords.AddRange(records); // add new records to the record list
            WritePendingRecords(); // write changes to disk

            // step 2:
            m_databaseHeader.UpdateStatus = DatabaseHeaderUpdateStatus.Change;
            m_databaseHeader.PendingTransactionID = pendingTransactionID;
            m_databaseHeader.PendingTotalNumberOfVolumeRecords = GetPendingTotalNumberOfRecords<VolumeRecord>();
            m_databaseHeader.PendingTotalNumberOfComponentRecords = GetPendingTotalNumberOfRecords<ComponentRecord>();
            m_databaseHeader.PendingTotalNumberOfExtentRecords = GetPendingTotalNumberOfRecords<ExtentRecord>();
            m_databaseHeader.PendingTotalNumberOfDiskRecords = GetPendingTotalNumberOfRecords<DiskRecord>();
            WriteDatabaseHeader();

            // step 3:
            SetKernelUpdateLogLastEntry(m_databaseHeader.CommitTransactionID, pendingTransactionID);
            
            // step 4:
            m_databaseHeader.UpdateStatus = DatabaseHeaderUpdateStatus.Commit;
            WriteDatabaseHeader();
            
            // step 5:
            DeletePendingDeletionRecords();
            ActivatePendingActivationRecords();

            // step 6:
            m_databaseHeader.UpdateStatus = DatabaseHeaderUpdateStatus.Clean;
            m_databaseHeader.CommitTransactionID = pendingTransactionID;
            m_databaseHeader.CommittedTotalNumberOfVolumeRecords = (uint)this.VolumeRecords.Count;
            m_databaseHeader.CommittedTotalNumberOfComponentRecords = (uint)this.ComponentRecords.Count;
            m_databaseHeader.CommittedTotalNumberOfExtentRecords = (uint)this.ExtentRecords.Count;
            m_databaseHeader.CommittedTotalNumberOfDiskRecords = (uint)this.DiskRecords.Count;
            WriteDatabaseHeader();

            m_nextRecordID = m_databaseHeader.CommitTransactionID + 1;
        }

        virtual public void VerifyDatabaseConsistency()
        {
            if (m_databaseHeader.PendingTransactionID != m_databaseHeader.CommitTransactionID ||
                m_databaseHeader.UpdateStatus != DatabaseHeaderUpdateStatus.Clean)
            {
                throw new Exception("Database is in inconsistent state");
            }

            if (m_databaseHeader.MajorVersion != 4 || m_databaseHeader.MinorVersion != 10)
            {
                throw new NotImplementedException("Database version is not supported");
            }
        }

        /// <summary>
        /// mark old records as pending deletion and return them
        /// </summary>
        private void MarkOldRecordsAsPendingDeletion(List<DatabaseRecord> newRecords)
        {
            foreach (DatabaseRecord newRecord in newRecords)
            { 
                int index = m_databaseRecords.IndexOf(newRecord);
                if (index >= 0) // same record ID exist
                {
                    m_databaseRecords[index].UpdateStatus = DatabaseRecordUpdateStatus.ActivePendingDeletion;
                    m_databaseRecords[index].UpdateHeader();
                }
            }
        }

        /// <summary>
        /// Write all pending activation / pending deletion records to disk
        /// </summary>
        private void WritePendingRecords()
        {
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                if (record.UpdateStatus != DatabaseRecordUpdateStatus.Active)
                {
                    foreach (DatabaseRecordFragment fragment in record.Fragments)
                    {
                        WriteDatabaseRecordFragment(fragment);
                    }
                }
            }
        }

        private void DeletePendingDeletionRecords()
        {
            List<DatabaseRecord> pendingDeletion = new List<DatabaseRecord>();
            // find all 'PendingDeletion' records:
            for (int index = 0; index < m_databaseRecords.Count; index++)
            {
                if (m_databaseRecords[index].UpdateStatus == DatabaseRecordUpdateStatus.ActivePendingDeletion)
                {
                    pendingDeletion.Add(m_databaseRecords[index]);
                    m_databaseRecords.RemoveAt(index);
                    index--;
                }
            }

            // remove records from the disks
            foreach (DatabaseRecord record in pendingDeletion)
            {
                foreach (DatabaseRecordFragment fragment in record.Fragments)
                {
                    fragment.Clear();
                    WriteDatabaseRecordFragment(fragment);
                }
            }
        }

        private void ActivatePendingActivationRecords()
        {
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                if (record.UpdateStatus == DatabaseRecordUpdateStatus.PendingActivation)
                {
                    record.UpdateStatus = DatabaseRecordUpdateStatus.Active;
                    record.UpdateHeader();

                    foreach (DatabaseRecordFragment fragment in record.Fragments)
                    {
                        WriteDatabaseRecordFragment(fragment);
                    }
                }
            }
        }

        /// <summary>
        /// Assign valid SequenceNumber and GroupNumber to each record fragment
        /// </summary>
        private void PrepareNewRecordsForWriting(List<DatabaseRecord> newRecords, ulong pendingTransactionID)
        {
            uint startFromSequenceNumber = FirstSequenceNumber;
            uint startFromGroupNumber = 1;

            foreach (DatabaseRecord record in newRecords)
            {
                // we assign TransactionID to new records, or CommittedTransactionID to updated records that uses them
                // Note: The record ID tells us about the order of record creation (is it helpful during database recovery?)
                if (record.Id == 0)
                {
                    record.Id = AllocateNewRecordID();
                }

                if (record is VolumeRecord)
                {
                    ((VolumeRecord)record).CommitTransactionID = pendingTransactionID;
                }
                else if (record is ComponentRecord)
                {
                    ((ComponentRecord)record).CommitTransactionID = pendingTransactionID;
                }
                else if (record is ExtentRecord)
                {
                    ((ExtentRecord)record).CommitTransactionID = pendingTransactionID;
                }
                if (record is DiskRecord)
                {
                    ((DiskRecord)record).CommitTransactionID = pendingTransactionID;
                }
                else if (record is DiskGroupRecord)
                {
                    ((DiskGroupRecord)record).CommitTransactionID = pendingTransactionID;
                }

                record.UpdateStatus = DatabaseRecordUpdateStatus.PendingActivation;
                // any changes to the record header / data after this line will not be reflected:
                record.UpdateFragments((int)DatabaseHeader.BlockSize);

                uint groupNumber = GetAvailableFragmentGroupNumber(startFromGroupNumber);
                foreach (DatabaseRecordFragment fragment in record.Fragments)
                {
                    fragment.SequenceNumber = GetAvailableFragmentSequenceNumber(startFromSequenceNumber);
                    fragment.GroupNumber = groupNumber;

                    startFromSequenceNumber = fragment.SequenceNumber + 1;
                }

                startFromGroupNumber = groupNumber + 1;
            }
        }

        virtual protected void WriteDatabaseHeader()
        {
            VolumeManagerDatabaseHeader.WriteToDisk(m_disk, m_databaseHeader);
        }

        virtual protected void WriteDatabaseRecordFragment(DatabaseRecordFragment fragment)
        {
            WriteDatabaseRecordFragment(m_disk, fragment, (int)m_databaseHeader.BlockSize);
        }

        virtual protected void SetKernelUpdateLogLastEntry(ulong committedTransactionID, ulong pendingTransactionID)
        {
            m_kernelUpdateLog.SetLastEntry(m_disk, committedTransactionID, pendingTransactionID);
        }
        
        /// <param name="searchFrom">We use startSequenceNumber to avoid using the same SequenceNumber twice</param>
        public uint GetAvailableFragmentSequenceNumber(uint startFromSequenceNumber)
        {
            List<uint> sequenceNumbers = new List<uint>();
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                foreach (DatabaseRecordFragment fragment in record.Fragments)
                { 
                    sequenceNumbers.Add(fragment.SequenceNumber);
                }
            }
            sequenceNumbers.Sort();

            for (uint sequenceNumber = startFromSequenceNumber; sequenceNumber < m_databaseHeader.NumberOfVBlks; sequenceNumber++)
            {
                if (!sequenceNumbers.Contains(sequenceNumber))
                {
                    return sequenceNumber;
                }
            }

            throw new Exception("VMDB is full");
        }

        /// <param name="searchFrom">We use startFromGroupNumber to avoid using the same GroupNumber twice</param>
        public uint GetAvailableFragmentGroupNumber(uint startFromGroupNumber)
        {
            List<uint> groupNumbers = new List<uint>();
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                foreach (DatabaseRecordFragment fragment in record.Fragments)
                {
                    groupNumbers.Add(fragment.GroupNumber);
                }
            }
            groupNumbers.Sort();

            // number of groups can't be bigger than the number of fragments
            for (uint groupNumber = startFromGroupNumber; groupNumber < m_databaseHeader.NumberOfVBlks; groupNumber++)
            {
                if (!groupNumbers.Contains(groupNumber))
                {
                    return groupNumber;
                }
            }

            throw new Exception("VMDB is full, can't find available GroupNumber");
        }

        public List<T> GetRecords<T>() where T:DatabaseRecord
        { 
            List<T> result = new List<T>();
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                if (record is T)
                {
                    result.Add((T)record);
                }
            }
            return result;
        }

        public List<T> GetActiveRecords<T>() where T : DatabaseRecord
        {
            List<T> result = new List<T>();
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                if (record.UpdateStatus != DatabaseRecordUpdateStatus.PendingActivation && record is T)
                {
                    result.Add((T)record);
                }
            }
            return result;
        }

        public uint GetPendingTotalNumberOfRecords<T>() where T : DatabaseRecord
        {
            uint result = 0;
            foreach (DatabaseRecord record in m_databaseRecords)
            {
                if (record is T)
                {
                    if (record.UpdateStatus != DatabaseRecordUpdateStatus.ActivePendingDeletion)
                    {
                        result++;
                    }
                }
            }
            return result;
        }

        public VolumeRecord FindVolumeByVolumeID(ulong volumeID)
        {
            foreach (VolumeRecord record in this.VolumeRecords)
            {
                if (record.VolumeId == volumeID)
                {
                    return record;
                }
            }
            return null;
        }

        public VolumeRecord FindVolumeByVolumeGuid(Guid volumeGuid)
        {
            foreach (VolumeRecord record in this.VolumeRecords)
            {
                if (record.VolumeGuid == volumeGuid)
                {
                    return record;
                }
            }
            return null;
        }

        public List<ComponentRecord> FindComponentsByVolumeID(ulong volumeID)
        {
            List<ComponentRecord> result = new List<ComponentRecord>();
            foreach (ComponentRecord record in this.ComponentRecords)
            {
                if (record.VolumeId == volumeID)
                {
                    result.Add(record);
                }
            }
            return result;
        }

        /// <summary>
        /// Sorted by offset in column
        /// </summary>
        public List<ExtentRecord> FindExtentsByComponentID(ulong componentID)
        {
            List<ExtentRecord> result = new List<ExtentRecord>();
            foreach (ExtentRecord record in this.ExtentRecords)
            {
                if (record.ComponentId == componentID)
                {
                    result.Add(record);
                }
            }
            
            result.Sort(CompareByOffsetInColumn);
            //result.Sort(CompareByColumnIndex);
            return result;
        }
        
        public List<ExtentRecord> FindExtentsByDiskID(ulong diskID)
        {
            List<ExtentRecord> result = new List<ExtentRecord>();
            foreach (ExtentRecord record in this.ExtentRecords)
            {
                if (record.DiskId == diskID)
                {
                    result.Add(record);
                }
            }
            return result;
        }

        public ExtentRecord FindExtentByExtentID(ulong extentID)
        {
            foreach (ExtentRecord record in this.ExtentRecords)
            {
                if (record.ExtentId == extentID)
                {
                    return record;
                }
            }
            return null;
        }

        public DiskRecord FindDiskByDiskID(ulong diskID)
        {
            foreach (DiskRecord record in this.DiskRecords)
            {
                if (record.DiskId == diskID)
                {
                    return record;
                }
            }
            return null;
        }

        public DiskRecord FindDiskByDiskGuid(Guid diskGuid)
        {
            foreach (DiskRecord record in this.DiskRecords)
            {
                if (record.DiskGuid == diskGuid)
                {
                    return record;
                }
            }
            return null;
        }

        public VolumeManagerDatabaseHeader DatabaseHeader
        {
            get
            {
                return m_databaseHeader;
            }
        }

        public List<DatabaseRecord> DatabaseRecords
        {
            get
            {
                return m_databaseRecords;
            }
        }

        public KernelUpdateLog KernelUpdateLog
        {
            get
            {
                return m_kernelUpdateLog;
            }
        }
        
        public List<DiskRecord> DiskRecords
        {
            get
            {
                return GetActiveRecords<DiskRecord>();
            }
        }

        public List<VolumeRecord> VolumeRecords
        {
            get
            {
                return GetActiveRecords<VolumeRecord>();
            }
        }

        public List<ComponentRecord> ComponentRecords
        {
            get
            {
                return GetActiveRecords<ComponentRecord>();
            }
        }

        public List<DiskGroupRecord> DiskGroupRecords
        {
            get
            {
                return GetActiveRecords<DiskGroupRecord>();
            }
        }

        public List<ExtentRecord> ExtentRecords
        {
            get
            {
                return GetActiveRecords<ExtentRecord>();
            }
        }

        public Guid DiskGroupGuid
        {
            get
            {
                return m_databaseHeader.DiskGroupGuid;
            }
        }

        public string DiskGroupName
        {
            get
            {
                return m_databaseHeader.DiskGroupName;
            }
        }

        public static VolumeManagerDatabase ReadFromDisk(DynamicDisk disk)
        {
            return ReadFromDisk(disk.Disk, disk.PrivateHeader, disk.TOCBlock);
        }

        public static VolumeManagerDatabase ReadFromDisk(Disk disk)
        {
            if (DynamicDisk.IsDynamicDisk(disk))
            {
                PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(disk);
                if (privateHeader != null)
                {
                    return ReadFromDisk(disk, privateHeader);
                }
            }
            return null;
        }

        public static VolumeManagerDatabase ReadFromDisk(Disk disk, PrivateHeader privateHeader)
        {
            TOCBlock tocBlock = TOCBlock.ReadFromDisk(disk, privateHeader);
            if (tocBlock != null)
            {
                return ReadFromDisk(disk, privateHeader, tocBlock);
            }
            return null;
        }

        public static VolumeManagerDatabase ReadFromDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            VolumeManagerDatabaseHeader databaseHeader = VolumeManagerDatabaseHeader.ReadFromDisk(disk, privateHeader, tocBlock);
            if (databaseHeader == null)
            {
                return null;
            }
            List<DatabaseRecord> databaseRecords = new List<DatabaseRecord>();

            // The first VBLK entry is the subsequent entry to the VMDB, which located at (ConfigurationStartLBA + Item1Start)
            ulong firstSector = privateHeader.PrivateRegionStartLBA + tocBlock.ConfigStart + 1;  // we skip the VMDB
            int sectorCount = (int)Math.Ceiling(databaseHeader.NumberOfVBlks * databaseHeader.BlockSize / (double)disk.BytesPerSector);
            byte[] databaseBytes = disk.ReadSectors((long)firstSector, sectorCount);

            // read all VBLK blocks:
            // Note: fragments are not necessarily contiguous!
            Dictionary<uint, List<DatabaseRecordFragment>> fragments = new Dictionary<uint, List<DatabaseRecordFragment>>();
            for (uint index = 0; index < databaseHeader.NumberOfVBlks - 4; index++)
            {
                byte[] fragmentBytes = new byte[databaseHeader.BlockSize];
                Array.Copy(databaseBytes, (long)index * databaseHeader.BlockSize, fragmentBytes, 0, databaseHeader.BlockSize);
                DatabaseRecordFragment fragment = DatabaseRecordFragment.GetDatabaseRecordFragment(fragmentBytes);

                if (fragment != null) // null fragment means VBLK is empty
                {
                    if (fragments.ContainsKey(fragment.GroupNumber))
                    {
                        fragments[fragment.GroupNumber].Add(fragment);
                    }
                    else
                    {
                        List<DatabaseRecordFragment> recordFragments = new List<DatabaseRecordFragment>();
                        recordFragments.Add(fragment);
                        fragments.Add(fragment.GroupNumber, recordFragments);
                    }
                }
            }

            // We have all the fragments and we can now assemble the records:
            // We assume that fragments with lower FragmentNumber appear in the database before fragments
            // of the same group with higher FragmentNumber.
            foreach (List<DatabaseRecordFragment> recorFragments in fragments.Values)
            {
                DatabaseRecord databaseRecord = DatabaseRecord.GetDatabaseRecord(recorFragments);
                databaseRecords.Add(databaseRecord);
            }

            // read all KLog blocks
            KernelUpdateLog kernelUpdateLog = KernelUpdateLog.ReadFromDisk(disk, privateHeader, tocBlock);
            DynamicDisk dynamicDisk = new DynamicDisk(disk, privateHeader, tocBlock);
            return new VolumeManagerDatabase(dynamicDisk, databaseHeader, databaseRecords, kernelUpdateLog);
        }

        public static void WriteDatabaseRecordFragment(DynamicDisk disk, DatabaseRecordFragment fragment, int blockSize)
        {
            if (fragment.SequenceNumber < 4)
            {
                throw new ArgumentException("VBLK SequenceNumber must start from 4");
            }

            PrivateHeader privateHeader = disk.PrivateHeader;
            TOCBlock tocBlock = disk.TOCBlock;
            ulong sectorIndex = privateHeader.PrivateRegionStartLBA + tocBlock.ConfigStart;
            int fragmentsPerSector = (int)(disk.Disk.BytesPerSector / blockSize);
            sectorIndex += (ulong)(fragment.SequenceNumber / fragmentsPerSector);
            byte[] sectorBytes = disk.Disk.ReadSector((long)sectorIndex);
            byte[] fragmentBytes = fragment.GetBytes(blockSize); // should we use the same database header?
            int indexInSector = (int)(fragment.SequenceNumber % fragmentsPerSector);
            Array.Copy(fragmentBytes, 0, sectorBytes, indexInSector * blockSize, blockSize);
            disk.Disk.WriteSectors((long)sectorIndex, sectorBytes);
        }

        private static int CompareByColumnIndex(ExtentRecord x, ExtentRecord y)
        {
            return x.ColumnIndex.CompareTo(y.ColumnIndex);
        }

        private static int CompareByOffsetInColumn(ExtentRecord x, ExtentRecord y)
        {
            return x.OffsetInColumnLBA.CompareTo(y.OffsetInColumnLBA);
        }
    }
}
