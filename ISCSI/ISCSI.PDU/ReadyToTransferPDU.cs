/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using SCSI;
using Utilities;

namespace ISCSI
{
    public class ReadyToTransferPDU : ISCSIPDU
    {
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public uint R2TSN;
        public uint BufferOffset;
        public uint DesiredDataTransferLength;

        public ReadyToTransferPDU()
        {
            OpCode = ISCSIOpCodeName.ReadyToTransfer;
            Final = true;
        }

        public ReadyToTransferPDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);
            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);
            R2TSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 16);
            BufferOffset = BigEndianConverter.ToUInt32(OpCodeSpecific, 20);
            DesiredDataTransferLength = BigEndianConverter.ToUInt32(OpCodeSpecific, 24);
        }

        public override byte[] GetBytes()
        {
            LUNOrOpCodeSpecific = LUN.GetBytes();

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 0, TargetTransferTag);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, StatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 12, MaxCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 16, R2TSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 20, BufferOffset);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 24, DesiredDataTransferLength);

            return base.GetBytes();
        }
    }
}
