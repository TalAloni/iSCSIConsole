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
    public class BlockLimitsVPDPage
    {
        public byte PeripheralQualifier;
        public PeripheralDeviceType PeripheralDeviceType;
        public VitalProductDataPageName PageCode; // VitalProductDataPageName
        public byte PageLength;
        public ushort OptimalTransferLengthGranularity;
        public uint MaximumTransferLength;
        public uint OptimalTransferLength;

        public BlockLimitsVPDPage()
        {
            PageCode = VitalProductDataPageName.BlockLimits;
            PageLength = 12;
        }

        public BlockLimitsVPDPage(byte[] buffer, int offset)
        {
            PeripheralQualifier = (byte)(buffer[offset + 0] >> 5);
            PeripheralDeviceType = (PeripheralDeviceType)(buffer[offset + 0] & 0x1F);
            PageCode = (VitalProductDataPageName)buffer[offset + 1];
            PageLength = buffer[offset + 3];
            OptimalTransferLengthGranularity = BigEndianConverter.ToUInt16(buffer, offset + 6);
            MaximumTransferLength = BigEndianConverter.ToUInt32(buffer, offset + 8);
            OptimalTransferLength = BigEndianConverter.ToUInt32(buffer, offset + 12);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[4 + PageLength];
            buffer[0] |= (byte)(PeripheralQualifier << 5);
            buffer[0] |= (byte)(PeripheralQualifier & 0x1F);
            buffer[1] = (byte)PageCode;
            buffer[3] = PageLength;
            BigEndianWriter.WriteUInt16(buffer, 6, OptimalTransferLengthGranularity);
            BigEndianWriter.WriteUInt32(buffer, 8, MaximumTransferLength);
            BigEndianWriter.WriteUInt32(buffer, 12, OptimalTransferLength);
            return buffer;
        }

    }
}
