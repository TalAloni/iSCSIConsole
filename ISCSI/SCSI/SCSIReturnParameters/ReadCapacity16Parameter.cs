/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class ReadCapacity16Parameter
    {
        public const int Length = 32;

        public ulong ReturnedLBA;        // the LBA of the last logical block on the direct-access block device
        public uint BlockLengthInBytes; // block size

        public ReadCapacity16Parameter()
        { 
        }

        public ReadCapacity16Parameter(byte[] buffer)
        {
            ReturnedLBA = BigEndianConverter.ToUInt64(buffer, 0);
            BlockLengthInBytes = BigEndianConverter.ToUInt32(buffer, 8);
        }

        public ReadCapacity16Parameter(long diskSize, uint blockSizeInBytes)
        {
            ReturnedLBA = (ulong)diskSize / blockSizeInBytes - 1; // zero-based LBA of the last logical block
            BlockLengthInBytes = blockSizeInBytes;
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            BigEndianWriter.WriteUInt64(buffer, 0, ReturnedLBA);
            BigEndianWriter.WriteUInt32(buffer, 8, BlockLengthInBytes);
            return buffer;
        }
    }
}
