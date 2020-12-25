/* Copyright (C) 2014-2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        private bool m_isReadOnly;
        private NTFSBootRecord m_bootRecord; // Partition's boot record
        private MasterFileTable m_mft;
        private LogFile m_logFile;
        private NTFSLogClient m_logClient;
        private VolumeBitmap m_bitmap;
        private object m_mftLock = new object(); // We use this lock to synchronize MFT and directory indexes operations (and their associated logging operations)
        private object m_bitmapLock = new object();
        private VolumeInformationRecord m_volumeInformation;
        private readonly bool GenerateDosNames = false;
        private readonly int NumberOfClustersRequiredToExtendIndex;

        public NTFSVolume(Volume volume) : this(volume, false)
        { 
        }

        public NTFSVolume(Volume volume, bool isReadOnly) : this(volume, isReadOnly, false)
        {
        }

        public NTFSVolume(Volume volume, bool isReadOnly, bool useMftMirror)
        {
            m_volume = volume;
            m_isReadOnly = volume.IsReadOnly || isReadOnly;

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
            NumberOfClustersRequiredToExtendIndex = (int)Math.Ceiling((double)(IndexData.ExtendGranularity * m_bootRecord.BytesPerIndexRecord) / m_bootRecord.BytesPerCluster);
        }

        public virtual FileRecord GetFileRecord(string path)
        {
            if (path != String.Empty && !path.StartsWith(@"\"))
            {
                throw new InvalidPathException(String.Format("The path '{0}' is invalid", path));
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
            lock (m_mftLock)
            {
                for (int index = 0; index < components.Length; index++)
                {
                    FileRecord directoryRecord = GetFileRecord(directoryReference);
                    if (index < components.Length - 1)
                    {
                        if (!directoryRecord.IsDirectory)
                        {
                            throw new InvalidPathException(String.Format("The path '{0}' is invalid", path));
                        }
                        IndexData indexData = new IndexData(this, directoryRecord, AttributeType.FileName);
                        directoryReference = indexData.FindFileNameRecordSegmentReference(components[index]);
                        if (directoryReference == null)
                        {
                            throw new DirectoryNotFoundException(String.Format("Could not find part of the path '{0}'", path));
                        }
                    }
                    else // Last component
                    {
                        IndexData indexData = new IndexData(this, directoryRecord, AttributeType.FileName);
                        MftSegmentReference fileReference = indexData.FindFileNameRecordSegmentReference(components[index]);
                        if (fileReference == null)
                        {
                            throw new FileNotFoundException(String.Format("Could not find file '{0}'", path));
                        }
                        FileRecord fileRecord = GetFileRecord(fileReference);
                        if (!fileRecord.IsMetaFile)
                        {
                            return fileRecord;
                        }
                    }
                }
            }
            // We should never get here
            throw new InvalidPathException();
        }

        protected internal virtual FileRecord GetFileRecord(MftSegmentReference fileReference)
        {
            lock (m_mftLock)
            {
                return m_mft.GetFileRecord(fileReference);
            }
        }

        public virtual FileRecord CreateFile(MftSegmentReference parentDirectory, string fileName, bool isDirectory)
        {
            if (fileName.Length > FileNameRecord.MaxFileNameLength)
            {
                throw new InvalidNameException();
            }

            // Worst case scenrario: the MFT might be full and the parent directory index requires multiple splits.
            // We assume IndexData.ExtendGranularity is bigger than or equal to the number of splits.
            if (NumberOfFreeClusters < m_mft.NumberOfClustersRequiredToExtend + NumberOfClustersRequiredToExtendIndex)
            {
                throw new DiskFullException();
            }

            lock (m_mftLock)
            {
                FileRecord parentDirectoryRecord = GetFileRecord(parentDirectory);
                IndexData parentDirectoryIndex = new IndexData(this, parentDirectoryRecord, AttributeType.FileName);

                if (parentDirectoryIndex.ContainsFileName(fileName))
                {
                    throw new AlreadyExistsException();
                }

                List<FileNameRecord> fileNameRecords = IndexHelper.GenerateFileNameRecords(parentDirectory, fileName, isDirectory, GenerateDosNames, parentDirectoryIndex);
                uint transactionID = m_logClient.AllocateTransactionID();
                FileRecord fileRecord = m_mft.CreateFile(fileNameRecords, transactionID);

                // Update parent directory index
                foreach (FileNameRecord fileNameRecord in fileNameRecords)
                {
                    parentDirectoryIndex.AddEntry(fileRecord.BaseSegmentReference, fileNameRecord.GetBytes());
                }
                m_logClient.WriteForgetTransactionRecord(transactionID);
                m_logClient.WriteRestartRecord(true);
                return fileRecord;
            }
        }

        protected internal void UpdateFileRecord(FileRecord fileRecord)
        {
            UpdateFileRecord(fileRecord, null);
        }

        protected internal void UpdateFileRecord(FileRecord fileRecord, uint? transactionID)
        {
            lock (m_mftLock)
            {
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
                        m_logClient.WriteRestartRecord(true);
                    }
                }
            }
        }

        public virtual void MoveFile(FileRecord fileRecord, MftSegmentReference newParentDirectory, string newFileName)
        {
            // Worst case scenrario: the new parent directory index requires multiple splits.
            // We assume IndexData.ExtendGranularity is bigger than or equal to the number of splits.
            if (NumberOfFreeClusters < NumberOfClustersRequiredToExtendIndex)
            {
                throw new DiskFullException();
            }

            lock (m_mftLock)
            {
                FileRecord oldParentDirectoryRecord = GetFileRecord(fileRecord.ParentDirectoryReference);
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
                }

                if (newParentDirectoryIndex.ContainsFileName(newFileName))
                {
                    throw new AlreadyExistsException();
                }

                List<FileNameRecord> fileNameRecords = fileRecord.FileNameRecords;
                uint transactionID = m_logClient.AllocateTransactionID();
                foreach (FileNameRecord fileNameRecord in fileNameRecords)
                {
                    oldParentDirectoryIndex.RemoveEntry(fileNameRecord.GetBytes());
                }

                // Windows will not update the dates and FileAttributes in $File_Name as often as their counterparts in $STANDARD_INFORMATION.
                DateTime creationTime = fileRecord.StandardInformation.CreationTime;
                DateTime modificationTime = fileRecord.StandardInformation.ModificationTime;
                DateTime mftModificationTime = fileRecord.StandardInformation.MftModificationTime;
                DateTime lastAccessTime = fileRecord.StandardInformation.LastAccessTime;
                ulong allocatedLength = fileRecord.FileNameRecord.AllocatedLength;
                FileAttributes fileAttributes = fileRecord.StandardInformation.FileAttributes;
                ushort packedEASize = fileRecord.FileNameRecord.PackedEASize;
                // Windows NTFS v5.1 driver does not usually update the value of the FileSize field belonging to the FileNameRecords that are stored in the FileRecord.
                // The driver does update the value during a rename, which is inconsistent file creation and is likely to be incidental rather than intentional.
                // We will set the value to 0 to be consistent with file creation.
                fileNameRecords = IndexHelper.GenerateFileNameRecords(newParentDirectory, newFileName, fileRecord.IsDirectory, GenerateDosNames, newParentDirectoryIndex, creationTime, modificationTime, mftModificationTime, lastAccessTime, allocatedLength, 0, fileAttributes, packedEASize);
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
                    if (!fileRecord.IsDirectory)
                    {
                        fileNameRecord.FileSize = fileRecord.DataRecord.DataLength;
                    }
                    newParentDirectoryIndex.AddEntry(fileRecord.BaseSegmentReference, fileNameRecord.GetBytes());
                }
                m_logClient.WriteForgetTransactionRecord(transactionID);
                m_logClient.WriteRestartRecord(true);
            }
        }

        public virtual void DeleteFile(FileRecord fileRecord)
        {
            lock (m_mftLock)
            {
                MftSegmentReference parentDirectory = fileRecord.ParentDirectoryReference;
                FileRecord parentDirectoryRecord = GetFileRecord(parentDirectory);
                IndexData parentDirectoryIndex = new IndexData(this, parentDirectoryRecord, AttributeType.FileName);

                if (fileRecord.IsDirectory)
                {
                    IndexData directoryIndex = new IndexData(this, fileRecord, AttributeType.FileName);
                    if (!directoryIndex.IsEmpty)
                    {
                        throw new DirectoryNotEmptyException();
                    }
                }

                uint transactionID = m_logClient.AllocateTransactionID();
                // Update parent directory index
                List<FileNameRecord> fileNameRecords = fileRecord.FileNameRecords;
                foreach (FileNameRecord fileNameRecord in fileNameRecords)
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
                m_logClient.WriteRestartRecord(true);
            }
        }

        public virtual KeyValuePairList<MftSegmentReference, FileNameRecord> GetFileNameRecordsInDirectory(MftSegmentReference directoryReference)
        {
            KeyValuePairList<MftSegmentReference, FileNameRecord> result;
            lock (m_mftLock)
            {
                FileRecord directoryRecord = GetFileRecord(directoryReference);
                if (!directoryRecord.IsDirectory)
                {
                    throw new ArgumentException("directoryReference belongs to a file record which is not a directory");
                }
                IndexData indexData = new IndexData(this, directoryRecord, AttributeType.FileName);
                result = indexData.GetAllFileNameRecords();
            }

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
            return result;
        }

        internal void UpdateDirectoryIndex(MftSegmentReference parentDirectory, List<FileNameRecord> fileNameRecords)
        {
            lock (m_mftLock)
            {
                FileRecord parentDirectoryRecord = GetFileRecord(parentDirectory);
                IndexData parentDirectoryIndex = new IndexData(this, parentDirectoryRecord, AttributeType.FileName);
                foreach (FileNameRecord fileNameRecord in fileNameRecords)
                {
                    parentDirectoryIndex.UpdateFileNameRecord(fileNameRecord);
                }
            }
        }

        protected internal byte[] ReadCluster(long clusterLCN, ContentType contentType)
        {
            return ReadClusters(clusterLCN, 1, contentType);
        }

        protected internal virtual byte[] ReadClusters(long clusterLCN, int count, ContentType contentType)
        {
            long firstSectorIndex = clusterLCN * m_bootRecord.SectorsPerCluster;
            int sectorsToRead = m_bootRecord.SectorsPerCluster * count;

            return m_volume.ReadSectors(firstSectorIndex, sectorsToRead);
        }

        protected internal virtual void WriteClusters(long clusterLCN, byte[] data, ContentType contentType)
        {
            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a filesystem mounted for readonly access");
            }

            long firstSectorIndex = clusterLCN * m_bootRecord.SectorsPerCluster;
            m_volume.WriteSectors(firstSectorIndex, data);
        }

        protected internal virtual byte[] ReadSectors(long sectorIndex, int sectorCount, ContentType contentType)
        {
            return m_volume.ReadSectors(sectorIndex, sectorCount);
        }

        protected internal virtual void WriteSectors(long sectorIndex, byte[] data, ContentType contentType)
        {
            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a filesystem mounted for readonly access");
            }

            m_volume.WriteSectors(sectorIndex, data);
        }

        internal VolumeNameRecord GetVolumeNameRecord()
        {
            FileRecord volumeRecord = m_mft.GetVolumeRecord();
            return (VolumeNameRecord)volumeRecord.GetAttributeRecord(AttributeType.VolumeName, String.Empty);
        }

        internal VolumeInformationRecord GetVolumeInformationRecord()
        {
            FileRecord volumeRecord = m_mft.GetVolumeRecord();
            return (VolumeInformationRecord)volumeRecord.GetAttributeRecord(AttributeType.VolumeInformation, String.Empty);
        }

        internal AttributeDefinition GetAttributeDefinition()
        {
            return new AttributeDefinition(this);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Bytes Per Sector: " + m_bootRecord.BytesPerSector);
            builder.AppendLine("Bytes Per Cluster: " + m_bootRecord.BytesPerCluster);
            builder.AppendLine("Bytes Per File Record Segment: " + m_bootRecord.BytesPerFileRecordSegment);
            builder.AppendLine("First MFT Cluster (LCN): " + m_bootRecord.MftStartLCN);
            builder.AppendLine("First MFT Mirror Cluster (LCN): " + m_bootRecord.MftMirrorStartLCN);
            builder.AppendLine("Volume size (bytes): " + this.Size);
            builder.AppendLine();

            VolumeInformationRecord volumeInformationRecord = GetVolumeInformationRecord();
            builder.AppendFormat("NTFS Version: {0}.{1}\n", volumeInformationRecord.MajorVersion, volumeInformationRecord.MinorVersion);
            builder.AppendLine();

            FileRecord mftRecord = m_mft.GetMftRecord();
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

            byte[] bootRecord = ReadSectors(0, 1, ContentType.FileData);
            long backupBootSectorIndex = (long)m_bootRecord.TotalSectors;
            byte[] backupBootRecord = ReadSectors(backupBootSectorIndex, 1, ContentType.FileData);
            builder.AppendLine();
            builder.AppendLine("Valid backup boot sector: " + ByteUtils.AreByteArraysEqual(bootRecord, backupBootRecord));
            builder.AppendLine("Free space: " + this.FreeSpace);
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

        public byte MajorVersion
        {
            get
            {
                return m_volumeInformation.MajorVersion;
            }
        }

        public byte MinorVersion
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
                return m_isReadOnly;
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

        protected internal int BytesPerCluster
        {
            get
            {
                return m_bootRecord.BytesPerCluster;
            }
        }

        protected internal int BytesPerSector
        {
            get
            {
                return m_bootRecord.BytesPerSector;
            }
        }

        protected internal int SectorsPerCluster
        {
            get
            {
                return m_bootRecord.SectorsPerCluster;
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

        internal long NumberOfClustersRequiredToExtendMft
        {
            get
            {
                return m_mft.NumberOfClustersRequiredToExtend;
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
