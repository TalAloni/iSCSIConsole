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

        public ReadyToTransferPDU(byte[] buffer) : base(buffer)
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

            Array.Copy(BigEndianConverter.GetBytes(TargetTransferTag), 0, OpCodeSpecific, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(StatSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpCmdSN), 0, OpCodeSpecific, 8, 4);
            Array.Copy(BigEndianConverter.GetBytes(MaxCmdSN), 0, OpCodeSpecific, 12, 4);
            Array.Copy(BigEndianConverter.GetBytes(R2TSN), 0, OpCodeSpecific, 16, 4);
            Array.Copy(BigEndianConverter.GetBytes(BufferOffset), 0, OpCodeSpecific, 20, 4);
            Array.Copy(BigEndianConverter.GetBytes(DesiredDataTransferLength), 0, OpCodeSpecific, 24, 4);

            return base.GetBytes();
        }
    }
}
