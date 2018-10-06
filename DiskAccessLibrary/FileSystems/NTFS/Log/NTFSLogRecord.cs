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
    public class NTFSLogRecord
    {
        private const int FixedLength = 32;

        /* Start of NTFS_LOG_RECORD_HEADER */
        public NTFSLogOperation RedoOperation;
        public NTFSLogOperation UndoOperation;
        // ushort RedoOffset; // Offset MUST be aligned to 8 byte boundary
        // ushort RedoLength;
        // ushort UndoOffset; // Offset MUST be aligned to 8 byte boundary
        // ushort UndoLength;
        public ushort TargetAttributeIndex; // 0 for the MFT itself
        // ushort LCNsToFollow;
        public ushort RecordOffset;
        public ushort AttributeOffset;
        public ushort ClusterBlockOffset;
        public ushort Reserved;
        public long TargetVCN;
        public List<long> LCNsForPage = new List<long>();
        /* End of NTFS_LOG_RECORD_HEADER */
        public byte[] RedoData;
        public byte[] UndoData;

        public NTFSLogRecord()
        {
            RedoData = new byte[0];
            UndoData = new byte[0];
        }

        public NTFSLogRecord(byte[] recordBytes)
        {
            RedoOperation = (NTFSLogOperation)LittleEndianConverter.ToUInt16(recordBytes, 0x00);
            UndoOperation = (NTFSLogOperation)LittleEndianConverter.ToUInt16(recordBytes, 0x02);
            ushort redoOffset = LittleEndianConverter.ToUInt16(recordBytes, 0x04);
            ushort redoLength = LittleEndianConverter.ToUInt16(recordBytes, 0x06);
            ushort undoOffset = LittleEndianConverter.ToUInt16(recordBytes, 0x08);
            ushort undoLength = LittleEndianConverter.ToUInt16(recordBytes, 0x0A);
            TargetAttributeIndex = LittleEndianConverter.ToUInt16(recordBytes, 0x0C);
            ushort lcnsToFollow = LittleEndianConverter.ToUInt16(recordBytes, 0x0E);
            RecordOffset = LittleEndianConverter.ToUInt16(recordBytes, 0x10);
            AttributeOffset = LittleEndianConverter.ToUInt16(recordBytes, 0x12);
            ClusterBlockOffset = LittleEndianConverter.ToUInt16(recordBytes, 0x14);
            Reserved = LittleEndianConverter.ToUInt16(recordBytes, 0x16);
            TargetVCN = (long)LittleEndianConverter.ToUInt64(recordBytes, 0x18);
            for (int index = 0; index < lcnsToFollow; index++)
            {
                long lcn = (long)LittleEndianConverter.ToUInt64(recordBytes, 0x20 + index * 8);
                LCNsForPage.Add(lcn);
            }
            /*int dataOffset = 0x20 + lcnsToFollow * 8;
            int dataLength = recordBytes.Length - dataOffset;*/
            RedoData = ByteReader.ReadBytes(recordBytes, redoOffset, redoLength);
            if (undoOffset == redoOffset && undoLength == redoLength)
            {
                UndoData = RedoData;
            }
            else if (UndoOperation != NTFSLogOperation.CompensationLogRecord)
            {
                UndoData = ByteReader.ReadBytes(recordBytes, undoOffset, undoLength);
            }
            else
            {
                // This record is logging the undo of a previous operation (e.g. when aborting a transaction)
                UndoData = new byte[0];
            }
        }

        public byte[] GetBytes()
        {
            int redoDataOffset = 0x20 + LCNsForPage.Count * 8;
            int undoDataOffset;
            if (UndoData == RedoData)
            {
                undoDataOffset = redoDataOffset;
            }
            else
            {
                undoDataOffset = redoDataOffset + (int)Math.Ceiling((double)RedoData.Length / 8) * 8;
            }

            byte[] recordBytes = new byte[this.Length];
            LittleEndianWriter.WriteUInt16(recordBytes, 0x00, (ushort)RedoOperation);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x02, (ushort)UndoOperation);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x04, (ushort)redoDataOffset);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x06, (ushort)RedoData.Length);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x08, (ushort)undoDataOffset);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x0A, (ushort)UndoData.Length);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x0C, TargetAttributeIndex);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x0E, (ushort)LCNsForPage.Count);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x10, RecordOffset);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x12, AttributeOffset);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x14, ClusterBlockOffset);
            LittleEndianWriter.WriteUInt16(recordBytes, 0x16, Reserved);
            LittleEndianWriter.WriteUInt64(recordBytes, 0x18, (ulong)TargetVCN);
            for (int index = 0; index < LCNsForPage.Count; index++)
            {
                LittleEndianWriter.WriteUInt64(recordBytes, 0x20 + index * 8, (ulong)LCNsForPage[index]);
            }
            ByteWriter.WriteBytes(recordBytes, undoDataOffset, UndoData);
            if (UndoData != RedoData)
            {
                ByteWriter.WriteBytes(recordBytes, redoDataOffset, RedoData);
            }
            return recordBytes;
        }

        public int Length
        {
            get
            {
                int length = FixedLength + LCNsForPage.Count * 8;
                if (UndoData == RedoData)
                {
                    length += UndoData.Length;
                }
                else
                {
                    length += (int)Math.Ceiling((double)UndoData.Length / 8);
                    length += RedoData.Length;
                }
                return length;
            }
        }
    }
}
