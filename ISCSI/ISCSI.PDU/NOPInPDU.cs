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
    // NOP-Out = Sent back from the target to the initiator (in response to a NOP-In PDU)
    public class NOPInPDU : ISCSIPDU
    {
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;

        public NOPInPDU()
        {
            OpCode = ISCSIOpCodeName.NOPIn;
            Final = true;
        }

        public NOPInPDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);

            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);
        }

        public override byte[] GetBytes()
        {
            LUNOrOpCodeSpecific = LUN.GetBytes();

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 0, TargetTransferTag);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, StatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 12, MaxCmdSN);

            return base.GetBytes();
        }
    }
}
