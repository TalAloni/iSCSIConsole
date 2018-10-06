/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public partial class NTFSVolume
    {
        /// <summary>
        /// This method is slower and should only be used for recovery purposes.
        /// </summary>
        public List<FileRecord> GetFileRecordsInDirectoryFromMft(long directoryBaseSegmentNumber)
        {
            return GetFileRecordsInDirectoryFromMft(directoryBaseSegmentNumber, false);
        }

        /// <summary>
        /// This method is slower and should only be used for recovery purposes.
        /// </summary>
        private List<FileRecord> GetFileRecordsInDirectoryFromMft(long directoryBaseSegmentNumber, bool includeMetaFiles)
        {
            long maxNumOfRecords = m_mft.GetNumberOfUsableSegments();
            List<FileRecord> result = new List<FileRecord>();

            for (long index = 0; index < maxNumOfRecords; index++)
            {
                FileRecord record;
                try
                {
                    record = m_mft.GetFileRecord(index);
                }
                catch (InvalidDataException)
                {
                    continue;
                }
                if (record != null)
                {
                    if (record.ParentDirectoryReference.SegmentNumber == directoryBaseSegmentNumber)
                    {
                        if (record.IsInUse && (includeMetaFiles || !record.IsMetaFile))
                        {
                            result.Add(record);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// This method is slower and should only be used for recovery purposes.
        /// </summary>
        private KeyValuePairList<MftSegmentReference, FileNameRecord> GetFileNameRecordsInDirectoryFromMft(long directoryBaseSegmentNumber)
        {
            KeyValuePairList<MftSegmentReference, FileNameRecord> result = new KeyValuePairList<MftSegmentReference, FileNameRecord>();
            List<FileRecord> fileRecords = GetFileRecordsInDirectoryFromMft(directoryBaseSegmentNumber);
            foreach (FileRecord fileRecord in fileRecords)
            {
                result.Add(fileRecord.BaseSegmentReference, fileRecord.FileNameRecord);
            }
            return result;
        }
    }
}
