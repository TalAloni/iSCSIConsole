/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// IndexRoot attribute is always resident.
    /// </remarks>
    public class IndexRootRecord : ResidentAttributeRecord
    {
        public const int IndexHeaderOffset = 0x10;
 
        public AttributeType IndexedAttributeType;
        public CollationRule CollationRule;
        public uint BytesPerIndexRecord;
        public byte BlocksPerIndexRecord; // In units of clusters when BytesPerIndexRecord >= Volume.BytesPerCluster, otherwise in units of 512 byte blocks.
        // 3 zero bytes
        private IndexHeader m_indexHeader;
        public List<IndexEntry> IndexEntries;

        public IndexRootRecord(string name, ushort instance) : base(AttributeType.IndexRoot, name, instance)
        {
            m_indexHeader = new IndexHeader();
            IndexEntries = new List<IndexEntry>();
        }
        
        public IndexRootRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            IndexedAttributeType = (AttributeType)LittleEndianConverter.ToUInt32(this.Data, 0x00);
            CollationRule = (CollationRule)LittleEndianConverter.ToUInt32(this.Data, 0x04);
            BytesPerIndexRecord = LittleEndianConverter.ToUInt32(this.Data, 0x08);
            BlocksPerIndexRecord = ByteReader.ReadByte(this.Data, 0x0C);
            // 3 zero bytes (padding to 8-byte boundary)
            m_indexHeader = new IndexHeader(this.Data, 0x10);

            int entriesOffset = IndexHeaderOffset + (int)m_indexHeader.EntriesOffset;
            IndexEntries = IndexEntry.ReadIndexEntries(this.Data, entriesOffset);
        }

        public override byte[] GetBytes()
        {
            this.Data = new byte[this.DataLength];
            m_indexHeader.EntriesOffset = IndexHeader.Length;
            m_indexHeader.TotalLength = (uint)this.Data.Length - IndexHeaderOffset;
            m_indexHeader.AllocatedLength = (uint)this.Data.Length - IndexHeaderOffset;

            LittleEndianWriter.WriteUInt32(this.Data, 0x00, (uint)IndexedAttributeType);
            LittleEndianWriter.WriteUInt32(this.Data, 0x04, (uint)CollationRule);
            LittleEndianWriter.WriteUInt32(this.Data, 0x08, (uint)BytesPerIndexRecord);
            ByteWriter.WriteByte(this.Data, 0x0C, BlocksPerIndexRecord);
            m_indexHeader.WriteBytes(this.Data, 0x10);
            IndexEntry.WriteIndexEntries(this.Data, IndexHeaderOffset + IndexHeader.Length, IndexEntries);

            return base.GetBytes();
        }

        public override ulong DataLength
        {
            get
            {
                return (ulong)(IndexHeaderOffset + IndexHeader.Length + IndexEntry.GetLength(IndexEntries));
            }
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
