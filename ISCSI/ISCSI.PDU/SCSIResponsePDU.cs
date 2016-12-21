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
    public class SCSIResponsePDU : ISCSIPDU
    {
        public bool BidirectionalReadResidualOverflow;
        public bool BidirectionalReadResidualUnderflow;
        public bool ResidualOverflow;
        public bool ResidualUnderflow;
        public ISCSIResponseName Response;
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
            OpCode = ISCSIOpCodeName.SCSIResponse;
            Final = true;
        }

        public SCSIResponsePDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            BidirectionalReadResidualOverflow = (OpCodeSpecificHeader[0] & 0x10) != 0;
            BidirectionalReadResidualUnderflow = (OpCodeSpecificHeader[0] & 0x08) != 0;
            ResidualOverflow = (OpCodeSpecificHeader[0] & 0x04) != 0;
            ResidualUnderflow = (OpCodeSpecificHeader[0] & 0x02) != 0;
            Response = (ISCSIResponseName)OpCodeSpecificHeader[1];
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
            OpCodeSpecificHeader[1] = (byte)Response;
            OpCodeSpecificHeader[2] = (byte)Status;

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 0, SNACKTag);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, StatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 12, MaxCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 16, ExpDataSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 20, BidirectionalReadResidualCount);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 24, ResidualCount);

            return base.GetBytes();
        }
    }
}
