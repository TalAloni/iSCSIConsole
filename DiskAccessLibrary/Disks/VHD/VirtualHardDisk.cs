/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Utilities;
using DiskAccessLibrary.VHD;

namespace DiskAccessLibrary
{
    public partial class VirtualHardDisk : DiskImage, IDiskGeometry
    {
        // VHD sector size is set to 512 bytes.
        public const int BytesPerDiskSector = 512;

        private RawDiskImage m_file;
        private VHDFooter m_vhdFooter;
        // Dynamic VHD:
        private DynamicDiskHeader m_dynamicHeader;
        private BlockAllocationTable m_blockAllocationTable;

        // CHS:
        private long m_cylinders;
        private int m_tracksPerCylinder; // a.k.a. heads
        private int m_sectorsPerTrack;

        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public VirtualHardDisk(string virtualHardDiskPath) : base(virtualHardDiskPath)
        {
            // We can't read the VHD footer using this.ReadSector() because it's out of the disk boundaries
            m_file = new RawDiskImage(virtualHardDiskPath, BytesPerDiskSector);
            byte[] buffer = m_file.ReadSector(m_file.Size / m_file.BytesPerSector - 1);
            m_vhdFooter = new VHDFooter(buffer);

            if (!m_vhdFooter.IsValid)
            {
                // check to see if a header is present (dynamic VHD) and use it instead
                buffer = m_file.ReadSector(0);
                m_vhdFooter = new VHDFooter(buffer);
                if (!m_vhdFooter.IsValid)
                {
                    throw new InvalidDataException("Invalid VHD footer");
                }
            }

            if (m_vhdFooter.DiskType == VirtualHardDiskType.Fixed)
            {
            }
            else if (m_vhdFooter.DiskType == VirtualHardDiskType.Dynamic)
            {
                buffer = m_file.ReadSectors(1, 2);
                m_dynamicHeader = new DynamicDiskHeader(buffer);
                m_blockAllocationTable = BlockAllocationTable.ReadBlockAllocationTable(virtualHardDiskPath, m_dynamicHeader);

                this.IsReadOnly = true;
            }
            else
            {
                throw new NotImplementedException("Differencing VHD is not supported");
            }

            SetGeometry();
        }

        private void SetGeometry()
        {
            byte heads;
            byte sectorsPerTrack;
            ushort cylinders;
            GetDiskGeometry((ulong)this.TotalSectors, out heads, out sectorsPerTrack, out cylinders);
            m_cylinders = cylinders;
            m_tracksPerCylinder = heads;
            m_sectorsPerTrack = sectorsPerTrack;
        }

        public override bool ExclusiveLock()
        {
            return m_file.ExclusiveLock();
        }

        public override bool ReleaseLock()
        {
            return m_file.ReleaseLock();
        }

