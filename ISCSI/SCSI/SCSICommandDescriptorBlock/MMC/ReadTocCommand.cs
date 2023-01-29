using System;
using Utilities;

namespace SCSI
{
    internal class ReadTocCommand : SCSICommandDescriptorBlock
    {
        public const int PacketLength = 12;
        public bool MSF;
        public byte Format;
        public byte TrackSessionNumber;
        public short AllocationLength;

        public ReadTocCommand()
        {
            OpCode = SCSIOpCodeName.ReadToc;
        }

        public ReadTocCommand(byte[] buffer, int offset)
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            MSF = (buffer[offset + 1] & 0x02) == 1;
            Format = (byte)(buffer[offset + 2] & 0xF);
            TrackSessionNumber = buffer[offset + 6];
            AllocationLength = BigEndianConverter.ToInt16(buffer, offset + 7);
            TransferLength = (uint)AllocationLength;
            Control = buffer[offset + 9];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[PacketLength];
            buffer[0] = (byte)OpCode;
            if (MSF)
            {
                buffer[1] |= 0x02;
            }
            buffer[2] |= (byte)(Format & 0xF);
            buffer[6] = TrackSessionNumber;
            BigEndianWriter.WriteInt16(buffer, 7, AllocationLength);
            buffer[9] = Control;

            return buffer;
        }
    }
}
