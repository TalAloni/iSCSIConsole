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
    public class SCSICommandPDU : ISCSIPDU
    {
        public bool Read;
        public bool Write;
        public byte TaskAttributes;
        public LUNStructure LUN;
        public uint ExpectedDataTransferLength; // in bytes (for the whole operation and not just this command)
        public uint CmdSN;
        public uint ExpStatSN;
        public SCSICommandDescriptorBlock CommandDescriptorBlock;

        public SCSICommandPDU() : base()
        {
            OpCode = ISCSIOpCodeName.SCSICommand;
        }

        public SCSICommandPDU(byte[] buffer) : this(buffer, true)
        {
        }

        public SCSICommandPDU(byte[] buffer, bool parseCDB) : base(buffer)
        {
            Read = (OpCodeSpecificHeader[0] & 0x40) != 0;
            Write = (OpCodeSpecificHeader[0] & 0x20) != 0;
            TaskAttributes = (byte)(OpCodeSpecificHeader[0] & 0x7);

            LUN = new LUNStructure(LUNOrOpCodeSpecific, 0);

            ExpectedDataTransferLength = BigEndianConverter.ToUInt32(OpCodeSpecific, 0);
            CmdSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 4);
            ExpStatSN = BigEndianConverter.ToUInt32(OpCodeSpecific, 8);

            if (parseCDB)
            {
                CommandDescriptorBlock = SCSICommandDescriptorBlock.FromBytes(OpCodeSpecific, 12);
            }
        }

        public override byte[] GetBytes()
        {
            if (Read)
            {
                OpCodeSpecificHeader[0] |= 0x40;
            }
            if (Write)
            {
                OpCodeSpecificHeader[0] |= 0x20;
            }
            OpCodeSpecificHeader[0] |= TaskAttributes;

            LUNOrOpCodeSpecific = LUN.GetBytes();

            Array.Copy(BigEndianConverter.GetBytes(ExpectedDataTransferLength), 0, OpCodeSpecific, 0, 4);
            Array.Copy(BigEndianConverter.GetBytes(CmdSN), 0, OpCodeSpecific, 4, 4);
            Array.Copy(BigEndianConverter.GetBytes(ExpStatSN), 0, OpCodeSpecific, 8, 4);
            byte[] cdbBytes = CommandDescriptorBlock.GetBytes();
            Array.Copy(cdbBytes, 0, OpCodeSpecific, 12, cdbBytes.Length);
            
            return base.GetBytes();
        }
    }
}
