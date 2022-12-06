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
        }

        public override byte[] GetBytes() => throw new NotImplementedException();
    }
}
