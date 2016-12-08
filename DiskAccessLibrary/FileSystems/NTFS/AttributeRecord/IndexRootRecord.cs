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
    [Flags]
    public enum IndexRootFlags : byte
    { 
        LargeIndex = 0x01, // denotes the presence of IndexAllocation record
    }

    // IndexRoot attribute is always resident
    public class IndexRootRecord : ResidentAttributeRecord
    {
        public const string FileNameIndexName = "$I30";

        /* Index root start */ 
        public AttributeType IndexedAttributeType; // FileName for directories
        public CollationRule CollationRule;
        public uint IndexAllocationEntryLength; // in bytes
        public byte ClustersPerIndexRecord;
        // 3 zero bytes
        /* Index root end */
        /* Index Header start */
        public uint EntriesOffset; // relative to Index Header start offset
        public uint IndexLength;  // including the Index Header
        public uint AllocatedLength;
        public IndexRootFlags IndexFlags;
        // 3 zero bytes
        /* Index Header end */

        public List<IndexNodeEntry> IndexEntries = new List<IndexNodeEntry>();
        public List<FileNameIndexEntry> FileNameEntries = new List<FileNameIndexEntry>();

        public IndexRootRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            IndexedAttributeType = (AttributeType)LittleEndianConverter.ToUInt32(this.Data, 0x00);
            CollationRule = (CollationRule)LittleEndianConverter.ToUInt32(this.Data, 0x04);
            IndexAllocationEntryLength = LittleEndianConverter.ToUInt32(this.Data, 0x08);
            ClustersPerIndexRecord = ByteReader.ReadByte(this.Data, 0x0C);
            // 3 zero bytes (padding to 8-byte boundary)
            EntriesOffset = LittleEndianConverter.ToUInt32(this.Data, 0x10);
            IndexLength = LittleEndianConverter.ToUInt32(this.Data, 0x14);
            AllocatedLength = LittleEndianConverter.ToUInt32(this.Data, 0x18);
            IndexFlags = (IndexRootFlags)ByteReader.ReadByte(this.Data, 0x1C);
            // 3 zero bytes (padding to 8-byte boundary)

            if (Name == FileNameIndexName)
            {
                int position = 0x10 + (int)EntriesOffset;
                if (IsLargeIndex)
                {
                    IndexNode node = new IndexNode(this.Data, position);
                    IndexEntries = node.Entries;
                }
                else
                {
                    FileNameIndexLeafNode leaf = new FileNameIndexLeafNode(this.Data, position);
                    FileNameEntries = leaf.Entries;
                }
            }
        }

        public KeyValuePairList<MftSegmentReference, FileNameRecord> GetSmallIndexEntries()
        {
            if (IsLargeIndex)
            {
                throw new ArgumentException("Not a small index");
            }

            KeyValuePairList<MftSegmentReference, FileNameRecord> result = new KeyValuePairList<MftSegmentReference, FileNameRecord>();

            foreach (FileNameIndexEntry entry in FileNameEntries)
            {
                result.Add(entry.FileReference, entry.Record);
            }
            return result;
        }

        public bool IsLargeIndex
        {
            get
            {
                return (IndexFlags & IndexRootFlags.LargeIndex) > 0;
            }
        }
    }
}
