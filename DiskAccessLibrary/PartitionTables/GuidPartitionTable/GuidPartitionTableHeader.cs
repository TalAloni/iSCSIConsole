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
    public class GuidPartitionTableHeader
    {
        public const string GuidPartitionTableSignature = "EFI PART";
        public const uint GuidPartitionTableRevision = 0x10000;
        public const long GuidPartitionTableHeaderLBA = 1;

        private string Signature;
        public uint Revision;
        public uint HeaderSize;
        private uint CRC32;
        // public uint Reserved
        public ulong CurrentLBA;
        public ulong BackupLBA;
        public ulong FirstUsableLBA; // for partitions
        public ulong LastUsableLBA; // for partitions
        public Guid DiskGuid;
        public ulong PartitionEntriesLBA;
        public uint NumberOfPartitionEntries;
        public uint SizeOfPartitionEntry;
        public uint PartitionArrayCRC32;

        public GuidPartitionTableHeader()
        {
            Signature = GuidPartitionTableSignature;
            Revision = GuidPartitionTableRevision;
        }

        public GuidPartitionTableHeader(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            Revision = LittleEndianConverter.ToUInt32(buffer, 8);
            HeaderSize = LittleEndianConverter.ToUInt32(buffer, 12);
            CRC32 = LittleEndianConverter.ToUInt32(buffer, 16);
            CurrentLBA = LittleEndianConverter.ToUInt64(buffer, 24);
            BackupLBA = LittleEndianConverter.ToUInt64(buffer, 32);
            FirstUsableLBA = LittleEndianConverter.ToUInt64(buffer, 40);
            LastUsableLBA = LittleEndianConverter.ToUInt64(buffer, 48);
            DiskGuid = LittleEndianConverter.ToGuid(buffer, 56);
            PartitionEntriesLBA = LittleEndianConverter.ToUInt64(buffer, 72);
            NumberOfPartitionEntries = LittleEndianConverter.ToUInt32(buffer, 80);
            SizeOfPartitionEntry = LittleEndianConverter.ToUInt32(buffer, 84);
            PartitionArrayCRC32 = LittleEndianConverter.ToUInt32(buffer, 88);
        }

        public byte[] GetBytes(int bytesPerSector)
        {
            byte[] buffer = new byte[bytesPerSector];
            ByteWriter.WriteAnsiString(buffer, 0, Signature, 8);
            LittleEndianWriter.WriteUInt32(buffer, 8, Revision);
            LittleEndianWriter.WriteUInt32(buffer, 12, HeaderSize);
            LittleEndianWriter.WriteUInt64(buffer, 24, CurrentLBA);
            LittleEndianWriter.WriteUInt64(buffer, 32, BackupLBA);
            LittleEndianWriter.WriteUInt64(buffer, 40, FirstUsableLBA);
            LittleEndianWriter.WriteUInt64(buffer, 48, LastUsableLBA);
            LittleEndianWriter.WriteGuid(buffer, 56, DiskGuid);
            LittleEndianWriter.WriteUInt64(buffer, 72, PartitionEntriesLBA);
            LittleEndianWriter.WriteUInt32(buffer, 80, NumberOfPartitionEntries);
            LittleEndianWriter.WriteUInt32(buffer, 84, SizeOfPartitionEntry);
            LittleEndianWriter.WriteUInt32(buffer, 88, PartitionArrayCRC32);

            CRC32 = ComputeCRC32(buffer);
            LittleEndianWriter.WriteUInt32(buffer, 16, CRC32);

            return buffer;
        }

        public GuidPartitionTableHeader Clone()
        {
            GuidPartitionTableHeader clone = (GuidPartitionTableHeader)this.MemberwiseClone();
            return clone;
        }

        public static uint ComputeCRC32(byte[] buffer)
        {
            uint headerSize = LittleEndianConverter.ToUInt32(buffer, 12);
            byte[] temp = new byte[headerSize];
            Array.Copy(buffer, temp, headerSize);
            uint crc32 = 0;
            LittleEndianWriter.WriteUInt32(temp, 16, crc32);
            return Utilities.CRC32.Compute(temp);
        }

        public static GuidPartitionTableHeader ReadFromDisk(Disk disk)
        {
            GuidPartitionTableHeader header = ReadPrimaryFromDisk(disk);
            if (header == null)
            {
                // try reading secondary GPT header:
                header = ReadSecondaryFromDisk(disk, header);
            }
            return header;
        }

        public static GuidPartitionTableHeader ReadPrimaryFromDisk(Disk disk)
        {
            return ReadFromDisk(disk, GuidPartitionTableHeaderLBA);
        }

        /// <summary>
        /// primaryHeader can be NULL
        /// </summary>
        public static GuidPartitionTableHeader ReadSecondaryFromDisk(Disk disk, GuidPartitionTableHeader primaryHeader)
        {
            if (primaryHeader == null)
            {
                return ReadFromDisk(disk, disk.TotalSectors - 1);
            }
            else
            {
                // The secondary GPT header is supposed to be located in the last sector of the disk,
                // however, this is not always the case, the disk could have been extended or the backup GPT scheme was written in an uncommon way
                return ReadFromDisk(disk, (long)primaryHeader.BackupLBA);
            }
        }

        public static GuidPartitionTableHeader ReadFromDisk(Disk disk, long sectorIndex)
        {
            byte[] buffer = disk.ReadSector(sectorIndex);
            string signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            uint revision = LittleEndianConverter.ToUInt32(buffer, 8);
            uint crc32 = LittleEndianConverter.ToUInt32(buffer, 16);
            if (signature == GuidPartitionTableSignature &&
                revision == GuidPartitionTableRevision &&
                crc32 == ComputeCRC32(buffer))
            {
                return new GuidPartitionTableHeader(buffer);
            }
            return null;
        }

        /// <summary>
        /// GuidPartitionTableHeader.CurrentLBA Will be used as the writing location,
        /// This will help preserve cases where the backup GPT scheme was written in an uncommon way.
        /// </summary>
        public static void WriteToDisk(Disk disk, GuidPartitionTableHeader header)
        {
            disk.WriteSectors((long)header.CurrentLBA, header.GetBytes(disk.BytesPerSector));
        }
    }
}
