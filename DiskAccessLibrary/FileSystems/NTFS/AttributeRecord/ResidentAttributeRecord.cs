/* Copyright (C) 2014-2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// ATTRIBUTE_RECORD_HEADER: https://docs.microsoft.com/en-us/windows/desktop/DevNotes/attribute-record-header
    /// </summary>
    public class ResidentAttributeRecord : AttributeRecord
    {
        public const int HeaderLength = 0x18;

        public byte[] Data;
        private ResidentForm m_residentForm;
        private byte m_reserved;

        public ResidentAttributeRecord(AttributeType attributeType, string name) : base(attributeType, name, true)
        {
            Data = new byte[0];
        }

        public ResidentAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            uint dataLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
            ushort dataOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            m_residentForm = (ResidentForm)ByteReader.ReadByte(buffer, offset + 0x16);
            m_reserved = ByteReader.ReadByte(buffer, offset + 0x17);

            if (dataOffset + dataLength > this.RecordLengthOnDisk)
            {
                throw new InvalidDataException("Corrupt resident attribute, data outside of attribute record");
            }

            if (dataOffset % 8 > 0)
            {
                throw new InvalidDataException("Corrupt resident attribute, data not aligned to 8-byte boundary");
            }

            Data = ByteReader.ReadBytes(buffer, offset + dataOffset, (int)dataLength);
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[this.RecordLength];
            WriteHeader(buffer, HeaderLength);
            uint dataLength = (uint)Data.Length;
            ushort dataOffset = (ushort)(Math.Ceiling((double)(HeaderLength + Name.Length * 2) / 8) * 8);

            LittleEndianWriter.WriteUInt32(buffer, 0x10, dataLength);
            LittleEndianWriter.WriteUInt16(buffer, 0x14, dataOffset);
            ByteWriter.WriteByte(buffer, 0x16, (byte)m_residentForm);
            ByteWriter.WriteByte(buffer, 0x17, m_reserved);
            ByteWriter.WriteBytes(buffer, dataOffset, Data);

            return buffer;
        }

        public override AttributeRecord Clone()
        {
            ResidentAttributeRecord clone = (ResidentAttributeRecord)this.MemberwiseClone();
            clone.Data = (byte[])this.Data.Clone();
            return clone;
        }

        public override ulong DataLength
        {
            get
            {
                return (ulong)Data.Length;
            }
        }

        /// <summary>
        /// Each attribute record must be aligned to 8-byte boundary, so RecordLength must be a multiple of 8.
        /// When reading attributes, they may contain additional padding,
        /// so we should use RecordLengthOnDisk to advance the buffer position instead.
        /// </summary>
        public override int RecordLength
        {
            get 
            {
                return GetRecordLength(Name.Length, (int)this.DataLength);
            }
        }

        public bool IsIndexed
        {
            get
            {
                return (m_residentForm & ResidentForm.Indexed) > 0;
            }
            set
            {
                if (value)
                {
                    m_residentForm |= ResidentForm.Indexed;
                }
                else
                {
                    m_residentForm &= ~ResidentForm.Indexed;
                }
            }
        }

        /// <summary>
        /// Each attribute record must be aligned to 8-byte boundary, so RecordLength must be a multiple of 8.
        /// </summary>
        public static int GetRecordLength(int nameLength, int dataLength)
        {
            // Data must be aligned to 8-byte boundary
            int length = (int)Math.Ceiling((double)(HeaderLength + nameLength * 2) / 8) * 8;
            // Each record must be aligned to 8-byte boundary
            length += (int)Math.Ceiling((double)dataLength / 8) * 8;
            return length;
        }

        public static ResidentAttributeRecord Create(AttributeType type, string name)
        {
            switch (type)
            {
                case AttributeType.StandardInformation:
                    return new StandardInformationRecord(name);
                case AttributeType.FileName:
                    return new FileNameAttributeRecord(name);
                case AttributeType.VolumeName:
                    return new VolumeNameRecord(name);
                case AttributeType.VolumeInformation:
                    return new VolumeInformationRecord(name);
                case AttributeType.IndexRoot:
                    return new IndexRootRecord(name);
                case AttributeType.IndexAllocation:
                    throw new ArgumentException("IndexAllocation attribute is always non-resident");
                default:
                    return new ResidentAttributeRecord(type, name);
            }
        }
    }
}
