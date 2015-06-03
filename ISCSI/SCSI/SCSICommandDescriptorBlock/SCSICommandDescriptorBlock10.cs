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
    // 10-byte command
    public class SCSICommandDescriptorBlock10 : SCSICommandDescriptorBlock
    {
        public SCSICommandDescriptorBlock10() : base()
        { 
        }

        public SCSICommandDescriptorBlock10(byte[] buffer, int offset)
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            MiscellaneousCDBInformationHeader = (byte)((buffer[offset + 1] & 0xE0) >> 5);
            ServiceAction = (byte)((buffer[offset + 1] & 0x1F));

            LogicalBlockAddress = BigEndianConverter.ToUInt32(buffer, offset + 2);
            MiscellaneousCDBinformation = buffer[offset + 6];
            TransferLength = BigEndianConverter.ToUInt16(buffer, offset + 7);
            Control = buffer[offset + 9];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[10];
            buffer[0] = (byte)OpCode;
            buffer[1] |= (byte)(MiscellaneousCDBInformationHeader << 5);
            buffer[1] |= (byte)(ServiceAction & 0x1F);
            Array.Copy(BigEndianConverter.GetBytes(LogicalBlockAddress), 0, buffer, 2, 4);
            buffer[6] = MiscellaneousCDBinformation;
            Array.Copy(BigEndianConverter.GetBytes((ushort)TransferLength), 0, buffer, 7, 2);
            buffer[9] = Control;
            return buffer;
        }
    }
}
