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
    // Data-In = Data sent to the initiator (READ operations)
    public class SCSIDataInPDU : ISCSIPDU
    {
        public bool Acknowledge;
        public bool ResidualOverflow;
        public bool ResidualUnderflow;
        public bool StatusPresent; // indicate that the Command Status field contains status
        public SCSIStatusCodeName Status;
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public uint DataSN;
        public uint BufferOffset;
        public uint ResidualCount;

        public SCSIDataInPDU()
        {
            OpCode = ISCSIOpCodeName.SCSIDataIn;
        }

        public SCSIDataInPDU(byte[] buffer) : base(buffer)
        {
            Acknowledge = (OpCodeSpecificHeader[0] & 0x40) != 0;
            ResidualOverflow = (OpCodeSpecificHeader[0] & 0x04) != 0;
            ResidualUnderflow = (OpCodeSpecificHeader[0] & 0x02) != 0;
            StatusPresent = (OpCodeSpecificHeader[0] & 0x01) != 0;

            Status = (SCSIStatusCodeName)OpCodeSpecificHeader[2];

            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);

            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);
            DataSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 16);
            BufferOffset = BigEndianConverter.ToUInt32(OpCodeSpecific, 20);
            ResidualCount = BigEndianConverter.ToUInt32(OpCodeSpecific, 24);
        }

        public override byte[] GetBytes()
        {
            if (Acknowledge)
            {
                OpCodeSpecificHeader[0] |= 0x40;
            }
            if (ResidualOverflow)
            {
                OpCodeSpecificHeader[0] |= 0x04;
            }
            if (ResidualUnderflow)
            {
                OpCodeSpecificHeader[0] |= 0x02;
            }
            if (StatusPresent)
            {
                OpCodeSpecificHeader[0] |= 0x01;
                // If this bit is set to 1, the F bit MUST also be set to 1.
                Final = true;
            }

            OpCodeSpecificHeader[2] = (byte)Status;

            LUNOrOpCodeSpecific = LUN.GetBytes();

            Array.Copy(BigEndianConverter.GetBytes(TargetTransferTag), 0, OpCodeSpecific, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(StatSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpCmdSN), 0, OpCodeSpecific, 8, 4);
            Array.Copy(BigEndianConverter.GetBytes(MaxCmdSN), 0, OpCodeSpecific, 12, 4);
            Array.Copy(BigEndianConverter.GetBytes(DataSN), 0, OpCodeSpecific, 16, 4);
            Array.Copy(BigEndianConverter.GetBytes(BufferOffset), 0, OpCodeSpecific, 20, 4);
            Array.Copy(BigEndianConverter.GetBytes(ResidualCount), 0, OpCodeSpecific, 24, 4);

            return base.GetBytes();
        }
    }
}
