/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace DiskAccessLibrary
{
    public class GuidPartitionTable
    {
        public static List<GuidPartitionEntry> ReadEntriesFromDisk(Disk disk)
        {
            GuidPartitionTableHeader primaryHeader = GuidPartitionTableHeader.ReadPrimaryFromDisk(disk);
            if (primaryHeader != null)
            {
                List<GuidPartitionEntry> result = ReadEntriesFromDisk(disk, primaryHeader);
                if (result != null)
                {
                    return result;
                }
            }

            GuidPartitionTableHeader secondaryHeader = GuidPartitionTableHeader.ReadSecondaryFromDisk(disk, primaryHeader);
            if (secondaryHeader != null)
            {
                return ReadEntriesFromDisk(disk, secondaryHeader);
            }
            return null;
        }

        private static List<GuidPartitionEntry> ReadEntriesFromDisk(Disk disk, GuidPartitionTableHeader header)
        {
            int bufferLength = (int)(header.NumberOfPartitionEntries * header.SizeOfPartitionEntry);
            int sectorsToRead = (int)Math.Ceiling((double)bufferLength / disk.BytesPerSector);
            byte[] buffer = disk.ReadSectors((long)header.PartitionEntriesLBA, sectorsToRead);
            if (buffer.Length > bufferLength)
            {
                buffer = ByteReader.ReadBytes(buffer, 0, bufferLength);
            }
            uint expectedCRC32 = CRC32.Compute(buffer);
            if (header.PartitionArrayCRC32 != expectedCRC32)
            {
                return null;
            }
            
            int offset = 0;
            List<GuidPartitionEntry> result = new List<GuidPartitionEntry>();
            for (int index = 0; index < header.NumberOfPartitionEntries; index++)
            {
                GuidPartitionEntry entry = new GuidPartitionEntry(buffer, offset);
                entry.EntryIndex = index;
                // Unused entries use Guid.Empty as PartitionTypeGuid
                if (entry.PartitionTypeGuid != Guid.Empty)
                {
                    result.Add(entry);
                }
                offset += (int)header.SizeOfPartitionEntry;
            }
            return result;
        }

        public static uint ComputePartitionArrayCRC32(Disk disk, GuidPartitionTableHeader header)
        {
            int sectorsToRead = (int)Math.Ceiling((double)header.NumberOfPartitionEntries * header.SizeOfPartitionEntry / disk.BytesPerSector);
            byte[] buffer = disk.ReadSectors((long)header.PartitionEntriesLBA, sectorsToRead);
            return CRC32.Compute(buffer);
        }

        public static void InitializeDisk(Disk disk, long firstUsableLBA, long reservedPartitionSizeLBA)
        {
            if (reservedPartitionSizeLBA > 0 && reservedPartitionSizeLBA * disk.BytesPerSector < 1024 * 1024)
            {
                // The LDM database will take 1MB out of the reserved partition.
                // less than 1MB will cause the conversion to dynamic disk to fail.
                throw new ArgumentException("Reserved partition size must be at least 1MB");
            }

            List<GuidPartitionEntry> partitionEntries = new List<GuidPartitionEntry>();
            if (reservedPartitionSizeLBA > 0)
            {
                GuidPartitionEntry reservedEntry = new GuidPartitionEntry();
                reservedEntry.PartitionGuid = Guid.NewGuid();
                reservedEntry.PartitionTypeGuid = GPTPartition.MicrosoftReservedPartititionTypeGuid;
                reservedEntry.FirstLBA = (ulong)firstUsableLBA;
                reservedEntry.LastLBA = (ulong)(firstUsableLBA + reservedPartitionSizeLBA - 1);
                reservedEntry.PartitionName = "Microsoft reserved partition";
                partitionEntries.Add(reservedEntry);
            }
            InitializeDisk(disk, firstUsableLBA, partitionEntries);
        }

        public static void InitializeDisk(Disk disk, long firstUsableLBA, List<GuidPartitionEntry> partitionEntries)
        {
            MasterBootRecord mbr = new MasterBootRecord();
            mbr.DiskSignature = (uint)new Random().Next(Int32.MaxValue);
            mbr.PartitionTable[0].PartitionTypeName = PartitionTypeName.EFIGPT;
            mbr.PartitionTable[0].FirstSectorLBA = 1;
            mbr.PartitionTable[0].SectorCountLBA = (uint)Math.Min(disk.TotalSectors - firstUsableLBA, UInt32.MaxValue);
            mbr.MBRSignature = 0xAA55;
            MasterBootRecord.WriteToDisk(disk, mbr);

            const int DefaultNumberOfEntries = 128;
            const int DefaultSizeOfEntry = 128;
            int partitionEntriesLength = DefaultNumberOfEntries * DefaultSizeOfEntry;
            long partitionEntriesPrimaryLBA = 2;
            long partitionEntriesSecondaryLBA = disk.TotalSectors - 1 - partitionEntriesLength / disk.BytesPerSector;

            GuidPartitionTableHeader primaryHeader = new GuidPartitionTableHeader();
            primaryHeader.HeaderSize = 92;
            primaryHeader.CurrentLBA = 1;
            primaryHeader.BackupLBA = (ulong)(disk.TotalSectors - 1);
            primaryHeader.DiskGuid = Guid.NewGuid();
            primaryHeader.FirstUsableLBA = (ulong)firstUsableLBA;
            primaryHeader.LastUsableLBA = (ulong)(partitionEntriesSecondaryLBA - 1);
            primaryHeader.PartitionEntriesLBA = (ulong)partitionEntriesPrimaryLBA;
            primaryHeader.NumberOfPartitionEntries = DefaultNumberOfEntries;
            primaryHeader.SizeOfPartitionEntry = DefaultSizeOfEntry;
            byte[] partitionTableEntries = new byte[partitionEntriesLength];

            for(int index = 0; index < partitionEntries.Count; index++)
            {
                partitionEntries[index].WriteBytes(partitionTableEntries, index * DefaultSizeOfEntry);
            }
            primaryHeader.PartitionArrayCRC32 = CRC32.Compute(partitionTableEntries);

            GuidPartitionTableHeader secondaryHeader = primaryHeader.Clone();
            secondaryHeader.CurrentLBA = (ulong)(disk.TotalSectors - 1);
            secondaryHeader.BackupLBA = 1;
            secondaryHeader.PartitionEntriesLBA = (ulong)partitionEntriesSecondaryLBA;

            GuidPartitionTableHeader.WriteToDisk(disk, primaryHeader);
            disk.WriteSectors(partitionEntriesPrimaryLBA, partitionTableEntries);
            GuidPartitionTableHeader.WriteToDisk(disk, secondaryHeader);
            disk.WriteSectors(partitionEntriesSecondaryLBA, partitionTableEntries);
        }

        /// <summary>
        /// Read valid GPT (header and partition table), and write it to the correct locations at the beginning and end of the disk.
        /// The protective MBR partition size will be updated as well.
        /// </summary>
        public static void RebaseDisk(Disk disk, MasterBootRecord mbr)
        {
            GuidPartitionTableHeader primaryHeader = GuidPartitionTableHeader.ReadPrimaryFromDisk(disk);
            GuidPartitionTableHeader secondaryHeader = null;
            List<GuidPartitionEntry> entries = null;
            if (primaryHeader != null)
            {
                entries = ReadEntriesFromDisk(disk, primaryHeader);
            }
            
            if (primaryHeader == null || entries == null)
            {
                secondaryHeader = GuidPartitionTableHeader.ReadSecondaryFromDisk(disk, primaryHeader);
                if (secondaryHeader != null)
                {
                    entries = ReadEntriesFromDisk(disk, secondaryHeader);
                }
            }

            if (entries == null)
            {
                throw new InvalidDataException("Both the primary and secondary GPT are corrupted");
            }

            if (secondaryHeader != null)
            {
                primaryHeader = secondaryHeader.Clone();
            }
            else
            {
                secondaryHeader = primaryHeader.Clone();
            }

            byte[] partitionTableEntries = GuidPartitionEntryCollection.GetBytes(primaryHeader, entries);

            int partitionEntriesLength = (int)(primaryHeader.NumberOfPartitionEntries * primaryHeader.SizeOfPartitionEntry);
            long partitionEntriesPrimaryLBA = 2;
            long partitionEntriesSecondaryLBA = disk.TotalSectors - 1 - partitionEntriesLength / disk.BytesPerSector;

            // If the disk was trimmed or converted to GPT without a secondary header, we don't want to overwrite partition data
            bool writeSecondaryGPT = primaryHeader.LastUsableLBA <= (ulong)(partitionEntriesSecondaryLBA - 1);

            primaryHeader.CurrentLBA = 1;
            primaryHeader.BackupLBA = (ulong)(disk.TotalSectors - 1);
            primaryHeader.PartitionEntriesLBA = (ulong)partitionEntriesPrimaryLBA;
            primaryHeader.LastUsableLBA = (ulong)(partitionEntriesSecondaryLBA - 1);
            
            secondaryHeader.CurrentLBA = (ulong)(disk.TotalSectors - 1);
            secondaryHeader.BackupLBA = 1;
            secondaryHeader.PartitionEntriesLBA = (ulong)partitionEntriesSecondaryLBA;
            secondaryHeader.LastUsableLBA = (ulong)(partitionEntriesSecondaryLBA - 1);

            // Update protective MBR partition size
            uint firstUsableLBA = mbr.PartitionTable[0].FirstSectorLBA;
            mbr.PartitionTable[0].SectorCountLBA = (uint)Math.Min(disk.TotalSectors - firstUsableLBA, UInt32.MaxValue);
            MasterBootRecord.WriteToDisk(disk, mbr);

            // Write primary and secondary GPT
            GuidPartitionTableHeader.WriteToDisk(disk, primaryHeader);
            disk.WriteSectors(partitionEntriesPrimaryLBA, partitionTableEntries);
            if (writeSecondaryGPT)
            {
                GuidPartitionTableHeader.WriteToDisk(disk, secondaryHeader);
                disk.WriteSectors(partitionEntriesSecondaryLBA, partitionTableEntries);
            }
        }
    }
}
