/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class FileRecordSegment
    {
        public const string ValidSignature = "FILE";
        public const int EndMarkerLength = 4;
        public const int NTFS30UpdateSequenceArrayOffset = 0x2A; // NTFS 3.0 and earlier (up to Windows 2000)
        public const int NTFS31UpdateSequenceArrayOffset = 0x30; // NTFS 3.1 and later   (XP and later)

        [Flags]
        public enum FileRecordFlags : ushort
        {
            None = 0x0000,
            InUse = 0x0001,
            IsDirectory = 0x0002,
            IsMetaFile = 0x0004,
            HasViewIndex = 0x0008,
        }

        /* Start of header */
        /* Start of MULTI_SECTOR_HEADER */
        public string Signature = ValidSignature;
        // ushort UpdateSequenceArrayOffset;
        // ushort UpdateSequenceArraySize; // number of (2 byte) words
        /* End of MULTI_SECTOR_HEADER */
        public ulong LogFileSequenceNumber;
        public ushort SequenceNumber; // This value is incremented each time that a file record segment is freed
        public ushort HardLinkCount;
        // ushort FirstAttributeOffset;
        private FileRecordFlags m_flags;
        // uint SegmentRealSize;
        // uint SegmentAllocatedSize;
        private ulong BaseFileRecordSegmentNumber; // If this is the base file record, the value is 0
        public ushort NextAttributeId; // Starting from 0
        // 2 zeros - padding
        public uint MftSegmentNumberXP; // Self-reference (on XP+)

        public ushort UpdateSequenceNumber; // a.k.a. USN
        /* End of header */

        private long m_mftSegmentNumber; // We use our own segment number to support NTFS 3.0 (note that MftSegmentNumberXP is UInt32, which is another reason to avoid it)

        private List<AttributeRecord> m_immediateAttributes = new List<AttributeRecord>(); // Attribute records that are stored in the base file record

        public FileRecordSegment(byte[] buffer, int bytesPerSector, long segmentNumber) : this(buffer, 0, bytesPerSector, segmentNumber)
        { 
        }

        public FileRecordSegment(byte[] buffer, int offset, int bytesPerSector, long segmentNumber)
        {
            Signature = ByteReader.ReadAnsiString(buffer, offset + 0x00, 4);

            ushort updateSequenceArrayOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x04);
            ushort updateSequenceArraySize = LittleEndianConverter.ToUInt16(buffer, offset + 0x06);
            LogFileSequenceNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            SequenceNumber = LittleEndianConverter.ToUInt16(buffer, offset + 0x10);
            HardLinkCount = LittleEndianConverter.ToUInt16(buffer, offset + 0x12);
            ushort firstAttributeOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            m_flags = (FileRecordFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x16);
            uint segmentRealSize = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            uint segmentAllocatedSize = LittleEndianConverter.ToUInt32(buffer, offset + 0x1C);

            BaseFileRecordSegmentNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x20); 
            NextAttributeId = LittleEndianConverter.ToUInt16(buffer, offset + 0x28);
            // 2 zeros - padding
            MftSegmentNumberXP = LittleEndianConverter.ToUInt32(buffer, offset + 0x2C);

            // There is an UpdateSequenceNumber for the FileRecordSegment,
            // and an entry in the UpdateSequenceArray for each sector of the record
            // The last two bytes of each sector contains this entry for integrity-check purposes
            int position = offset + updateSequenceArrayOffset;
            UpdateSequenceNumber = LittleEndianConverter.ToUInt16(buffer, position);
            position += 2;
            // This stores the data that was supposed to be placed at the end of each sector, and was replaced with an UpdateSequenceNumber
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

            // read attributes
            position = offset + firstAttributeOffset;
            while (!IsEndMarker(buffer, position))
            {
                AttributeRecord attribute = AttributeRecord.FromBytes(buffer, position);
                
                m_immediateAttributes.Add(attribute);
                position += (int)attribute.StoredRecordLength;
                if (position > buffer.Length)
                {
                    throw new InvalidDataException("Improper attribute length");
                }
            }

            m_mftSegmentNumber = segmentNumber;
        }

        /// <param name="segmentLength">This refers to the maximum length of FileRecord as defined in the Volume's BootRecord</param>
        public byte[] GetBytes(int segmentLength, int bytesPerCluster, ushort minorNTFSVersion)
        {
            int strideCount = segmentLength / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);

            ushort updateSequenceArrayOffset;
            if (minorNTFSVersion == 0)
            {
                updateSequenceArrayOffset = NTFS30UpdateSequenceArrayOffset;
            }
            else
            {
                updateSequenceArrayOffset = NTFS31UpdateSequenceArrayOffset;
            }

            ushort firstAttributeOffset = GetFirstAttributeOffset(segmentLength, minorNTFSVersion);

            byte[] buffer = new byte[segmentLength];
            ByteWriter.WriteAnsiString(buffer, 0, Signature, 4);
            LittleEndianWriter.WriteUInt16(buffer, 0x04, updateSequenceArrayOffset);
            LittleEndianWriter.WriteUInt16(buffer, 0x06, updateSequenceArraySize);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, LogFileSequenceNumber);
            LittleEndianWriter.WriteUInt16(buffer, 0x10, SequenceNumber);
            LittleEndianWriter.WriteUInt16(buffer, 0x12, HardLinkCount);
            LittleEndianWriter.WriteUInt16(buffer, 0x14, firstAttributeOffset);
            LittleEndianWriter.WriteUInt16(buffer, 0x16, (ushort)m_flags);

            LittleEndianWriter.WriteInt32(buffer, 0x1C, segmentLength);
            LittleEndianWriter.WriteUInt64(buffer, 0x20, BaseFileRecordSegmentNumber);
            LittleEndianWriter.WriteUInt16(buffer, 0x28, NextAttributeId);
            if (minorNTFSVersion == 1)
            {
                LittleEndianWriter.WriteUInt32(buffer, 0x2C, MftSegmentNumberXP);
            }

            // write attributes
            int position = firstAttributeOffset;
            foreach (AttributeRecord attribute in m_immediateAttributes)
            {
                byte[] attributeBytes = attribute.GetBytes(bytesPerCluster);
                ByteWriter.WriteBytes(buffer, position, attributeBytes);
                position += attributeBytes.Length;
            }

            byte[] marker = GetEndMarker();
            ByteWriter.WriteBytes(buffer, position, marker);
            position += marker.Length;
            position += 4; // record (length) is aligned to 8-byte boundary

            uint segmentRealSize = (uint)position;
            LittleEndianWriter.WriteUInt32(buffer, 0x18, segmentRealSize);

            // write UpdateSequenceNumber and UpdateSequenceReplacementData
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.EncodeSegmentBuffer(buffer, 0, segmentLength, UpdateSequenceNumber);
            position = updateSequenceArrayOffset;
            LittleEndianWriter.WriteUInt16(buffer, position, UpdateSequenceNumber);
            position += 2;
            foreach (byte[] endOfSectorBytes in updateSequenceReplacementData)
            {
                ByteWriter.WriteBytes(buffer, position, endOfSectorBytes);
                position += 2;
            }

            return buffer;
        }

        public AttributeRecord GetImmediateAttributeRecord(AttributeType type)
        {
            foreach (AttributeRecord attribute in m_immediateAttributes)
            {
                if (attribute.AttributeType == type)
                {
                    return attribute;
                }
            }

            return null;
        }

        /// <summary>
        /// Indicates that the file / directory wasn't deleted
        /// </summary>
        public bool IsInUse
        {
            get
            {
                return (m_flags & FileRecordFlags.InUse) != 0;
            }
            set
            {
                m_flags &= ~FileRecordFlags.InUse;
            }
        }

        public bool IsDirectory
        {
            get
            {
                return (m_flags & FileRecordFlags.IsDirectory) != 0;
                //return (GetAttributeRecord(AttributeType.IndexRoot) != null);
            }
        }
        
        public List<AttributeRecord> ImmediateAttributes
        {
            get
            {
                return m_immediateAttributes;
            }
        }

        public bool IsBaseFileRecord
        {
            get
            {
                // If this is the base file record, the value is 0
                // http://msdn.microsoft.com/en-us/library/bb470124%28v=vs.85%29.aspx
                return (BaseFileRecordSegmentNumber == 0);
            }
        }

        public bool HasAttributeList
        {
            get
            {
                AttributeRecord attributeList = GetImmediateAttributeRecord(AttributeType.AttributeList);
                return (attributeList != null);
            }
        }

        /*
        public uint RecordRealSize
        {
            get
            {
                return m_recordRealSize;
            }
        }*/

        public override bool Equals(object obj)
        {
            if (obj is FileRecordSegment)
            {
                return ((FileRecordSegment)obj).MftSegmentNumber == MftSegmentNumber;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return MftSegmentNumber.GetHashCode();
        }

        public long MftSegmentNumber
        {
            get
            {
                return m_mftSegmentNumber;
            }
        }

        public static bool IsEndMarker(byte[] buffer, int offset)
        {
            uint type = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            return (type == 0xFFFFFFFF);
        }

        /// <summary>
        /// Get file record end marker
        /// </summary>
        public static byte[] GetEndMarker()
        {
            byte[] buffer = new byte[4];
            LittleEndianWriter.WriteUInt32(buffer, 0, 0xFFFFFFFF);
            return buffer;
        }

        public static ushort GetFirstAttributeOffset(int segmentLength, ushort minorNTFSVersion)
        {
            int strideCount = segmentLength / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);

            ushort updateSequenceArrayOffset;
            if (minorNTFSVersion == 0)
            {
                updateSequenceArrayOffset = NTFS30UpdateSequenceArrayOffset;
            }
            else
            {
                updateSequenceArrayOffset = NTFS31UpdateSequenceArrayOffset;
            }

            // aligned to 8 byte boundary
            // Note: I had an issue with 4 byte boundary under Windows 7 using disk with 2048 bytes per sector.
            //       Windows used an 8 byte boundary.
            ushort firstAttributeOffset = (ushort)(Math.Ceiling((double)(updateSequenceArrayOffset + updateSequenceArraySize * 2) / 8) * 8);
            return firstAttributeOffset;
        }

        public static bool ContainsFileRecordSegment(byte[] recordBytes)
        {
            return ContainsFileRecordSegment(recordBytes, 0);
        }

        public static bool ContainsFileRecordSegment(byte[] recordBytes, int offset)
        {
            string fileSignature = ByteReader.ReadAnsiString(recordBytes, offset, 4);
            return (fileSignature == ValidSignature);
        }

        public static bool ContainsMftSegmentNumber(List<FileRecordSegment> list, long mftSegmentNumber)
        {
            foreach (FileRecordSegment segment in list)
            {
                if (segment.MftSegmentNumber == mftSegmentNumber)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
