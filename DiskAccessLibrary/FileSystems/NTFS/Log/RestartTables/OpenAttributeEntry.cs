/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// OPEN_ATTRIBUTE_ENTRY
    /// Note: v0.0 is used in both NTFS v1.2 and NTFS 3.0+, however, some of the fields have been repurposed or made obsolete.
    /// </summary>
    /// <remarks>
    /// NTFS v1.2:
    /// Offset 0x04: PointerToAttributeName (UInt32)
    /// Offset 0x18: DirtyPagesSeen (BOOLEAN)
    /// Offset 0x19: AttributeNamePresent (BOOLEAN)
    /// Offset 0x20: AttributeName (UNICODE_STRING, 8 bytes)
    /// </remarks>
    public class OpenAttributeEntry : RestartTableEntry
    {
        public const int LengthV0 = 0x2C;
        public const int LengthV1 = 0x28;

        private const int FileReferenceOffsetV0 = 0x08;
        private const int LsnOfOpenRecordOffsetV0 = 0x10;
        private const int DirtyPagesSeenOffsetV0 = 0x18;
        private const int AttributeTypeCodeOffsetV0 = 0x1C;
        private const int BytesPerIndexBufferOffsetV0 = 0x28;
        private const int BytesPerIndexBufferOffsetV1 = 0x04;
        private const int AttributeTypeCodeOffsetV1 = 0x08;
        private const int DirtyPagesSeenOffsetV1 = 0x0C;
        private const int FileReferenceOffsetV1 = 0x10;
        private const int LsnOfOpenRecordOffsetV1 = 0x18;
        
        private uint m_majorVersion;

        // uint AllocatedOrNextFree;
        /// <summary>Self-reference (Offset of the attribute in the open attribute table)</summary>
        public uint AttributeOffset; // Used only by v0.0 on NTFS v3.0+.
        public MftSegmentReference FileReference;
        /// <summary>This is the LSN of the client record preceding the OpenNonResidentAttribute record</summary>
        public ulong LsnOfOpenRecord;
        /// <summary>Indicates that a DirtyPageEntry is referring to this entry</summary>
        public bool DirtyPagesSeen;       // Used by (v0.0 on) NTFS v1.2 and by v1.0 (on NTFS v3.0+), NOT used by v0.0 on NTFS v3.0+.
        public bool AttributeNamePresent; // NTFS v1.2 only
        // 2 reserved bytes
        public AttributeType AttributeTypeCode;
        public ulong PointerToAttributeName;
        public uint BytesPerIndexBuffer;

        public OpenAttributeEntry(uint majorVersion)
        {
            m_majorVersion = majorVersion;
        }

        public OpenAttributeEntry(byte[] buffer, int offset, uint majorVersion)
        {
            m_majorVersion = majorVersion;

            int fileReferenceOffset = (m_majorVersion == 0) ? FileReferenceOffsetV0 : FileReferenceOffsetV1;
            int lsnOfOpenRecordOffset = (m_majorVersion == 0) ? LsnOfOpenRecordOffsetV0 : LsnOfOpenRecordOffsetV1;
            int dirtyPagesSeenOffset = (m_majorVersion == 0) ? DirtyPagesSeenOffsetV0 : DirtyPagesSeenOffsetV1;
            int attributeTypeCodeOffset = (m_majorVersion == 0) ? AttributeTypeCodeOffsetV0 : AttributeTypeCodeOffsetV1;
            int bytesPerIndexBufferOffset = (m_majorVersion == 0) ? BytesPerIndexBufferOffsetV0 : BytesPerIndexBufferOffsetV1;

            AllocatedOrNextFree = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            if (majorVersion == 0)
            {
                AttributeOffset = LittleEndianConverter.ToUInt32(buffer, offset + 0x04);
            }
            FileReference = new MftSegmentReference(buffer, offset + fileReferenceOffset);
            LsnOfOpenRecord = LittleEndianConverter.ToUInt64(buffer, offset + lsnOfOpenRecordOffset);
            DirtyPagesSeen = Convert.ToBoolean(ByteReader.ReadByte(buffer, offset + dirtyPagesSeenOffset));
            if (m_majorVersion == 0)
            {
                // This field exists in NTFS v1.2, will be false in NTFS v3.0+
                AttributeNamePresent = Convert.ToBoolean(ByteReader.ReadByte(buffer, offset + 0x19));
            }
            AttributeTypeCode = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + attributeTypeCodeOffset);
            PointerToAttributeName = LittleEndianConverter.ToUInt64(buffer, offset + 0x20);

            if (AttributeTypeCode == AttributeType.IndexAllocation)
            {
                BytesPerIndexBuffer = LittleEndianConverter.ToUInt32(buffer, offset + bytesPerIndexBufferOffset);
            }
        }

        public override void WriteBytes(byte[] buffer, int offset)
        {
            int fileReferenceOffset = (m_majorVersion == 0) ? FileReferenceOffsetV0 : FileReferenceOffsetV1;
            int lsnOfOpenRecordOffset = (m_majorVersion == 0) ? LsnOfOpenRecordOffsetV0 : LsnOfOpenRecordOffsetV1;
            int dirtyPagesSeenOffset = (m_majorVersion == 0) ? DirtyPagesSeenOffsetV0 : DirtyPagesSeenOffsetV1;
            int attributeTypeCodeOffset = (m_majorVersion == 0) ? AttributeTypeCodeOffsetV0 : AttributeTypeCodeOffsetV1;
            int bytesPerIndexBufferOffset = (m_majorVersion == 0) ? BytesPerIndexBufferOffsetV0 : BytesPerIndexBufferOffsetV1;
            
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x00, AllocatedOrNextFree);
            if (m_majorVersion == 0)
            {
                LittleEndianWriter.WriteUInt32(buffer, offset + 0x04, AttributeOffset);
            }
            FileReference.WriteBytes(buffer, offset + fileReferenceOffset);
            LittleEndianWriter.WriteUInt64(buffer, offset + lsnOfOpenRecordOffset, LsnOfOpenRecord);
            ByteWriter.WriteByte(buffer, offset + dirtyPagesSeenOffset, Convert.ToByte(DirtyPagesSeen));
            if (m_majorVersion == 0)
            {
                // This field exists in NTFS v1.2, should be set to be false in NTFS v3.0+
                ByteWriter.WriteByte(buffer, offset + 0x19, Convert.ToByte(AttributeNamePresent));
            }
            LittleEndianWriter.WriteUInt32(buffer, offset + attributeTypeCodeOffset, (uint)AttributeTypeCode);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x20, PointerToAttributeName);

            if (AttributeTypeCode == AttributeType.IndexAllocation)
            {
                LittleEndianWriter.WriteUInt32(buffer, offset + bytesPerIndexBufferOffset, BytesPerIndexBuffer);
            }
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[this.Length];
            WriteBytes(buffer, 0);
            return buffer;
        }

        public override int Length
        {
            get
            {
                if (m_majorVersion == 0)
                {
                    return LengthV0;
                }
                else
                {
                    return LengthV1;
                }
            }
        }
    }
}
