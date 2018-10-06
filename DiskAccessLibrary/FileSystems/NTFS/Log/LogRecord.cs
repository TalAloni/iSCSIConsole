/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class LogRecord
    {
        public const int HeaderLength = 0x30; // Including padding to 8 byte boundary

        /* Start of LFS_RECORD_HEADER */
        public ulong ThisLsn;
        public ulong ClientPreviousLsn;
        public ulong ClientUndoNextLsn;
        // uint ClientDataLength;
        public ushort ClientSeqNumber;
        public ushort ClientIndex;
        public LogRecordType RecordType;
        public uint TransactionId;
        public LogRecordFlags Flags;
        // ushort AlignWord
        /* End of LFS_RECORD_HEADER */
        public byte[] Data;

        public LogRecord()
        {
            Data = new byte[0];
        }

        public LogRecord(byte[] buffer, int offset)
        {
            ThisLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x00);
            ClientPreviousLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            ClientUndoNextLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            uint clientDataLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            ClientSeqNumber = LittleEndianConverter.ToUInt16(buffer, offset + 0x1C);
            ClientIndex = LittleEndianConverter.ToUInt16(buffer, offset + 0x1E);
            RecordType = (LogRecordType)LittleEndianConverter.ToUInt32(buffer, offset + 0x20);
            TransactionId = LittleEndianConverter.ToUInt32(buffer, offset + 0x24);
            Flags = (LogRecordFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x28);
            Data = ByteReader.ReadBytes(buffer, offset + HeaderLength, (int)clientDataLength);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[this.Length];
            WriteBytes(buffer, 0);
            return buffer;
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x00, ThisLsn);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x08, ClientPreviousLsn);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x10, ClientUndoNextLsn);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x18, (uint)Data.Length);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x1C, ClientSeqNumber);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x1E, ClientIndex);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x20, (uint)RecordType);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x24, TransactionId);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x28, (ushort)Flags);
            ByteWriter.WriteBytes(buffer, offset + HeaderLength, Data);
        }

        public int Length
        {
            get
            {
                // Each record is padded to 8 byte boundary
                return HeaderLength + (int)Math.Ceiling((double)Data.Length / 8) * 8;
            }
        }
    }
}
