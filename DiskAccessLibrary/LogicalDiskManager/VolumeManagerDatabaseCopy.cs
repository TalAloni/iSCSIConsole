/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager
{
    /// <summary>
    /// A Volume Manager Database copy stored on a specific disk
    /// </summary>
    public class VolumeManagerDatabaseCopy : VolumeManagerDatabase
    {
        private DynamicDisk m_disk;
        private VolumeManagerDatabaseHeader m_databaseHeader;
        private List<DatabaseRecord> m_databaseRecords;
        private KernelUpdateLog m_kernelUpdateLog;

        public VolumeManagerDatabaseCopy(DynamicDisk disk, VolumeManagerDatabaseHeader databaseHeader, List<DatabaseRecord> databaseRecords, KernelUpdateLog kernelUpdateLog) :
            base(databaseHeader, databaseRecords, kernelUpdateLog)
        {
            m_disk = disk;
            m_databaseHeader = databaseHeader;
            m_databaseRecords = databaseRecords;
            m_kernelUpdateLog = kernelUpdateLog;
        }

        public override void WriteDatabaseHeader()
        {
            VolumeManagerDatabaseHeader.WriteToDisk(m_disk, m_databaseHeader);
        }

        public override void WriteDatabaseRecordFragment(DatabaseRecordFragment fragment)
        {
            WriteDatabaseRecordFragment(m_disk, fragment, (int)m_databaseHeader.BlockSize);
        }

        public override void SetKernelUpdateLogLastEntry(ulong committedTransactionID, ulong pendingTransactionID)
        {
            m_kernelUpdateLog.SetLastEntry(m_disk, committedTransactionID, pendingTransactionID);
        }
    }
}
