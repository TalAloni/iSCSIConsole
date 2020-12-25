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
    public class NTFSBootRecord
    {
        public const string ValidSignature = "NTFS    ";
        public const int JumpLength = 3;
        public const int SignatureLength = 8;
        public const int CodeLength = 426; // 510 - 0x54
        public const int Length = 512;

        public byte[] Jump = new byte[JumpLength];
        public string OEMID = String.Empty;

        // BIOS parameter block:
        public ushort BytesPerSector = 512;
        public byte SectorsPerCluster = 8;
        // public ushort ReservedSectors         // always 0
        // public byte NumberOfFATs              // always 0
        // public ushort MaxRootDirectoryEntries // always 0 for NTFS
        // public ushort SmallSectorsCount       // always 0 for NTFS
        public byte MediaDescriptor;             // always 0xF8 (Fixed Disk)
        // public ushort SectorsPerFAT           // always 0 for NTFS
        public ushort SectorsPerTrack;
        public ushort NumberOfHeads;
        public uint NumberOfHiddenSectors; // Offset to the start of the partition relative to the disk in sectors
        //public uint LargeSectorsCount;   // always 0 for NTFS

        // Extended BIOS parameter block:
        public byte PhysicalDriveNumber;   // 0x00 floppy, 0x80 hard disk
        // public byte CurrentHead;        // always 0
        public byte ExtendedBootSignature; // always set to 0x80
        public ulong TotalSectors;         // Excluding backup boot sector at the end of the volume.
        public ulong MftStartLCN;
        public ulong MftMirrorStartLCN;
        public sbyte RawClustersPerFileRecordSegment; // signed
        public sbyte RawClustersPerIndexRecord;       // signed
        public ulong VolumeSerialNumber;
        public uint Checksum;                         // Not used
        public byte[] Code = new byte[CodeLength];    // 510 - 0x54
        public ushort BootRecordSignature;

        public NTFSBootRecord()
        {
            OEMID = ValidSignature;
            BytesPerSector = 512;
            SectorsPerCluster = 8;
            MediaDescriptor = 0xF8; // Fixed Disk
            PhysicalDriveNumber = 0x80;
            ExtendedBootSignature = 0x80;
            BootRecordSignature = 0xAA55;
        }

        /// <summary>
        /// boot record is the first sector of the partition (not to be confused with the master boot record which is the first sector of the disk)
        /// </summary>
        public NTFSBootRecord(byte[] buffer)
        {
            Jump = ByteReader.ReadBytes(buffer, 0x00, JumpLength);
            OEMID = ByteReader.ReadAnsiString(buffer, 0x03, SignatureLength);
            
            BytesPerSector = LittleEndianConverter.ToUInt16(buffer, 0x0B);
            SectorsPerCluster = ByteReader.ReadByte(buffer, 0x0D);
            MediaDescriptor = ByteReader.ReadByte(buffer, 0x15);
            SectorsPerTrack = LittleEndianConverter.ToUInt16(buffer, 0x18);
            NumberOfHeads = LittleEndianConverter.ToUInt16(buffer, 0x1A);
            NumberOfHiddenSectors = LittleEndianConverter.ToUInt32(buffer, 0x1C);

            PhysicalDriveNumber = ByteReader.ReadByte(buffer, 0x24);
            ExtendedBootSignature = ByteReader.ReadByte(buffer, 0x26);
            TotalSectors = LittleEndianConverter.ToUInt64(buffer, 0x28);
            MftStartLCN = LittleEndianConverter.ToUInt64(buffer, 0x30);
            MftMirrorStartLCN = LittleEndianConverter.ToUInt64(buffer, 0x38);
            RawClustersPerFileRecordSegment = (sbyte)ByteReader.ReadByte(buffer, 0x40);
            RawClustersPerIndexRecord = (sbyte)ByteReader.ReadByte(buffer, 0x44);
            VolumeSerialNumber = LittleEndianConverter.ToUInt64(buffer, 0x48);
            Checksum = LittleEndianConverter.ToUInt32(buffer, 0x50);
            Code = ByteReader.ReadBytes(buffer, 0x54, CodeLength);
            BootRecordSignature = LittleEndianConverter.ToUInt16(buffer, 0x1FE);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[BytesPerSector];
            ByteWriter.WriteBytes(buffer, 0x00, Jump, Math.Min(Jump.Length, JumpLength));
            ByteWriter.WriteAnsiString(buffer, 0x03, OEMID, Math.Min(OEMID.Length, SignatureLength));

            LittleEndianWriter.WriteUInt16(buffer, 0x0B, BytesPerSector);
            ByteWriter.WriteByte(buffer, 0x0D, SectorsPerCluster);
            ByteWriter.WriteByte(buffer, 0x15, MediaDescriptor);
            LittleEndianWriter.WriteUInt16(buffer, 0x18, SectorsPerTrack);
            LittleEndianWriter.WriteUInt16(buffer, 0x1A, NumberOfHeads);
            LittleEndianWriter.WriteUInt32(buffer, 0x1C, NumberOfHiddenSectors);

            ByteWriter.WriteByte(buffer, 0x24, PhysicalDriveNumber);
            ByteWriter.WriteByte(buffer, 0x26, ExtendedBootSignature);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, TotalSectors);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, MftStartLCN);
            LittleEndianWriter.WriteUInt64(buffer, 0x38, MftMirrorStartLCN);
            ByteWriter.WriteByte(buffer, 0x40, (byte)RawClustersPerFileRecordSegment);
            ByteWriter.WriteByte(buffer, 0x44, (byte)RawClustersPerIndexRecord);
            LittleEndianWriter.WriteUInt64(buffer, 0x48, VolumeSerialNumber);
            LittleEndianWriter.WriteUInt32(buffer, 0x50, Checksum);
            ByteWriter.WriteBytes(buffer, 0x54, Code, Math.Min(Code.Length, CodeLength));
            LittleEndianWriter.WriteUInt16(buffer, 0x1FE, BootRecordSignature);
            return buffer;
        }

        private int ConvertClustersToBytes(int rawClustersPerFileRecord)
        {
            if (rawClustersPerFileRecord < 0)
            {
                return 1 << (-rawClustersPerFileRecord);
            }
            else
            {
                return rawClustersPerFileRecord * BytesPerCluster;
            }
        }

        private sbyte ConvertBytesToClusters(int numberOfBytes)
        {
            if (numberOfBytes >= BytesPerCluster)
            {
                return (sbyte)(numberOfBytes / BytesPerCluster);
            }
            else
            {
                return (sbyte)(-(int)Math.Log(numberOfBytes, 2));
            }
        }
        
        public int BytesPerCluster
        {
            get
            {
                return SectorsPerCluster * BytesPerSector;
            }
        }

        public int BytesPerFileRecordSegment
        {
            get
            {
                return ConvertClustersToBytes(RawClustersPerFileRecordSegment);
            }
            set
            {
                RawClustersPerFileRecordSegment = ConvertBytesToClusters(value);
            }
        }

        public int BytesPerIndexRecord
        {
            get
            {
                return ConvertClustersToBytes(RawClustersPerIndexRecord);
            }
            set
            {
                RawClustersPerIndexRecord = ConvertBytesToClusters(value);
            }
        }

        public bool IsValid
        {
            get
            {
                return String.Equals(OEMID, ValidSignature);
            }
        }

        /// <summary>
        /// File record segment length is a multiple of BytesPerSector
        /// </summary>
        public int SectorsPerFileRecordSegment
        {
            get
            {
                return this.BytesPerFileRecordSegment / BytesPerSector;
            }
        }

        public static NTFSBootRecord ReadRecord(byte[] buffer)
        {
            string OEMID = ByteReader.ReadAnsiString(buffer, 0x03, SignatureLength);
            bool isValid = String.Equals(OEMID, ValidSignature);
            if (isValid)
            {
                return new NTFSBootRecord(buffer);
            }
            else
            {
                return null;
            }
        }
    }
}
