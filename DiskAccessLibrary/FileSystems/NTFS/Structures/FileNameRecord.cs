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
    // FileName attribute is always resident
    // This is the record itself (the data that is contained in the attribute / index key)
    public class FileNameRecord
    {
        public const int FixedLength = 0x42;

        public MftSegmentReference ParentDirectory;
        public DateTime CreationTime;
        public DateTime ModificationTime;
        public DateTime MftModificationTime;
        public DateTime LastAccessTime;
        public ulong AllocatedSize; // of the file
        public ulong RealSize; // of the file
        // byte FileNameLength
        public FilenameNamespace Namespace; // Type of filename (e.g. 8.3, long filename etc.)
        public string FileName;

        public FileNameRecord(byte[] buffer, int offset)
        {
            ParentDirectory = new MftSegmentReference(buffer, offset + 0x00);
            CreationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x08);
            ModificationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x10);
            MftModificationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x18);
            LastAccessTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x20);
            AllocatedSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x28);
            RealSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x30);
            byte fnLen = ByteReader.ReadByte(buffer, offset + 0x40);
            Namespace = (FilenameNamespace)ByteReader.ReadByte(buffer, offset + 0x41);
            FileName = Encoding.Unicode.GetString(buffer, offset + 0x42, fnLen * 2);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[FixedLength + FileName.Length * 2];

            ParentDirectory.WriteBytes(buffer, 0x00);
            StandardInformationRecord.WriteDateTime(buffer, 0x08, CreationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x10, ModificationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x18, MftModificationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x20, LastAccessTime);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, AllocatedSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, RealSize);
            ByteWriter.WriteByte(buffer, 0x40, (byte)FileName.Length);
            ByteWriter.WriteByte(buffer, 0x41, (byte)Namespace);
            ByteWriter.WriteBytes(buffer, 0x42, Encoding.Unicode.GetBytes(FileName));

            return buffer;
        }
    }
}
