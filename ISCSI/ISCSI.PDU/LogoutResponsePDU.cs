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
    public class LogoutResponsePDU : ISCSIPDU
    {
        public LogoutResponse Response;

        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public ushort TimeToWait;
        public ushort TimeToRetain;

        public LogoutResponsePDU() : base()
        {
            OpCode = ISCSIOpCodeName.LogoutResponse;
            Final = true;
        }

        public LogoutResponsePDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            Response = (LogoutResponse)OpCodeSpecificHeader[1];
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);

            TimeToWait = BigEndianConverter.ToUInt16(OpCodeSpecific, 20);
            TimeToRetain = BigEndianConverter.ToUInt16(OpCodeSpecific, 22);
        }

        public override byte[] GetBytes()
        {
            OpCodeSpecificHeader[1] = (byte)Response;

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, StatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 12, MaxCmdSN);
            BigEndianWriter.WriteUInt16(OpCodeSpecific, 20, TimeToWait);
            BigEndianWriter.WriteUInt16(OpCodeSpecific, 22, TimeToRetain);

            return base.GetBytes();
        }
    }
}
