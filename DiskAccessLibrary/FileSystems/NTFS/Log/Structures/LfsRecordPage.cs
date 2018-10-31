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
    /// <remarks>
    /// This record should be read according to the version number specified in LogRestartPage.
    /// v1.0 use the LFS_UNPACKED_RECORD_PAGE structure, which is not being used in later versions.
    /// </remarks>
    public class LfsRecordPage
    {
        private const string ValidSignature = "RCRD";
        public const uint UninitializedPageSignature = 0xFFFFFFFF;
        private const int UpdateSequenceArrayOffset = 0x28;

        /* Start of LFS_RECORD_PAGE_HEADER */
        // MULTI_SECTOR_HEADER
        public ulong LastLsnOrFileOffset; // Last LSN that starts on this page for regular log pages, FileOffset for tail copies (indicates the location in the file where the page should be placed)
        public LfsRecordPageFlags Flags;
        public ushort PageCount; // Number of pages written as part of the IO transfer. a MultiPage record is likely to be written in two separate IO transfers (since the last page may have room for more records that will be written in a later transfer)
        public ushort PagePosition; // One-based
        /* Start of LFS_PACKED_RECORD_PAGE */
        public ushort NextRecordOffset; // The offset of the free space in the page, if the last record has MultiPage flag set this value is not incremented and will point to the start of the record.
        // ushort WordAlign
        // uint DWordAlign
        public ulong LastEndLsn; // Last LSN that ends on this page
        public ushort UpdateSequenceNumber; // a.k.a. USN
        // byte[] UpdateSequenceReplacementData
        /* End of LFS_PACKED_RECORD_PAGE */
        /* End of LFS_RECORD_PAGE_HEADER */
        public byte[] Data;

        private int m_dataOffset;

        public LfsRecordPage(int pageLength, int dataOffset)
        {
            Data = new byte[pageLength - dataOffset];
            m_dataOffset = dataOffset;
        }

        public LfsRecordPage(byte[] pageBytes, int dataOffset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(pageBytes, 0x00);
            if (multiSectorHeader.Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid RCRD record signature");
            }
            LastLsnOrFileOffset = LittleEndianConverter.ToUInt64(pageBytes, 0x08);
            Flags = (LfsRecordPageFlags)LittleEndianConverter.ToUInt32(pageBytes, 0x10);
            PageCount = LittleEndianConverter.ToUInt16(pageBytes, 0x14);
            PagePosition = LittleEndianConverter.ToUInt16(pageBytes, 0x16);
            NextRecordOffset = LittleEndianConverter.ToUInt16(pageBytes, 0x18);
            LastEndLsn = LittleEndianConverter.ToUInt64(pageBytes, 0x20);
            UpdateSequenceNumber = LittleEndianConverter.ToUInt16(pageBytes, multiSectorHeader.UpdateSequenceArrayOffset);
            Data = ByteReader.ReadBytes(pageBytes, dataOffset, pageBytes.Length - dataOffset);

            m_dataOffset = dataOffset;
        }

        public byte[] GetBytes(int bytesPerLogPage, bool applyUsaProtection)
        {
            int strideCount = bytesPerLogPage / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(ValidSignature, UpdateSequenceArrayOffset, updateSequenceArraySize);

            byte[] buffer = new byte[bytesPerLogPage];
            multiSectorHeader.WriteBytes(buffer, 0);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, LastLsnOrFileOffset);
            LittleEndianWriter.WriteUInt32(buffer, 0x10, (uint)Flags);
            LittleEndianWriter.WriteUInt16(buffer, 0x14, PageCount);
            LittleEndianWriter.WriteUInt16(buffer, 0x16, PagePosition);
            LittleEndianWriter.WriteUInt16(buffer, 0x18, NextRecordOffset);
            LittleEndianWriter.WriteUInt64(buffer, 0x20, LastEndLsn);
            LittleEndianWriter.WriteUInt16(buffer, UpdateSequenceArrayOffset, UpdateSequenceNumber);
            ByteWriter.WriteBytes(buffer, m_dataOffset, Data);

            if (applyUsaProtection)
            {
                MultiSectorHelper.ApplyUsaProtection(buffer, 0);
            }
            return buffer;
        }

        public LfsRecord ReadRecord(int recordOffset)
        {
            return new LfsRecord(Data, recordOffset - m_dataOffset);
        }

        public byte[] ReadBytes(int recordOffset, int bytesToRead)
        {
            return ByteReader.ReadBytes(Data, recordOffset - m_dataOffset, bytesToRead);
        }

        public void WriteBytes(int recordOffset, byte[] recordBytes)
        {
            WriteBytes(recordOffset, recordBytes, recordBytes.Length);
        }

        public void WriteBytes(int recordOffset, byte[] recordBytes, int bytesToWrite)
        {
            ByteWriter.WriteBytes(Data, recordOffset - m_dataOffset, recordBytes, bytesToWrite);
        }

        public bool HasRecordEnd
        {
            get
            {
                return (Flags & LfsRecordPageFlags.RecordEnd) != 0;
            }
            set
            {
                if (value)
                {
                    Flags |= LfsRecordPageFlags.RecordEnd;
                }
                else
                {
                    Flags &= ~LfsRecordPageFlags.RecordEnd;
                }
            }
        }

        public static int GetDataOffset(int bytesPerLogPage)
        {
            int strideCount = bytesPerLogPage / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            return (int)Math.Ceiling((double)(UpdateSequenceArrayOffset + updateSequenceArraySize * 2) / 8) * 8;
        }
    }
}
