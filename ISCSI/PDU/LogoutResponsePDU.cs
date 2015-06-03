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
    public class LogoutResponsePDU : ISCSIPDU
    {
        public byte Response;

        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public ushort TimeToWait;
        public ushort TimeToRetain;

        public LogoutResponsePDU() : base()
        {
            OpCode = (byte)ISCSIOpCodeName.LogoutResponse;
            Final = true;
        }

        public LogoutResponsePDU(byte[] buffer) : base(buffer)
        {
            Response = OpCodeSpecificHeader[1];
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);

            TimeToWait = BigEndianConverter.ToUInt16(OpCodeSpecific, 20);
            TimeToRetain = BigEndianConverter.ToUInt16(OpCodeSpecific, 22);
        }

        public override byte[] GetBytes()
        {
            OpCodeSpecificHeader[1] = Response;

            Array.Copy(BigEndianConverter.GetBytes(StatSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpCmdSN), 0, OpCodeSpecific, 8, 4);
            Array.Copy(BigEndianConverter.GetBytes(MaxCmdSN), 0, OpCodeSpecific, 12, 4);

            Array.Copy(BigEndianConverter.GetBytes(TimeToWait), 0, OpCodeSpecific, 20, 2);
            Array.Copy(BigEndianConverter.GetBytes(TimeToRetain), 0, OpCodeSpecific, 22, 2);

            return base.GetBytes();
        }
    }
}
