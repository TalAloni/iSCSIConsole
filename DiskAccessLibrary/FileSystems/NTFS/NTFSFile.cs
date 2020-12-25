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
    /// This class provides an interface to access the data of an NTFS file's data attribute,
    /// In the case of an unnamed data stream (i.e. the primary data stream), the file name record(s) and directory index will be updated to reflect any changes.
    /// </summary>
    public class NTFSFile
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeData m_data;

        public NTFSFile(NTFSVolume volume, MftSegmentReference fileReference) : this(volume, volume.GetFileRecord(fileReference))
        {
        }

        public NTFSFile(NTFSVolume volume, FileRecord fileRecord) : this(volume, fileRecord, String.Empty)
        {
        }

        /// <param name="attributeName">The name of the data attribute we wish to access</param>
        public NTFSFile(NTFSVolume volume, FileRecord fileRecord, string attributeName)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            AttributeRecord attributeRecord = fileRecord.GetAttributeRecord(AttributeType.Data, attributeName);
            m_data = new AttributeData(m_volume, m_fileRecord, attributeRecord);
        }

        public byte[] ReadData(ulong offset, int length)
        {
            return m_data.ReadBytes(offset, length);
        }

        public void WriteData(ulong offset, byte[] data)
        {
            ulong fileSizeBefore = m_data.Length;
            m_data.WriteBytes(offset, data);
            if (m_data.AttributeName == String.Empty && fileSizeBefore != m_data.Length)
            {
                UpdateDirectoryIndex();
            }
        }

        public void SetLength(ulong newLengthInBytes)
        {
            if (newLengthInBytes > m_data.Length)
            {
                ulong additionalLengthInBytes = newLengthInBytes - m_data.Length;
                m_data.Extend(additionalLengthInBytes);
            }
            else if (newLengthInBytes < m_data.Length)
            {
                m_data.Truncate(newLengthInBytes);
            }
            else
            {
                return;
            }

            if (m_data.AttributeName == String.Empty)
            {
                UpdateDirectoryIndex();
            }
        }

        private void UpdateDirectoryIndex()
        {
            List<FileNameRecord> fileNameRecords = m_fileRecord.FileNameRecords;
            foreach (FileNameRecord fileNameRecord in fileNameRecords)
            {
                fileNameRecord.AllocatedLength = m_data.AllocatedLength;
                fileNameRecord.FileSize = m_data.Length;
            }
            m_volume.UpdateDirectoryIndex(m_fileRecord.ParentDirectoryReference, fileNameRecords);
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

        public AttributeData Data
        {
            get
            {
                return m_data;
            }
        }

        public ulong Length
        {
            get
            {
                return m_data.Length;
            }
        }
    }
}
