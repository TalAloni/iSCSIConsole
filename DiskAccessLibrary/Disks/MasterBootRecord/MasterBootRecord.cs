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
    public class MasterBootRecord
    {
        public const ushort ValidMBRSignature = 0xAA55;
        public const int NumberOfPartitionEntries = 4;

        public byte[] Code = new byte[440];
        public uint DiskSignature;
        public PartitionTableEntry[] PartitionTable = new PartitionTableEntry[4];
        public ushort MBRSignature;

        public MasterBootRecord()
        {
            for (int index = 0; index < NumberOfPartitionEntries; index++)
            {
                PartitionTable[index] = new PartitionTableEntry();
            }
        }

        public MasterBootRecord(byte[] buffer)
        {
            Array.Copy(buffer, Code, 440);
            DiskSignature = LittleEndianConverter.ToUInt32(buffer, 440);
            int offset = 446;
            for (int index = 0; index < NumberOfPartitionEntries; index++)
            {
                PartitionTable[index] = new PartitionTableEntry(buffer, offset);
                offset += 16;
            }
            MBRSignature = LittleEndianConverter.ToUInt16(buffer, 510);
        }

        public byte[] GetBytes(int sectorSize)
        {
            byte[] buffer = new byte[sectorSize];
            ByteWriter.WriteBytes(buffer, 0, Code, Math.Min(Code.Length, 440));
            LittleEndianWriter.WriteUInt32(buffer, 440, DiskSignature);
            
            int offset = 446;
            for (int index = 0; index < NumberOfPartitionEntries; index++)
            {
                PartitionTable[index].WriteBytes(buffer, offset);
                offset += PartitionTableEntry.Length;
            }

            LittleEndianWriter.WriteUInt16(buffer, 510, MBRSignature);

            return buffer;
        }

        public bool IsGPTBasedDisk
        {
            get
            {
                return (PartitionTable[0].PartitionTypeName == PartitionTypeName.EFIGPT);
            }
        }

        public static MasterBootRecord ReadFromDisk(Disk disk)
        {
            byte[] buffer = disk.ReadSector(0);
            ushort signature = LittleEndianConverter.ToUInt16(buffer, 510);
            if (signature == ValidMBRSignature)
            {
                return new MasterBootRecord(buffer);
            }
            else
            {
                return null;
            }
        }

        public static void WriteToDisk(Disk disk, MasterBootRecord mbr)
        {
            byte[] buffer = mbr.GetBytes(disk.BytesPerSector);
            disk.WriteSectors(0, buffer);
        }
    }
}
