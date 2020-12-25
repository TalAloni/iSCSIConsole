/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public enum PartitionType : byte
    {
        RAW = 0x06,   // RAW and FAT16 are both 0x06
        FAT16 = 0x06,
        NTFS = 0x07,
        FAT32 = 0x0B,
    }

    public class VolumeRecord : DatabaseRecord
    {
        public string VolumeTypeString = String.Empty; // "gen" or "raid5"
        public string DisableDriverLetterAssignmentString = String.Empty; // Disable driver letter assignment: "8000000000000000"
        public string StateString = "ACTIVE"; // "ACTIVE", "SYNC"
        public ReadPolicyName ReadPolicy;
        public uint VolumeNumber; // start at 5 and unused numbers are reused (new volumes are set to 0xFFFFFFFF)
        public VolumeFlags VolumeFlags;
        public uint NumberOfComponents;
        public ulong CommitTransactionID;
        public ulong UnknownTransactionID; // Not always set, and when it does, it's smaller than CommitTransactionID
        public ulong SizeLBA;       // PaddedVarULong, Number of logical sectors
        // 4 Zeros
        public PartitionType PartitionType;
        public Guid VolumeGuid;
        public ulong UnknownID1;
        public ulong UnknownID2;
        public ulong ColumnSizeLBA; // PaddedVarULong, this is either the column size or the size of the first extent
        public string MountHint = String.Empty;

        public VolumeRecord()
        {
            this.RecordRevision = 5;
            this.RecordType = RecordType.Volume;
        }

        public VolumeRecord(List<DatabaseRecordFragment> fragments) : base(fragments)
        {
            // Data begins at 0x10 (VBLK header is at 0x00)
            int offset = 0x00; // relative to Data
            ReadCommonFields(this.Data, ref offset);
            if (RecordRevision != 5)
            {
                throw new NotImplementedException("Unsupported record revision");
            }
            VolumeTypeString = ReadVarString(this.Data, ref offset);
            DisableDriverLetterAssignmentString = ReadVarString(this.Data, ref offset);
            StateString = ByteReader.ReadAnsiString(this.Data, ref offset, 14).Trim('\0');
            ReadPolicy = (ReadPolicyName)ByteReader.ReadByte(this.Data, ref offset);
            VolumeNumber = ReadVarUInt(this.Data, ref offset);
            VolumeFlags = (VolumeFlags)BigEndianReader.ReadUInt32(this.Data, ref offset);
            NumberOfComponents = ReadVarUInt(this.Data, ref offset);
            CommitTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);
            UnknownTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);

            SizeLBA = ReadVarULong(this.Data, ref offset);
            offset += 4;
            PartitionType = (PartitionType)ByteReader.ReadByte(this.Data, ref offset);
            VolumeGuid = BigEndianReader.ReadGuid(this.Data, ref offset);

            if (HasUnknownID1Flag)
            {
                UnknownID1 = ReadVarULong(this.Data, ref offset);
            }

            if (HasUnknownID2Flag)
            {
                UnknownID2 = ReadVarULong(this.Data, ref offset);
            }

            if (HasColumnSizeFlag)
            {
                ColumnSizeLBA = ReadVarULong(this.Data, ref offset);
            }

            if (HasMountHintFlag)
            {
                MountHint = ReadVarString(this.Data, ref offset);
            }
        }

        public override byte[] GetDataBytes()
        {
            int dataLength = 64; // fixed length components
            dataLength += VarULongSize(VolumeId);
            dataLength += Name.Length + 1;
            dataLength += VolumeTypeString.Length + 1;
            dataLength += DisableDriverLetterAssignmentString.Length + 1;
            dataLength += VarUIntSize(VolumeNumber);
            dataLength += VarUIntSize(NumberOfComponents);
            dataLength += PaddedVarULongSize(SizeLBA);

            if (HasUnknownID1Flag)
            {
                dataLength += VarULongSize(UnknownID1);
            }

            if (HasUnknownID2Flag)
            {
                dataLength += VarULongSize(UnknownID2);
            }

            if (HasColumnSizeFlag)
            {
                dataLength += PaddedVarULongSize(ColumnSizeLBA);
            }

            if (HasMountHintFlag)
            {
                dataLength += MountHint.Length + 1;
            }

            byte[] data = new byte[dataLength];
            int offset = 0x00;
            WriteCommonFields(data, ref offset);
            WriteVarString(data, ref offset, VolumeTypeString);
            WriteVarString(data, ref offset, DisableDriverLetterAssignmentString);
            ByteWriter.WriteAnsiString(data, ref offset, StateString, 14);

            ByteWriter.WriteByte(data, ref offset, (byte)ReadPolicy);
            WriteVarUInt(data, ref offset, VolumeNumber);
            BigEndianWriter.WriteUInt32(data, ref offset, (uint)VolumeFlags);
            WriteVarUInt(data, ref offset, NumberOfComponents);
            BigEndianWriter.WriteUInt64(data, ref offset, CommitTransactionID);
            BigEndianWriter.WriteUInt64(data, ref offset, UnknownTransactionID);
            WritePaddedVarULong(data, ref offset, SizeLBA);
            offset += 4;
            ByteWriter.WriteByte(data, ref offset, (byte)PartitionType);
            BigEndianWriter.WriteGuid(data, ref offset, VolumeGuid);

            if (HasUnknownID1Flag)
            {
                WriteVarULong(data, ref offset, UnknownID1);
            }

            if (HasUnknownID2Flag)
            {
                WriteVarULong(data, ref offset, UnknownID2);
            }

            if (HasColumnSizeFlag)
            {
                WritePaddedVarULong(data, ref offset, ColumnSizeLBA);
            }

            if (HasMountHintFlag)
            {
                WriteVarString(data, ref offset, MountHint);
            }

            return data;
            
        }

        public ulong VolumeId
        {
            get
            {
                return this.Id;
            }
        }

        public bool HasUnknownID1Flag
        {
            get
            {
                return (Flags & 0x08) != 0;
            }
        }

        public bool HasUnknownID2Flag
        {
            get
            {
                return (Flags & 0x20) != 0;
            }
        }
        
        public bool HasColumnSizeFlag
        {
            get
            {
                return (Flags & 0x80) != 0;
            }
        }

        public bool HasMountHintFlag
        {
            get
            {
                return (Flags & 0x02) != 0;
            }
        }
    }
}
