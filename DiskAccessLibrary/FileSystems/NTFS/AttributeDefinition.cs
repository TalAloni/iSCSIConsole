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
    public class AttributeDefinition : NTFSFile
    {
        private List<AttributeDefinitionEntry> m_list;

        public AttributeDefinition(NTFSVolume volume) : base(volume, MasterFileTable.AttrDefSegmentReference)
        {
        }

        public List<AttributeDefinitionEntry> ReadList()
        {
            ulong position = 0;
            List<AttributeDefinitionEntry> entries = new List<AttributeDefinitionEntry>();
            while (position < this.Data.Length)
            {
                byte[] entryBytes = this.ReadData(position, AttributeDefinitionEntry.Length);
                entries.Add(new AttributeDefinitionEntry(entryBytes, 0));
                position += AttributeDefinitionEntry.Length;
            }
            return entries;
        }

        public List<AttributeDefinitionEntry> List
        {
            get
            {
                if (m_list == null)
                {
                    m_list = ReadList();
                }
                return m_list;
            }
        }
    }
}
