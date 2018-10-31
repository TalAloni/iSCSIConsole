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
    /// DUPLICATED_INFORMATION
    /// </summary>
    public class DuplicatedInformation
    {
        public const int Length = 0x38;

        public DateTime CreationTime;
        public DateTime ModificationTime;
        public DateTime MftModificationTime;
        public DateTime LastAccessTime;
        public ulong AllocatedLength; // of the file
        public ulong FileSize; // of the file
        public FileAttributes FileAttributes;
        public ushort PackedEASize;
        // ushort Reserved;

        public DuplicatedInformation()
        {
        }

        public DuplicatedInformation(byte[] buffer, int offset)
        {
            CreationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x00);
            ModificationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x08);
            MftModificationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x10);
            LastAccessTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x18);
            AllocatedLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x20);
            FileSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x28);
            FileAttributes = (FileAttributes)LittleEndianConverter.ToUInt32(buffer, offset + 0x30);
            PackedEASize = LittleEndianConverter.ToUInt16(buffer, offset + 0x34);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];

            StandardInformationRecord.WriteDateTime(buffer, 0x00, CreationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x08, ModificationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x10, MftModificationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x18, LastAccessTime);
            LittleEndianWriter.WriteUInt64(buffer, 0x20, AllocatedLength);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, FileSize);
            LittleEndianWriter.WriteUInt32(buffer, 0x30, (uint)FileAttributes);
            LittleEndianWriter.WriteUInt16(buffer, 0x34, PackedEASize);

            return buffer;
        }
    }
}
