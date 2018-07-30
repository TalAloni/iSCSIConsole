/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class NonResidentAttributeRecord : AttributeRecord
    {
        public const int HeaderLength = 0x40;

        // the first and last VCNs of the attribute:
        // Note: the maximum NTFS file size is 2^64 bytes, so total number of file clusters can be represented using long
        public long LowestVCN;  // The lowest VCN covered by this attribute record, stored as unsigned, but is within the range of long, see note above. (a.k.a. LowVCN)
        public long HighestVCN; // The highest VCN covered by this attribute record, stored as unsigned, but is within the range of long, see note above. (a.k.a. HighVCN)
        //private ushort mappingPairsOffset;
        public ushort CompressionUnitSize;
        // 4 reserved bytes
        // ulong AllocatedLength;       // An even multiple of the cluster size (not valid if the LowestVCN member is nonzero*)
        public ulong FileSize;          // The real size of a file with all of its runs combined, not valid if the LowestVCN member is nonzero
        public ulong ValidDataLength;   // Actual data written so far, (always less than or equal to the file size).
                                        // Data beyond ValidDataLength should be treated as 0. (not valid if the LowestVCN member is nonzero*)
        // * See: http://msdn.microsoft.com/en-us/library/bb470039%28v=vs.85%29.aspx

        private DataRunSequence m_dataRunSequence = new DataRunSequence();
        // Data run NULL terminator here
        // I've noticed that Windows Server 2003 puts 0x00 0x01 here for the $MFT FileRecord, seems to have no effect
        // (I've set it to 0 for the $MFT FileRecord in the MFT and the MFT mirror, and chkdsk did not report a problem.

        public NonResidentAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            LowestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            HighestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x18);
            ushort mappingPairsOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x20);
            CompressionUnitSize = LittleEndianConverter.ToUInt16(buffer, offset + 0x22);
            ulong allocatedLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x28);
            FileSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x30);
            ValidDataLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x38);

            int position = offset + mappingPairsOffset;
            while (position < offset + this.StoredRecordLength)
            {
                DataRun run = new DataRun();
                int length = run.Read(buffer, position);
                position += length;

                // Length 1 means there was only a header byte (i.e. terminator)
                if (length == 1)
                {
                    break;
                }

                m_dataRunSequence.Add(run);
            }

            if ((HighestVCN - LowestVCN + 1) != m_dataRunSequence.DataClusterCount)
            {
                throw new InvalidDataException("Invalid non-resident attribute record");
            }
        }

        public override byte[] GetBytes(int bytesPerCluster)
        {
            int dataRunSequenceLength = m_dataRunSequence.RecordLength;
            ushort mappingPairsOffset = (ushort)(HeaderLength + Name.Length * 2);
            uint length = this.RecordLength;
            byte[] buffer = new byte[length];
            WriteHeader(buffer, HeaderLength);

            ulong allocatedLength = (ulong)(m_dataRunSequence.DataClusterCount * bytesPerCluster);
            ushort dataRunsOffset = (ushort)(HeaderLength + Name.Length * 2);

            LittleEndianWriter.WriteInt64(buffer, 0x10, LowestVCN);
            LittleEndianWriter.WriteInt64(buffer, 0x18, HighestVCN);
            LittleEndianWriter.WriteUInt16(buffer, 0x20, mappingPairsOffset);
            LittleEndianWriter.WriteUInt16(buffer, 0x22, CompressionUnitSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, allocatedLength);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, FileSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x38, FileSize);

            int position = dataRunsOffset;
            foreach (DataRun run in m_dataRunSequence)
            { 
                byte[] runBytes = run.GetBytes();
                Array.Copy(runBytes, 0, buffer, position, runBytes.Length);
                position += runBytes.Length;
            }
            buffer[position] = 0; // Null termination


            return buffer;
        }

        /// <summary>
        /// Will read all of the data the attribute have, this should only be used when the data length is manageable
        /// </summary>
        public override byte[] GetData(NTFSVolume volume)
        {
            long clusterCount = HighestVCN - LowestVCN + 1;
            if (clusterCount > Int32.MaxValue)
            {
                throw new InvalidOperationException("Improper usage of GetData() method");
            }
            return ReadDataClusters(volume, LowestVCN, (int)clusterCount);
        }

        /// <param name="clusterVCN">Cluster index</param>
        public byte[] ReadDataCluster(NTFSVolume volume, long clusterVCN)
        {
            return ReadDataClusters(volume, clusterVCN, 1);
        }

        /// <param name="count">Maximum number of clusters to read</param>
        public byte[] ReadDataClusters(NTFSVolume volume, long firstClusterVCN, int count)
        {
            long lastClusterVcnToRead = firstClusterVCN + count - 1;
            if (firstClusterVCN < LowestVCN || firstClusterVCN > HighestVCN)
            {
                string message = String.Format("Cluster VCN {0}-{1} is not within the valid range ({2}-{3})", firstClusterVCN, lastClusterVcnToRead, LowestVCN, HighestVCN);
                throw new ArgumentOutOfRangeException(message);
            }

            if (lastClusterVcnToRead > HighestVCN)
            {
                lastClusterVcnToRead = HighestVCN;
            }

            byte[] result = new byte[count * volume.BytesPerCluster];
            KeyValuePairList<long, int> sequence = m_dataRunSequence.TranslateToLCN(firstClusterVCN - LowestVCN, count);
            long bytesRead = 0;
            foreach (KeyValuePair<long, int> run in sequence)
            {
                byte[] clusters = volume.ReadClusters(run.Key, run.Value);
                Array.Copy(clusters, 0, result, bytesRead, clusters.Length);
                bytesRead += clusters.Length;
            }

            // If the last cluster is only partially used or we have been asked to read clusters beyond the last cluster, trim result.
            // (Either of those cases could only be true if we have just read the last cluster).
            if (lastClusterVcnToRead == (long)HighestVCN)
            {
                long bytesToUse = (long)(FileSize - (ulong)firstClusterVCN * (uint)volume.BytesPerCluster);
                if (bytesToUse < result.Length)
                {
                    byte[] resultTrimmed = new byte[bytesToUse];
                    Array.Copy(result, resultTrimmed, bytesToUse);
                    return resultTrimmed;
                }
            }

            return result;
        }

        public void WriteDataClusters(NTFSVolume volume, long firstClusterVCN, byte[] data)
        {
            int count;
            long lastClusterVcnToWrite;

            if (data.Length % volume.BytesPerCluster > 0)
            {
                int paddedLength = (int)Math.Ceiling((double)data.Length / volume.BytesPerCluster) * volume.BytesPerCluster;
                // last cluster could be partial, we must zero-fill it before write
                count = paddedLength / volume.BytesPerCluster;
                lastClusterVcnToWrite = firstClusterVCN + count - 1;
                if (lastClusterVcnToWrite == HighestVCN)
                {
                    byte[] temp = new byte[paddedLength];
                    Array.Copy(data, temp, data.Length);
                    data = temp;
                }
                else
                {
                    // only the last cluster can be partial
                    throw new ArgumentException("Cannot write partial cluster");
                }
            }
            else
            {
                count = data.Length / volume.BytesPerCluster;
                lastClusterVcnToWrite = firstClusterVCN + count - 1;                
            }
            
            if (firstClusterVCN < LowestVCN || lastClusterVcnToWrite > HighestVCN)
            {
                string message = String.Format("Cluster VCN {0}-{1} is not within the valid range ({2}-{3})", firstClusterVCN, firstClusterVCN + count, LowestVCN, HighestVCN);
                throw new ArgumentOutOfRangeException(message);
            }

            KeyValuePairList<long, int> sequence = m_dataRunSequence.TranslateToLCN(firstClusterVCN, count);
            long bytesWritten = 0;
            foreach (KeyValuePair<long, int> run in sequence)
            {
                byte[] clusters = new byte[run.Value * volume.BytesPerCluster];
                Array.Copy(data, bytesWritten, clusters, 0, clusters.Length);
                volume.WriteClusters(run.Key, clusters);
                bytesWritten += clusters.Length;
            }
        }

        public byte[] ReadDataSectors(NTFSVolume volume, long firstSectorIndex, int count)
        {
            long firstClusterVcn = firstSectorIndex / volume.SectorsPerCluster;
            int sectorsToSkip = (int)(firstSectorIndex % volume.SectorsPerCluster);

            int clustersToRead = (int)Math.Ceiling((double)(count + sectorsToSkip) / volume.SectorsPerCluster);

            byte[] clusters = ReadDataClusters(volume, firstClusterVcn, clustersToRead);
            byte[] result = new byte[count * volume.BytesPerSector];
            Array.Copy(clusters, sectorsToSkip * volume.BytesPerSector, result, 0, result.Length);
            return result;
        }

        public void WriteDataSectors(NTFSVolume volume, long firstSectorIndex, byte[] data)
        {
            int count = data.Length / volume.BytesPerSector;
            long firstClusterVcn = firstSectorIndex / volume.SectorsPerCluster;
            int sectorsToSkip = (int)(firstSectorIndex % volume.SectorsPerCluster);

            int clustersToRead = (int)Math.Ceiling((double)(count + sectorsToSkip) / volume.SectorsPerCluster);
            byte[] clusters = ReadDataClusters(volume, firstClusterVcn, clustersToRead);
            Array.Copy(data, 0, clusters, sectorsToSkip * volume.BytesPerSector, data.Length);
            WriteDataClusters(volume, firstClusterVcn, clusters);
        }

        public void ExtendRecord(NTFSVolume volume, ulong additionalLength)
        {
            long numberOfClusters = (long)Math.Ceiling((double)FileSize / volume.BytesPerCluster);
            int freeBytesInLastCluster = (int)(numberOfClusters * volume.BytesPerCluster - (long)FileSize);

            if (additionalLength > (uint)freeBytesInLastCluster)
            {
                ulong bytesToAllocate = additionalLength - (uint)freeBytesInLastCluster;

                long clustersToAllocate = (long)Math.Ceiling((double)bytesToAllocate / volume.BytesPerCluster);
                AllocateAdditionalClusters(volume, clustersToAllocate);
            }

            FileSize += additionalLength;
        }

        // The maximum NTFS file size is 2^64 bytes, so total number of file clusters can be represented using long
        public void AllocateAdditionalClusters(NTFSVolume volume, long clustersToAllocate)
        {
            ulong desiredStartLCN = (ulong)DataRunSequence.DataLastLCN;
            KeyValuePairList<ulong, long> freeClusterRunList = volume.AllocateClusters(desiredStartLCN, clustersToAllocate);
            for (int index = 0; index < freeClusterRunList.Count; index++)
            {
                ulong runStartLCN = freeClusterRunList[index].Key;
                long runLength = freeClusterRunList[index].Value;

                bool mergeWithLastRun = (index == 0 && runStartLCN == desiredStartLCN);
                if (mergeWithLastRun)
                {
                    // we append this run to the last run
                    DataRun lastRun = DataRunSequence[DataRunSequence.Count - 1];
                    lastRun.RunLength += (long)runLength;
                    HighestVCN += (long)runLength;
                }
                else
                {
                    DataRun run = new DataRun();
                    ulong previousLCN = (ulong)DataRunSequence.LastDataRunStartLCN;
                    run.RunOffset = (long)(runStartLCN - previousLCN);
                    run.RunLength = (long)runLength;
                    HighestVCN += runLength;
                    DataRunSequence.Add(run);
                }
            }
        }

        /// <summary>
        /// This method should only be used for informational purposes.
        /// </summary>
        public KeyValuePairList<long, int> GetClustersInUse()
        {
            long clusterCount = HighestVCN - LowestVCN + 1;
            KeyValuePairList<long, int> sequence = m_dataRunSequence.TranslateToLCN(0, (int)clusterCount);
            return sequence;
        }

        /// <summary>
        /// When reading attributes, they may contain additional padding,
        /// so we should use StoredRecordLength to advance the buffer position instead.
        /// </summary>
        public override uint RecordLength
        {
            get 
            {
                int dataRunSequenceLength = m_dataRunSequence.RecordLength;
                ushort mappingPairsOffset = (ushort)(HeaderLength + Name.Length * 2);
                uint length = (uint)(mappingPairsOffset + dataRunSequenceLength);
                // Each record is aligned to 8-byte boundary
                length = (uint)Math.Ceiling((double)length / 8) * 8;
                return length;
            }
        }

        public DataRunSequence DataRunSequence
        {
            get
            {
                return m_dataRunSequence;
            }
        }
        
        public long DataClusterCount
        {
            get
            {
                return HighestVCN - LowestVCN + 1;;
            }
        }
    }
}
