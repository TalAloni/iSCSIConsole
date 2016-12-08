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

namespace DiskAccessLibrary.VHD
{
    public class ParentLocatorEntry
    {
        public const int Length = 24;

        public uint PlatformCode;
        public uint PlatformDataSpace;
        public uint PlatformDataLength;
        public uint Reserved;
        public ulong PlatformDataOffset;

        public ParentLocatorEntry()
        {
        }

        public ParentLocatorEntry(byte[] buffer, int offset)
        {
            PlatformCode = BigEndianConverter.ToUInt32(buffer, offset + 0);
            PlatformDataSpace = BigEndianConverter.ToUInt32(buffer, offset + 4);
            PlatformDataLength = BigEndianConverter.ToUInt32(buffer, offset + 8);
            Reserved = BigEndianConverter.ToUInt32(buffer, offset + 12);
            PlatformDataOffset = BigEndianConverter.ToUInt64(buffer, offset + 16);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            BigEndianWriter.WriteUInt32(buffer, offset + 0, PlatformCode);
            BigEndianWriter.WriteUInt32(buffer, offset + 4, PlatformDataSpace);
            BigEndianWriter.WriteUInt32(buffer, offset + 8, PlatformDataLength);
            BigEndianWriter.WriteUInt32(buffer, offset + 12, Reserved);
            BigEndianWriter.WriteUInt64(buffer, offset + 16, PlatformDataOffset);
        }
    }
}
