/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// <summary>
    /// RESTART_AREA
    /// </summary>
    /// <remarks>
    /// Windows NT 4.0 writes records that are 64 bytes long.
    /// Windows 2000 / XP / 2003 write records that are 104 bytes long.
    /// Windows Vista / 7 / 8 / 10 write records that are 112 bytes long.
    /// 
    /// When x64 versions of Windows format the volume they set the version to 1.0 (x86 will set the version to 0.0).
    /// Note that the record version will be maintained when moving disks between operating systems using the same record length.
    /// </remarks>
    public class NTFSRestartRecord
    {
        public const int LengthNTFS12 = 64;
        public const int LengthNTFS30 = 104;

        public uint MajorVersion;
        public uint MinorVersion;
        public ulong StartOfCheckpointLsn;
        public ulong OpenAttributeTableLsn;
        public ulong AttributeNamesLsn;
        public ulong DirtyPageTableLsn;
        public ulong TransactionTableLsn;
        public uint OpenAttributeTableLength;
        public uint AttributeNamesLength;
        public uint DirtyPageTableLength;
        public uint TransactionTableLength;

        public NTFSRestartRecord()
        {
            MajorVersion = 1;
            MinorVersion = 0;
        }

        public NTFSRestartRecord(byte[] recordBytes)
        {
            MajorVersion = LittleEndianConverter.ToUInt32(recordBytes, 0x00);
            MinorVersion = LittleEndianConverter.ToUInt32(recordBytes, 0x04);
            StartOfCheckpointLsn = LittleEndianConverter.ToUInt64(recordBytes, 0x08);
            OpenAttributeTableLsn = LittleEndianConverter.ToUInt64(recordBytes, 0x10);
            AttributeNamesLsn = LittleEndianConverter.ToUInt64(recordBytes, 0x18);
            DirtyPageTableLsn = LittleEndianConverter.ToUInt64(recordBytes, 0x20);
            TransactionTableLsn = LittleEndianConverter.ToUInt64(recordBytes, 0x28);
            OpenAttributeTableLength = LittleEndianConverter.ToUInt32(recordBytes, 0x30);
            AttributeNamesLength = LittleEndianConverter.ToUInt32(recordBytes, 0x34);
            DirtyPageTableLength = LittleEndianConverter.ToUInt32(recordBytes, 0x38);
            TransactionTableLength = LittleEndianConverter.ToUInt32(recordBytes, 0x3C);
        }

        public byte[] GetBytes(ushort majorNTFSVersion)
        {
            int length = (majorNTFSVersion == 3) ? LengthNTFS30 : LengthNTFS12;
            byte[] recordBytes = new byte[LengthNTFS30];
            LittleEndianWriter.WriteUInt32(recordBytes, 0x00, MajorVersion);
            LittleEndianWriter.WriteUInt32(recordBytes, 0x04, MinorVersion);
            LittleEndianWriter.WriteUInt64(recordBytes, 0x08, StartOfCheckpointLsn);
            LittleEndianWriter.WriteUInt64(recordBytes, 0x10, OpenAttributeTableLsn);
            LittleEndianWriter.WriteUInt64(recordBytes, 0x18, AttributeNamesLsn);
            LittleEndianWriter.WriteUInt64(recordBytes, 0x20, DirtyPageTableLsn);
            LittleEndianWriter.WriteUInt64(recordBytes, 0x28, TransactionTableLsn);
            LittleEndianWriter.WriteUInt32(recordBytes, 0x30, OpenAttributeTableLength);
            LittleEndianWriter.WriteUInt32(recordBytes, 0x30, AttributeNamesLength);
            LittleEndianWriter.WriteUInt32(recordBytes, 0x30, DirtyPageTableLength);
            LittleEndianWriter.WriteUInt32(recordBytes, 0x30, TransactionTableLength);
            return recordBytes;
        }
    }
}
