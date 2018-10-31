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
    public partial class IndexData
    {
        private const int ExtendGranularity = 16; // Number of IndexRecord slots to allocate during each time we extend the data, we wish to avoid the data being too fragmented.

        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeType m_indexedAttributeType; // Type of the attribute being indexed
        private string m_indexName;
        private IndexRootRecord m_rootRecord;
        private IndexAllocationRecord m_indexAllocationRecord;
        private NonResidentAttributeData m_indexAllocationData;
        private AttributeRecord m_bitmapRecord;
        private BitmapData m_bitmapData;

        public IndexData(NTFSVolume volume, FileRecord fileRecord, AttributeType indexedAttributeType)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_indexedAttributeType = indexedAttributeType;
            m_indexName = IndexHelper.GetIndexName(indexedAttributeType);
            m_rootRecord = (IndexRootRecord)m_fileRecord.GetAttributeRecord(AttributeType.IndexRoot, m_indexName);
            // I have observed the NTFS v5.1 driver keeping the IndexAllocation and associated Bitmap attributes after deleting files from the directory even though m_rootRecord.IsParentNode is set to false.
            m_indexAllocationRecord = (IndexAllocationRecord)m_fileRecord.GetAttributeRecord(AttributeType.IndexAllocation, m_indexName);
            m_bitmapRecord = m_fileRecord.GetAttributeRecord(AttributeType.Bitmap, m_indexName);
            if (m_indexAllocationRecord != null && m_bitmapRecord != null)
            {
                m_indexAllocationData = new NonResidentAttributeData(m_volume, m_fileRecord, m_indexAllocationRecord);
                long numberOfUsableBits = (long)(m_indexAllocationRecord.DataLength / m_rootRecord.BytesPerIndexRecord);
                m_bitmapData = new BitmapData(m_volume, m_fileRecord, m_bitmapRecord, numberOfUsableBits);
            }
            else if (m_rootRecord.IsParentNode && m_indexAllocationRecord == null)
            {
                throw new InvalidDataException("Missing Index Allocation Record");
            }
            else if (m_rootRecord.IsParentNode && m_bitmapRecord == null)
            {
                throw new InvalidDataException("Missing Index Bitmap Record");
            }
        }

        public KeyValuePair<MftSegmentReference, byte[]>? FindEntry(byte[] key)
        {
            if (!m_rootRecord.IsParentNode)
            {
                int index = CollationHelper.FindIndexInLeafNode(m_rootRecord.IndexEntries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    IndexEntry entry = m_rootRecord.IndexEntries[index];
                    return new KeyValuePair<MftSegmentReference, byte[]>(entry.FileReference, entry.Key);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                bool isParentNode = true;
                List<IndexEntry> entries = m_rootRecord.IndexEntries;
                int index;
                while (isParentNode)
                {
                    index = CollationHelper.FindIndexInParentNode(entries, key, m_rootRecord.CollationRule);
                    IndexEntry entry = entries[index];
                    if (!entry.IsLastEntry && CollationHelper.Compare(entry.Key, key, m_rootRecord.CollationRule) == 0)
                    {
                        return new KeyValuePair<MftSegmentReference, byte[]>(entry.FileReference, entry.Key);
                    }
                    else
                    {
                        long subnodeVBN = entry.SubnodeVBN;
                        IndexRecord indexRecord = ReadIndexRecord(subnodeVBN);
                        isParentNode = indexRecord.IsParentNode;
                        entries = indexRecord.IndexEntries;
                    }
                }

                index = CollationHelper.FindIndexInLeafNode(entries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    IndexEntry entry = entries[index];
                    return new KeyValuePair<MftSegmentReference, byte[]>(entry.FileReference, entry.Key);
                }
                else
                {
                    return null;
                }
            }
        }

        public void AddEntry(MftSegmentReference fileReference, byte[] key)
        {
            IndexEntry entry = new IndexEntry();
            entry.FileReference = fileReference;
            entry.Key = key;
            if (!m_rootRecord.IsParentNode)
            {
                int insertIndex = CollationHelper.FindIndexForSortedInsert(m_rootRecord.IndexEntries, key, m_rootRecord.CollationRule);
                m_rootRecord.IndexEntries.Insert(insertIndex, entry);
                if (m_rootRecord.RecordLength >= m_volume.AttributeRecordLengthToMakeNonResident)
                {
                    if (m_indexAllocationRecord == null)
                    {
                        m_indexAllocationRecord = (IndexAllocationRecord)m_fileRecord.CreateAttributeRecord(AttributeType.IndexAllocation, m_indexName);
                        m_indexAllocationData = new NonResidentAttributeData(m_volume, m_fileRecord, m_indexAllocationRecord);
                        m_bitmapRecord = m_fileRecord.CreateAttributeRecord(AttributeType.Bitmap, m_indexName);
                        m_bitmapData = new BitmapData(m_volume, m_fileRecord, m_bitmapRecord, 0);
                    }
                    SplitRootIndexRecord();
                }
                else
                {
                    m_volume.UpdateFileRecord(m_fileRecord);
                }
            }
            else
            {
                KeyValuePairList<int, IndexRecord> path = FindInsertPath(key);
                IndexRecord leafRecord = path[path.Count - 1].Value;
                long leafRecordVBN = leafRecord.RecordVBN;
                int insertIndex = CollationHelper.FindIndexForSortedInsert(leafRecord.IndexEntries, key, m_rootRecord.CollationRule);
                leafRecord.IndexEntries.Insert(insertIndex, entry);
                long leafRecordIndex = ConvertToRecordIndex(leafRecordVBN);
                if (leafRecord.DoesFit((int)m_rootRecord.BytesPerIndexRecord))
                {
                    WriteIndexRecord(leafRecordIndex, leafRecord);
                }
                else
                {
                    // Split index record
                    SplitIndexRecord(path);
                }
            }
        }

        private KeyValuePairList<int, IndexRecord> FindInsertPath(byte[] key)
        {
            bool isParentNode = true;
            List<IndexEntry> entries = m_rootRecord.IndexEntries;
            KeyValuePairList<int, IndexRecord> path = new KeyValuePairList<int, IndexRecord>();
            while (isParentNode)
            {
                int index = CollationHelper.FindIndexInParentNode(entries, key, m_rootRecord.CollationRule);
                long subnodeVBN = entries[index].SubnodeVBN;
                IndexRecord indexRecord = ReadIndexRecord(subnodeVBN);
                isParentNode = indexRecord.IsParentNode;
                entries = indexRecord.IndexEntries;
                path.Add(index, indexRecord);
            }
            return path;
        }

        /// <remarks>
        /// The root node can contain a limited number of entries compare to an IndexRecord,
        /// so there is no point splitting it to two child nodes, a single one would be sufficient.
        /// </remarks>
        private void SplitRootIndexRecord()
        {
            IndexRecord childRecord = new IndexRecord();
            childRecord.IsParentNode = m_rootRecord.IsParentNode;
            childRecord.IndexEntries = new List<IndexEntry>(m_rootRecord.IndexEntries);
            long childRecordIndex = AllocateIndexRecord();
            childRecord.RecordVBN = ConvertToVirtualBlockNumber(childRecordIndex);
            WriteIndexRecord(childRecordIndex, childRecord);

            IndexEntry rootEntry = new IndexEntry();
            rootEntry.SubnodeVBN = childRecord.RecordVBN;
            rootEntry.ParentNodeForm = true;

            m_rootRecord.IndexEntries.Clear();
            m_rootRecord.IsParentNode = true;
            m_rootRecord.IndexEntries.Add(rootEntry);
            m_volume.UpdateFileRecord(m_fileRecord);
        }

        /// <param name="path">Key is index in parent node</param>
        private void SplitIndexRecord(KeyValuePairList<int, IndexRecord> path)
        {
            int indexInParentRecord = path[path.Count - 1].Key;
            // We will treat the record we want to split as the right node, and create a left node
            IndexRecord rightNode = path[path.Count - 1].Value;
            long rightNodeVBN = rightNode.RecordVBN;
            long rightNodeIndex = ConvertToRecordIndex(rightNodeVBN);
            List<IndexEntry> rightNodeEntries = rightNode.IndexEntries;
            int splitIndex = rightNodeEntries.Count / 2;
            IndexEntry middleEntry = rightNodeEntries[splitIndex];
            IndexRecord leftNode = new IndexRecord();
            leftNode.IsParentNode = rightNode.IsParentNode;
            leftNode.IndexEntries = rightNodeEntries.GetRange(0, splitIndex);
            rightNodeEntries.RemoveRange(0, splitIndex + 1);
            if (rightNode.IsParentNode)
            {
                // A parent node has n keys and points to (n + 1) subnodes,
                // When splitting it to two nodes we will take the pointer from the entry we wish to push to the parent node,
                // and use it as the last pointer in the left node.
                IndexEntry leftNodeLastEntry = new IndexEntry();
                leftNodeLastEntry.ParentNodeForm = true;
                leftNodeLastEntry.IsLastEntry = true;
                leftNodeLastEntry.SubnodeVBN = middleEntry.SubnodeVBN;
                leftNode.IndexEntries.Add(leftNodeLastEntry);
            }
            long leftNodeIndex = AllocateIndexRecord();
            leftNode.RecordVBN = ConvertToVirtualBlockNumber(leftNodeIndex);
            IndexEntry newParentEntry = new IndexEntry(middleEntry.FileReference, middleEntry.Key);
            newParentEntry.ParentNodeForm = true;
            newParentEntry.SubnodeVBN = leftNode.RecordVBN;
            WriteIndexRecord(rightNodeIndex, rightNode);
            WriteIndexRecord(leftNodeIndex, leftNode);
            if (path.Count > 1)
            {
                IndexRecord parentRecord = path[path.Count - 2].Value;
                long parentRecordIndex = ConvertToRecordIndex(parentRecord.RecordVBN);
                List<IndexEntry> parentEntries = parentRecord.IndexEntries;
                parentEntries.Insert(indexInParentRecord, newParentEntry);
                if (parentRecord.DoesFit((int)m_rootRecord.BytesPerIndexRecord))
                {
                    WriteIndexRecord(parentRecordIndex, parentRecord);
                }
                else
                {
                    // Split parent index record
                    path.RemoveAt(path.Count - 1);
                    SplitIndexRecord(path);
                }
            }
            else
            {
                m_rootRecord.IndexEntries.Insert(indexInParentRecord, newParentEntry);
                if (m_rootRecord.RecordLength >= m_volume.AttributeRecordLengthToMakeNonResident)
                {
                    SplitRootIndexRecord();
                }
                else
                {
                    m_volume.UpdateFileRecord(m_fileRecord);
                }
            }
        }

        public void RemoveEntry(byte[] key)
        {
            if (!m_rootRecord.IsParentNode)
            {
                int index = CollationHelper.FindIndexInLeafNode(m_rootRecord.IndexEntries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    m_rootRecord.IndexEntries.RemoveAt(index);
                }
                m_volume.UpdateFileRecord(m_fileRecord);
            }
            else
            {
                int indexOfEntryToRemove;
                KeyValuePairList<int, IndexRecord> path = FindRemovalPath(key, out indexOfEntryToRemove);
                if (path == null)
                {
                    return;
                }

                if ((path.Count > 0 && path[path.Count - 1].Value.IsParentNode) || path.Count == 0)
                {
                    // We find the rightmost leaf entry in the left branch and put it instead.
                    // Note: Excluding the root of the branch, the rightmost leaf entry in the branch collates last.
                    KeyValuePairList<int, IndexRecord> pathToLeaf = FindPathToRightmostLeaf(path, indexOfEntryToRemove);
                    IndexRecord leaf = pathToLeaf[pathToLeaf.Count - 1].Value;
                    IndexEntry entryToRemoveFromLeaf = leaf.IndexEntries[leaf.IndexEntries.Count - 1];
                    leaf.IndexEntries.RemoveAt(leaf.IndexEntries.Count - 1);
                    long leafRecordIndex = ConvertToRecordIndex(leaf.RecordVBN);
                    // Note: CHKDSK does not accept an empty IndexRecord, however, we must not call RemovePointer just yet because it might affect the parent as well.
                    WriteIndexRecord(leafRecordIndex, leaf);

                    if (path.Count == 0)
                    {
                        m_rootRecord.IndexEntries[indexOfEntryToRemove].FileReference = entryToRemoveFromLeaf.FileReference;
                        m_rootRecord.IndexEntries[indexOfEntryToRemove].Key = entryToRemoveFromLeaf.Key;
                        m_volume.UpdateFileRecord(m_fileRecord);
                    }
                    else
                    {
                        path[path.Count - 1].Value.IndexEntries[indexOfEntryToRemove].FileReference = entryToRemoveFromLeaf.FileReference;
                        path[path.Count - 1].Value.IndexEntries[indexOfEntryToRemove].Key = entryToRemoveFromLeaf.Key;
                        long recordIndex = ConvertToRecordIndex(path[path.Count - 1].Value.RecordVBN);
                        WriteIndexRecord(recordIndex, path[path.Count - 1].Value);
                    }

                    if (leaf.IndexEntries.Count == 0)
                    {
                        int indexOfLeafPointer = pathToLeaf[pathToLeaf.Count - 1].Key;
                        RemovePointer(pathToLeaf.GetRange(0, pathToLeaf.Count - 1), indexOfLeafPointer);
                        DeallocateIndexRecord(leafRecordIndex);
                    }
                }
                else
                {
                    int indexInParentRecord = path[path.Count - 1].Key;
                    IndexRecord leaf = path[path.Count - 1].Value;

                    leaf.IndexEntries.RemoveAt(indexOfEntryToRemove);
                    long recordIndex = ConvertToRecordIndex(leaf.RecordVBN);
                    if (leaf.IndexEntries.Count > 0)
                    {
                        WriteIndexRecord(recordIndex, leaf);
                    }
                    else
                    {
                        path.RemoveAt(path.Count - 1);
                        RemovePointer(path, indexInParentRecord);
                        DeallocateIndexRecord(recordIndex);
                    }
                }
            }
        }

        private KeyValuePairList<int, IndexRecord> FindRemovalPath(byte[] key, out int indexOfEntryToRemove)
        {
            bool isParentNode = true;
            List<IndexEntry> entries = m_rootRecord.IndexEntries;
            KeyValuePairList<int, IndexRecord> path = new KeyValuePairList<int, IndexRecord>();
            while (isParentNode)
            {
                int index = CollationHelper.FindIndexInParentNode(entries, key, m_rootRecord.CollationRule);
                if (!entries[index].IsLastEntry && CollationHelper.Compare(entries[index].Key, key, m_rootRecord.CollationRule) == 0)
                {
                    indexOfEntryToRemove = index;
                    return path;
                }
                long subnodeVBN = entries[index].SubnodeVBN;
                IndexRecord indexRecord = ReadIndexRecord(subnodeVBN);
                isParentNode = indexRecord.IsParentNode;
                entries = indexRecord.IndexEntries;
                path.Add(index, indexRecord);
            }

            indexOfEntryToRemove = CollationHelper.FindIndexInLeafNode(entries, key, m_rootRecord.CollationRule);
            if (indexOfEntryToRemove >= 0)
            {
                return path;
            }
            else
            {
                return null;
            }
        }

        private KeyValuePairList<int, IndexRecord> FindPathToRightmostLeaf(KeyValuePairList<int, IndexRecord> startPath, int indexInLastEntry)
        {
            KeyValuePairList<int, IndexRecord> path = new KeyValuePairList<int, IndexRecord>(startPath);
            List<IndexEntry> entries;
            if (startPath.Count == 0)
            {
                entries = m_rootRecord.IndexEntries;
            }
            else
            {
                entries = startPath[startPath.Count - 1].Value.IndexEntries;
            }
            IndexRecord indexRecord = ReadIndexRecord(entries[indexInLastEntry].SubnodeVBN);
            path.Add(indexInLastEntry, indexRecord);
            return FindPathToRightmostLeaf(path);
        }

        private KeyValuePairList<int, IndexRecord> FindPathToRightmostLeaf(KeyValuePairList<int, IndexRecord> startPath)
        {
            KeyValuePairList<int, IndexRecord> path = new KeyValuePairList<int, IndexRecord>(startPath);
            int indexInParentRecord = startPath[startPath.Count - 1].Key;
            IndexRecord indexRecord = startPath[startPath.Count - 1].Value;
            while (indexRecord.IsParentNode)
            {
                indexInParentRecord = indexRecord.IndexEntries.Count - 1;
                long subnodeVBN = indexRecord.IndexEntries[indexInParentRecord].SubnodeVBN;
                indexRecord = ReadIndexRecord(subnodeVBN);
                path.Add(indexInParentRecord, indexRecord);
            }

            return path;
        }

        /// <summary>
        /// Will remove the pointer while preserving the entry (if present)
        /// </summary>
        private void RemovePointer(KeyValuePairList<int, IndexRecord> path, int indexOfEntryToRemove)
        {
            int indexInParentRecord = path[path.Count - 1].Key;
            IndexRecord indexRecord = path[path.Count - 1].Value;
            long recordIndex = ConvertToRecordIndex(indexRecord.RecordVBN);
            IndexEntry pointer = indexRecord.IndexEntries[indexOfEntryToRemove];
            if (pointer.IsLastEntry)
            {
                if (indexRecord.IndexEntries.Count == 1)
                {
                    if (path.Count > 1)
                    {
                        path.RemoveAt(path.Count - 1);
                        RemovePointer(path, indexInParentRecord);
                    }
                    else
                    {
                        RemovePointerFromRoot(indexInParentRecord);
                    }
                    DeallocateIndexRecord(recordIndex);
                }
                else
                {
                    MftSegmentReference fileReferenceToReinsert = indexRecord.IndexEntries[indexOfEntryToRemove - 1].FileReference;
                    byte[] keyToReinsert = indexRecord.IndexEntries[indexOfEntryToRemove - 1].Key;
                    indexRecord.IndexEntries.RemoveAt(indexOfEntryToRemove);
                    indexRecord.IndexEntries[indexOfEntryToRemove - 1].FileReference = MftSegmentReference.NullReference;
                    indexRecord.IndexEntries[indexOfEntryToRemove - 1].Key = new byte[0];
                    WriteIndexRecord(recordIndex, indexRecord);
                    AddEntry(fileReferenceToReinsert, keyToReinsert);
                }
            }
            else
            {
                MftSegmentReference fileReferenceToReinsert = indexRecord.IndexEntries[indexOfEntryToRemove].FileReference;
                byte[] keyToReinsert = indexRecord.IndexEntries[indexOfEntryToRemove].Key;
                indexRecord.IndexEntries.RemoveAt(indexOfEntryToRemove);
                WriteIndexRecord(recordIndex, indexRecord);
                AddEntry(fileReferenceToReinsert, keyToReinsert);
            }
        }

        /// <summary>
        /// Will remove the pointer while preserving the entry (if present)
        /// </summary>
        private void RemovePointerFromRoot(int indexOfEntryToRemove)
        {
            IndexEntry pointer = m_rootRecord.IndexEntries[indexOfEntryToRemove];
            if (pointer.IsLastEntry)
            {
                if (m_rootRecord.IndexEntries.Count == 1)
                {
                    m_rootRecord.IndexEntries.RemoveAt(indexOfEntryToRemove);
                    m_rootRecord.IsParentNode = false;
                }
                else
                {
                    MftSegmentReference fileReferenceToReinsert = m_rootRecord.IndexEntries[indexOfEntryToRemove - 1].FileReference;
                    byte[] keyToReinsert = m_rootRecord.IndexEntries[indexOfEntryToRemove - 1].Key;
                    m_rootRecord.IndexEntries.RemoveAt(indexOfEntryToRemove);
                    m_rootRecord.IndexEntries[indexOfEntryToRemove - 1].FileReference = MftSegmentReference.NullReference;
                    m_rootRecord.IndexEntries[indexOfEntryToRemove - 1].Key = new byte[0];
                    AddEntry(fileReferenceToReinsert, keyToReinsert);
                }
            }
            else
            {
                MftSegmentReference fileReferenceToReinsert = pointer.FileReference;
                byte[] keyToReinsert = pointer.Key;
                m_rootRecord.IndexEntries.RemoveAt(indexOfEntryToRemove);
                AddEntry(fileReferenceToReinsert, keyToReinsert);
            }
            m_volume.UpdateFileRecord(m_fileRecord);
        }

        public KeyValuePairList<MftSegmentReference, byte[]> GetAllEntries()
        {
            KeyValuePairList<MftSegmentReference, byte[]> result = new KeyValuePairList<MftSegmentReference, byte[]>();
            if (!m_rootRecord.IsParentNode)
            {
                foreach (IndexEntry entry in m_rootRecord.IndexEntries)
                {
                    result.Add(entry.FileReference, entry.Key);
                }
            }
            else
            {
                List<IndexEntry> parents = new List<IndexEntry>(m_rootRecord.IndexEntries);
                SortedList<long> subnodesVisited = new SortedList<long>();

                while (parents.Count > 0)
                {
                    IndexEntry parent = parents[0];
                    if (!subnodesVisited.Contains(parent.SubnodeVBN))
                    {
                        IndexRecord record = ReadIndexRecord(parent.SubnodeVBN);
                        if (record.IsParentNode)
                        {
                            parents.InsertRange(0, record.IndexEntries);
                        }
                        else
                        {
                            foreach (IndexEntry entry in record.IndexEntries)
                            {
                                result.Add(entry.FileReference, entry.Key);
                            }
                        }
                        subnodesVisited.Add(parent.SubnodeVBN);
                    }
                    else
                    {
                        if (!parent.IsLastEntry)
                        {
                            // Some of the tree data in NTFS is contained in non-leaf keys
                            result.Add(parent.FileReference, parent.Key);
                        }
                        parents.RemoveAt(0);
                    }
                }
            }
            return result;
        }

        /// <returns>Record Index</returns>
        private long AllocateIndexRecord()
        {
            uint transactionID = m_volume.LogClient.AllocateTransactionID();
            long? indexRecord = m_bitmapData.AllocateRecord(transactionID);
            if (indexRecord == null)
            {
                long numberOfUsableBits = m_bitmapData.NumberOfUsableBits;
                m_indexAllocationData.Extend(m_rootRecord.BytesPerIndexRecord * ExtendGranularity);
                m_bitmapData.ExtendBitmap(ExtendGranularity);
                indexRecord = m_bitmapData.AllocateRecord(numberOfUsableBits, transactionID);
            }
            m_volume.LogClient.WriteForgetTransactionRecord(transactionID);
            return indexRecord.Value;
        }

        private void DeallocateIndexRecord(long recordIndex)
        {
            uint transactionID = m_volume.LogClient.AllocateTransactionID();
            m_bitmapData.DeallocateRecord(recordIndex, transactionID);
            m_volume.LogClient.WriteForgetTransactionRecord(transactionID);
            // TODO: We may truncate the IndexAllocation attribute data and bitmap
        }

        private IndexRecord ReadIndexRecord(long subnodeVBN)
        {
            long sectorIndex = ConvertToSectorIndex(subnodeVBN);
            byte[] recordBytes = m_indexAllocationData.ReadSectors(sectorIndex, this.SectorsPerIndexRecord);
            MultiSectorHelper.RevertUsaProtection(recordBytes, 0);
            IndexRecord record = new IndexRecord(recordBytes, 0);
            return record;
        }

        private void WriteIndexRecord(long recordIndex, IndexRecord indexRecord)
        {
            long sectorsPerIndexRecord = m_rootRecord.BytesPerIndexRecord / m_volume.BytesPerSector;
            long sectorIndex = recordIndex * sectorsPerIndexRecord;

            m_indexAllocationData.WriteSectors(sectorIndex, indexRecord.GetBytes((int)m_rootRecord.BytesPerIndexRecord, true));
        }

        private long ConvertToSectorIndex(long recordVBN)
        {
            if (m_rootRecord.BytesPerIndexRecord >= m_volume.BytesPerCluster)
            {
                // The VBN is a VCN so we need to translate to sector index
                return recordVBN * m_volume.SectorsPerCluster;
            }
            else
            {
                return recordVBN * IndexRecord.BytesPerIndexRecordBlock / m_volume.BytesPerSector;
            }
        }

        private long ConvertToRecordIndex(long recordVBN)
        {
            long sectorIndex = ConvertToSectorIndex(recordVBN);
            long sectorsPerIndexRecord = m_rootRecord.BytesPerIndexRecord / m_volume.BytesPerSector;
            return sectorIndex / sectorsPerIndexRecord;
        }

        private long ConvertToVirtualBlockNumber(long recordIndex)
        {
            long sectorsPerIndexRecord = m_rootRecord.BytesPerIndexRecord / m_volume.BytesPerSector;
            long sectorIndex = recordIndex * sectorsPerIndexRecord;
            if (m_rootRecord.BytesPerIndexRecord >= m_volume.BytesPerCluster)
            {
                // The VBN is a VCN so we need to translate to cluster number
                return sectorIndex / m_volume.SectorsPerCluster;
            }
            else
            {
                return sectorIndex * m_volume.BytesPerSector / IndexRecord.BytesPerIndexRecordBlock;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return (m_rootRecord.IndexEntries.Count == 0);
            }
        }

        private int SectorsPerIndexRecord
        {
            get
            {
                return (int)m_rootRecord.BytesPerIndexRecord / m_volume.BytesPerSector;
            }
        }
    }
}
