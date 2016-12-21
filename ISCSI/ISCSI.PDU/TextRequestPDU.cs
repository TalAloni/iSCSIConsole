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
    public class TextRequestPDU : ISCSIPDU
    {
        public bool Continue;
        public LUNStructure LUN;
        public uint TargetTransferTag;
        public uint CmdSN;
        public uint ExpStatSN;

        public string Text = String.Empty;

        public TextRequestPDU() : base()
        {
            OpCode = ISCSIOpCodeName.TextRequest;
        }

        public TextRequestPDU(byte[] buffer, int offset) : base(buffer, offset)
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

            BigEndianWriter.WriteUInt32(OpCodeSpecific, 0, TargetTransferTag);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 4, CmdSN);
            BigEndianWriter.WriteUInt32(OpCodeSpecific, 8, ExpStatSN);

            Data = ASCIIEncoding.ASCII.GetBytes(Text);

            return base.GetBytes();
        }

        public KeyValuePairList<string, string> TextParameters
        {
            set
            {
                Text = KeyValuePairUtils.ToNullDelimitedString(value);
            }
        }
    }
}
