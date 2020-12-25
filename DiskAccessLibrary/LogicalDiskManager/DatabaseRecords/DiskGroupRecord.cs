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
    public class DiskGroupRecord : DatabaseRecord
    {
        public Guid DiskGroupGuid;
        public Guid DiskSetGuid; // revision 4 only
        // 4 zeros (probably reserved for Disk Group Flags)
        public ulong CommitTransactionID;
        public uint NumberOfConfigCopies; // 0xFFFFFFFF means 'all', 0 means 'default' (DMDiag list this as 'copies: nconfig=x')
        public uint NumberOfLogCopies;    // 0xFFFFFFFF means 'all', 0 means 'default' (DMDiag list this as 'copies: nlog=x')
        public uint MinorsGreaterThanOrEqualTo; // DMDiag reports this as 'minors:   >= x', more than 4 bytes are reported as error

        public DiskGroupRecord(List<DatabaseRecordFragment> fragments) : base(fragments)
        {
            // Data begins at 0x10 (VBLK header is at 0x00)
            int offset = 0x00; // relative to Data
            ReadCommonFields(this.Data, ref offset);
            if (RecordRevision == 3)
            {
                DiskGroupGuid = new Guid(ReadVarString(this.Data, ref offset));
            }
            else if (RecordRevision == 4)
            {
                DiskGroupGuid = BigEndianReader.ReadGuid(this.Data, ref offset);
                DiskSetGuid = BigEndianReader.ReadGuid(this.Data, ref offset);
            }
            else
            {
                throw new NotImplementedException("Unsupported record revision");
            }
            offset += 4; // 4 Zeros
            CommitTransactionID = BigEndianReader.ReadUInt64(this.Data, ref offset);

            if (HasNumberOfCopiesFlag)
            {
                NumberOfConfigCopies = ReadVarUInt(this.Data, ref offset);
                NumberOfLogCopies = ReadVarUInt(this.Data, ref offset);
            }

            if (HasMinorsFlag)
            {
                MinorsGreaterThanOrEqualTo = ReadVarUInt(this.Data, ref offset);
            }
        }

        public override byte[] GetDataBytes()
        {
            int dataLength = 8; // header fixed length components
            dataLength += VarULongSize(DiskGroupId);
            dataLength += Name.Length + 1;
            if (RecordRevision == 3)
            {
                dataLength += 12; // fixed length components
                dataLength += DiskGroupGuid.ToString().Length + 1;
            }
            else // RecordRevision == 4
            {
                dataLength += 44; // fixed length components
            }

            if (HasNumberOfCopiesFlag)
            {
                dataLength += VarUIntSize(NumberOfConfigCopies);
                dataLength += VarUIntSize(NumberOfLogCopies);
            }

            if (HasMinorsFlag)
            {
                dataLength += VarUIntSize(MinorsGreaterThanOrEqualTo);
            }

            byte[] data = new byte[dataLength];
            int offset = 0x00;
            WriteCommonFields(data, ref offset);
            if (RecordRevision == 3)
            {
                WriteVarString(data, ref offset, DiskGroupGuid.ToString());
            }
            else
            {
                BigEndianWriter.WriteGuid(data, ref offset, DiskGroupGuid);
                BigEndianWriter.WriteGuid(data, ref offset, DiskSetGuid);
            }
            offset += 4;
            BigEndianWriter.WriteUInt64(data, ref offset, CommitTransactionID);

            if (HasNumberOfCopiesFlag)
            {
                WriteVarUInt(data, ref offset, NumberOfConfigCopies);
                WriteVarUInt(data, ref offset, NumberOfLogCopies);
            }

            if (HasMinorsFlag)
            {
                WriteVarUInt(data, ref offset, MinorsGreaterThanOrEqualTo);
            }

            return data;
        }

        public ulong DiskGroupId
        {
            get
            {
                return this.Id;
            }
        }

        public string DiskGroupGuidString
        {
            get
            {
                return DiskGroupGuid.ToString();
            }
        }

        /// <summary>
        /// If this flag is not rpesent, DMDiag will report 'copies: nconfig=default nlog=default'
        /// </summary>
        public bool HasNumberOfCopiesFlag
        {
            get
            {
                return ((Flags & 0x08) != 0);
            }
        }

        public bool HasMinorsFlag
        {
            get
            {
                return ((Flags & 0x10) != 0);
            }
        }
    }
}
