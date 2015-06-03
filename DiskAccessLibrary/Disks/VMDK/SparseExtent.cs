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
using Utilities;

namespace DiskAccessLibrary.VMDK
{
    public class SparseExtent : Disk
    {
        private string m_path;
        private SparseExtentHeader m_header;
        private VirtualMachineDiskDescriptor m_descriptor;
        private Nullable<uint> m_grainTableStartSector;

        public SparseExtent(string path)
        {
            m_path = path;

            byte[] headerBytes = ReadFileSectors(0, 1);
            m_header = new SparseExtentHeader(headerBytes);

            if (m_header.IsValidAndSupported)
            {
                if (m_header.DescriptorOffset > 0)
                {
                    byte[] descriptorBytes = ReadFileSectors((long)m_header.DescriptorOffset, (int)m_header.DescriptorSize);
                    string text = ASCIIEncoding.ASCII.GetString(descriptorBytes);
                    List<string> lines = VirtualMachineDiskDescriptor.GetLines(text);
                    m_descriptor = new VirtualMachineDiskDescriptor(lines);
                }
            }
        }

        public byte[] ReadFileSectors(long sectorIndex, int sectorCount)
        {
            // We should use noncached I/O operations in case KB981166 is not installed on the host.
            FileStream stream = new FileStream(m_path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.WriteThrough);
            long offset = sectorIndex * BytesPerSector;
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] result = new byte[BytesPerSector * sectorCount];
            stream.Read(result, 0, BytesPerSector * sectorCount);
            stream.Close();
            return result;
        }

        private KeyValuePairList<long, int> MapSectors(long sectorIndex, int sectorCount)
        {
            if (m_grainTableStartSector == null)
            {
                byte[] grainDirectoryBytes = ReadFileSectors((long)m_header.GDOffset, 1);
                m_grainTableStartSector = LittleEndianConverter.ToUInt32(grainDirectoryBytes, 0);
            }

            long grainIndex = sectorIndex / (long)m_header.GrainSize;
            long grainSectorIndexInTable = grainIndex / 128;
            int grainIndexInBuffer = (int)grainIndex % 128;
            int sectorsToReadFromTable = (int)Math.Max(Math.Ceiling((double)(sectorCount - (128 - grainIndexInBuffer)) / 4), 1);
            byte[] grainTableBuffer = ReadFileSectors(m_grainTableStartSector.Value + grainSectorIndexInTable, sectorsToReadFromTable);

            long sectorIndexInGrain = sectorIndex % (long)m_header.GrainSize;

            KeyValuePairList<long, int> result = new KeyValuePairList<long, int>();
            uint grainOffset = LittleEndianConverter.ToUInt32(grainTableBuffer, grainIndexInBuffer * 4);
            grainOffset += (uint)sectorIndexInGrain;
            int sectorsLeft = sectorCount;
            int sectorsProcessedInGrain = (int)Math.Min(sectorsLeft, (long)m_header.GrainSize - sectorIndexInGrain);
            result.Add(grainOffset, sectorsProcessedInGrain);
            sectorsLeft -= sectorsProcessedInGrain;

            while (sectorsLeft > 0)
            {
                grainIndexInBuffer++;
                grainOffset = LittleEndianConverter.ToUInt32(grainTableBuffer, grainIndexInBuffer * 4);
                sectorsProcessedInGrain = (int)Math.Min(sectorsLeft, (long)m_header.GrainSize);
                long lastSectorIndex = result[result.Count - 1].Key;
                int lastSectorCount = result[result.Count - 1].Value;
                if (lastSectorIndex + lastSectorCount == grainOffset)
                {
                    result[result.Count - 1] = new KeyValuePair<long, int>(lastSectorIndex, lastSectorCount + sectorsProcessedInGrain);
                }
                else
                {
                    result.Add(grainOffset, sectorsProcessedInGrain);
                }
                sectorsLeft -= sectorsProcessedInGrain;
            }

            return result;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            byte[] result = new byte[sectorCount * this.BytesPerSector];
            int offset = 0;
            KeyValuePairList<long, int> map = MapSectors(sectorIndex, sectorCount);
            foreach (KeyValuePair<long, int> entry in map)
            {
                byte[] temp;
                if (entry.Key == 0) // 0 means that the grain is not yet allocated
                {
                    temp = new byte[entry.Value * this.BytesPerSector];
                }
                else
                {
                    temp = ReadFileSectors(entry.Key, entry.Value);
                }
                Array.Copy(temp, 0, result, offset, temp.Length);
                offset += temp.Length;
            }

            return result;
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void CheckBoundaries(long sectorIndex, int sectorCount)
        {
            if (sectorIndex < 0 || sectorIndex + (sectorCount - 1) >= this.TotalSectors)
            {
                throw new ArgumentOutOfRangeException("Attempted to access data outside of disk");
            }
        }

        public override int BytesPerSector
        {
            get
            {
                return VirtualMachineDisk.BytesPerDiskImageSector;
            }
        }

        public override long Size
        {
            get
            {
                return (long)(m_header.Capacity * (ulong)this.BytesPerSector);
            }
        }

        public VirtualMachineDiskDescriptor Descriptor
        {
            get
            {
                return m_descriptor;
            }
        }
    }
}
