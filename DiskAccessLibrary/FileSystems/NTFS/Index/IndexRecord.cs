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
    /// <summary>
    /// INDEX_ALLOCATION_BUFFER
    /// </summary>
    public class IndexRecord
    {
        public const string ValidSignature = "INDX";
        public const int IndexHeaderOffset = 0x18;
        public const int UpdateSequenceArrayOffset = 0x28;
        public const int BytesPerIndexRecordBlock = 512;

        // MULTI_SECTOR_HEADER
        public ulong LogFileSequenceNumber;
        public long RecordVBN; // Stored as unsigned, but is within the range of long
        private IndexHeader m_indexHeader;
        public ushort UpdateSequenceNumber;
        // byte[] UpdateSequenceReplacementData
        // Padding to align to 8-byte boundary
        public List<IndexEntry> IndexEntries;

        public IndexRecord()
        {
            m_indexHeader = new IndexHeader();
            IndexEntries = new List<IndexEntry>();
        }

        public IndexRecord(byte[] buffer, int offset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            if (multiSectorHeader.Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid INDX record signature");
            }
            LogFileSequenceNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            RecordVBN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            m_indexHeader = new IndexHeader(buffer, offset + 0x18);
            UpdateSequenceNumber = LittleEndianConverter.ToUInt16(buffer, offset + multiSectorHeader.UpdateSequenceArrayOffset);

            int entriesOffset = 0x18 + (int)m_indexHeader.EntriesOffset;
            IndexEntries = IndexEntry.ReadIndexEntries(buffer, entriesOffset);
        }

        public byte[] GetBytes(int bytesPerIndexRecord, bool applyUsaProtection)
        {
            int strideCount = bytesPerIndexRecord / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(ValidSignature, UpdateSequenceArrayOffset, updateSequenceArraySize);

            int updateSequenceArrayPaddedLength = (int)Math.Ceiling((double)(updateSequenceArraySize * 2) / 8) * 8;

            m_indexHeader.EntriesOffset = (uint)(IndexHeader.Length + updateSequenceArrayPaddedLength);
            m_indexHeader.TotalLength = (uint)(IndexHeader.Length + updateSequenceArrayPaddedLength + IndexEntry.GetLength(IndexEntries));
            m_indexHeader.AllocatedLength = (uint)(bytesPerIndexRecord - IndexHeaderOffset);

            int length = applyUsaProtection ? bytesPerIndexRecord : GetNumberOfBytesInUse(bytesPerIndexRecord);
            byte[] buffer = new byte[length];
            multiSectorHeader.WriteBytes(buffer, 0x00);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, LogFileSequenceNumber);
            LittleEndianWriter.WriteUInt64(buffer, 0x10, (ulong)RecordVBN);
            m_indexHeader.WriteBytes(buffer, 0x18);
            LittleEndianWriter.WriteUInt16(buffer, UpdateSequenceArrayOffset, UpdateSequenceNumber);

            IndexEntry.WriteIndexEntries(buffer, UpdateSequenceArrayOffset + updateSequenceArrayPaddedLength, IndexEntries);

            if (applyUsaProtection)
            {
                MultiSectorHelper.ApplyUsaProtection(buffer, 0);
            }
            return buffer;
        }

        public int GetNumberOfBytesInUse(int bytesPerIndexRecord)
        {
            int strideCount = bytesPerIndexRecord / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            int updateSequenceArrayPaddedLength = (int)Math.Ceiling((double)(updateSequenceArraySize * 2) / 8) * 8;
            return IndexHeaderOffset + IndexHeader.Length + updateSequenceArrayPaddedLength + IndexEntry.GetLength(IndexEntries);
        }

        public bool DoesFit(int bytesPerIndexRecord)
        {
            int numberOfBytesInUse = GetNumberOfBytesInUse(bytesPerIndexRecord);
            return (numberOfBytesInUse <= bytesPerIndexRecord);
        }

        public bool IsParentNode
        {
            get
            {
                return m_indexHeader.IsParentNode;
            }
            set
            {
                m_indexHeader.IsParentNode = value;
            }
        }
    }
}
