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
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class FileRecord // A collection of base record segment and zero or more file record segments making up this file record
    {
        List<FileRecordSegment> m_segments;
        private List<AttributeRecord> m_attributes;

        private DataRecord m_dataRecord;

        public FileRecord(FileRecordSegment segment)
        {
            m_segments = new List<FileRecordSegment>();
            m_segments.Add(segment);
        }

        public FileRecord(List<FileRecordSegment> segments)
        {
            m_segments = segments;
        }

        public void UpdateSegments(int maximumSegmentLength, int bytesPerSector, ushort minorNTFSVersion)
        {
            foreach (FileRecordSegment segment in m_segments)
            {
                segment.ImmediateAttributes.Clear();
            }

            int segmentLength = FileRecordSegment.GetFirstAttributeOffset(maximumSegmentLength, minorNTFSVersion);
            segmentLength += FileRecordSegment.EndMarkerLength;

            foreach (AttributeRecord attribute in this.Attributes)
            {
                segmentLength += (int)attribute.RecordLength;
            }

            if (segmentLength <= maximumSegmentLength)
            {
                // a single record segment is needed
                FileRecordSegment baseRecordSegment = m_segments[0];
                foreach (AttributeRecord attribute in this.Attributes)
                {
                    baseRecordSegment.ImmediateAttributes.Add(attribute);
                }

                // free the rest of the segments, if there are any
                for (int index = 1; index < m_segments.Count; index++)
                {
                    m_segments[index].IsInUse = false;
                }
            }
            else
            {
                // we have to check if we can make some data streams non-resident,
                // otherwise we have to use child segments and create an attribute list
                throw new NotImplementedException();
            }
        }

        public List<AttributeRecord> GetAssembledAttributes()
        {
            return GetAssembledAttributes(m_segments);
        }

        public List<FileRecordSegment> Segments
        {
            get
            {
                return m_segments;
            }
        }

        public List<AttributeRecord> Attributes
        {
            get
            {
                if (m_attributes == null)
                {
                    m_attributes = GetAssembledAttributes();
                }
                return m_attributes;
            }
        }

        public StandardInformationRecord StandardInformation
        {
            get
            {
                foreach (AttributeRecord attribute in this.Attributes)
                {
                    if (attribute is StandardInformationRecord)
                    {
                        return (StandardInformationRecord)attribute;
                    }
                }
                return null;
            }
        }

        public FileNameRecord GetFileNameRecord(FilenameNamespace filenameNamespace)
        {
            foreach (AttributeRecord attribute in this.Attributes)
            {
                if (attribute is FileNameAttributeRecord)
                {
                    FileNameRecord fileNameAttribute = ((FileNameAttributeRecord)attribute).Record;
                    if (fileNameAttribute.Namespace == filenameNamespace)
                    {
                        return fileNameAttribute;
                    }
                }
            }
            return null;
        }

        public FileNameRecord LongFileNameRecord
        {
            get
            {
                FileNameRecord record = GetFileNameRecord(FilenameNamespace.Win32);
                if (record == null)
                {
                    record = GetFileNameRecord(FilenameNamespace.POSIX);
                }
                return record;
            }
        }

        // 8.3 filename
        public FileNameRecord ShortFileNameRecord
        {
            get
            {
                FileNameRecord record = GetFileNameRecord(FilenameNamespace.DOS);
                if (record == null)
                {
                    // Win32AndDOS means that both the Win32 and the DOS filenames are identical and hence have been saved in this single filename record.
                    record = GetFileNameRecord(FilenameNamespace.Win32AndDOS);
                }
                return record;
            }
        }

        public FileNameRecord FileNameRecord
        {
            get
            {
                FileNameRecord fileNameRecord = this.LongFileNameRecord;
                if (fileNameRecord == null)
                {
                    fileNameRecord = this.ShortFileNameRecord;
                }

                return fileNameRecord;
            }
        }

        /// <summary>
        /// Will return the long filename of the file
        /// </summary>
        public string FileName
        {
            get
            {
                FileNameRecord fileNameRecord = this.FileNameRecord;
                if (fileNameRecord != null)
                {
                    return fileNameRecord.FileName;
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        public long ParentDirMftSegmentNumber
        {
            get
            {
                FileNameRecord fileNameRecord = this.LongFileNameRecord;
                if (fileNameRecord == null)
                {
                    fileNameRecord = this.ShortFileNameRecord;
                }

                if (fileNameRecord != null)
                {
                    return fileNameRecord.ParentDirectory.SegmentNumber;
                }
                else
                {
                    return 0;
                }
            }
        }

        public AttributeRecord GetAttributeRecord(AttributeType type, string name)
        {
            foreach (AttributeRecord attribute in this.Attributes)
            {
                if (attribute.AttributeType == type && attribute.Name == name)
                {
                    return attribute;
                }
            }

            return null;
        }

        public DataRecord DataRecord
        {
            get
            {
                if (m_dataRecord == null)
                {
                    AttributeRecord record = GetAttributeRecord(AttributeType.Data, String.Empty);
                    if (record != null)
                    {
                        m_dataRecord = new DataRecord(record);
                    }
                }
                return m_dataRecord;
            }
        }

        public NonResidentAttributeRecord NonResidentDataRecord
        {
            get
            {
                if (this.DataRecord.Record is NonResidentAttributeRecord)
                {
                    return (NonResidentAttributeRecord)m_dataRecord.Record;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Segment number of base record
        /// </summary>
        public long MftSegmentNumber
        {
            get
            {
                return m_segments[0].MftSegmentNumber;
            }
        }

        /// <summary>
        /// Sequence number of base record
        /// </summary>
        public long SequenceNumber
        {
            get
            {
                return m_segments[0].SequenceNumber;
            }
        }

        public bool IsInUse
        {
            get
            {
                return m_segments[0].IsInUse;
            }
        }

        public bool IsDirectory
        {
            get
            {
                return m_segments[0].IsDirectory;
            }
        }

        public int StoredAttributesLength
        {
            get
            {
                int length = 0;
                foreach (AttributeRecord attribute in this.Attributes)
                {
                    length += (int)attribute.StoredRecordLength;
                }
                return length;
            }
        }

        public bool IsMetaFile
        {
            get
            {
                return (this.MftSegmentNumber <= MasterFileTable.LastReservedMftSegmentNumber);
            }
        }

        public static List<AttributeRecord> GetAssembledAttributes(List<FileRecordSegment> segments)
        {
            List<AttributeRecord> result = new List<AttributeRecord>();
            // we need to assemble fragmented attributes (if there are any)
            // if two attributes have the same AttributeType and Name, then we need to assemble them back together.
            // Note: only non-resident attributes can be fragmented
            // Reference: http://technet.microsoft.com/en-us/library/cc976808.aspx
            Dictionary<KeyValuePair<AttributeType, string>, List<NonResidentAttributeRecord>> fragments = new Dictionary<KeyValuePair<AttributeType, string>, List<NonResidentAttributeRecord>>();
            foreach (FileRecordSegment segment in segments)
            {
                foreach (AttributeRecord attribute in segment.ImmediateAttributes)
                {
                    if (attribute is ResidentAttributeRecord)
                    {
                        result.Add(attribute);
                    }
                    else
                    {
                        KeyValuePair<AttributeType, string> key = new KeyValuePair<AttributeType, string>(attribute.AttributeType, attribute.Name);
                        if (fragments.ContainsKey(key))
                        {
                            fragments[key].Add((NonResidentAttributeRecord)attribute);
                        }
                        else
                        {
                            List<NonResidentAttributeRecord> attributeFragments = new List<NonResidentAttributeRecord>();
                            attributeFragments.Add((NonResidentAttributeRecord)attribute);
                            fragments.Add(key, attributeFragments);
                        }
                    }
                }
            }

            // assemble all non-resident attributes
            foreach (List<NonResidentAttributeRecord> attributeFragments in fragments.Values)
            {
                // we assume attribute fragments are written to disk sorted by LowestVCN
                NonResidentAttributeRecord baseAttribute = attributeFragments[0];
                if (baseAttribute.LowestVCN != 0)
                {
                    Console.WriteLine(baseAttribute.AttributeType);
                    Console.WriteLine(segments[0].MftSegmentNumber);
                    throw new InvalidDataException("attribute fragments must be sorted");
                }

                if (baseAttribute.DataRunSequence.DataClusterCount != baseAttribute.HighestVCN + 1)
                {
                    Console.WriteLine(baseAttribute.DataRunSequence.ToString());
                    string message = String.Format("Cannot properly assemble data run sequence 0, expected length: {0}, sequence length: {1}",
                        baseAttribute.HighestVCN + 1, baseAttribute.DataRunSequence.DataClusterCount);
                    throw new Exception(message);
                }

                for (int index = 1; index < attributeFragments.Count; index++)
                {
                    NonResidentAttributeRecord attributeFragment = attributeFragments[index];
                    if (attributeFragment.LowestVCN == baseAttribute.HighestVCN + 1)
                    {
                        // The DataRunSequence of each additional file record segment starts at absolute LCN,
                        // so we need to convert it to relative offset before adding it to the base DataRunSequence
                        long absoluteOffset = attributeFragment.DataRunSequence[0].RunOffset;
                        long previousLCN = baseAttribute.DataRunSequence.LastDataRunStartLCN;
                        long relativeOffset = absoluteOffset - previousLCN;
                        attributeFragment.DataRunSequence[0].RunOffset = relativeOffset;

                        baseAttribute.DataRunSequence.AddRange(attributeFragment.DataRunSequence);
                        baseAttribute.HighestVCN = attributeFragment.HighestVCN;

                        if (baseAttribute.DataRunSequence.DataClusterCount != baseAttribute.HighestVCN + 1)
                        {
                            Console.WriteLine(attributeFragment.DataRunSequence.ToString());
                            string message = String.Format("Cannot properly assemble data run sequence, expected length: {0}, sequence length: {1}",
                                baseAttribute.HighestVCN + 1, baseAttribute.DataRunSequence.DataClusterCount);
                            throw new Exception(message);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("Invalid attribute fragments order");
                    }
                }

                result.Add(baseAttribute);
            }

            return result;
        }
    }
}
