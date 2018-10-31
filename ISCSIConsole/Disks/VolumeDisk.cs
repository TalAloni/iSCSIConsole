using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSIConsole
{
    public class VolumeDisk : Disk // a fake disk that serves a single volume
    {
        private Volume m_volume;
        private bool m_isReadOnly;
        
        public VolumeDisk(Volume volume, bool isReadOnly)
        {
            m_volume = volume;
            m_isReadOnly = volume.IsReadOnly || isReadOnly;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            return m_volume.ReadSectors(sectorIndex, sectorCount);
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (!IsReadOnly)
            {
                m_volume.WriteSectors(sectorIndex, data);
            }
        }

        public override int BytesPerSector
        {
            get
            {
                return m_volume.BytesPerSector;
            }
        }

        public override long Size
        {
            get
            {
                return m_volume.Size;
            }
        }

        public override bool IsReadOnly
        {
            get
            {
                return m_isReadOnly;
            }
        }

        public Volume Volume
        {
            get
            {
                return m_volume;
            }
        }
    }
}
