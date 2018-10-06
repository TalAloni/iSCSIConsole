/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// 1. A file can only have one attribute list and the $ATTRIBUTE_LIST record must reside in the base record segment.
    /// 2. The attribute list's data is not necessarily resident.
    /// 3. An attribute list can point to both resident and non-resident attributes.
    /// </remarks>
    /// http://blogs.technet.com/b/askcore/archive/2009/10/16/the-four-stages-of-ntfs-file-growth.aspx
    public class AttributeList : AttributeData
    {
        public AttributeList(NTFSVolume volume, AttributeRecord attributeRecord) : base(volume, null, attributeRecord)
        {
        }

        public List<AttributeListEntry> ReadEntries()
        {
            byte[] data = ReadClusters(0, (int)ClusterCount);

            List<AttributeListEntry> entries = new List<AttributeListEntry>();
            int position = 0;
            while (position < data.Length)
            {
                AttributeListEntry entry = new AttributeListEntry(data, position);
                entries.Add(entry);
                position += entry.LengthOnDisk;
            }

            return entries;
        }

        public void WriteEntries(List<AttributeListEntry> entries)
        {
            byte[] data = GetBytes(entries);
            WriteBytes(0, data);
            if ((uint)this.Length > data.Length)
            {
                Truncate((uint)data.Length);
            }
        }

        public static int GetLength(List<AttributeListEntry> entries)
        {
            int result = 0;
            foreach (AttributeListEntry entry in entries)
            {
                result += entry.Length;
            }
            return result;
        }

        public static byte[] GetBytes(List<AttributeListEntry> entries)
        {
            int length = GetLength(entries);
            byte[] buffer = new byte[length];
            int position = 0;
            foreach (AttributeListEntry entry in entries)
            {
                entry.WriteBytes(buffer, position);
                position += entry.Length;
            }
            return buffer;
        }

        /// <summary>
        /// Return list containing the segment reference to all of the segments that are listed in this attribute list
        /// </summary>
        public static List<MftSegmentReference> GetSegmentReferenceList(List<AttributeListEntry> entries)
        {
            List<MftSegmentReference> result = new List<MftSegmentReference>();
            foreach (AttributeListEntry entry in entries)
            {
                if (!MftSegmentReference.ContainsSegmentNumber(result, entry.SegmentReference.SegmentNumber))
                {
                    result.Add(entry.SegmentReference);
                }
            }
            return result;
        }
    }
}
