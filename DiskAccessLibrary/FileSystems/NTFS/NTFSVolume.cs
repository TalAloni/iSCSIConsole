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
using DiskAccessLibrary;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public partial class NTFSVolume : FileSystem, IDiskFileSystem
    {
        private Volume m_volume;
        private MasterFileTable m_mft;
        private ClusterUsageBitmap m_bitmap;

        private NTFSBootRecord m_bootRecord; // partition's boot record

        public NTFSVolume(Volume volume) : this(volume, false)
        { 
        }

        public NTFSVolume(Volume volume, bool useMftMirror)
        {
            m_volume = volume;

            byte[] bootSector = m_volume.ReadSector(0);
            m_bootRecord = NTFSBootRecord.ReadRecord(bootSector);
            if (m_bootRecord != null)
            {
                m_mft = new MasterFileTable(this, useMftMirror);
                m_bitmap = new ClusterUsageBitmap(this);
            }
        }

        public FileRecord GetFileRecord(string path)
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
                return m_mft.GetFileRecord(MasterFileTable.RootDirSegmentNumber);
            }

            path = path.Substring(1);

            string[] components = path.Split('\\');
            long directorySegmentNumber = MasterFileTable.RootDirSegmentNumber;
            for (int index = 0; index < components.Length; index++)
            {
                KeyValuePairList<MftSegmentReference, FileNameRecord> records = GetFileNameRecordsInDirectory(directorySegmentNumber);
                if (index < components.Length - 1)
                {
                    FileRecord record = FindDirectoryRecord(records, components[index]);
                    if (record != null)
                    {
                        directorySegmentNumber = record.MftSegmentNumber;
                    }
                    else
                    {
                        return null;
                    }
                }
                else // last component
                {
                    foreach (KeyValuePair<MftSegmentReference, FileNameRecord> record in records)
                    {
                        if (String.Equals(record.Value.FileName, components[index], StringComparison.InvariantCultureIgnoreCase))
                        {
                            FileRecord fileRecord = m_mft.GetFileRecord(record.Key);
                            if (!fileRecord.IsMetaFile)
                            {
                                return fileRecord;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private FileRecord FindDirectoryRecord(KeyValuePairList<MftSegmentReference, FileNameRecord> records, string directoryName)
        {
            foreach (KeyValuePair<MftSegmentReference, FileNameRecord> record in records)
            {
                if (String.Equals(record.Value.FileName, directoryName, StringComparison.InvariantCultureIgnoreCase))
                {
                    FileRecord directoryRecord = m_mft.GetFileRecord(record.Key);
                    if (directoryRecord.IsDirectory && !directoryRecord.IsMetaFile)
                    {
                        return directoryRecord;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        private FileRecord FindDirectoryRecord(List<FileRecord> records, string directoryName)
        {
            foreach (FileRecord record in records)
            {
                if (record.IsDirectory && String.Equals(record.FileName, directoryName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return record;
                }
            }
            return null;
        }

        public override FileSystemEntry GetEntry(string path)
        {
            FileRecord record = GetFileRecord(path);
            if (record != null)
            {
                ulong size = record.IsDirectory ? 0 : record.DataRecord.DataRealSize;
                FileAttributes attributes = record.StandardInformation.FileAttributes;
                bool isHidden = (attributes & FileAttributes.Hidden) > 0;
                bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
                bool isArchived = (attributes & FileAttributes.Archive) > 0;
                return new FileSystemEntry(path, record.FileName, record.IsDirectory, size, record.FileNameRecord.CreationTime, record.FileNameRecord.ModificationTime, record.FileNameRecord.LastAccessTime, isHidden, isReadonly, isArchived);
            }
            else
            {
                return null;
            }
        }

        public override FileSystemEntry CreateFile(string path)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override FileSystemEntry CreateDirectory(string path)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Move(string source, string destination)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Delete(string path)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public KeyValuePairList<MftSegmentReference, FileNameRecord> GetFileNameRecordsInDirectory(long directoryBaseSegmentNumber)
        {
            FileRecord record = m_mft.GetFileRecord(directoryBaseSegmentNumber);
            KeyValuePairList<MftSegmentReference, FileNameRecord> result = null;
            if (record != null && record.IsDirectory)
            {
                IndexRootRecord indexRoot = (IndexRootRecord)record.GetAttributeRecord(AttributeType.IndexRoot, IndexRootRecord.FileNameIndexName);
                IndexAllocationRecord indexAllocation = (IndexAllocationRecord)record.GetAttributeRecord(AttributeType.IndexAllocation, IndexRootRecord.FileNameIndexName);
                
                if (indexRoot.IsLargeIndex)
                {
                    if (indexAllocation != null)
                    {
                        result = indexAllocation.GetAllEntries(this, indexRoot);
                    }
                }
                else
                {
                    result = indexRoot.GetSmallIndexEntries();
                }

                if (result != null)
                {
                    for (int index = 0; index < result.Count; index++)
                    {
                        if (result[index].Value.Namespace == FilenameNamespace.DOS)
                        {
                            // The same FileRecord can have multiple entries, each with it's own namespace
                            result.RemoveAt(index);
                            index--;
                        }
                    }
                }
            }
            return result;
        }

        public List<FileRecord> GetFileRecordsInDirectory(long directoryBaseSegmentNumber, bool includeMetaFiles)
        {
            KeyValuePairList<MftSegmentReference, FileNameRecord> entries = GetFileNameRecordsInDirectory(directoryBaseSegmentNumber);
            List<FileRecord> result = new List<FileRecord>();
            
            if (entries != null)
            {
                List<MftSegmentReference> files = entries.Keys;
                foreach (MftSegmentReference reference in files)
                {
                    FileRecord record = m_mft.GetFileRecord(reference);
                    if (record != null)
                    {
                        if (record.IsInUse && (includeMetaFiles || !record.IsMetaFile))
                        {
                            result.Add(record);
                        }
                    }
                }
            }

            return result;
        }
        
        public override List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            FileRecord directoryRecord = GetFileRecord(path);
            if (directoryRecord != null && directoryRecord.IsDirectory)
            {
                long directoryBaseSegmentNumber = directoryRecord.MftSegmentNumber;
                List<FileRecord> records = GetFileRecordsInDirectory(directoryBaseSegmentNumber, false);
                List<FileSystemEntry> result = new List<FileSystemEntry>();

                if (!path.EndsWith(@"\"))
                {
                    path = path + @"\";
                }

                foreach (FileRecord record in records)
                {
                    string fullPath = path + record.FileName;
                    ulong size = record.IsDirectory ? 0 : record.DataRecord.DataRealSize;
                    FileAttributes attributes = record.StandardInformation.FileAttributes;
                    bool isHidden = (attributes & FileAttributes.Hidden) > 0;
                    bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
                    bool isArchived = (attributes & FileAttributes.Archive) > 0;
                    FileSystemEntry entry = new FileSystemEntry(fullPath, record.FileName, record.IsDirectory, size, record.FileNameRecord.CreationTime, record.FileNameRecord.ModificationTime, record.FileNameRecord.LastAccessTime, isHidden, isReadonly, isArchived);
                    result.Add(entry);
                }
                return result;
            }
            else
            {
                return null;
            }
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (mode == FileMode.Open || mode == FileMode.Truncate)
            {
                FileRecord record = GetFileRecord(path);
                if (record != null && !record.IsDirectory)
                {
                    NTFSFile file = new NTFSFile(this, record.MftSegmentNumber);
                    NTFSFileStream stream = new NTFSFileStream(file);

                    if (mode == FileMode.Truncate)
                    {
                        stream.SetLength(0);
                    }
                    return stream;
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            throw new Exception("The method or operation is not implemented.");
        }

        public override void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived)
        {
            FileRecord record = GetFileRecord(path);
            if (isHidden.HasValue)
            {
                if (isHidden.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Hidden;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Hidden;
                }
            }

            if (isReadonly.HasValue)
            {
                if (isReadonly.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Readonly;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Readonly;
                }
            }

            if (isArchived.HasValue)
            {
                if (isArchived.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Archive;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Archive;
                }
            }

            m_mft.UpdateFileRecord(record);
        }

        public override void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /*
        [Obsolete]
        public object GetParentFolderIdentifier(object folderIdentifier)
        {
            return GetSegmentNumberOfParentRecord((long)folderIdentifier);
        }

        [Obsolete]
        public void CopyFile(object folderIdentifier, string sourceFileName, string destination)
        {
            FileRecord record = GetFileRecordFromDirectory((long)folderIdentifier, sourceFileName);
            CopyFile(record, destination);
        }

        [Obsolete]
        public void ListFilesInDirectorySlow(long directoryBaseSegmentNumber)
        {
            long maxNumOfRecords = m_mft.GetMaximumNumberOfSegments();

            for (long index = 0; index < maxNumOfRecords; index++)
            {
                FileRecord record = m_mft.GetFileRecord(index);
                if (record != null)
                {
                    if (record.ParentDirMftSegmentNumber == directoryBaseSegmentNumber)
                    {
                        if (record.IsInUse && record.MftSegmentNumber > MasterFileTable.LastReservedMftSegmentNumber)
                        {
                            Console.WriteLine(record.FileName);
                        }
                    }
                }
            }
        }
        
        [Obsolete]
        public FileRecord GetFileRecordFromDirectory(long directoryBaseSegmentNumber, string fileName)
        {
            List<FileRecord> records = GetRecordsInDirectory(directoryBaseSegmentNumber, true);
            foreach(FileRecord record in records)
            {
                if (record.FileName.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        [Obsolete]
        public long GetSegmentNumberOfParentRecord(long baseSegmentNumber)
        {
            return m_mft.GetFileRecord(baseSegmentNumber).ParentDirMftSegmentNumber;
        }

        [Obsolete]
        public void PrintAllFiles()
        {
            long maxNumOfRecords = m_mft.GetMaximumNumberOfSegments();

            for (long index = 0; index < maxNumOfRecords; index++)
            {
                FileRecord record = m_mft.GetFileRecord(index);
                if (record != null)
                {
                    Console.WriteLine(record.FileName);
                }
            }
        }
        
        [Obsolete]
        public void CopyFile(FileRecord record, string destination)
        {
            if (record.DataRecord != null)
            {
                FileStream stream = new FileStream(destination, FileMode.Create, FileAccess.Write);

                int transferSizeInClusters = PhysicalDisk.MaximumDirectTransferSizeLBA / SectorsPerCluster;
                for(long index = 0; index < record.DataRecord.DataClusterCount; index += transferSizeInClusters)
                {
                    long clustersLeft = record.DataRecord.DataClusterCount - index;
                    int transferSize = (int)Math.Min(transferSizeInClusters, clustersLeft);
                    byte[] buffer = record.DataRecord.ReadDataClusters(this, index, transferSize);
                    stream.Write(buffer, 0, buffer.Length);
                }
                stream.Close();
            }
        }*/

        // logical cluster
        public byte[] ReadCluster(long clusterLCN)
        {
            return ReadClusters(clusterLCN, 1);
        }

        public byte[] ReadClusters(long clusterLCN, int count)
        {
            long firstSectorIndex = clusterLCN * m_bootRecord.SectorsPerCluster;
            int sectorsToRead = m_bootRecord.SectorsPerCluster * count;

            byte[] result = m_volume.ReadSectors(firstSectorIndex, sectorsToRead);

            return result;
        }

        public void WriteClusters(long clusterLCN, byte[] data)
        {
            long firstSectorIndex = clusterLCN * m_bootRecord.SectorsPerCluster;
            m_volume.WriteSectors(firstSectorIndex, data);
        }

        public byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            return m_volume.ReadSectors(sectorIndex, sectorCount);
        }

        public void WriteSectors(long sectorIndex, byte[] data)
        {
            m_volume.WriteSectors(sectorIndex, data);
        }

        public VolumeInformationRecord GetVolumeInformationRecord()
        {
            FileRecord volumeRecord = m_mft.GetVolumeRecord();
            if (volumeRecord != null)
            {
                VolumeInformationRecord volumeInformationRecord = (VolumeInformationRecord)volumeRecord.GetAttributeRecord(AttributeType.VolumeInformation, String.Empty);
                return volumeInformationRecord;
            }
            else
            {
                throw new InvalidDataException("Invalid NTFS volume record");
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            if (m_bootRecord != null)
            {
                builder.AppendLine("Bytes Per Sector: " + m_bootRecord.BytesPerSector);
                builder.AppendLine("Bytes Per Cluster: " + m_bootRecord.BytesPerCluster);
                builder.AppendLine("File Record Length: " + m_bootRecord.FileRecordSegmentLength);
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
                    builder.AppendLine("Number of $MFT Clusters: " + mftRecord.NonResidentDataRecord.DataRunSequence.DataClusterCount);

                    builder.Append(mftRecord.NonResidentDataRecord.DataRunSequence.ToString());

                    builder.AppendLine("Number of $MFT Attributes: " + mftRecord.Attributes.Count);
                    builder.AppendLine("Length of $MFT Attributes: " + mftRecord.StoredAttributesLength);
                    builder.AppendLine();

                    FileRecord bitmapRecord = m_mft.GetBitmapRecord();
                    if (bitmapRecord != null)
                    {
                        builder.AppendLine("$Bitmap LCN: " + bitmapRecord.NonResidentDataRecord.DataRunSequence.FirstDataRunLCN);
                        builder.AppendLine("$Bitmap Clusters: " + bitmapRecord.NonResidentDataRecord.DataRunSequence.DataClusterCount);

                        builder.AppendLine("Number of $Bitmap Attributes: " + bitmapRecord.Attributes.Count);
                        builder.AppendLine("Length of $Bitmap Attributes: " + bitmapRecord.StoredAttributesLength);
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

        public override string Name
        {
            get
            {
                return "NTFS";
            }
        }

        public long RootFolderIdentifier
        {
            get
            {
                return MasterFileTable.RootDirSegmentNumber;
            }
        }

        public bool IsValid
        {
            get
            {
                return (m_bootRecord != null && m_mft.GetMftRecord() != null);
            }
        }

        public bool IsValidAndSupported
        {
            get
            {
                return (this.IsValid && MajorVersion == 3 && MinorVersion == 1);
            }
        }

        public override long Size
        {
            get
            {
                return (long)(m_bootRecord.TotalSectors * m_bootRecord.BytesPerSector);
            }
        }

        public override long FreeSpace
        {
            get
            {
                return m_bitmap.CountNumberOfFreeClusters() * this.BytesPerCluster;
            }
        }

        public NTFSBootRecord BootRecord
        {
            get
            {
                return m_bootRecord;
            }
        }

        public MasterFileTable MasterFileTable
        {
            get
            {
                return m_mft;
            }
        }

        public int BytesPerCluster
        {
            get
            {
                return m_bootRecord.BytesPerCluster;
            }
        }

        public int BytesPerSector
        {
            get
            {
                return m_bootRecord.BytesPerSector;
            }
        }

        public int SectorsPerCluster
        {
            get
            {
                return m_bootRecord.SectorsPerCluster;
            }
        }

        public int SectorsPerFileRecordSegment
        {
            get
            {
                return m_bootRecord.SectorsPerFileRecordSegment;
            }
        }

        public ushort MajorVersion
        {
            get
            {
                VolumeInformationRecord volumeInformationRecord = GetVolumeInformationRecord();
                return volumeInformationRecord.MajorVersion;
            }
        }

        public ushort MinorVersion
        {
            get
            {
                VolumeInformationRecord volumeInformationRecord = GetVolumeInformationRecord();
                return volumeInformationRecord.MinorVersion;
            }
        }

        public KeyValuePairList<ulong, long> AllocateClusters(ulong desiredStartLCN, long numberOfClusters)
        {
            return m_bitmap.AllocateClusters(desiredStartLCN, numberOfClusters);
        }
    }
}
