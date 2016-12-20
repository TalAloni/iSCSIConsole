/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.VHD
{
    /// <summary>
    /// a.k.a. BAT
    /// </summary>
    public class BlockAllocationTable
    {
        public const uint UnusedEntry = 0xFFFFFFFF;
        public uint[] Entries;

        public BlockAllocationTable(uint maxTableEntries)
        {
            Entries = new uint[maxTableEntries];
            for (int index = 0; index < maxTableEntries; index++)
            {
                Entries[index] = UnusedEntry;
            }
        }

        public BlockAllocationTable(byte[] buffer, uint maxTableEntries)
        { 
            Entries = new uint[maxTableEntries];
            for (int index = 0; index < maxTableEntries; index++)
            {
                Entries[index] = BigEndianConverter.ToUInt32(buffer, index * 4);
            }
        }

        public byte[] GetBytes()
        {
            // The BAT is always extended to a sector boundary
            int bufferLength = (int)Math.Ceiling((double)Entries.Length * 4 / VirtualHardDisk.BytesPerDiskSector) * VirtualHardDisk.BytesPerDiskSector;
            byte[] buffer = new byte[bufferLength];
            for (int index = 0; index < Entries.Length; index++)
            {
                BigEndianWriter.WriteUInt32(buffer, index * 4, Entries[index]);
            }

            return buffer;
        }

        public bool IsBlockInUse(uint blockIndex)
        {
            return Entries[blockIndex] != UnusedEntry;
        }

        public static BlockAllocationTable ReadBlockAllocationTable(string path, DynamicDiskHeader dynamicHeader)
        {
            uint maxTableEntries = dynamicHeader.MaxTableEntries;
            long sectorIndex = (long)(dynamicHeader.TableOffset / VirtualHardDisk.BytesPerDiskSector);
            int sectorCount = (int)Math.Ceiling((double)maxTableEntries * 4 / VirtualHardDisk.BytesPerDiskSector);
            byte[] buffer = new RawDiskImage(path, VirtualHardDisk.BytesPerDiskSector).ReadSectors(sectorIndex, sectorCount);
            return new BlockAllocationTable(buffer, maxTableEntries);
        }
    }
}
