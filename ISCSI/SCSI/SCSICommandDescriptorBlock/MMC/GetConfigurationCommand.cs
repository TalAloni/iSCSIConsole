using System;
using Utilities;

namespace SCSI
{
    internal class GetConfigurationCommand : SCSICommandDescriptorBlock
    {
        public const int PacketLength = 12;
        // Request Type
        public byte RT;
        public ushort StartingFeatureNumber;
        public short AllocationLength;

        public GetConfigurationCommand()
        {
            OpCode = SCSIOpCodeName.GetConfiguration;
        }

        public GetConfigurationCommand(byte[] buffer, int offset)
        {
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            RT = (byte)(buffer[offset +1] & 0x03);
            StartingFeatureNumber = BigEndianConverter.ToUInt16(buffer, offset + 2);
            AllocationLength = BigEndianConverter.ToInt16(buffer, offset +7);
            TransferLength = (uint)AllocationLength;
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[PacketLength];
            buffer[0] = (byte)OpCode;
            buffer[1] = RT;
            BigEndianWriter.WriteUInt16(buffer, 2, StartingFeatureNumber);
            BigEndianWriter.WriteInt16(buffer, 7, AllocationLength);

            return buffer;
        }
    }
}
