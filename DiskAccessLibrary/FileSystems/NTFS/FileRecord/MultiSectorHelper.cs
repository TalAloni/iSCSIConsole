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
    public class MultiSectorHelper
    {
        public const int BytesPerStride = 512;

        /// <returns>
        /// An array of the missing 2 bytes from each stride (that have been replaced with replaced with an UpdateSequenceNumber)
        /// </returns>
        public static List<byte[]> ReadUpdateSequenceArray(byte[] buffer, int offset, ushort updateSequenceArraySize, out ushort updateSequenceNumber)
        {
            updateSequenceNumber = LittleEndianConverter.ToUInt16(buffer, offset);
            offset += 2;
            // This stores the data that was supposed to be placed at the end of each sector, and was replaced with an UpdateSequenceNumber
            List<byte[]> updateSequenceReplacementData = new List<byte[]>();
            for (int index = 0; index < updateSequenceArraySize - 1; index++)
            {
                byte[] endOfSectorBytes = new byte[2];
                endOfSectorBytes[0] = buffer[offset + 0];
                endOfSectorBytes[1] = buffer[offset + 1];
                updateSequenceReplacementData.Add(endOfSectorBytes);
                offset += 2;
            }

            return updateSequenceReplacementData;
        }

        public static void WriteUpdateSequenceArray(byte[] buffer, int offset, ushort updateSequenceArraySize, ushort updateSequenceNumber, List<byte[]> updateSequenceReplacementData)
        {
            LittleEndianWriter.WriteUInt16(buffer, offset, updateSequenceNumber);
            offset += 2;
            foreach (byte[] endOfSectorBytes in updateSequenceReplacementData)
            {
                ByteWriter.WriteBytes(buffer, offset, endOfSectorBytes);
                offset += 2;
            }
        }

        public static void DecodeSegmentBuffer(byte[] buffer, int offset, ushort updateSequenceNumber, List<byte[]> updateSequenceReplacementData)
        {
            // The USN will be written at the end of each 512-byte stride, even if the device has more (or less) than 512 bytes per sector.
            // https://docs.microsoft.com/en-us/windows/desktop/DevNotes/multi-sector-header
            
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
