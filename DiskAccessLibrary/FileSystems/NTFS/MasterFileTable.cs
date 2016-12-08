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

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class MasterFileTable
    {
        public const int LastReservedMftSegmentNumber = 23; // 12-23 are reserved for additional metafiles
        
        public const long MasterFileTableSegmentNumber = 0;
        public const long MftMirrorSegmentNumber = 1;
        // $LogFile = 2
        public const long VolumeSegmentNumber = 3;
        // $AttrDef = 4
        public const long RootDirSegmentNumber = 5;
        public const long BitmapSegmentNumber = 6;
        // $Boot = 7
        // $BadClus = 8
        // $Secure = 9
        // $UpCase = 10
        // $Extend = 11
        
        // The $Extend Metafile is simply a directory index that contains information on where to locate the last four metafiles ($ObjId, $Quota, $Reparse and $UsnJrnl)

        public NTFSVolume m_volume;
        private bool m_useMftMirror;
        
        private FileRecord m_mftRecord;

        public MasterFileTable(NTFSVolume volume, bool useMftMirror)
        {
            m_volume = volume;
            m_useMftMirror = useMftMirror;

            m_mftRecord = ReadMftRecord();
        }

        private FileRecord ReadMftRecord()
        {
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            if (bootRecord != null)
            {
                long mftStartLCN;
                if (m_useMftMirror)
                {
                    mftStartLCN = (long)bootRecord.MftMirrorStartLCN;
                }
                else
                {
                    mftStartLCN = (long)bootRecord.MftStartLCN;
                }
                
                FileRecordSegment mftRecordSegment = GetRecordSegmentOfMasterFileTable(mftStartLCN, MasterFileTableSegmentNumber);
                if (!mftRecordSegment.IsBaseFileRecord)
                {
                    return null;
                }

                AttributeRecord attributeListRecord = mftRecordSegment.GetImmediateAttributeRecord(AttributeType.AttributeList);
                if (attributeListRecord == null)
                {
                    return new FileRecord(mftRecordSegment);
                }
                else
                {
                    // I have never personally seen an MFT with an attribute list
                    AttributeListRecord attributeList = new AttributeListRecord(m_volume, attributeListRecord);
                    List<MftSegmentReference> references = attributeList.GetSegmentReferenceList();
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
                        FileRecordSegment segment = GetRecordSegmentOfMasterFileTable(mftStartLCN, reference);
                        if (segment != null)
                        {
                            recordSegments.Add(segment);
                        }
                        else
                        {
                            // MFT is invalid
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

        private FileRecordSegment GetRecordSegmentOfMasterFileTable(long mftStartLCN, MftSegmentReference reference)
        {
            FileRecordSegment result = GetRecordSegmentOfMasterFileTable(mftStartLCN, reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been modified, and an older version has been requested
                return null;
            }
            return result;
        }

        /// <summary>
        /// We can't use GetFileRecordSegment before strapping the MFT
        /// </summary>
        private FileRecordSegment GetRecordSegmentOfMasterFileTable(long mftStartLCN, long segmentNumber)
        {
            long sectorIndex = mftStartLCN * m_volume.SectorsPerCluster + segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] bytes = m_volume.ReadSectors(sectorIndex, m_volume.SectorsPerFileRecordSegment);
            FileRecordSegment result = new FileRecordSegment(bytes, 0, m_volume.BytesPerSector, MasterFileTableSegmentNumber);
            return result;
        }

        public FileRecordSegment GetFileRecordSegment(MftSegmentReference reference)
        {
            FileRecordSegment result = GetFileRecordSegment(reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been modified, and an older version has been requested
                return null;
            }
            return result;
        }

        private FileRecordSegment GetFileRecordSegment(long segmentNumber)
        { 
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            // Note: File record always start at the beginning of a sector
            // Note: Record can span multiple clusters, or alternatively, several records can be stored in the same cluster
            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] segmentBytes = m_mftRecord.NonResidentDataRecord.ReadDataSectors(m_volume, firstSectorIndex, m_volume.SectorsPerFileRecordSegment);

            if (FileRecordSegment.ContainsFileRecordSegment(segmentBytes))
            {
                FileRecordSegment recordSegment = new FileRecordSegment(segmentBytes, m_volume.BootRecord.BytesPerSector, segmentNumber);
                return recordSegment;
            }
            else
            {
                return null;
            }
        }

        public FileRecord GetFileRecord(MftSegmentReference reference)
        {
            FileRecord result = GetFileRecord(reference.SegmentNumber);
            if (result != null)
            {
                if (result.SequenceNumber != reference.SequenceNumber)
                {
                    // The file record segment has been modified, and an older version has been requested
                    return null;
                }
            }
            return result;
        }

        public FileRecord GetFileRecord(long baseSegmentNumber)
        {
            FileRecordSegment baseRecordSegment = GetFileRecordSegment(baseSegmentNumber);
            if (baseRecordSegment != null && baseRecordSegment.IsBaseFileRecord)
            {
                AttributeRecord attributeListRecord = baseRecordSegment.GetImmediateAttributeRecord(AttributeType.AttributeList);
                if (attributeListRecord == null)
                {
                    return new FileRecord(baseRecordSegment);
                }
                else
                {
                    // The attribute list contains entries for every attribute the record has (excluding the attribute list),
                    // including attributes that reside within the base record segment.
                    AttributeListRecord attributeList = new AttributeListRecord(m_volume, attributeListRecord);
                    List<MftSegmentReference> references = attributeList.GetSegmentReferenceList();
                    int baseSegmentIndex = MftSegmentReference.IndexOfSegmentNumber(references, baseSegmentNumber);
                    
                    if (baseSegmentIndex >= 0)
                    {
                        references.RemoveAt(baseSegmentIndex);
                    }

                    List<FileRecordSegment> recordSegments = new List<FileRecordSegment>();
                    // we want the base record segment first
                    recordSegments.Add(baseRecordSegment);

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
            return GetFileRecord(VolumeSegmentNumber);
        }

        public FileRecord GetBitmapRecord()
        {
            return GetFileRecord(BitmapSegmentNumber);
        }

        public void UpdateFileRecord(FileRecord record)
        {
            NTFSBootRecord bootRecord = m_volume.BootRecord;
            record.UpdateSegments(bootRecord.FileRecordSegmentLength, m_volume.BytesPerSector, m_volume.MinorVersion);
            
            foreach (FileRecordSegment segment in record.Segments)
            {
                if (segment.MftSegmentNumber >= 0)
                {
                    UpdateFileRecordSegment(segment);
                }
                else
                { 
                    // new segment, we must allocate space for it
                    throw new NotImplementedException();
                }
            }
        }

        public void UpdateFileRecordSegment(FileRecordSegment recordSegment)
        {
            long segmentNumber = recordSegment.MftSegmentNumber;
            
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;

            byte[] recordSegmentBytes = recordSegment.GetBytes(bootRecord.FileRecordSegmentLength, m_volume.BytesPerCluster, m_volume.MinorVersion);

            m_mftRecord.NonResidentDataRecord.WriteDataSectors(m_volume, firstSectorIndex, recordSegmentBytes);
        }

        // NTFS limit is 2^32-1 files, but in theory the number of record segments can be higher
        // http://technet.microsoft.com/en-us/library/cc938432.aspx
        public long GetMaximumNumberOfSegments()
        {
            NTFSBootRecord bootRecord = m_volume.BootRecord;
            if (bootRecord != null)
            {
                long maximumNumberOfRecords = (long)(m_mftRecord.NonResidentDataRecord.FileSize / (uint)m_volume.BootRecord.FileRecordSegmentLength);
                return maximumNumberOfRecords;
            }
            else
            {
                return 0;
            }
        }
    }
}
