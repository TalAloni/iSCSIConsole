/* Copyright (C) 2017 Tal Aloni <tal.aloni.il@gmail.com>.
 * Copyright (C) 2017 Alex Bowden <alex.bowden@outlook.com>.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using Utilities;

namespace SCSI.Win32
{
    public class SCSICommandParser
    {
        private static bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        public static SCSIDataDirection GetDataDirection(byte[] commandBytes)
        {
            switch ((SCSIOpCodeName)commandBytes[0])
            {
                case SCSIOpCodeName.Read16:
                case SCSIOpCodeName.ReadReverse16:
                case SCSIOpCodeName.Read6:
                case SCSIOpCodeName.ReadReverse6:
                case SCSIOpCodeName.Read10:
                case SCSIOpCodeName.Read12:
                case SCSIOpCodeName.ReadBlockLimits:
                case SCSIOpCodeName.ReadCapacity10:
                case SCSIOpCodeName.ReadDefectData10:
                case SCSIOpCodeName.ReadDefectData12:
                case SCSIOpCodeName.ReadLong10:
                case SCSIOpCodeName.ReadPosition:
                case SCSIOpCodeName.RecoverBufferedData:
                case SCSIOpCodeName.ReportDensitySupport:
                case SCSIOpCodeName.MaintenanceIn:
                case SCSIOpCodeName.ServiceActionIn12:
                case SCSIOpCodeName.ServiceActionIn16:
                case SCSIOpCodeName.Inquiry:
                case SCSIOpCodeName.LogSelect10:
                case SCSIOpCodeName.LogSense10:
                case SCSIOpCodeName.ModeSelect6:
                case SCSIOpCodeName.ModeSelect10:
                case SCSIOpCodeName.ModeSense6:
                case SCSIOpCodeName.ModeSense10:
                case SCSIOpCodeName.PersistentReserveIn:
                case SCSIOpCodeName.ReadAttribute16:
                case SCSIOpCodeName.ReadBuffer10:
                case SCSIOpCodeName.ThirdPartyCopyIn:
                case SCSIOpCodeName.ReceiveDiagnosticResults:
                case SCSIOpCodeName.ReportLUNs:
                case SCSIOpCodeName.RequestSense:
                case SCSIOpCodeName.SecurityProtocolIn:
                    return SCSIDataDirection.In;
                case SCSIOpCodeName.Erase16:
                case SCSIOpCodeName.WriteFilemarks16:
                case SCSIOpCodeName.Erase6:
                case SCSIOpCodeName.Locate10:
                case SCSIOpCodeName.Space6:
                case SCSIOpCodeName.WriteFilemarks6:
                case SCSIOpCodeName.FormatUnit:
                case SCSIOpCodeName.LoadUnload:
                case SCSIOpCodeName.Locate16:
                case SCSIOpCodeName.Rewind:
                case SCSIOpCodeName.SetCapacity:
                case SCSIOpCodeName.TestUnitReady:
                case SCSIOpCodeName.PreFetch16:
                    return SCSIDataDirection.NoData;
                default:
                    return SCSIDataDirection.In;
            }
        }

        // Parse CDB allocation length (bytes) based on SPC-3, SSC-3, and SBC-3
        public static uint GetCDBTransferLength(byte[] commandBytes, PeripheralDeviceType deviceType)
        {
            switch ((SCSIOpCodeName)commandBytes[0])
            {
                case SCSIOpCodeName.RecoverBufferedData:           // DATA_IN (2-4)
                    return BigEndianReader.ReadUInt24(commandBytes, 2);
                case SCSIOpCodeName.ReadBlockLimits:               // DATA_IN (6 bytes)
                    return 6;
                case SCSIOpCodeName.ReadCapacity10:                // DATA_IN (8 bytes)
                    return 8;
                case SCSIOpCodeName.ReadPosition:                  // DATA_IN (7-8) 
                    if (deviceType == PeripheralDeviceType.SequentialAccessDevice)
                    {
                        if (commandBytes[1] == 0x00 || commandBytes[1] == 0x01)
                        {
                            return 20;
                        }
                        else if (commandBytes[1] == 0x06)
                        {
                            return 32;
                        }
                        else
                        {
                            return BigEndianConverter.ToUInt16(commandBytes, 7);
                        }
                    }
                    else
                    {
                        return BigEndianConverter.ToUInt16(commandBytes, 7);
                    }
                case SCSIOpCodeName.ReportDensitySupport:          // DATA_IN (7-8)
                case SCSIOpCodeName.LogSelect10:                   // DATA_IN (7-8)
                case SCSIOpCodeName.LogSense10:                    // DATA_IN (7-8)
                case SCSIOpCodeName.ModeSelect10:                  // DATA_IN (7-8)
                case SCSIOpCodeName.ModeSense10:                   // DATA_IN (7-8)
                case SCSIOpCodeName.PersistentReserveIn:           // DATA_IN (7-8)
                case SCSIOpCodeName.ReadLong10:                    // DATA_IN (7-8)
                case SCSIOpCodeName.ReadDefectData10:              // DATA_IN (7-8)
                    return BigEndianConverter.ToUInt16(commandBytes, 7);
                case SCSIOpCodeName.ModeSelect6:                   // DATA_IN (4)
                case SCSIOpCodeName.ModeSense6:                    // DATA_IN (4)
                case SCSIOpCodeName.RequestSense:                  // DATA_IN (4)
                    return (uint)commandBytes[4];
                case SCSIOpCodeName.ReadAttribute16:               // DATA_IN (10-13)
                case SCSIOpCodeName.ThirdPartyCopyIn:              // DATA_IN (10-13) ?
                    return BigEndianConverter.ToUInt32(commandBytes, 10);
                case SCSIOpCodeName.ReadBuffer10:                  // DATA_IN (6-8)
                    return BigEndianReader.ReadUInt24(commandBytes, 6);
                case SCSIOpCodeName.ReceiveDiagnosticResults:      // DATA_IN (3-4)
                case SCSIOpCodeName.Inquiry:                       // DATA_IN (3-4)
                    return BigEndianConverter.ToUInt16(commandBytes, 3);
                case SCSIOpCodeName.ReportLUNs:                    // DATA_IN (6-9)
                case SCSIOpCodeName.SecurityProtocolIn:            // DATA_IN (6-9)
                case SCSIOpCodeName.ReadDefectData12:              // DATA_IN (6-9)
                    return BigEndianConverter.ToUInt32(commandBytes, 6);
                case SCSIOpCodeName.ServiceActionIn16:
                    if (commandBytes[1] == (byte)ServiceAction.ReadLong16)     // DATA_IN (12-13)
                    {
                        return BigEndianConverter.ToUInt16(commandBytes, 12);
                    }
                    if (commandBytes[1] == (byte)ServiceAction.ReadCapacity16) // DATA_IN (10-13)
                    {
                        return BigEndianConverter.ToUInt32(commandBytes, 10);
                    }
                    return 512;
                default:
                    // XXX: Need to complete SBC-3 (ex: XDREAD)
                    return 512;
            }
        }

        public static uint GetDeviceReadTransferLength(byte[] commandBytes, PeripheralDeviceType deviceType, uint blockSize)
        {
            if (deviceType == PeripheralDeviceType.DirectAccessBlockDevice ||
                deviceType == PeripheralDeviceType.CDRomDevice)
            {
                return GetBlockDeviceReadTransferLength(commandBytes, blockSize);
            }
            else if (deviceType == PeripheralDeviceType.SequentialAccessDevice)
            {
                return SCSICommandParser.GetSequentialAccessDeviceReadTransferLength(commandBytes, blockSize);
            }

            throw new NotSupportedException("Device Type Not Supported!");
        }

        public static uint GetBlockDeviceReadTransferLength(byte[] commandBytes, uint blockSize)
        {
            uint transferLength = 0;

            switch ((SCSIOpCodeName)commandBytes[0])
            {
                case SCSIOpCodeName.Read6:                         // DATA_IN (4)
                    transferLength = (uint)commandBytes[4];
                    if (transferLength == 0)
                    {
                        transferLength = 256;
                    }
                    break;
                case SCSIOpCodeName.Read10:                        // DATA_IN (7-8)
                    transferLength = BigEndianConverter.ToUInt16(commandBytes, 7);
                    break;
                case SCSIOpCodeName.Read12:                        // DATA_IN (6-9)
                    transferLength = BigEndianConverter.ToUInt32(commandBytes, 6);
                    break;
                case SCSIOpCodeName.Read16:                        // DATA_IN (10-13)
                    transferLength = BigEndianConverter.ToUInt32(commandBytes, 10);
                    break;
                default:
                    throw new NotSupportedException("Invalid CDB when parsing READ Transfer Length");
            }

            return blockSize * transferLength;
        }

        public static uint GetSequentialAccessDeviceReadTransferLength(byte[] commandBytes, uint blockSize)
        {
            uint transferLength = 0;
            bool fixedBlockSize = false;

            switch ((SCSIOpCodeName)commandBytes[0])
            {
                case SCSIOpCodeName.Read16:                        // DATA_IN (12-14)
                case SCSIOpCodeName.ReadReverse16:                 // DATA_IN (12-14)
                    transferLength = BigEndianReader.ReadUInt24(commandBytes, 12);
                    break;
                case SCSIOpCodeName.Read6:                         // DATA_IN (2-4)
                case SCSIOpCodeName.ReadReverse6:                  // DATA_IN (2-4)
                    transferLength = BigEndianReader.ReadUInt24(commandBytes, 2);
                    break;
                default:
                    throw new NotSupportedException("Invalid CDB when parsing READ Transfer Length");
            }

            fixedBlockSize = IsBitSet(commandBytes[1], 0);
            if (fixedBlockSize)
            {
                return blockSize * transferLength;
            }
            else
            {
                /*
                 * If FIXED == 0, using Variable Block Length
                 * This means TRANSFER LENGTH is in bytes, not blocks
                 */
                return transferLength;
            }
        }
    }
}
