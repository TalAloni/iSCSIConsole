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
    // NOP-Out = Sent from the initiator to the target
    public class NOPOutPDU : ISCSIPDU
    {
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint CmdSN;
        public uint ExpStatSN;
        
        public NOPOutPDU()
        {
            OpCode = ISCSIOpCodeName.NOPOut;
            Final = true;
        }

        public NOPOutPDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);

            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
        }

        public override byte[] GetBytes()
        {
            LUNOrOpCodeSpecific = LUN.GetBytes();

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 0, TargetTransferTag);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, CmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpStatSN);

            return base.GetBytes();
        }
    }
}
