/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace SCSI
{
    /// <summary>
    /// Fixed format sense data
    /// </summary>
    public class SenseDataParameter
    {
        public bool Valid;
        public byte ResponseCode;
        public bool FileMark;
        public bool EOM; // End-of-Medium
        public bool ILI; // Incorrect length indicator
        public byte SenseKey; // 4 bits
        public byte[] Information = new byte[4];
        public byte AdditionalSenseLength;
        public byte AdditionalSenseCode;
        public byte AdditionalSenseCodeQualifier;

        public SenseDataParameter()
        { 

        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[18];
            AdditionalSenseLength = 10;

            if (Valid)
            {
                buffer[0] |= 0x80;
            }
            buffer[0] |= (byte)(ResponseCode & 0x7F);

            if (FileMark)
            {
                buffer[2] |= 0x80;
            }
            if (EOM)
            {
                buffer[2] |= 0x40;
            }
            if (ILI)
            {
                buffer[2] |= 0x20;
            }

            buffer[2] |= (byte)(SenseKey & 0xF);
            Array.Copy(Information, 0, buffer, 3, 4);
            buffer[7] = AdditionalSenseLength;
            buffer[12] = AdditionalSenseCode;
            buffer[13] = AdditionalSenseCodeQualifier;
            return buffer;
        }

        public static SenseDataParameter GetDataProtectSenseData()
        {
            SenseDataParameter senseData = new SenseDataParameter();
            senseData.Valid = true;
            senseData.ResponseCode = 0x70; // current errors
            senseData.SenseKey = 0x07;     // DATA PROTECT
            senseData.AdditionalSenseCode = 0x27; // Command not allowed
            senseData.AdditionalSenseCodeQualifier = 0x00;
            return senseData;
        }

        public static SenseDataParameter GetIllegalRequestSenseData(byte additionalSenseCode, byte additionalSenseCodeQualifier)
        {
            SenseDataParameter senseData = new SenseDataParameter();
            senseData.Valid = true;
            senseData.ResponseCode = 0x70; // current errors
            senseData.SenseKey = 0x05;     // ILLEGAL REQUEST
            senseData.AdditionalSenseCode = additionalSenseCode;
            senseData.AdditionalSenseCodeQualifier = additionalSenseCodeQualifier;
            return senseData;
        }

        public static SenseDataParameter GetIllegalRequestInvalidFieldInCDBSenseData()
        {
            return GetIllegalRequestSenseData(0x24, 0x00); // Invalid field in CDB
        }

        public static SenseDataParameter GetIllegalRequestInvalidLUNSenseData()
        {
            return GetIllegalRequestSenseData(0x25, 0x00); // Invalid LUN
        }

        public static SenseDataParameter GetIllegalRequestLBAOutOfRangeSenseData()
        {
            return GetIllegalRequestSenseData(0x21, 0x00); // LBA out of range
        }

        public static SenseDataParameter GetIllegalRequestUnsupportedCommandCodeSenseData()
        {
            return GetIllegalRequestSenseData(0x20, 0x00); // Invalid / unsupported command code
        }

        public static SenseDataParameter GetMediumErrorUnrecoverableReadErrorSenseData()
        {
            SenseDataParameter senseData = new SenseDataParameter();
            senseData.Valid = true;
            senseData.ResponseCode = 0x70; // current errors
            senseData.SenseKey = 0x03;     // MEDIUM ERROR
            senseData.AdditionalSenseCode = 0x11; // Peripheral Device Write Fault
            senseData.AdditionalSenseCodeQualifier = 0x00;
            return senseData;
        }

        public static SenseDataParameter GetMediumErrorWriteFaultSenseData()
        {
            SenseDataParameter senseData = new SenseDataParameter();
            senseData.Valid = true;
            senseData.ResponseCode = 0x70; // current errors
            senseData.SenseKey = 0x03;     // MEDIUM ERROR
            senseData.AdditionalSenseCode = 0x03; // Peripheral Device Write Fault
            senseData.AdditionalSenseCodeQualifier = 0x00;
            return senseData;
        }

        public static SenseDataParameter GetNoSenseSenseData()
        {
            SenseDataParameter senseData = new SenseDataParameter();
            senseData.Valid = true;
            senseData.ResponseCode = 0x70; // current errors
            senseData.SenseKey = 0x00;     // NO SENSE
            senseData.AdditionalSenseCode = 0x00; // No Additional Sense Information
            return senseData;
        }

        /// <summary>
        /// Reported when CRC error is encountered
        /// </summary>
        public static SenseDataParameter GetWriteFaultSenseData()
        {
            SenseDataParameter senseData = new SenseDataParameter();
            senseData.Valid = true;
            senseData.ResponseCode = 0x70; // current errors
            senseData.SenseKey = 0x03;     // MEDIUM ERROR
            senseData.AdditionalSenseCode = 0x03; // Peripheral Device Write Fault
            senseData.AdditionalSenseCodeQualifier = 0x00;
            return senseData;
        }
    }
}
