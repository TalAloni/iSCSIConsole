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
    public class LoginRequestPDU : ISCSIPDU
    {
        public bool Transit;
        public bool Continue;
        public byte CurrentStage; // 0..3
        public byte NextStage;    // 0..3

        public byte VersionMax;
        public byte VersionMin;

        public ulong ISID;
        public ushort TSIH;
        public ushort CID; // ConnectionID
        public uint CmdSN;
        public uint ExpStatSN;

        public KeyValuePairList<string, string> LoginParameters = new KeyValuePairList<string,string>(); // in text request format

        public LoginRequestPDU() : base()
        {
            OpCode = (byte)ISCSIOpCodeName.LoginRequest;
            ImmediateDelivery = true;
        }

        public LoginRequestPDU(byte[] buffer) : base(buffer)
        {
            Transit = Final; // the Transit bit replaces the Final bit
            Continue = (OpCodeSpecificHeader[0] & 0x40) != 0;
            CurrentStage = (byte)((OpCodeSpecificHeader[0] & 0x0C) >> 2);
            NextStage = (byte)(OpCodeSpecificHeader[0] & 0x03);

            VersionMax = OpCodeSpecificHeader[1];
            VersionMin = OpCodeSpecificHeader[2];

            ISID = BigEndianConverter.ToUInt32(LUNOrOpCodeSpecific, 0) << 16 | BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 4);
            TSIH = BigEndianConverter.ToUInt16(LUNOrOpCodeSpecific, 6);

            CID = BigEndianConverter.ToUInt16(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);

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
            OpCodeSpecificHeader[0] |= NextStage;

            OpCodeSpecificHeader[1] = VersionMax;
            OpCodeSpecificHeader[2] = VersionMin;

            Array.Copy(BigEndianConverter.GetBytes(ISID), 2, LUNOrOpCodeSpecific, 0, 6);
            Array.Copy(BigEndianConverter.GetBytes(TSIH), 0, LUNOrOpCodeSpecific, 6, 2);

            Array.Copy(BigEndianConverter.GetBytes(CID), 0, OpCodeSpecific, 0, 2);
            Array.Copy(BigEndianConverter.GetBytes(CmdSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpStatSN), 0, OpCodeSpecific, 8, 4);

            string parametersString = KeyValuePairUtils.GetNullDelimitedKeyValuePair(LoginParameters);
            Data = ASCIIEncoding.ASCII.GetBytes(parametersString);

            return base.GetBytes();
        }
    }
}
