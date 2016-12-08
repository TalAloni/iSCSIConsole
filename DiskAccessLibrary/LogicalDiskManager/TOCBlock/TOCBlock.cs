/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class TOCBlock
    {
        public const int Length = 512;
        public const string TOCBlockSignature = "TOCBLOCK";

        public string Signature = TOCBlockSignature;
        //private uint Checksum;                 // sum of all bytes in sector excluding the 4 checksum bytes
        public ulong UpdateSequenceNumber;    // The most recent TOCBlock has this value in sync with the Private Header's UpdateSequenceNumber
        // 16 zeros
        public List<TOCRegion> Regions = new List<TOCRegion>();

        private bool m_isChecksumValid;

        public TOCBlock(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 8);
            uint checksum = BigEndianConverter.ToUInt32(buffer, 0x08);
            UpdateSequenceNumber = BigEndianConverter.ToUInt64(buffer, 0x0C);
            // 16 zeros
            int offset = 0x24;

            // If the first character is not null (0x00), then there is a region defined
            while (buffer[offset] != 0)
            {
                TOCRegion region = new TOCRegion(buffer, offset);
                Regions.Add(region);
                offset += TOCRegion.Length;
            }

            BigEndianWriter.WriteUInt32(buffer, 0x08, (uint)0); // we exclude the checksum field from checksum calculations
            m_isChecksumValid = (checksum == PrivateHeader.CalculateChecksum(buffer));
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0x00, Signature, 8);
            // we'll write checksum later
            BigEndianWriter.WriteUInt64(buffer, 0x0C, UpdateSequenceNumber);
            int offset = 0x24;
            foreach (TOCRegion region in Regions)
            {
                region.WriteBytes(buffer, offset);
                offset += TOCRegion.Length;
            }

            uint checksum = PrivateHeader.CalculateChecksum(buffer);
            BigEndianWriter.WriteUInt32(buffer, 0x08, checksum);
            return buffer;
        }

        public static TOCBlock ReadFromDisk(Disk disk, PrivateHeader privateHeader)
        {
            TOCBlock tocBlock = ReadFromDisk(disk, privateHeader, true);
            if (tocBlock == null)
            {
                tocBlock = ReadFromDisk(disk, privateHeader, false);
            }
            return tocBlock;
        }

        public static TOCBlock ReadFromDisk(Disk disk, PrivateHeader privateHeader, bool usePrimaryTOC)
        {
            ulong sectorIndex;
            if (usePrimaryTOC)
            {
                sectorIndex = privateHeader.PrivateRegionStartLBA + privateHeader.PrimaryTocLBA;
            }
            else
            {
                sectorIndex = privateHeader.PrivateRegionStartLBA + privateHeader.SecondaryTocLBA;
            }

            byte[] sector = disk.ReadSector((long)sectorIndex);
            string signature = ByteReader.ReadAnsiString(sector, 0x00, 8);
            if (signature == TOCBlockSignature)
            {
                TOCBlock tocBlock = new TOCBlock(sector);
                if (tocBlock.IsChecksumValid)
                {
                    return tocBlock;
                }
            }

            return null;
        }

        /// <summary>
        /// This method will write privateHeader to disk as well
        /// </summary>
        public static void WriteToDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            privateHeader.UpdateSequenceNumber++;
            tocBlock.UpdateSequenceNumber++;
            byte[] bytes = tocBlock.GetBytes();
            disk.WriteSectors((long)(privateHeader.PrivateRegionStartLBA + privateHeader.PreviousPrimaryTocLBA), bytes);
            disk.WriteSectors((long)(privateHeader.PrivateRegionStartLBA + privateHeader.PreviousSecondaryTocLBA), bytes);
            privateHeader.PrimaryTocLBA = privateHeader.PreviousPrimaryTocLBA;
            privateHeader.SecondaryTocLBA = privateHeader.PreviousSecondaryTocLBA;
            PrivateHeader.WriteToDisk(disk, privateHeader);
        }

        public ulong ConfigStart
        {
            get
            {
                foreach (TOCRegion region in Regions)
                { 
                    if (region.Name == "config")
                    {
                        return region.StartLBA;
                    }
                }
                throw new InvalidDataException("Config entry was not found");
            }
        }

        public ulong LogStart
        {
            get
            {
                foreach (TOCRegion region in Regions)
                {
                    if (region.Name == "log")
                    {
                        return region.StartLBA;
                    }
                }
                throw new InvalidDataException("Log entry was not found");
            }
        }

        public bool IsChecksumValid
        {
            get
            {
                return m_isChecksumValid;
            }
        }
    }
}
