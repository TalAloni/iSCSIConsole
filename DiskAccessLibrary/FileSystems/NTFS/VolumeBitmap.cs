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
    /// <summary>
    /// This class is used to read and update the volume bitmap (the $Bitmap metafile).
    /// Each bit in this file represents a cluster, extra bits are always set to 1.
    /// </summary>
    public class VolumeBitmap : NTFSFile
    {
        private long? m_numberOfFreeClusters;
        private long m_searchStartIndex = 0;
        private long m_numberOfClustersInVolume; // This will correctly reflect the current number of clusters in the volume when extending the bitmap.
        private readonly int ExtendGranularity = 8; // The number of bytes added to the bitmap when extending it, MUST be multiple of 8.

        public VolumeBitmap(NTFSVolume volume) : base(volume, MasterFileTable.BitmapSegmentReference)
        {
            m_numberOfClustersInVolume = volume.Size / volume.BytesPerCluster;
        }

        /// <summary>
        /// The caller must verify that there are enough free clusters before calling this method.
        /// </summary>
        public KeyValuePairList<long, long> AllocateClusters(long numberOfClusters)
        {
            KeyValuePairList<long, long> runList = AllocateClusters(m_searchStartIndex, numberOfClusters);
            long lastRunLCN = runList[runList.Count - 1].Key;
            long lastRunLength = runList[runList.Count - 1].Value;
            m_searchStartIndex = lastRunLCN + lastRunLength;
            return runList;
        }

        /// <summary>
        /// The caller must verify that there are enough free clusters before calling this method.
        /// </summary>
        public KeyValuePairList<long, long> AllocateClusters(long desiredStartLCN, long numberOfClusters)
        {
            int bitsPerCluster = Volume.BytesPerCluster * 8;
            KeyValuePairList<long, long> freeClusterRunList = FindClustersToAllocate(desiredStartLCN, numberOfClusters);
            if (freeClusterRunList == null)
            {
                throw new DiskFullException("Not enough free clusters");
            }
            // Mark the clusters as used in the volume bitmap
            for (int index = 0; index < freeClusterRunList.Count; index++)
            {
                long runStartLCN = freeClusterRunList[index].Key;
                long runLength = freeClusterRunList[index].Value;
                long bitmapVCN = (long)(runStartLCN / (uint)bitsPerCluster);
                int bitOffsetInBitmap = (int)(runStartLCN % (uint)bitsPerCluster);
                int bitsToAllocateInFirstCluster = bitsPerCluster - bitOffsetInBitmap;
                int bitmapClustersToRead = 1;
                if (runLength > bitsToAllocateInFirstCluster)
                {
                    bitmapClustersToRead += (int)Math.Ceiling((double)(runLength - bitsToAllocateInFirstCluster) / bitsPerCluster);
                }

                byte[] bitmap = ReadDataClusters(bitmapVCN, bitmapClustersToRead);
                for (int offset = 0; offset < runLength; offset++)
                {
                    SetBit(bitmap, bitOffsetInBitmap + offset);
                }
                WriteDataClusters(bitmapVCN, bitmap);
            }

            if (m_numberOfFreeClusters != null)
            {
                m_numberOfFreeClusters -= numberOfClusters;
            }
            return freeClusterRunList;
        }

        /// <summary>
        /// Return list of free cluster runs.
        /// </summary>
        /// <returns>key is cluster LCN, value is run length</returns>
        private KeyValuePairList<long, long> FindClustersToAllocate(long desiredStartLCN, long numberOfClusters)
        {
            KeyValuePairList<long, long> result = new KeyValuePairList<long, long>();

            long leftToFind;
            long endLCN = m_numberOfClustersInVolume - 1;
            KeyValuePairList<long, long> segment = FindClustersToAllocate(desiredStartLCN, endLCN, numberOfClusters, out leftToFind);
            result.AddRange(segment);

            if (leftToFind > 0 && desiredStartLCN > 0)
            {
                segment = FindClustersToAllocate(0, desiredStartLCN - 1, leftToFind, out leftToFind);
                result.AddRange(segment);
            }

            if (leftToFind > 0)
            {
                return null;
            }

            return result;
        }

        /// <param name="clustersToAllocate">Number of clusters to allocate</param>
        private KeyValuePairList<long, long> FindClustersToAllocate(long startLCN, long endLCN, long clustersToAllocate, out long leftToFind)
        {
            int bitsPerCluster = Volume.BytesPerCluster * 8;
            KeyValuePairList<long, long> result = new KeyValuePairList<long, long>();
            leftToFind = clustersToAllocate;

            long runStartLCN = 0; // temporary
            long runLength = 0;
            long nextLCN = startLCN;
            long bufferedBitmapVCN = -1;
            byte[] bufferedBitmap = null;
            while (nextLCN <= endLCN && leftToFind > 0)
            {
                long currentBitmapVCN = (long)(nextLCN / (uint)bitsPerCluster);
                if (currentBitmapVCN != bufferedBitmapVCN)
                {
                    bufferedBitmap = ReadDataCluster(currentBitmapVCN);
                    bufferedBitmapVCN = currentBitmapVCN;
                }

                int bitOffsetInBitmap = (int)(nextLCN % (uint)bitsPerCluster);
                if (IsBitClear(bufferedBitmap, bitOffsetInBitmap))
                {
                    if (runLength == 0)
                    {
                        runStartLCN = nextLCN;
                        runLength = 1;
                    }
                    else
                    {
                        runLength++;
                    }
                    leftToFind--;
                }
                else
                {
                    if (runLength > 0)
                    {
                        // Add this run
                        result.Add(runStartLCN, runLength);
                        runLength = 0;
                    }
                }
                nextLCN++;
            }

            // Add the last run
            if (runLength > 0)
            {
                result.Add(runStartLCN, runLength);
            }

            return result;
        }

        public void DeallocateClusters(long startLCN, long numberOfClusters)
        {
            int bitsPerCluster = Volume.BytesPerCluster * 8;
            long bitmapVCN = (long)(startLCN / (uint)bitsPerCluster);
            int bitOffsetInBitmap = (int)(startLCN % (uint)bitsPerCluster);
            int bitsToDeallocateInFirstCluster = bitsPerCluster - bitOffsetInBitmap;
            int bitmapClustersToRead = 1;
            if (numberOfClusters > bitsToDeallocateInFirstCluster)
            {
                bitmapClustersToRead += (int)Math.Ceiling((double)(numberOfClusters - bitsToDeallocateInFirstCluster) / bitsPerCluster);
            }

            byte[] bitmap = ReadDataClusters(bitmapVCN, bitmapClustersToRead);
            for (int offset = 0; offset < numberOfClusters; offset++)
            {
                ClearBit(bitmap, bitOffsetInBitmap + offset);
            }
            WriteDataClusters(bitmapVCN, bitmap);

            if (m_numberOfFreeClusters != null)
            {
                m_numberOfFreeClusters += numberOfClusters;
            }
        }

        // Each bit in the $Bitmap file represents a cluster.
        // The size of the $Bitmap file is always a multiple of 8 bytes, extra bits are always set to 1.
        private long CountNumberOfFreeClusters()
        {
            int transferSizeInClusters = Settings.MaximumTransferSizeLBA / Volume.SectorsPerCluster;
            long endLCN = m_numberOfClustersInVolume - 1;
            long bitmapLastVCN = endLCN / (Volume.BytesPerCluster * 8);
            
            long result = 0;

            // Build lookup table
            byte[] lookup = new byte[256];
            for (int index = 0; index < 256; index++)
            {
                lookup[index] = CountNumberOfClearBits((byte)index);
            }

            // Extra bits will be marked as used, so no need for special treatment
            for (long bitmapVCN = 0; bitmapVCN <= bitmapLastVCN; bitmapVCN += transferSizeInClusters)
            {
                byte[] bitmap = ReadDataClusters(bitmapVCN, transferSizeInClusters);
                for (int index = 0; index < bitmap.Length; index++)
                {
                    result += lookup[bitmap[index]];
                }
            }

            return result;
        }

        /// <param name="numberOfAdditionalClusters">
        /// The number of additional clusters being added to the volume.
        /// </param>
        /// <remarks>
        /// 1TB of additional allocation will result in a bitmap of 32 MB (assuming 4KB clusters).
        /// 128TB of additional allocation will result in a bitmap of 512 MB (assuming 8KB clusters).
        /// </remarks>
        internal void Extend(long numberOfAdditionalClusters)
        {
            int bytesPerCluster = Volume.BytesPerCluster;
            int bitsPerCluster = bytesPerCluster * 8;
            if (m_numberOfFreeClusters == null)
            {
                m_numberOfFreeClusters = CountNumberOfFreeClusters();
            }
            // When extending the $Bitmap file we might need to allocate additional clusters for the bitmap,
            // However, we haven't yet extended the bitmap so those clusters must be allocated within the current bitmap.
            // To reduce the chance of running out of space, we first use all the remaining bits in the last bitmap cluster.
            long currentNumberOfClustersInVolume = Volume.Size / bytesPerCluster;
            int usedBitsInLastCluster = (int)((currentNumberOfClustersInVolume - 1) % bitsPerCluster) + 1;
            int unusedBitsInLastCluster = bitsPerCluster - usedBitsInLastCluster;
            if (unusedBitsInLastCluster < numberOfAdditionalClusters)
            {
                const int MinimumNumberOfFreeClusters = 2;
                long bitmapClustersNeeded = (long)Math.Ceiling((double)(numberOfAdditionalClusters - unusedBitsInLastCluster) / bitsPerCluster);
                if (unusedBitsInLastCluster + m_numberOfFreeClusters < MinimumNumberOfFreeClusters)
                {
                    throw new DiskFullException("Not enough free clusters to extend $Bitmap before extending the volume");
                }
            }

            if (unusedBitsInLastCluster > 0)
            {
                int numberOfVolumeClustersToAllocate = (int)Math.Min(unusedBitsInLastCluster, numberOfAdditionalClusters);
                long bitmapClusterVCN = currentNumberOfClustersInVolume / bitsPerCluster;
                byte[] bitmap = ReadDataCluster(bitmapClusterVCN);
                int originalBitmapLength = bitmap.Length;
                int finalBitmapLength = (int)Math.Ceiling((double)(usedBitsInLastCluster + numberOfVolumeClustersToAllocate) / (ExtendGranularity * 8)) * ExtendGranularity;
                if (bitmap.Length < finalBitmapLength)
                {
                    bitmap = ByteUtils.Concatenate(bitmap, new byte[finalBitmapLength - bitmap.Length]);
                }
                // The NTFS v5.1 driver extend the $Bitmap file in chunks of 8 bytes, the last bytes
                // may contain extra bits that were previously set as used, and now have to be free.
                int numberOfBitsToClear = originalBitmapLength * 8 - usedBitsInLastCluster;
                for (int offset = 0; offset < numberOfBitsToClear; offset++)
                {
                    ClearBit(bitmap, usedBitsInLastCluster + offset);
                }
                WriteData((ulong)(bitmapClusterVCN * bytesPerCluster), bitmap);
                m_numberOfFreeClusters += numberOfVolumeClustersToAllocate;
                m_numberOfClustersInVolume += numberOfVolumeClustersToAllocate;
                numberOfAdditionalClusters -= numberOfVolumeClustersToAllocate;
            }

            while (numberOfAdditionalClusters > 0)
            {
                int numberOfVolumeClustersToAllocate = (int)Math.Min(Math.Min(m_numberOfFreeClusters.Value * bitsPerCluster, numberOfAdditionalClusters), Settings.MaximumTransferSizeLBA * 8);
                int bitmapLength = (int)Math.Ceiling((double)numberOfVolumeClustersToAllocate / (ExtendGranularity * 8)) * ExtendGranularity;
                byte[] bitmap = new byte[bitmapLength];
                // Mark extra bits as used:
                int bitOffsetInBitmap = numberOfVolumeClustersToAllocate;
                while (bitOffsetInBitmap < bitmap.Length * 8)
                {
                    SetBit(bitmap, bitOffsetInBitmap);
                    bitOffsetInBitmap++;
                }
                WriteData(this.Length, bitmap);
                m_numberOfFreeClusters += numberOfVolumeClustersToAllocate;
                m_numberOfClustersInVolume += numberOfVolumeClustersToAllocate;
                numberOfAdditionalClusters -= numberOfVolumeClustersToAllocate;
            }
        }

        private byte[] ReadDataCluster(long bitmapClusterVCN)
        {
            // VolumeBitmap data record is always non-resident (the NTFS v5.1 driver does not support a VolumeBitmap having a resident data record)
            return this.Data.ReadCluster(bitmapClusterVCN);
        }

        private byte[] ReadDataClusters(long bitmapClusterVCN, int count)
        {
            return this.Data.ReadClusters(bitmapClusterVCN, count);
        }

        private void WriteDataClusters(long bitmapClusterVCN, byte[] data)
        {
            this.Data.WriteCluster(bitmapClusterVCN, data);
        }

        public long NumberOfFreeClusters
        {
            get
            {
                if (m_numberOfFreeClusters == null)
                {
                    m_numberOfFreeClusters = CountNumberOfFreeClusters();
                }
                return m_numberOfFreeClusters.Value;
            }
        }

        private static bool IsBitClear(byte[] bitmap, int bitOffsetInBitmap)
        {
            int byteOffset = bitOffsetInBitmap / 8;
            int bitOffsetInByte = bitOffsetInBitmap % 8;
            bool isInUse = ((bitmap[byteOffset] >> bitOffsetInByte) & 0x01) != 0;
            return !isInUse;
        }

        private static void SetBit(byte[] bitmap, int bitOffsetInBitmap)
        {
            int byteOffset = bitOffsetInBitmap / 8;
            int bitOffsetInByte = bitOffsetInBitmap % 8;
            bitmap[byteOffset] |= (byte)(0x01 << bitOffsetInByte);
        }

        private static void ClearBit(byte[] bitmap, int bitOffsetInBitmap)
        {
            int byteOffset = bitOffsetInBitmap / 8;
            int bitOffsetInByte = bitOffsetInBitmap % 8;
            bitmap[byteOffset] &= (byte)(~(0x01 << bitOffsetInByte));
        }

        private static byte CountNumberOfClearBits(byte bitmap)
        {
            byte result = 0;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                bool isClear = ((bitmap >> bitIndex) & 0x01) == 0;
                if (isClear)
                {
                    result++;
                }
            }
            return result;
        }
    }
}
