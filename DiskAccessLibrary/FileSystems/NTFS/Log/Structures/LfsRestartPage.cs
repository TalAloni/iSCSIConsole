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
    /// Windows NT 3.51 sets the version to 1.0
    /// Windows NT 4.0 and later set the version to 1.1
    /// </remarks>
    public class LfsRestartPage
    {
        private const string ValidSignature = "RSTR";
        private const string ModifiedSignature = "CHKD"; // Indicates that CHKDSK was run
        private const int UpdateSequenceArrayOffset = 0x1E;

        /* Start of LFS_RESTART_PAGE_HEADER */
        // MULTI_SECTOR_HEADER
        public ulong ChkDskLsn;
        private uint m_systemPageSize;
        public uint LogPageSize;
        // ushort RestartOffset;
        public short MinorVersion;
        public short MajorVersion;
        public ushort UpdateSequenceNumber; // a.k.a. USN
        // byte[] UpdateSequenceReplacementData
        /* End of LFS_RESTART_PAGE_HEADER */
        public LfsRestartArea LogRestartArea;

        public LfsRestartPage()
        {
            LogRestartArea = new LfsRestartArea();
            MinorVersion = 1;
            MajorVersion = 1;
        }

        public LfsRestartPage(byte[] buffer, int offset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            if (multiSectorHeader.Signature != ValidSignature && multiSectorHeader.Signature != ModifiedSignature)
            {
                throw new InvalidDataException("Invalid RSTR record signature");
            }
            ChkDskLsn = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            m_systemPageSize = LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
            LogPageSize = LittleEndianConverter.ToUInt32(buffer, offset + 0x14);
            ushort restartOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x18);
            MinorVersion = LittleEndianConverter.ToInt16(buffer, offset + 0x1A);
            MajorVersion = LittleEndianConverter.ToInt16(buffer, offset + 0x1C);
            UpdateSequenceNumber = LittleEndianConverter.ToUInt16(buffer, offset + multiSectorHeader.UpdateSequenceArrayOffset);
            LogRestartArea = new LfsRestartArea(buffer, offset + restartOffset);
        }

        public byte[] GetBytes(int bytesPerSystemPage, bool applyUsaProtection)
        {
            m_systemPageSize = (uint)bytesPerSystemPage;
            int strideCount = bytesPerSystemPage / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(ValidSignature, UpdateSequenceArrayOffset, updateSequenceArraySize);
            int restartOffset = (int)Math.Ceiling((double)(UpdateSequenceArrayOffset + updateSequenceArraySize * 2) / 8) * 8;

            byte[] buffer = new byte[bytesPerSystemPage];
            multiSectorHeader.WriteBytes(buffer, 0);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, ChkDskLsn);
            LittleEndianWriter.WriteUInt32(buffer, 0x10, m_systemPageSize);
            LittleEndianWriter.WriteUInt32(buffer, 0x14, LogPageSize);
            LittleEndianWriter.WriteUInt16(buffer, 0x18, (ushort)restartOffset);
            LittleEndianWriter.WriteInt16(buffer, 0x1A, MinorVersion);
            LittleEndianWriter.WriteInt16(buffer, 0x1C, MajorVersion);
            LittleEndianWriter.WriteUInt16(buffer, UpdateSequenceArrayOffset, UpdateSequenceNumber);
            LogRestartArea.WriteBytes(buffer, restartOffset);

            if (applyUsaProtection)
            {
                MultiSectorHelper.ApplyUsaProtection(buffer, 0);
            }
            return buffer;
        }

        public static uint GetSystemPageSize(byte[] buffer, int offset)
        {
            return LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
        }

        public uint SystemPageSize
        {
            get
            {
                return m_systemPageSize;
            }
        }
    }
}
