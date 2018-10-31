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
    /// BITMAP_RANGE
    /// </summary>
    internal class BitmapRange
    {
        public const int Length = 8;

        public uint BitmapOffset;
        public uint NumberOfBits;

        public BitmapRange(uint bitmapOffset, uint numberOfBits)
        {
            BitmapOffset = bitmapOffset;
            NumberOfBits = numberOfBits;
        }

        public BitmapRange(byte[] buffer, int offset)
        {
            BitmapOffset = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            NumberOfBits = LittleEndianConverter.ToUInt32(buffer, offset + 0x04);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x00, BitmapOffset);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x04, NumberOfBits);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            WriteBytes(buffer, 0);
            return buffer;
        }
    }
}
