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
    /// <summary>
    /// LFS_RESTART_AREA
    /// </summary>
    public class LogRestartArea
    {
        public const int FixedLengthNTFS12 = 0x30;
        public const int FixedLengthNTFS31 = 0x40;
        public const ushort NoClients = 0xFFFF;

        public ulong CurrentLsn;
        // ushort LogClients;
        public ushort ClientFreeList;  // The index of the first free log client record in the array
        public ushort ClientInUseList; // The index of the first in-use log client record in the array
        public LogRestartFlags Flags;
        public uint SeqNumberBits;
        public ushort RestartAreaLength;
        // ushort ClientArrayOffset;
        public ulong FileSize;
        public uint LastLsnDataLength; // Not including the LFS_RECORD_HEADER
        public ushort RecordHeaderLength;
        public ushort LogPageDataOffset;
        // uint Unknown
        public List<LogClientRecord> LogClientArray = new List<LogClientRecord>();

        public LogRestartArea()
        {
            RecordHeaderLength = LogRecord.HeaderLength;
        }

        public LogRestartArea(byte[] buffer, int offset)
        {
            CurrentLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x00);
            ushort logClients = LittleEndianConverter.ToUInt16(buffer, offset + 0x08);
            ClientFreeList = LittleEndianConverter.ToUInt16(buffer, offset + 0x0A);
            ClientInUseList = LittleEndianConverter.ToUInt16(buffer, offset + 0x0C);
            Flags = (LogRestartFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x0E);
            SeqNumberBits = LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
            RestartAreaLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            ushort clientArrayOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x16);
            FileSize = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            LastLsnDataLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x20);
            RecordHeaderLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x24);
            LogPageDataOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x26);
            int position = offset + clientArrayOffset;
            for (int index = 0; index < logClients; index++)
            {
                LogClientRecord clientRecord = new LogClientRecord(buffer, position);
                LogClientArray.Add(clientRecord);
                position += clientRecord.Length;
            }
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            int clientArrayOffset = FixedLengthNTFS31;

            LittleEndianWriter.WriteUInt64(buffer, offset + 0x00, CurrentLsn);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x08, (ushort)LogClientArray.Count);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x0A, ClientFreeList);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x0C, ClientInUseList);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x0E, (ushort)Flags);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x10, SeqNumberBits);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x14, RestartAreaLength);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x16, (ushort)clientArrayOffset);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x18, FileSize);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x20, LastLsnDataLength);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x24, RecordHeaderLength);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x26, LogPageDataOffset);
            int position = offset + clientArrayOffset;
            foreach(LogClientRecord clientRecord in LogClientArray)
            {
                clientRecord.WriteBytes(buffer, position);
                position += clientRecord.Length;
            }
        }

        public int Length
        {
            get
            {
                int length = FixedLengthNTFS31;
                foreach (LogClientRecord clientRecord in LogClientArray)
                {
                    length += clientRecord.Length;
                }
                return length;
            }
        }
    }
}
