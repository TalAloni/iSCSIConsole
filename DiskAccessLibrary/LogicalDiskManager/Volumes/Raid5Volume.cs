/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    /// <summary>
    /// Windows Software RAID-5 array
    /// </summary>
    public class Raid5Volume : DynamicVolume
    {
        private List<DynamicColumn> m_columns = new List<DynamicColumn>(); // must be sorted
        private int m_sectorsPerStripe;
        private long m_size;
        
        /// <param name="diskArray">One of the disks in the array can be null</param>
        public Raid5Volume(List<DynamicColumn> columns, int sectorsPerStripe, Guid volumeGuid, Guid diskGroupGuid) : base(volumeGuid, diskGroupGuid)
        {
            m_columns = columns;
            m_sectorsPerStripe = sectorsPerStripe;
            m_size = m_columns[0].Size * (m_columns.Count - 1);
        }

        // Each ArrayPosition is within a single stripe
        public List<ArrayPosition> TranslateSectors(long startSectorIndex, int sectorCount)
        {
            List<ArrayPosition> result = new List<ArrayPosition>();

            int numberOfColumns = m_columns.Count;

            int sectorsLeft = sectorCount;
            long currentSectorIndex = startSectorIndex;
            while (sectorsLeft > 0)
            {
                long dataStripeIndexInVolume = currentSectorIndex / m_sectorsPerStripe; // stripe index if we don't count parity stripes
                long stripeIndexInColumn = dataStripeIndexInVolume / (numberOfColumns - 1);

                int parityColumnIndex = (numberOfColumns - 1) - (int)(stripeIndexInColumn % numberOfColumns);
                int columnIndex = (int)(dataStripeIndexInVolume % numberOfColumns);

                // Another way to calculate columnIndex:
                //int stripeVerticalIndex = (int)(dataStripeIndexInVolume % (numberOfColumns - 1));
                //int columnIndex2 = (parityColumnIndex + 1 + stripeVerticalIndex) % numberOfColumns;


                long columnSectorIndex = stripeIndexInColumn * m_sectorsPerStripe + (currentSectorIndex % m_sectorsPerStripe);
                
                int sectorsToReadFromCurrentColumnStripe = Math.Min(m_sectorsPerStripe - (int)(columnSectorIndex % m_sectorsPerStripe), sectorsLeft);

                // e.g. :
                // Column 0: 0 3 P ...
                // Column 1: 1 P 4 ...
                // Column 2: P 2 5 ...

                // Column 0: 0 4 8  P ...
                // Column 1: 1 5 P 09 ...
                // Column 2: 2 P 6 10 ...
                // Column 3: P 3 7 11 ...

                ArrayPosition position = new ArrayPosition(columnIndex, columnSectorIndex, sectorsToReadFromCurrentColumnStripe);
                result.Add(position);
                currentSectorIndex += sectorsToReadFromCurrentColumnStripe;
                sectorsLeft -= sectorsToReadFromCurrentColumnStripe;
            }

            return result;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            List<ArrayPosition> readPositions = TranslateSectors(sectorIndex, sectorCount);

            byte[] result = new byte[sectorCount * BytesPerSector];
            int bytesRead = 0;
            foreach (ArrayPosition readPosition in readPositions)
            {
                DynamicColumn column = m_columns[readPosition.DiskIndex];
                byte[] stripeBytes;

                if (column.IsOperational)
                {
                    stripeBytes = column.ReadSectors(readPosition.SectorIndex, readPosition.SectorCount);
                }
                else
                {
                    stripeBytes = new byte[readPosition.SectorCount * BytesPerSector];
                    for (int index = 0; index < m_columns.Count; index++)
                    {
                        if (index != readPosition.DiskIndex)
                        {
                            byte[] currentBytes = m_columns[index].ReadSectors(readPosition.SectorIndex, readPosition.SectorCount);
                            stripeBytes = ByteUtils.XOR(stripeBytes, currentBytes);
                        }
                    }
                }

                Array.Copy(stripeBytes, 0, result, bytesRead, stripeBytes.Length);
                bytesRead += stripeBytes.Length;
            }

            return result;
        }
        
        // We support degraded arrays
        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            CheckBoundaries(sectorIndex, data.Length / this.BytesPerSector);

            int numberOfColumns = m_columns.Count;

            int sectorCount = data.Length / this.BytesPerSector;
            List<ArrayPosition> writePositions = TranslateSectors(sectorIndex, sectorCount);

            int bytesWritten = 0;
            foreach (ArrayPosition writePosition in writePositions)
            {
                DynamicColumn column = m_columns[writePosition.DiskIndex];

                byte[] stripeBytes = new byte[writePosition.SectorCount * this.BytesPerSector];
                Array.Copy(data, bytesWritten, stripeBytes, 0, stripeBytes.Length);
                
                // first we obtain the necessary data from the other columns
                long stripeIndexInColumn = writePosition.SectorIndex / m_sectorsPerStripe;
                int parityColumnIndex = (numberOfColumns - 1) - (int)(stripeIndexInColumn % numberOfColumns);
                List<byte[]> segment = new List<byte[]>();
                for (int index = 0; index < numberOfColumns; index++)
                {
                    if (m_columns[index].IsOperational)
                    {
                        byte[] bytes = m_columns[index].ReadSectors(writePosition.SectorIndex, writePosition.SectorCount);
                        segment.Add(bytes);
                    }
                    else
                    {
                        segment.Add(null);
                    }
                }

                int missingColumnIndex = segment.IndexOf(null);
                if (missingColumnIndex >= 0)
                {
                    if (missingColumnIndex != parityColumnIndex && missingColumnIndex != writePosition.DiskIndex)
                    {
                        // let's calculate the missing data stripe
                        byte[] missingBytes = segment[parityColumnIndex];
                        for (int index = 0; index < numberOfColumns; index++)
                        {
                            if (index != missingColumnIndex && index != parityColumnIndex)
                            {
                                missingBytes = ByteUtils.XOR(missingBytes, segment[index]);
                            }
                        }
                        segment[missingColumnIndex] = missingBytes;
                    }
                }

                if (column.IsOperational)
                {
                    column.WriteSectors(writePosition.SectorIndex, stripeBytes);
                }

                if (missingColumnIndex != parityColumnIndex)
                {
                    // lets calculate the new parity disk
                    segment[writePosition.DiskIndex] = stripeBytes;

                    byte[] parityBytes = new byte[writePosition.SectorCount * this.BytesPerSector];
                    for (int index = 0; index < numberOfColumns; index++)
                    {
                        if (index != parityColumnIndex)
                        {
                            parityBytes = ByteUtils.XOR(parityBytes, segment[index]);
                        }
                    }
                    m_columns[parityColumnIndex].WriteSectors(writePosition.SectorIndex, parityBytes);
                }

                bytesWritten += stripeBytes.Length;
            }
        }

        public byte[] ReadStripes(long stripeIndex, int stripeCount)
        {
            return ReadSectors(stripeIndex * m_sectorsPerStripe, m_sectorsPerStripe * stripeCount);
        }

        public void WriteStripes(long stripeIndex, byte[] data)
        {
            WriteSectors(stripeIndex * m_sectorsPerStripe, data);
        }

        public override long Size
        {
            get 
            {
                return m_size;
            }
        }

        /// <summary>
        /// The number of sectors is always a multiple of SectorsPerStripe
        /// (if we modify the number of sectors manually to any other number, Windows will mark the array as "Failed" ["Too many bad RAID-5 column"])
        /// </summary>
        public long TotalStripes
        {
            get
            {
                return this.TotalSectors / m_sectorsPerStripe;
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

        public int BytesPerStripe
        {
            get
            {
                return m_sectorsPerStripe * this.BytesPerSector;
            }
        }

        public int NumberOfColumns
        {
            get
            {
                return m_columns.Count;
            }
        }

        public long ColumnSize
        {
            get
            {
                return m_columns[0].Size;
            }
        }

        /// <summary>
        /// RAID-5 array can operate with a single missing disk (Failed redundancy)
        /// </summary>
        public override bool IsOperational
        {
            get
            {
                bool isDegraded = false;
                foreach (DynamicColumn column in m_columns)
                {
                    if (!column.IsOperational)
                    {
                        if (isDegraded)
                        {
                            return false;
                        }
                        else
                        {
                            isDegraded = true;
                        }
                    }
                }
                return true;
            }
        }
    }

    public class ArrayPosition
    {
        public ArrayPosition(int diskIndex, long sectorIndex, int sectorCount)
        {
            DiskIndex = diskIndex;
            SectorIndex = sectorIndex;
            SectorCount = sectorCount;
        }

        public int DiskIndex;   // Extent index or column index
        public long SectorIndex;
        public int SectorCount; // We are not going to read > 2^32 sectors at once
    }
}
