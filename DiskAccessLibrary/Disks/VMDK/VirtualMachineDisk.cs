/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;
using DiskAccessLibrary.VMDK;

namespace DiskAccessLibrary
{
    public partial class VirtualMachineDisk : DiskImage, IDiskGeometry
    {
        // VMDK sector size is set to 512 bytes.
        public const int BytesPerDiskSector = 512;

        private const uint BaseDiskParentCID = 0xffffffff;

        private string m_descriptorPath;
        private VirtualMachineDiskDescriptor m_descriptor;

        private DiskImage m_extent;

        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public VirtualMachineDisk(string descriptorPath) : this(descriptorPath, false)
        {
        }

        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public VirtualMachineDisk(string descriptorPath, bool isReadOnly) : base(descriptorPath)
        {
            m_descriptorPath = descriptorPath;

            m_descriptor = VirtualMachineDiskDescriptor.ReadFromFile(m_descriptorPath);
            bool isDescriptorEmbedded = false;
            if (m_descriptor == null)
            {
                SparseExtent sparse;
                try
                {
                    sparse = new SparseExtent(m_descriptorPath);
                }
                catch (InvalidDataException)
                {
                    throw new InvalidDataException("Missing VMDK descriptor");
                }

                if (sparse.Descriptor != null)
                {
                    isDescriptorEmbedded = true;
                    m_descriptor = sparse.Descriptor;
                    m_extent = sparse;
                }
                else
                {
                    throw new InvalidDataException("Missing VMDK descriptor");
                }
            }

            if (m_descriptor.Version != 1)
            {
                throw new NotImplementedException("Unsupported VMDK descriptor version");
            }

            if (m_descriptor.ParentContentID != BaseDiskParentCID)
            {
                throw new InvalidDataException("VMDK descriptor ParentContentID does not match BaseDiskParentCID");
            }

            if (!isDescriptorEmbedded && m_descriptor.DiskType != VirtualMachineDiskType.MonolithicFlat)
            {
                throw new NotImplementedException("Unsupported VMDK disk type");
            }

            if (isDescriptorEmbedded && m_descriptor.DiskType != VirtualMachineDiskType.MonolithicSparse)
            {
                throw new NotImplementedException("Unsupported VMDK disk type");
            }

            foreach (VirtualMachineDiskExtentEntry extentEntry in m_descriptor.ExtentEntries)
            {
                if (!isDescriptorEmbedded && extentEntry.ExtentType != ExtentType.Flat)
                {
                    throw new NotImplementedException("Unsupported VMDK extent type");
                }

                if (isDescriptorEmbedded && extentEntry.ExtentType != ExtentType.Sparse)
                {
                    throw new NotImplementedException("Unsupported VMDK extent type");
                }
            }

            if (m_descriptor.ExtentEntries.Count != 1)
            {
                throw new NotImplementedException("Unsupported number of VMDK extents");
            }

            if (m_descriptor.DiskType == VirtualMachineDiskType.MonolithicFlat)
            {
                VirtualMachineDiskExtentEntry entry = m_descriptor.ExtentEntries[0];
                string directory = System.IO.Path.GetDirectoryName(descriptorPath);
                string extentPath = directory + @"\" + entry.FileName;
                DiskImage extent = new RawDiskImage(extentPath, BytesPerDiskSector, isReadOnly);
                m_extent = extent;
            }
        }

        public override bool ExclusiveLock()
        {
            return m_extent.ExclusiveLock();
        }

#if Win32
        public override bool ExclusiveLock(bool useOverlappedIO)
        {
            return m_extent.ExclusiveLock(useOverlappedIO);
        }
#endif

        public override bool ReleaseLock()
        {
            return m_extent.ReleaseLock();
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            return m_extent.ReadSectors(sectorIndex, sectorCount);
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a readonly disk");
            }
            m_extent.WriteSectors(sectorIndex, data);
        }

        public override void Extend(long numberOfAdditionalBytes)
        {
            if (m_descriptor.DiskType == VirtualMachineDiskType.MonolithicFlat)
            {
                // Add updated extent entries
                List<string> lines = VirtualMachineDiskDescriptor.ReadASCIITextLines(m_descriptorPath);
                m_descriptor.ExtentEntries[0].SizeInSectors += numberOfAdditionalBytes / this.BytesPerSector;
                m_descriptor.UpdateExtentEntries(lines);

                File.WriteAllLines(m_descriptorPath, lines.ToArray(), Encoding.ASCII);
                m_extent.Extend(numberOfAdditionalBytes);
            }
            else
            {
                throw new NotImplementedException("Extending a monolithic sparse is not supported");
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

        public long Cylinders
        {
            get
            {
                return m_descriptor.Cylinders;
            }
        }

        public int TracksPerCylinder
        {
            get
            {
                return m_descriptor.TracksPerCylinder;
            }
        }

        public int SectorsPerTrack
        {
            get
            {
                return m_descriptor.SectorsPerTrack;
            }
        }
    }
}
