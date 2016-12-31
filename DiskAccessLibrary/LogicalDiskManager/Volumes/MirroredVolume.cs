/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class MirroredVolume : DynamicVolume
    {
        private List<DynamicVolume> m_volumes;

        public MirroredVolume(List<DynamicVolume> volumes, Guid volumeGuid, Guid diskGroupGuid) : base(volumeGuid, diskGroupGuid)
        {
            m_volumes = volumes;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            foreach (DynamicVolume volume in m_volumes)
            {
                if (volume.IsOperational)
                {
                    return volume.ReadSectors(sectorIndex, sectorCount);
                }
            }

            throw new InvalidOperationException("Cannot read from a failed volume");
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            foreach (DynamicVolume volume in m_volumes)
            {
                volume.WriteSectors(sectorIndex, data);
            }
        }

        public override List<DynamicColumn> Columns
        {
            get 
            {
                return m_volumes[0].Columns;
            }
        }

        public override int BytesPerSector
        {
            get
            {
                foreach (DynamicVolume volume in m_volumes)
                {
                    if (volume.IsOperational)
                    {
                        return volume.BytesPerSector;
                    }
                }
                return DynamicColumn.DefaultBytesPerSector;
            }
        }

        public override long Size
        {
            get 
            {
                return m_volumes[0].Size;
            }
        }
        
        public override bool IsHealthy
        {
            get
            {
                foreach (DynamicVolume volume in m_volumes)
                {
                    if (!volume.IsHealthy)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// A mirrroed volume can operate as long as a single component is operational
        /// </summary>
        public override bool IsOperational
        {
            get
            {
                foreach (DynamicVolume volume in m_volumes)
                {
                    if (volume.IsOperational)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public List<DynamicVolume> Components
        {
            get
            {
                return m_volumes;
            }
        }
    }
}
