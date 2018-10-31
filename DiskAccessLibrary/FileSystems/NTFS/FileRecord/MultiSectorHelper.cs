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

        /// <remarks>
        /// The USN will be written at the end of each 512-byte stride, even if the device has more (or less) than 512 bytes per sector.
        /// https://docs.microsoft.com/en-us/windows/desktop/DevNotes/multi-sector-header
        /// </remarks>
        public static void RevertUsaProtection(byte[] buffer, int offset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            int position = offset + multiSectorHeader.UpdateSequenceArrayOffset;
            uint updateSequenceNumber = LittleEndianReader.ReadUInt16(buffer, ref position);

            // First do validation check - make sure the USN matches on all sectors)
            for (int index = 0; index < multiSectorHeader.UpdateSequenceArraySize - 1; ++index)
            {
                if (updateSequenceNumber != LittleEndianConverter.ToUInt16(buffer, offset + (BytesPerStride * (index + 1)) - 2))
                {
                    throw new InvalidDataException("Corrupt multi-sector transfer, USN does not match MultiSectorHeader");
                }
            }

            for (int index = 0; index < multiSectorHeader.UpdateSequenceArraySize - 1; index++)
            {
                byte[] endOfSectorBytes = ByteReader.ReadBytes(buffer, ref position, 2);
                ByteWriter.WriteBytes(buffer, offset + (BytesPerStride * (index + 1)) - 2, endOfSectorBytes);
            }
        }

        public static void ApplyUsaProtection(byte[] buffer, int offset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            int position = offset + multiSectorHeader.UpdateSequenceArrayOffset;
            ushort updateSequenceNumber = LittleEndianReader.ReadUInt16(buffer, ref position);

            for (int index = 0; index < multiSectorHeader.UpdateSequenceArraySize - 1; index++)
            {
                // Read in the bytes that are replaced by the USN
                byte[] endOfSectorBytes = ByteReader.ReadBytes(buffer, offset + (BytesPerStride * (index + 1)) - 2, 2);
                // Relocate the bytes that are replaced by the USN
                ByteWriter.WriteBytes(buffer, ref position, endOfSectorBytes);
                // Write the USN
                LittleEndianWriter.WriteUInt16(buffer, offset + (BytesPerStride * (index + 1)) - 2, updateSequenceNumber);
            }
        }
    }
}
