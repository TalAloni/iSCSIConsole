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
    // 16-byte command
    public class SCSICommandDescriptorBlock16 : SCSICommandDescriptorBlock
    {
        public SCSICommandDescriptorBlock16() : base()
        { 
        }

        public SCSICommandDescriptorBlock16(byte[] buffer, int offset)
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            MiscellaneousCDBInformationHeader = (byte)((buffer[offset + 1] & 0xE0) >> 5);
            ServiceAction = (byte)((buffer[offset + 1] & 0x1F));

            LogicalBlockAddress = BigEndianConverter.ToUInt32(buffer, offset + 2);
            AdditionalCDBdata = BigEndianConverter.ToUInt32(buffer, offset + 6);
            TransferLength = BigEndianConverter.ToUInt32(buffer, offset + 10);
            MiscellaneousCDBinformation = buffer[offset + 14];
            Control = buffer[offset + 15];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[16];
            buffer[0] = (byte)OpCode;
            buffer[1] |= (byte)(MiscellaneousCDBInformationHeader << 5);
            buffer[1] |= (byte)(ServiceAction & 0x1F);
            BigEndianWriter.WriteUInt32(buffer, 2, LogicalBlockAddress);
            BigEndianWriter.WriteUInt32(buffer, 6, AdditionalCDBdata);
            BigEndianWriter.WriteUInt32(buffer, 10, TransferLength);
            buffer[14] = MiscellaneousCDBinformation;
            buffer[15] = Control;
            return buffer;
        }
    }
}
