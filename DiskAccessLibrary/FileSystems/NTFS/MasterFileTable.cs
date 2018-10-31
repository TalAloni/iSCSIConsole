/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    internal class MasterFileTable
    {
        internal const int FirstReservedSegmentNumber = 16; // 16-23 are reserved for additional FileRecordSegments for the MFT record
        internal const int FirstUserSegmentNumber = 24;
        private const int ExtendGranularity = 16; // The number of records added to the MFT when extending it, MUST be multiple of 8

        internal const long MasterFileTableSegmentNumber = 0;
        internal const long MftMirrorSegmentNumber = 1;
        private const long LogFileSegmentNumber = 2;
        private const long VolumeSegmentNumber = 3;
        private const long AttrDefSegmentNumber = 4;
        private const long RootDirSegmentNumber = 5;
        private const long BitmapSegmentNumber = 6;
        private const long BootSegmentNumber = 7;
        private const long BadClusSegmentNumber = 8;
        private const long SecureSegmentNumber = 9;
        private const long UpCaseSegmentNumber = 10;
        private const long ExtendSegmentNumber = 11;
        // The $Extend Metafile is simply a directory index that contains information on where to locate the last four metafiles ($ObjId, $Quota, $Reparse and $UsnJrnl)
        internal static readonly MftSegmentReference LogSegmentReference = new MftSegmentReference(LogFileSegmentNumber, (ushort)LogFileSegmentNumber);
        private static readonly MftSegmentReference VolumeSegmentReference = new MftSegmentReference(VolumeSegmentNumber, (ushort)VolumeSegmentNumber);
        internal static readonly MftSegmentReference AttrDefSegmentReference = new MftSegmentReference(AttrDefSegmentNumber, (ushort)AttrDefSegmentNumber);
        internal static readonly MftSegmentReference RootDirSegmentReference = new MftSegmentReference(RootDirSegmentNumber, (ushort)RootDirSegmentNumber);
        internal static readonly MftSegmentReference BitmapSegmentReference = new MftSegmentReference(BitmapSegmentNumber, (ushort)BitmapSegmentNumber);
        private static readonly MftSegmentReference BootSegmentReference = new MftSegmentReference(BootSegmentNumber, (ushort)BootSegmentNumber);
        internal readonly int AttributeRecordLengthToMakeNonResident;

        private NTFSVolume m_volume;
        private FileRecord m_mftRecord;
        private NTFSFile m_mftFile;

        public MasterFileTable(NTFSVolume volume) : this(volume, false, false)
        {
        }

        /// <param name="useMftMirror">Strap the MFT using the MFT mirror</param>
        public MasterFileTable(NTFSVolume volume, bool useMftMirror) : this(volume, useMftMirror, false)
        {
        }

        /// <param name="useMftMirror">Strap the MFT using the MFT mirror</param>
        public MasterFileTable(NTFSVolume volume, bool useMftMirror, bool manageMftMirror)
        {
            m_volume = volume;
            m_mftRecord = ReadMftRecord(useMftMirror, manageMftMirror);
            m_mftFile = new NTFSFile(m_volume, m_mftRecord);
            AttributeRecordLengthToMakeNonResident = m_volume.BytesPerFileRecordSegment * 5 / 16; // We immitate the NTFS v5.1 driver
        }

        private FileRecord ReadMftRecord(bool useMftMirror, bool readMftMirror)
        {
            NTFSBootRecord bootRecord = m_volume.BootRecord;
            long mftStartLCN = useMftMirror ? (long)bootRecord.MftMirrorStartLCN : (long)bootRecord.MftStartLCN;
            long mftSegmentNumber = readMftMirror ? MftMirrorSegmentNumber : MasterFileTableSegmentNumber;
            FileRecordSegment mftRecordSegment = ReadFileRecordSegment(mftStartLCN, mftSegmentNumber);
            if (!mftRecordSegment.IsBaseFileRecord)
            {
                throw new InvalidDataException("Invalid MFT record, not a base record");
            }

            AttributeRecord attributeListRecord = mftRecordSegment.GetImmediateAttributeRecord(AttributeType.AttributeList, String.Empty);
            if (attributeListRecord == null)
            {
                return new FileRecord(mftRecordSegment);
            }
            else
            {
                AttributeList attributeList = new AttributeList(m_volume, attributeListRecord);
                List<AttributeListEntry> entries = attributeList.ReadEntries();
                List<MftSegmentReference> references = AttributeList.GetSegmentReferenceList(entries);
                int baseSegmentIndex = MftSegmentReference.IndexOfSegmentNumber(references, MasterFileTableSegmentNumber);

                if (baseSegmentIndex >= 0)
                {
                    references.RemoveAt(baseSegmentIndex);
                }

                List<FileRecordSegment> recordSegments = new List<FileRecordSegment>();
                // we want the base record segment first
                recordSegments.Add(mftRecordSegment);

                foreach (MftSegmentReference reference in references)
                {
                    FileRecordSegment segment = ReadFileRecordSegment(mftStartLCN, reference);
                    if (segment != null)
                    {
                        recordSegments.Add(segment);
                    }
                    else
                    {
                        throw new InvalidDataException("Invalid MFT record, missing segment");
                    }
                }
                return new FileRecord(recordSegments);
            }
        }

        private FileRecordSegment ReadFileRecordSegment(long mftStartLCN, MftSegmentReference reference)
        {
            FileRecordSegment result = ReadFileRecordSegment(mftStartLCN, reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been freed and reallocated, and an obsolete version is being requested
                return null;
            }
            return result;
        }

        /// <summary>
        /// This method is used to read the record segment of the MFT itself.
        /// Only after strapping the MFT we can use GetFileRecordSegment which relies on the MFT file record.
        /// </summary>
        private FileRecordSegment ReadFileRecordSegment(long mftStartLCN, long segmentNumber)
        {
            long sectorIndex = mftStartLCN * m_volume.SectorsPerCluster + segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] segmentBytes = m_volume.ReadSectors(sectorIndex, m_volume.SectorsPerFileRecordSegment);
            MultiSectorHelper.RevertUsaProtection(segmentBytes, 0);
            FileRecordSegment result = new FileRecordSegment(segmentBytes, 0, segmentNumber);
            return result;
        }

        public FileRecordSegment GetFileRecordSegment(MftSegmentReference reference)
        {
            FileRecordSegment result = GetFileRecordSegment(reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been modified, and an older version has been requested
                throw new InvalidDataException("MftSegmentReference SequenceNumber does not match FileRecordSegment");
            }
            return result;
        }

        private FileRecordSegment GetFileRecordSegment(long segmentNumber)
        { 
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            // Note: File record always start at the beginning of a sector
            // Note: Record can span multiple clusters, or alternatively, several records can be stored in the same cluster
            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] segmentBytes = m_mftFile.Data.ReadSectors(firstSectorIndex, m_volume.SectorsPerFileRecordSegment);

            if (FileRecordSegment.ContainsFileRecordSegment(segmentBytes))
            {
                MultiSectorHelper.RevertUsaProtection(segmentBytes, 0);
                FileRecordSegment recordSegment = new FileRecordSegment(segmentBytes, 0, segmentNumber);
                return recordSegment;
            }
            else
            {
                return null;
            }
        }

        public FileRecord GetFileRecord(MftSegmentReference fileReference)
        {
            FileRecord result = GetFileRecord(fileReference.SegmentNumber);
            if (result != null)
            {
                if (result.BaseSequenceNumber != fileReference.SequenceNumber)
                {
                    // The file record segment has been freed and reallocated, and an obsolete version is being requested
                    throw new InvalidDataException("MftSegmentReference SequenceNumber does not match BaseFileRecordSegment");
                }
            }
            return result;
        }

        public FileRecord GetFileRecord(long baseSegmentNumber)
        {
            FileRecordSegment baseSegment = GetFileRecordSegment(baseSegmentNumber);
            if (baseSegment != null && baseSegment.IsBaseFileRecord)
            {
                AttributeRecord attributeListRecord = baseSegment.GetImmediateAttributeRecord(AttributeType.AttributeList, String.Empty);
                if (attributeListRecord == null)
                {
                    return new FileRecord(baseSegment);
                }
                else
                {
                    // The attribute list contains entries for every attribute the record has (excluding the attribute list),
                    // including attributes that reside within the base record segment.
                    AttributeList attributeList = new AttributeList(m_volume, attributeListRecord);
                    List<AttributeListEntry> entries = attributeList.ReadEntries();
                    List<MftSegmentReference> references = AttributeList.GetSegmentReferenceList(entries);
                    int baseSegmentIndex = MftSegmentReference.IndexOfSegmentNumber(references, baseSegmentNumber);
                    
                    if (baseSegmentIndex >= 0)
                    {
                        references.RemoveAt(baseSegmentIndex);
                    }

                    List<FileRecordSegment> recordSegments = new List<FileRecordSegment>();
                    // we want the base record segment first
                    recordSegments.Add(baseSegment);

                    foreach (MftSegmentReference reference in references)
                    {
                        FileRecordSegment segment = GetFileRecordSegment(reference);
                        if (segment != null)
                        {
                            recordSegments.Add(segment);
                        }
                        else
                        {
                            // record is invalid
                            return null;
                        }
                    }
                    return new FileRecord(recordSegments);
                }
            }
            else
            {
                return null;
            }
        }

        public FileRecord GetMftRecord()
        {
            return m_mftRecord;
        }

        public FileRecord GetVolumeRecord()
        {
            return GetFileRecord(VolumeSegmentReference);
        }

        public FileRecord GetVolumeBitmapRecord()
        {
            return GetFileRecord(BitmapSegmentReference);
        }

        public void UpdateFileRecord(FileRecord fileRecord, uint transactionID)
        {
            Dictionary<MftSegmentReference, byte[]> undoDictionary = new Dictionary<MftSegmentReference, byte[]>();
            foreach (FileRecordSegment segment in fileRecord.Segments)
            {
                byte[] segmentBytes = GetFileRecordSegment(segment.SegmentNumber).GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion, false);
                undoDictionary.Add(segment.SegmentReference, segmentBytes);
            }

            AttributeRecord oldAttributeList = fileRecord.BaseSegment.GetImmediateAttributeRecord(AttributeType.AttributeList, String.Empty);
            fileRecord.UpdateSegments(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion);
            FileRecordSegment baseSegment = fileRecord.BaseSegment;
            for (int segmentIndex = 1; segmentIndex < fileRecord.Segments.Count; segmentIndex++)
            {
                FileRecordSegment segment = fileRecord.Segments[segmentIndex];
                if (segment.SegmentReference == MftSegmentReference.NullReference)
                {
                    // New segment, we must allocate space for it
                    MftSegmentReference segmentReference;
                    if (baseSegment.SegmentNumber == MasterFileTable.MasterFileTableSegmentNumber)
                    {
                        segmentReference = AllocateReservedFileRecordSegment();
                    }
                    else
                    {
                        segmentReference = AllocateFileRecordSegment(transactionID);
                    }
                    FileRecordSegment newSegment = new FileRecordSegment(segmentReference.SegmentNumber, segmentReference.SequenceNumber, baseSegment.SegmentReference);
                    newSegment.IsInUse = true;
                    newSegment.IsDirectory = fileRecord.IsDirectory;
                    newSegment.NextAttributeInstance = segment.NextAttributeInstance;
                    newSegment.ImmediateAttributes.AddRange(segment.ImmediateAttributes);
                    fileRecord.Segments[segmentIndex] = newSegment;
                }
                else if (segment.ImmediateAttributes.Count == 0)
                {
                    byte[] undoData = undoDictionary[segment.SegmentReference];
                    ulong streamOffset = (ulong)(segment.SegmentNumber * m_volume.BytesPerFileRecordSegment);
                    m_volume.LogClient.WriteLogRecord(m_mftRecord.BaseSegmentReference, m_mftRecord.DataRecord, streamOffset, NTFSLogOperation.DeallocateFileRecordSegment, new byte[0], NTFSLogOperation.InitializeFileRecordSegment, undoData, transactionID);
                    DeallocateFileRecordSegment(segment, transactionID);
                    fileRecord.Segments.RemoveAt(segmentIndex);
                    segmentIndex--;
                }
            }

            for (int segmentIndex = 1; segmentIndex < fileRecord.Segments.Count; segmentIndex++)
            {
                FileRecordSegment segment = fileRecord.Segments[segmentIndex];
                byte[] undoData;
                byte[] redoData = segment.GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion, false);
                ulong streamOffset = (ulong)(segment.SegmentNumber * m_volume.BytesPerFileRecordSegment);
                if (undoDictionary.TryGetValue(segment.SegmentReference, out undoData))
                {
                    m_volume.LogClient.WriteLogRecord(m_mftRecord.BaseSegmentReference, m_mftRecord.DataRecord, streamOffset, NTFSLogOperation.InitializeFileRecordSegment, redoData, NTFSLogOperation.InitializeFileRecordSegment, undoData, transactionID);
                }
                else
                {
                    // New segment
                    m_volume.LogClient.WriteLogRecord(m_mftRecord.BaseSegmentReference, m_mftRecord.DataRecord, streamOffset, NTFSLogOperation.InitializeFileRecordSegment, redoData, NTFSLogOperation.DeallocateFileRecordSegment, new byte[0], transactionID);
                }
                UpdateFileRecordSegment(segment);
            }

            if (oldAttributeList is NonResidentAttributeRecord)
            {
                new NonResidentAttributeData(m_volume, null, (NonResidentAttributeRecord)oldAttributeList).Truncate(0);
            }

            bool needsAttributeList = (fileRecord.Segments.Count > 1);
            if (needsAttributeList)
            {
                List<AttributeListEntry> entries = FileRecordHelper.BuildAttributeList(fileRecord.Segments, m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion);
                int dataLength = AttributeList.GetLength(entries);
                int attributeListRecordLength = ResidentAttributeRecord.GetRecordLength(0, dataLength);
                int numberOfBytesFreeInBaseSegment = baseSegment.GetNumberOfBytesFree(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion);
                bool isResident = (attributeListRecordLength <= numberOfBytesFreeInBaseSegment);
                AttributeRecord attributeListRecord = baseSegment.CreateAttributeListRecord(isResident);
                AttributeList attributeList = new AttributeList(m_volume, attributeListRecord);
                attributeList.WriteEntries(entries);
            }

            byte[] baseRecordUndoData = undoDictionary[baseSegment.SegmentReference];
            byte[] baseRecordRedoData = baseSegment.GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion, false);
            ulong baseRecordStreamOffset = (ulong)(baseSegment.SegmentNumber * m_volume.BytesPerFileRecordSegment);
            m_volume.LogClient.WriteLogRecord(m_mftRecord.BaseSegmentReference, m_mftRecord.DataRecord, baseRecordStreamOffset, NTFSLogOperation.InitializeFileRecordSegment, baseRecordRedoData, NTFSLogOperation.InitializeFileRecordSegment, baseRecordUndoData, transactionID);
            UpdateFileRecordSegment(baseSegment);
        }

        public void UpdateFileRecordSegment(FileRecordSegment recordSegment)
        {
            long segmentNumber = recordSegment.SegmentNumber;
            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;
            if (segmentNumber >= FirstReservedSegmentNumber)
            {
                recordSegment.UpdateSequenceNumber++;
            }
            recordSegment.LogFileSequenceNumber = 0;
            byte[] recordSegmentBytes = recordSegment.GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion, true);

            m_mftFile.Data.WriteSectors(firstSectorIndex, recordSegmentBytes);
        }

        public FileRecord CreateFile(List<FileNameRecord> fileNameRecords, uint transactionID)
        {
            if (fileNameRecords.Count == 0)
            {
                throw new ArgumentException();
            }
            bool isDirectory = fileNameRecords[0].IsDirectory;
            MftSegmentReference segmentReference = AllocateFileRecordSegment(transactionID);
            FileRecordSegment fileRecordSegment = new FileRecordSegment(segmentReference.SegmentNumber, segmentReference.SequenceNumber);
            
            // UpdateFileRecord() expects the base segment to be present on disk
            byte[] redoData = fileRecordSegment.GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion, false);
            ulong streamOffset = (ulong)(fileRecordSegment.SegmentNumber * m_volume.BytesPerFileRecordSegment);
            m_volume.LogClient.WriteLogRecord(m_mftRecord.BaseSegmentReference, m_mftRecord.DataRecord, streamOffset, NTFSLogOperation.InitializeFileRecordSegment, redoData, NTFSLogOperation.DeallocateFileRecordSegment, new byte[0], transactionID);
            UpdateFileRecordSegment(fileRecordSegment);

            fileRecordSegment.ReferenceCount = (ushort)fileNameRecords.Count; // Each FileNameRecord is about to be indexed
            fileRecordSegment.IsInUse = true;
            fileRecordSegment.IsDirectory = isDirectory;

            FileRecord fileRecord = new FileRecord(fileRecordSegment);
            StandardInformationRecord standardInformation = (StandardInformationRecord)fileRecord.CreateAttributeRecord(AttributeType.StandardInformation, String.Empty);
            standardInformation.CreationTime = fileNameRecords[0].CreationTime;
            standardInformation.ModificationTime = fileNameRecords[0].ModificationTime;
            standardInformation.MftModificationTime = fileNameRecords[0].MftModificationTime;
            standardInformation.LastAccessTime = fileNameRecords[0].LastAccessTime;
            standardInformation.FileAttributes = 0;
            foreach (FileNameRecord fileNameRecord in fileNameRecords)
            {
                FileNameAttributeRecord fileNameAttribute = (FileNameAttributeRecord)fileRecord.CreateAttributeRecord(AttributeType.FileName, String.Empty);
                fileNameAttribute.IsIndexed = true;
                fileNameAttribute.Record = fileNameRecord;
            }
            
            if (isDirectory)
            {
                string indexName = IndexHelper.GetIndexName(AttributeType.FileName);
                IndexRootRecord indexRoot = (IndexRootRecord)fileRecord.CreateAttributeRecord(AttributeType.IndexRoot, indexName);
                IndexHelper.InitializeIndexRoot(indexRoot, AttributeType.FileName, m_volume.BytesPerIndexRecord, m_volume.BytesPerCluster);
            }
            else
            {
                fileRecord.CreateAttributeRecord(AttributeType.Data, String.Empty);
            }

            UpdateFileRecord(fileRecord, transactionID);

            return fileRecord;
        }

        public void DeleteFile(FileRecord fileRecord, uint transactionID)
        {
            foreach (FileRecordSegment segment in fileRecord.Segments)
            {
                byte[] undoData = GetFileRecordSegment(segment.SegmentNumber).GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.MinorVersion, false);
                ulong streamOffset = (ulong)(segment.SegmentNumber * m_volume.BytesPerFileRecordSegment);
                m_volume.LogClient.WriteLogRecord(m_mftRecord.BaseSegmentReference, m_mftRecord.DataRecord, streamOffset, NTFSLogOperation.DeallocateFileRecordSegment, new byte[0], NTFSLogOperation.InitializeFileRecordSegment, undoData, transactionID);
                DeallocateFileRecordSegment(segment, transactionID);
            }
        }

        private MftSegmentReference AllocateReservedFileRecordSegment()
        {
            BitmapData bitmap = m_mftFile.Bitmap;
            if (bitmap == null)
            {
                throw new InvalidDataException("Invalid MFT Record, missing Bitmap attribute");
            }
            long? segmentNumber = bitmap.AllocateRecord(FirstReservedSegmentNumber, FirstUserSegmentNumber - 1);
            if (!segmentNumber.HasValue)
            {
                throw new DiskFullException();
            }

            FileRecordSegment previousSegment = GetFileRecordSegment(segmentNumber.Value);
            ushort sequenceNumber = (previousSegment == null) ? (ushort)1 : previousSegment.SequenceNumber;
            return new MftSegmentReference(segmentNumber.Value, sequenceNumber);
        }

        private MftSegmentReference AllocateFileRecordSegment(uint transactionID)
        {
            BitmapData bitmap = m_mftFile.Bitmap;
            if (bitmap == null)
            {
                throw new InvalidDataException("Invalid MFT Record, missing Bitmap attribute");
            }
            long mftBitmapSearchStartIndex = MasterFileTable.FirstUserSegmentNumber;
            long? segmentNumber = bitmap.AllocateRecord(mftBitmapSearchStartIndex, transactionID);
            if (!segmentNumber.HasValue)
            {
                long numberOfUsableBits = bitmap.NumberOfUsableBits;
                Extend();
                segmentNumber = bitmap.AllocateRecord(numberOfUsableBits, transactionID);
            }

            FileRecordSegment previousSegment = GetFileRecordSegment(segmentNumber.Value);
            ushort sequenceNumber = (previousSegment == null) ? (ushort)1 : previousSegment.SequenceNumber;
            return new MftSegmentReference(segmentNumber.Value, sequenceNumber);
        }

        /// <summary>
        /// It's up to the caller to log the changes to the file record segment
        /// </summary>
        private void DeallocateFileRecordSegment(FileRecordSegment segment, uint transactionID)
        {
            BitmapData bitmap = m_mftFile.Bitmap;
            if (bitmap == null)
            {
                throw new InvalidDataException("Invalid MFT Record, missing Bitmap attribute");
            }
            ushort nextSequenceNumber = (ushort)(segment.SequenceNumber + 1);
            FileRecordSegment segmentToWrite = new FileRecordSegment(segment.SegmentNumber, nextSequenceNumber);
            UpdateFileRecordSegment(segmentToWrite);
            bitmap.DeallocateRecord(segment.SegmentNumber, transactionID);
        }

        public void Extend()
        {
            ulong additionalDataLength = (ulong)(m_volume.BytesPerFileRecordSegment * ExtendGranularity);
            ulong additionalBitmapLength = ExtendGranularity / 8;
            // We calculate the maximum possible number of free cluster required
            long numberOfClustersRequiredForData = (long)Math.Ceiling((double)additionalDataLength / m_volume.BytesPerCluster);
            long numberOfClustersRequiredForBitmap = (long)Math.Ceiling((double)additionalBitmapLength / m_volume.BytesPerCluster);
            if (numberOfClustersRequiredForData + numberOfClustersRequiredForBitmap > m_volume.NumberOfFreeClusters)
            {
                throw new DiskFullException();
            }

            // We have to extend the bitmap first because one of the constructor parameters is the size of the data.
            // MFT Bitmap: ValidDataLength could be smaller than FileSize, however, we will later copy the value of ValidDataLength.
            // to the MFT mirror, we have to make sure that the copy will not become stale after writing beyond the current ValidDataLength.
            m_mftFile.Bitmap.ExtendBitmap(ExtendGranularity, true);

            // MFT Data: ValidDataLength must be equal to FileSize.
            // This will take care of the FileNameRecord and RootDirectory index as well.
            // Note: The NTFS v5.1 driver does not bother updating the FileNameRecord.
            m_mftFile.WriteData(m_mftFile.Data.Length, new byte[additionalDataLength]);

            // Update the MFT mirror
            MasterFileTable mftMirror = new MasterFileTable(m_volume, false, true);
            // When the MFT has an attribute list, CHKDSK expects the mirror to contain the segment references from the MFT as-is.
            FileRecordSegment mftRecordSegmentFromMirror = mftMirror.GetFileRecordSegment(MasterFileTableSegmentNumber);
            mftRecordSegmentFromMirror.ImmediateAttributes.Clear();
            mftRecordSegmentFromMirror.ImmediateAttributes.AddRange(m_mftFile.FileRecord.BaseSegment.ImmediateAttributes);
            // CHKDSK seems to expect the mirror's NextAttributeInstance to be the same as the MFT.
            mftRecordSegmentFromMirror.NextAttributeInstance = m_mftFile.FileRecord.BaseSegment.NextAttributeInstance;
            mftMirror.UpdateFileRecordSegment(mftRecordSegmentFromMirror);
        }

        // In NTFS v3.1 the FileRecord's self reference SegmentNumber is 32 bits,
        // but the MftSegmentReference's SegmentNumber is 48 bits.
        public long GetNumberOfUsableSegments()
        {
            return (long)(m_mftRecord.NonResidentDataRecord.FileSize / (uint)m_volume.BytesPerFileRecordSegment);
        }
    }
}
