/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// This class is used to read and update the bitmap of a Bitmap attribute (e.g. the MFT bitmap where each bit represents a FileRecord).
    /// Windows extends the MFT bitmap in multiple of 8 bytes, so the number of bits may be greater than the record count.
    /// </summary>
    /// <remarks>
    /// The Bitmap attribute can be either resident or non-resident.
    /// </remarks>
    public class BitmapData : AttributeData
    {
        private const int ExtendGranularity = 8; // The number of bytes added to the bitmap when extending it, MUST be multiple of 8.

        private long m_searchStartIndex = 0;
        private long m_numberOfUsableBits;

        public BitmapData(NTFSVolume volume, FileRecord fileRecord, AttributeRecord attributeRecord, long numberOfUsableBits) : base(volume, fileRecord, attributeRecord)
        {
            m_numberOfUsableBits = numberOfUsableBits;
        }

        /// <returns>Record index</returns>
        public long? AllocateRecord(uint transactionID)
        {
            long? recordIndex = AllocateRecord(m_searchStartIndex, transactionID);
            if (recordIndex.HasValue)
            {
                m_searchStartIndex = recordIndex.Value + 1;
            }
            else
            {
                recordIndex = AllocateRecord(0, m_searchStartIndex - 1, transactionID);
                if (recordIndex.HasValue)
                {
                    m_searchStartIndex = recordIndex.Value + 1;
                }
            }
            return recordIndex;
        }

        /// <returns>Record index</returns>
        public long? AllocateRecord(long searchStartIndex, uint transactionID)
        {
            return AllocateRecord(searchStartIndex, m_numberOfUsableBits - 1, transactionID);
        }

        /// <returns>Record index</returns>
        public long? AllocateRecord(long searchStartIndex, long searchEndIndex, uint transactionID)
        {
            long bufferedVCN = -1;
            byte[] bufferedClusterBytes = null;

            for (long index = searchStartIndex; index <= searchEndIndex; index++)
            {
                long currentVCN = index / (Volume.BytesPerCluster * 8);
                if (currentVCN != bufferedVCN)
                {
                    bufferedClusterBytes = ReadCluster(currentVCN);
                    bufferedVCN = currentVCN;
                }

                int bitOffsetInCluster = (int)(index % (Volume.BytesPerCluster * 8));
                if (IsBitClear(bufferedClusterBytes, bitOffsetInCluster))
                {
                    if (!this.AttributeRecord.IsResident)
                    {
                        BitmapRange bitmapRange = new BitmapRange((uint)bitOffsetInCluster, 1);
                        byte[] operationData = bitmapRange.GetBytes();
                        ulong streamOffset = (ulong)(currentVCN * Volume.BytesPerCluster);
                        Volume.LogClient.WriteLogRecord(FileRecord.BaseSegmentReference, this.AttributeRecord, streamOffset, NTFSLogOperation.SetBitsInNonResidentBitMap, operationData, NTFSLogOperation.ClearBitsInNonResidentBitMap, operationData, transactionID);
                    }
                    SetBit(bufferedClusterBytes, bitOffsetInCluster);
                    WriteCluster(currentVCN, bufferedClusterBytes);
                    return index;
                }
            }

            return null;
        }

        public void DeallocateRecord(long recordIndex, uint transactionID)
        {
            long currentVCN = recordIndex / (Volume.BytesPerCluster * 8);
            int bitOffsetInCluster = (int)(recordIndex % (Volume.BytesPerCluster * 8));
            byte[] clusterBytes = ReadCluster(currentVCN);
            if (!IsBitClear(clusterBytes, bitOffsetInCluster))
            {
                if (!this.AttributeRecord.IsResident)
                {
                    BitmapRange bitmapRange = new BitmapRange((uint)bitOffsetInCluster, 1);
                    byte[] operationData = bitmapRange.GetBytes();
                    ulong streamOffset = (ulong)(currentVCN * Volume.BytesPerCluster);
                    Volume.LogClient.WriteLogRecord(FileRecord.BaseSegmentReference, this.AttributeRecord, streamOffset, NTFSLogOperation.ClearBitsInNonResidentBitMap, operationData, NTFSLogOperation.SetBitsInNonResidentBitMap, operationData, transactionID);
                }
                ClearBit(clusterBytes, bitOffsetInCluster);
                WriteCluster(currentVCN, clusterBytes);
            }
        }

        public void ExtendBitmap(long numberOfBits)
        {
            ExtendBitmap(numberOfBits, false);
        }

        /// <param name="prewriteBytes">True to zero out the extension in advance, False to rely on ValidDataLength</param>
        internal void ExtendBitmap(long numberOfBits, bool prewriteBytes)
        {
            long numberOfUnusedBits = (long)(this.Length * 8 - (ulong)m_numberOfUsableBits);
            if (numberOfBits > numberOfUnusedBits)
            {
                long additionalBits = numberOfBits - numberOfUnusedBits;
                ulong additionalBytes = (ulong)Math.Ceiling((double)additionalBits / (ExtendGranularity * 8)) * ExtendGranularity;
                if (prewriteBytes)
                {
                    this.WriteBytes(this.Length, new byte[additionalBytes]);
                }
                else
                {
                    this.Extend(additionalBytes);
                }
            }
            m_numberOfUsableBits += numberOfBits;
        }

        public void TruncateBitmap(long newLengthInBits)
        {
            m_numberOfUsableBits = newLengthInBits;
            ulong newLengthInBytes = (ulong)Math.Ceiling((double)newLengthInBits / (ExtendGranularity * 8)) * ExtendGranularity;
            this.Truncate(newLengthInBytes);
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

        public long NumberOfUsableBits
        {
            get
            {
                return m_numberOfUsableBits;
            }
        }
    }
}
