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
    public class LongLBAModeParameterBlockDescriptor
    {
        public const int Length = 16;

        public ulong NumberOfBlocks;
        public uint Reserved;
        public uint LogicalBlockLength;

        public LongLBAModeParameterBlockDescriptor()
        { 
        }

        public LongLBAModeParameterBlockDescriptor(byte[] buffer, int offset)
        {
            NumberOfBlocks = BigEndianConverter.ToUInt64(buffer, offset + 0);
            Reserved = BigEndianConverter.ToUInt32(buffer, offset + 8);
            LogicalBlockLength = BigEndianConverter.ToUInt32(buffer, offset + 12);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            BigEndianWriter.WriteUInt64(buffer, 0, NumberOfBlocks);
            BigEndianWriter.WriteUInt32(buffer, 8, Reserved);
            BigEndianWriter.WriteUInt32(buffer, 12, LogicalBlockLength);
            return buffer;
        }
    }
}
