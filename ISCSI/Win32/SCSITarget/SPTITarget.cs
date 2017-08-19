/* Copyright (C) 2017 Tal Aloni <tal.aloni.il@gmail.com>.
 * Copyright (C) 2017 Alex Bowden <alex.bowden@outlook.com>.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using DiskAccessLibrary;
using Utilities;

namespace SCSI.Win32
{
    // An excellent C# example of SPTI can be seen here:
    // https://github.com/brandonlw/Psychson/blob/master/DriveCom/DriveCom/PhisonDevice.cs
    public class SPTITarget : SCSITarget
    {
        public const int IOCTL_SCSI_GET_INQUIRY_DATA = 0x4100c;
        public const int IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;
        public const int SCSI_TIMEOUT = 60;

        public event EventHandler<LogEntry> OnLogEntry;

        private string m_path;
        private SafeFileHandle m_handle;
        private bool m_emulateReportLUNs;

        private LogicalUnitManager m_logicalUnitManager = new LogicalUnitManager();

        public SPTITarget(string path) : this(path, false)
        {
        }

        public SPTITarget(string path, bool emulateReportLUNs)
        {
            m_path = path;
            m_handle = HandleUtils.GetFileHandle(m_path, FileAccess.ReadWrite, ShareMode.None);
            m_emulateReportLUNs = emulateReportLUNs;
        }

        public void Close()
        {
            m_handle.Close();
        }

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(SafeHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        /// <summary>
        /// This takes the iSCSI command and forwards it to a SCSI Passthrough device. It then returns the response.
        /// </summary>
        public override SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response)
        {
            // SPTI only supports up to 16 byte CDBs
            if (commandBytes.Length > SCSI_PASS_THROUGH_DIRECT.CdbBufferLength)
            {
                response = VirtualSCSITarget.FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            if (commandBytes[0] == (byte)SCSIOpCodeName.ReportLUNs)
            {
                if (m_emulateReportLUNs)
                {
                    Log(Severity.Verbose, "SCSI Command: ReportLUNs");
                    ReportLUNsParameter parameter = new ReportLUNsParameter();
                    parameter.LUNList.Add(0);
                    response = parameter.GetBytes();
                    if (m_logicalUnitManager.FindLogicalUnit(0) == null)
                    {
                        m_logicalUnitManager.AddLogicalUnit(new LogicalUnit());
                    }
                    return SCSIStatusCodeName.Good;
                }
                else
                {
                    return ReportLUNs(out response);
                }
            }

            LogicalUnit logicalUnit = m_logicalUnitManager.FindLogicalUnit((byte)lun);
            if (logicalUnit == null)
            {
                response = VirtualSCSITarget.FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            // Pad all CDBs to 16 bytes
            Array.Resize(ref commandBytes, SCSI_PASS_THROUGH_DIRECT.CdbBufferLength);

            // Build SCSI Passthrough structure
            SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER scsi = BuildSCSIPassThroughStructure(commandBytes, logicalUnit, data);
            if (scsi == null)
            {
                response = VirtualSCSITarget.FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            uint bytesReturned;
            IntPtr inBuffer = inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(scsi));
            uint size = (uint)Marshal.SizeOf(scsi);
            Marshal.StructureToPtr(scsi, inBuffer, true);

            // Forward SCSI command to target
            SCSIStatusCodeName status;
            if (!DeviceIoControl(m_handle, IOCTL_SCSI_PASS_THROUGH_DIRECT, inBuffer, size, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero))
            {
                // Notes:
                // 1. DeviceIoControl will return ERROR_INVALID_HANDLE under Windows Vista or later if not running as administrator.
                // 2. If a device class driver has claimed the device then passthrough IOCTLs must go through the device class driver.
                //    Sending IOCTLs to the port driver will return ERROR_INVALID_FUNCTION in such cases.
                //    To work with an HBA one can disable the disk drivers of disks connected to that HBA.
                Win32Error lastError = (Win32Error)Marshal.GetLastWin32Error();
                Log(Severity.Error, "DeviceIoControl/IOCTL_SCSI_PASS_THROUGH_DIRECT error: {0}, Device path: {1}", lastError, m_path);
                response = VirtualSCSITarget.FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                status = SCSIStatusCodeName.CheckCondition;
            }
            else
            {
                Marshal.PtrToStructure(inBuffer, scsi);
                status = (SCSIStatusCodeName)scsi.Spt.ScsiStatus;
                if (status != SCSIStatusCodeName.Good)
                {
                    Log(Severity.Verbose, "SCSI Status: {0}, Sense: {1}", status, BitConverter.ToString(scsi.Sense));
                    response = new byte[scsi.Sense.Length + 2];
                    BigEndianWriter.WriteUInt16(response, 0, (ushort)scsi.Sense.Length);
                    ByteWriter.WriteBytes(response, 2, scsi.Sense);
                }
                else
                {
                    if (scsi.Spt.DataTransferLength > 0)
                    {
                        if (scsi.Spt.DataIn == (byte)SCSIDataDirection.In)
                        {
                            response = new byte[scsi.Spt.DataTransferLength];
                            Marshal.Copy(scsi.Spt.DataBuffer, response, 0, response.Length);
                        }
                        else
                        {
                            response = new byte[0];
                        }
                        Log(Severity.Verbose, "SCSI Status: {0}, Response Length: {1}", status, response.Length);

                        if (commandBytes[0] == (byte)SCSIOpCodeName.Inquiry)
                        {
                            InterceptInquiry(logicalUnit, commandBytes, response);
                        }
                        else if (commandBytes[0] == (byte)SCSIOpCodeName.ModeSelect6)
                        {
                            InterceptModeSelect6(logicalUnit, commandBytes, data);
                        }
                        else if (commandBytes[0] == (byte)SCSIOpCodeName.ModeSense6)
                        {
                            InterceptModeSense6(logicalUnit, commandBytes, response);
                        }
                        else if (commandBytes[0] == (byte)SCSIOpCodeName.ReadCapacity10)
                        {
                            InterceptReadCapacity10(logicalUnit, commandBytes, response);
                        }
                        else if (commandBytes[0] == (byte)SCSIOpCodeName.ModeSelect10)
                        {
                            InterceptModeSelect10(logicalUnit, commandBytes, data);
                        }
                        else if (commandBytes[0] == (byte)SCSIOpCodeName.ModeSense10)
                        {
                            InterceptModeSense10(logicalUnit, commandBytes, response);
                        }
                        else if (commandBytes[0] == (byte)SCSIOpCodeName.ServiceActionIn16 && commandBytes[1] == (byte)ServiceAction.ReadCapacity16)
                        {
                            InterceptReadCapacity16(logicalUnit, commandBytes, response);
                        }
                    }
                    else
                    {
                        // SPTI request was GOOD, no data in response buffer.
                        response = new byte[0];
                        Log(Severity.Verbose, "SCSI Status: {0}", status);
                    }
                }
            }

            Marshal.FreeHGlobal(inBuffer);
            if (scsi.Spt.DataBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(scsi.Spt.DataBuffer);
            }

            return status;
        }

        private SCSIStatusCodeName ReportLUNs(out byte[] response)
        {
            uint bytesReturned;
            uint outBufferLength = 4096;
            IntPtr outBuffer = Marshal.AllocHGlobal((int)outBufferLength);
            SCSIStatusCodeName status;
            if (!DeviceIoControl(m_handle, IOCTL_SCSI_GET_INQUIRY_DATA, IntPtr.Zero, 0, outBuffer, outBufferLength, out bytesReturned, IntPtr.Zero))
            {
                Win32Error lastError = (Win32Error)Marshal.GetLastWin32Error();
                Log(Severity.Error, "DeviceIoControl/IOCTL_SCSI_GET_INQUIRY_DATA error: {0}, Device path: {1}", lastError, m_path);
                response = VirtualSCSITarget.FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                status = SCSIStatusCodeName.CheckCondition;
            }
            else
            {
                List<SCSI_INQUIRY_DATA> devices = SCSI_ADAPTER_BUS_INFO.GetInquiryDataForAllDevices(outBuffer);
                ReportLUNsParameter parameter = new ReportLUNsParameter();
                foreach (SCSI_INQUIRY_DATA device in devices)
                {
                    // If the device has been claimed by a class driver then passthrough IOCTLs must go through the class driver
                    if (!device.DeviceClaimed)
                    {
                        PeripheralDeviceType deviceType = (PeripheralDeviceType)(device.InquiryData[0] & 0x1F);
                        if (deviceType == PeripheralDeviceType.DirectAccessBlockDevice |
                            deviceType == PeripheralDeviceType.SequentialAccessDevice |
                            deviceType == PeripheralDeviceType.CDRomDevice)
                        {
                            byte? associatedLUN = m_logicalUnitManager.FindAssociatedLUN(device.PathId, device.TargetId, device.Lun);
                            if (!associatedLUN.HasValue)
                            {
                                associatedLUN = m_logicalUnitManager.FindUnusedLUN();
                                LogicalUnit logicalUnit = new LogicalUnit();
                                logicalUnit.AssociatedLun = associatedLUN.Value;
                                logicalUnit.PathId = device.PathId;
                                logicalUnit.TargetId = device.TargetId;
                                logicalUnit.TargetLun = device.Lun;
                                logicalUnit.DeviceType = deviceType;
                                m_logicalUnitManager.AddLogicalUnit(logicalUnit);
                                Log(Severity.Verbose, "Assigned virtual LUN {0} to device PathId: {1}, TargetId: {2}, LUN: {3}", associatedLUN.Value, device.PathId, device.TargetId, device.Lun);
                            }

                            if (!associatedLUN.HasValue)
                            {
                                throw new NotImplementedException("The maximum number of LUNs supported has been reached");
                            }
                            parameter.LUNList.Add(associatedLUN.Value);
                        }
                    }
                }
                response = parameter.GetBytes();
                Log(Severity.Verbose, "DeviceIoControl/IOCTL_SCSI_GET_INQUIRY_DATA reported {0} usable devices", parameter.LUNList.Count);
                status = SCSIStatusCodeName.Good;
            }
            Marshal.FreeHGlobal(outBuffer);
            return status;
        }

        private SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER BuildSCSIPassThroughStructure(byte[] commandBytes, LogicalUnit logicalUnit, byte[] data)
        {
            SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER scsi = null;
            scsi = new SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER();
            scsi.Spt.Length = (ushort)Marshal.SizeOf(scsi.Spt);
            scsi.Spt.PathId = logicalUnit.PathId;
            scsi.Spt.TargetId = logicalUnit.TargetId;
            scsi.Spt.Lun = logicalUnit.TargetLun;
            scsi.Spt.CdbLength = (byte)commandBytes.Length;
            scsi.Spt.Cdb = commandBytes;

            if (data != null && data.Length > 0)
            {
                // DATA OUT (from initiator to target, WRITE)
                scsi.Spt.DataIn = (byte)SCSIDataDirection.Out;
                scsi.Spt.DataTransferLength = (uint)data.Length;
                scsi.Spt.DataBuffer = Marshal.AllocHGlobal((int)scsi.Spt.DataTransferLength);
                Marshal.Copy(data, 0, scsi.Spt.DataBuffer, data.Length);
            }
            else
            {
                // DATA IN (to initiator from target, READ)
                scsi.Spt.DataIn = (byte)SCSICommandParser.GetDataDirection(commandBytes);
                if ((SCSIDataDirection)scsi.Spt.DataIn == SCSIDataDirection.In)
                {
                    scsi.Spt.DataTransferLength = GetDataInTransferLength(commandBytes, logicalUnit);
                    scsi.Spt.DataBuffer = Marshal.AllocHGlobal((int)scsi.Spt.DataTransferLength);
                }
                else
                {
                    scsi.Spt.DataTransferLength = 0; // No data!
                    scsi.Spt.DataBuffer = IntPtr.Zero;
                }
            }
            Log(Severity.Verbose, "SCSI Command: {0}, Data Length: {1}, Transfer Direction: {2}, Transfer Length: {3}, LUN: {4}", (SCSIOpCodeName)commandBytes[0], data.Length, (SCSIDataDirection)scsi.Spt.DataIn, scsi.Spt.DataTransferLength, logicalUnit.AssociatedLun);
            scsi.Spt.TimeOutValue = SCSI_TIMEOUT;
            scsi.Spt.SenseInfoOffset = (uint)Marshal.OffsetOf(typeof(SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER), "Sense");
            scsi.Spt.SenseInfoLength = (byte)scsi.Sense.Length;

            return scsi;
        }

        private uint GetDataInTransferLength(byte[] commandBytes, LogicalUnit logicalUnit)
        {
            switch ((SCSIOpCodeName)commandBytes[0])
            {
                case SCSIOpCodeName.Read16:                        // DATA_IN (12-14)
                case SCSIOpCodeName.ReadReverse16:                 // DATA_IN (12-14)
                case SCSIOpCodeName.Read6:                         // DATA_IN (2-4)
                case SCSIOpCodeName.ReadReverse6:                  // DATA_IN (2-4)
                case SCSIOpCodeName.Read10:                        // DATA_IN (7-8)
                case SCSIOpCodeName.Read12:                        // DATA_IN (6-9)
                    {
                        if (logicalUnit.BlockSize == null)
                        {
                            throw new NotSupportedException("Command Sequence Not Supported!");
                        }
                        return SCSICommandParser.GetDeviceReadTransferLength(commandBytes, logicalUnit.DeviceType, logicalUnit.BlockSize.Value);
                    }
                default:
                    return SCSICommandParser.GetCDBTransferLength(commandBytes, logicalUnit.DeviceType);
            }
        }

        // Intercept Inquiry and update the peripheral device type
        private void InterceptInquiry(LogicalUnit logicalUnit, byte[] commandBytes, byte[] response)
        {
            bool EVPD = ((commandBytes[1] & 0x01) != 0);
            byte pageCode = commandBytes[2];
            if (!EVPD && pageCode == 0)
            {
                logicalUnit.DeviceType = (PeripheralDeviceType)(response[0] & 0x1F);
                Log(Severity.Verbose, "LUN: {0}, DeviceType updated to {1}", logicalUnit.AssociatedLun, logicalUnit.DeviceType);
            }
        }

        private void InterceptModeSelect6(LogicalUnit logicalUnit, byte[] commandBytes, byte[] data)
        {
            ModeParameterHeader6 header = new ModeParameterHeader6(data, 0);
            if (header.BlockDescriptorLength == ShortLBAModeParameterBlockDescriptor.Length)
            {
                ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor(data, ModeParameterHeader6.Length);
                logicalUnit.BlockSize = descriptor.LogicalBlockLength;
                Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
            }
        }

        private void InterceptModeSense6(LogicalUnit logicalUnit, byte[] commandBytes, byte[] response)
        {
            ModeParameterHeader6 header = new ModeParameterHeader6(response, 0);
            if (header.BlockDescriptorLength == ShortLBAModeParameterBlockDescriptor.Length)
            {
                ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor(response, ModeParameterHeader6.Length);
                logicalUnit.BlockSize = descriptor.LogicalBlockLength;
                Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
            }
        }

        private void InterceptModeSelect10(LogicalUnit logicalUnit, byte[] commandBytes, byte[] data)
        {
            ModeParameterHeader10 header = new ModeParameterHeader10(data, 0);
            if (header.BlockDescriptorLength == ShortLBAModeParameterBlockDescriptor.Length)
            {
                ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor(data, ModeParameterHeader10.Length);
                logicalUnit.BlockSize = descriptor.LogicalBlockLength;
                Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
            }
            else if (header.BlockDescriptorLength == LongLBAModeParameterBlockDescriptor.Length)
            {
                LongLBAModeParameterBlockDescriptor descriptor = new LongLBAModeParameterBlockDescriptor(data, ModeParameterHeader10.Length);
                logicalUnit.BlockSize = descriptor.LogicalBlockLength;
                Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
            }
        }

        private void InterceptModeSense10(LogicalUnit logicalUnit, byte[] commandBytes, byte[] response)
        {
            ModeParameterHeader10 header = new ModeParameterHeader10(response, 0);
            if (header.BlockDescriptorLength == ShortLBAModeParameterBlockDescriptor.Length)
            {
                ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor(response, ModeParameterHeader10.Length);
                logicalUnit.BlockSize = descriptor.LogicalBlockLength;
                Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
            }
            else if (header.BlockDescriptorLength == LongLBAModeParameterBlockDescriptor.Length)
            {
                LongLBAModeParameterBlockDescriptor descriptor = new LongLBAModeParameterBlockDescriptor(commandBytes, ModeParameterHeader10.Length);
                logicalUnit.BlockSize = descriptor.LogicalBlockLength;
                Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
            }
        }

        private void InterceptReadCapacity10(LogicalUnit logicalUnit, byte[] commandBytes, byte[] response)
        {
            ReadCapacity10Parameter parameter = new ReadCapacity10Parameter(response);
            logicalUnit.BlockSize = parameter.BlockLengthInBytes;
            Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
        }

        private void InterceptReadCapacity16(LogicalUnit logicalUnit, byte[] commandBytes, byte[] response)
        {
            ReadCapacity16Parameter parameter = new ReadCapacity16Parameter(response);
            logicalUnit.BlockSize = parameter.BlockLengthInBytes;
            Log(Severity.Verbose, "LUN: {0}, BlockSize updated to {1}", logicalUnit.AssociatedLun, logicalUnit.BlockSize);
        }

        public void Log(Severity severity, string message)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<LogEntry> handler = OnLogEntry;
            if (handler != null)
            {
                handler(this, new LogEntry(DateTime.Now, severity, "SPTI Target", message));
            }
        }

        public void Log(Severity severity, string message, params object[] args)
        {
            Log(severity, String.Format(message, args));
        }
    }
}
