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
    [Flags]
    public enum ExtentFlags : uint
    {
        Recover = 0x0008,
        KDetach = 0x0010,
        Relocate = 0x0020,
        BootExtended = 0x0040,
        OrigBoot = 0x0100,
        Volatile = 0x1000,
    }

    // a.k.a. subdisk
    public class ExtentRecord : DatabaseRecord
    {
        public ExtentFlags ExtentFlags;
        public ulong CommitTransactionID;
        public ulong DiskOffsetLBA;        // extent location relative to DataStartLBA (from the private header)
        public ulong OffsetInColumnLBA;    // the location of this extent in the column (for single column volumes this means the offset in the volume)
        public ulong SizeLBA;     // PaddedVarUlong
        public ulong ComponentId;
        public ulong DiskId;
        public uint ColumnIndex; // On Striped / RAID-5 volumes, this is the interleave order, note that column can be comprised of one or more extents
        public ulong UnknownTransactionID;
        public uint Unknown1;
        public ulong HiddenCount; // PaddedVarUlong, Hidden sectors?
        
        public ExtentRecord()
        {
            this.RecordRevision = 3;
            this.RecordType = RecordType.Extent;
        }

        public ExtentRecord(List<DatabaseRecordFragment> fragments) : base(fragments)
        {
            // Data begins at 0x10 (VBLK header is at 0x00)
            int offset = 0x00; // relative to Data
            ReadCommonFields(this.Data, ref offset);
            if (RecordRevision != 3)
            {
                throw new NotImplementedException("Unsupported record revision");
            }
            ExtentFlags = (ExtentFlags)BigEndianReader.ReadUInt32(this.Data, ref offset);
            CommitTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);
            DiskOffsetLBA = BigEndianReader.ReadUInt64(this.Data, ref offset);
            OffsetInColumnLBA = BigEndianReader.ReadUInt64(this.Data, ref offset);
            SizeLBA = ReadVarULong(this.Data, ref offset);
            ComponentId = ReadVarULong(this.Data, ref offset);
            DiskId = ReadVarULong(this.Data, ref offset);

            if (HasColumnIndexFlag)
            {
                ColumnIndex = ReadVarUInt(this.Data, ref offset);
            }

            if (HasUnknownTransactionIDFlag)
            {
                UnknownTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);
            }

            if (HasUnknown1Flag)
            {
                Unknown1 = ReadVarUInt(this.Data, ref offset);
            }

            if (HasHiddenFlag)
            {
                HiddenCount = ReadVarULong(this.Data, ref offset);
            }
        }

        public override byte[] GetDataBytes()
        {
            int dataLength = 36; // fixed length components
            dataLength += VarULongSize(ExtentId);
            dataLength += Name.Length + 1;
            dataLength += PaddedVarULongSize(SizeLBA);
            dataLength += VarULongSize(ComponentId);
            dataLength += VarULongSize(DiskId);
            if (HasColumnIndexFlag)
            {
                dataLength += VarULongSize(ColumnIndex);
            }
            if (HasUnknownTransactionIDFlag)
            {
                dataLength += 8;
            }
            if (HasUnknown1Flag)
            {
                dataLength += VarULongSize(Unknown1);
            }
            if (HasHiddenFlag)
            {
                dataLength += PaddedVarULongSize(HiddenCount);
            }
            
            byte[] data = new byte[dataLength];
            int offset = 0x00;
            WriteCommonFields(data, ref offset);
            BigEndianWriter.WriteUInt32(data, ref offset, (uint)ExtentFlags);
            BigEndianWriter.WriteUInt64(data, ref offset, CommitTransactionID);
            BigEndianWriter.WriteUInt64(data, ref offset, DiskOffsetLBA);
            BigEndianWriter.WriteUInt64(data, ref offset, OffsetInColumnLBA);
            WritePaddedVarULong(data, ref offset, SizeLBA);
            WriteVarULong(data, ref offset, ComponentId);
            WriteVarULong(data, ref offset, DiskId);

            if (HasColumnIndexFlag)
            {
                WriteVarUInt(data, ref offset, ColumnIndex);
            }
            if (HasUnknownTransactionIDFlag)
            {
                BigEndianWriter.WriteUInt64(data, ref offset, UnknownTransactionID);
            }
            if (HasUnknown1Flag)
            {
                WriteVarUInt(data, ref offset, Unknown1);
            }
            if (HasHiddenFlag)
            {
                WritePaddedVarULong(data, ref offset, HiddenCount);
            }
            
            return data;
        }

        public ulong ExtentId
        {
            get
            {
                return this.Id;
            }
        }

        /// <summary>
        /// Hidden sectors?
        /// </summary>
        public bool HasHiddenFlag
        {
            get
            {
                return ((Flags & 0x02) != 0);
            }
        }

        public bool HasColumnIndexFlag
        {
            get
            {
                return ((Flags & 0x08) != 0);
            }
            set
            {
                if (value)
                {
                    this.Flags = 0x08;
                }
                else
                {
                    this.Flags &= 0xF7;
                }
            }
        }

        public bool HasUnknownTransactionIDFlag
        {
            get
            {
                return ((Flags & 0x20) != 0);
            }
        }

        /// <summary>
        /// Windows Vista and newer set this flag by default, earlier versions do not
        /// </summary>
        public bool HasUnknown1Flag
        {
            get
            {
                return ((Flags & 0x40) != 0);
            }
        }
    }
}
