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
        public const byte SCSI_IOCTL_DATA_OUT = 0;
        public const byte SCSI_IOCTL_DATA_IN = 1;
        public const byte SCSI_IOCTL_DATA_UNSPECIFIED = 2;
        public const int IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;
        public const int SCSI_TIMEOUT = 60;

        public const byte Read16                     = 0x88; // DATA_IN (12-14)
        public const byte ReadReverse16              = 0x81; // DATA_IN (12-14)
        public const byte Read6                      = 0x08; // DATA_IN (2-4)
        public const byte ReadReverse6               = 0x0F; // DATA_IN (2-4)
        public const byte ReadBlockLimits            = 0x05; // DATA_IN (6 bytes)
        public const byte ReadPosition               = 0x34; // DATA_IN (7-8)
        public const byte RecoverBufferedData        = 0x14; // DATA_IN (2-4)
        public const byte ReportDensitySupport       = 0x44; // DATA_IN (7-8)
        public const byte MaintenanceIn              = 0xA3; // DATA_IN (TODO)
        public const byte ServiceActionIn            = 0xAB; // DATA_IN (TODO)
        public const byte ServiceActionIn16          = 0x9E; // DATA_IN
        public const byte Inquiry                    = 0x12; // DATA_IN (3-4)
        public const byte LogSelect10                = 0x4C; // DATA_IN (7-8)
        public const byte LogSense10                 = 0x4D; // DATA_IN (7-8)
        public const byte ModeSelect6                = 0x15; // DATA_IN (4)
        public const byte ModeSelect10               = 0x55; // DATA_IN (7-8)
        public const byte ModeSense6                 = 0x1A; // DATA_IN (4)
        public const byte ModeSense10                = 0x5A; // DATA_IN (7-8)
        public const byte PersistentReserveIn        = 0x5E; // DATA_IN (7-8)
        public const byte ReadAttribute16            = 0x8C; // DATA_IN (10-13)
        public const byte ReadBuffer10               = 0x3C; // DATA_IN (6-8)
        public const byte ReceiveCopyData            = 0x84; // DATA_IN (10-13) ?
        public const byte ReceiveCredential          = 0x7F; // DATA_IN (10-11)
        public const byte ReceiveDiagnosticResults   = 0x1C; // DATA_IN (3-4)
        public const byte ReportLuns                 = 0xA0; // DATA_IN (6-9)
        public const byte RequestSense               = 0x03; // DATA_IN (4)
        public const byte SecurityProtocolIn         = 0xA2; // DATA_IN (6-9)

        public const byte Erase16                    = 0x93; // UNSPECIFIED
        public const byte Verify16                   = 0x8F; // if BYTCMP == 0, UNSPECIFIED else DATA_OUT
        public const byte WriteFilemarks16           = 0x80; // UNSPECIFIED
        public const byte Erase6                     = 0x19; // UNSPECIFIED
        public const byte Locate10                   = 0x2B; // UNSPECIFIED
        public const byte Space6                     = 0x11; // UNSPECIFIED
        public const byte Verify6                    = 0x13; // if BYTCMP == 0, UNSPECIFIED else DATA_OUT
        public const byte WriteFilemarks6            = 0x10; // UNSPECIFIED
        public const byte FormatMedium               = 0x04; // UNSPECIFIED
        public const byte LoadUnload                 = 0x1B; // UNSPECIFIED
        public const byte Locate16                   = 0x92; // UNSPECIFIED
        public const byte Rewind                     = 0x01; // UNSPECIFIED
        public const byte SetCapacity                = 0x0B; // UNSPECIFIED
        public const byte TestUnitReady              = 0x00; // UNSPECIFIED

        public enum SCSIUnspecifiedOpCode : byte
        {
            Erase16 = 0x93,
            Verify16 = 0x8F,
            WriteFilemarks16 = 0x80,
            Erase6 = 0x19,
            Locate10 = 0x2B,
            Space6 = 0x11,
            Verify6 = 0x13,
            WriteFilemarks6 = 0x10,
            FormatMedium = 0x04,
            LoadUnload = 0x1B,
            Locate16 = 0x92,
            Rewind = 0x01,
            SetCapacity = 0x0B,
            TestUnitReady = 0x00,
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

        public event EventHandler<LogEntry> OnLogEntry;

        private string m_path;
        private SafeHandle m_handle;

        public SPTITarget(string path)
        {
            m_handle = HandleUtils.GetFileHandle(path, FileAccess.ReadWrite, ShareMode.ReadWrite);
            m_path = path;
        }

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(SafeHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        /// <summary>
        /// This takes the iSCSI command and forwards it to a SCSI Passthrough device. It then returns the response.
        /// </summary>
        public override SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, object task, out byte[] response)
        {
            SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER scsi = null;
            IntPtr inBuffer = IntPtr.Zero;
            response = new byte[0];

            try {
                Log(Severity.Verbose, "Forwarding SCSI Command (0x{0})", commandBytes[0].ToString("X"));
                Log(Severity.Verbose, "cdb length {0}", commandBytes.Length);
                Log(Severity.Verbose, "data length {0}", data.Length);
                Log(Severity.Verbose, "scsi exp transf len {0}", ((SCSICommandPDU)task).ExpectedDataTransferLength);

                scsi = new SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER();
                scsi.spt.Cdb = commandBytes;
                scsi.spt.Length = (short)Marshal.SizeOf(scsi.spt);
                scsi.spt.CdbLength = (byte)commandBytes.Length;
                /*
                 * DataIn: DATA_OUT for WRITE operations.
                 *         DATA_IN for READ operations.
                 *         UNSPECIFIED for NO DATA TRANSFER commands
                 *         (Fallback to DATA_IN for some unsupported commands)
                 */
                if (data != null && data.Length > 0)
                {
                    scsi.spt.DataIn = SCSI_IOCTL_DATA_OUT;
                    scsi.spt.DataTransferLength = data.Length;
                }
                else if (Enum.IsDefined(typeof(SCSIUnspecifiedOpCode), scsi.spt.Cdb[0]))
                {
                    scsi.spt.DataIn = SCSI_IOCTL_DATA_UNSPECIFIED;
                }
                else
                {
                    scsi.spt.DataIn = SCSI_IOCTL_DATA_IN;
                    scsi.spt.DataTransferLength = GetDataInTransferLength(commandBytes);
                }
                Log(Severity.Verbose, "spt DataTransferLength {0}", scsi.spt.DataTransferLength);
                scsi.spt.TimeOutValue = SCSI_TIMEOUT;
                scsi.spt.DataBuffer = Marshal.AllocHGlobal(scsi.spt.DataTransferLength);
                scsi.spt.SenseInfoOffset = (uint)Marshal.OffsetOf(typeof(SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER), "sense");
                scsi.spt.SenseInfoLength = (byte)scsi.sense.Length;

                // Sending data to the passthrough target
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
                        // Non-Data Transfer Success
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

        public int GetDataInTransferLength(byte[] cdb)
        {
            switch (cdb[0])
            {
                case Read16:                        // DATA_IN (12-14)
                case ReadReverse16:                 // DATA_IN (12-14)
                    return (int)BigEndianConverter.ToUInt24(cdb, 12);
                case Read6:                         // DATA_IN (2-4)
                case ReadReverse6:                  // DATA_IN (2-4)
                case RecoverBufferedData:           // DATA_IN (2-4)
                    return (int)BigEndianConverter.ToUInt24(cdb, 2);
                case ReadBlockLimits:               // DATA_IN (6 bytes)
                    return 6;
                case ReadPosition:                  // DATA_IN (7-8)
                case ReportDensitySupport:          // DATA_IN (7-8)
                case LogSelect10:                   // DATA_IN (7-8)
                case LogSense10:                    // DATA_IN (7-8)
                case ModeSelect10:                  // DATA_IN (7-8)
                case ModeSense10:                   // DATA_IN (7-8)
                case PersistentReserveIn:           // DATA_IN (7-8)
                    return BigEndianConverter.ToUInt16(cdb, 7);
                case ModeSelect6:                   // DATA_IN (4)
                case ModeSense6:                    // DATA_IN (4)
                case RequestSense:                  // DATA_IN (4)
                    return (int)cdb[4];
                case ReadAttribute16:               // DATA_IN (10-13)
                case ReceiveCopyData:               // DATA_IN (10-13) ?
                    return (int)BigEndianConverter.ToUInt32(cdb, 10);
                case ReadBuffer10:                  // DATA_IN (6-8)
                    return BigEndianConverter.ToUInt16(cdb, 6);
                case ReceiveCredential:             // DATA_IN (10-11)
                    return BigEndianConverter.ToUInt16(cdb, 10);
                case ReceiveDiagnosticResults:      // DATA_IN (3-4)
                case Inquiry:                       // DATA_IN (3-4)
                    return BigEndianConverter.ToUInt16(cdb, 3);
                case ReportLuns:                    // DATA_IN (6-9)
                case SecurityProtocolIn:            // DATA_IN (6-9)
                    return (int)BigEndianConverter.ToUInt32(cdb, 6);
                case MaintenanceIn:                 // DATA_IN (TODO)
                case ServiceActionIn:               // DATA_IN (TODO)
                case ServiceActionIn16:             // DATA_IN
                default:
                    return 512;
            }
        }
    }
}
