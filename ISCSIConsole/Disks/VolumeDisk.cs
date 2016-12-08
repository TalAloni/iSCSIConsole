using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSIConsole
{
    public class VolumeDisk : Disk // a fake disk that serves a single volume
    {
        private Volume m_volume;
        
        public VolumeDisk(Volume volume)
        {
            m_volume = volume;
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
    }
}
