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
    public class SCSIResponsePDU : ISCSIPDU
    {
        public bool BidirectionalReadResidualOverflow;
        public bool BidirectionalReadResidualUnderflow;
        public bool ResidualOverflow;
        public bool ResidualUnderflow;
        public byte Response;
        public SCSIStatusCodeName Status;
        public uint SNACKTag;
        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public uint ExpDataSN;
        public uint BidirectionalReadResidualCount;
        public uint ResidualCount;

        public SCSIResponsePDU() : base()
        {
            OpCode = (byte)ISCSIOpCodeName.SCSIResponse;
            Final = true;
        }

        public SCSIResponsePDU(byte[] buffer) : base(buffer)
        {
            BidirectionalReadResidualOverflow = (OpCodeSpecificHeader[0] & 0x10) != 0;
            BidirectionalReadResidualUnderflow = (OpCodeSpecificHeader[0] & 0x08) != 0;
            ResidualOverflow = (OpCodeSpecificHeader[0] & 0x04) != 0;
            ResidualUnderflow = (OpCodeSpecificHeader[0] & 0x02) != 0;
            Response = OpCodeSpecificHeader[1];
            Status = (SCSIStatusCodeName)OpCodeSpecificHeader[2];

            SNACKTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);
            ExpDataSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 16);
            BidirectionalReadResidualCount = BigEndianConverter.ToUInt32(OpCodeSpecific, 20);
            ResidualCount = BigEndianConverter.ToUInt32(OpCodeSpecific, 24);
        }

        public override byte[] GetBytes()
        {
            if (BidirectionalReadResidualOverflow)
            {
                OpCodeSpecificHeader[0] |= 0x10;
            }
            if (BidirectionalReadResidualUnderflow)
            {
                OpCodeSpecificHeader[0] |= 0x08;
            }
            if (ResidualOverflow)
            {
                OpCodeSpecificHeader[0] |= 0x04;
            }
            if (ResidualUnderflow)
            {
                OpCodeSpecificHeader[0] |= 0x02;
            }
            OpCodeSpecificHeader[1] = Response;
            OpCodeSpecificHeader[2] = (byte)Status;

            Array.Copy(BigEndianConverter.GetBytes(SNACKTag), 0, OpCodeSpecific, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(StatSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpCmdSN), 0, OpCodeSpecific, 8, 4);
            Array.Copy(BigEndianConverter.GetBytes(MaxCmdSN), 0, OpCodeSpecific, 12, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpDataSN), 0, OpCodeSpecific, 16, 4);
            Array.Copy(BigEndianConverter.GetBytes(BidirectionalReadResidualCount), 0, OpCodeSpecific, 20, 4);
            Array.Copy(BigEndianConverter.GetBytes(ResidualCount), 0, OpCodeSpecific, 24, 4);

            return base.GetBytes();
        }
    }
}