        /// <summary>
        /// Sector refers to physical disk sector, we can only read complete sectors
        /// </summary>
        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            if (m_vhdFooter.DiskType == VirtualHardDiskType.Fixed)
            {
                return m_file.ReadSectors(sectorIndex, sectorCount);
            }
            else // dynamic
            {
                byte[] buffer = new byte[sectorCount * this.BytesPerSector];
                int sectorOffset = 0;
                while (sectorOffset < sectorCount)
                {
                    uint blockIndex = (uint)((sectorIndex + sectorOffset) * this.BytesPerSector / m_dynamicHeader.BlockSize);
                    int sectorOffsetInBlock = (int)(((sectorIndex + sectorOffset) * this.BytesPerSector % m_dynamicHeader.BlockSize) / this.BytesPerSector);
                    int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / this.BytesPerSector);
                    int sectorsRemainingInBlock = sectorsInBlock - sectorOffsetInBlock;
                    int sectorsToRead = Math.Min(sectorCount - sectorOffset, sectorsRemainingInBlock);

                    if (m_blockAllocationTable.IsBlockInUse(blockIndex))
                    {
                        uint blockStartSector = m_blockAllocationTable.Entries[blockIndex];
                        // each data block has a sector bitmap preceding the data, the bitmap is padded to a 512-byte sector boundary.
                        int blockBitmapSectorCount = (int)Math.Ceiling((double)sectorsInBlock / (this.BytesPerSector * 8));
                        // "All sectors within a block whose corresponding bits in the bitmap are zero must contain 512 bytes of zero on disk"
                        byte[] temp = m_file.ReadSectors(blockStartSector + blockBitmapSectorCount + sectorOffsetInBlock, sectorsToRead);
                        ByteWriter.WriteBytes(buffer, sectorOffset * this.BytesPerSector, temp);
                    }
                    sectorOffset += sectorsToRead;
                }
                return buffer;
            }
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            CheckBoundaries(sectorIndex, data.Length / this.BytesPerSector);
            m_file.WriteSectors(sectorIndex, data);
        }

        public override void Extend(long numberOfAdditionalBytes)
        {
            if (numberOfAdditionalBytes % this.BytesPerSector > 0)
            {
                throw new ArgumentException("numberOfAdditionalBytes must be a multiple of BytesPerSector");
            }

            if (m_vhdFooter.DiskType == VirtualHardDiskType.Fixed)
            {
                long length = this.Size; // does not include the footer
                m_file.Extend(numberOfAdditionalBytes);
                m_vhdFooter.CurrentSize += (ulong)numberOfAdditionalBytes;
                byte[] footerBytes = m_vhdFooter.GetBytes();
                m_file.WriteSectors((length + numberOfAdditionalBytes) / this.BytesPerSector, footerBytes);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public long Cylinders
        {
            get
            {
                return m_cylinders;
            }
        }

        public int TracksPerCylinder
        {
            get
            {
                return m_tracksPerCylinder;
            }
        }

        public int SectorsPerTrack
        {
            get
            {
                return m_sectorsPerTrack;
            }
        }

        public override int BytesPerSector
        {
            get
            {
                return BytesPerDiskSector;
            }
        }

        public override long Size
        {
            get
            {
                return (long)m_vhdFooter.CurrentSize;
            }
        }

        public VHDFooter Footer
        {
            get
            {
                return m_vhdFooter;
            }
        }

        // Taken From VHD format specs (Appendix)
        public static void GetDiskGeometry(ulong totalSectors, out byte heads, out byte sectorsPerTrack, out ushort cylinders)
        {
            int cylindersTimesHeads;

            // If more than ~128GB truncate at ~128GB
            if (totalSectors > 65535 * 16 * 255)
            {
                totalSectors = 65535 * 16 * 255;
            }

            // If more than ~32GB, break partition table compatibility.
            // Partition table has max 63 sectors per track.  Otherwise
            // we're looking for a geometry that's valid for both BIOS
            // and ATA.
            if (totalSectors >= 65535 * 16 * 63)
            {
                sectorsPerTrack = 255;
                heads = 16;
                cylindersTimesHeads = (int)(totalSectors / sectorsPerTrack);
            }
            else
            {
                sectorsPerTrack = 17;
                cylindersTimesHeads = (int)(totalSectors / sectorsPerTrack);

                heads = (byte)((cylindersTimesHeads + 1023) / 1024);

                if (heads < 4)
                {
                    heads = 4;
                }
                if (cylindersTimesHeads >= (heads * 1024) || heads > 16)
                {
                    sectorsPerTrack = 31;
                    heads = 16;
                    cylindersTimesHeads = (int)(totalSectors / sectorsPerTrack);
                }
                if (cylindersTimesHeads >= (heads * 1024))
                {
                    sectorsPerTrack = 63;
                    heads = 16;
                    cylindersTimesHeads = (int)(totalSectors / sectorsPerTrack);
                }
            }
            cylinders = (ushort)(cylindersTimesHeads / heads);
        }

        /// <param name="size">In bytes</param>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public static VirtualHardDisk Create(string path, long size)
        {
            VHDFooter footer = new VHDFooter();
            footer.OriginalSize = (ulong)size;
            footer.CurrentSize = (ulong)size;
            footer.SetCurrentTimeStamp();
            footer.SetDiskGeometry((ulong)size / BytesPerDiskSector);

            RawDiskImage diskImage = RawDiskImage.Create(path, size + VHDFooter.Length, BytesPerDiskSector);
            diskImage.WriteSectors(size / BytesPerDiskSector, footer.GetBytes());

            return new VirtualHardDisk(path);
        }
    }
}
