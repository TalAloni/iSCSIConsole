/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager
{
    /// <summary>
    /// Column is a sequence of one or more (dynamic) disk extents, it's an abstraction of disk,
    /// Simple Volume uses a single column that has one extent,
    /// Spanned volume uses a single column to span data across multiple extents,
    /// Striped / RAID-5 write the data in stripes across multiple columns in an orderly fashion. (each column can contain more than one extent)
    /// </summary>
    public class DynamicColumn
    {
        public const int DefaultBytesPerSector = 512; // for missing disks

        private int m_bytesPerSector;
        List<DynamicDiskExtent> m_extents = new List<DynamicDiskExtent>();

        public DynamicColumn(DynamicDiskExtent extent)
        {
            m_extents.Add(extent);
            m_bytesPerSector = GetBytesPerSector(m_extents, DefaultBytesPerSector);
        }

        public DynamicColumn(List<DynamicDiskExtent> extents)
        {
            m_extents = extents;
            m_bytesPerSector = GetBytesPerSector(m_extents, DefaultBytesPerSector);
        }

        private List<ArrayPosition> TranslateSectors(long startSectorIndex, int sectorCount)
        {
            List<ArrayPosition> result = new List<ArrayPosition>();

            int numberOfDisks = m_extents.Count;

            int sectorsLeft = sectorCount;
            long currentSectorIndex = startSectorIndex;
            while (sectorsLeft > 0)
            {
                long extentStartSectorInColumn = 0;
                long nextExtentStartSectorInColumn = 0;
                for (int index = 0; index < m_extents.Count; index++)
                {
                    DynamicDiskExtent extent = m_extents[index];
                    extentStartSectorInColumn = nextExtentStartSectorInColumn;
                    nextExtentStartSectorInColumn += extent.TotalSectors;
                    if (currentSectorIndex >= extentStartSectorInColumn && currentSectorIndex < nextExtentStartSectorInColumn)
                    {
                        long sectorIndexInExtent = currentSectorIndex - extentStartSectorInColumn;
                        int sectorCountInExtent = (int)Math.Min(extent.TotalSectors - sectorIndexInExtent, sectorsLeft);
                        ArrayPosition position = new ArrayPosition(index, sectorIndexInExtent, sectorCountInExtent);
                        result.Add(position);
                        currentSectorIndex += sectorCountInExtent;
                        sectorsLeft -= sectorCountInExtent;
                    }
                }
            }

            return result;
        }

        public byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            List<ArrayPosition> readPositions = TranslateSectors(sectorIndex, sectorCount);

            byte[] result = new byte[sectorCount * BytesPerSector];
            int bytesRead = 0;
            foreach (ArrayPosition readPosition in readPositions)
            {
                DynamicDiskExtent extent = m_extents[readPosition.DiskIndex];
                byte[] extentBytes = extent.ReadSectors(readPosition.SectorIndex, readPosition.SectorCount);

                Array.Copy(extentBytes, 0, result, bytesRead, extentBytes.Length);
                bytesRead += extentBytes.Length;
            }

            return result;
        }

        public void WriteSectors(long sectorIndex, byte[] data)
        {
            int sectorCount = data.Length / BytesPerSector;
            List<ArrayPosition> writePositions = TranslateSectors(sectorIndex, sectorCount);

            int bytesWritten = 0;
            foreach (ArrayPosition writePosition in writePositions)
            {
                DynamicDiskExtent extent = m_extents[writePosition.DiskIndex];
                byte[] extentBytes = new byte[writePosition.SectorCount * BytesPerSector];
                Array.Copy(data, bytesWritten, extentBytes, 0, extentBytes.Length);
                extent.WriteSectors(writePosition.SectorIndex, extentBytes);
                
                bytesWritten += extentBytes.Length;
            }
        }

        public List<DynamicDiskExtent> Extents
        {
            get
            {
                return m_extents;
            }
        }

        public long Size
        {
            get
            {
                long result = 0;
                foreach (DynamicDiskExtent extent in m_extents)
                {
                    result += extent.Size;
                }
                return result;
            }
        }

        /// <summary>
        /// "All disks holding extents for a given volume must have the same sector size"
        /// </summary>
        public int BytesPerSector
        {
            get
            {
                return m_bytesPerSector;
            }
        }

        public bool IsOperational
        {
            get
            {
                foreach (DynamicDiskExtent extent in m_extents)
                {
                    if (extent.Disk == null)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public static int GetBytesPerSector(List<DynamicDiskExtent> extents, int defaultValue)
        {
            int? bytesPerSector = GetBytesPerSector(extents);
            return bytesPerSector.HasValue ? bytesPerSector.Value : defaultValue;
        }

        /// <summary>
        /// "All disks holding extents for a given volume must have the same sector size"
        /// </summary>
        public static int? GetBytesPerSector(List<DynamicDiskExtent> extents)
        {
            foreach (DynamicDiskExtent extent in extents)
            {
                if (extent.Disk != null)
                {
                    return extent.BytesPerSector;
                }
            }
            return null;
        }
    }
}
