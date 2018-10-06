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

        public NonResidentAttributeData(NTFSVolume volume, FileRecord fileRecord, NonResidentAttributeRecord attributeRecord)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_attributeRecord = attributeRecord;
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
                    byte[] data = m_volume.ReadSectors(run.Key, run.Value);
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
                m_volume.WriteSectors(run.Key, sectors);
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
                if (clustersToAllocate > m_volume.NumberOfFreeClusters)
                {
                    throw new DiskFullException();
                }
                AllocateAdditionalClusters(clustersToAllocate);
            }

            m_attributeRecord.FileSize += additionalLengthInBytes;
            if (m_fileRecord != null)
            {
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
    }
}
