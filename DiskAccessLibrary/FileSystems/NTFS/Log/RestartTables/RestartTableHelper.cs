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
    public class RestartTableHelper
    {
        public static bool IsPointerValid(uint nextFree, int entrySize, int tableSize)
        {
            if (nextFree == 0)
            {
                return true;
            }

            return (nextFree >= RestartTableHeader.Length) || ((nextFree - RestartTableHeader.Length) % entrySize == 0) || (nextFree < tableSize);
        }

        public static List<T> ReadTable<T>(byte[] tableBytes, uint majorVersion) where T : RestartTableEntry
        {
            List<T> result = new List<T>();
            RestartTableHeader header = new RestartTableHeader(tableBytes, 0);
            int offset = RestartTableHeader.Length;
            while (offset < tableBytes.Length)
            {
                uint allocatedOrNextFree = LittleEndianConverter.ToUInt32(tableBytes, offset + 0x00);
                if (allocatedOrNextFree == RestartTableEntry.RestartEntryAllocated)
                {
                    T entry = RestartTableEntry.ReadEntry<T>(tableBytes, offset, majorVersion);
                    result.Add(entry);
                }
                else if (!IsPointerValid(allocatedOrNextFree, header.EntrySize, tableBytes.Length))
                {
                    throw new InvalidDataException("Invalid restart table entry, AllocatedOrNextFree points to invalid location");
                }
                offset += header.EntrySize;
            }
            return result;
        }

        public static byte[] GetTableBytes<T>(List<T> entries) where T : RestartTableEntry
        {
            int tableLength = RestartTableHeader.Length;
            int entryLength = 0;
            foreach (T entry in entries)
            {
                if (entry.Length > entryLength)
                {
                    entryLength = entry.Length;
                }
            }
            tableLength += entries.Count * entryLength;
            byte[] tableBytes = new byte[tableLength];
            RestartTableHeader header = new RestartTableHeader();
            header.EntrySize = (ushort)entryLength;
            header.NumberEntries = (ushort)entries.Count;
            header.NumberAllocated = (ushort)entries.Count;
            header.FreeGoal = UInt32.MaxValue;
            header.FirstFree = 0;
            header.LastFree = 0;
            header.WriteBytes(tableBytes, 0);
            for (int index = 0; index < entries.Count; index++)
            {
                entries[index].AllocatedOrNextFree = RestartTableEntry.RestartEntryAllocated;
                entries[index].WriteBytes(tableBytes, RestartTableHeader.Length + index * entryLength);
            }
            return tableBytes;
        }
    }
}
