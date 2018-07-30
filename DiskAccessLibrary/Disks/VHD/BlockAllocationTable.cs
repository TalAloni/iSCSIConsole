/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        private uint[] m_entries;

        public BlockAllocationTable(uint maxTableEntries)
        {
            m_entries = new uint[maxTableEntries];
            for (int index = 0; index < maxTableEntries; index++)
            {
                m_entries[index] = UnusedEntry;
            }
        }

        public BlockAllocationTable(byte[] buffer, uint maxTableEntries)
        { 
            m_entries = new uint[maxTableEntries];
            for (int index = 0; index < maxTableEntries; index++)
            {
                m_entries[index] = BigEndianConverter.ToUInt32(buffer, index * 4);
            }
        }

        public byte[] GetBytes()
        {
            // The BAT is always extended to a sector boundary
            int bufferLength = (int)Math.Ceiling((double)m_entries.Length * 4 / VirtualHardDisk.BytesPerDiskSector) * VirtualHardDisk.BytesPerDiskSector;
            byte[] buffer = new byte[bufferLength];
            for (int index = 0; index < m_entries.Length; index++)
            {
                BigEndianWriter.WriteUInt32(buffer, index * 4, m_entries[index]);
            }

            return buffer;
        }

        public bool IsBlockInUse(uint blockIndex)
        {
            return m_entries[blockIndex] != UnusedEntry;
        }

        public bool IsBlockInUse(uint blockIndex, out uint blockStartSector)
        {
            blockStartSector = m_entries[blockIndex];
            return m_entries[blockIndex] != UnusedEntry;
        }

        public void SetBlockStartSector(uint blockIndex, uint blockStartSector)
        {
            if (m_entries[blockIndex] != UnusedEntry)
            {
                throw new InvalidOperationException("Block is already allocated");
            }

            m_entries[blockIndex] = blockStartSector;
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
