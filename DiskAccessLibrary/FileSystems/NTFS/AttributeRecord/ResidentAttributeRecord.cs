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
    public class ResidentAttributeRecord : AttributeRecord
    {
        public const int HeaderLength = 0x18;

        public byte[] Data;
        public byte IndexedFlag;

        public ResidentAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            uint dataLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
            ushort dataOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            IndexedFlag = ByteReader.ReadByte(buffer, offset + 0x16);

            if (dataOffset + dataLength > this.StoredRecordLength)
            {
                throw new InvalidDataException("Corrupt attribute, data outside of attribute record");
            }

            Data = ByteReader.ReadBytes(buffer, offset + dataOffset, (int)dataLength);
        }

        public override byte[] GetBytes(int bytesPerCluster)
        {
            uint length = this.RecordLength;
            byte[] buffer = new byte[length];
            WriteHeader(buffer, HeaderLength);
            uint dataLength = (uint)Data.Length;
            ushort dataOffset = (ushort)(HeaderLength + Name.Length * 2);

            LittleEndianWriter.WriteUInt32(buffer, 0x10, dataLength);
            LittleEndianWriter.WriteUInt16(buffer, 0x14, dataOffset);
            ByteWriter.WriteByte(buffer, 0x16, IndexedFlag);
            ByteWriter.WriteBytes(buffer, dataOffset, Data);

            return buffer;
        }

        public override byte[] GetData(NTFSVolume volume)
        {
            return Data;
        }

        /// <summary>
        /// When reading attributes, they may contain additional padding,
        /// so we should use StoredRecordLength to advance the buffer position instead.
        /// </summary>
        public override uint RecordLength
        {
            get 
            {
                uint length = (uint)(AttributeRecord.AttributeRecordHeaderLength + 8 + Name.Length * 2 + Data.Length);
                // Each record is aligned to 8-byte boundary
                length = (uint)Math.Ceiling((double)length / 8) * 8;
                return length;
            }
        }
    }
}
