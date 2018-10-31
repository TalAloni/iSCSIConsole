/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// TRANSACTION_ENTRY
    /// </summary>
    public class TransactionEntry : RestartTableEntry
    {
        public const int EntryLength = 0x28;

        // uint AllocatedOrNextFree;
        public TransactionState TransactionState;
        // 3 reserved  bytes
        public ulong FirstLsn;   // First LSN for the transaction
        public ulong PreviousLsn;
        public ulong UndoNextLsn;
        public uint UndoRecords; // Number of of undo log records pending abort
        public int UndoBytes;    // Number of of bytes in undo log records pending abort

        public TransactionEntry()
        {
        }

        public TransactionEntry(byte[] buffer, int offset)
        {
            AllocatedOrNextFree = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            TransactionState = (TransactionState)ByteReader.ReadByte(buffer, offset + 0x04);
            FirstLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            PreviousLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            UndoNextLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x18);
            UndoRecords = LittleEndianConverter.ToUInt32(buffer, offset + 0x20);
            UndoBytes = LittleEndianConverter.ToInt32(buffer, offset + 0x24);
        }

        public override void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x00, AllocatedOrNextFree);
            ByteWriter.WriteByte(buffer, offset + 0x04, (byte)TransactionState);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x08, FirstLsn);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x10, PreviousLsn);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x18, UndoNextLsn);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x20, UndoRecords);
            LittleEndianWriter.WriteInt32(buffer, offset + 0x24, UndoBytes);
        }

        public override int Length
        {
            get
            {
                return EntryLength;
            }
        }
    }
}
