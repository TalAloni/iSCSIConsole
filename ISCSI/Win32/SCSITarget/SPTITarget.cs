/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>.
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
using DiskAccessLibrary;
using Utilities;
using ISCSI;


namespace SCSI
{
    // An excellent C# example of SPTI can be seen here:
    // https://github.com/brandonlw/Psychson/blob/master/DriveCom/DriveCom/PhisonDevice.cs
    public class SPTITarget : SCSITarget
    {
        public const int IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;
        public const int SCSI_TIMEOUT = 60;
        public event EventHandler<LogEntry> OnLogEntry;

        private string m_path;
        private SafeHandle m_handle;

        public SPTITarget(string path)
        {
            m_handle = HandleUtils.GetFileHandle(path, FileAccess.ReadWrite, ShareMode.ReadWrite);
            m_path = path;
        }

        [StructLayout(LayoutKind.Sequential)]
        class SCSI_PASS_THROUGH_DIRECT
        {
            private const int _CDB_LENGTH = 16;

            public short Length;
            public byte ScsiStatus;
            public byte PathId;
            public byte TargetId;
            public byte Lun;
            public byte CdbLength;
            public byte SenseInfoLength;
            public byte DataIn;
            public int DataTransferLength;
            public int TimeOutValue;
            public IntPtr DataBuffer;
            public uint SenseInfoOffset;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = _CDB_LENGTH)]
            public byte[] Cdb;

            public SCSI_PASS_THROUGH_DIRECT()
            {
                Cdb = new byte[_CDB_LENGTH];
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        class SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER
        {
            private const int _SENSE_LENGTH = 32;
            internal SCSI_PASS_THROUGH_DIRECT spt = new SCSI_PASS_THROUGH_DIRECT();

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = _SENSE_LENGTH)]
            internal byte[] sense;

