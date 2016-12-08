/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DiskAccessLibrary
{
    [Flags]
    public enum DiGetClassFlags : uint
    {
        DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
        DIGCF_PRESENT = 0x00000002,
        DIGCF_ALLCLASSES = 0x00000004,
        DIGCF_PROFILE = 0x00000008,
        DIGCF_DEVICEINTERFACE = 0x00000010,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public uint cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
    }

    public class DeviceInterfaceUtils // SetupDi functions
    {
        public static readonly Guid DiskClassGuid = new Guid("53F56307-B6BF-11D0-94F2-00A0C91EFB8B");
        const Int64 INVALID_HANDLE_VALUE = -1;

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(           // 1st form using a ClassGUID only, with null Enumerator
           ref Guid ClassGuid,
           IntPtr Enumerator,
           IntPtr hwndParent,
           uint Flags
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList
        (
             IntPtr DeviceInfoSet
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiEnumDeviceInterfaces(
           IntPtr hDevInfo,
           ref SP_DEVINFO_DATA devInfo,
           ref Guid interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        // Alternate signature if you do not care about SP_DEVINFO_DATA and wish to pass NULL (IntPtr.Zero)
        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiEnumDeviceInterfaces(
           IntPtr hDevInfo,
           IntPtr devInfo,
           ref Guid interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           ref SP_DEVINFO_DATA deviceInfoData
        );

        // Alternate signature if you do not care about SP_DEVINFO_DATA and wish to pass NULL (IntPtr.Zero)
        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           IntPtr deviceInfoData
        );

        // Alternate signature - first call (we wish to pass IntPtr instead of reference to SP_DEVICE_INTERFACE_DETAIL_DATA)
        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           IntPtr deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           IntPtr deviceInfoData
        );

        /// <summary>
        /// returns a handle to a device information set that contains requested device information elements.
        /// 
        /// The caller must delete the returned device information set when it is no longer needed
        /// by calling DestroyDeviceInfoList().
        /// </summary>
        // http://msdn.microsoft.com/en-us/library/windows/hardware/ff551069%28v=vs.85%29.aspx
        public static IntPtr GetClassDevices(Guid classGuid)
        {
            uint flags = (uint)(DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICEINTERFACE); // Only Devices present & Interface class
            IntPtr hDevInfo = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, flags);
            if (hDevInfo.ToInt64() == INVALID_HANDLE_VALUE)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Unable to retrieve class devices, Win32 Error: {0}", errorCode);
                throw new IOException(message);
            }
            else
            {
                return hDevInfo;
            }
        }

        public static void DestroyDeviceInfoList(IntPtr hDevInfo)
        {
            bool success = DeviceInterfaceUtils.SetupDiDestroyDeviceInfoList(hDevInfo);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Unable to destroy device info list, Win32 Error: {0}", errorCode);
                throw new IOException(message);
            }
        }

        // http://msdn.microsoft.com/en-us/library/windows/hardware/ff551120%28v=vs.85%29.aspx
        public static SP_DEVICE_INTERFACE_DETAIL_DATA GetDeviceInterfaceDetail(IntPtr hDevInfo, SP_DEVICE_INTERFACE_DATA deviceInterfaceData)
        {
            // For ERROR_INVALID_USER_BUFFER error see:
            // http://msdn.microsoft.com/en-us/library/windows/hardware/ff552343%28v=vs.85%29.aspx
            // http://stackoverflow.com/questions/10728644/properly-declare-sp-device-interface-detail-data-for-pinvoke
            uint requiredSize;
            SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
            bool success = SetupDiGetDeviceInterfaceDetail(hDevInfo, ref deviceInterfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode == (int)Win32Error.ERROR_INSUFFICIENT_BUFFER)
                {
                    uint size = requiredSize;
                    // cbSize should parallel to sizeof (SP_INTERFACE_DEVICE_DETAIL_DATA) in C
                    // cbSize is calculated as sizeof(DWORD) + padding + sizeof(TCHAR) + padding
                    int cbSize;
                    if (IntPtr.Size == 4)
                    {
                        cbSize = sizeof(uint) + Marshal.SystemDefaultCharSize;
                    }
                    else
                    {
                        // take x64 padding into account
                        cbSize = sizeof(uint) + 4;
                    }
                    IntPtr lpOutBuffer = Marshal.AllocHGlobal((int)size);
                    Marshal.WriteInt32(lpOutBuffer, cbSize);

                    success = SetupDiGetDeviceInterfaceDetail(hDevInfo, ref deviceInterfaceData, lpOutBuffer, size, out requiredSize, IntPtr.Zero);
                    deviceInterfaceDetailData = (SP_DEVICE_INTERFACE_DETAIL_DATA)Marshal.PtrToStructure(lpOutBuffer, typeof(SP_DEVICE_INTERFACE_DETAIL_DATA));
                    Marshal.FreeHGlobal(lpOutBuffer);

                    if (!success)
                    {
                        errorCode = Marshal.GetLastWin32Error();
                        string message = String.Format("Unable to retrieve device interface detail data, Win32 Error: {0}", errorCode);
                        throw new IOException(message);
                    }
                }
                else
                {
                    errorCode = Marshal.GetLastWin32Error();
                    string message = String.Format("Unable to retrieve device interface detail data, Win32 Error: {0}", errorCode);
                    throw new IOException(message);
                }
            }
            return deviceInterfaceDetailData;
        }

        // Great C++ Example of enumerating and locating specific attach storage devices:
        // http://code.msdn.microsoft.com/windowshardware/CppStorageEnum-90ad5fa9
        public static List<string> GetDevicePathList(Guid deviceClassGuid)
        {
            IntPtr hDevInfo = GetClassDevices(deviceClassGuid);
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            uint index = 0;

            List<string> result = new List<string>();
            while (true)
            {
                bool success = DeviceInterfaceUtils.SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref deviceClassGuid, index, ref deviceInterfaceData);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == (int)Win32Error.ERROR_NO_MORE_ITEMS)
                    {
                        break;
                    }
                    else
                    {
                        string message = String.Format("Unable to enumerate device interfaces, Win32 Error: {0}", errorCode);
                        throw new IOException(message);
                    }
                }

                SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = DeviceInterfaceUtils.GetDeviceInterfaceDetail(hDevInfo, deviceInterfaceData);
                result.Add(deviceInterfaceDetailData.DevicePath);
                index++;
            }

            DestroyDeviceInfoList(hDevInfo);

            return result;
        }
    }
}
