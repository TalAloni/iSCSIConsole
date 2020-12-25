/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// LFS_CLIENT_RECORD
    /// </summary>
    public class LfsClientRecord
    {
        public const int ClientNameMaxLength = 64; // 64 unicode characters
        public const int Length = 0x20 + ClientNameMaxLength * 2;

        public ulong OldestLsn;
        public ulong ClientRestartLsn;
        public ushort PrevClient;
        public ushort NextClient;
        public ushort SeqNumber;
        // ushort AlignWord
        // uint AlignDWord
        // uint ClientNameLength // Number of bytes
        public string ClientName;

        public LfsClientRecord(string clientName)
        {
            ClientName = clientName;
        }

        public LfsClientRecord(byte[] buffer, int offset)
        {
            OldestLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x00);
            ClientRestartLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            PrevClient = LittleEndianConverter.ToUInt16(buffer, offset + 0x10);
            NextClient = LittleEndianConverter.ToUInt16(buffer, offset + 0x12);
            SeqNumber = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            uint clientNameLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x1C);
            ClientName = ByteReader.ReadUTF16String(buffer, offset + 0x20, (int)(clientNameLength / 2));
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x00, OldestLsn);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x08, ClientRestartLsn);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x10, PrevClient);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x12, NextClient);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x14, SeqNumber);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x1C, (uint)(ClientName.Length * 2));
            ByteWriter.WriteUTF16String(buffer, offset + 0x20, ClientName);
        }
    }
}
