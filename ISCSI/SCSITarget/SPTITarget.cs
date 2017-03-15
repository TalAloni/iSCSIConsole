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
using ISCSI.Server;

namespace SCSI
{
    // An excellent C# example of SPTI can be seen here:
    // https://github.com/brandonlw/Psychson/blob/master/DriveCom/DriveCom/PhisonDevice.cs
    public class SPTITarget : SCSITarget
    {
        public const byte SCSI_IOCTL_DATA_OUT = 0;
        public const byte SCSI_IOCTL_DATA_IN = 1;

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
            const int IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;
            const int SCSI_TIMEOUT = 60;
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
                scsi.spt.DataIn = data != null && data.Length > 0 ? SCSI_IOCTL_DATA_OUT : SCSI_IOCTL_DATA_IN;
                scsi.spt.DataTransferLength = data != null && data.Length > 0 ? data.Length : (int)((SCSICommandPDU)task).ExpectedDataTransferLength;
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
                var size = (uint)Marshal.SizeOf(scsi);
                Marshal.StructureToPtr(scsi, inBuffer, true);

                if (!DeviceIoControl(m_handle, IOCTL_SCSI_PASS_THROUGH_DIRECT,
                    inBuffer, size, inBuffer, size, out bytesReturned, IntPtr.Zero))
                {
                    int lastError = Marshal.GetLastWin32Error();
                    Log(Severity.Error, "DeviceIoControl failed! err: {0}", lastError);
                    throw new InvalidOperationException("DeviceIOControl Failed!");
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
                        if (scsi.spt.DataTransferLength > 0)
                        {
                            response = new byte[scsi.spt.DataTransferLength];
                            Marshal.Copy(scsi.spt.DataBuffer, response, 0, response.Length);

                            Log(Severity.Verbose, "bytesreturned: {0}", bytesReturned.ToString());
                            Log(Severity.Verbose, "resp bytes: {0}", BitConverter.ToString(response));

                            return SCSIStatusCodeName.Good;
                        }
                        else
                        {
                            // XXX: If DataTransferLength is 0? I guess just say it's GOOD.
                            if (scsi.spt.ScsiStatus == 0)
                            {
                                return SCSIStatusCodeName.Good;
                            }
                            else
                            {
                                throw new NotSupportedException("SCSI DataTransferLength == 0 & SCSI Status != GOOD (Not Implemented)");
                            }
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
        
        public List<Disk> Disks
        {
            get
            {
                return null;
            }
        }
    }
}
