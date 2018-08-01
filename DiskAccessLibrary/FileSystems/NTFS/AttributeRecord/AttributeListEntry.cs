/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class AttributeListEntry
    {
        public const int HeaderLength = 0x1A; // Excluding name

        public AttributeType AttributeType;
        public ushort Length; // The size of this structure, plus the optional name buffer, in bytes
        public byte NameLength;  // number of bytes
        public byte NameOffset;
        public ulong LowestVCN;
        public MftSegmentReference SegmentReference;
        public ushort AttributeID;
        public string AttributeName = String.Empty;

        public AttributeListEntry(byte[] buffer, int offset)
        {
            AttributeType = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            Length = LittleEndianConverter.ToUInt16(buffer, offset + 0x04);
            if (Length < AttributeListEntry.HeaderLength)
            {
                throw new InvalidDataException("Invalid attribute list entry, data length is less than the valid minimum");
            }
            else if (Length > buffer.Length - offset)
            {
                throw new InvalidDataException("Invalid attribute list entry, data length exceed list length");
            }
            NameLength = buffer[offset + 0x06];
            NameOffset = buffer[offset + 0x07];
            LowestVCN = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            SegmentReference = new MftSegmentReference(buffer, offset + 0x10);
            AttributeID = LittleEndianConverter.ToUInt16(buffer, offset + 0x18);
            if (NameLength > 0)
            {
                AttributeName = UnicodeEncoding.Unicode.GetString(buffer, offset + NameOffset, NameLength);
            }
        }
    }
}
