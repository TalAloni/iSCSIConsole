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
    public class MftSegmentReference
    {
        public const int Length = 8;

        public long SegmentNumber; // 6 bytes
        public ushort SequenceNumber;
        
        public MftSegmentReference(byte[] buffer, int offset)
        {
            SegmentNumber = (long)(LittleEndianConverter.ToUInt64(buffer, offset + 0x00) & 0xFFFFFFFFFFFF);
            SequenceNumber = LittleEndianConverter.ToUInt16(buffer, offset + 0x06);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteInt64(buffer, offset + 0x00, SegmentNumber & 0xFFFFFFFFFFFF);
            LittleEndianWriter.WriteUInt16(buffer, offset + 0x06, SequenceNumber);
        }

        public static int IndexOfSegmentNumber(List<MftSegmentReference> list, long segmentNumber)
        {
            for(int index = 0; index < list.Count; index++)
            {
                if (list[index].SegmentNumber == segmentNumber)
                {
                    return index;
                }
            }
            return -1;
        }

        public static bool ContainsSegmentNumber(List<MftSegmentReference> list, long segmentNumber)
        {
            return (IndexOfSegmentNumber(list, segmentNumber) >= 0);
        }
    }
}
