/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class DynamicDiskHeader
    {
        public const int Length = 1024;
        public const string DynamidDiskHeaderCookie = "cxsparse";

        public string Cookie; // 8 bytes
        public ulong DataOffset; // The documentation says 0xFFFFFFFF, but all programs use 0xFFFFFFFFFFFFFFFF
        public ulong TableOffset;
        public uint HeaderVersion;
        public uint MaxTableEntries;
        public uint BlockSize;
        //public uint Checksum;
        public Guid ParentUniqueID;
        public uint ParentTimeStamp;
        public uint Reserved;
        public string ParentUnicodeName = String.Empty; // 8 bytes
        public ParentLocatorEntry ParentLocatorEntry1;
        public ParentLocatorEntry ParentLocatorEntry2;
        public ParentLocatorEntry ParentLocatorEntry3;
        public ParentLocatorEntry ParentLocatorEntry4;
        public ParentLocatorEntry ParentLocatorEntry5;
        public ParentLocatorEntry ParentLocatorEntry6;
        public ParentLocatorEntry ParentLocatorEntry7;
        public ParentLocatorEntry ParentLocatorEntry8;

        private bool m_isValid = true;

        public DynamicDiskHeader()
        {
            Cookie = DynamidDiskHeaderCookie;
            DataOffset = 0xFFFFFFFFFFFFFFFF;
            HeaderVersion = 0x00010000;
            ParentLocatorEntry1 = new ParentLocatorEntry();
            ParentLocatorEntry2 = new ParentLocatorEntry();
            ParentLocatorEntry3 = new ParentLocatorEntry();
            ParentLocatorEntry4 = new ParentLocatorEntry();
            ParentLocatorEntry5 = new ParentLocatorEntry();
            ParentLocatorEntry6 = new ParentLocatorEntry();
            ParentLocatorEntry7 = new ParentLocatorEntry();
            ParentLocatorEntry8 = new ParentLocatorEntry();
        }

        public DynamicDiskHeader(byte[] buffer)
        {
            Cookie = ByteReader.ReadAnsiString(buffer, 0x00, 8);
            DataOffset = BigEndianConverter.ToUInt64(buffer, 0x08);
            TableOffset = BigEndianConverter.ToUInt64(buffer, 0x10);
            HeaderVersion = BigEndianConverter.ToUInt32(buffer, 0x18);
            MaxTableEntries = BigEndianConverter.ToUInt32(buffer, 0x1C);
            BlockSize = BigEndianConverter.ToUInt32(buffer, 0x20);
            uint checksum = BigEndianConverter.ToUInt32(buffer, 0x24);
            ParentUniqueID = BigEndianConverter.ToGuid(buffer, 0x28);
            ParentTimeStamp = BigEndianConverter.ToUInt32(buffer, 0x38);
            Reserved = BigEndianConverter.ToUInt32(buffer, 0x3C);
            ParentUnicodeName = ByteReader.ReadUTF16String(buffer, 0x40, 256).TrimEnd('\0');
            ParentLocatorEntry1 = new ParentLocatorEntry(buffer, 0x240);
            ParentLocatorEntry2 = new ParentLocatorEntry(buffer, 0x258);
            ParentLocatorEntry3 = new ParentLocatorEntry(buffer, 0x270);
            ParentLocatorEntry4 = new ParentLocatorEntry(buffer, 0x288);
            ParentLocatorEntry5 = new ParentLocatorEntry(buffer, 0x2A0);
            ParentLocatorEntry6 = new ParentLocatorEntry(buffer, 0x2B8);
            ParentLocatorEntry7 = new ParentLocatorEntry(buffer, 0x2D0);
            ParentLocatorEntry8 = new ParentLocatorEntry(buffer, 0x2E8);

            byte[] temp = (byte[])buffer.Clone();
            BigEndianWriter.WriteInt32(temp, 0x24, 0);
            uint expectedChecksum = VHDFooter.CalculateChecksum(temp);
            m_isValid = String.Equals(Cookie, DynamidDiskHeaderCookie) && (checksum == expectedChecksum) && (HeaderVersion == 0x00010000);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0x00, Cookie, 8);
            BigEndianWriter.WriteUInt64(buffer, 0x08, DataOffset);
            BigEndianWriter.WriteUInt64(buffer, 0x10, TableOffset);
            BigEndianWriter.WriteUInt32(buffer, 0x18, HeaderVersion);
            BigEndianWriter.WriteUInt32(buffer, 0x1C, MaxTableEntries);
            BigEndianWriter.WriteUInt32(buffer, 0x20, BlockSize);
            // We'll write the checksum later
            BigEndianWriter.WriteGuidBytes(buffer, 0x28, ParentUniqueID);
            BigEndianWriter.WriteUInt32(buffer, 0x38, ParentTimeStamp);
            BigEndianWriter.WriteUInt32(buffer, 0x3C, Reserved);
            ByteWriter.WriteUTF16String(buffer, 0x40, ParentUnicodeName, 256);
            ParentLocatorEntry1.WriteBytes(buffer, 0x240);
            ParentLocatorEntry2.WriteBytes(buffer, 0x258);
            ParentLocatorEntry3.WriteBytes(buffer, 0x270);
            ParentLocatorEntry4.WriteBytes(buffer, 0x288);
            ParentLocatorEntry5.WriteBytes(buffer, 0x2A0);
            ParentLocatorEntry6.WriteBytes(buffer, 0x2B8);
            ParentLocatorEntry7.WriteBytes(buffer, 0x2D0);
            ParentLocatorEntry8.WriteBytes(buffer, 0x2E8);

            uint checksum = VHDFooter.CalculateChecksum(buffer);
            BigEndianWriter.WriteUInt32(buffer, 0x24, checksum);

            return buffer;
        }
    }
}
