/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class MultiSectorHelper
    {
        public const int BytesPerStride = 512;

        public static void DecodeSegmentBuffer(byte[] buffer, int offset, ushort updateSequenceNumber, List<byte[]> updateSequenceReplacementData)
        {
            // The USN will be written at the end of each 512-byte stride, even if the device has more (or less) than 512 bytes per sector.
            // http://msdn.microsoft.com/en-us/library/bb470212%28v=vs.85%29.aspx
            
            // First do validation check - make sure the USN matches on all sectors)
            for (int i = 0; i < updateSequenceReplacementData.Count; ++i)
            {
                if (updateSequenceNumber != LittleEndianConverter.ToUInt16(buffer, offset + (BytesPerStride * (i + 1)) - 2))
                {
                    throw new InvalidDataException("Corrupt file system record found");
                }
            }

            // Now replace the USNs with the actual data from the UpdateSequenceReplacementData array
            for (int i = 0; i < updateSequenceReplacementData.Count; ++i)
            {
                Array.Copy(updateSequenceReplacementData[i], 0, buffer, offset + (BytesPerStride * (i + 1)) - 2, 2);
            }
        }

        public static List<byte[]> EncodeSegmentBuffer(byte[] buffer, int offset, int segmentLength, ushort updateSequenceNumber)
        {
            int numberOfStrides = segmentLength / BytesPerStride;
            List<byte[]> updateSequenceReplacementData = new List<byte[]>();

            // Read in the bytes that are replaced by the USN
            for (int i = 0; i < numberOfStrides; i++)
            {
                byte[] endOfSectorBytes = ByteReader.ReadBytes(buffer, offset + (BytesPerStride * (i + 1)) - 2, 2);
                updateSequenceReplacementData.Add(endOfSectorBytes);
            }

            // Overwrite the bytes that are replaced with the USN
            for (int i = 0; i < updateSequenceReplacementData.Count; i++)
            {
                LittleEndianWriter.WriteUInt16(buffer, offset + (BytesPerStride * (i + 1)) - 2, updateSequenceNumber);
            }

            return updateSequenceReplacementData;
        }
    }
}
