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
    public enum IndexEntryFlags : ushort
    {
        PointsToSubnode = 0x01,
        LastEntryInNode = 0x02,
    }

    public class IndexNodeEntry // intermediate node entry
    {
        public MftSegmentReference SegmentReference; // 0 for self reference
        //ushort RecordLength;
        //ushort KeyLength;
        public IndexEntryFlags Flags;
        // 2 zero bytes (padding)
        public byte[] Key;
        public long SubnodeVCN; // if PointsToSubnode flag is set, stored as ulong but can be represented using long

        public IndexNodeEntry(byte[] buffer, ref int offset)
        {
            SegmentReference = new MftSegmentReference(buffer, offset + 0x00);
            ushort recordLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x08);
            ushort keyLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x0A);
            Flags = (IndexEntryFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x0C);
            Key = ByteReader.ReadBytes(buffer, offset + 0x10, keyLength);
            if (PointsToSubnode)
            {
                // key is padded to align to 8 byte boundary
                int keyLengthWithPadding = (int)Math.Ceiling((double)keyLength / 8) * 8;
                SubnodeVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10 + keyLengthWithPadding);
            }
            offset += recordLength;
        }

        public bool IsLastEntry
        {
            get
            {
                return ((Flags & IndexEntryFlags.LastEntryInNode) > 0);
            }
        }

        public bool PointsToSubnode
        {
            get
            {
                return ((Flags & IndexEntryFlags.PointsToSubnode) > 0);
            }
        }
    }
}
