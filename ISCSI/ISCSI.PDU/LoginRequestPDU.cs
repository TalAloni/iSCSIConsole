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
    public class LoginRequestPDU : ISCSIPDU
    {
        public bool Transit; // indicates that the initiator is ready to transit to the next stage
        public bool Continue; // indicates that the text (set of key=value pairs) in this Login Request is not complete
        public byte CurrentStage; // 0..3
        public byte NextStage;    // 0..3

        public byte VersionMax;
        public byte VersionMin;

        public ulong ISID;
        public ushort TSIH;
        public ushort CID; // ConnectionID
        public uint CmdSN;
        public uint ExpStatSN;

        public string LoginParametersText = String.Empty; // A key=value pair can start in one PDU and continue on the next

        public LoginRequestPDU() : base()
        {
            OpCode = ISCSIOpCodeName.LoginRequest;
            ImmediateDelivery = true;
        }

        public LoginRequestPDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            Transit = Final; // the Transit bit replaces the Final bit
            Continue = (OpCodeSpecificHeader[0] & 0x40) != 0;
            CurrentStage = (byte)((OpCodeSpecificHeader[0] & 0x0C) >> 2);
            NextStage = (byte)(OpCodeSpecificHeader[0] & 0x03);

            VersionMax = OpCodeSpecificHeader[1];
            VersionMin = OpCodeSpecificHeader[2];

            ISID = (ulong)BigEndianConverter.ToUInt32(LUNOrOpCodeSpecific, 0) << 16 | BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 4);
            TSIH = BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 6);

            CID = BigEndianConverter.ToUInt16(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);

            LoginParametersText = Encoding.ASCII.GetString(Data);
        }

        public override byte[] GetBytes()
        {
            if (Transit)
            {
                Final = true; // the Transit bit replaces the Final bit
            }
            if (Continue)
            {
                OpCodeSpecificHeader[0] |= 0x40;
            }
            OpCodeSpecificHeader[0] |= (byte)(CurrentStage << 2);
            OpCodeSpecificHeader[0] |= NextStage;

            OpCodeSpecificHeader[1] = VersionMax;
            OpCodeSpecificHeader[2] = VersionMin;

            BigEndianWriter.WriteUInt64(LUNOrOpCodeSpecific, 0, ISID << 16 | TSIH);

            BigEndianWriter.WriteUInt16(OpCodeSpecific, 0, CID);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, CmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpStatSN);

            Data = ASCIIEncoding.ASCII.GetBytes(LoginParametersText);

            return base.GetBytes();
        }

        public KeyValuePairList<string, string> LoginParameters
        {
            set
            {
                LoginParametersText = KeyValuePairUtils.ToNullDelimitedString(value);
            }
        }
    }
}
