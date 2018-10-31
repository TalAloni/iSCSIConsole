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
    /// Adapter providing FileSystem implementation for NTFS (using NTFSVolume).
    /// </summary>
    public class NTFSFileSystem : FileSystem, IExtendableFileSystem
    {
        private NTFSVolume m_volume;

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
            string parentDirectoryName = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            FileRecord parentDirectoryRecord = m_volume.GetFileRecord(parentDirectoryName);
            if (parentDirectoryRecord != null)
            {
                FileRecord fileRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, fileName, false);
                return ToFileSystemEntry(path, fileRecord);
            }
            else
            {
                throw new DirectoryNotFoundException();
            }
        }

        public override FileSystemEntry CreateDirectory(string path)
        {
            string parentDirectoryName = Path.GetDirectoryName(path);
            string directoryName = Path.GetFileName(path);
            FileRecord parentDirectoryRecord = m_volume.GetFileRecord(parentDirectoryName);
            if (parentDirectoryRecord != null)
            {
                FileRecord directoryRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, directoryName, true);
                return ToFileSystemEntry(path, directoryRecord);
            }
            else
            {
                throw new DirectoryNotFoundException();
            }
        }

        public override void Move(string source, string destination)
        {
            FileRecord sourceFileRecord = m_volume.GetFileRecord(source);
            if (sourceFileRecord == null)
            {
                throw new FileNotFoundException();
            }

            string destinationDirectory = Path.GetDirectoryName(destination);
            string destinationFileName = Path.GetFileName(destination);
            FileRecord destinationDirectoryFileRecord = m_volume.GetFileRecord(destinationDirectory);
            if (destinationDirectoryFileRecord == null)
            {
                throw new DirectoryNotFoundException();
            }

            m_volume.MoveFile(sourceFileRecord, destinationDirectoryFileRecord.BaseSegmentReference, destinationFileName);
        }

        public override void Delete(string path)
        {
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            if (fileRecord != null)
            {
                if (fileRecord.IsDirectory)
                {
                    IndexData directoryIndex = new IndexData(m_volume, fileRecord, AttributeType.FileName);
                    if (!directoryIndex.IsEmpty)
                    {
                        throw new DirectoryNotEmptyException();
                    }
                }
                m_volume.DeleteFile(fileRecord);
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public override List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            FileRecord directoryRecord = m_volume.GetFileRecord(path);
            if (directoryRecord != null && directoryRecord.IsDirectory)
            {
                KeyValuePairList<MftSegmentReference, FileNameRecord> records = m_volume.GetFileNameRecordsInDirectory(directoryRecord.BaseSegmentReference);
                List<FileSystemEntry> result = new List<FileSystemEntry>();

                path = FileSystem.GetDirectoryPath(path);

                foreach (FileNameRecord record in records.Values)
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
            FileRecord record;
            if (mode == FileMode.CreateNew)
            {
                record = m_volume.GetFileRecord(path);
                if (record != null)
                {
                    throw new AlreadyExistsException();
                }
            }

            if (mode == FileMode.CreateNew || mode == FileMode.Create || mode == FileMode.OpenOrCreate)
            {
                record = m_volume.GetFileRecord(path);
                if (record == null)
                {
                    string directoryPath = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileName(path);
                    FileRecord directoryRecord = m_volume.GetFileRecord(directoryPath);
                    if (directoryRecord == null)
                    {
                        throw new DirectoryNotFoundException();
                    }
                    record = m_volume.CreateFile(directoryRecord.BaseSegmentReference, fileName, false);
                }
                else if (mode == FileMode.Create)
                {
                    mode = FileMode.Truncate;
                }
            }
            else // Open, Truncate or Append
            {
                record = m_volume.GetFileRecord(path);
                if (record == null)
                {
                    throw new FileNotFoundException();
                }
            }

            if (record.IsDirectory)
            {
                throw new UnauthorizedAccessException();
            }

            NTFSFile file = new NTFSFile(m_volume, record);
            NTFSFileStream stream = new NTFSFileStream(file);

            if (mode == FileMode.Truncate)
            {
                stream.SetLength(0);
            }
            else if (mode == FileMode.Append)
            {
                stream.Seek((long)file.Length, SeekOrigin.Begin);
            }
            return stream;
        }

        public override void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            if (record != null)
            {
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

                record.StandardInformation.MftModificationTime = DateTime.Now;
                m_volume.UpdateFileRecord(record);
            }
        }

        public override void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            if (record != null)
            {
                if (creationDT.HasValue)
                {
                    record.StandardInformation.CreationTime = creationDT.Value;
                    record.FileNameRecord.CreationTime = creationDT.Value;
                }

                if (lastWriteDT.HasValue)
                {
                    record.StandardInformation.ModificationTime = lastWriteDT.Value;
                    record.FileNameRecord.ModificationTime = lastWriteDT.Value;
                }

                if (lastAccessDT.HasValue)
                {
                    record.StandardInformation.LastAccessTime = lastAccessDT.Value;
                    record.FileNameRecord.LastAccessTime = lastAccessDT.Value;
                }

                record.StandardInformation.MftModificationTime = DateTime.Now;
                record.FileNameRecord.MftModificationTime = DateTime.Now;
                m_volume.UpdateFileRecord(record);
            }
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

        public override bool SupportsNamedStreams
        {
            get
            {
                return false;
            }
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileRecord record)
        {
            ulong size = record.IsDirectory ? 0 : record.DataRecord.DataLength;
            FileAttributes attributes = record.StandardInformation.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, record.FileName, record.IsDirectory, size, record.FileNameRecord.CreationTime, record.FileNameRecord.ModificationTime, record.FileNameRecord.LastAccessTime, isHidden, isReadonly, isArchived);
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileNameRecord record)
        {
            ulong size = record.FileSize;
            bool isDirectory = record.IsDirectory;
            FileAttributes attributes = record.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, record.FileName, isDirectory, size, record.CreationTime, record.ModificationTime, record.LastAccessTime, isHidden, isReadonly, isArchived);
        }
    }
}
