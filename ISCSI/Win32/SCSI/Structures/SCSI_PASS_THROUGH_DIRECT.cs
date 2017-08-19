using System;
using System.Runtime.InteropServices;

namespace SCSI.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public class SCSI_PASS_THROUGH_DIRECT
    {
        public const int CdbBufferLength = 16;

        public ushort Length;
        public byte ScsiStatus;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte CdbLength;
        public byte SenseInfoLength;
        public byte DataIn;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public IntPtr DataBuffer;
        public uint SenseInfoOffset;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CdbBufferLength)]
        public byte[] Cdb;

        public SCSI_PASS_THROUGH_DIRECT()
        {
            Cdb = new byte[CdbBufferLength];
        }
    }
}
