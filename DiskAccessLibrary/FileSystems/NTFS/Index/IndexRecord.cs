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
    public class IndexRecord
    {
        public const string ValidSignature = "INDX";

        /* Index Record Header start*/
        /* Start of MULTI_SECTOR_HEADER */
        public string Signature = ValidSignature;
        // private ushort UpdateSequenceArrayOffset;
        // private ushort UpdateSequenceArraySize; // number of (2 byte) words
        /* End of MULTI_SECTOR_HEADER */
        public ulong LogFileSequenceNumber;
        public ulong RecordVCN;
        /* Header */
        /* Index Header start */
        public uint EntriesOffset; // relative to Index record header start offset
        public uint IndexLength;  // including the Index record header
        public uint AllocatedLength; // including the Index record header
        public bool HasChildren; // level?
        // 3 zero bytes (padding)
        /* Index Header end */
        public ushort UpdateSequenceNumber;
        /* Index Record Header end*/

        public List<IndexNodeEntry> IndexEntries = new List<IndexNodeEntry>();
        public List<FileNameIndexEntry> FileNameEntries = new List<FileNameIndexEntry>();

        public IndexRecord(byte[] buffer, int offset)
        {
            Signature = ByteReader.ReadAnsiString(buffer, offset + 0x00, 4);
            if (Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid INDX record signature");
            }
            ushort updateSequenceArrayOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x04);
            ushort updateSequenceArraySize = LittleEndianConverter.ToUInt16(buffer, offset + 0x06);
            LogFileSequenceNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            RecordVCN = LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            EntriesOffset = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            IndexLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x1C);
            AllocatedLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x20);
            HasChildren = ByteReader.ReadByte(buffer, offset + 0x24) > 0;

            int position = offset + updateSequenceArrayOffset;
            UpdateSequenceNumber = LittleEndianConverter.ToUInt16(buffer, position);
            position += 2;
            List<byte[]> updateSequenceReplacementData = new List<byte[]>();
            for (int index = 0; index < updateSequenceArraySize - 1; index++)
            {
                byte[] endOfSectorBytes = new byte[2];
                endOfSectorBytes[0] = buffer[position + 0];
                endOfSectorBytes[1] = buffer[position + 1];
                updateSequenceReplacementData.Add(endOfSectorBytes);
                position += 2;
            }

            MultiSectorHelper.DecodeSegmentBuffer(buffer, offset, UpdateSequenceNumber, updateSequenceReplacementData);

            position = 0x18 + (int)EntriesOffset;
            if (HasChildren)
            {
                IndexNode node = new IndexNode(buffer, position);
                IndexEntries = node.Entries;
            }
            else
            {
                FileNameIndexLeafNode leaf = new FileNameIndexLeafNode(buffer, position);
                FileNameEntries = leaf.Entries;
            }
        }
    }
}