            public SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER()
            {
                sense = new byte[_SENSE_LENGTH];
            }
        };

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(SafeHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        /// <summary>
        /// This takes the iSCSI command and forwards it to a SCSI Passthrough device. It then returns the response.
        /// </summary>
        public override SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response)
        {
            SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER scsi = null;
            IntPtr inBuffer = IntPtr.Zero;
            response = new byte[0];

            try {
                Log(Severity.Verbose, "Forwarding SCSI Command (0x{0})", commandBytes[0].ToString("X"));
                Log(Severity.Verbose, "cdb length {0}", commandBytes.Length);
                Log(Severity.Verbose, "data length {0}", data.Length);

                scsi = new SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER();
                scsi.spt.Cdb = commandBytes;
                scsi.spt.Length = (short)Marshal.SizeOf(scsi.spt);
                scsi.spt.CdbLength = (byte)commandBytes.Length;
                if (data != null && data.Length > 0)
                {
                    scsi.spt.DataIn = (byte)SCSIDataDirection.In;
                    scsi.spt.DataTransferLength = data.Length;
                }
                else
                {
                    scsi.spt.DataIn = (byte)GetDataDirection(commandBytes);
                    scsi.spt.DataTransferLength = GetDataInTransferLength(commandBytes);
                }
                Log(Severity.Verbose, "spt DataTransferLength {0}", scsi.spt.DataTransferLength);
                scsi.spt.TimeOutValue = SCSI_TIMEOUT;
                scsi.spt.DataBuffer = Marshal.AllocHGlobal(scsi.spt.DataTransferLength);
                scsi.spt.SenseInfoOffset = (uint)Marshal.OffsetOf(typeof(SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER), "sense");
                scsi.spt.SenseInfoLength = (byte)scsi.sense.Length;

                // Sending data to the passthrough device
                if (data != null && data.Length > 0)
                {
                    Marshal.Copy(data, 0, scsi.spt.DataBuffer, data.Length);
                }

                uint bytesReturned;
                inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(scsi));
                uint size = (uint)Marshal.SizeOf(scsi);
                Marshal.StructureToPtr(scsi, inBuffer, true);

                if (!DeviceIoControl(m_handle, IOCTL_SCSI_PASS_THROUGH_DIRECT,
                    inBuffer, size, inBuffer, size, out bytesReturned, IntPtr.Zero))
                {
                    int lastError = Marshal.GetLastWin32Error();
                    Log(Severity.Error, "DeviceIoControl failed! err: {0}", lastError);
                    response = VirtualSCSITarget.FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                    return SCSIStatusCodeName.CheckCondition;
                }
                else
                {
                    Marshal.PtrToStructure(inBuffer, scsi);
                    Log(Severity.Verbose, "scsi status: {0}", scsi.spt.ScsiStatus);
                    Log(Severity.Verbose, "scsi sense : {0}", BitConverter.ToString(scsi.sense));
                    if (scsi.spt.ScsiStatus != 0)
                    {
                        response = new byte[scsi.sense.Length + 2];
                        BigEndianWriter.WriteUInt16(response, 0, (ushort)scsi.sense.Length);
                        ByteWriter.WriteBytes(response, 2, scsi.sense);
                        // XXX: Should directly return scsi.spt.ScsiStatus
                        if (scsi.spt.ScsiStatus != 0x02)
                        {
                            throw new NotSupportedException("SCSI Status != CheckCondition (Not Implemented)");
                        }
                        return SCSIStatusCodeName.CheckCondition;
                    }
                    else
                    {
                        // Data Transfer Success
                        if (scsi.spt.DataTransferLength > 0)
                        {
                            response = new byte[scsi.spt.DataTransferLength];
                            Marshal.Copy(scsi.spt.DataBuffer, response, 0, response.Length);

                            Log(Severity.Verbose, "bytesreturned: {0}", bytesReturned.ToString());
                            Log(Severity.Verbose, "resp bytes: {0}", BitConverter.ToString(response));

                            return SCSIStatusCodeName.Good;
                        }
                        // Non-data Transfer Success
                        else
                        {
                            return SCSIStatusCodeName.Good;
                        }
                    }
                }
            }
            finally
            {
                if (scsi != null && scsi.spt.DataBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(scsi.spt.DataBuffer);
                }

                if (inBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
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

        public static SCSIDataDirection GetDataDirection(byte[] commandBytes)
        {
            switch (commandBytes[0])
            {
                case (byte)SCSIOpCodeName.Read16:
                case (byte)SCSIOpCodeName.ReadReverse16:
                case (byte)SCSIOpCodeName.Read6:
                case (byte)SCSIOpCodeName.ReadReverse6:
                case (byte)SCSIOpCodeName.ReadBlockLimits:
                case (byte)SCSIOpCodeName.ReadPosition:
                case (byte)SCSIOpCodeName.RecoverBufferedData:
                case (byte)SCSIOpCodeName.ReportDensitySupport:
                case (byte)SCSIOpCodeName.MaintenanceIn:
                case (byte)SCSIOpCodeName.ServiceActionIn12:
                case (byte)SCSIOpCodeName.ServiceActionIn16:
                case (byte)SCSIOpCodeName.Inquiry:
                case (byte)SCSIOpCodeName.LogSelect10:
                case (byte)SCSIOpCodeName.LogSense10:
                case (byte)SCSIOpCodeName.ModeSelect6:
                case (byte)SCSIOpCodeName.ModeSelect10:
                case (byte)SCSIOpCodeName.ModeSense6:
                case (byte)SCSIOpCodeName.ModeSense10:
                case (byte)SCSIOpCodeName.PersistentReserveIn:
                case (byte)SCSIOpCodeName.ReadAttribute16:
                case (byte)SCSIOpCodeName.ReadBuffer10:
                case (byte)SCSIOpCodeName.ThirdPartyCopyIn:
                case (byte)SCSIOpCodeName.ReceiveDiagnosticResults:
                case (byte)SCSIOpCodeName.ReportLUNs:
                case (byte)SCSIOpCodeName.RequestSense:
                case (byte)SCSIOpCodeName.SecurityProtocolIn:
                    return SCSIDataDirection.In;
                case (byte)SCSIOpCodeName.Erase16:
                case (byte)SCSIOpCodeName.Verify16: // if BYTCMP == 0, UNSPECIFIED else DATA_OUT
                case (byte)SCSIOpCodeName.WriteFilemarks16:
                case (byte)SCSIOpCodeName.Erase6:
                case (byte)SCSIOpCodeName.Locate10:
                case (byte)SCSIOpCodeName.Space6:
                case (byte)SCSIOpCodeName.Verify6: // if BYTCMP == 0, UNSPECIFIED else DATA_OUT
                case (byte)SCSIOpCodeName.WriteFilemarks6:
                case (byte)SCSIOpCodeName.FormatUnit:
                case (byte)SCSIOpCodeName.LoadUnload:
                case (byte)SCSIOpCodeName.Locate16:
                case (byte)SCSIOpCodeName.Rewind:
                case (byte)SCSIOpCodeName.SetCapacity:
                case (byte)SCSIOpCodeName.TestUnitReady:
                    return SCSIDataDirection.NoData;
                default:
                    return SCSIDataDirection.In;
            }
        }

        public static int GetDataInTransferLength(byte[] commandBytes)
        {
            switch (commandBytes[0])
            {
                case (byte)SCSIOpCodeName.Read16:                        // DATA_IN (12-14)
                case (byte)SCSIOpCodeName.ReadReverse16:                 // DATA_IN (12-14)
                    return (int)BigEndianConverter.ToUInt24(commandBytes, 12);
                case (byte)SCSIOpCodeName.Read6:                         // DATA_IN (2-4)
                case (byte)SCSIOpCodeName.ReadReverse6:                  // DATA_IN (2-4)
                case (byte)SCSIOpCodeName.RecoverBufferedData:           // DATA_IN (2-4)
                    return (int)BigEndianConverter.ToUInt24(commandBytes, 2);
                case (byte)SCSIOpCodeName.ReadBlockLimits:               // DATA_IN (6 bytes)
                    return 6;
                case (byte)SCSIOpCodeName.ReadPosition:                  // DATA_IN (7-8)
                case (byte)SCSIOpCodeName.ReportDensitySupport:          // DATA_IN (7-8)
                case (byte)SCSIOpCodeName.LogSelect10:                   // DATA_IN (7-8)
                case (byte)SCSIOpCodeName.LogSense10:                    // DATA_IN (7-8)
                case (byte)SCSIOpCodeName.ModeSelect10:                  // DATA_IN (7-8)
                case (byte)SCSIOpCodeName.ModeSense10:                   // DATA_IN (7-8)
                case (byte)SCSIOpCodeName.PersistentReserveIn:           // DATA_IN (7-8)
                    return BigEndianConverter.ToUInt16(commandBytes, 7);
                case (byte)SCSIOpCodeName.ModeSelect6:                   // DATA_IN (4)
                case (byte)SCSIOpCodeName.ModeSense6:                    // DATA_IN (4)
                case (byte)SCSIOpCodeName.RequestSense:                  // DATA_IN (4)
                    return (int)commandBytes[4];
                case (byte)SCSIOpCodeName.ReadAttribute16:               // DATA_IN (10-13)
                case (byte)SCSIOpCodeName.ThirdPartyCopyIn:              // DATA_IN (10-13) ?
                    return (int)BigEndianConverter.ToUInt32(commandBytes, 10);
                case (byte)SCSIOpCodeName.ReadBuffer10:                  // DATA_IN (6-8)
                    return BigEndianConverter.ToUInt16(commandBytes, 6);
                case (byte)SCSIOpCodeName.ReceiveDiagnosticResults:      // DATA_IN (3-4)
                case (byte)SCSIOpCodeName.Inquiry:                       // DATA_IN (3-4)
                    return BigEndianConverter.ToUInt16(commandBytes, 3);
                case (byte)SCSIOpCodeName.ReportLUNs:                    // DATA_IN (6-9)
                case (byte)SCSIOpCodeName.SecurityProtocolIn:            // DATA_IN (6-9)
                    return (int)BigEndianConverter.ToUInt32(commandBytes, 6);
                case (byte)SCSIOpCodeName.MaintenanceIn:                 // DATA_IN (TODO)
                case (byte)SCSIOpCodeName.ServiceActionIn12:             // DATA_IN (TODO)
                case (byte)SCSIOpCodeName.ServiceActionIn16:             // DATA_IN
                default:
                    return 512;
            }
        }
    }
}
