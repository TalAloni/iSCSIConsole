using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utilities;

namespace SCSI.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public class SCSI_ADAPTER_BUS_INFO
    {
        public byte NumberOfBuses;
        public SCSI_BUS_DATA[] BusData;

        public SCSI_ADAPTER_BUS_INFO()
        {
        }

        public static SCSI_ADAPTER_BUS_INFO FromIntPtr(IntPtr ptr)
        {
            SCSI_ADAPTER_BUS_INFO busInfo = new SCSI_ADAPTER_BUS_INFO();
            byte numberOfBuses = Marshal.ReadByte(ptr);
            ptr = new IntPtr(ptr.ToInt64() + 4);
            busInfo.NumberOfBuses = numberOfBuses;
            busInfo.BusData = new SCSI_BUS_DATA[numberOfBuses];
            for (int index = 0; index < numberOfBuses; index++)
            {
                busInfo.BusData[index] = (SCSI_BUS_DATA)Marshal.PtrToStructure(ptr, typeof(SCSI_BUS_DATA));
                ptr = new IntPtr(ptr.ToInt64() + Marshal.SizeOf(typeof(SCSI_BUS_DATA)));
            }
            return busInfo;
        }

        public static List<SCSI_INQUIRY_DATA> GetInquiryDataForAllDevices(IntPtr busInfoPtr)
        {
            SCSI_ADAPTER_BUS_INFO busInfo = FromIntPtr(busInfoPtr);
            List<SCSI_INQUIRY_DATA> devices = new List<SCSI_INQUIRY_DATA>();
            foreach (SCSI_BUS_DATA busData in busInfo.BusData)
            {
                byte numberOfLuns = busData.NumberOfLogicalUnits;
                uint inquiryDataOffset = busData.InquiryDataOffset;
                for (int lunIndex = 0; lunIndex < numberOfLuns; lunIndex++)
                {
                    IntPtr inquiryDataPtr = new IntPtr(busInfoPtr.ToInt64() + inquiryDataOffset);
                    SCSI_INQUIRY_DATA inquiryData = SCSI_INQUIRY_DATA.FromIntPtr(inquiryDataPtr);
                    devices.Add(inquiryData);
                    inquiryDataOffset = inquiryData.NextInquiryDataOffset;
                }
            }
            return devices;
        }
    }
}
