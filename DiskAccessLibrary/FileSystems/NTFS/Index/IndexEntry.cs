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
    /// INDEX_ENTRY
    /// </summary>
    public class IndexEntry
    {
        public const int FixedLength = 16;

        public MftSegmentReference FileReference;
        // ushort EntryLength;
        // ushort AttributeLength;
        public IndexEntryFlags Flags;
        // 2 zero bytes (padding)
        public byte[] Key;
        /// <summary>In units of clusters when IndexRootRecord.BytesPerIndexRecord >= Volume.BytesPerCluster, otherwise in units of 512 byte blocks.</summary>
        /// <remarks>Present if ParentNodeForm flag is set</remarks>
        public long SubnodeVBN; // Stored as ulong but can be represented using long

        public IndexEntry()
        {
            FileReference = MftSegmentReference.NullReference;
            Key = new byte[0];
        }

        public IndexEntry(MftSegmentReference fileReference, byte[] key)
        {
            FileReference = fileReference;
            Key = key;
        }

        public IndexEntry(byte[] buffer, int offset) : this(buffer, ref offset)
        {
        }

        public IndexEntry(byte[] buffer, ref int offset)
        {
            FileReference = new MftSegmentReference(buffer, offset + 0x00);
            ushort entryLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x08);
            ushort keyLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x0A);
            Flags = (IndexEntryFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x0C);
            Key = ByteReader.ReadBytes(buffer, offset + 0x10, keyLength);
            if (ParentNodeForm)
            {
                // Key is padded to align to 8 byte boundary
                int keyLengthWithPadding = (int)Math.Ceiling((double)keyLength / 8) * 8;
                SubnodeVBN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10 + keyLengthWithPadding);
            }
            offset += entryLength;
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            ushort entryLength = (ushort)this.Length;
            FileReference.WriteBytes(buffer, offset + 0x00);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x08, entryLength);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x0A, (ushort)Key.Length);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x0C, (ushort)Flags);
            ByteWriter.WriteBytes(buffer, offset + 0x10, Key);
            if (ParentNodeForm)
            {
                LittleEndianWriter.WriteUInt64(buffer, offset + entryLength - 8, (ulong)SubnodeVBN);
            }
        }

        public bool IsLastEntry
        {
            get
            {
                return ((Flags & IndexEntryFlags.LastEntryInNode) > 0);
            }
            set
            {
                if (value)
                {
                    Flags |= IndexEntryFlags.LastEntryInNode;
                }
                else
                {
                    Flags &= ~IndexEntryFlags.LastEntryInNode;
                }
            }
        }

        public bool ParentNodeForm
        {
            get
            {
                return ((Flags & IndexEntryFlags.ParentNodeForm) > 0);
            }
            set
            {
                if (value)
                {
                    Flags |= IndexEntryFlags.ParentNodeForm;
                }
                else
                {
                    Flags &= ~IndexEntryFlags.ParentNodeForm;
                }
            }
        }

        public int Length
        {
            get
            {
                // Key MUST be padded to align to 8 byte boundary
                int keyPaddedLength = (int)Math.Ceiling((double)Key.Length / 8) * 8;
                ushort entryLength = (ushort)(FixedLength + keyPaddedLength);
                if (ParentNodeForm)
                {
                    entryLength += 8;
                }
                return entryLength;
            }
        }

        public static int GetLength(List<IndexEntry> entries)
        {
            int length = 0;
            foreach (IndexEntry entry in entries)
            {
                length += entry.Length;
            }

            if (entries.Count == 0 || !entries[entries.Count - 1].ParentNodeForm)
            {
                length += FixedLength;
            }
            return length;
        }

        public static List<IndexEntry> ReadIndexEntries(byte[] buffer, int offset)
        {
            List<IndexEntry> entries = new List<IndexEntry>();
            while (true)
            {
                IndexEntry entry = new IndexEntry(buffer, ref offset);
                if (entry.IsLastEntry && !entry.ParentNodeForm)
                {
                    break;
                }
                entries.Add(entry);
                if (entry.IsLastEntry)
                {
                    break;
                }
            }
            return entries;
        }

        public static void WriteIndexEntries(byte[] buffer, int offset, List<IndexEntry> entries)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                IndexEntry entry = entries[index];
                if (index == entries.Count - 1)
                {
                    entry.IsLastEntry = entry.ParentNodeForm;
                }
                else
                {
                    entry.IsLastEntry = false;
                }
                entry.WriteBytes(buffer, offset);
                offset += entry.Length;
            }

            if (entries.Count == 0 || !entries[entries.Count - 1].IsLastEntry)
            {
                IndexEntry lastEntry = new IndexEntry();
                lastEntry.IsLastEntry = true;
                lastEntry.WriteBytes(buffer, offset);
            }
        }
    }
}
