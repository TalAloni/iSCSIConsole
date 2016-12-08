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

namespace DiskAccessLibrary
{
    public class PartitionTableEntry
    {
        public const int Length = 16;

        public byte Status;
        public CHSAddress FirstSectorCHS;
        public byte PartitionType;
        public CHSAddress LastSectorCHS;
        public uint FirstSectorLBA;
        public uint SectorCountLBA;

        public PartitionTableEntry()
        {
            FirstSectorCHS = new CHSAddress();
            LastSectorCHS = new CHSAddress();
        }

        public PartitionTableEntry(byte[] buffer, int offset)
        {
            Status = buffer[offset + 0x00];
            FirstSectorCHS = new CHSAddress(buffer, offset + 0x01);
            PartitionType = buffer[offset + 0x04];
            LastSectorCHS = new CHSAddress(buffer, offset + 0x05);
            FirstSectorLBA = LittleEndianConverter.ToUInt32(buffer, offset + 0x08);
            SectorCountLBA = LittleEndianConverter.ToUInt32(buffer, offset + 0x0C);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            buffer[offset + 0x00] = Status;
            FirstSectorCHS.WriteBytes(buffer, offset + 0x01);
            buffer[offset + 0x04] = PartitionType;
            LastSectorCHS.WriteBytes(buffer, offset + 0x05);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x08, FirstSectorLBA);
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x0C, SectorCountLBA);
        }

        public PartitionTypeName PartitionTypeName
        {
            get
            {
                return (PartitionTypeName)PartitionType;
            }
            set
            {
                PartitionType = (byte)value;
            }
        }

        public bool IsBootable
        {
            get
            {
                return (Status == 0x80);
            }
            set
            {
                Status |= 0x80;
            }
        }

        public bool IsValid
        {
            get
            {
                return (Status == 0x80 || Status == 0x00);
            }
        }

        public uint LastSectorLBA
        {
            get
            {
                return this.FirstSectorLBA + this.SectorCountLBA - 1;
            }
        }
    }
}
