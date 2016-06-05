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
        public KeyValuePairList<string, string> LoginParameters = new KeyValuePairList<string,string>(); // in text request format

        public LoginResponsePDU() : base()
        {
            OpCode = ISCSIOpCodeName.LoginResponse;
        }

        public LoginResponsePDU(byte[] buffer) : base(buffer)
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
            Array.Copy(BigEndianConverter.GetBytes(ISID), 2, LUNOrOpCodeSpecific, 0, 6);
            Array.Copy(BigEndianConverter.GetBytes(TSIH), 0, LUNOrOpCodeSpecific, 6, 2);

            Array.Copy(BigEndianConverter.GetBytes(StatSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpCmdSN), 0, OpCodeSpecific, 8, 4);
            Array.Copy(BigEndianConverter.GetBytes(MaxCmdSN), 0, OpCodeSpecific, 12, 4);
            Array.Copy(BigEndianConverter.GetBytes((ushort)Status), 0, OpCodeSpecific, 16, 2);

            string parametersString = KeyValuePairUtils.ToNullDelimitedString(LoginParameters);
            Data = ASCIIEncoding.ASCII.GetBytes(parametersString);
            
            return base.GetBytes();
        }
    }
}
