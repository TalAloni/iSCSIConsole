/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        // dynamic disks will only work with 512-byte sectors
        // http://msdn.microsoft.com/en-us/windows/hardware/gg463528
        // Note: Simple volumes will somewhat work on > 512-byte sectors
        public const int BytesPerDynamicDiskSector = 512;

        private Guid m_volumeGuid;
        private Guid m_diskGroupGuid;
        public ulong VolumeID;
        public string Name;
        public string DiskGroupName;

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

        public override int BytesPerSector
        {
            get
            {
                return BytesPerDynamicDiskSector;
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
    }
}
