/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using Utilities;

namespace SCSI
{
    public class ModeSense10CommandDescriptorBlock : SCSICommandDescriptorBlock
    {
        public const int PacketLength = 10;

        public bool DBD; // Disable block descriptors
        public byte PC; // Page Control
        public ModePageCodeName PageCode;
        public byte SubpageCode;
        public bool LLBA;

        public ModeSense10CommandDescriptorBlock() : base()
        {
            OpCode = SCSIOpCodeName.ModeSense10;
        }

        public ModeSense10CommandDescriptorBlock(byte[] buffer, int offset) : base()
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            DBD = (buffer[offset + 1] & 0x08) != 0;
            PC = (byte)(buffer[offset + 2] >> 6);
            PageCode = (ModePageCodeName)(buffer[offset + 2] & 0x3F);
            SubpageCode = buffer[offset + 3];
            AllocationLength =  BigEndianConverter.ToInt16(buffer,offset + 7);
            Control = buffer[offset + 9];
        }

        public override byte[] GetBytes()
        {
            var buffer = new byte[PacketLength];
            buffer[0] = (byte)OpCode;
            if (DBD)
            {
                buffer[1] |= 0x08;
            }
            buffer[2] |= (byte)(PC << 6);
            buffer[2] |= (byte)((byte)PageCode & 0x3F);
            buffer[3] = SubpageCode;
            BigEndianWriter.WriteInt16(buffer, 7, AllocationLength);
            buffer[9] = Control;
            return buffer;
        }

        public short AllocationLength
        {
            get
            {
                return (short)TransferLength;
            }
            set
            {
                TransferLength = (uint)value;
            }
        }
    }
}
