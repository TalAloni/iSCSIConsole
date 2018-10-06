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
        public bool ContainsFileName(string fileName)
        {
            byte[] key = FileNameRecord.GetIndexKeyBytes(fileName);
            return (FindEntry(key) != null);
        }

        public MftSegmentReference FindFileNameRecordSegmentReference(string fileName)
        {
            byte[] key = FileNameRecord.GetIndexKeyBytes(fileName);
            KeyValuePair<MftSegmentReference, byte[]>? entry = FindEntry(key);
            if (entry != null)
            {
                return entry.Value.Key;
            }
            else
            {
                return null;
            }
        }

        public KeyValuePair<MftSegmentReference, FileNameRecord>? FindFileNameRecord(string fileName)
        {
            byte[] key = FileNameRecord.GetIndexKeyBytes(fileName);
            KeyValuePair<MftSegmentReference, byte[]>? entry = FindEntry(key);
            if (entry != null)
            {
                MftSegmentReference fileReference = entry.Value.Key;
                FileNameRecord fileNameRecord = new FileNameRecord(entry.Value.Value, 0);
                return new KeyValuePair<MftSegmentReference, FileNameRecord>(fileReference, fileNameRecord);
            }
            else
            {
                return null;
            }
        }

        public bool UpdateFileNameRecord(FileNameRecord fileNameRecord)
        {
            byte[] key = fileNameRecord.GetBytes();
            if (!m_rootRecord.IsParentNode)
            {
                int index = CollationHelper.FindIndexInLeafNode(m_rootRecord.IndexEntries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    m_rootRecord.IndexEntries[index].Key = key;
                    m_volume.UpdateFileRecord(m_fileRecord);
                    return true;
                }
            }
            else
            {
                IndexRecord indexRecord = null;
                bool isParentNode = true;
                List<IndexEntry> entries = m_rootRecord.IndexEntries;
                int index;
                while (isParentNode)
                {
                    index = CollationHelper.FindIndexInParentNode(entries, key, m_rootRecord.CollationRule);
                    IndexEntry entry = entries[index];
                    if (!entry.IsLastEntry && CollationHelper.Compare(entry.Key, key, m_rootRecord.CollationRule) == 0)
                    {
                        entries[index].Key = key;
                        if (indexRecord == null)
                        {
                            m_volume.UpdateFileRecord(m_fileRecord);
                        }
                        else
                        {
                            long recordIndex = ConvertToRecordIndex(indexRecord.RecordVBN);
                            WriteIndexRecord(recordIndex, indexRecord);
                        }
                        return true;
                    }
                    else
                    {
                        long subnodeVBN = entry.SubnodeVBN;
                        indexRecord = ReadIndexRecord(subnodeVBN);
                        isParentNode = indexRecord.IsParentNode;
                        entries = indexRecord.IndexEntries;
                    }
                }

                index = CollationHelper.FindIndexInLeafNode(entries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    entries[index].Key = key;
                    if (indexRecord == null)
                    {
                        m_volume.UpdateFileRecord(m_fileRecord);
                    }
                    else
                    {
                        long recordIndex = ConvertToRecordIndex(indexRecord.RecordVBN);
                        WriteIndexRecord(recordIndex, indexRecord);
                    }
                    return true;
                }
            }

            return false;
        }

        public KeyValuePairList<MftSegmentReference, FileNameRecord> GetAllFileNameRecords()
        {
            KeyValuePairList<MftSegmentReference, FileNameRecord> result = new KeyValuePairList<MftSegmentReference, FileNameRecord>();
            KeyValuePairList<MftSegmentReference, byte[]> entries = GetAllEntries();
            foreach (KeyValuePair<MftSegmentReference, byte[]> entry in entries)
            {
                FileNameRecord fileNameRecord = new FileNameRecord(entry.Value, 0);
                result.Add(entry.Key, fileNameRecord);
            }

            return result;
        }
    }
}
