/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    [Flags]
    // as reported by DMDiag
    public enum PrivateHeaderFlags : uint
    {
        Shared = 0x01,        // private when the flag is absent
        NoAutoImport = 0x02,  // autoimport when the flag is absent
    }

    public class PrivateHeader
    {
        public const int Length = 512;
        public const int PrivateHeaderSectorIndex = 6;
        public const string PrivateHeaderSignature = "PRIVHEAD";

        public string Signature = PrivateHeaderSignature;
        //private uint Checksum;
        // Versions: 2.11 for Windows XP \ Server 2003
        //           2.12 for Windows 7 \ Server 2008
        //           2.12 for Veritas Storage Foundation 4.0 (regardless of whether Windows Disk Management compatible group is checked or not)
        public ushort MajorVersion;
        public ushort MinorVersion;
        public DateTime LastUpdateDT; // Set when UpdateSequenceNumber is being updated
        public ulong UpdateSequenceNumber; // as reported by DMDiag, kept in sync with UpdateSequenceNumber of the most recent TOCBlock 
        public ulong PrimaryPrivateHeaderLBA; // Within the private region
        public ulong SecondaryPrivateHeaderLBA; // Within the private region
        public string DiskGuidString;
        public string HostGuidString;
        public string DiskGroupGuidString;
        public string DiskGroupName; // Veritas will limit DiskGroupName to 18 characters, Windows will use the NetBIOS name (limited to 15 characters) and will append 'Dg0'
        public uint BytesPerSector; // a.k.a. iosize
        public PrivateHeaderFlags Flags;
        public ushort PublicRegionSliceNumber;
        public ushort PrivateRegionSliceNumber;
        /// <summary>
        /// MBR: Windows XP / Server 2003 will ignore this value and will use the first sector of the second disk track (usually sector number 63, as there are usually 63 sectors per track [0-62])
        /// </summary>
        public ulong PublicRegionStartLBA;
        public ulong PublicRegionSizeLBA;
        public ulong PrivateRegionStartLBA;
        public ulong PrivateRegionSizeLBA;
        /// <summary>
        /// PrimaryTocLBA / SecondaryTocLBA: on write operation, the updated TOC will be written to a different location,
        /// and then PrimaryTocLBA / SecondaryTocLBA will be updated to point to it.
        /// </summary>
        public ulong PrimaryTocLBA;
        public ulong SecondaryTocLBA;
        public uint NumberOfConfigs; // in private region?
        public uint NumberOfLogs;    // in private region?
        public ulong ConfigSizeLBA;  // all config regions in private region in total?
        public ulong LogSizeLBA;     // all log regions in private region in total?
        public uint DiskSignature;
        public Guid DiskSetGuid;
        public Guid DiskSetGuidRepeat;

        private bool m_isChecksumValid;

        public PrivateHeader(byte[] buffer)
        {
            if (buffer.Length > Length)
            {
                // Checksum only applies to the first 512 bytes (even when the sector size > 512 bytes)
                buffer = ByteReader.ReadBytes(buffer, 0, 512);
            }
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 8);
            uint checksum = BigEndianConverter.ToUInt32(buffer, 0x08);
            MajorVersion = BigEndianConverter.ToUInt16(buffer, 0x0C);
            MinorVersion = BigEndianConverter.ToUInt16(buffer, 0x0E);
            LastUpdateDT = DateTime.FromFileTimeUtc(BigEndianConverter.ToInt64(buffer, 0x10));
            UpdateSequenceNumber = BigEndianConverter.ToUInt64(buffer, 0x18);
            PrimaryPrivateHeaderLBA = BigEndianConverter.ToUInt64(buffer, 0x20);
            SecondaryPrivateHeaderLBA = BigEndianConverter.ToUInt64(buffer, 0x28);
            DiskGuidString = ByteReader.ReadAnsiString(buffer, 0x30, 0x40).Trim('\0');
            HostGuidString = ByteReader.ReadAnsiString(buffer, 0x70, 0x40).Trim('\0');
            DiskGroupGuidString = ByteReader.ReadAnsiString(buffer, 0xB0, 0x40).Trim('\0');
            DiskGroupName = ByteReader.ReadAnsiString(buffer, 0xF0, 31).Trim('\0');
            BytesPerSector = BigEndianConverter.ToUInt32(buffer, 0x10F);
            Flags = (PrivateHeaderFlags)BigEndianConverter.ToUInt32(buffer, 0x113);
            PublicRegionSliceNumber = BigEndianConverter.ToUInt16(buffer, 0x117);
            PrivateRegionSliceNumber = BigEndianConverter.ToUInt16(buffer, 0x119);
            PublicRegionStartLBA = BigEndianConverter.ToUInt64(buffer, 0x11B);
            PublicRegionSizeLBA = BigEndianConverter.ToUInt64(buffer, 0x123);
            PrivateRegionStartLBA = BigEndianConverter.ToUInt64(buffer, 0x12B);
            PrivateRegionSizeLBA = BigEndianConverter.ToUInt64(buffer, 0x133);
            PrimaryTocLBA = BigEndianConverter.ToUInt64(buffer, 0x13B);
            SecondaryTocLBA = BigEndianConverter.ToUInt64(buffer, 0x143);

            NumberOfConfigs = BigEndianConverter.ToUInt32(buffer, 0x14B);
            NumberOfLogs = BigEndianConverter.ToUInt32(buffer, 0x14F);
            ConfigSizeLBA = BigEndianConverter.ToUInt64(buffer, 0x153);
            LogSizeLBA = BigEndianConverter.ToUInt64(buffer, 0x15B);

            DiskSignature = BigEndianConverter.ToUInt32(buffer, 0x163);
            DiskSetGuid = BigEndianConverter.ToGuid(buffer, 0x167);
            DiskSetGuidRepeat = BigEndianConverter.ToGuid(buffer, 0x177);

            BigEndianWriter.WriteUInt32(buffer, 0x08, (uint)0); // we exclude the checksum field from checksum calculations
            m_isChecksumValid = (checksum == CalculateChecksum(buffer));
        }

        /// <summary>
        /// Private header may need to be padded with zeros in order to fill an entire sector
        /// </summary>
        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0x00, Signature, 8);
            // we'll write the checksum later
            BigEndianWriter.WriteUInt16(buffer, 0x0C, MajorVersion);
            BigEndianWriter.WriteUInt16(buffer, 0x0E, MinorVersion);
            BigEndianWriter.WriteInt64(buffer, 0x10, LastUpdateDT.ToFileTimeUtc());
            BigEndianWriter.WriteUInt64(buffer, 0x18, UpdateSequenceNumber);
            BigEndianWriter.WriteUInt64(buffer, 0x20, PrimaryPrivateHeaderLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x28, SecondaryPrivateHeaderLBA);
            ByteWriter.WriteAnsiString(buffer, 0x30, DiskGuidString, 0x40);
            ByteWriter.WriteAnsiString(buffer, 0x70, HostGuidString, 0x40);
            ByteWriter.WriteAnsiString(buffer, 0xB0, DiskGroupGuidString, 0x40);
            ByteWriter.WriteAnsiString(buffer, 0xF0, DiskGroupName, 31);
            BigEndianWriter.WriteUInt32(buffer, 0x10F, BytesPerSector);
            BigEndianWriter.WriteUInt32(buffer, 0x113, (uint)Flags);

            BigEndianWriter.WriteUInt16(buffer, 0x117, PublicRegionSliceNumber);
            BigEndianWriter.WriteUInt16(buffer, 0x119, PrivateRegionSliceNumber);
            BigEndianWriter.WriteUInt64(buffer, 0x11B, PublicRegionStartLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x123, PublicRegionSizeLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x12B, PrivateRegionStartLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x133, PrivateRegionSizeLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x13B, PrimaryTocLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x143, SecondaryTocLBA);

            BigEndianWriter.WriteUInt32(buffer, 0x14B, NumberOfConfigs);
            BigEndianWriter.WriteUInt32(buffer, 0x14F, NumberOfLogs);
            BigEndianWriter.WriteUInt64(buffer, 0x153, ConfigSizeLBA);
            BigEndianWriter.WriteUInt64(buffer, 0x15B, LogSizeLBA);

            BigEndianWriter.WriteUInt32(buffer, 0x163, DiskSignature);
            BigEndianWriter.WriteGuidBytes(buffer, 0x167, DiskSetGuid);
            BigEndianWriter.WriteGuidBytes(buffer, 0x177, DiskSetGuidRepeat);

            uint checksum = CalculateChecksum(buffer);
            BigEndianWriter.WriteUInt32(buffer, 0x08, checksum);

            return buffer;
        }

        public static PrivateHeader ReadFromDisk(Disk disk)
        {
            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            if (mbr.IsGPTBasedDisk)
            {
                return ReadFromGPTBasedDisk(disk);
            }
            else
            {
                return ReadFromMBRBasedDisk(disk);
            }
        }

        public static PrivateHeader ReadFromMBRBasedDisk(Disk disk)
        {
            // check for private header at the last sector of the disk
            PrivateHeader privateHeader = ReadFromDiskEnd(disk, true);
            if (privateHeader != null)
            {
                if (privateHeader.IsChecksumValid)
                {
                    return privateHeader;
                }
                else
                {
                    // primary has invalid checksum, try secondary private header
                    long sectorIndex = (long)(privateHeader.PrivateRegionStartLBA + privateHeader.SecondaryPrivateHeaderLBA);
                    return ReadFromDisk(disk, sectorIndex, false);
                }
            }
            else
            {
                // maybe the disk was cloned to a bigger disk, check sector 6
                return ReadFromDiskStart(disk);
            }
        }

        public static PrivateHeader ReadFromGPTBasedDisk(Disk disk)
        {
            List<GuidPartitionEntry> entries = GuidPartitionTable.ReadEntriesFromDisk(disk);
            int index = GuidPartitionEntryCollection.GetIndexOfPartitionTypeGuid(entries, GPTPartition.PrivateRegionPartitionTypeGuid);
            // the private header will be located at the last sector of the private region
            PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(disk, (long)entries[index].LastLBA, true);
            if (privateHeader != null)
            {
                if (privateHeader.IsChecksumValid)
                {
                    return privateHeader;
                }
                else
                {
                    // primary has invalid checksum, try secondary private header
                    long sectorIndex = (long)(privateHeader.PrivateRegionStartLBA + privateHeader.SecondaryPrivateHeaderLBA);
                    return ReadFromDisk(disk, sectorIndex, false);
                }
            }
            return null;
        }

        public static PrivateHeader ReadFromDiskEnd(Disk disk, bool returnPrivateHeaderWithInvalidChecksum)
        { 
            long sectorCount = disk.Size / disk.BytesPerSector;
            long sectorIndex = sectorCount - 1;
            return ReadFromDisk(disk, sectorIndex, returnPrivateHeaderWithInvalidChecksum);
        }

        public static PrivateHeader ReadFromDiskStart(Disk disk)
        {
            return ReadFromDisk(disk, PrivateHeaderSectorIndex, false);
        }

        public static PrivateHeader ReadFromDisk(Disk disk, long sectorIndex, bool returnPrivateHeaderWithInvalidChecksum)
        {
            byte[] sector = disk.ReadSector(sectorIndex);
            string signature = ByteReader.ReadAnsiString(sector, 0x00, 8);
            if (signature == PrivateHeaderSignature)
            {
                PrivateHeader privateHeader = new PrivateHeader(sector);
                if (privateHeader.IsChecksumValid || returnPrivateHeaderWithInvalidChecksum)
                {
                    return privateHeader;
                }
            }

            return null;
        }

        public static void WriteToDisk(Disk disk, PrivateHeader privateHeader)
        {
            byte[] bytes = privateHeader.GetBytes();
            if (disk.BytesPerSector > Length)
            {
                bytes = ByteUtils.Concatenate(bytes, new byte[disk.BytesPerSector - PrivateHeader.Length]);
            }

            disk.WriteSectors((long)(privateHeader.PrivateRegionStartLBA +  privateHeader.PrimaryPrivateHeaderLBA), bytes);
            disk.WriteSectors((long)(privateHeader.PrivateRegionStartLBA + privateHeader.SecondaryPrivateHeaderLBA), bytes);

            // update sector 6 if a Private Header is already present there
            byte[] sector = disk.ReadSector(PrivateHeaderSectorIndex);
            string signature = ByteReader.ReadAnsiString(sector, 0x00, 8);
            if (signature == PrivateHeaderSignature)
            {
                disk.WriteSectors(PrivateHeaderSectorIndex, bytes);
            }
        }

        public Guid DiskGuid
        {
            get
            {
                return new Guid(this.DiskGuidString);
            }
            set
            {
                this.DiskGuidString = value.ToString();
            }
        }

        public Guid DiskGroupGuid
        {
            get
            {
                return new Guid(this.DiskGroupGuidString);
            }
            set
            {
                this.DiskGroupGuidString = value.ToString();
            }
        }

        public bool IsChecksumValid
        {
            get
            {
                return m_isChecksumValid;
            }
        }

        public static uint CalculateChecksum(byte[] bytes)
        {
            uint result = 0;
            for (int index = 0; index < bytes.Length; index++)
            {
                result += bytes[index];
            }
            return result;
        }
    }
}
