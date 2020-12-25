/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary
{
    public class GuidPartitionEntryCollection
    {
        public static int GetIndexOfPartitionGuid(List<GuidPartitionEntry> entries, Guid partitionGuid)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].PartitionGuid.Equals(partitionGuid))
                {
                    return index;
                }
            }
            return -1;
        }

        public static bool ContainsPartitionTypeGuid(List<GuidPartitionEntry> entries, Guid typeGuid)
        {
            int index = GetIndexOfPartitionTypeGuid(entries, typeGuid);
            return (index >= 0);
        }

        public static int GetIndexOfPartitionTypeGuid(List<GuidPartitionEntry> entries, Guid typeGuid)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].PartitionTypeGuid.Equals(typeGuid))
                {
                    return index;
                }
            }
            return -1;
        }

        public static byte[] GetBytes(GuidPartitionTableHeader header, List<GuidPartitionEntry> entries)
        {
            byte[] buffer = new byte[header.NumberOfPartitionEntries * header.SizeOfPartitionEntry];
            int count = (int)Math.Min(header.NumberOfPartitionEntries, entries.Count);
            for (int index = 0; index < count; index++)
            {
                entries[index].WriteBytes(buffer, index * (int)header.SizeOfPartitionEntry);
            }
            return buffer;
        }
    }
}
