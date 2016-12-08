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

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class DataRunSequence : List<DataRun>
    {
        public DataRunSequence() : base()
        {
            
        }

        /// <param name="startClusterOffset">Distance from LowestVCN</param>
        public KeyValuePairList<long, int> TranslateToLCN(long startClusterOffset, int clusterCount)
        {
            KeyValuePairList<long, int> result = new KeyValuePairList<long, int>();

            long previousLCN = 0;
            long clusterOffset = startClusterOffset;
            int clustersLeftToTranslate = clusterCount;
            bool translating = false;
            for(int index = 0; index < this.Count; index++)
            {
                DataRun run = this[index];
                long lcn = previousLCN + run.RunOffset;

                if (!translating) // still searching for startClusterVCN
                {
                    if (clusterOffset >= run.RunLength) // startClusterVCN is not in this run, check in the next run
                    {
                        clusterOffset -= run.RunLength;
                    }
                    else
                    {
                        translating = true;
                        
                        long startClusterLCN = lcn + clusterOffset;
                        long clustersLeftInRun = run.RunLength - clusterOffset; // how many clusters can be read from this run
                        int clustersTranslated = (int)Math.Min(clustersLeftToTranslate, clustersLeftInRun);
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
                    int clustersTranslated = (int)Math.Min(clustersLeftToTranslate, run.RunLength);
                    result.Add(lcn, clustersTranslated);
                    clustersLeftToTranslate -= clustersTranslated;

                    if (clustersLeftToTranslate == 0)
                    {
                        break;
                    }
                }
                previousLCN = lcn;
            }

            return result;
        }

        public long GetDataClusterLCN(long clusterVCN)
        {
            long previousLCN = 0;
            long clusterOffset = clusterVCN;
            foreach (DataRun run in this)
            {
                long lcn = previousLCN + run.RunOffset;
                
                if (clusterOffset >= run.RunLength) // not in this run, check in the next run
                {
                    clusterOffset = clusterOffset - run.RunLength;
                }
                else
                {
                    return lcn + clusterOffset;
                }

                previousLCN = lcn;
            }

            throw new InvalidDataException("Invalid cluster VCN");
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            long vcn = 0;
            for(int index = 0; index < this.Count; index++)
            {
                DataRun run = this[index];

                long absoluteLCN = GetDataClusterLCN(vcn);
                builder.AppendFormat("Data Run Number {0}: Absolute LCN: {1}, Length: {2}\n", index, absoluteLCN, run.RunLength);
                vcn += run.RunLength;
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
                    return -1;
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

        // The maximum NTFS file size is 2^64 bytes, so total number of file clusters can be represented using long
        // http://technet.microsoft.com/en-us/library/cc938937.aspx
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
