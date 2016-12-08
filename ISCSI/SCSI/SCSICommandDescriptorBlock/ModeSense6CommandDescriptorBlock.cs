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

namespace SCSI
{
    public class ModeSense6CommandDescriptorBlock : SCSICommandDescriptorBlock
    {
        public bool DBD; // Disable block descriptors
        public byte PC; // Page Control
        public ModePageCodeName PageCode;
        public byte SubpageCode;

        public ModeSense6CommandDescriptorBlock() : base()
        {
            OpCode = SCSIOpCodeName.ModeSense6;
        }

        public ModeSense6CommandDescriptorBlock(byte[] buffer, int offset) : base()
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            DBD = (buffer[offset + 1] & 0x08) != 0;
            PC = (byte)(buffer[offset + 2] >> 6);
            PageCode = (ModePageCodeName)(buffer[offset + 2] & 0x3F);
            SubpageCode = buffer[offset + 3];
            AllocationLength = buffer[offset + 4];
            Control = buffer[offset + 5];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[6];
            buffer[0] = (byte)OpCode;
            if (DBD)
            {
                buffer[1] |= 0x08;
            }
            buffer[2] |= (byte)(PC << 6);
            buffer[2] |= (byte)((byte)PageCode & 0x3F);
            buffer[3] = SubpageCode;
            buffer[4] = AllocationLength;
            buffer[5] = Control;
            return buffer;
        }

        public byte AllocationLength
        {
            get
            {
                return (byte)TransferLength;
            }
            set
            {
                TransferLength = value;
            }
        }
    }
}
