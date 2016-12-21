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
    public class LoginResponsePDU : ISCSIPDU
    {
        public bool Transit;
        public bool Continue;
        public byte CurrentStage; // 0..3
        public byte NextStage;    // 0..3

        public byte VersionMax;
        public byte VersionActive;
        public ulong ISID;
        public ushort TSIH;

        public uint StatSN;
        public uint ExpCmdSN;
        public uint MaxCmdSN;
        public LoginResponseStatusName Status; // StatusClass & StatusDetail
        public string LoginParametersText = String.Empty; // A key=value pair can start in one PDU and continue on the next

        public LoginResponsePDU() : base()
        {
            OpCode = ISCSIOpCodeName.LoginResponse;
        }

        public LoginResponsePDU(byte[] buffer, int offset) : base(buffer, offset)
        {
            Transit = Final; // the Transit bit replaces the Final bit
            Continue = (OpCodeSpecificHeader[0] & 0x40) != 0;
            CurrentStage = (byte)((OpCodeSpecificHeader[0] & 0x0C) >> 2);
            NextStage = (byte)(OpCodeSpecificHeader[0] & 0x03);

            VersionMax = OpCodeSpecificHeader[1];
            VersionActive = OpCodeSpecificHeader[2];
            ISID = (ulong)BigEndianConverter.ToUInt32(LUNOrOpCodeSpecific, 0) << 16 | BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 4);
            TSIH = BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 6);

            StatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);
            MaxCmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 12);
            Status = (LoginResponseStatusName)BigEndianConverter.ToUInt16(OpCodeSpecific, 16);

            string parametersString = Encoding.ASCII.GetString(Data);
            LoginParameters = KeyValuePairUtils.GetKeyValuePairList(parametersString);
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
            OpCodeSpecificHeader[0] |= (byte)NextStage;

            OpCodeSpecificHeader[1] = VersionMax;
            OpCodeSpecificHeader[2] = VersionActive;
            BigEndianWriter.WriteUInt64(LUNOrOpCodeSpecific, 0, ISID << 16 | TSIH);

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, StatSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpCmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 12, MaxCmdSN);
            BigEndianWriter.WriteUInt16(OpCodeSpecific, 16, (ushort)Status);

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
