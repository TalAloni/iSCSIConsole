/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class FileNameIndexEntry // leaf entry
    {
        public MftSegmentReference FileReference;
        public ushort RecordLength;
        public ushort FileNameOffset;
        public IndexEntryFlags IndexFlags;
        // 2 zero bytes (padding)
        public FileNameRecord Record;

        public FileNameIndexEntry(byte[] buffer, int offset)
        {
            FileReference = new MftSegmentReference(buffer, offset + 0x00);
            RecordLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x08);
            FileNameOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x0A);
            IndexFlags = (IndexEntryFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x0C);

            if (RecordLength > 16)// otherwise it's the last record
            {
                Record = new FileNameRecord(buffer, offset + 0x10);
            }
        }

        public bool IsLastEntry
        {
            get
            {
                return ((IndexFlags & IndexEntryFlags.LastEntryInNode) > 0);
            }
        }

        public string FileName
        {
            get
            {
                return Record.FileName;
            }
        }

        public FilenameNamespace Namespace
        {
            get
            {
                return Record.Namespace;
            }
        }
    }
}
