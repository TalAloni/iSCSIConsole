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
    public class DeviceIdentificationVPDPage
    {
        public byte PeripheralQualifier;
        public PeripheralDeviceType PeripheralDeviceType;
        public VitalProductDataPageName PageCode; // VitalProductDataPageName
        public ushort PageLength;
        public List<IdentificationDescriptor> IdentificationDescriptorList = new List<IdentificationDescriptor>();

        public DeviceIdentificationVPDPage()
        {
            PageCode = VitalProductDataPageName.DeviceIdentification;
        }

        public DeviceIdentificationVPDPage(byte[] buffer, int offset)
        {
            PeripheralQualifier = (byte)(buffer[offset + 0] >> 5);
            PeripheralDeviceType = (PeripheralDeviceType)(buffer[offset + 0] & 0x1F);
            PageCode = (VitalProductDataPageName)buffer[offset + 1];
            PageLength = BigEndianConverter.ToUInt16(buffer, 2);
            int parameterOffset = 4;
            while (parameterOffset < PageLength)
            {
                IdentificationDescriptor descriptor = new IdentificationDescriptor(buffer, offset + parameterOffset);
                IdentificationDescriptorList.Add(descriptor);
                parameterOffset += descriptor.Length;
            }
        }

        public byte[] GetBytes()
        {
            PageLength = 0;
            foreach(IdentificationDescriptor descriptor in IdentificationDescriptorList)
            {
                PageLength += (ushort)descriptor.Length;
            }

            byte[] buffer = new byte[4 + PageLength];
            buffer[0] |= (byte)(PeripheralQualifier << 5);
            buffer[0] |= (byte)(PeripheralQualifier & 0x1F);
            buffer[1] = (byte)PageCode;
            BigEndianWriter.WriteUInt16(buffer, 2, PageLength);

            int offset = 4;
            foreach (IdentificationDescriptor descriptor in IdentificationDescriptorList)
            {
                Array.Copy(descriptor.GetBytes(), 0, buffer, offset, descriptor.Length);
                offset += descriptor.Length;
            }
            return buffer;
        }
    }
}
