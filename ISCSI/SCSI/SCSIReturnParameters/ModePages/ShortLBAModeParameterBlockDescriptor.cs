/* Copyright (C) 2012-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class ShortLBAModeParameterBlockDescriptor
    {
        public const int Length = 8;

        public uint NumberOfBlocks;
        public byte Reserved;
        public uint LogicalBlockLength; // 3 bytes

        public ShortLBAModeParameterBlockDescriptor()
        { 
        }

        public ShortLBAModeParameterBlockDescriptor(byte[] buffer, int offset)
        {
            NumberOfBlocks = BigEndianConverter.ToUInt32(buffer, offset + 0);
            Reserved = ByteReader.ReadByte(buffer, offset + 4);
            LogicalBlockLength = BigEndianReader.ReadUInt24(buffer, offset + 5);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            BigEndianWriter.WriteUInt32(buffer, 0, NumberOfBlocks);
            ByteWriter.WriteByte(buffer, 4, Reserved);
            BigEndianWriter.WriteUInt24(buffer, 5, LogicalBlockLength);
            return buffer;
        }
    }
}
