/* Copyright (C) 2012-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class UnitSerialNumberVPDPage
    {
        public byte PeripheralQualifier;
        public PeripheralDeviceType PeripheralDeviceType;
        public VitalProductDataPageName PageCode; // VitalProductDataPageName
        public byte PageLength;
        public string ProductSerialNumber;

        public UnitSerialNumberVPDPage()
        {
            PageCode = VitalProductDataPageName.UnitSerialNumber;
        }

        public UnitSerialNumberVPDPage(byte[] buffer, int offset)
        {
            PeripheralQualifier = (byte)(buffer[offset + 0] >> 5);
            PeripheralDeviceType = (PeripheralDeviceType)(buffer[offset + 0] & 0x1F);
            PageCode = (VitalProductDataPageName)buffer[offset + 1];
            PageLength = buffer[offset + 3];
            ProductSerialNumber = ASCIIEncoding.ASCII.GetString(buffer, offset + 4, PageLength);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[4 + ProductSerialNumber.Length];
            buffer[0] |= (byte)(PeripheralQualifier << 5);
            buffer[0] |= (byte)(PeripheralQualifier & 0x1F);
            buffer[1] = (byte)PageCode;
            buffer[3] = (byte)ProductSerialNumber.Length;
            ByteWriter.WriteAnsiString(buffer, 4, ProductSerialNumber);
            return buffer;
        }

    }
}
