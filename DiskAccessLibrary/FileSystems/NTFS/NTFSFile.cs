/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class NTFSFile
    {
        NTFSVolume m_volume;
        FileRecord m_fileRecord;

        public NTFSFile(NTFSVolume volume, long baseSegmentNumber)
        {
            m_volume = volume;
            m_fileRecord = m_volume.MasterFileTable.GetFileRecord(baseSegmentNumber);
        }

        public NTFSFile(NTFSVolume volume, FileRecord fileRecord)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
        }

        public byte[] ReadFromFile(ulong offset, int length)
        {
            long clusterVCN = (long)offset / m_volume.BytesPerCluster;
            int offsetInCluster = (int)(offset % (uint)m_volume.BytesPerCluster);
            int clusterCount = (int)Math.Ceiling((double)(offsetInCluster + length) / m_volume.BytesPerCluster);
            byte[] clustersBytes = m_fileRecord.DataRecord.ReadDataClusters(m_volume, clusterVCN, clusterCount);
            int readLength = clustersBytes.Length - offsetInCluster;
            if (readLength < length)
            {
                length = readLength;
            }
            byte[] result = new byte[length];
            Array.Copy(clustersBytes, offsetInCluster, result, 0, length);
            return result;
        }

        public void WriteToFile(ulong offset, byte[] bytes)
        {
            ulong currentSize = m_fileRecord.DataRecord.DataRealSize;
            if (offset + (uint)bytes.Length > currentSize)
            { 
                // file needs to be extended
                ulong additionalLength = offset + (uint)bytes.Length - currentSize;
                ExtendFile(additionalLength);
            }

            int position = 0;
            long clusterVCN = (long)(offset / (uint)m_volume.BytesPerCluster);
            int offsetInCluster = (int)(offset % (uint)m_volume.BytesPerCluster);
            if (offsetInCluster > 0)
            {
                int bytesLeftInCluster = m_volume.BytesPerCluster - offsetInCluster;
                int numberOfBytesToCopy = Math.Min(bytesLeftInCluster, bytes.Length);
                // Note: it's safe to send 'bytes' to ModifyCluster(), because it will ignore additional bytes after the first cluster
                ModifyCluster(clusterVCN, offsetInCluster, bytes);
                position += numberOfBytesToCopy;
                clusterVCN++;
            }

            while (position < bytes.Length)
            {
                int bytesLeft = bytes.Length - position;
                int numberOfBytesToCopy = Math.Min(m_volume.BytesPerCluster, bytesLeft);
                byte[] clusterBytes = new byte[numberOfBytesToCopy];
                Array.Copy(bytes, position, clusterBytes, 0, numberOfBytesToCopy);
                if (numberOfBytesToCopy < m_volume.BytesPerCluster)
                {
                    ModifyCluster(clusterVCN, 0, clusterBytes);
                }
                else
                {
                    FileRecord.DataRecord.WriteDataCluster(m_volume, clusterVCN, clusterBytes);
                }
                clusterVCN++;
                position += clusterBytes.Length;
            }
            m_volume.MasterFileTable.UpdateFileRecord(m_fileRecord);
        }

        /// <summary>
        /// Will read cluster and then modify the given bytes
        /// </summary>
        private void ModifyCluster(long clusterVCN, int offsetInCluster, byte[] bytes)
        {
            int bytesLeftInCluster = m_volume.BytesPerCluster - offsetInCluster;
            int numberOfBytesToCopy = Math.Min(bytesLeftInCluster, bytes.Length);

            byte[] clusterBytes = m_fileRecord.DataRecord.ReadDataCluster(m_volume, clusterVCN);
            // last cluster could be partial
            if (clusterBytes.Length < offsetInCluster + numberOfBytesToCopy)
            {
                byte[] temp = new byte[offsetInCluster + numberOfBytesToCopy];
                Array.Copy(clusterBytes, temp, clusterBytes.Length);
                clusterBytes = temp;
            }

            Array.Copy(bytes, 0, clusterBytes, offsetInCluster, numberOfBytesToCopy);
            FileRecord.DataRecord.WriteDataCluster(m_volume, clusterVCN, clusterBytes);
        }

        /// <param name="additionalLength">In bytes</param>
        public void ExtendFile(ulong additionalLength)
        {
            m_fileRecord.DataRecord.ExtendRecord(m_volume, additionalLength);
            if (m_fileRecord.LongFileNameRecord != null)
            {
                m_fileRecord.LongFileNameRecord.AllocatedSize = m_fileRecord.DataRecord.GetDataAllocatedSize(m_volume.BytesPerCluster);
                m_fileRecord.LongFileNameRecord.RealSize = m_fileRecord.DataRecord.DataRealSize;
            }
            if (m_fileRecord.ShortFileNameRecord != null)
            {
                m_fileRecord.ShortFileNameRecord.AllocatedSize = m_fileRecord.DataRecord.GetDataAllocatedSize(m_volume.BytesPerCluster);
                m_fileRecord.ShortFileNameRecord.RealSize = m_fileRecord.DataRecord.DataRealSize;
            }
            m_volume.MasterFileTable.UpdateFileRecord(m_fileRecord);
        }

        public NTFSVolume Volume
        {
            get
            {
                return m_volume;
            }
        }

        public FileRecord FileRecord
        {
            get
            {
                return m_fileRecord;
            }
        }

        public ulong Length
        {
            get
            {
                return m_fileRecord.DataRecord.DataRealSize;
            }
        }
    }
}
