/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using DiskAccessLibrary;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// Adapter providing FileSystem implementation for NTFS (using NTFSVolume).
    /// </summary>
    public class NTFSFileSystem : FileSystem, IExtendableFileSystem
    {
        NTFSVolume m_volume;

        public NTFSFileSystem(Volume volume)
        {
            m_volume = new NTFSVolume(volume);
        }

        public NTFSFileSystem(NTFSVolume volume)
        {
            m_volume = volume;
        }

        public override FileSystemEntry GetEntry(string path)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            if (record != null)
            {
                return ToFileSystemEntry(path, record);
            }
            else
            {
                return null;
            }
        }

        public override FileSystemEntry CreateFile(string path)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override FileSystemEntry CreateDirectory(string path)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override void Move(string source, string destination)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override void Delete(string path)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            FileRecord directoryRecord = m_volume.GetFileRecord(path);
            if (directoryRecord != null && directoryRecord.IsDirectory)
            {
                List<FileRecord> records = m_volume.GetFileRecordsInDirectory(directoryRecord.MftSegmentNumber);
                List<FileSystemEntry> result = new List<FileSystemEntry>();

                path = FileSystem.GetDirectoryPath(path);

                foreach (FileRecord record in records)
                {
                    string fullPath = path + record.FileName;
                    FileSystemEntry entry = ToFileSystemEntry(fullPath, record);
                    result.Add(entry);
                }
                return result;
            }
            else
            {
                return null;
            }
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share, FileOptions options)
        {
            if (mode == FileMode.Open || mode == FileMode.Truncate)
            {
                FileRecord record = m_volume.GetFileRecord(path);
                if (record != null && !record.IsDirectory)
                {
                    NTFSFile file = new NTFSFile(m_volume, record);
                    NTFSFileStream stream = new NTFSFileStream(file);

                    if (mode == FileMode.Truncate)
                    {
                        stream.SetLength(0);
                    }
                    return stream;
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            if (isHidden.HasValue)
            {
                if (isHidden.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Hidden;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Hidden;
                }
            }

            if (isReadonly.HasValue)
            {
                if (isReadonly.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Readonly;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Readonly;
                }
            }

            if (isArchived.HasValue)
            {
                if (isArchived.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Archive;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Archive;
                }
            }

            m_volume.MasterFileTable.UpdateFileRecord(record);
        }

        public override void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public long GetMaximumSizeToExtend()
        {
            return m_volume.GetMaximumSizeToExtend();
        }

        public void Extend(long numberOfAdditionalSectors)
        {
            m_volume.Extend(numberOfAdditionalSectors);
        }

        public override string ToString()
        {
            return m_volume.ToString();
        }

        public override string Name
        {
            get
            {
                return "NTFS";
            }
        }

        public override long Size
        {
            get
            {
                return m_volume.Size;
            }
        }

        public override long FreeSpace
        {
            get
            {
                return m_volume.FreeSpace;
            }
        }

        public bool IsValidAndSupported
        {
            get
            {
                return m_volume.IsValidAndSupported;
            }
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileRecord record)
        {
            ulong size = record.IsDirectory ? 0 : record.DataRecord.DataRealSize;
            FileAttributes attributes = record.StandardInformation.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, record.FileName, record.IsDirectory, size, record.FileNameRecord.CreationTime, record.FileNameRecord.ModificationTime, record.FileNameRecord.LastAccessTime, isHidden, isReadonly, isArchived);
        }
    }
}
