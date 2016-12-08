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

namespace ISCSI
{
    public class LogoutRequestPDU : ISCSIPDU
    {
        public LogoutReasonCode ReasonCode;
        
        public ushort CID;
        public uint CmdSN;
        public uint ExpStatSN;

        public LogoutRequestPDU() : base()
        {
            OpCode = ISCSIOpCodeName.LogoutRequest;
            Final = true;
        }

        public LogoutRequestPDU(byte[] buffer) : base(buffer)
        {
            ReasonCode = (LogoutReasonCode)(OpCodeSpecificHeader[0] & 0x7F);
            CID = BigEndianConverter.ToUInt16(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
        }

        public override byte[] GetBytes()
        {
            OpCodeSpecificHeader[0] = (byte)ReasonCode; // Final bit will be added by base.GetBytes()

            Array.Copy(BigEndianConverter.GetBytes(CID), 0, OpCodeSpecific, 0, 2);
            Array.Copy(BigEndianConverter.GetBytes(CmdSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpStatSN), 0, OpCodeSpecific, 8, 4);

            return base.GetBytes();
        }
    }
}
