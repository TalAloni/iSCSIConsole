using System;
using System.Runtime.InteropServices;

namespace SCSI.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public class SCSI_BUS_DATA
    {
        public byte NumberOfLogicalUnits;
        public byte InitiatorBusId;
        public uint InquiryDataOffset;

        public SCSI_BUS_DATA()
        {
        }
    }
}
