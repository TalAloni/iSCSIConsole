/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    // VolumeInformation attribute is always resident
    public class VolumeInformationRecord : ResidentAttributeRecord
    {
        public byte MajorVersion;
        public byte MinorVersion;
        public ushort VolumeFlags;

        public VolumeInformationRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            MajorVersion = ByteReader.ReadByte(this.Data, 0x08);
            MinorVersion = ByteReader.ReadByte(this.Data, 0x09);
            VolumeFlags = LittleEndianConverter.ToUInt16(this.Data, 0x0A);
        }
    }
}
