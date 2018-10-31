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
    /// RESTART_TABLE
    /// </summary>
    public class RestartTableHeader
    {
        public const int Length = 24;
        
        public ushort EntrySize;
        public ushort NumberEntries;
        public ushort NumberAllocated;
        // 6 reserved bytes
        public uint FreeGoal;
        public uint FirstFree;
        public uint LastFree;

        public RestartTableHeader()
        {
        }

        public RestartTableHeader(byte[] buffer, int offset)
        {
            EntrySize = LittleEndianConverter.ToUInt16(buffer, offset + 0x00);
            NumberEntries = LittleEndianConverter.ToUInt16(buffer, offset + 0x02);
            NumberAllocated = LittleEndianConverter.ToUInt16(buffer, offset + 0x04);
            FreeGoal = LittleEndianConverter.ToUInt32(buffer, offset + 0x0C);
            FirstFree = LittleEndianConverter.ToUInt32(buffer, offset + 0x10);
            LastFree = LittleEndianConverter.ToUInt32(buffer, offset + 0x14);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x00, EntrySize);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x02, NumberEntries);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x04, NumberAllocated);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x0C, FreeGoal);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x10, FirstFree);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x14, LastFree);
        }
    }
}
