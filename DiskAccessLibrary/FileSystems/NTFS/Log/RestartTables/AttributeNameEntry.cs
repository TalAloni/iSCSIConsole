/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// <summary>
    /// ATTRIBUTE_NAME_ENTRY
    /// </summary>
    public class AttributeNameEntry
    {
        public const int FixedLength = 4;

        public ushort OpenAttributeOffset; // Offset of the attibute with this name in the open attribute table
        // ushort NameLength;
        public string Name; // Null terminated

        public AttributeNameEntry()
        {
            Name = String.Empty;
        }

        public AttributeNameEntry(byte[] buffer, int offset)
        {
            OpenAttributeOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x00);
            ushort nameLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x02);
            Name = UnicodeEncoding.Unicode.GetString(buffer, offset + 0x04, nameLength);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x00, OpenAttributeOffset);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x02, (ushort)(Name.Length * 2));
            ByteWriter.WriteUTF16String(buffer, offset + 0x04, Name + "\0");
        }

        public int Length
        {
            get
            {
                // All entries are null terminated except the terminating entry
                return FixedLength + (Name.Length + 1) * 2;
            }
        }

        public static List<AttributeNameEntry> ReadTable(byte[] tableBytes)
        {
            List<AttributeNameEntry> result = new List<AttributeNameEntry>();
            int offset = 0;
            while (offset < tableBytes.Length)
            {
                AttributeNameEntry entry = new AttributeNameEntry(tableBytes, offset);
                if (entry.OpenAttributeOffset == 0 || entry.Name.Length == 0)
                {
                    break;
                }
                result.Add(entry);
                offset += entry.Length;
            }
            return result;
        }

        public static byte[] GetTableBytes(List<AttributeNameEntry> entries)
        {
            int tableLength = 0;
            foreach (AttributeNameEntry entry in entries)
            {
                tableLength += entry.Length;
            }

            tableLength += AttributeNameEntry.FixedLength; // Terminating entry

            byte[] tableBytes = new byte[tableLength];
            int offset = 0;
            foreach (AttributeNameEntry entry in entries)
            {
                entry.WriteBytes(tableBytes, offset);
                offset += entry.Length;
            }

            // No need to write the terminating entry, the 0's are already in place
            return tableBytes;
        }
    }
}
