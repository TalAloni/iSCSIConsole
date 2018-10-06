/* Copyright (C) 2014-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

    public class DeviceInfo
    {
        public string DevicePath;
        public string DeviceDescription;
        public string FriendlyName;

        /// <summary>
        /// Device manager shows the friendly name if it exists and the device description otherwise.
        /// </summary>
        public string DeviceName
        {
            get
            {
                if (!String.IsNullOrEmpty(FriendlyName))
                {
                    return FriendlyName;
                }
                else
                {
                    return DeviceDescription;
                }
            }
        }
    }

    public class DeviceInterfaceUtils // SetupDi functions
    {
        public static readonly Guid DiskClassGuid = new Guid("53F56307-B6BF-11D0-94F2-00A0C91EFB8B");
        public static readonly Guid CDRomClassGuid = new Guid("53F56308-B6BF-11D0-94F2-00A0C91EFB8B");
        public static readonly Guid TapeClassGuid = new Guid("53F5630B-B6BF-11D0-94F2-00A0C91EFB8B");
        public static readonly Guid MediumChangerClassGuid = new Guid("53F56310-B6BF-11D0-94F2-00A0C91EFB8B");
        public static readonly Guid StoragePortClassGuid = new Guid("2ACCFE60-C130-11D2-B082-00A0C91EFB8B");

        private const Int64 INVALID_HANDLE_VALUE = -1;

        private const uint SPDRP_DEVICEDESC = 0x00000000;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(           // 1st form using a ClassGUID only, with null Enumerator
           ref Guid classGuid,
           IntPtr enumerator,
           IntPtr hwndParent,
           uint flags
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList
        (
             IntPtr deviceInfoSet
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            ref SP_DEVINFO_DATA deviceInfoData // Out
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiEnumDeviceInterfaces(
           IntPtr deviceInfoSet,
           ref SP_DEVINFO_DATA deviceInfoData,
           ref Guid interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        // Alternate signature if you do not care about SP_DEVINFO_DATA and wish to pass NULL (IntPtr.Zero)
        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiEnumDeviceInterfaces(
           IntPtr deviceInfoSet,
           IntPtr deviceInfoData,
           ref Guid interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr deviceInfoSet,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           ref SP_DEVINFO_DATA deviceInfoData
        );

        // Alternate signature if you do not care about SP_DEVINFO_DATA and wish to pass NULL (IntPtr.Zero)
        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr deviceInfoSet,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           IntPtr deviceInfoData
        );

        // Alternate signature - first call (we wish to pass IntPtr instead of reference to SP_DEVICE_INTERFACE_DETAIL_DATA)
        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr deviceInfoSet,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           IntPtr deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           IntPtr deviceInfoData
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            UInt32 property,
            out UInt32 propertyRegDataType,
            byte[] propertyBuffer,              // The caller will allocate this byte array for the callee to fill
            UInt32 propertyBufferSize,
            out UInt32 requiredSize
        );

        /// <summary>
        /// returns a handle to a device information set that contains requested device information elements.
        /// </summary>
        /// <remarks>
        /// The caller must delete the returned device information set when it is no longer needed
        /// by calling DestroyDeviceInfoList().
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdigetclassdevsw
        /// </remarks>
        public static IntPtr GetClassDevices(Guid classGuid)
        {
            uint flags = (uint)(DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICEINTERFACE); // Only Devices present & Interface class
            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, flags);
            if (deviceInfoSet.ToInt64() == INVALID_HANDLE_VALUE)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Unable to retrieve class devices, Win32 Error: {0}", errorCode);
                throw new IOException(message);
            }
            else
            {
                return deviceInfoSet;
            }
        }

        public static void DestroyDeviceInfoList(IntPtr deviceInfoSet)
        {
            bool success = SetupDiDestroyDeviceInfoList(deviceInfoSet);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                string message = String.Format("Unable to destroy device info list, Win32 Error: {0}", errorCode);
                throw new IOException(message);
            }
        }

        /// <remarks>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdigetdeviceinterfacedetaila
        /// </remarks>
        public static SP_DEVICE_INTERFACE_DETAIL_DATA GetDeviceInterfaceDetail(IntPtr deviceInfoSet, SP_DEVICE_INTERFACE_DATA deviceInterfaceData)
        {
            // For ERROR_INVALID_USER_BUFFER error see:
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-_sp_device_interface_detail_data_a
            // http://stackoverflow.com/questions/10728644/properly-declare-sp-device-interface-detail-data-for-pinvoke
            uint requiredSize;
            SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
            bool success = SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
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

                    success = SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, lpOutBuffer, size, out requiredSize, IntPtr.Zero);
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
            IntPtr deviceInfoSet = GetClassDevices(deviceClassGuid);
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            uint index = 0;

            List<string> result = new List<string>();
            while (true)
            {
                bool success = SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref deviceClassGuid, index, ref deviceInterfaceData);
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

                SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = GetDeviceInterfaceDetail(deviceInfoSet, deviceInterfaceData);
                result.Add(deviceInterfaceDetailData.DevicePath);
                index++;
            }

            DestroyDeviceInfoList(deviceInfoSet);

            return result;
        }

        private static string GetDeviceStringProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, uint property)
        {
            uint propertyRegDataType;
            byte[] propertyBuffer = new byte[128];
            uint requiredSize;
            bool success = SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, out propertyRegDataType, propertyBuffer, (uint)propertyBuffer.Length, out requiredSize);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode == (int)Win32Error.ERROR_INSUFFICIENT_BUFFER)
                {
                    propertyBuffer = new byte[requiredSize];
                    success = SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, out propertyRegDataType, propertyBuffer, (uint)propertyBuffer.Length, out requiredSize);
                }
                else if (errorCode == (int)Win32Error.ERROR_INVALID_DATA)
                {
                    return null;
                }
                
                if (!success)
                {
                    string message = String.Format("Unable to retrieve property, Win32 Error: {0}", errorCode);
                    throw new IOException(message);
                }
            }
            string value = UnicodeEncoding.Unicode.GetString(propertyBuffer, 0, (int)requiredSize);
            value = value.TrimEnd(new char[] { '\0' });
            return value;
        }

        // https://msdn.microsoft.com/windows/hardware/drivers/install/device-information-sets
        public static List<DeviceInfo> GetDeviceList(Guid deviceClassGuid)
        {
            IntPtr deviceInfoSet = GetClassDevices(deviceClassGuid);
            SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
            deviceInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            uint index = 0;

            List<DeviceInfo> result = new List<DeviceInfo>();
            while (true)
            {
                bool success = SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfoData);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == (int)Win32Error.ERROR_NO_MORE_ITEMS)
                    {
                        break;
                    }
                    else
                    {
                        string message = String.Format("Unable to enumerate devices, Win32 Error: {0}", errorCode);
                        throw new IOException(message);
                    }
                }

                success = SetupDiEnumDeviceInterfaces(deviceInfoSet, ref deviceInfoData, ref deviceClassGuid, 0, ref deviceInterfaceData);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    string message = String.Format("Unable to enumerate device interfaces, Win32 Error: {0}", errorCode);
                    throw new IOException(message);
                }

                SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = GetDeviceInterfaceDetail(deviceInfoSet, deviceInterfaceData);
                DeviceInfo deviceInfo = new DeviceInfo();
                deviceInfo.DevicePath = deviceInterfaceDetailData.DevicePath;
                deviceInfo.DeviceDescription = GetDeviceStringProperty(deviceInfoSet, deviceInfoData, SPDRP_DEVICEDESC);
                deviceInfo.FriendlyName = GetDeviceStringProperty(deviceInfoSet, deviceInfoData, SPDRP_FRIENDLYNAME);
                result.Add(deviceInfo);
                index++;
            }

            DestroyDeviceInfoList(deviceInfoSet);

            return result;
        }
    }
}
