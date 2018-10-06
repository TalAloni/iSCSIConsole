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
    /// ATTRIBUTE_LIST_ENTRY
    /// </summary>
    public class AttributeListEntry
    {
        public const int HeaderLength = 0x1A; // Excluding name

        public AttributeType AttributeType;
        private ushort m_lengthOnDisk; // The size of this structure, plus the optional name buffer, in bytes
        // byte NameLength; // Number of characters
        // byte NameOffset;
        public long LowestVCN;  // Stored as unsigned, but is within the range of long
        public MftSegmentReference SegmentReference;
        public ushort Instance; // The instance within the FileRecordSegment
        public string AttributeName = String.Empty;

        public AttributeListEntry()
        {
        }

        public AttributeListEntry(byte[] buffer, int offset)
        {
            AttributeType = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            m_lengthOnDisk = LittleEndianConverter.ToUInt16(buffer, offset + 0x04);
            if (m_lengthOnDisk < AttributeListEntry.HeaderLength)
            {
                throw new InvalidDataException("Invalid attribute list entry, data length is less than the valid minimum");
            }
            else if (m_lengthOnDisk > buffer.Length - offset)
            {
                throw new InvalidDataException("Invalid attribute list entry, data length exceed list length");
            }
            byte nameLength = ByteReader.ReadByte(buffer, offset + 0x06);
            byte nameOffset = ByteReader.ReadByte(buffer, offset + 0x07);
            LowestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            SegmentReference = new MftSegmentReference(buffer, offset + 0x10);
            Instance = LittleEndianConverter.ToUInt16(buffer, offset + 0x18);
            if (nameLength > 0)
            {
                AttributeName = ByteReader.ReadUTF16String(buffer, offset + nameOffset, nameLength);
            }
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x00, (uint)AttributeType);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x04, (ushort)this.Length);
            ByteWriter.WriteByte(buffer, offset + 0x06, (byte)AttributeName.Length);
            ByteWriter.WriteByte(buffer, offset + 0x07, (byte)HeaderLength);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x08, (ulong)LowestVCN);
            SegmentReference.WriteBytes(buffer, offset + 0x10);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x18, Instance);
            ByteWriter.WriteUTF16String(buffer, offset + HeaderLength, AttributeName);
        }

        public int Length
        {
            get
            {
                // Entries are padded to align to 8 byte boundary
                int length = HeaderLength + AttributeName.Length * 2;
                return (int)Math.Ceiling((double)length / 8) * 8;
            }
        }

        public int LengthOnDisk
        {
            get
            {
                return m_lengthOnDisk;
            }
        }
    }
}
