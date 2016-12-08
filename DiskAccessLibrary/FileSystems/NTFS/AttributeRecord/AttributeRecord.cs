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
    /// <summary>
    /// ATTRIBUTE_RECORD_HEADER: http://msdn.microsoft.com/en-us/library/bb470039%28v=vs.85%29.aspx
    /// </summary>
    public abstract class AttributeRecord
    {
        public const int AttributeRecordHeaderLength = 16; // The part that is common to both resident and non-resident attributes

        /* Start of header */
        private AttributeType m_type;
        public uint StoredRecordLength;
        private byte m_nonResidentFlag; // a.k.a. FormCode
        private byte m_nameLength;  // number of characters
        // ushort NameOffset;
        public ushort Flags;
        public ushort AttributeID; // a.k.a. Instance
        /* End of header */
        public string Name = String.Empty;

        protected AttributeRecord(byte[] buffer, int offset)
        {
            m_type = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            StoredRecordLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x04);
            m_nonResidentFlag = buffer[offset + 0x08];
            m_nameLength = buffer[offset + 0x09];
            ushort nameOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x0A);
            Flags = LittleEndianConverter.ToUInt16(buffer, offset + 0x0C);
            AttributeID = LittleEndianConverter.ToUInt16(buffer, offset + 0x0E);
            if (m_nameLength > 0)
            {
                Name = UnicodeEncoding.Unicode.GetString(buffer, offset + nameOffset, m_nameLength * 2);
            }
        }

        public abstract byte[] GetBytes(int bytesPerCluster);

        public void WriteHeader(byte[] buffer, ushort nameOffset)
        {
            m_nameLength = (byte)(Name.Length * 2);
            
            buffer[0x00] = (byte)m_type;
            LittleEndianWriter.WriteUInt32(buffer, 0x04, this.RecordLength);
            buffer[0x08] = m_nonResidentFlag;
            buffer[0x09] = m_nameLength;
            LittleEndianWriter.WriteUInt16(buffer, 0x0A, nameOffset);
            LittleEndianWriter.WriteUInt16(buffer, 0x0C, Flags);
            LittleEndianWriter.WriteUInt16(buffer, 0x0E, AttributeID);

            if (m_nameLength > 0)
            {
                Array.Copy(UnicodeEncoding.Unicode.GetBytes(Name), 0, buffer, nameOffset, m_nameLength);
            }
        }

        public abstract byte[] GetData(NTFSVolume volume);

        public AttributeType AttributeType
        {
            get
            {
                return m_type;
            }
        }

        public bool IsResidentRecord
        {
            get
            {
                return (m_nonResidentFlag == 0x00);
            }
        }

        public static AttributeRecord FromBytes(byte[] buffer, int offset)
        {
            byte nonResidentFlag = buffer[offset + 0x08];
            AttributeType attributeType = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            if (nonResidentFlag == 0x00) // resident
            {
                if (attributeType == AttributeType.StandardInformation)
                {
                    return new StandardInformationRecord(buffer, offset);
                }
                else if (attributeType == AttributeType.FileName)
                {
                    return new FileNameAttributeRecord(buffer, offset);
                }
                else if (attributeType == AttributeType.VolumeInformation)
                {
                    return new VolumeInformationRecord(buffer, offset);
                }
                else if (attributeType == AttributeType.IndexRoot)
                {
                    return new IndexRootRecord(buffer, offset);
                }
                else
                {
                    return new ResidentAttributeRecord(buffer, offset);
                }
            }
            else // non-resident
            {
                if (attributeType == AttributeType.IndexAllocation)
                {
                    return new IndexAllocationRecord(buffer, offset);
                }
                else
                {
                    return new NonResidentAttributeRecord(buffer, offset);
                }
            }
        }

        /// <summary>
        /// When reading attributes, they may contain additional padding,
        /// so we should use StoredRecordLength to advance the buffer position instead.
        /// </summary>
        public abstract uint RecordLength
        {
            get;
        }
    }
}
