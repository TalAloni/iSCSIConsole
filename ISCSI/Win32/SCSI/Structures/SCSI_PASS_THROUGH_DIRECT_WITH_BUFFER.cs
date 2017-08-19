using System;
using System.Runtime.InteropServices;

namespace SCSI.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public class SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER
    {
        public const int SenseBufferLength = 32;

        public SCSI_PASS_THROUGH_DIRECT Spt = new SCSI_PASS_THROUGH_DIRECT();

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SenseBufferLength)]
        public byte[] Sense;

        public SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER()
        {
            Sense = new byte[SenseBufferLength];
        }
    }
}
