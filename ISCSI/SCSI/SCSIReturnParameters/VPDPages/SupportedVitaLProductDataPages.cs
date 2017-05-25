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
    public class SupportedVitaLProductDataPages
    {
        public byte PeripheralQualifier;
        public PeripheralDeviceType PeripheralDeviceType;
        public byte PageLength;
        public List<byte> SupportedPageList = new List<byte>();

        public SupportedVitaLProductDataPages()
        { 
        }

        public SupportedVitaLProductDataPages(byte[] buffer, int offset)
        {
            PeripheralQualifier = (byte)(buffer[offset + 0] >> 5);
            PeripheralDeviceType = (PeripheralDeviceType)(buffer[offset + 0] & 0x1F);
            PageLength = buffer[offset + 3];
            for (int index = 0; index < PageLength; index++)
            { 
                SupportedPageList.Add(buffer[offset + 4 + index]);
            }
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[4 + SupportedPageList.Count];
            buffer[0] |= (byte)(PeripheralQualifier << 5);
            buffer[0] |= (byte)(PeripheralQualifier & 0x1F);
            buffer[3] = (byte)SupportedPageList.Count;
            SupportedPageList.Sort(); // must be sorted by ascending order
            SupportedPageList.CopyTo(buffer, 4);
            return buffer;
        }
    }
}
