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
    /// ATTRIBUTE_DEFINITION_COLUMNS
    /// </summary>
    public class AttributeDefinitionEntry
    {
        public const int Length = 160;
        private const int AttributeNameLength = 64; // Number of characters

        public string AttributeName;
        public AttributeType AttributeType;
        public uint DisplayRule;
        public CollationRule CollationRule;
        public AttributeDefinitionFlags Flags;
        public ulong MinimumLength;
        public ulong MaximumLength;

        public AttributeDefinitionEntry()
        {
        }

        public AttributeDefinitionEntry(byte[] buffer, int offset)
        {
            AttributeName = ByteReader.ReadUTF16String(buffer, offset + 0x00, AttributeNameLength).TrimEnd(new char[] { '\0' });
            AttributeType = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + 0x80);
            DisplayRule = LittleEndianConverter.ToUInt32(buffer, offset + 0x84);
            CollationRule = (CollationRule)LittleEndianConverter.ToUInt32(buffer, offset + 0x88);
            Flags = (AttributeDefinitionFlags)LittleEndianConverter.ToUInt32(buffer, offset + 0x8C);
            MinimumLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x90);
            MaximumLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x98);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            ByteWriter.WriteUTF16String(buffer, offset + 0x00, AttributeName.PadRight(AttributeNameLength, '\0'), AttributeNameLength);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x80, (uint)AttributeType);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x84, DisplayRule);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x88, (uint)CollationRule);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x8C, (uint)Flags);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x90, MinimumLength);
            LittleEndianWriter.WriteUInt64(buffer, offset + 0x98, MaximumLength);
        }
    }
}
