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

namespace DiskAccessLibrary
{
    public class GuidPartitionEntry
    {
        // length is defined in GPT Header

        public Guid PartitionTypeGuid;
        public Guid PartitionGuid;
        public ulong FirstLBA;
        public ulong LastLBA;
        public ulong AttributeFlags;
        public string PartitionName;

        public int EntryIndex; // We may use this later for write operations

        public GuidPartitionEntry()
        {
            PartitionName = String.Empty;
        }

        public GuidPartitionEntry(byte[] buffer, int offset)
        {
            PartitionTypeGuid = LittleEndianConverter.ToGuid(buffer, offset + 0);
            PartitionGuid = LittleEndianConverter.ToGuid(buffer, offset + 16);
            FirstLBA = LittleEndianConverter.ToUInt64(buffer, offset + 32);
            LastLBA = LittleEndianConverter.ToUInt64(buffer, offset + 40);
            AttributeFlags = LittleEndianConverter.ToUInt64(buffer, offset + 48);
            PartitionName = UnicodeEncoding.Unicode.GetString(ByteReader.ReadBytes(buffer, offset + 56, 72)).TrimEnd('\0');
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteGuidBytes(buffer, offset + 0, PartitionTypeGuid);
            LittleEndianWriter.WriteGuidBytes(buffer, offset + 16, PartitionGuid);
            LittleEndianWriter.WriteUInt64(buffer, offset + 32, FirstLBA);
            LittleEndianWriter.WriteUInt64(buffer, offset + 40, LastLBA);
            LittleEndianWriter.WriteUInt64(buffer, offset + 48, AttributeFlags);
            while (PartitionName.Length < 36)
            {
                PartitionName += "\0";
            }
            ByteWriter.WriteUTF16String(buffer, offset + 56, PartitionName, 36);
        }

        public ulong SizeLBA
        {
            get
            {
                return LastLBA - FirstLBA + 1;
            }
        }

        public static void WriteToDisk(Disk disk, GuidPartitionTableHeader header, GuidPartitionEntry entry)
        {
            long sectorIndex = (long)header.PartitionEntriesLBA + entry.EntryIndex * header.SizeOfPartitionEntry / disk.BytesPerSector;
            int entriesPerSector = (int)(disk.BytesPerSector / header.SizeOfPartitionEntry);
            int indexInSector = (int)(entry.EntryIndex % entriesPerSector);
            byte[] buffer = disk.ReadSector(sectorIndex);
            entry.WriteBytes(buffer, indexInSector * (int)header.SizeOfPartitionEntry);
            disk.WriteSectors(sectorIndex, buffer);
        }
    }
}
