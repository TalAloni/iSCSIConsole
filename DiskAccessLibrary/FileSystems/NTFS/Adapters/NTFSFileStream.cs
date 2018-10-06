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
    /// A Stream wrapper for NTFSFile
    /// </summary>
    public class NTFSFileStream : Stream
    {
        private NTFSFile m_file;
        private long m_position;

        public NTFSFileStream(NTFSFile file)
        {
            m_file = file;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                m_position = offset;
            }
            else if (origin == SeekOrigin.Current)
            {
                m_position += offset;
            }
            else if (origin == SeekOrigin.End)
            {
                m_position = (long)m_file.Length + offset;
            }

            return m_position;
        }

        public override void SetLength(long value)
        {
            m_file.SetLength((ulong)value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            byte[] data = m_file.ReadData((ulong)m_position, count);
            Array.Copy(data, 0, buffer, offset, data.Length);
            m_position += data.Length;
            return data.Length;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] data = new byte[count];
            Array.Copy(buffer, offset, data, 0, count);
            m_file.WriteData((ulong)m_position, data);
            m_position += count;
        }

        public override void Flush()
        {
            // Everything was written directly to disk, no need to flush
        }

        public override long Length
        {
            get
            {
                return (long)m_file.Length;
            }
        }

        public override long Position
        {
            get
            {
                return m_position;
            }
            set
            {
                m_position = value;
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }
    }
}
