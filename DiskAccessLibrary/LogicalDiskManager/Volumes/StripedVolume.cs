/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class StripedVolume : DynamicVolume
    {
        private List<DynamicColumn> m_columns = new List<DynamicColumn>(); // must be sorted
        private int m_sectorsPerStripe;
        private long m_size;

        public StripedVolume(List<DynamicColumn> columns, int sectorsPerStripe, Guid volumeGuid, Guid diskGroupGuid) : base(volumeGuid, diskGroupGuid)
        {
            m_columns = columns;
            m_sectorsPerStripe = sectorsPerStripe;
            m_size = m_columns[0].Size * m_columns.Count;
        }

        public List<ArrayPosition> TranslateSectors(long startSectorIndex, int sectorCount)
        {
            List<ArrayPosition> result = new List<ArrayPosition>();

            int numberOfColumns = m_columns.Count;

            int sectorsLeft = sectorCount;
            long currentSectorIndex = startSectorIndex;
            while (sectorsLeft > 0)
            {
                long stripeIndexInVolume = currentSectorIndex / m_sectorsPerStripe;
                long stripeIndexInColumn = stripeIndexInVolume / numberOfColumns;

                int diskIndex = (int)(stripeIndexInVolume % numberOfColumns);

                long columnSectorIndex = stripeIndexInColumn * m_sectorsPerStripe + (currentSectorIndex % m_sectorsPerStripe);

                int sectorsToReadFromCurrentColumnStripe = Math.Min(m_sectorsPerStripe - (int)(columnSectorIndex % m_sectorsPerStripe), sectorsLeft);

                // e.g. :
                // Column 0: 0 3 6 ...
                // Column 1: 1 4 7 ...
                // Column 2: 2 5 8 ...

                ArrayPosition position = new ArrayPosition(diskIndex, columnSectorIndex, sectorsToReadFromCurrentColumnStripe);
                result.Add(position);
                currentSectorIndex += sectorsToReadFromCurrentColumnStripe;
                sectorsLeft -= sectorsToReadFromCurrentColumnStripe;
            }

            return result;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            List<ArrayPosition> readPositions = TranslateSectors(sectorIndex, sectorCount);

            byte[] result = new byte[sectorCount * BytesPerSector];
            int bytesRead = 0;
            foreach (ArrayPosition readPosition in readPositions)
            {
                DynamicColumn column = m_columns[readPosition.DiskIndex];
                byte[] stripeBytes = column.ReadSectors(readPosition.SectorIndex, (int)readPosition.SectorCount);
                Array.Copy(stripeBytes, 0, result, bytesRead, stripeBytes.Length);
                bytesRead += stripeBytes.Length;
            }

            return result;
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            int sectorCount = data.Length / this.BytesPerSector;
            List<ArrayPosition> writePositions = TranslateSectors(sectorIndex, sectorCount);

            int bytesWritten = 0;
            foreach (ArrayPosition writePosition in writePositions)
            {
                byte[] stripeBytes = new byte[writePosition.SectorCount * this.BytesPerSector];
                Array.Copy(data, bytesWritten, stripeBytes, 0, stripeBytes.Length);
                DynamicColumn column = m_columns[writePosition.DiskIndex];
                column.WriteSectors(writePosition.SectorIndex, stripeBytes);
                bytesWritten += stripeBytes.Length;
            }
        }

        public override long Size
        {
            get 
            {
                return m_size;
            }
        }

        public override List<DynamicColumn> Columns
        {
            get 
            {
                return m_columns;
            }
        }

        public int SectorsPerStripe
        {
            get
            {
                return m_sectorsPerStripe;
            }
        }

        public int NumberOfColumns
        {
            get
            {
                return m_columns.Count;
            }
        }
    }
}
