/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public abstract class DynamicVolume : Volume
    {
        private Guid m_volumeGuid;
        private Guid m_diskGroupGuid;
        public ulong VolumeID;
        public string Name;
        public string DiskGroupName;
        private int? m_bytesPerSector;

        public DynamicVolume(Guid volumeGuid, Guid diskGroupGuid)
        {
            m_volumeGuid = volumeGuid;
            m_diskGroupGuid = diskGroupGuid;
        }

        public override bool Equals(object obj)
        {
            if (obj is DynamicVolume)
            {
                return ((DynamicVolume)obj).VolumeGuid == this.VolumeGuid;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.VolumeGuid.GetHashCode();
        }

        /// <summary>
        /// "All disks holding extents for a given volume must have the same sector size"
        /// </summary>
        public override int BytesPerSector
        {
            get
            {
                if (!m_bytesPerSector.HasValue)
                {
                    m_bytesPerSector = GetBytesPerSector(this.Columns, DynamicColumn.DefaultBytesPerSector);
                }
                return m_bytesPerSector.Value;
            }
        }

        public Guid VolumeGuid
        {
            get 
            {
                return m_volumeGuid;
            }
        }

        public Guid DiskGroupGuid
        {
            get
            {
                return m_diskGroupGuid;
            }
        }

        public override List<DiskExtent> Extents
        {
            get
            {
                List<DiskExtent> result = new List<DiskExtent>();
                foreach (DynamicDiskExtent extent in this.DynamicExtents)
                {
                    result.Add(extent);
                }
                return result;
            }
        }

        public abstract List<DynamicColumn> Columns
        {
            get;
        }

        public List<DynamicDiskExtent> DynamicExtents
        {
            get
            {
                List<DynamicDiskExtent> result = new List<DynamicDiskExtent>();
                foreach (DynamicColumn column in Columns)
                {
                    result.AddRange(column.Extents);
                }
                return result;
            }
        }

        public virtual bool IsHealthy
        {
            get
            {
                foreach (DynamicColumn column in this.Columns)
                {
                    if (!column.IsOperational)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public virtual bool IsOperational
        {
            get
            {
                return IsHealthy;
            }
        }

        public static int GetBytesPerSector(List<DynamicColumn> columns, int defaultValue)
        {
            int? bytesPerSector = GetBytesPerSector(columns);
            return bytesPerSector.HasValue ? bytesPerSector.Value : defaultValue;
        }

        /// <summary>
        /// "All disks holding extents for a given volume must have the same sector size"
        /// </summary>
        public static int? GetBytesPerSector(List<DynamicColumn> columns)
        {
            foreach (DynamicColumn column in columns)
            {
                int? bytesPerSector = DynamicColumn.GetBytesPerSector(column.Extents);
                if (bytesPerSector.HasValue)
                {
                    return bytesPerSector.Value;
                }
            }
            return null;
        }
    }
}
