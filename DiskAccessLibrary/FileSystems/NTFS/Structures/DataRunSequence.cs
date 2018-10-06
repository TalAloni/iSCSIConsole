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

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// MappingPairs array
    /// </summary>
    public class DataRunSequence : List<DataRun>
    {
        public DataRunSequence() : base()
        {
        }

        public DataRunSequence(byte[] buffer, int offset, int length) : base()
        {
            int position = offset;
            while (position < offset + length)
            {
                DataRun run = new DataRun(buffer, position);
                position += run.RecordLengthOnDisk;

                // Length 1 means there was only a header byte (i.e. terminator)
                if (run.RecordLengthOnDisk == 1)
                {
                    break;
                }

                this.Add(run);
            }
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            foreach (DataRun run in this)
            {
                run.WriteBytes(buffer, offset);
                offset += run.RecordLength;
            }
            buffer[offset] = 0; // Sequence terminator
            // I've noticed that Windows Server 2003 puts 0x00 0x01 as sequence terminator for the $MFT FileRecord, seems to have no effect
            // (I've set it to 0x00 for the $MFT FileRecord in the MFT and the MFT mirror, and chkdsk did not report a problem.
        }

        public void Truncate(long newClusterCount)
        {
            long clustersCovered = 0;
            for (int index = 0; index < this.Count; index++)
            {
                DataRun run = this[index];
                if (clustersCovered >= newClusterCount)
                {
                    this.RemoveRange(index, this.Count - index);
                    return;
                }
                else if (clustersCovered + run.RunLength > newClusterCount)
                {
                    run.RunLength = newClusterCount - clustersCovered;
                }
                clustersCovered += run.RunLength;
            }
        }

        public KeyValuePairList<long, int> TranslateToLBN(long firstSectorIndex, int sectorCount, int sectorsPerCluster)
        {
            KeyValuePairList<long, int> result = new KeyValuePairList<long, int>();

            long previousLCN = 0;
            long sectorOffset = firstSectorIndex;
            int sectorsLeftToTranslate = sectorCount;
            bool translating = false;
            for (int index = 0; index < this.Count; index++)
            {
                DataRun run = this[index];
                long runStartLCN = previousLCN + run.RunOffset;
                long runStartLSN = runStartLCN * sectorsPerCluster;
                long runLengthInClusters = run.RunLength;
                long runLengthInSectors = runLengthInClusters * sectorsPerCluster;

                if (!translating) // still searching for firstSectorIndex
                {
                    if (sectorOffset >= runLengthInSectors) // firstSectorIndex is not in this run, check in the next run
                    {
                        sectorOffset -= runLengthInSectors;
                    }
                    else
                    {
                        translating = true;

                        long startSectorLSN = runStartLSN + sectorOffset;
                        long sectorsLeftInRun = runLengthInSectors - sectorOffset; // how many sectors can be read from this run
                        int sectorsTranslated = (int)Math.Min(sectorsLeftToTranslate, sectorsLeftInRun);
                        result.Add(startSectorLSN, sectorsTranslated);
                        sectorsLeftToTranslate -= sectorsTranslated;

                        if (sectorsLeftToTranslate == 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    int sectorsTranslated = (int)Math.Min(sectorsLeftToTranslate, runLengthInSectors);
                    result.Add(runStartLSN, sectorsTranslated);
                    sectorsLeftToTranslate -= sectorsTranslated;

                    if (sectorsLeftToTranslate == 0)
                    {
                        break;
                    }
                }
                previousLCN = runStartLCN;
            }

            return result;
        }

        public KeyValuePairList<long, long> TranslateToLCN(long firstClusterVCN, long clusterCount)
        {
            KeyValuePairList<long, long> result = new KeyValuePairList<long, long>();

            long previousLCN = 0;
            long clusterOffset = firstClusterVCN;
            long clustersLeftToTranslate = clusterCount;
            bool translating = false;
            for(int index = 0; index < this.Count; index++)
            {
                DataRun run = this[index];
                long runStartLCN = previousLCN + run.RunOffset;

                if (!translating) // still searching for firstClusterVCN
                {
                    if (clusterOffset >= run.RunLength) // firstClusterVCN is not in this run, check in the next run
                    {
                        clusterOffset -= run.RunLength;
                    }
                    else
                    {
                        translating = true;
                        
                        long startClusterLCN = runStartLCN + clusterOffset;
                        long clustersLeftInRun = run.RunLength - clusterOffset; // how many clusters can be read from this run
                        long clustersTranslated = (long)Math.Min(clustersLeftToTranslate, clustersLeftInRun);
                        result.Add(startClusterLCN, clustersTranslated);
                        clustersLeftToTranslate -= clustersTranslated;

                        if (clustersLeftToTranslate == 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    long clustersTranslated = (long)Math.Min(clustersLeftToTranslate, run.RunLength);
                    result.Add(runStartLCN, clustersTranslated);
                    clustersLeftToTranslate -= clustersTranslated;

                    if (clustersLeftToTranslate == 0)
                    {
                        break;
                    }
                }
                previousLCN = runStartLCN;
            }

            return result;
        }

        public long GetDataClusterLCN(long clusterVCN)
        {
            long previousLCN = 0;
            long clusterOffset = clusterVCN;
            foreach (DataRun run in this)
            {
                long runStartLCN = previousLCN + run.RunOffset;
                
                if (clusterOffset >= run.RunLength) // not in this run, check in the next run
                {
                    clusterOffset = clusterOffset - run.RunLength;
                }
                else
                {
                    return runStartLCN + clusterOffset;
                }

                previousLCN = runStartLCN;
            }

            throw new InvalidDataException("Invalid cluster VCN");
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            long previousLCN = 0;
            for(int index = 0; index < this.Count; index++)
            {
                DataRun run = this[index];
                long runStartLCN = previousLCN + run.RunOffset;
                builder.AppendFormat("Data Run Number {0}: Start LCN: {1}, Length: {2}\n", index, runStartLCN, run.RunLength);
                previousLCN = runStartLCN;
            }

            builder.AppendFormat("Number of clusters in sequence: {0}\n", DataClusterCount); 
            return builder.ToString();
        }

        public long FirstDataRunLCN
        {
            get
            {
                if (this.Count > 0)
                {
                    return this[0].RunOffset;
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// LCN of the first cluster in the last data run
        /// </summary>
        public long LastDataRunStartLCN
        {
            get
            {
                if (this.Count > 0)
                {
                    long clusterIndex = 0;
                    for (int index = 0; index < this.Count - 1; index++)
                    {
                        DataRun run = this[index];
                        clusterIndex += run.RunLength;
                    }
                    return GetDataClusterLCN(clusterIndex);
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// LCN of the last cluster in the last data run
        /// </summary>
        public long DataLastLCN
        {
            get
            {
                return GetDataClusterLCN(this.DataClusterCount - 1);
            }
        }

        /// <remarks>
        /// The maximum NTFS file size is 2^64 bytes, so total number of file clusters can be represented using long
        /// https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-2000-server/cc938937(v=technet.10)
        /// </remarks>
        public long DataClusterCount
        {
            get
            {
                long result = 0;
                foreach (DataRun run in this)
                {
                    result += run.RunLength;
                }
                return result;
            }
        }

        public int RecordLength
        {
            get
            {
                int dataRunRecordSequenceLength = 0;
                foreach (DataRun run in this)
                {
                    dataRunRecordSequenceLength += run.RecordLength;
                }
                dataRunRecordSequenceLength += 1; // Null Termination
                return dataRunRecordSequenceLength;
            }
        }
    }
}
