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
    public enum DeviceType : uint
    {
        FILE_DEVICE_CD_ROM = 0x00000002,
        FILE_DEVICE_DISK = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PARTITION_INFORMATION
    {
        public long StartingOffset;
        public long PartitionLength;
        public uint HiddenSectors;
        public uint PartitionNumber;
        public byte PartitionType;
        [MarshalAs(UnmanagedType.I1)]
        public bool BootIndicator;
        [MarshalAs(UnmanagedType.I1)]
        public bool RecognizedPartition;
        [MarshalAs(UnmanagedType.I1)]
        public bool RewritePartition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISK_GEOMETRY
    {
        public long Cylinders;
        public int MediaType;
        public uint TracksPerCylinder;
        public uint SectorsPerTrack;
        public uint BytesPerSector;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISK_GEOMETRY_EX
    {
        public DISK_GEOMETRY Geometry;
        public long DiskSize;
        byte Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DEVICE_NUMBER
    {
        public int DeviceType;
        public int DeviceNumber;
        public int PartitionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GET_DISK_ATTRIBUTES
    {
        public int Version;
        public int Reserved;
        public long Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SET_DISK_ATTRIBUTES
    {
        public int Version;
        [MarshalAs(UnmanagedType.I1)]
        public bool Persist;
        [MarshalAs(UnmanagedType.I1)]
        public bool Reserved1A;
        [MarshalAs(UnmanagedType.I1)]
        public bool Reserved1B;
        [MarshalAs(UnmanagedType.I1)]
        public bool Reserved1C;
        public long Attributes;
        public long AttributesMask;
        public int Reserved2A;
        public int Reserved2B;
        public int Reserved2C;
        public int Reserved2D;
    }

    public class PhysicalDiskUtils
    {
        private const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;
        private const int IOCTL_DISK_GET_DISK_ATTRIBUTES = 0x000700F0;
        private const uint IOCTL_DISK_GET_PARTITION_INFO = 0x00074004;
        private const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
        private const uint IOCTL_DISK_UPDATE_PROPERTIES = 0x00070140;
        private const int IOCTL_DISK_SET_DISK_ATTRIBUTES = 0x0007C0F4;
        private const int IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;

        private const uint DISK_ATTRIBUTE_OFFLINE = 0x1;
        private const uint DISK_ATTRIBUTE_READ_ONLY = 0x2;

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(SafeHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        /// <summary>
        /// Used to determine the exact disk size on Windows 2000
        /// Note: A lot of other programs (including msinfo32) will display the inaccurate
        /// size of Cylinders * TracksPerCylinder * SectorsPerTrack * BytesPerSector
        /// </summary>
        /// <param name="hDevice">Handle to disk or partition</param>
        public static long GetPartitionSize(SafeFileHandle hDevice)
        {
            uint dummy;
            PARTITION_INFORMATION partitionInformation = new PARTITION_INFORMATION();
            IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(partitionInformation));
            Marshal.StructureToPtr(partitionInformation, lpOutBuffer, false);
            bool success = DeviceIoControl(hDevice, IOCTL_DISK_GET_PARTITION_INFO, IntPtr.Zero, 0,
                                lpOutBuffer, (uint)Marshal.SizeOf(typeof(PARTITION_INFORMATION)), out dummy, IntPtr.Zero);
            partitionInformation = (PARTITION_INFORMATION)Marshal.PtrToStructure(lpOutBuffer, typeof(PARTITION_INFORMATION));
            Marshal.FreeHGlobal(lpOutBuffer);
            if (!success)
            {
                throw new IOException("Unable to retrieve disk geometry");
            }
            return partitionInformation.PartitionLength;
        }

        /// <summary>
        /// Supported on Windows 2000
        /// </summary>
        public static DISK_GEOMETRY GetDiskGeometry(SafeFileHandle hDevice)
        {
            uint dummy;
            DISK_GEOMETRY diskGeometry = new DISK_GEOMETRY();
            IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(diskGeometry));
            Marshal.StructureToPtr(diskGeometry, lpOutBuffer, false);
            bool success = DeviceIoControl(hDevice, IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0,
                                lpOutBuffer, (uint)Marshal.SizeOf(typeof(DISK_GEOMETRY)), out dummy, IntPtr.Zero);
            diskGeometry = (DISK_GEOMETRY)Marshal.PtrToStructure(lpOutBuffer, typeof(DISK_GEOMETRY));
            Marshal.FreeHGlobal(lpOutBuffer);
            if (!success)
            {
                throw new IOException("Unable to retrieve disk geometry");
            }
            return diskGeometry;
        }

        public static DISK_GEOMETRY_EX GetDiskGeometryEx(SafeFileHandle hDevice)
        {
            uint dummy;
            DISK_GEOMETRY_EX diskGeometryEx = new DISK_GEOMETRY_EX();
            IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(diskGeometryEx));
            Marshal.StructureToPtr(diskGeometryEx, lpOutBuffer, false);
            bool success = DeviceIoControl(hDevice, IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0,
                                lpOutBuffer, (uint)Marshal.SizeOf(typeof(DISK_GEOMETRY_EX)), out dummy, IntPtr.Zero);
            diskGeometryEx = (DISK_GEOMETRY_EX)Marshal.PtrToStructure(lpOutBuffer, typeof(DISK_GEOMETRY_EX));
            Marshal.FreeHGlobal(lpOutBuffer);
            if (!success)
            {
                // Windows 2000 will throw this exception because IOCTL_DISK_GET_DRIVE_GEOMETRY_EX is only supported from XP onward
                throw new IOException("Unable to retrieve disk geometry");
            }
            return diskGeometryEx;
        }

        public static DISK_GEOMETRY GetDiskGeometryAndSize(SafeFileHandle hDevice, out long diskSize)
        {
            if (Environment.OSVersion.Version > new Version(5, 0, Int32.MaxValue, Int32.MaxValue))
            {
                // XP and upward
                DISK_GEOMETRY_EX diskGeometryEx = GetDiskGeometryEx(hDevice);
                diskSize = diskGeometryEx.DiskSize;
                return diskGeometryEx.Geometry;
            }
            else
            {
                // Windows 2000
                DISK_GEOMETRY diskGeometry = GetDiskGeometry(hDevice);
                // diskSize = diskGeometry.Cylinders * diskGeometry.TracksPerCylinder * diskGeometry.SectorsPerTrack * diskGeometry.BytesPerSector;
                diskSize = GetPartitionSize(hDevice);
                return diskGeometry;
            }
        }

        public static long GetDiskSize(SafeFileHandle hDevice)
        {
            long size;
            GetDiskGeometryAndSize(hDevice, out size);
            return size;
        }

        public static int GetBytesPerSector(SafeFileHandle hDevice)
        {
            long size;
            DISK_GEOMETRY diskGeometry = GetDiskGeometryAndSize(hDevice, out size);
            return (int)diskGeometry.BytesPerSector;
        }

        /// <summary>
        /// Invalidates the cached partition table and re-enumerates the device
        /// </summary>
        public static bool UpdateDiskProperties(SafeFileHandle hDevice)
        {
            uint dummy;
            bool success = DeviceIoControl(hDevice, IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0,
                                IntPtr.Zero, 0, out dummy, IntPtr.Zero);
            return success;
        }

        public static STORAGE_DEVICE_NUMBER GetDeviceNumber(SafeFileHandle hDevice)
        {
            uint dummy;
            STORAGE_DEVICE_NUMBER storageDeviceNumber = new STORAGE_DEVICE_NUMBER();
            IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(storageDeviceNumber));
            bool success = DeviceIoControl(hDevice, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0,
                                lpOutBuffer, (uint)Marshal.SizeOf(typeof(STORAGE_DEVICE_NUMBER)), out dummy, IntPtr.Zero);
            storageDeviceNumber = (STORAGE_DEVICE_NUMBER)Marshal.PtrToStructure(lpOutBuffer, typeof(STORAGE_DEVICE_NUMBER));
            Marshal.FreeHGlobal(lpOutBuffer);
            if (!success)
            {
                throw new IOException("Unable to retrieve device number");
            }
            return storageDeviceNumber;
        }

        public static List<int> GetPhysicalDiskIndexList()
        {
            List<string> devicePathList = DeviceInterfaceUtils.GetDevicePathList(DeviceInterfaceUtils.DiskClassGuid);
            List<int> result = new List<int>();
            foreach (string devicePath in devicePathList)
            {
                SafeFileHandle hDevice = HandleUtils.GetFileHandle(devicePath, FileAccess.Read, ShareMode.ReadWrite);
                STORAGE_DEVICE_NUMBER number = GetDeviceNumber(hDevice);
                hDevice.Close();
                result.Add(number.DeviceNumber);
            }
            // We'll now sort the list based on disk number
            result.Sort();
            return result;
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        public static bool GetOnlineStatus(SafeFileHandle hDevice)
        {
            bool isReadOnly;
            return GetOnlineStatus(hDevice, out isReadOnly);
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        public static bool GetOnlineStatus(SafeFileHandle hDevice, out bool isReadOnly)
        {
            uint dummy;
            GET_DISK_ATTRIBUTES attributes = new GET_DISK_ATTRIBUTES();
            IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(attributes));
            bool success = DeviceIoControl(hDevice, IOCTL_DISK_GET_DISK_ATTRIBUTES, IntPtr.Zero, 0,
                                lpOutBuffer, (uint)Marshal.SizeOf(typeof(GET_DISK_ATTRIBUTES)), out dummy, IntPtr.Zero);
            attributes = (GET_DISK_ATTRIBUTES)Marshal.PtrToStructure(lpOutBuffer, typeof(GET_DISK_ATTRIBUTES));
            Marshal.FreeHGlobal(lpOutBuffer);
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException("Unable to get disk attributes, Error: " + error.ToString());
            }
            bool isOffline = (attributes.Attributes & DISK_ATTRIBUTE_OFFLINE) > 0;
            isReadOnly = (attributes.Attributes & DISK_ATTRIBUTE_READ_ONLY) > 0;
            return !isOffline;
        }

        /// <summary>
        /// Take the disk offline / online, Available on Windows Vista and newer
        /// </summary>
        /// <param name="persist">persist is effective for both online==true and online==false</param>
        private static void TrySetOnlineStatus(SafeFileHandle hDevice, bool online, bool persist)
        {
            uint dummy;
            SET_DISK_ATTRIBUTES attributes = new SET_DISK_ATTRIBUTES();
            attributes.Version = Marshal.SizeOf(attributes);
            attributes.Attributes = online ? 0 : DISK_ATTRIBUTE_OFFLINE;
            attributes.AttributesMask = DISK_ATTRIBUTE_OFFLINE;
            attributes.Persist = persist;
            IntPtr lpInBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(attributes));
            Marshal.StructureToPtr(attributes, lpInBuffer, true);
            bool success = DeviceIoControl(hDevice, IOCTL_DISK_SET_DISK_ATTRIBUTES, lpInBuffer, (uint)Marshal.SizeOf(typeof(SET_DISK_ATTRIBUTES)),
                                           IntPtr.Zero, 0, out dummy, IntPtr.Zero);
            Marshal.FreeHGlobal(lpInBuffer);
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException("Unable to set disk attributes, Error: " + error.ToString());
            }
        }

        /// <summary>
        /// Available on Windows Vista and newer
        /// </summary>
        public static bool SetOnlineStatus(SafeFileHandle hDevice, bool online, bool persist)
        {
            if (GetOnlineStatus(hDevice) != online)
            {
                TrySetOnlineStatus(hDevice, online, persist);
                return (GetOnlineStatus(hDevice) == online);
            }
            return true;
        }
    }
}
