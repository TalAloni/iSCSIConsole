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
    public class RejectPDU : ISCSIPDU
    {
        public RejectReason Reason;
        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public uint DataSN_R2TSN;

        public RejectPDU() : base()
        {
            OpCode = ISCSIOpCodeName.Reject;
            Final = true;
            InitiatorTaskTag = 0xFFFFFFFF;
        }

        public RejectPDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            Reason = (RejectReason)OpCodeSpecificHeader[1];
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);
            DataSN_R2TSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 16);
        }

        public override byte[] GetBytes()
        {
            OpCodeSpecificHeader[1] = (byte)Reason;

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, StatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 12, MaxCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 16, DataSN_R2TSN);

            return base.GetBytes();
        }
    }
}
