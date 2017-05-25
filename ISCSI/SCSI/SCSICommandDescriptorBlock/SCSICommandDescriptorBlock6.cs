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
    /// <summary>
    /// 6-byte SCSI CDB
    /// </summary>
    public class SCSICommandDescriptorBlock6 : SCSICommandDescriptorBlock
    {
        public SCSICommandDescriptorBlock6(SCSIOpCodeName opCode) : base()
        {
            this.OpCode = opCode;
        }

        public SCSICommandDescriptorBlock6(byte[] buffer, int offset) : base()
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            MiscellaneousCDBInformationHeader = (byte)((buffer[offset + 1] & 0xE0) >> 5);

            uint temp = BigEndianReader.ReadUInt24(buffer, offset + 1);
            LogicalBlockAddress = temp & 0x1FFFFF;
            TransferLength = buffer[offset + 4];
            Control = buffer[offset + 5];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[6];
            buffer[0] = (byte)OpCode;
            buffer[1] |= (byte)(MiscellaneousCDBInformationHeader << 5);
            buffer[1] |= (byte)((LogicalBlockAddress >> 16) & 0x1F);
            buffer[2] = (byte)((LogicalBlockAddress >> 8) & 0xFF);
            buffer[3] = (byte)((LogicalBlockAddress >> 0) & 0xFF);
            buffer[4] = (byte)TransferLength;
            buffer[5] = Control;
            return buffer;
        }
    }
}
