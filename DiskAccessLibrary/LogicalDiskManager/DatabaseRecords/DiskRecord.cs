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
    public enum DiskFlags : uint
    { 
        Reserved = 0x01,
        Spare = 0x02,
        Failing = 0x04,
        EncapPending = 0x08,
        MergeRequired = 0x0010,
        Removed = 0x0100,
        Detached = 0x0200,
    }

    public class DiskRecord : DatabaseRecord
    {
        public Guid DiskGuid;
        public string LastDeviceName = String.Empty;
        public DiskFlags DiskFlags;
        public ulong CommitTransactionID;

        public Guid AltGuidRev4; // no clue what this is

        public DiskRecord()
        {
            this.RecordRevision = 3;
            this.RecordType = RecordType.Disk;
        }

        public DiskRecord(List<DatabaseRecordFragment> fragments) : base(fragments)
        {
            // Data begins at 0x10 (VBLK header is at 0x00)
            int offset = 0x00; // relative to Data
            ReadCommonFields(this.Data, ref offset);
            if (RecordRevision == 3)
            {
                string diskGuidString = ReadVarString(this.Data, ref offset);
                DiskGuid = new Guid(diskGuidString);
                
            }
            else if (RecordRevision == 4)
            {
                DiskGuid = BigEndianReader.ReadGuidBytes(this.Data, ref offset);
                AltGuidRev4 = BigEndianReader.ReadGuidBytes(this.Data, ref offset);
            }
            else
            {
                throw new NotImplementedException("Unsupported record revision");
            }
            LastDeviceName = ReadVarString(this.Data, ref offset);
            DiskFlags = (DiskFlags)BigEndianReader.ReadUInt32(this.Data, ref offset);
            CommitTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);
        }

        public override byte[] GetDataBytes()
        {
            int dataLength = 0;
            dataLength += VarULongSize(DiskId);
            dataLength += Name.Length + 1;
            if (RecordRevision == 3)
            {
                dataLength += 20; // fixed length components
                dataLength += DiskGuid.ToString().Length + 1;
                dataLength += LastDeviceName.Length + 1;
            }
            else // RecordRevision == 4
            {
                dataLength += 53; // fixed length components
            }

            byte[] data = new byte[dataLength];
            int offset = 0x00;
            WriteCommonFields(data, ref offset);
            if (RecordRevision == 3)
            {
                WriteVarString(data, ref offset, DiskGuid.ToString());
                
            }
            else // RecordRevision == 4
            {
                BigEndianWriter.WriteGuidBytes(data, ref offset, DiskGuid);
                BigEndianWriter.WriteGuidBytes(data, ref offset, AltGuidRev4);
            }
            WriteVarString(data, ref offset, LastDeviceName);
            BigEndianWriter.WriteUInt32(data, ref offset, (uint)DiskFlags);
            BigEndianWriter.WriteUInt64(data, ref offset, CommitTransactionID);
            return data;
        }

        public ulong DiskId
        {
            get
            {
                return this.Id;
            }
        }

        public string DiskGuidString
        {
            get
            {
                return DiskGuid.ToString();
            }
        }
    }
}
