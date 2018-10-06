/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// This class provides a unified interface to access the data of an AttributeRecord.
    /// </summary>
    /// <remarks>
    /// The $Data attribute can be either resident or non-resident.
    /// </remarks>
    public class AttributeData
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeRecord m_attributeRecord;

        public AttributeData(NTFSVolume volume, FileRecord fileRecord, AttributeRecord attributeRecord)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_attributeRecord = attributeRecord;
        }

        public byte[] ReadBytes(ulong offset, int length)
        {
            if (offset >= this.Length)
            {
                return new byte[0];
            }
            long sectorIndex = (long)(offset / (uint)m_volume.BytesPerSector);
            int offsetInSector = (int)(offset % (uint)m_volume.BytesPerSector);
            int sectorCount = (int)Math.Ceiling((double)(offsetInSector + length) / m_volume.BytesPerSector);
            byte[] sectorsBytes = ReadSectors(sectorIndex, sectorCount);
            int readLength = Math.Min(sectorsBytes.Length - offsetInSector, length);
            return ByteReader.ReadBytes(sectorsBytes, offsetInSector, readLength);
        }

        public void WriteBytes(ulong offset, byte[] data)
        {
            ulong currentSize = this.Length;
            ulong nextBytePosition = offset + (uint)data.Length;
            if (nextBytePosition > currentSize)
            {
                // Data needs to be extended
                ulong additionalLength = nextBytePosition - currentSize;
                Extend(additionalLength);
            }

            long firstSectorIndex = (long)(offset / (uint)m_volume.BytesPerSector);
            int offsetInFirstSector = (int)(offset % (uint)m_volume.BytesPerSector);
            // Note that the calculation of lastSectorIndex is only correct when nextBytePosition % BytesPerSector > 0
            long lastSectorIndex = (long)(nextBytePosition / (uint)m_volume.BytesPerSector);
            int offsetInLastSector = (int)(nextBytePosition % (uint)m_volume.BytesPerSector);
            if (offsetInFirstSector > 0 && offsetInLastSector > 0 && firstSectorIndex == lastSectorIndex)
            {
                byte[] sectorBytes = ReadSectors(firstSectorIndex, 1);
                ByteWriter.WriteBytes(sectorBytes, offsetInFirstSector, data);
                data = sectorBytes;
            }
            else
            {
                if (offsetInFirstSector > 0)
                {
                    byte[] firstSectorBytes = ReadSectors(firstSectorIndex, 1);
                    byte[] bytesToKeep = ByteReader.ReadBytes(firstSectorBytes, 0, offsetInFirstSector);
                    data = ByteUtils.Concatenate(bytesToKeep, data);
                }

                if (offsetInLastSector > 0)
                {
                    byte[] lastSectorBytes = ReadSectors(lastSectorIndex, 1);
                    byte[] bytesToKeep = ByteReader.ReadBytes(lastSectorBytes, offsetInLastSector, lastSectorBytes.Length - offsetInLastSector);
                    data = ByteUtils.Concatenate(data, bytesToKeep);
                }
            }

            WriteSectors(firstSectorIndex, data);
        }

        public byte[] ReadCluster(long clusterVCN)
        {
            return ReadClusters(clusterVCN, 1);
        }

        public byte[] ReadClusters(long firstClusterVCN, int count)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, m_fileRecord, (NonResidentAttributeRecord)m_attributeRecord);
                return attributeData.ReadClusters(firstClusterVCN, count);
            }
            else
            {
                int sectorsPerCluster = m_volume.SectorsPerCluster;
                long firstSectorIndex = firstClusterVCN * sectorsPerCluster;
                int sectorCount = count * sectorsPerCluster;
                return ReadSectors(firstSectorIndex, sectorCount);
            }
        }

        public void WriteCluster(long clusterVCN, byte[] data)
        {
            WriteClusters(clusterVCN, data);
        }

        public void WriteClusters(long firstClusterVCN, byte[] data)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, m_fileRecord, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.WriteClusters(firstClusterVCN, data);
            }
            else
            {
                long firstSectorIndex = firstClusterVCN * m_volume.SectorsPerCluster;
                WriteSectors(firstClusterVCN, data);
            }
        }

        public byte[] ReadSectors(long firstSectorIndex, int count)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, m_fileRecord, (NonResidentAttributeRecord)m_attributeRecord);
                return attributeData.ReadSectors(firstSectorIndex, count);
            }
            else
            {
                byte[] data = ((ResidentAttributeRecord)m_attributeRecord).Data;
                long totalSectors = (long)Math.Ceiling((double)data.Length / m_volume.BytesPerSector);
                long highestSectorIndex = Math.Max(totalSectors - 1, 0);
                if (firstSectorIndex < 0 || firstSectorIndex > highestSectorIndex)
                {
                    throw new ArgumentOutOfRangeException("firstSectorIndex is not within the valid range");
                }

                int offset = (int)firstSectorIndex * m_volume.BytesPerSector;
                int bytesToRead;
                if (offset + count * m_volume.BytesPerSector <= data.Length)
                {
                    bytesToRead = count * m_volume.BytesPerCluster;
                }
                else
                {
                    bytesToRead = data.Length - offset;
                }
                return ByteReader.ReadBytes(data, offset, bytesToRead);
            }
        }

        public void WriteSectors(long firstSectorIndex, byte[] data)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, m_fileRecord, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.WriteSectors(firstSectorIndex, data);
            }
            else
            {
                byte[] recordData = ((ResidentAttributeRecord)m_attributeRecord).Data;
                long totalSectors = (long)Math.Ceiling((double)recordData.Length / m_volume.BytesPerSector);
                long highestSectorIndex = Math.Max(totalSectors - 1, 0);
                if (firstSectorIndex < 0 || firstSectorIndex > highestSectorIndex)
                {
                    throw new ArgumentOutOfRangeException("firstSectorIndex is not within the valid range");
                }

                int offset = (int)firstSectorIndex * m_volume.BytesPerSector;
                int bytesToWrite;
                if (offset + data.Length <= recordData.Length)
                {
                    bytesToWrite = data.Length;
                }
                else
                {
                    bytesToWrite = recordData.Length - offset;
                }
                ByteWriter.WriteBytes(recordData, offset, data, bytesToWrite);
                if (m_fileRecord != null)
                {
                    m_volume.UpdateFileRecord(m_fileRecord);
                }
            }
        }

        public void Extend(ulong additionalLengthInBytes)
        {
            ulong currentSize = this.Length;
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, m_fileRecord, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.Extend(additionalLengthInBytes);
            }
            else
            {
                byte[] data = ((ResidentAttributeRecord)m_attributeRecord).Data;
                ulong finalDataLength = (uint)data.Length + additionalLengthInBytes;
                ulong finalRecordLength = (uint)m_attributeRecord.RecordLength + additionalLengthInBytes;
                if (finalRecordLength >= (ulong)m_volume.AttributeRecordLengthToMakeNonResident &&
                    m_attributeRecord.AttributeType != AttributeType.AttributeList) // We will create an attribute list with the right attribute form in advance.
                {
                    // Convert the attribute to non-resident
                    long clustersToAllocate = (long)Math.Ceiling((double)finalDataLength / m_volume.BytesPerCluster);
                    if (clustersToAllocate > m_volume.NumberOfFreeClusters)
                    {
                        throw new DiskFullException();
                    }
                    NonResidentAttributeRecord attributeRecord = NonResidentAttributeRecord.Create(m_attributeRecord.AttributeType, m_attributeRecord.Name, m_attributeRecord.Instance);
                    NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, null, attributeRecord);
                    attributeData.Extend(finalDataLength);
                    attributeData.WriteClusters(0, data);
                    // Note that we overwrite the old attribute only after writing the non-resident data
                    if (m_fileRecord != null)
                    {
                        m_fileRecord.RemoveAttributeRecord(m_attributeRecord.AttributeType, m_attributeRecord.Name);
                        m_fileRecord.Attributes.Add(attributeRecord);
                    }
                    m_attributeRecord = attributeRecord;
                }
                else
                {
                    int currentLength = data.Length;
                    byte[] temp = new byte[currentLength + (int)additionalLengthInBytes];
                    Array.Copy(data, temp, data.Length);
                    ((ResidentAttributeRecord)m_attributeRecord).Data = temp;
                }

                if (m_fileRecord != null)
                {
                    m_volume.UpdateFileRecord(m_fileRecord);
                }
            }
        }

        public void Truncate(ulong newLengthInBytes)
        {
            ulong currentSize = this.Length;
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, m_fileRecord, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.Truncate(newLengthInBytes);
            }
            else
            {
                byte[] data = ((ResidentAttributeRecord)m_attributeRecord).Data;
                byte[] temp = new byte[newLengthInBytes];
                Array.Copy(data, temp, temp.Length);
                ((ResidentAttributeRecord)m_attributeRecord).Data = temp;
                if (m_fileRecord != null)
                {
                    m_volume.UpdateFileRecord(m_fileRecord);
                }
            }
        }

        public ulong AllocatedLength
        {
            get
            {
                if (m_attributeRecord is NonResidentAttributeRecord)
                {
                    return ((NonResidentAttributeRecord)m_attributeRecord).AllocatedLength;
                }
                else
                {
                    return (ulong)((ResidentAttributeRecord)m_attributeRecord).Data.Length;
                }
            }
        }

        public ulong Length
        {
            get
            {
                if (m_attributeRecord is NonResidentAttributeRecord)
                {
                    return ((NonResidentAttributeRecord)m_attributeRecord).FileSize;
                }
                else
                {
                    return (ulong)((ResidentAttributeRecord)m_attributeRecord).Data.Length;
                }
            }
        }

        public long ClusterCount
        {
            get
            {
                if (m_attributeRecord is NonResidentAttributeRecord)
                {
                    return ((NonResidentAttributeRecord)m_attributeRecord).DataClusterCount;
                }
                else
                {
                    // A resident AttributeRecord could span several clusters
                    int dataLength = ((ResidentAttributeRecord)m_attributeRecord).Data.Length;
                    return (long)Math.Ceiling((double)dataLength / m_volume.BytesPerCluster);
                }
            }
        }

        protected NTFSVolume Volume
        {
            get
            {
                return m_volume;
            }
        }

        protected FileRecord FileRecord
        {
            get
            {
                return m_fileRecord;
            }
        }

        public AttributeRecord AttributeRecord
        {
            get
            {
                return m_attributeRecord;
            }
        }
    }
}
