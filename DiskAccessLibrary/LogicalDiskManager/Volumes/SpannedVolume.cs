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
    public class SpannedVolume : DynamicVolume
    {
        private DynamicColumn m_column;

        public SpannedVolume(DynamicColumn column, Guid volumeGuid, Guid diskGroupGuid) : base(volumeGuid, diskGroupGuid)
        {
            m_column = column;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            return m_column.ReadSectors(sectorIndex, sectorCount);
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            CheckBoundaries(sectorIndex, data.Length / this.BytesPerSector);
            m_column.WriteSectors(sectorIndex, data);
        }

        public override List<DynamicColumn> Columns
        {
            get 
            {
                List<DynamicColumn> result = new List<DynamicColumn>();
                result.Add(m_column);
                return result;
            }
        }

        public override long Size
        {
            get 
            {
                return m_column.Size;
            }
        }
    }
}
