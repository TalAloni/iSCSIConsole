/* Copyright (C) 2014-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using DiskAccessLibrary.FileSystems.Abstractions;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// Adapter providing FileSystem implementation for NTFS (using NTFSVolume).
    /// </summary>
    public class NTFSFileSystem : FileSystem, IExtendableFileSystem
    {
        private NTFSVolume m_volume;
        private Dictionary<long, List<NTFSFileStream>> m_openStreams = new Dictionary<long, List<NTFSFileStream>>();

        public NTFSFileSystem(Volume volume, bool isReadOnly)
        {
            m_volume = new NTFSVolume(volume, isReadOnly);
        }

        public NTFSFileSystem(NTFSVolume volume)
        {
            m_volume = volume;
        }

        public override FileSystemEntry GetEntry(string path)
        {
            string streamName = GetStreamName(path);
            path = GetFilePath(path);
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            if (streamName != String.Empty && fileRecord.GetAttributeRecord(AttributeType.Data, streamName) == null)
            {
                throw new FileNotFoundException(String.Format("The file '{0}' does not contain a stream named '{1}'", path, streamName));
            }
            return ToFileSystemEntry(path, fileRecord);
        }

        public override FileSystemEntry CreateFile(string path)
        {
            string streamName = GetStreamName(path);
            path = GetFilePath(path);
            string parentDirectoryName = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            FileRecord parentDirectoryRecord = m_volume.GetFileRecord(parentDirectoryName);
            FileRecord fileRecord = null;
            if (streamName == String.Empty)
            {
                fileRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, fileName, false);
            }
            else
            {
                try
                {
                    fileRecord = m_volume.GetFileRecord(path);
                }
                catch (FileNotFoundException)
                {
                }

                if (fileRecord == null)
                {
                    fileRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, fileName, false);
                }
                else
                {
                    // We might need to allocate an additional FileRecordSegment so we have to make sure we can extend the MFT if it is full
                    if (m_volume.NumberOfFreeClusters < m_volume.NumberOfClustersRequiredToExtendMft)
                    {
                        throw new DiskFullException();
                    }
                }
                fileRecord.CreateAttributeRecord(AttributeType.Data, streamName);
                m_volume.UpdateFileRecord(fileRecord);
            }
            return ToFileSystemEntry(path, fileRecord);
        }

        public override FileSystemEntry CreateDirectory(string path)
        {
            string parentDirectoryName = Path.GetDirectoryName(path);
            string directoryName = Path.GetFileName(path);
            FileRecord parentDirectoryRecord = m_volume.GetFileRecord(parentDirectoryName);
            FileRecord directoryRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, directoryName, true);
            return ToFileSystemEntry(path, directoryRecord);
        }

        public override void Move(string source, string destination)
        {
            FileRecord sourceFileRecord = m_volume.GetFileRecord(source);
            string destinationDirectory = Path.GetDirectoryName(destination);
            string destinationFileName = Path.GetFileName(destination);
            FileRecord destinationDirectoryFileRecord = m_volume.GetFileRecord(destinationDirectory);
            m_volume.MoveFile(sourceFileRecord, destinationDirectoryFileRecord.BaseSegmentReference, destinationFileName);
        }

        public override void Delete(string path)
        {
            string streamName = GetStreamName(path);
            path = GetFilePath(path);
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            if (streamName == String.Empty)
            {
                m_volume.DeleteFile(fileRecord);
            }
            else
            {
                // We only delete the named stream
                AttributeRecord dataRecord = fileRecord.GetAttributeRecord(AttributeType.Data, streamName);
                if (dataRecord == null)
                {
                    throw new FileNotFoundException(String.Format("The file '{0}' does not contain a stream named '{1}'", path, streamName));
                }
                AttributeData attributeData = new AttributeData(m_volume, fileRecord, dataRecord);
                attributeData.Truncate(0);
                fileRecord.RemoveAttributeRecord(AttributeType.Data, streamName);
                m_volume.UpdateFileRecord(fileRecord);
            }
        }

        public override List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            FileRecord directoryRecord = m_volume.GetFileRecord(path);
            if (!directoryRecord.IsDirectory)
            {
                throw new InvalidPathException(String.Format("'{0}' is not a directory", path));
            }

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

        public override List<KeyValuePair<string, ulong>> ListDataStreams(string path)
        {
            List<KeyValuePair<string, ulong>> result = new List<KeyValuePair<string, ulong>>();
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            foreach (AttributeRecord attribute in fileRecord.Attributes)
            {
                if (attribute.AttributeType == AttributeType.Data)
                {
                    string streamName = String.Format(":{0}:$DATA", attribute.Name);
                    result.Add(new KeyValuePair<string, ulong>(streamName, attribute.DataLength));
                }
            }
            return result;
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share, FileOptions options)
        {
            string streamName = GetStreamName(path);
            path = GetFilePath(path);
            FileRecord fileRecord = null;
            AttributeRecord dataRecord = null;
            if (mode == FileMode.CreateNew || mode == FileMode.Create || mode == FileMode.OpenOrCreate)
            {
                bool fileExists = false;
                try
                {
                    fileRecord = m_volume.GetFileRecord(path);
                    fileExists = true;
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }

                if (fileExists)
                {
                    if (mode == FileMode.CreateNew)
                    {
                        if (streamName == String.Empty)
                        {
                            throw new AlreadyExistsException();
                        }
                        else
                        {
                            dataRecord = fileRecord.GetAttributeRecord(AttributeType.Data, streamName);
                            if (dataRecord != null)
                            {
                                throw new AlreadyExistsException();
                            }
                        }
                    }

                    if (streamName != String.Empty && dataRecord == null)
                    {
                        // We might need to allocate an additional FileRecordSegment so we have to make sure we can extend the MFT if it is full
                        if (m_volume.NumberOfFreeClusters < m_volume.NumberOfClustersRequiredToExtendMft)
                        {
                            throw new DiskFullException();
                        }
                        fileRecord.CreateAttributeRecord(AttributeType.Data, streamName);
                        m_volume.UpdateFileRecord(fileRecord);
                    }
                    
                    if (mode == FileMode.Create)
                    {
                        mode = FileMode.Truncate;
                    }
                }
                else
                {
                    string directoryPath = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileName(path);
                    FileRecord directoryRecord = m_volume.GetFileRecord(directoryPath);
                    fileRecord = m_volume.CreateFile(directoryRecord.BaseSegmentReference, fileName, false);
                    if (streamName != String.Empty)
                    {
                        fileRecord.CreateAttributeRecord(AttributeType.Data, streamName);
                        m_volume.UpdateFileRecord(fileRecord);
                    }
                }
            }
            else // Open, Truncate or Append
            {
                fileRecord = m_volume.GetFileRecord(path);
                dataRecord = fileRecord.GetAttributeRecord(AttributeType.Data, streamName);
                if (streamName != String.Empty && dataRecord == null)
                {
                    throw new FileNotFoundException(String.Format("The file '{0}' does not contain a stream named '{1}'", path, streamName));
                }
            }

            if (fileRecord.IsDirectory)
            {
                throw new UnauthorizedAccessException();
            }

            List<NTFSFileStream> openStreams;
            lock (m_openStreams)
            {
                if (m_openStreams.TryGetValue(fileRecord.BaseSegmentNumber, out openStreams))
                {
                    if ((access & FileAccess.Write) != 0)
                    {
                        // Currently we only support opening a file stream for write access if no other stream is opened for that file
                        throw new SharingViolationException();
                    }
                    else if ((access & FileAccess.Read) != 0)
                    {
                        foreach (NTFSFileStream openStream in openStreams)
                        {
                            if (openStream.CanWrite && ((share & FileShare.Write) == 0))
                            {
                                throw new SharingViolationException();
                            }
                        }
                    }
                }
                else
                {
                    openStreams = new List<NTFSFileStream>();
                    m_openStreams.Add(fileRecord.BaseSegmentNumber, openStreams);
                }
            }

            NTFSFile file = new NTFSFile(m_volume, fileRecord, streamName);
            NTFSFileStream stream = new NTFSFileStream(file, access);
            openStreams.Add(stream);
            stream.Closed += delegate(object sender, EventArgs e)
            {
                openStreams.Remove(stream);
                if (openStreams.Count == 0)
                {
                    lock (m_openStreams)
                    {
                        m_openStreams.Remove(fileRecord.BaseSegmentNumber);
                    }
                }
            };

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
            // The dates and FileAttributes stored in $Standard_Information are accessible to user-level processes,
            // while the ones in $File_Name are maintained internally and not updated often.
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            if (isHidden.HasValue)
            {
                if (isHidden.Value)
                {
                    fileRecord.StandardInformation.FileAttributes |= FileAttributes.Hidden;
                }
                else
                {
                    fileRecord.StandardInformation.FileAttributes &= ~FileAttributes.Hidden;
                }
            }

            if (isReadonly.HasValue)
            {
                if (isReadonly.Value)
                {
                    fileRecord.StandardInformation.FileAttributes |= FileAttributes.Readonly;
                }
                else
                {
                    fileRecord.StandardInformation.FileAttributes &= ~FileAttributes.Readonly;
                }
            }

            if (isArchived.HasValue)
            {
                if (isArchived.Value)
                {
                    fileRecord.StandardInformation.FileAttributes |= FileAttributes.Archive;
                }
                else
                {
                    fileRecord.StandardInformation.FileAttributes &= ~FileAttributes.Archive;
                }
            }

            fileRecord.StandardInformation.MftModificationTime = DateTime.Now;
            m_volume.UpdateFileRecord(fileRecord);
        }

        public override void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            // The dates and FileAttributes stored in $Standard_Information are accessible to user-level processes,
            // while the ones in $File_Name are maintained internally and not updated often.
            // http://cyberforensicator.com/2018/03/25/windows-10-time-rules/
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            if (creationDT.HasValue)
            {
                fileRecord.StandardInformation.CreationTime = creationDT.Value;
            }

            if (lastWriteDT.HasValue)
            {
                fileRecord.StandardInformation.ModificationTime = lastWriteDT.Value;
            }

            if (lastAccessDT.HasValue)
            {
                fileRecord.StandardInformation.LastAccessTime = lastAccessDT.Value;
            }

            fileRecord.StandardInformation.MftModificationTime = DateTime.Now;

            List<FileNameRecord> fileNameRecords = fileRecord.FileNameRecords;
            foreach(FileNameRecord fileNameRecord in fileNameRecords)
            {
                if (creationDT.HasValue)
                {
                    fileNameRecord.CreationTime = creationDT.Value;
                }

                if (lastWriteDT.HasValue)
                {
                    fileNameRecord.ModificationTime = lastWriteDT.Value;
                }

                if (lastAccessDT.HasValue)
                {
                    fileNameRecord.LastAccessTime = lastAccessDT.Value;
                }

                fileNameRecord.MftModificationTime = DateTime.Now;
            }
            m_volume.UpdateFileRecord(fileRecord);
            if (!fileRecord.IsDirectory)
            {
                // Windows NTFS v5.1 driver does not usually update the value of the FileSize field belonging
                // to the FileNameRecords that are stored in the FileRecord, it is likely to be 0.
                foreach (FileNameRecord fileNameRecord in fileNameRecords)
                {
                    fileNameRecord.FileSize = fileRecord.DataRecord.DataLength;
                }
            }
            m_volume.UpdateDirectoryIndex(fileRecord.ParentDirectoryReference, fileNameRecords);
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
                return true;
            }
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileRecord fileRecord)
        {
            // Windows will not update the dates and FileAttributes in $File_Name as often as their counterparts in $STANDARD_INFORMATION.
            ulong size = fileRecord.IsDirectory ? 0 : fileRecord.DataRecord.DataLength;
            FileAttributes attributes = fileRecord.StandardInformation.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, fileRecord.FileName, fileRecord.IsDirectory, size, fileRecord.StandardInformation.CreationTime, fileRecord.StandardInformation.ModificationTime, fileRecord.StandardInformation.LastAccessTime, isHidden, isReadonly, isArchived);
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileNameRecord fileNameRecord)
        {
            ulong size = fileNameRecord.FileSize;
            bool isDirectory = fileNameRecord.IsDirectory;
            FileAttributes attributes = fileNameRecord.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, fileNameRecord.FileName, isDirectory, size, fileNameRecord.CreationTime, fileNameRecord.ModificationTime, fileNameRecord.LastAccessTime, isHidden, isReadonly, isArchived);
        }

        /// <summary>
        /// Removes the stream name from the path
        /// </summary>
        public static string GetFilePath(string path)
        {
            int index = path.IndexOf(':');
            if (index >= 0)
            {
                path = path.Substring(0, index);
            }
            return path;
        }

        public static string GetStreamName(string path)
        {
            int index = path.IndexOf(':');
            if (index >= 0)
            {
                string streamName = path.Substring(index + 1);
                if (streamName.EndsWith(":$DATA"))
                {
                    streamName = streamName.Substring(0, streamName.Length - 6);
                }

                if (streamName.Contains(":"))
                {
                    throw new InvalidPathException("Invalid stream name");
                }
                return streamName;
            }
            return String.Empty;
        }
    }
}
