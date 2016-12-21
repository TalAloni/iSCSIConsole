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
    // Data-Out = Data sent to the target (WRITE operations)
    public class SCSIDataOutPDU : ISCSIPDU
    {
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint ExpStatSN;
        public uint DataSN;
        public uint BufferOffset;

        public SCSIDataOutPDU()
        {
            OpCode = ISCSIOpCodeName.SCSIDataOut;
        }

        public SCSIDataOutPDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);

            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            DataSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 16);
            BufferOffset = BigEndianConverter.ToUInt32(OpCodeSpecific, 20);
        }

        public override byte[] GetBytes()
        {
            LUNOrOpCodeSpecific = LUN.GetBytes();

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 0, TargetTransferTag);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpStatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 16, DataSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 20, BufferOffset);

            return base.GetBytes();
        }
    }
}
