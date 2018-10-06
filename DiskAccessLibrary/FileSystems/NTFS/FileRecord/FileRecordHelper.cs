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
    public class FileRecordHelper
    {
        /// <remarks>
        /// Only non-resident attributes can be fragmented.
        /// References:
        /// https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-2000-server/cc976808(v=technet.10)
        /// https://blogs.technet.microsoft.com/askcore/2009/10/16/the-four-stages-of-ntfs-file-growth/
        /// </remarks>
        public static List<AttributeRecord> GetAssembledAttributes(List<FileRecordSegment> segments)
        {
            List<AttributeRecord> result = new List<AttributeRecord>();
            // If two non-resident attributes have the same AttributeType and Name, then we need to assemble them back together.
            // Additional fragments immediately follow after the initial fragment.
            AttributeType currentAttributeType = AttributeType.None;
            string currentAttributeName = String.Empty;
            List<NonResidentAttributeRecord> fragments = new List<NonResidentAttributeRecord>();
            foreach (FileRecordSegment segment in segments)
            {
                foreach (AttributeRecord attribute in segment.ImmediateAttributes)
                {
                    if (attribute.AttributeType == AttributeType.AttributeList)
                    {
                        continue;
                    }

                    bool additionalFragment = (attribute is NonResidentAttributeRecord) && (fragments.Count > 0) &&
                                              (attribute.AttributeType == currentAttributeType) && (attribute.Name == currentAttributeName);

                    if (!additionalFragment && fragments.Count > 0)
                    {
                        NonResidentAttributeRecord assembledAttribute = AssembleFragments(fragments, segments[0].NextAttributeInstance);
                        segments[0].NextAttributeInstance++;
                        result.Add(assembledAttribute);
                        fragments.Clear();
                    }

                    if (attribute is ResidentAttributeRecord)
                    {
                        result.Add(attribute);
                    }
                    else
                    {
                        fragments.Add((NonResidentAttributeRecord)attribute);
                        if (!additionalFragment)
                        {
                            currentAttributeType = attribute.AttributeType;
                            currentAttributeName = attribute.Name;
                        }
                    }
                }
            }

            if (fragments.Count > 0)
            {
                NonResidentAttributeRecord assembledAttribute = AssembleFragments(fragments, segments[0].NextAttributeInstance);
                segments[0].NextAttributeInstance++;
                result.Add(assembledAttribute);
            }

            return result;
        }

        private static NonResidentAttributeRecord AssembleFragments(List<NonResidentAttributeRecord> attributeFragments, ushort nextAttributeInstance)
        {
            // Attribute fragments are written to disk sorted by LowestVCN
            NonResidentAttributeRecord firstFragment = attributeFragments[0];
            if (firstFragment.LowestVCN != 0)
            {
                string message = String.Format("Attribute fragments must be sorted. Attribute type: {0}", firstFragment.AttributeType);
                throw new InvalidDataException(message);
            }

            NonResidentAttributeRecord attribute = NonResidentAttributeRecord.Create(firstFragment.AttributeType, firstFragment.Name, nextAttributeInstance);
            attribute.Flags = firstFragment.Flags;
            attribute.LowestVCN = 0;
            attribute.HighestVCN = -1;
            attribute.CompressionUnit = firstFragment.CompressionUnit;
            attribute.AllocatedLength = firstFragment.AllocatedLength;
            attribute.FileSize = firstFragment.FileSize;
            attribute.ValidDataLength = firstFragment.ValidDataLength;

            foreach(NonResidentAttributeRecord attributeFragment in attributeFragments)
            {
                if (attributeFragment.LowestVCN == attribute.HighestVCN + 1)
                {
                    // The DataRunSequence of each NonResidentDataRecord fragment starts at absolute LCN,
                    // We need to convert it to relative offset before adding it to the base DataRunSequence
                    long runLength = attributeFragment.DataRunSequence[0].RunLength;
                    long absoluteOffset = attributeFragment.DataRunSequence[0].RunOffset;
                    long previousLCN = attribute.DataRunSequence.LastDataRunStartLCN;
                    long relativeOffset = absoluteOffset - previousLCN;

                    int runIndex = attribute.DataRunSequence.Count;
                    attribute.DataRunSequence.AddRange(attributeFragment.DataRunSequence);
                    attribute.DataRunSequence[runIndex] = new DataRun(runLength, relativeOffset);
                    attribute.HighestVCN = attributeFragment.HighestVCN;
                }
                else
                {
                    throw new InvalidDataException("Invalid attribute fragments order");
                }
            }

            return attribute;
        }

        public static void SliceAttributes(List<FileRecordSegment> segments, List<AttributeRecord> attributes, int bytesPerFileRecordSegment, ushort minorNTFSVersion)
        {
            int bytesAvailableInSegment = FileRecordSegment.GetNumberOfBytesAvailable(bytesPerFileRecordSegment, minorNTFSVersion);
            LinkedList<KeyValuePair<AttributeRecord, bool>> remainingAttributes = new LinkedList<KeyValuePair<AttributeRecord, bool>>();
            FileRecordSegment baseFileRecordSegment = segments[0];
            long segmentNumber = baseFileRecordSegment.SegmentNumber;
            bool isMftFileRecord = (segmentNumber == MasterFileTable.MasterFileTableSegmentNumber || segmentNumber == MasterFileTable.MftMirrorSegmentNumber);
            foreach (AttributeRecord attribute in attributes)
            {
                if (attribute.AttributeType == AttributeType.StandardInformation ||
                    attribute.AttributeType == AttributeType.FileName)
                {
                    baseFileRecordSegment.ImmediateAttributes.Add(attribute);
                }
                else if (isMftFileRecord && attribute.AttributeType == AttributeType.Data)
                {
                    List<NonResidentAttributeRecord> slices = SliceAttributeRecord((NonResidentAttributeRecord)attribute, bytesPerFileRecordSegment / 2, bytesAvailableInSegment);
                    baseFileRecordSegment.ImmediateAttributes.Add(slices[0]);
                    slices.RemoveAt(0);
                    foreach (NonResidentAttributeRecord slice in slices)
                    {
                        remainingAttributes.AddLast(new KeyValuePair<AttributeRecord, bool>(slice, true));
                    }
                }
                else
                {
                    remainingAttributes.AddLast(new KeyValuePair<AttributeRecord, bool>(attribute, false));
                }
            }

            int segmentIndex = 1;
            int remainingLengthInCurrentSegment = bytesAvailableInSegment;
            while (remainingAttributes.Count > 0)
            {
                AttributeRecord attribute = remainingAttributes.First.Value.Key;
                bool isSlice = remainingAttributes.First.Value.Value;

                if (segmentIndex == segments.Count)
                {
                    MftSegmentReference newSegmentReference = MftSegmentReference.NullReference;
                    FileRecordSegment newFileRecordSegment = new FileRecordSegment(newSegmentReference.SegmentNumber, newSegmentReference.SequenceNumber, baseFileRecordSegment.SegmentReference);
                    newFileRecordSegment.IsInUse = true;
                    segments.Add(newFileRecordSegment);
                }

                if (attribute.RecordLength <= remainingLengthInCurrentSegment)
                {
                    remainingLengthInCurrentSegment -= (int)attribute.RecordLength;
                    segments[segmentIndex].ImmediateAttributes.Add(attribute);
                    remainingAttributes.RemoveFirst();
                    // Instead of renumbering each attribute slice in the new FileRecordSegment, we use the original Instance number.
                    if (segments[segmentIndex].NextAttributeInstance <= attribute.Instance)
                    {
                        segments[segmentIndex].NextAttributeInstance = (ushort)(attribute.Instance + 1);
                    }
                }
                else
                {
                    if (attribute is ResidentAttributeRecord || isSlice)
                    {
                        segmentIndex++;
                        remainingLengthInCurrentSegment = bytesAvailableInSegment;
                    }
                    else
                    {
                        NonResidentAttributeRecord nonResidentAttribute = ((NonResidentAttributeRecord)attribute);
                        List<NonResidentAttributeRecord> slices = SliceAttributeRecord((NonResidentAttributeRecord)attribute, remainingLengthInCurrentSegment, bytesAvailableInSegment);
                        remainingAttributes.RemoveFirst();
                        slices.Reverse();
                        foreach (NonResidentAttributeRecord slice in slices)
                        {
                            remainingAttributes.AddFirst(new KeyValuePair<AttributeRecord, bool>(slice, true));
                        }
                    }
                }
            }
        }

        private static List<NonResidentAttributeRecord> SliceAttributeRecord(NonResidentAttributeRecord record, int remainingLengthInCurrentSegment, int bytesAvailableInSegment)
        {
            List<NonResidentAttributeRecord> result = new List<NonResidentAttributeRecord>();
            int numberOfRunsFitted = 0;
            int availableLength = remainingLengthInCurrentSegment;
            while (numberOfRunsFitted < record.DataRunSequence.Count)
            {
                NonResidentAttributeRecord slice = FitMaxNumberOfRuns(record, numberOfRunsFitted, availableLength);
                if (slice != null)
                {
                    result.Add(slice);
                    numberOfRunsFitted += slice.DataRunSequence.Count;
                }
                availableLength = bytesAvailableInSegment;
            }

            return result;
        }

        private static NonResidentAttributeRecord FitMaxNumberOfRuns(NonResidentAttributeRecord record, int runIndex, int availableLength)
        {
            // Each attribute record is aligned to 8-byte boundary, we must have enough room for padding
            availableLength = (int)Math.Floor((double)availableLength / 8) * 8;
            // Note that we're using the original record Instance instead of using the FileRecordSegment.NextAttributeInstance
            NonResidentAttributeRecord slice = new NonResidentAttributeRecord(record.AttributeType, record.Name, record.Instance);
            DataRunSequence dataRuns = record.DataRunSequence;
            long clusterCount = 0;
            for (int index = 0; index < runIndex; index++)
            {
                clusterCount += dataRuns[index].RunLength;
            }
            slice.LowestVCN = clusterCount;
            slice.DataRunSequence.Add(dataRuns[runIndex]);
            
            if (runIndex == 0)
            {
                slice.CompressionUnit = record.CompressionUnit;
                slice.AllocatedLength = record.AllocatedLength;
                slice.FileSize = record.FileSize;
                slice.ValidDataLength = record.ValidDataLength;
            }
            else
            {
                // The DataRunSequence of each NonResidentDataRecord fragment starts at absolute LCN
                long runLength = dataRuns[runIndex].RunLength;
                long runStartLCN = dataRuns.GetDataClusterLCN(clusterCount);
                slice.DataRunSequence[0] = new DataRun(runLength, runStartLCN);
            }
            clusterCount += dataRuns[runIndex].RunLength;

            int sliceRecordLength = NonResidentAttributeRecord.HeaderLength + record.Name.Length * 2 + slice.DataRunSequence.RecordLength;
            if (sliceRecordLength > availableLength)
            {
                return null;
            }

            runIndex++;
            while (runIndex < dataRuns.Count && sliceRecordLength + dataRuns[runIndex].RecordLength <= availableLength)
            {
                slice.DataRunSequence.Add(record.DataRunSequence[runIndex]);
                sliceRecordLength += dataRuns[runIndex].RecordLength;
                clusterCount += dataRuns[runIndex].RunLength;
                runIndex++;
            }

            slice.HighestVCN = clusterCount - 1;
            return slice;
        }

        /// <remarks>
        /// An attribute list MUST be sorted by AttributeType with a secondary sort by AttributeName.
        /// </remarks>
        public static List<AttributeListEntry> BuildAttributeList(List<FileRecordSegment> segments, int bytesPerFileRecordSegment, ushort minorNTFSVersion)
        {
            int bytesAvailableInSegment = FileRecordSegment.GetNumberOfBytesAvailable(bytesPerFileRecordSegment, minorNTFSVersion);

            List<AttributeListEntry> result = new List<AttributeListEntry>();
            foreach (FileRecordSegment segment in segments)
            {
                foreach (AttributeRecord attribute in segment.ImmediateAttributes)
                {
                    AttributeListEntry entry = new AttributeListEntry();
                    entry.AttributeType = attribute.AttributeType;
                    if (attribute is NonResidentAttributeRecord)
                    {
                        entry.LowestVCN = ((NonResidentAttributeRecord)attribute).LowestVCN;
                    }
                    entry.SegmentReference = segment.SegmentReference;
                    entry.Instance = attribute.Instance;
                    entry.AttributeName = attribute.Name;
                    result.Add(entry);
                }
            }
            return result;
        }

        /// <remarks>
        /// FileRecordSegment attributes MUST be sorted by AttributeType with a secondary sort by Name.
        /// </remarks>
        public static void InsertSorted(List<AttributeRecord> attributes, AttributeRecord attribute)
        {
            int insertIndex = SortedList<AttributeRecord>.FindIndexForSortedInsert(attributes, CompareAttributeTypes, attribute);
            attributes.Insert(insertIndex, attribute);
        }

        private static int CompareAttributeTypes(AttributeRecord attribute1, AttributeRecord attribute2)
        {
            int result = attribute1.AttributeType.CompareTo(attribute2.AttributeType);
            if (result == 0)
            {
                result = String.Compare(attribute1.Name, attribute2.Name, StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }
    }
}
