using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary
{
    public class SPTIDevice : Disk // a fake disk that serves a single device
    {
        private string m_path;
        private string m_description;

        public SPTIDevice(string diskImagePath)
        {
            m_path = diskImagePath;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            return null;
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
        }

        public override int BytesPerSector
        {
            get 
            {
                return 0;
            }
        }

        public override long Size
        {
            get 
            {
                return 0;
            }
        }

        public string Path
        {
            get
            {
                return m_path;
            }
        }

        public string Description
        {
            get
            {
                return m_description;
            }
            set
            {
                m_description = Description;
            }
        }
    }
}
