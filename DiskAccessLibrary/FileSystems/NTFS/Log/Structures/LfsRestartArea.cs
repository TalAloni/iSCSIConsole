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
    public class LfsRestartArea
    {
        public const int FixedLengthNTFS12 = 0x30; // Note: Windows NT 4.0 uses 0x30, Windows NT 3.51 uses 0x28
        public const int FixedLengthNTFS31 = 0x40;
        public const ushort NoClient = 0xFFFF;

        public ulong CurrentLsn;
        // ushort LogClients;
        public ushort ClientFreeList;  // The index of the first free log client record in the array
        public ushort ClientInUseList; // The index of the first in-use log client record in the array
        public LfsRestartFlags Flags;
        public uint SeqNumberBits;
        public ushort RestartAreaLength;
        // ushort ClientArrayOffset;
        public ulong FileSize;
        public uint LastLsnDataLength; // Not including the LFS_RECORD_HEADER
        public ushort RecordHeaderLength;
        public ushort LogPageDataOffset;
        public uint RevisionNumber; // This value is incremented by 1 every time the LogRestartArea is being written (initial value is chosen at random)
        public List<LfsClientRecord> LogClientArray = new List<LfsClientRecord>();

        public LfsRestartArea()
        {
            RecordHeaderLength = LfsRecord.HeaderLength;
        }

        public LfsRestartArea(byte[] buffer, int offset)
        {
            CurrentLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x00);
            ushort logClients = LittleEndianConverter.ToUInt16(buffer, offset + 0x08);
            ClientFreeList = LittleEndianConverter.ToUInt16(buffer, offset + 0x0A);
            ClientInUseList = LittleEndianConverter.ToUInt16(buffer, offset + 0x0C);
            Flags = (LfsRestartFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x0E);
            SeqNumberBits = LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
            RestartAreaLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            ushort clientArrayOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x16);
            FileSize = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            LastLsnDataLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x20);
            RecordHeaderLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x24);
            LogPageDataOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x26);
            if (clientArrayOffset >= 0x30)
            {
                RevisionNumber = LittleEndianConverter.ToUInt32(buffer, offset + 0x28);
            }
            int position = offset + clientArrayOffset;
            for (int index = 0; index < logClients; index++)
            {
                LfsClientRecord clientRecord = new LfsClientRecord(buffer, position);
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
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x28, RevisionNumber);
            int position = offset + clientArrayOffset;
            foreach (LfsClientRecord clientRecord in LogClientArray)
            {
                clientRecord.WriteBytes(buffer, position);
                position += clientRecord.Length;
            }
        }

        /// <summary>
        /// The number of bits chosen to represent the log file size
        /// (must be greater than or equal to the number of bits needed)
        /// </summary>
        public int FileSizeBits
        {
            get
            {
                return 64 - (int)SeqNumberBits + 3;
            }
            set
            {
                // All log records are aligned to 8-byte boundary
                SeqNumberBits = (uint)(64 - (value - 3));
            }
        }

        /// <summary>
        /// Windows 2000 and earlier will close the log file by setting the
        /// ClientInUseList to NoClient when the volume is dismounted cleanly.
        /// </summary>
        public bool IsInUse
        {
            get
            {
                return (ClientInUseList != NoClient);
            }
        }

        /// <summary>
        /// Windows XP and later will set the clean bit when the volume is dismounted cleanly.
        /// </summary>
        public bool IsClean
        {
            get
            {
                return (Flags & LfsRestartFlags.CleanDismount) != 0;
            }
            set
            {
                if (value)
                {
                    Flags |= LfsRestartFlags.CleanDismount;
                }
                else
                {
                    Flags &= ~LfsRestartFlags.CleanDismount;
                }
            }
        }

        public int Length
        {
            get
            {
                int length = FixedLengthNTFS31;
                foreach (LfsClientRecord clientRecord in LogClientArray)
                {
                    length += clientRecord.Length;
                }
                return length;
            }
        }
    }
}
