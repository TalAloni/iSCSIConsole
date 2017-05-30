/* Copyright (C) 2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace SCSI
{
    public class ModeParameterHeader10
    {
        public const int Length = 8;

        public ushort ModeDataLength; // Excluding this field
        public byte MediumType;
        public bool WP;               // Write Protect, indicates that the medium is write-protected
        public bool DPOFUA;         // DPO and FUA support
        public bool LongLBA;
        public ushort BlockDescriptorLength;

        public ModeParameterHeader10()
        {
            ModeDataLength = 6;
        }

        public ModeParameterHeader10(byte[] buffer, int offset)
        {
            ModeDataLength = BigEndianConverter.ToUInt16(buffer, offset + 0);
            MediumType = ByteReader.ReadByte(buffer, offset + 2);
            WP = ((buffer[offset + 3] & 0x80) != 0);
            DPOFUA = ((buffer[offset + 3] & 0x10) != 0);
            LongLBA = ((buffer[offset + 4] & 0x01) != 0);
            BlockDescriptorLength = BigEndianConverter.ToUInt16(buffer, offset + 6);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            BigEndianWriter.WriteUInt16(buffer, 0, ModeDataLength);
            ByteWriter.WriteByte(buffer, 2, MediumType);
            if (WP)
            {
                buffer[3] |= 0x80;
            }
            if (DPOFUA)
            {
                buffer[3] |= 0x10;
            }
            if (LongLBA)
            {
                buffer[4] |= 0x01;
            }
            BigEndianWriter.WriteUInt16(buffer, 6, BlockDescriptorLength);
            return buffer;
        }
    }
}
