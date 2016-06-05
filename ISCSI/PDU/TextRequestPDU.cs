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
    public class TextRequestPDU : ISCSIPDU
    {
        public bool Continue;
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint CmdSN;
        public uint ExpStatSN;

        public string Text;

        public TextRequestPDU() : base()
        {
            OpCode = ISCSIOpCodeName.TextRequest;
        }

        public TextRequestPDU(byte[] buffer) : base(buffer)
        {
            Continue = (OpCodeSpecificHeader[0] & 0x40) != 0;

            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);

            TargetTransferTag = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);

            Text = ASCIIEncoding.ASCII.GetString(Data);
        }

        public override byte[] GetBytes()
        {
            if (Continue)
            {
                OpCodeSpecificHeader[0] |= 0x40;
            }

            LUNOrOpCodeSpecific = LUN.GetBytes();
            
            Array.Copy(BigEndianConverter.GetBytes(TargetTransferTag), 0, OpCodeSpecific, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(CmdSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpStatSN), 0, OpCodeSpecific, 8, 4);

            Data = ASCIIEncoding.ASCII.GetBytes(Text);

            return base.GetBytes();
        }
    }
}
