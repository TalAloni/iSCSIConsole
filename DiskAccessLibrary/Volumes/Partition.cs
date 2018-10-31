/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary
{
    public abstract class Partition : Volume
    {
        private DiskExtent m_extent;

        public Partition(DiskExtent extent)
        {
            m_extent = extent;
        }

        public Partition(Disk disk, long firstSector, long size)
        {
            m_extent = new DiskExtent(disk, firstSector, size);
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

        public override bool IsReadOnly
        {
            get
            {
                return m_extent.IsReadOnly;
            }
        }

        public DiskExtent Extent
        {
            get
            {
                return m_extent;
            }
        }

        public override List<DiskExtent> Extents
        {
            get
            {
                List<DiskExtent> result = new List<DiskExtent>();
                result.Add(m_extent);
                return result;
            }
        }

        public Disk Disk
        {
            get
            {
                return m_extent.Disk;
            }
        }

        public long FirstSector
        {
            get
            {
                return m_extent.FirstSector;
            }
        }
    }
}
