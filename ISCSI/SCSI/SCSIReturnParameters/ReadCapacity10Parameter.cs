/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSI
{
    public class ReadCapacity10Parameter
    {
        public uint ReturnedLBA;        // the LBA of the last logical block on the direct-access block device
        public uint BlockLengthInBytes; // block size

        public ReadCapacity10Parameter()
        { 
        }

        public ReadCapacity10Parameter(long diskSize, uint blockSizeInBytes)
        {
            // If the number of logical blocks exceeds the maximum value that is able to be specified in the RETURNED LOGICAL BLOCK ADDRESS field,
            // the device server shall set the RETURNED LOGICAL BLOCK ADDRESS field to 0xFFFFFFFF
            long diskSizeLBA = diskSize / blockSizeInBytes - 1; // zero-based LBA of the last logical block
            if (diskSizeLBA <= UInt32.MaxValue)
            {
                ReturnedLBA = (uint)diskSizeLBA; 
            }
            else
            {
                ReturnedLBA = 0xFFFFFFFF;
            }
            
            BlockLengthInBytes = blockSizeInBytes;
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[8];
            Array.Copy(BigEndianConverter.GetBytes(ReturnedLBA), 0, buffer, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(BlockLengthInBytes), 0, buffer, 4, 4);
            return buffer;
        }
    }
}
