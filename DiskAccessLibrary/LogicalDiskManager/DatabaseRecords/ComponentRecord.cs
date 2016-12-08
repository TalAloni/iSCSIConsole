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

namespace DiskAccessLibrary.LogicalDiskManager
{
    public enum ComponentFlags : uint
    { 
        Recover = 0x02,
        IOFail = 0x04,
        Pending = 0x08,
    }

    // a.k.a. Plex
    public class ComponentRecord : DatabaseRecord
    {
        public string StateString = "ACTIVE";
        public ExtentLayoutName ExtentLayout;
        public ComponentFlags ComponentFlags;
        public uint NumberOfExtents;   // number of extents
        public ulong CommitTransactionID;
        // 8 zeros
        public ulong VolumeId;
        public ulong LogSD; // DMDiag reports this as 'logging:  logsd=x'
        public ulong StripeSizeLBA;  // PaddedVarULong
        public uint NumberOfColumns; // DMDiag will read this value as (signed) Int32

        public ComponentRecord()
        {
            this.RecordRevision = 3;
            this.RecordType = RecordType.Component;
        }

        public ComponentRecord(List<DatabaseRecordFragment> fragments) : base(fragments)
        {
            // Data begins at 0x10 (VBLK header is at 0x00)
            int offset = 0x00; // relative to Data
            ReadCommonFields(this.Data, ref offset);
            if (RecordRevision != 3)
            {
                throw new NotImplementedException("Unsupported record revision");
            }
            StateString = ReadVarString(this.Data, ref offset);
            ExtentLayout = (ExtentLayoutName)ByteReader.ReadByte(this.Data, ref offset);
            ComponentFlags = (ComponentFlags)BigEndianReader.ReadUInt32(this.Data, ref offset);
            NumberOfExtents = ReadVarUInt(this.Data, ref offset);
            CommitTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);
            offset += 8;
            VolumeId = ReadVarULong(this.Data, ref offset);
            LogSD = ReadVarULong(this.Data, ref offset);

            if (HasStripedExtentsFlag)
            {
                StripeSizeLBA = ReadVarULong(this.Data, ref offset);
                NumberOfColumns = ReadVarUInt(this.Data, ref offset);
            }
        }

        public override byte[] GetDataBytes()
        {
            int dataLength = 29; // fixed length components
            dataLength += VarULongSize(ComponentId);
            dataLength += Name.Length + 1;
            dataLength += StateString.Length + 1;
            dataLength += VarUIntSize(NumberOfExtents);
            dataLength += VarULongSize(VolumeId);
            dataLength += VarULongSize(LogSD);

            if (HasStripedExtentsFlag)
            {
                dataLength += PaddedVarULongSize(StripeSizeLBA);
                dataLength += VarUIntSize(NumberOfColumns);
            }

            byte[] data = new byte[dataLength];
            int offset = 0x00;
            WriteCommonFields(data, ref offset);
            WriteVarString(data, ref offset, StateString);
            ByteWriter.WriteByte(data, ref offset, (byte)ExtentLayout);
            BigEndianWriter.WriteUInt32(data, ref offset, (uint)ComponentFlags);
            WriteVarUInt(data, ref offset, NumberOfExtents);
            BigEndianWriter.WriteUInt64(data, ref offset, CommitTransactionID);
            offset += 8;
            WriteVarULong(data, ref offset, VolumeId);
            WriteVarULong(data, ref offset, LogSD);
            if (HasStripedExtentsFlag)
            {
                WritePaddedVarULong(data, ref offset, StripeSizeLBA);
                WriteVarUInt(data, ref offset, NumberOfColumns);
            }

            return data;
        }

        public ulong ComponentId
        {
            get
            {
                return this.Id;
            }
        }

        public bool HasStripedExtentsFlag
        {
            get
            {
                return ((Flags & 0x10) != 0);
            }
            set
            {
                if (value)
                {
                    this.Flags = 0x10;
                }
                else
                {
                    this.Flags = 0;
                }
            }
        }
    }
}
