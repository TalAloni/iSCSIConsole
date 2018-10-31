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
    public class IndexHelper
    {
        public static string GetIndexName(AttributeType indexedAttributeType)
        {
            return "$I" + ((uint)indexedAttributeType).ToString("X");
        }

        public static void InitializeIndexRoot(IndexRootRecord indexRoot, AttributeType indexedAttributeType, int bytesPerIndexRecord, int bytesPerCluster)
        {
            indexRoot.IndexedAttributeType = indexedAttributeType;
            if (indexedAttributeType == AttributeType.FileName)
            {
                indexRoot.CollationRule = CollationRule.Filename;
            }
            indexRoot.BytesPerIndexRecord = (uint)bytesPerIndexRecord;
            if (bytesPerIndexRecord >= bytesPerCluster)
            {
                indexRoot.BlocksPerIndexRecord = (byte)(bytesPerIndexRecord / bytesPerCluster);
            }
            else
            {
                indexRoot.BlocksPerIndexRecord = (byte)(bytesPerIndexRecord / IndexRecord.BytesPerIndexRecordBlock);
            }
        }

        public static List<FileNameRecord> GenerateFileNameRecords(MftSegmentReference parentDirectory, string fileName, bool isDirectory, bool generateDosName, IndexData parentDirectoryIndex)
        {
            DateTime creationTime = DateTime.Now;
            return GenerateFileNameRecords(parentDirectory, fileName, isDirectory, generateDosName, parentDirectoryIndex, creationTime, creationTime, creationTime, creationTime, 0, 0, 0, 0);
        }

        public static List<FileNameRecord> GenerateFileNameRecords(MftSegmentReference parentDirectory, string fileName, bool isDirectory, bool generateDosName, IndexData parentDirectoryIndex, DateTime creationTime, DateTime modificationTime, DateTime mftModificationTime, DateTime lastAccessTime, ulong allocatedLength, ulong fileSize, FileAttributes fileAttributes, ushort packedEASize)
        {
            FileNameRecord fileNameRecord = new FileNameRecord(parentDirectory, fileName, isDirectory, creationTime);
            fileNameRecord.ParentDirectory = parentDirectory;
            fileNameRecord.CreationTime = creationTime;
            fileNameRecord.ModificationTime = modificationTime;
            fileNameRecord.MftModificationTime = mftModificationTime;
            fileNameRecord.LastAccessTime = lastAccessTime;
            fileNameRecord.AllocatedLength = allocatedLength;
            fileNameRecord.FileSize = fileSize;
            fileNameRecord.FileAttributes = fileAttributes;
            fileNameRecord.PackedEASize = packedEASize;
            fileNameRecord.IsDirectory = isDirectory;
            fileNameRecord.FileName = fileName;
            bool createDosOnlyRecord = false;
            if (generateDosName)
            {
                fileNameRecord.Flags = FileNameFlags.Win32;
                if (DosFileNameHelper.IsValidDosFileName(fileName))
                {
                    fileNameRecord.Flags |= FileNameFlags.DOS;
                }
                else
                {
                    createDosOnlyRecord = true;
                }
            }
            else
            {
                // This is similar to Windows 8.1, When FileNameFlags.Win32 is set, Windows Server 2003's CHKDSK expects to find a record with FileNameFlags.DOS set.
                fileNameRecord.Flags = FileNameFlags.POSIX;
            }

            List<FileNameRecord> result = new List<FileNameRecord>();
            result.Add(fileNameRecord);
            if (createDosOnlyRecord)
            {
                string dosFileName = DosFileNameHelper.GenerateDosName(parentDirectoryIndex, fileName);
                FileNameRecord dosOnlyRecord = new FileNameRecord(parentDirectory, dosFileName, isDirectory, creationTime);
                dosOnlyRecord.Flags = FileNameFlags.DOS;
                result.Add(dosOnlyRecord);
            }

            return result;
        }
    }
}
