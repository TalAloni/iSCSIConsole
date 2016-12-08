/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class SimpleVolume : DynamicVolume
    {
        DynamicDiskExtent m_extent;

        public SimpleVolume(DynamicDiskExtent extent, Guid volumeGuid, Guid diskGroupGuid) : base(volumeGuid, diskGroupGuid)
        {
            m_extent = extent;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            return m_extent.ReadSectors(sectorIndex, sectorCount);
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            m_extent.WriteSectors(sectorIndex, data);
        }

        public override int BytesPerSector
        {
            get 
            {
                return m_extent.BytesPerSector;
            }
        }

        public override long Size
        {
            get 
            {
                return m_extent.Size;
            }
        }
        
        public long FirstSector
        {
            get
            {
                return m_extent.FirstSector;
            }
        }
        
        public Disk Disk
        {
            get
            {
                return m_extent.Disk;
            }
        }

        public DynamicDiskExtent DiskExtent
        {
            get
            {
                return m_extent;
            }
        }

        public override List<DynamicColumn> Columns
        {
            get 
            {
                List<DynamicDiskExtent> extents = new List<DynamicDiskExtent>();
                extents.Add(m_extent);
                List<DynamicColumn> result = new List<DynamicColumn>();
                result.Add(new DynamicColumn(extents));
                return result;
            }
        }
    }
}
