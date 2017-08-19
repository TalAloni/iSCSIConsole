using System;
using System.Runtime.InteropServices;

namespace SCSI.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public class SCSI_INQUIRY_DATA
    {
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public bool DeviceClaimed; // Indicates that the device has been claimed by a class driver. 
        public uint InquiryDataLength;
        public uint NextInquiryDataOffset;
        public byte[] InquiryData;

        public SCSI_INQUIRY_DATA()
        {
        }

        public static SCSI_INQUIRY_DATA FromIntPtr(IntPtr ptr)
        {
            SCSI_INQUIRY_DATA inquiryData = new SCSI_INQUIRY_DATA();
            inquiryData.PathId = Marshal.ReadByte(ptr, 0);
            inquiryData.TargetId = Marshal.ReadByte(ptr, 1);
            inquiryData.Lun = Marshal.ReadByte(ptr, 2);
            inquiryData.DeviceClaimed = Convert.ToBoolean(Marshal.ReadByte(ptr, 3));
            inquiryData.InquiryDataLength = (uint)Marshal.ReadInt32(ptr, 4);
            inquiryData.NextInquiryDataOffset = (uint)Marshal.ReadInt32(ptr, 8);
            inquiryData.InquiryData = new byte[inquiryData.InquiryDataLength];
            int inquiryDataOffset = 12;
            IntPtr inquiryDataPtr = new IntPtr(ptr.ToInt64() + inquiryDataOffset);
            Marshal.Copy(inquiryDataPtr, inquiryData.InquiryData, 0, inquiryData.InquiryData.Length);
            return inquiryData;
        }
    }
}
