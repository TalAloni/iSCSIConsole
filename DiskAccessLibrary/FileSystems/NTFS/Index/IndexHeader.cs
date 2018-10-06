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
    public class IndexHeader
    {
        public const int Length = 16;

        public uint EntriesOffset;   // Relative to Index Header start offset
        public uint TotalLength;     // Length from the start of the header to the end of the last entry, a.k.a. FirstFreeByte
        public uint AllocatedLength; // BytesAvailable
        public IndexHeaderFlags IndexFlags;
        // 3 zero bytes

        public IndexHeader()
        {
        }

        public IndexHeader(byte[] buffer, int offset)
        {
            EntriesOffset = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            TotalLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x04);
            AllocatedLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x08);
            IndexFlags = (IndexHeaderFlags)ByteReader.ReadByte(buffer, offset + 0x0C);
            // 3 zero bytes (padding to 8-byte boundary)
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x00, EntriesOffset);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x04, TotalLength);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x08, AllocatedLength);
            ByteWriter.WriteByte(buffer, offset + 0x0C, (byte)IndexFlags);
        }

        public bool IsParentNode
        {
            get
            {
                return (IndexFlags & IndexHeaderFlags.ParentNode) > 0;
            }
            set
            {
                if (value)
                {
                    IndexFlags |= IndexHeaderFlags.ParentNode;
                }
                else
                {
                    IndexFlags &= ~IndexHeaderFlags.ParentNode;
                }
            }
        }
    }
}
