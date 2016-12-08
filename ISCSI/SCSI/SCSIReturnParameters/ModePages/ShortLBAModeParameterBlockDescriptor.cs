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
    public class ShortLBAModeParameterBlockDescriptor
    {
        public uint LogicalBlockLength;

        public ShortLBAModeParameterBlockDescriptor()
        { 
        }

        public ShortLBAModeParameterBlockDescriptor(byte[] buffer, int offset)
        { 
            byte[] temp = new byte[4];
            Array.Copy(buffer, offset + 5, temp, 1, 3);
            LogicalBlockLength = BigEndianConverter.ToUInt32(temp, 0);
        }

        public byte[] GetBytes()
        { 
            byte[] buffer = new byte[8];
            Array.Copy(BigEndianConverter.GetBytes(LogicalBlockLength), 1, buffer, 5, 3);
            return buffer;
        }

        public int Length
        {
            get
            {
                return 4;
            }
        }
    }
}
