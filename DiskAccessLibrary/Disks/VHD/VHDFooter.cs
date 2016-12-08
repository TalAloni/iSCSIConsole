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
    public class VHDFooter
    {
        public const int Length = 512;
        public const string VirtualHardDiskCookie = "conectix";

        public string Cookie; // 8 bytes
        public uint Features = 0x2;
        public uint FileFormatVersion;
        public ulong DataOffset; // The documentation says 0xFFFFFFFF, but all programs use 0xFFFFFFFFFFFFFFFF
        public uint TimeStamp; // Number of seconds since January 1, 2000 12:00:00 AM in UTC
        public string CreatorApplication;
        public uint CreatorVersion;
        public uint CreatorHostOS; // Windows
        public ulong OriginalSize;
        public ulong CurrentSize;
        public uint DiskGeometry;
        public VirtualHardDiskType DiskType;
        //public uint Checksum;
        public Guid UniqueId;
        public byte SavedState;

        private bool m_isValid = true;

        public VHDFooter()
        {
            Cookie = VirtualHardDiskCookie;
            FileFormatVersion = 0x00010000;
            DataOffset = 0xFFFFFFFFFFFFFFFF;
            CreatorApplication = "DXSL"; // Disk Access Library
            CreatorHostOS = 0x5769326B;
            DiskType = VirtualHardDiskType.Fixed;
            UniqueId = Guid.NewGuid();
        }

        public VHDFooter(byte[] buffer)
        {
            Cookie = ByteReader.ReadAnsiString(buffer, 0x00, 8);
            Features = BigEndianConverter.ToUInt32(buffer, 0x08);
            FileFormatVersion = BigEndianConverter.ToUInt32(buffer, 0x0C);
            DataOffset = BigEndianConverter.ToUInt64(buffer, 0x10);
            TimeStamp = BigEndianConverter.ToUInt32(buffer, 0x18);
            CreatorApplication = ByteReader.ReadAnsiString(buffer, 0x1C, 4);
            CreatorVersion = BigEndianConverter.ToUInt32(buffer, 0x20);
            CreatorHostOS = BigEndianConverter.ToUInt32(buffer, 0x24);
            OriginalSize = BigEndianConverter.ToUInt64(buffer, 0x28);
            CurrentSize = BigEndianConverter.ToUInt64(buffer, 0x30);
            DiskGeometry = BigEndianConverter.ToUInt32(buffer, 0x38);
            DiskType = (VirtualHardDiskType)BigEndianConverter.ToUInt32(buffer, 0x3C);
            uint checksum = BigEndianConverter.ToUInt32(buffer, 0x40);

            UniqueId = BigEndianConverter.ToGuid(buffer, 0x44);
            SavedState = ByteReader.ReadByte(buffer, 0x54);

            byte[] temp = (byte[])buffer.Clone();
            BigEndianWriter.WriteInt32(temp, 0x40, 0);
            uint expectedChecksum = CalculateChecksum(temp);
            m_isValid = String.Equals(Cookie, VirtualHardDiskCookie) && (checksum == expectedChecksum) && (FileFormatVersion == 0x00010000);
        }

        public void SetCurrentTimeStamp()
        {
            TimeSpan since2000 = DateTime.UtcNow - new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeStamp = (uint)since2000.TotalSeconds;
        }

        public void SetDiskGeometry(ulong totalSectors)
        {
            byte heads;
            byte sectorsPerTrack;
            ushort cylinders;
            VirtualHardDisk.GetDiskGeometry(totalSectors, out heads, out sectorsPerTrack, out cylinders);
            DiskGeometry = (uint)cylinders << 16;
            DiskGeometry |= (uint)heads << 8;
            DiskGeometry |= sectorsPerTrack;
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0x00, Cookie, 8);
            BigEndianWriter.WriteUInt32(buffer, 0x08, Features);
            BigEndianWriter.WriteUInt32(buffer, 0x0C, FileFormatVersion);
            BigEndianWriter.WriteUInt64(buffer, 0x10, DataOffset);
            BigEndianWriter.WriteUInt32(buffer, 0x18, TimeStamp);
            ByteWriter.WriteAnsiString(buffer, 0x1C, CreatorApplication, 4);
            BigEndianWriter.WriteUInt32(buffer, 0x20, CreatorVersion);
            BigEndianWriter.WriteUInt32(buffer, 0x24, CreatorHostOS);
            BigEndianWriter.WriteUInt64(buffer, 0x28, OriginalSize);
            BigEndianWriter.WriteUInt64(buffer, 0x30, CurrentSize);
            BigEndianWriter.WriteUInt32(buffer, 0x38, DiskGeometry);
            BigEndianWriter.WriteUInt32(buffer, 0x3C, (uint)DiskType);
            // We'll write the checksum later
            BigEndianWriter.WriteGuidBytes(buffer, 0x44, UniqueId);
            ByteWriter.WriteByte(buffer, 0x54, SavedState);

            uint checksum = CalculateChecksum(buffer);
            BigEndianWriter.WriteUInt32(buffer, 0x40, checksum);

            return buffer;
        }

        public bool IsValid
        {
            get
            {
                return m_isValid;
            }
        }

        public static uint CalculateChecksum(byte[] buffer)
        {
            uint checksum = 0;
            for (int index = 0; index < Length; index++)
            {
                checksum += buffer[index];
            }

            checksum = ~checksum;

            return checksum;
        }
    }
}
