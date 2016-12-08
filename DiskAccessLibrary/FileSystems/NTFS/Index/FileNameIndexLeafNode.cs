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
    public class FileNameIndexLeafNode
    {
        public List<FileNameIndexEntry> Entries = new List<FileNameIndexEntry>();

        public FileNameIndexLeafNode(byte[] buffer, int offset)
        {
            int position = offset;
            while (true)
            {
                FileNameIndexEntry entry = new FileNameIndexEntry(buffer, position);
                if (entry.IsLastEntry)
                {
                    break;
                }
                Entries.Add(entry);
                position += entry.RecordLength;

                if (entry.RecordLength == 0)
                {
                    throw new InvalidDataException("Invalid FileName index entry");
                }
            }
        }
    }
}
