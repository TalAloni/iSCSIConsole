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
    // NOP-Out = Sent from the initiator to the target
    public class NOPOutPDU : ISCSIPDU
    {
        public ulong LUN;
        public uint TargetTransferTag;
        public uint CmdSN;
        public uint ExpStatSN;
        
        public NOPOutPDU()
        {
            OpCode = (byte)ISCSIOpCodeName.NOPOut;
            Final = true;
        }

        public NOPOutPDU(byte[] buffer) : base(buffer)
        {
            LUN = BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 0);

            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
        }

        public override byte[] GetBytes()
        {
            LUNOrOpCodeSpecific = BigEndianConverter.GetBytes(LUN);

            Array.Copy(BigEndianConverter.GetBytes(TargetTransferTag), 0, OpCodeSpecific, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(CmdSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpStatSN), 0, OpCodeSpecific, 8, 4);

            return base.GetBytes();
        }
    }
}
