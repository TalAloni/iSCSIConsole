/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// Implements the low level NTFS volume logic.
    /// This class can be used by higher level implementation that may include
    /// functions such as file copy, caching, symbolic links and etc.
    /// </summary>
    /// <remarks>
    /// If a caller wishes to access this class from multiple threads, the underlying volume must be thread safe.
    /// </remarks>
    public partial class NTFSVolume : IExtendableFileSystem
    {
        private Volume m_volume;
        private NTFSBootRecord m_bootRecord; // Partition's boot record
        private MasterFileTable m_mft;
        private LogFile m_logFile;
        private NTFSLogClient m_logClient;
        private VolumeBitmap m_bitmap;
        private ReaderWriterLock m_mftLock = new ReaderWriterLock(); // We use this lock to synchronize MFT and directory indexes operations
        private object m_bitmapLock = new object();
        private VolumeInformationRecord m_volumeInformation;
        private readonly bool m_generateDosNames = false;

        public NTFSVolume(Volume volume) : this(volume, false)
        { 
        }

        public NTFSVolume(Volume volume, bool useMftMirror)
        {
            m_volume = volume;

            byte[] bootSector = m_volume.ReadSector(0);
            m_bootRecord = NTFSBootRecord.ReadRecord(bootSector);
            if (m_bootRecord == null)
            {
                throw new InvalidDataException("The volume does not contain a valid NTFS boot record");
            }
            m_mft = new MasterFileTable(this, useMftMirror);
            m_volumeInformation = GetVolumeInformationRecord();
            if (m_volumeInformation.IsDirty)
            {
                throw new NotSupportedException("The volume is marked dirty, please run CHKDSK to repair the volume");
            }
            // Note: We could support NTFS v1.2 with minimal effort, but there isn't really any point.
            if (!(m_volumeInformation.MajorVersion == 3 && m_volumeInformation.MinorVersion == 0) &&
                !(m_volumeInformation.MajorVersion == 3 && m_volumeInformation.MinorVersion == 1))
            {
                throw new NotSupportedException(String.Format("NTFS v{0}.{1} is not supported", m_volumeInformation.MajorVersion, m_volumeInformation.MinorVersion));
            }
            m_logFile = new LogFile(this);
            m_logClient = new NTFSLogClient(m_logFile);
            m_bitmap = new VolumeBitmap(this);
        }

        public virtual FileRecord GetFileRecord(string path)
        {
            if (path != String.Empty && !path.StartsWith(@"\"))
            {
                throw new ArgumentException("Invalid path");
            }

            if (path.EndsWith(@"\"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            if (path == String.Empty)
            {
                return GetFileRecord(MasterFileTable.RootDirSegmentReference);
            }

            string[] components = path.Substring(1).Split('\\');
            MftSegmentReference directoryReference = MasterFileTable.RootDirSegmentReference;
            for (int index = 0; index < components.Length; index++)
            {
                FileRecord directoryRecord = GetFileRecord(directoryReference);
                if (index < components.Length - 1)
                {
                    if (!directoryRecord.IsDirectory)
                    {
                        return null;
                    }
                    IndexData indexData = new IndexData(this, directoryRecord, AttributeType.FileName);
                    directoryReference = indexData.FindFileNameRecordSegmentReference(components[index]);
                    if (directoryReference == null)
                    {
                        return null;
                    }
                }
                else // Last component
                {
                    IndexData indexData = new IndexData(this, directoryRecord, AttributeType.FileName);
                    MftSegmentReference fileReference = indexData.FindFileNameRecordSegmentReference(components[index]);
                    if (fileReference == null)
                    {
                        return null;
                    }
                    FileRecord fileRecord = GetFileRecord(fileReference);
                    if (fileRecord != null && !fileRecord.IsMetaFile)
                    {
                        return fileRecord;
                    }
                }
            }

            return null;
        }

        protected internal virtual FileRecord GetFileRecord(MftSegmentReference fileReference)
        {
            m_mftLock.AcquireReaderLock(Timeout.Infinite);
            FileRecord fileRecord = m_mft.GetFileRecord(fileReference);
            m_mftLock.ReleaseReaderLock();
            return fileRecord;
        }

        public virtual FileRecord CreateFile(MftSegmentReference parentDirectory, string fileName, bool isDirectory)
        {
            // Worst case scenrario: the MFT might be full and the parent directory index requires multiple splits
            if (NumberOfFreeClusters < 24)
            {
                throw new DiskFullException();
            }
            FileRecord parentDirectoryRecord = GetFileRecord(parentDirectory);
            m_mftLock.AcquireWriterLock(Timeout.Infinite);
            IndexData parentDirectoryIndex = new IndexData(this, parentDirectoryRecord, AttributeType.FileName);

            if (parentDirectoryIndex.ContainsFileName(fileName))
            {
                m_mftLock.ReleaseWriterLock();
                throw new AlreadyExistsException();
            }

            List<FileNameRecord> fileNameRecords = IndexHelper.GenerateFileNameRecords(parentDirectory, fileName, isDirectory, m_generateDosNames, parentDirectoryIndex);
            uint transactionID = m_logClient.AllocateTransactionID();
            FileRecord fileRecord = m_mft.CreateFile(fileNameRecords, transactionID);

            // Update parent directory index
            foreach (FileNameRecord fileNameRecord in fileNameRecords)
            {
                parentDirectoryIndex.AddEntry(fileRecord.BaseSegmentReference, fileNameRecord.GetBytes());
            }
            m_logClient.WriteForgetTransactionRecord(transactionID);
            m_logClient.WriteRestartRecord(this.MajorVersion, true);
            m_mftLock.ReleaseWriterLock();

            return fileRecord;
        }

        protected internal virtual void UpdateFileRecord(FileRecord fileRecord)
        {
            UpdateFileRecord(fileRecord, null);
        }

        protected internal virtual void UpdateFileRecord(FileRecord fileRecord, uint? transactionID)
        {
            m_mftLock.AcquireWriterLock(Timeout.Infinite);
            bool allocateTransactionID = !transactionID.HasValue;
            if (allocateTransactionID)
            {
                transactionID = m_logClient.AllocateTransactionID();
            }
            m_mft.UpdateFileRecord(fileRecord, transactionID.Value);
            if (allocateTransactionID)
            {
                m_logClient.WriteForgetTransactionRecord(transactionID.Value);
                if (m_logClient.TransactionCount == 0)
                {
                    m_logClient.WriteRestartRecord(this.MajorVersion, true);
                }
            }
            m_mftLock.ReleaseWriterLock();
        }

        public virtual void MoveFile(FileRecord fileRecord, MftSegmentReference newParentDirectory, string newFileName)
        {
            // Worst case scenrario: the new parent directory index requires multiple splits
            if (NumberOfFreeClusters < 4)
            {
                throw new DiskFullException();
            }

            FileRecord oldParentDirectoryRecord = GetFileRecord(fileRecord.ParentDirectoryReference);
            m_mftLock.AcquireWriterLock(Timeout.Infinite);
            IndexData oldParentDirectoryIndex = new IndexData(this, oldParentDirectoryRecord, AttributeType.FileName);
            IndexData newParentDirectoryIndex;
            if (fileRecord.ParentDirectoryReference == newParentDirectory)
            {
                newParentDirectoryIndex = oldParentDirectoryIndex;
            }
            else
            {
                FileRecord newParentDirectoryRecord = GetFileRecord(newParentDirectory);
                newParentDirectoryIndex = new IndexData(this, newParentDirectoryRecord, AttributeType.FileName);
                if (newParentDirectoryIndex.ContainsFileName(newFileName))
                {
                    m_mftLock.ReleaseWriterLock();
                    throw new AlreadyExistsException();
                }
            }

            List<FileNameRecord> fileNameRecords = fileRecord.FileNameRecords;
            uint transactionID = m_logClient.AllocateTransactionID();
            foreach (FileNameRecord fileNameRecord in fileNameRecords)
            {
                oldParentDirectoryIndex.RemoveEntry(fileNameRecord.GetBytes());
            }

            DateTime creationTime = fileRecord.FileNameRecord.CreationTime;
            DateTime modificationTime = fileRecord.FileNameRecord.ModificationTime;
            DateTime mftModificationTime = fileRecord.FileNameRecord.MftModificationTime;
            DateTime lastAccessTime = fileRecord.FileNameRecord.LastAccessTime;
            ulong allocatedLength = fileRecord.FileNameRecord.AllocatedLength;
            ulong fileSize = fileRecord.FileNameRecord.FileSize;
            FileAttributes fileAttributes = fileRecord.FileNameRecord.FileAttributes;
            ushort packedEASize = fileRecord.FileNameRecord.PackedEASize;
            fileNameRecords = IndexHelper.GenerateFileNameRecords(newParentDirectory, newFileName, fileRecord.IsDirectory, m_generateDosNames, newParentDirectoryIndex, creationTime, modificationTime, mftModificationTime, lastAccessTime, allocatedLength, fileSize, fileAttributes, packedEASize);
            fileRecord.RemoveAttributeRecords(AttributeType.FileName, String.Empty);
            foreach (FileNameRecord fileNameRecord in fileNameRecords)
            {
                FileNameAttributeRecord fileNameAttribute = (FileNameAttributeRecord)fileRecord.CreateAttributeRecord(AttributeType.FileName, String.Empty);
                fileNameAttribute.IsIndexed = true;
                fileNameAttribute.Record = fileNameRecord;
            }
            UpdateFileRecord(fileRecord, transactionID);

            foreach (FileNameRecord fileNameRecord in fileNameRecords)
            {
                newParentDirectoryIndex.AddEntry(fileRecord.BaseSegmentReference, fileNameRecord.GetBytes());
            }
            m_logClient.WriteForgetTransactionRecord(transactionID);
            m_logClient.WriteRestartRecord(this.MajorVersion, true);
            m_mftLock.ReleaseWriterLock();
        }

        public virtual void DeleteFile(FileRecord fileRecord)
        {
            MftSegmentReference parentDirectory = fileRecord.ParentDirectoryReference;
            FileRecord parentDirectoryRecord = GetFileRecord(parentDirectory);
            m_mftLock.AcquireWriterLock(Timeout.Infinite);
            IndexData parentDirectoryIndex = new IndexData(this, parentDirectoryRecord, AttributeType.FileName);

            uint transactionID = m_logClient.AllocateTransactionID();
            // Update parent directory index
            List<FileNameRecord> fileNameRecords = fileRecord.FileNameRecords;
            foreach(FileNameRecord fileNameRecord in fileNameRecords)
            {
                parentDirectoryIndex.RemoveEntry(fileNameRecord.GetBytes());
            }

            // Deallocate all data clusters
            foreach (AttributeRecord atttributeRecord in fileRecord.Attributes)
            {
                if (atttributeRecord is NonResidentAttributeRecord)
                {
                    NonResidentAttributeData attributeData = new NonResidentAttributeData(this, fileRecord, (NonResidentAttributeRecord)atttributeRecord);
                    attributeData.Truncate(0);
                }
            }

            m_mft.DeleteFile(fileRecord, transactionID);
            m_logClient.WriteForgetTransactionRecord(transactionID);
            m_logClient.WriteRestartRecord(this.MajorVersion, true);
            m_mftLock.ReleaseWriterLock();
        }

        public virtual KeyValuePairList<MftSegmentReference, FileNameRecord> GetFileNameRecordsInDirectory(MftSegmentReference directoryReference)
        {
            FileRecord directoryRecord = GetFileRecord(directoryReference);
            KeyValuePairList<MftSegmentReference, FileNameRecord> result = null;
            if (directoryRecord != null && directoryRecord.IsDirectory)
            {
                m_mftLock.AcquireReaderLock(Timeout.Infinite);
                IndexData indexData = new IndexData(this, directoryRecord, AttributeType.FileName);
                result = indexData.GetAllFileNameRecords();
                m_mftLock.ReleaseReaderLock();

                for (int index = 0; index < result.Count; index++)
                {
                    bool isMetaFile = (result[index].Key.SegmentNumber < MasterFileTable.FirstUserSegmentNumber);
                    if (result[index].Value.Flags == FileNameFlags.DOS || isMetaFile)
                    {
                        // The same FileRecord can have multiple FileNameRecord entries, each with its own namespace
                        result.RemoveAt(index);
                        index--;
                    }
                }
            }
            return result;
        }

        // logical cluster
        protected internal byte[] ReadCluster(long clusterLCN)
        {
            return ReadClusters(clusterLCN, 1);
        }

        protected internal byte[] ReadClusters(long clusterLCN, int count)
        {
            long firstSectorIndex = clusterLCN * m_bootRecord.SectorsPerCluster;
            int sectorsToRead = m_bootRecord.SectorsPerCluster * count;

            byte[] result = m_volume.ReadSectors(firstSectorIndex, sectorsToRead);

            return result;
        }

        protected internal void WriteClusters(long clusterLCN, byte[] data)
        {
            long firstSectorIndex = clusterLCN * m_bootRecord.SectorsPerCluster;
            m_volume.WriteSectors(firstSectorIndex, data);
        }

        protected internal byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            return m_volume.ReadSectors(sectorIndex, sectorCount);
        }

        protected internal void WriteSectors(long sectorIndex, byte[] data)
        {
            m_volume.WriteSectors(sectorIndex, data);
        }

        internal VolumeNameRecord GetVolumeNameRecord()
        {
            FileRecord volumeRecord = m_mft.GetVolumeRecord();
            if (volumeRecord != null)
            {
                return (VolumeNameRecord)volumeRecord.GetAttributeRecord(AttributeType.VolumeName, String.Empty);
            }
            else
            {
                throw new InvalidDataException("Invalid NTFS volume record");
            }
        }

        internal VolumeInformationRecord GetVolumeInformationRecord()
        {
            FileRecord volumeRecord = m_mft.GetVolumeRecord();
            if (volumeRecord != null)
            {
                return (VolumeInformationRecord)volumeRecord.GetAttributeRecord(AttributeType.VolumeInformation, String.Empty);
            }
            else
            {
                throw new InvalidDataException("Invalid NTFS volume record");
            }
        }

        internal AttributeDefinition GetAttributeDefinition()
        {
            return new AttributeDefinition(this);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            if (m_bootRecord != null)
            {
                builder.AppendLine("Bytes Per Sector: " + m_bootRecord.BytesPerSector);
                builder.AppendLine("Bytes Per Cluster: " + m_bootRecord.BytesPerCluster);
                builder.AppendLine("Bytes Per File Record Segment: " + m_bootRecord.BytesPerFileRecordSegment);
                builder.AppendLine("First MFT Cluster (LCN): " + m_bootRecord.MftStartLCN);
                builder.AppendLine("First MFT Mirror Cluster (LCN): " + m_bootRecord.MftMirrorStartLCN);
                builder.AppendLine("Volume size (bytes): " + this.Size);
                builder.AppendLine();

                VolumeInformationRecord volumeInformationRecord = GetVolumeInformationRecord();
                if (volumeInformationRecord != null)
                {
                    builder.AppendFormat("NTFS Version: {0}.{1}\n", volumeInformationRecord.MajorVersion, volumeInformationRecord.MinorVersion);
                    builder.AppendLine();
                }

                FileRecord mftRecord = m_mft.GetMftRecord();
                if (mftRecord != null)
                {
                    builder.AppendLine("Number of $MFT Data Runs: " + mftRecord.NonResidentDataRecord.DataRunSequence.Count);
                    builder.AppendLine("$MFT Size in Clusters: " + mftRecord.NonResidentDataRecord.DataRunSequence.DataClusterCount);

                    builder.Append(mftRecord.NonResidentDataRecord.DataRunSequence.ToString());

                    builder.AppendLine("Number of $MFT Attributes: " + mftRecord.Attributes.Count);
                    builder.AppendLine("Length of $MFT Attributes: " + mftRecord.AttributesLengthOnDisk);
                    builder.AppendLine();

                    FileRecord volumeBitmapRecord = m_mft.GetVolumeBitmapRecord();
                    if (volumeBitmapRecord != null)
                    {
                        builder.AppendLine("Volume Bitmap Start LCN: " + volumeBitmapRecord.NonResidentDataRecord.DataRunSequence.FirstDataRunLCN);
                        builder.AppendLine("Volume Bitmap Size in Clusters: " + volumeBitmapRecord.NonResidentDataRecord.DataRunSequence.DataClusterCount);

                        builder.AppendLine("Number of Volume Bitmap Attributes: " + volumeBitmapRecord.Attributes.Count);
                        builder.AppendLine("Length of Volume Bitmap Attributes: " + volumeBitmapRecord.AttributesLengthOnDisk);
                    }
                }

                byte[] bootRecord = ReadSectors(0, 1);
                long backupBootSectorIndex = (long)m_bootRecord.TotalSectors;
                byte[] backupBootRecord = ReadSectors(backupBootSectorIndex, 1);
                builder.AppendLine();
                builder.AppendLine("Valid backup boot sector: " + ByteUtils.AreByteArraysEqual(bootRecord, backupBootRecord));
                builder.AppendLine("Free space: " + this.FreeSpace);
            }
            return builder.ToString();
        }

        internal KeyValuePairList<long, long> AllocateClusters(long numberOfClusters)
        {
            lock (m_bitmapLock)
            {
                return m_bitmap.AllocateClusters(numberOfClusters);
            }
        }

        internal KeyValuePairList<long, long> AllocateClusters(long desiredStartLCN, long numberOfClusters)
        {
            lock (m_bitmapLock)
            {
                return m_bitmap.AllocateClusters(desiredStartLCN, numberOfClusters);
            }
        }

        internal void DeallocateClusters(long startLCN, long numberOfClusters)
        {
            lock (m_bitmapLock)
            {
                m_bitmap.DeallocateClusters(startLCN, numberOfClusters);
            }
        }

        public ushort MajorVersion
        {
            get
            {
                return m_volumeInformation.MajorVersion;
            }
        }

        public ushort MinorVersion
        {
            get
            {
                return m_volumeInformation.MinorVersion;
            }
        }

        public long Size
        {
            get
            {
                return (long)(m_bootRecord.TotalSectors * m_bootRecord.BytesPerSector);
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return m_volume.IsReadOnly;
            }
        }

        public long NumberOfFreeClusters
        {
            get
            {
                return m_bitmap.NumberOfFreeClusters;
            }
        }

        public long FreeSpace
        {
            get
            {
                return m_bitmap.NumberOfFreeClusters * this.BytesPerCluster;
            }
        }

        internal NTFSBootRecord BootRecord
        {
            get
            {
                return m_bootRecord;
            }
        }

        internal int BytesPerCluster
        {
            get
            {
                return m_bootRecord.BytesPerCluster;
            }
        }

        internal int BytesPerSector
        {
            get
            {
                return m_bootRecord.BytesPerSector;
            }
        }

        internal int BytesPerFileRecordSegment
        {
            get
            {
                return m_bootRecord.BytesPerFileRecordSegment;
            }
        }

        internal int BytesPerIndexRecord
        {
            get
            {
                return m_bootRecord.BytesPerIndexRecord;
            }
        }

        internal int SectorsPerCluster
        {
            get
            {
                return m_bootRecord.SectorsPerCluster;
            }
        }

        internal int SectorsPerFileRecordSegment
        {
            get
            {
                return m_bootRecord.SectorsPerFileRecordSegment;
            }
        }

        internal int AttributeRecordLengthToMakeNonResident
        {
            get
            {
                return m_mft.AttributeRecordLengthToMakeNonResident;
            }
        }

        internal NTFSLogClient LogClient
        {
            get
            {
                return m_logClient;
            }
        }

        public static MftSegmentReference RootDirSegmentReference
        {
            get
            {
                return MasterFileTable.RootDirSegmentReference;
            }
        }
    }
}
