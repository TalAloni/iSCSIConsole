/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    [Flags]
    public enum TOCRegionFlags : ushort
    {
        NotExist = 0x01, // The present of this flag hide the region from display in DMDiag
        New = 0x02,      // As reported by DMDiag
        Delete = 0x04,   // As reported by DMDiag
        Disabled = 0x08  // As reported by DMDiag
    }

    public class TOCRegion
    {
        public const int Length = 34;

        public string Name;    // 'config' or 'log' (8 characters max)
        public TOCRegionFlags RegionFlags;
        public ulong StartLBA; // Sector Offset from PrivateRegionStart
        public ulong SizeLBA;  // Size of the region
        public ushort Unknown; // 00 06 ( other values will make DMDiag to report the database as invalid )
        public ushort CopyNumber;  // copy number, always 00 01
        // 4 zero bytes

        public TOCRegion(byte[] buffer, int offset)
        {
            Name = ByteReader.ReadAnsiString(buffer, offset + 0x00, 8).Trim('\0');
            RegionFlags = (TOCRegionFlags)BigEndianConverter.ToUInt16(buffer, offset + 0x08);
            StartLBA = BigEndianConverter.ToUInt64(buffer, offset + 0x0A);
            SizeLBA = BigEndianConverter.ToUInt64(buffer, offset + 0x12);
            Unknown = BigEndianConverter.ToUInt16(buffer, offset + 0x1A);
            CopyNumber = BigEndianConverter.ToUInt16(buffer, offset + 0x1C);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            ByteWriter.WriteAnsiString(buffer, offset + 0x00, Name, 8);
            BigEndianWriter.WriteUInt16(buffer, offset + 0x08, (ushort)RegionFlags);
            BigEndianWriter.WriteUInt64(buffer, offset + 0x0A, StartLBA);
            BigEndianWriter.WriteUInt64(buffer, offset + 0x12, SizeLBA);
            BigEndianWriter.WriteUInt16(buffer, offset + 0x1A, Unknown);
            BigEndianWriter.WriteUInt16(buffer, offset + 0x1C, CopyNumber);
        }
    }
}
