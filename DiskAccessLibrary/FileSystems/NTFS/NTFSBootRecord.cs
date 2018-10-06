/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class NTFSBootRecord
    {
        public const string ValidSignature = "NTFS    ";

        public byte[] Jump = new byte[3];
        public string OEMID = String.Empty;

        // BIOS parameter block:
        public ushort BytesPerSector = 512; // provides default until actual values are read
        public byte SectorsPerCluster = 8;  // provides default until actual values are read
        // public ushort ReservedSectors         // always 0
        // public byte NumberOfFATs              // always 0
        // public ushort MaxRootDirectoryEntries // always 0 for NTFS
        // public ushort SmallSectorsCount       // always 0 for NTFS
        public byte MediaDescriptor = 0xF8; // always F8 (Fixed Disk)
        // public ushort SectorsPerFAT       // always 0 for NTFS
        public ushort SectorsPerTrack;
        public ushort NumberOfHeads;
        public uint NumberOfHiddenSectors; // Offset to the start of the partition relative to the disk in sectors
        //public uint LargeSectorsCount; // always 0 for NTFS

        // Extended BIOS parameter block:
        public byte PhysicalDriveNumber = 0x80;   // 0x00 floppy, 0x80 hard disk
        // public byte CurrentHead; // always 0
        public byte ExtendedBootSignature = 0x80; // always set to 0x80
        public ulong TotalSectors;      // Excluding backup boot sector at the end of the volume.
        public ulong MftStartLCN;
        public ulong MftMirrorStartLCN;
        public sbyte RawClustersPerFileRecordSegment; // signed
        public sbyte RawClustersPerIndexRecord; // signed
        public ulong VolumeSerialNumber;
        public uint Checksum;

        public byte[] Code = new byte[428]; // 512 - 0x54

        /// <summary>
        /// boot record is the first sector of the partition (not to be confused with the master boot record which is the first sector of the disk)
        /// </summary>
        public NTFSBootRecord(byte[] buffer)
        {
            Array.Copy(buffer, 0x00, Jump, 0, 3);
            OEMID = ASCIIEncoding.ASCII.GetString(buffer, 0x03, 8);
            
            BytesPerSector = LittleEndianConverter.ToUInt16(buffer, 0x0B);
            SectorsPerCluster = buffer[0x0D];
            MediaDescriptor = buffer[0x15];
            SectorsPerTrack = LittleEndianConverter.ToUInt16(buffer, 0x18);
            NumberOfHeads = LittleEndianConverter.ToUInt16(buffer, 0x1A);
            NumberOfHiddenSectors = LittleEndianConverter.ToUInt32(buffer, 0x1C);

            PhysicalDriveNumber = buffer[0x24];
            ExtendedBootSignature = buffer[0x26];
            TotalSectors = LittleEndianConverter.ToUInt64(buffer, 0x28);
            MftStartLCN = LittleEndianConverter.ToUInt64(buffer, 0x30);
            MftMirrorStartLCN = LittleEndianConverter.ToUInt64(buffer, 0x38);
            RawClustersPerFileRecordSegment = (sbyte)buffer[0x40];
            RawClustersPerIndexRecord = (sbyte)buffer[0x44];
            VolumeSerialNumber = LittleEndianConverter.ToUInt64(buffer, 0x48);
            Checksum = LittleEndianConverter.ToUInt32(buffer, 0x50);

            Array.Copy(buffer, 0x54, Code, 0, Code.Length);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[BytesPerSector];
            Array.Copy(Jump, 0, buffer, 0x00, 3);
            ByteWriter.WriteAnsiString(buffer, 0x03, OEMID, 8);

            LittleEndianWriter.WriteUInt16(buffer, 0x0B, BytesPerSector);
            buffer[0x0D] = SectorsPerCluster;
            buffer[0x15] = MediaDescriptor;
            LittleEndianWriter.WriteUInt16(buffer, 0x18, SectorsPerTrack);
            LittleEndianWriter.WriteUInt16(buffer, 0x1A, NumberOfHeads);
            LittleEndianWriter.WriteUInt32(buffer, 0x1C, NumberOfHiddenSectors);

            buffer[0x24] = PhysicalDriveNumber;
            buffer[0x26] = ExtendedBootSignature;
            LittleEndianWriter.WriteUInt64(buffer, 0x28, TotalSectors);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, MftStartLCN);
            LittleEndianWriter.WriteUInt64(buffer, 0x38, MftMirrorStartLCN);
            buffer[0x40] = (byte)RawClustersPerFileRecordSegment;
            buffer[0x44] = (byte)RawClustersPerIndexRecord;
            LittleEndianWriter.WriteUInt64(buffer, 0x48, VolumeSerialNumber);
            LittleEndianWriter.WriteUInt32(buffer, 0x50, Checksum);

            Array.Copy(Code, 0, buffer, 0x54, Code.Length);
            return buffer;
        }
        
        public int BytesPerCluster
        {
            get
            {
                int clusterSize = SectorsPerCluster * BytesPerSector;
                return clusterSize;
            }
        }

        public int BytesPerFileRecordSegment
        {
            get
            {
                return CalcRecordSize(RawClustersPerFileRecordSegment);
            }
        }

        public int BytesPerIndexRecord
        {
            get
            {
                return CalcRecordSize(RawClustersPerIndexRecord);
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

        internal int CalcRecordSize(int rawClustersPerFileRecord)
        {
            if (rawClustersPerFileRecord < 0)
            {
                return 1 << (-rawClustersPerFileRecord);
            }
            else
            {
                return rawClustersPerFileRecord * SectorsPerCluster * BytesPerSector;
            }
        }

        public static NTFSBootRecord ReadRecord(byte[] buffer)
        {
            string OEMID = ASCIIEncoding.ASCII.GetString(buffer, 0x03, 8);
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
