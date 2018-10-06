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
    /// MFT_SEGMENT_REFERENCE: https://docs.microsoft.com/en-us/windows/desktop/devnotes/mft-segment-reference
    /// </summary>
    public class MftSegmentReference
    {
        public const int Length = 8;

        public long SegmentNumber; // 6 bytes
        public ushort SequenceNumber;

        public MftSegmentReference(long segmentNumber, ushort sequenceNumber)
        {
            SegmentNumber = segmentNumber;
            SequenceNumber = sequenceNumber;
        }
        
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

        public override bool Equals(object obj)
        {
            if (obj is MftSegmentReference)
            {
                MftSegmentReference reference = (MftSegmentReference)obj;
                return (SegmentNumber == reference.SegmentNumber) && (SequenceNumber == reference.SequenceNumber);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return SegmentNumber.GetHashCode();
        }

        public static bool operator ==(MftSegmentReference obj1, MftSegmentReference obj2)
        {
            if (Object.ReferenceEquals(obj1, null))
            {
                return Object.ReferenceEquals(obj2, null);
            }
            else
            {
                return obj1.Equals(obj2);
            }
        }

        public static bool operator !=(MftSegmentReference obj1, MftSegmentReference obj2)
        {
            return !(obj1 == obj2);
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

        public static MftSegmentReference NullReference
        {
            get
            {
                return new MftSegmentReference(0, 0);
            }
        }
    }
}
