/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// StandardInformation attribute is always resident.
    /// </remarks>
    public class StandardInformationRecord : ResidentAttributeRecord
    {
        public const int RecordDataLengthNTFS12 = 0x30;
        public const int RecordDataLengthNTFS30 = 0x48; // Note: even on NTFS 3.x, a few metafiles will use shorter length records.

        public DateTime CreationTime;        // File creation time
        public DateTime ModificationTime;    // Last time the DATA attribute was modified
        public DateTime MftModificationTime; // Last time any attribute was modified.
        public DateTime LastAccessTime;      // Last time the file was accessed.
        public FileAttributes FileAttributes;
        public uint MaximumVersionNumber;
        public uint VersionNumber;
        public uint ClassID; // NTFS v3.0+
        public uint OwnerID; // NTFS v3.0+
        public uint SecurityID; // NTFS v3.0+
        public ulong QuotaCharged; // NTFS v3.0+
        public ulong UpdateSequenceNumber; // a.k.a. USN, NTFS v3.0+

        public StandardInformationRecord(string name, ushort instance) : base(AttributeType.StandardInformation, name, instance)
        {
        }

        public StandardInformationRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            CreationTime = ReadDateTime(this.Data, 0x00);
            ModificationTime = ReadDateTime(this.Data, 0x08);
            MftModificationTime = ReadDateTime(this.Data, 0x10);
            LastAccessTime = ReadDateTime(this.Data, 0x18);
            FileAttributes = (FileAttributes)LittleEndianConverter.ToUInt32(this.Data, 0x20);
            MaximumVersionNumber = LittleEndianConverter.ToUInt32(this.Data, 0x24);
            VersionNumber = LittleEndianConverter.ToUInt32(this.Data, 0x28);
            if (this.Data.Length == RecordDataLengthNTFS30)
            {
                ClassID = LittleEndianConverter.ToUInt32(this.Data, 0x2C);
                OwnerID = LittleEndianConverter.ToUInt32(this.Data, 0x30);
                SecurityID = LittleEndianConverter.ToUInt32(this.Data, 0x34);
                QuotaCharged = LittleEndianConverter.ToUInt64(this.Data, 0x38);
                UpdateSequenceNumber = LittleEndianConverter.ToUInt64(this.Data, 0x40);
            }
        }

        public override byte[] GetBytes()
        {
            this.Data = new byte[this.DataLength];
            WriteDateTime(this.Data, 0x00, CreationTime);
            WriteDateTime(this.Data, 0x08, ModificationTime);
            WriteDateTime(this.Data, 0x10, MftModificationTime);
            WriteDateTime(this.Data, 0x18, LastAccessTime);
            LittleEndianWriter.WriteUInt32(this.Data, 0x20, (uint)FileAttributes);
            LittleEndianWriter.WriteUInt32(this.Data, 0x24, MaximumVersionNumber);
            LittleEndianWriter.WriteUInt32(this.Data, 0x28, VersionNumber);
            LittleEndianWriter.WriteUInt32(this.Data, 0x2C, ClassID);
            LittleEndianWriter.WriteUInt32(this.Data, 0x30, OwnerID);
            LittleEndianWriter.WriteUInt32(this.Data, 0x34, SecurityID);
            LittleEndianWriter.WriteUInt64(this.Data, 0x38, QuotaCharged);
            LittleEndianWriter.WriteUInt64(this.Data, 0x40, UpdateSequenceNumber);

            return base.GetBytes();
        }

        public override ulong DataLength
        {
            get
            {
                return RecordDataLengthNTFS30;
            }
        }

        public static DateTime ReadDateTime(byte[] buffer, int offset)
        {
            try
            {
                return DateTime.FromFileTimeUtc(LittleEndianConverter.ToInt64(buffer, offset));
            }
            catch (ArgumentException)
            {
                return DateTime.MinValue;
            }
        }

        public static void WriteDateTime(byte[] buffer, int offset, DateTime value)
        {
            LittleEndianWriter.WriteInt64(buffer, offset, value.ToFileTimeUtc());
        }
    }
}
