/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class VolumeManagerDatabaseHeader // VMDB
    {
        public const int Length = 512;
        public const string VolumeManagerDatabaseSignature = "VMDB";

        public string Signature = VolumeManagerDatabaseSignature;
        public uint NumberOfVBlks;              // Number of VBLK blocks in the database (This number includes the VMDB, which counts as 4 blocks)
        public uint BlockSize;                  // VBLK block size
        public uint HeaderSize;
        public DatabaseHeaderUpdateStatus UpdateStatus;
        // Versions: 4.10 for Windows XP \ Server 2003 \ Windows 7 \ Server 2008
        //           4.10 for Veritas Storage Foundation 4.0 ('Windows Disk Management compatible group' checked)
        //           4.12 for Veritas Storage Foundation 4.0 ('Windows Disk Management compatible group' unchecked)
        public ushort MajorVersion;
        public ushort MinorVersion;
        public string DiskGroupName = String.Empty;
        public string DiskGroupGuidString = String.Empty;
        public ulong CommitTransactionID;  // ID of last commit transaction
        public ulong PendingTransactionID; // ID of transaction to be committed (should be equal to CommitTransactionID unless an update is taking place)
        public uint CommittedTotalNumberOfVolumeRecords;     // Total number of Volume records already committed (before pending changes)
        public uint CommittedTotalNumberOfComponentRecords;  // see above
        public uint CommittedTotalNumberOfExtentRecords;     // see above
        public uint CommittedTotalNumberOfDiskRecords;       // see above
        public uint CommittedTotalNumberOfDiskAccessRecords; // DMDiag reports this as nda (number of 'da', which use to represents disk access records in other Veritas products)
        // Unused 8 bytes
        public uint PendingTotalNumberOfVolumeRecords;       // Total number of Volume records after pending changes
        public uint PendingTotalNumberOfComponentRecords;    // see above
        public uint PendingTotalNumberOfExtentRecords;       // see above
        public uint PendingTotalNumberOfDiskRecords;         // see above
        public uint PendingTotalNumberOfDiskAccessRecords;   // see above
        // Unused 8 bytes
        public DateTime LastModificationDT;

        public VolumeManagerDatabaseHeader(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 4);
            NumberOfVBlks = BigEndianConverter.ToUInt32(buffer, 0x04);
            BlockSize = BigEndianConverter.ToUInt32(buffer, 0x08);
            HeaderSize = BigEndianConverter.ToUInt32(buffer, 0x0C);
            UpdateStatus = (DatabaseHeaderUpdateStatus)BigEndianConverter.ToUInt16(buffer, 0x10);
            MajorVersion = BigEndianConverter.ToUInt16(buffer, 0x12);
            MinorVersion = BigEndianConverter.ToUInt16(buffer, 0x14);
            DiskGroupName = ByteReader.ReadAnsiString(buffer, 0x16, 31).Trim('\0');
            DiskGroupGuidString = ByteReader.ReadAnsiString(buffer, 0x35, 64).Trim('\0');

            CommitTransactionID = BigEndianConverter.ToUInt64(buffer, 0x75);
            PendingTransactionID = BigEndianConverter.ToUInt64(buffer, 0x7D);

            CommittedTotalNumberOfVolumeRecords = BigEndianConverter.ToUInt32(buffer, 0x85);
            CommittedTotalNumberOfComponentRecords = BigEndianConverter.ToUInt32(buffer, 0x89);
            CommittedTotalNumberOfExtentRecords = BigEndianConverter.ToUInt32(buffer, 0x8D);
            CommittedTotalNumberOfDiskRecords = BigEndianConverter.ToUInt32(buffer, 0x91);
            CommittedTotalNumberOfDiskAccessRecords = BigEndianConverter.ToUInt32(buffer, 0x95);
            // Unused 8 bytes
            PendingTotalNumberOfVolumeRecords = BigEndianConverter.ToUInt32(buffer, 0xA1);
            PendingTotalNumberOfComponentRecords = BigEndianConverter.ToUInt32(buffer, 0xA5);
            PendingTotalNumberOfExtentRecords = BigEndianConverter.ToUInt32(buffer, 0xA9);
            PendingTotalNumberOfDiskRecords = BigEndianConverter.ToUInt32(buffer, 0xAD);
            PendingTotalNumberOfDiskAccessRecords = BigEndianConverter.ToUInt32(buffer, 0xB1);
            // Unused 8 bytes
            LastModificationDT = DateTime.FromFileTimeUtc(BigEndianConverter.ToInt64(buffer, 0xBD));
        }

        /// <summary>
        /// VBLKs may reside in the same sector as the VMDB header.
        /// </summary>
        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0x00, Signature, 4);
            BigEndianWriter.WriteUInt32(buffer, 0x04, NumberOfVBlks);
            BigEndianWriter.WriteUInt32(buffer, 0x08, BlockSize);
            BigEndianWriter.WriteUInt32(buffer, 0x0C, HeaderSize);
            BigEndianWriter.WriteUInt16(buffer, 0x10, (ushort)UpdateStatus);
            BigEndianWriter.WriteUInt16(buffer, 0x12, MajorVersion);
            BigEndianWriter.WriteUInt16(buffer, 0x14, MinorVersion);
            ByteWriter.WriteAnsiString(buffer, 0x16, DiskGroupName, 31);
            ByteWriter.WriteAnsiString(buffer, 0x35, DiskGroupGuidString, 64);

            BigEndianWriter.WriteUInt64(buffer, 0x75, CommitTransactionID);
            BigEndianWriter.WriteUInt64(buffer, 0x7D, PendingTransactionID);

            BigEndianWriter.WriteUInt32(buffer, 0x85, CommittedTotalNumberOfVolumeRecords);
            BigEndianWriter.WriteUInt32(buffer, 0x89, CommittedTotalNumberOfComponentRecords);
            BigEndianWriter.WriteUInt32(buffer, 0x8D, CommittedTotalNumberOfExtentRecords);
            BigEndianWriter.WriteUInt32(buffer, 0x91, CommittedTotalNumberOfDiskRecords);
            BigEndianWriter.WriteUInt32(buffer, 0x95, CommittedTotalNumberOfDiskAccessRecords);

            BigEndianWriter.WriteUInt32(buffer, 0xA1, PendingTotalNumberOfVolumeRecords);
            BigEndianWriter.WriteUInt32(buffer, 0xA5, PendingTotalNumberOfComponentRecords);
            BigEndianWriter.WriteUInt32(buffer, 0xA9, PendingTotalNumberOfExtentRecords);
            BigEndianWriter.WriteUInt32(buffer, 0xAD, PendingTotalNumberOfDiskRecords);
            BigEndianWriter.WriteUInt32(buffer, 0xB1, PendingTotalNumberOfDiskAccessRecords);

            BigEndianWriter.WriteInt64(buffer, 0xBD, LastModificationDT.ToFileTimeUtc());

            return buffer;
        }

        public Guid DiskGroupGuid
        {
            get
            {
                return new Guid(DiskGroupGuidString);
            }
            set
            {
                DiskGroupGuidString = value.ToString();
            }
        }

        public bool IsVersionSupported
        {
            get
            {
                return (MajorVersion == 4 && (MinorVersion == 10 || MinorVersion == 12));
            }
        }

        public static VolumeManagerDatabaseHeader ReadFromDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            ulong sectorIndex = privateHeader.PrivateRegionStartLBA + tocBlock.ConfigStart;
            byte[] sector = disk.ReadSector((long)sectorIndex);
            string signature = ByteReader.ReadAnsiString(sector, 0x00, 4);
            if (signature == VolumeManagerDatabaseSignature)
            {
                return new VolumeManagerDatabaseHeader(sector);
            }
            else
            {
                return null;
            }
        }

        public static void WriteToDisk(DynamicDisk disk, VolumeManagerDatabaseHeader header)
        {
            WriteToDisk(disk.Disk, disk.PrivateHeader, disk.TOCBlock, header);
        }

        public static void WriteToDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock, VolumeManagerDatabaseHeader header)
        {
            ulong sectorIndex = privateHeader.PrivateRegionStartLBA + tocBlock.ConfigStart;
            byte[] headerBytes = header.GetBytes();
            if (disk.BytesPerSector > Length)
            {
                byte[] sectorBytes = disk.ReadSector((long)sectorIndex);
                ByteWriter.WriteBytes(sectorBytes, 0, headerBytes);
                disk.WriteSectors((long)sectorIndex, sectorBytes);
            }
            else
            {
                disk.WriteSectors((long)sectorIndex, headerBytes);
            }
        }
    }
}
