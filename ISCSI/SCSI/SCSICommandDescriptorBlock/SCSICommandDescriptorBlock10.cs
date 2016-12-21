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
    /// <summary>
    /// 10-byte SCSI CDB
    /// </summary>
    public class SCSICommandDescriptorBlock10 : SCSICommandDescriptorBlock
    {
        public SCSICommandDescriptorBlock10(SCSIOpCodeName opCode) : base()
        {
            this.OpCode = opCode;
        }

        public SCSICommandDescriptorBlock10(byte[] buffer, int offset) : base()
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            MiscellaneousCDBInformationHeader = (byte)((buffer[offset + 1] & 0xE0) >> 5);
            ServiceAction = (ServiceAction)((buffer[offset + 1] & 0x1F));

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
            buffer[1] |= (byte)((byte)ServiceAction & 0x1F);
            BigEndianWriter.WriteUInt32(buffer, 2, LogicalBlockAddress);
            buffer[6] = MiscellaneousCDBinformation;
            BigEndianWriter.WriteUInt16(buffer, 7, (ushort)TransferLength);
            buffer[9] = Control;
            return buffer;
        }
    }
}
