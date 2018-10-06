/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// MULTI_SECTOR_HEADER: https://docs.microsoft.com/en-us/windows/desktop/devnotes/multi-sector-header
    /// </summary>
    public class MultiSectorHeader
    {
        public const int Length = 8;
        private const int SignatureLength = 4;

        private string m_signature; // 4 bytes
        private ushort m_updateSequenceArrayOffset;
        private ushort m_updateSequenceArraySize; // The number of USHORT entries (The USN and the missing 2 bytes from each stride)

        public MultiSectorHeader(string signature, ushort updateSequenceArrayOffset, ushort updateSequenceArraySize)
        {
            m_signature = signature;
            m_updateSequenceArrayOffset = updateSequenceArrayOffset;
            m_updateSequenceArraySize = updateSequenceArraySize;
        }

        public MultiSectorHeader(byte[] buffer, int offset)
        {
            m_signature = ByteReader.ReadAnsiString(buffer, offset + 0x00, SignatureLength);
            m_updateSequenceArrayOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x04);
            m_updateSequenceArraySize = LittleEndianConverter.ToUInt16(buffer, offset + 0x06);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            ByteWriter.WriteAnsiString(buffer, offset + 0x00, m_signature, SignatureLength);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x04, m_updateSequenceArrayOffset);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x06, m_updateSequenceArraySize);
        }

        public string Signature
        {
            get
            {
                return m_signature;
            }
        }

        public ushort UpdateSequenceArrayOffset
        {
            get
            {
                return m_updateSequenceArrayOffset;
            }
        }

        public ushort UpdateSequenceArraySize
        {
            get
            {
                return m_updateSequenceArraySize;
            }
        }
    }
}
