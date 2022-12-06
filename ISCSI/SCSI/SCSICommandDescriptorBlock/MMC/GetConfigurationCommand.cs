using System;
using Utilities;

namespace SCSI
{
    internal class GetConfigurationCommand : SCSICommandDescriptorBlock
    {
        public const int PacketLength = 12;
        // Request Type
        public byte RT;
        // Starting Feature Number
        public ushort SFN;
        public short AllocationLength;

        public GetConfigurationCommand()
        {
            OpCode = SCSIOpCodeName.GetConfiguration;
        }

        public GetConfigurationCommand(byte[] buffer, int offset)
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            RT = (byte)(buffer[offset +1] & 0x03);
            SFN = BigEndianConverter.ToUInt16(buffer, offset + 2);
            AllocationLength = BigEndianConverter.ToInt16(buffer, offset +7);
            TransferLength = (uint)AllocationLength;
        }

        public override byte[] GetBytes() => throw new NotImplementedException();
    }
}
