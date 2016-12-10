/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    // http://blogs.technet.com/b/askcore/archive/2009/10/16/the-four-stages-of-ntfs-file-growth.aspx
    // Notes: 
    // 1. A file can only have one attribute list and the $ATTRIBUTE_LIST record must reside in the base record segment
    // 2. AttributeList record is not necessarily resident.
    // 3. AttributeList can point to both resident and non-resident records
    public class AttributeListRecord
    {
        private NTFSVolume m_volume;
        private AttributeRecord m_record;
        public List<AttributeListEntry> AttributeList = new List<AttributeListEntry>();

        public AttributeListRecord(NTFSVolume volume, AttributeRecord record)
        {
            m_volume = volume;
            m_record = record;

            byte[] data = m_record.GetData(volume);

            int position = 0;
            while (position < data.Length)
            {
                AttributeListEntry entry = new AttributeListEntry(data, position);
                AttributeList.Add(entry);
                position += entry.Length;
                
                if (entry.Length < AttributeListEntry.HeaderLength)
                {
                    string message = String.Format("Invalid attribute list entry, data length: {0}, position: {1}", data.Length, position);
                    throw new InvalidDataException(message);
                }
            }
        }

        /// <summary>
        /// Return list containing the segment reference to all of the segments that are listed in this attribute list
        /// </summary>
        public List<MftSegmentReference> GetSegmentReferenceList()
        {
            List<MftSegmentReference> result = new List<MftSegmentReference>();
            foreach (AttributeListEntry entry in AttributeList)
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
