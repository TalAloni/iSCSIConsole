/* Copyright (C) 2014-2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// This class is used to read and modify the data of a non-resident attribute
    /// </summary>
    public class NonResidentAttributeData
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private NonResidentAttributeRecord m_attributeRecord;
        private ContentType m_contentType;

        public NonResidentAttributeData(NTFSVolume volume, FileRecord fileRecord, NonResidentAttributeRecord attributeRecord)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_attributeRecord = attributeRecord;
            m_contentType = GetContentType(fileRecord, attributeRecord.AttributeType);
        }

        /// <param name="clusterVCN">Cluster index</param>
        public byte[] ReadCluster(long clusterVCN)
        {
            return ReadClusters(clusterVCN, 1);
        }

        /// <param name="count">Maximum number of clusters to read</param>
        public byte[] ReadClusters(long firstClusterVCN, int count)
        {
            int sectorsPerCluster = m_volume.SectorsPerCluster;
            long firstSectorIndex = firstClusterVCN * sectorsPerCluster;
            int sectorCount = count * sectorsPerCluster;
            return ReadSectors(firstSectorIndex, sectorCount);
        }

        public void WriteClusters(long firstClusterVCN, byte[] data)
        {
            long firstSectorIndex = firstClusterVCN * m_volume.SectorsPerCluster;
            WriteSectors(firstSectorIndex, data);
        }

        public byte[] ReadSectors(long firstSectorIndex, int count)
        {
            int sectorsPerCluster = m_volume.SectorsPerCluster;
            int bytesPerSector = m_volume.BytesPerSector;
            long firstClusterVCN = firstSectorIndex / sectorsPerCluster;
            long lastSectorIndexToRead = firstSectorIndex + count - 1;
            long lastClusterVCNToRead = lastSectorIndexToRead / sectorsPerCluster;
            if (firstClusterVCN < LowestVCN || firstClusterVCN > HighestVCN)
            {
                string message = String.Format("Cluster VCN {0}-{1} is not within the valid range ({2}-{3})", firstClusterVCN, lastClusterVCNToRead, LowestVCN, HighestVCN);
                throw new ArgumentOutOfRangeException(message);
            }

            long highestSectorIndex = (HighestVCN + 1) * sectorsPerCluster - 1;
            if (lastSectorIndexToRead > highestSectorIndex)
            {
                lastSectorIndexToRead = highestSectorIndex;
            }

            byte[] result = new byte[count * bytesPerSector];
            ulong firstBytePosition = (ulong)firstSectorIndex * (uint)bytesPerSector;
            if (firstBytePosition < ValidDataLength)
            {
                KeyValuePairList<long, int> sequence = m_attributeRecord.DataRunSequence.TranslateToLBN(firstSectorIndex, count, sectorsPerCluster);
                long bytesRead = 0;
                foreach (KeyValuePair<long, int> run in sequence)
                {
                    byte[] data = m_volume.ReadSectors(run.Key, run.Value, m_contentType);
                    Array.Copy(data, 0, result, bytesRead, data.Length);
                    bytesRead += data.Length;
                }
            }

            int numberOfBytesToReturn = count * bytesPerSector;
            if (firstBytePosition + (uint)numberOfBytesToReturn > FileSize)
            {
                // If the last sector is only partially used or we have been asked to read beyond FileSize, trim result.
                numberOfBytesToReturn = (int)(FileSize - (ulong)firstSectorIndex * (uint)bytesPerSector);
                byte[] resultTrimmed = new byte[numberOfBytesToReturn];
                Array.Copy(result, resultTrimmed, numberOfBytesToReturn);
                result = resultTrimmed;
            }

            if (firstBytePosition < ValidDataLength && ValidDataLength < firstBytePosition + (uint)numberOfBytesToReturn)
            {
                // Zero-out bytes outside ValidDataLength
                int numberOfValidBytesInResult = (int)(ValidDataLength - firstBytePosition);
                ByteWriter.WriteBytes(result, numberOfValidBytesInResult, new byte[result.Length - numberOfValidBytesInResult]);
            }

            return result;
        }

        public void WriteSectors(long firstSectorIndex, byte[] data)
        {
            int sectorCount;
            long lastSectorIndexToWrite;
            long lastClusterVCNToWrite;
            int bytesPerSector = m_volume.BytesPerSector;
            int sectorsPerCluster = m_volume.SectorsPerCluster;
            ulong firstBytePosition = (ulong)firstSectorIndex * (uint)bytesPerSector;
            ulong nextBytePosition = firstBytePosition + (uint)data.Length;
            if (data.Length % bytesPerSector > 0)
            {
                int paddedLength = (int)Math.Ceiling((double)data.Length / bytesPerSector) * bytesPerSector;
                // last sector could be partial, we must zero-fill it before write
                sectorCount = paddedLength / bytesPerSector;
                lastSectorIndexToWrite = firstSectorIndex + sectorCount - 1;
                lastClusterVCNToWrite = lastSectorIndexToWrite / sectorsPerCluster;
                if (lastClusterVCNToWrite == HighestVCN)
                {
                    byte[] paddedData = new byte[paddedLength];
                    Array.Copy(data, paddedData, data.Length);
                    data = paddedData;
                }
                else
                {
                    // only the last sector can be partial
                    throw new ArgumentException("Cannot write partial sector");
                }
            }
            else
            {
                sectorCount = data.Length / bytesPerSector;
                lastSectorIndexToWrite = firstSectorIndex + sectorCount - 1;
                lastClusterVCNToWrite = lastSectorIndexToWrite / sectorsPerCluster;
            }

            long firstClusterVCN = firstSectorIndex / sectorsPerCluster;
            if (firstClusterVCN < LowestVCN || lastClusterVCNToWrite > HighestVCN)
            {
                string message = String.Format("Cluster VCN {0}-{1} is not within the valid range ({2}-{3})", firstClusterVCN, lastClusterVCNToWrite, LowestVCN, HighestVCN);
                throw new ArgumentOutOfRangeException(message);
            }

            if (firstBytePosition > ValidDataLength)
            {
                // We need to zero-fill all the the bytes up to ValidDataLength
                long firstSectorIndexToFill = (long)(ValidDataLength / (uint)bytesPerSector);
                int transferSizeInSectors = Settings.MaximumTransferSizeLBA;
                for (long sectorIndexToFill = firstSectorIndexToFill; sectorIndexToFill < firstSectorIndex; sectorIndexToFill += transferSizeInSectors)
                {
                    int sectorsToWrite = (int)Math.Min(transferSizeInSectors, firstSectorIndex - firstSectorIndexToFill);
                    byte[] fillData = new byte[sectorsToWrite * bytesPerSector];
                    if (sectorIndexToFill == firstSectorIndexToFill)
                    {
                        int bytesToRetain = (int)(ValidDataLength % (uint)bytesPerSector);
                        if (bytesToRetain > 0)
                        {
                            byte[] dataToRetain = ReadSectors(firstSectorIndexToFill, 1);
                            Array.Copy(dataToRetain, 0, fillData, 0, bytesToRetain);
                        }
                    }
                    WriteSectors(sectorIndexToFill, fillData);
                }
            }

            KeyValuePairList<long, int> sequence = m_attributeRecord.DataRunSequence.TranslateToLBN(firstSectorIndex, sectorCount, sectorsPerCluster);
            long bytesWritten = 0;
            foreach (KeyValuePair<long, int> run in sequence)
            {
                byte[] sectors = new byte[run.Value * bytesPerSector];
                Array.Copy(data, bytesWritten, sectors, 0, sectors.Length);
                m_volume.WriteSectors(run.Key, sectors, m_contentType);
                bytesWritten += sectors.Length;
            }

            if (nextBytePosition > ValidDataLength)
            {
                m_attributeRecord.ValidDataLength = nextBytePosition;
                if (m_fileRecord != null)
                {
                    m_volume.UpdateFileRecord(m_fileRecord);
                }
            }
        }

        public void Extend(ulong additionalLengthInBytes)
        {
            ulong freeBytesInCurrentAllocation = AllocatedLength - FileSize;
            if (additionalLengthInBytes > freeBytesInCurrentAllocation)
            {
                ulong bytesToAllocate = additionalLengthInBytes - freeBytesInCurrentAllocation;
                long clustersToAllocate = (long)Math.Ceiling((double)bytesToAllocate / m_volume.BytesPerCluster);
                // We might need to allocate an additional FileRecordSegment so we have to make sure we can extend the MFT if it is full
                if (clustersToAllocate + m_volume.NumberOfClustersRequiredToExtendMft > m_volume.NumberOfFreeClusters)
                {
                    throw new DiskFullException();
                }
                AllocateAdditionalClusters(clustersToAllocate);
            }

            m_attributeRecord.FileSize += additionalLengthInBytes;
            if (m_fileRecord != null)
            {
                if (m_attributeRecord.AttributeType == AttributeType.Data && m_attributeRecord.Name == String.Empty)
                {
                    // Windows NTFS v5.1 driver updates the value of the AllocatedLength field but does not usually update the value of
                    // the FileSize field belonging to the FileNameRecords that are stored in the FileRecord, which is likely to be 0.
                    List<FileNameRecord> fileNameRecords = m_fileRecord.FileNameRecords;
                    foreach (FileNameRecord fileNameRecord in fileNameRecords)
                    {
                        fileNameRecord.AllocatedLength = m_attributeRecord.AllocatedLength;
                    }
                }
                m_volume.UpdateFileRecord(m_fileRecord);
            }
        }

        private void AllocateAdditionalClusters(long clustersToAllocate)
        {
            KeyValuePairList<long, long> freeClusterRunList;
            DataRunSequence dataRuns = m_attributeRecord.DataRunSequence;
            if (dataRuns.Count == 0)
            {
                freeClusterRunList = m_volume.AllocateClusters(clustersToAllocate);
            }
            else
            {
                long desiredStartLCN = dataRuns.DataLastLCN + 1;
                freeClusterRunList = m_volume.AllocateClusters(desiredStartLCN, clustersToAllocate);

                long firstRunStartLCN = freeClusterRunList[0].Key;
                long firstRunLength = freeClusterRunList[0].Value;
                if (firstRunStartLCN == desiredStartLCN)
                {
                    // Merge with last run
                    DataRun lastRun = dataRuns[dataRuns.Count - 1];
                    lastRun.RunLength += firstRunLength;
                    m_attributeRecord.HighestVCN += (long)firstRunLength;
                    freeClusterRunList.RemoveAt(0);
                }
            }

            for (int index = 0; index < freeClusterRunList.Count; index++)
            {
                long runStartLCN = freeClusterRunList[index].Key;
                long runLength = freeClusterRunList[index].Value;

                DataRun run = new DataRun();
                long previousLCN = m_attributeRecord.DataRunSequence.LastDataRunStartLCN;
                run.RunOffset = runStartLCN - previousLCN;
                run.RunLength = runLength;
                m_attributeRecord.HighestVCN += runLength;
                m_attributeRecord.DataRunSequence.Add(run);
            }

            // Extend() will update the FileRecord
            m_attributeRecord.AllocatedLength += (ulong)(clustersToAllocate * m_volume.BytesPerCluster);
        }

        public void Truncate(ulong newLengthInBytes)
        {
            long clustersToKeep = (long)Math.Ceiling((double)newLengthInBytes / m_volume.BytesPerCluster);
            if (clustersToKeep < ClusterCount)
            {
                KeyValuePairList<long, long> clustersToDeallocate = m_attributeRecord.DataRunSequence.TranslateToLCN(clustersToKeep, ClusterCount - clustersToKeep);
                m_attributeRecord.DataRunSequence.Truncate(clustersToKeep);
                m_attributeRecord.HighestVCN = clustersToKeep - 1;
                m_attributeRecord.AllocatedLength = (ulong)(clustersToKeep * m_volume.BytesPerCluster);

                foreach (KeyValuePair<long, long> runToDeallocate in clustersToDeallocate)
                {
                    m_volume.DeallocateClusters(runToDeallocate.Key, runToDeallocate.Value);
                }
            }

            m_attributeRecord.FileSize = newLengthInBytes;
            if (m_attributeRecord.ValidDataLength > newLengthInBytes)
            {
                m_attributeRecord.ValidDataLength = newLengthInBytes;
            }

            if (m_fileRecord != null)
            {
                if (m_attributeRecord.AttributeType == AttributeType.Data && m_attributeRecord.Name == String.Empty)
                {
                    // Windows NTFS v5.1 driver updates the value of the AllocatedLength field but does not usually update the value of
                    // the FileSize field belonging to the FileNameRecords that are stored in the FileRecord, which is likely to be 0.
                    List<FileNameRecord> fileNameRecords = m_fileRecord.FileNameRecords;
                    foreach (FileNameRecord fileNameRecord in fileNameRecords)
                    {
                        fileNameRecord.AllocatedLength = m_attributeRecord.AllocatedLength;
                    }
                }
                m_volume.UpdateFileRecord(m_fileRecord);
            }
        }

        public long LowestVCN
        {
            get
            {
                return m_attributeRecord.LowestVCN;
            }
        }

        public long HighestVCN
        {
            get
            {
                return m_attributeRecord.HighestVCN;
            }
        }

        public long ClusterCount
        {
            get
            {
                return m_attributeRecord.DataClusterCount;
            }
        }

        public ulong AllocatedLength
        {
            get
            {
                return m_attributeRecord.AllocatedLength;
            }
        }

        public ulong FileSize
        {
            get
            {
                return m_attributeRecord.FileSize;
            }
        }

        public ulong ValidDataLength
        {
            get
            {
                return m_attributeRecord.ValidDataLength;
            }
        }

        public NonResidentAttributeRecord AttributeRecord
        {
            get
            {
                return m_attributeRecord;
            }
        }

        public static ContentType GetContentType(FileRecord fileRecord, AttributeType attributeType)
        {
            if (fileRecord != null)
            {
                long baseSegmentNumber = fileRecord.BaseSegmentNumber;
                if (baseSegmentNumber == MasterFileTable.MasterFileTableSegmentNumber || baseSegmentNumber == MasterFileTable.MftMirrorSegmentNumber)
                {
                    return (attributeType == AttributeType.Data) ? ContentType.MftData : ContentType.MftBitmap;
                }
                else if (baseSegmentNumber == MasterFileTable.LogFileSegmentNumber)
                {
                    return ContentType.LogFileData;
                }
                else if (baseSegmentNumber == MasterFileTable.VolumeSegmentNumber)
                {
                    return ContentType.VolumeBitmap;
                }
            }
            return GetContentType(attributeType);
        }

        public static ContentType GetContentType(AttributeType attributeType)
        {
            if (attributeType == AttributeType.AttributeList)
            {
                return ContentType.MftData;
            }
            else if (attributeType == AttributeType.IndexAllocation)
            {
                return ContentType.IndexData;
            }
            else if (attributeType == AttributeType.Bitmap)
            {
                return ContentType.IndexBitmap;
            }
            else
            {
                return ContentType.FileData;
            }
        }
    }
}
