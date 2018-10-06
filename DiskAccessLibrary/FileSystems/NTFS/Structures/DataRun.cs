/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// The maximum NTFS file size is 2^64 bytes, so total number of file clusters can be represented using long
    /// https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-2000-server/cc938937(v=technet.10)
    /// </remarks>
    public class DataRun
    {
        public long RunLength; // In clusters
        public long RunOffset; // In clusters, relative to previous data run start LCN
        public bool IsSparse;
        private int m_recordLengthOnDisk;

        public DataRun()
        {
        }

        public DataRun(long runLength, long runOffset)
        {
            RunLength = runLength;
            RunOffset = runOffset;
        }

        public DataRun(byte[] buffer, int offset)
        {
            int runOffsetSize = buffer[offset] >> 4;
            int runLengthSize = buffer[offset] & 0x0F;

            RunLength = ReadVarLong(ref buffer, offset + 1, runLengthSize);
            if (RunLength < 0)
            {
                throw new InvalidDataException("Invalid Data Run record");
            }
            RunOffset = ReadVarLong(ref buffer, offset + 1 + runLengthSize, runOffsetSize);
            IsSparse = (runOffsetSize == 0);
            m_recordLengthOnDisk = 1 + runLengthSize + runOffsetSize;
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            if (IsSparse)
            {
                RunOffset = 0;
            }

            int runLengthSize = WriteVarLong(buffer, offset + 1, RunLength);
            int runOffsetSize;
            if (IsSparse)
            {
                runOffsetSize = 0;
            }
            else
            {
                runOffsetSize = WriteVarLong(buffer, offset + 1 + runLengthSize, RunOffset);
            }

            buffer[offset + 0] = (byte)((runLengthSize & 0x0F) | ((runOffsetSize << 4) & 0xF0));
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[RecordLength];
            WriteBytes(buffer, 0);
            return buffer;
        }

        private static long ReadVarLong(ref byte[] buffer, int offset, int size)
        {
            ulong val = 0;
            bool signExtend = false;

            for (int i = 0; i < size; ++i)
            {
                byte b = buffer[offset + i];
                val = val | (((ulong)b) << (i * 8));
                signExtend = (b & 0x80) != 0;
            }

            if (signExtend)
            {
                for (int i = size; i < 8; ++i)
                {
                    val = val | (((ulong)0xFF) << (i * 8));
                }
            }

            return (long)val;
        }

        private static int WriteVarLong(byte[] buffer, int offset, long val)
        {
            bool isPositive = val >= 0;

            int pos = 0;
            do
            {
                buffer[offset + pos] = (byte)(val & 0xFF);
                val >>= 8;
                pos++;
            }
            while (val != 0 && val != -1);

            // Avoid appearing to have a negative number that is actually positive,
            // record an extra empty byte if needed.
            if (isPositive && (buffer[offset + pos - 1] & 0x80) != 0)
            {
                buffer[offset + pos] = 0;
                pos++;
            }
            else if (!isPositive && (buffer[offset + pos - 1] & 0x80) != 0x80)
            {
                buffer[offset + pos] = 0xFF;
                pos++;
            }

            return pos;
        }

        private static int VarLongSize(long val)
        {
            bool isPositive = val >= 0;
            bool lastByteHighBitSet = false;

            int len = 0;
            do
            {
                lastByteHighBitSet = (val & 0x80) != 0;
                val >>= 8;
                len++;
            }
            while (val != 0 && val != -1);

            if ((isPositive && lastByteHighBitSet) || (!isPositive && !lastByteHighBitSet))
            {
                len++;
            }

            return len;
        }

        /// <summary>
        /// Length of the DataRun record
        /// </summary>
        public int RecordLength
        {
            get
            {
                int runLengthSize = VarLongSize(RunLength);
                int runOffsetSize = VarLongSize(RunOffset);
                return 1 + runLengthSize + runOffsetSize;
            }
        }

        public int RecordLengthOnDisk
        {
            get
            {
                return m_recordLengthOnDisk;
            }
        }
    }
}
