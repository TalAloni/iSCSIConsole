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
using DiskAccessLibrary;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// Implements the low level NTFS volume logic.
    /// This class can be used by higher level implementation that may include
    /// functions such as file copy, caching, symbolic links and etc.
    /// </summary>
    public partial class NTFSVolume : IExtendableFileSystem
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

            string[] components = path.Substring(1).Split('\\');
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
                    return FindRecord(records, components[index]);
                }
            }

            return null;
        }

        private FileRecord FindDirectoryRecord(KeyValuePairList<MftSegmentReference, FileNameRecord> records, string directoryName)
        {
            FileRecord directoryRecord = FindRecord(records, directoryName);
            if (directoryRecord.IsDirectory)
            {
                return directoryRecord;
            }
            return null;
        }

        private FileRecord FindRecord(KeyValuePairList<MftSegmentReference, FileNameRecord> records, string name)
        {
            KeyValuePair<MftSegmentReference, FileNameRecord>? nameRecord = FindFileNameRecord(records, name);
            if (nameRecord != null)
            {
                FileRecord record = m_mft.GetFileRecord(nameRecord.Value.Key);
                if (record.IsInUse && !record.IsMetaFile)
                {
                    return record;
                }
            }
            return null;
        }

        private KeyValuePairList<MftSegmentReference, FileNameRecord> GetFileNameRecordsInDirectory(long directoryBaseSegmentNumber)
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

        public List<FileRecord> GetFileRecordsInDirectory(long directoryBaseSegmentNumber)
        {
            return GetFileRecordsInDirectory(directoryBaseSegmentNumber, false);
        }

        private List<FileRecord> GetFileRecordsInDirectory(long directoryBaseSegmentNumber, bool includeMetaFiles)
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

        /// <summary>
        /// This method is slower and should only be used for recovery purposes.
        /// </summary>
        public List<FileRecord> GetFileRecordsInDirectoryFromMft(long directoryBaseSegmentNumber)
        {
            return GetFileRecordsInDirectoryFromMft(directoryBaseSegmentNumber, false);
        }

        /// <summary>
        /// This method is slower and should only be used for recovery purposes.
        /// </summary>
        private List<FileRecord> GetFileRecordsInDirectoryFromMft(long directoryBaseSegmentNumber, bool includeMetaFiles)
        {
            long maxNumOfRecords = m_mft.GetMaximumNumberOfSegments();
            List<FileRecord> result = new List<FileRecord>();

            for (long index = 0; index < maxNumOfRecords; index++)
            {
                FileRecord record;
                try
                {
                    record = m_mft.GetFileRecord(index);
                }
                catch (InvalidDataException)
                {
                    continue;
                }
                if (record != null)
                {
                    if (record.ParentDirMftSegmentNumber == directoryBaseSegmentNumber)
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

        public long Size
        {
            get
            {
                return (long)(m_bootRecord.TotalSectors * m_bootRecord.BytesPerSector);
            }
        }

        public long FreeSpace
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

        private static KeyValuePair<MftSegmentReference, FileNameRecord>? FindFileNameRecord(KeyValuePairList<MftSegmentReference, FileNameRecord> records, string name)
        {
            foreach (KeyValuePair<MftSegmentReference, FileNameRecord> record in records)
            {
                if (String.Equals(record.Value.FileName, name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return record;
                }
            }
            return null;
        }
    }
}
